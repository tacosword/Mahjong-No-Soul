using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Complete rewrite of NetworkedGameManager for synchronized Mahjong gameplay.
/// Manages game flow, tile distribution, turn order, and bot players.
/// </summary>
public class NetworkedGameManager : NetworkBehaviour
{
    public static NetworkedGameManager Instance { get; private set; }

    [Header("Tile Setup")]
    [SerializeField] private GameObject[] tilePrefabs; // All 136 unique tile prefabs
    [SerializeField] private Transform wallContainer; // Optional parent for wall tiles
    
    [Header("Hand Position Containers")]
    [SerializeField] private Transform handPositionSeat0;
    [SerializeField] private Transform handPositionSeat1;
    [SerializeField] private Transform handPositionSeat2;
    [SerializeField] private Transform handPositionSeat3;
    
    [Header("Discard Position Containers")]
    [SerializeField] private Transform discardPositionSeat0;
    [SerializeField] private Transform discardPositionSeat1;
    [SerializeField] private Transform discardPositionSeat2;
    [SerializeField] private Transform discardPositionSeat3;
    
    [Header("Hand Layout Settings")]
    [SerializeField] private float tileSpacing = 1.2f;
    [SerializeField] private float drawnTileOffset = 1.8f;
    [SerializeField] private Quaternion handRotation = Quaternion.Euler(-45f, 0f, 0f);
    
    [Header("Discard Layout Settings")]
    [SerializeField] private float discardSpacing = 1.0f;
    [SerializeField] private int discardColumns = 6;
    [SerializeField] private Quaternion discardRotation = Quaternion.Euler(0f, 0f, 0f);

    [Header("Meld/Kong Area Containers")]
    [SerializeField] private Transform meldPositionSeat0;
    [SerializeField] private Transform meldPositionSeat1;
    [SerializeField] private Transform meldPositionSeat2;
    [SerializeField] private Transform meldPositionSeat3;
    
    [Header("Kong Layout Settings")]
    [SerializeField] private float meldTileSpacing = 0.12f;
    [SerializeField] private float meldSetSpacing = 0.05f;

    [Header("Player UI")]
    [SerializeField] private GameObject kongButtonUI;

    /// <summary>
    /// Returns the Kong button scene object. NetworkedPlayerHand retrieves it from here
    /// because NetworkedGameManager is a scene object and can hold scene references,
    /// while the player prefab cannot.
    /// </summary>
    public GameObject KongButtonUI => kongButtonUI;

    /// <summary>
    /// Called directly by the Kong Button's OnClick event in the Canvas.
    /// The button lives in the scene so it can reference this scene object (NetworkedGameManager),
    /// but it cannot reference the player prefab. This method bridges the gap by finding
    /// the local player's NetworkedPlayerHand and calling OnKongButtonPressed on it.
    /// 
    /// In Unity: select your Kong Button → Button component → OnClick (+) →
    ///   drag the NetworkedGameManager GameObject into the slot →
    ///   select NetworkedGameManager.OnKongButtonClicked
    /// </summary>
    public void OnKongButtonClicked()
    {
        if (NetworkClient.localPlayer == null) return;
        // Use GetComponentInChildren in case NetworkedPlayerHand is on a child of the player root
        NetworkedPlayerHand hand = NetworkClient.localPlayer.GetComponentInChildren<NetworkedPlayerHand>();
        if (hand != null)
        {
            hand.OnKongButtonPressed();
        }
        else
        {
            Debug.LogWarning("[GameManager] OnKongButtonClicked: could not find NetworkedPlayerHand on local player");
        }
    }

    // Game state
    [SyncVar(hook = nameof(OnCurrentPlayerIndexChanged))]
    private int currentPlayerIndex = 0; // Whose turn it is (0-3)
    
    [SyncVar]
    private int dealerSeat = 0; // Who is the dealer (East)
    
    [SyncVar]
    private bool gameInProgress = false;
    
    // Wall of tiles (136 tiles total)
    private readonly SyncList<int> wall = new SyncList<int>();
    
    // Track which tiles are in each player's hand (by network ID)
    private readonly SyncList<uint> seat0HandTiles = new SyncList<uint>();
    private readonly SyncList<uint> seat1HandTiles = new SyncList<uint>();
    private readonly SyncList<uint> seat2HandTiles = new SyncList<uint>();
    private readonly SyncList<uint> seat3HandTiles = new SyncList<uint>();
    
    // Track drawn tiles (separate from hand)
    // Hooks fire on ALL clients the moment the drawn tile SyncVar changes.
    // This is the most reliable Kong-check trigger: it fires exactly when the tile
    // arrives, with no ordering race against currentPlayerIndex.
    [SyncVar(hook = nameof(OnSeat0DrawnTileChanged))]
    private uint seat0DrawnTile = 0;
    [SyncVar(hook = nameof(OnSeat1DrawnTileChanged))]
    private uint seat1DrawnTile = 0;
    [SyncVar(hook = nameof(OnSeat2DrawnTileChanged))]
    private uint seat2DrawnTile = 0;
    [SyncVar(hook = nameof(OnSeat3DrawnTileChanged))]
    private uint seat3DrawnTile = 0;
    
    // Track discarded tiles for each seat
    private readonly SyncList<uint> seat0Discards = new SyncList<uint>();
    private readonly SyncList<uint> seat1Discards = new SyncList<uint>();
    private readonly SyncList<uint> seat2Discards = new SyncList<uint>();
    private readonly SyncList<uint> seat3Discards = new SyncList<uint>();
    
    // Track flower tiles for each seat (displayed in 2x4 grid)
    private readonly SyncList<uint> seat0Flowers = new SyncList<uint>();
    private readonly SyncList<uint> seat1Flowers = new SyncList<uint>();
    private readonly SyncList<uint> seat2Flowers = new SyncList<uint>();
    private readonly SyncList<uint> seat3Flowers = new SyncList<uint>();
    
    // Track meld tiles (Kongs, Pons, Chis) spawned in the meld area for each seat
    // These are stored as lists of netIds per meld set: each entry is a "set" of tiles
    private Dictionary<int, List<List<uint>>> seatMeldSets = new Dictionary<int, List<List<uint>>>();
    
    // Player management
    private readonly NetworkPlayer[] players = new NetworkPlayer[4];
    private readonly bool[] isBot = new bool[4]; // Track which seats are bots
    
    // All spawned tile game objects (for everyone to see)
    private readonly Dictionary<uint, GameObject> spawnedTiles = new Dictionary<uint, GameObject>();
    
    // Wall index (tracks which tile to draw next)
    private int wallIndex = 0;

    public int CurrentPlayerIndex => currentPlayerIndex;
    public bool GameInProgress => gameInProgress;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Initialize meld sets tracking
        for (int i = 0; i < 4; i++)
        {
            seatMeldSets[i] = new List<List<uint>>();
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[GameManager] Server started");
    }

    /// <summary>
    /// Called by NetworkLobbyManager when game starts
    /// </summary>
    [Server]
    public void InitializeGame(List<NetworkPlayer> connectedPlayers)
    {
        Debug.Log($"[GameManager] InitializeGame called with {connectedPlayers.Count} players");
        
        // Assign real players to seats
        for (int i = 0; i < connectedPlayers.Count && i < 4; i++)
        {
            players[i] = connectedPlayers[i];
            isBot[i] = false;
            
            // NetworkedPlayerHand should already be on the prefab
            // Just verify it exists
            NetworkedPlayerHand hand = players[i].GetComponent<NetworkedPlayerHand>();
            if (hand == null)
            {
                Debug.LogError($"[GameManager] Player {players[i].Username} is missing NetworkedPlayerHand component! Add it to the Player prefab.");
            }
            else
            {
                Debug.Log($"[GameManager] Player {players[i].Username} has NetworkedPlayerHand component");
            }
            
            Debug.Log($"[GameManager] Seat {i}: Real player {players[i].Username}");
        }
        
        // Fill remaining seats with bots
        for (int i = connectedPlayers.Count; i < 4; i++)
        {
            players[i] = null;
            isBot[i] = true;
            Debug.Log($"[GameManager] Seat {i}: Bot player");
        }
        
        // Start the first round
        StartNewRound();
    }

    [Server]
    public void StartNewRound()
    {
        Debug.Log("[GameManager] Starting new round");
        
        gameInProgress = true;
        currentPlayerIndex = dealerSeat;
        wallIndex = 0;
        
        // Clear all hands and discards
        ClearAllHands();
        
        // Build and shuffle wall
        BuildWall();
        ShuffleWall();
        
        // Deal initial tiles
        DealInitialHands();
        
        // Dealer (seat matching dealerSeat) gets 14 tiles, others get 13
        DrawTileForSeat(dealerSeat);
        
        // Reposition dealer's hand with the drawn tile
        PositionHandTiles(dealerSeat);

        // Notify all human seats to check for Kong opportunities in their starting hands.
        // Done via coroutine to wait for tile spawn messages to reach clients first.
        StartCoroutine(NotifySeatsAfterDeal());
        
        Debug.Log($"[GameManager] Round started. Dealer is seat {dealerSeat}, current turn: {currentPlayerIndex}");
    }

    [Server]
    private void BuildWall()
    {
        wall.Clear();
        
        // Standard Mahjong set: 136 tiles
        // Each suit (Circles, Bamboo, Characters) has tiles 1-9, 4 copies each = 108 tiles
        // Winds (East, South, West, North) 4 copies each = 16 tiles
        // Dragons (Red, Green, White) 4 copies each = 12 tiles
        // Total = 136 tiles
        
        // For simplicity, we'll use sort values directly
        // Circles: 200-208 (suit 2, values 0-8)
        // Bamboo: 300-308 (suit 3, values 0-8)
        // Characters: 100-108 (suit 1, values 0-8)
        // Winds: 400-403 (suit 4, values 0-3)
        // Dragons: 500-502 (suit 5, values 0-2)
        
        List<int> tempWall = new List<int>();
        
        // Add number tiles (1-9 in each suit, 4 copies)
        // NOTE: Mahjong tiles are numbered 1-9, not 0-8
        for (int suit = 1; suit <= 3; suit++)
        {
            for (int value = 1; value <= 9; value++)
            {
                int sortValue = (suit * 100) + value;
                for (int copy = 0; copy < 4; copy++)
                {
                    tempWall.Add(sortValue);
                }
            }
        }
        
        // Add winds (4 types: East=1, South=2, West=3, North=4, 4 copies each)
        for (int value = 1; value <= 4; value++)
        {
            int sortValue = 400 + value;
            for (int copy = 0; copy < 4; copy++)
            {
                tempWall.Add(sortValue);
            }
        }
        
        // Add dragons (3 types: White=1, Green=2, Red=3, 4 copies each)
        for (int value = 1; value <= 3; value++)
        {
            int sortValue = 500 + value;
            for (int copy = 0; copy < 4; copy++)
            {
                tempWall.Add(sortValue);
            }
        }
        
        // Add flower tiles (601-604 and 701-704, one of each = 8 total)
        for (int value = 601; value <= 604; value++)
        {
            tempWall.Add(value);
        }
        for (int value = 701; value <= 704; value++)
        {
            tempWall.Add(value);
        }
        
        // Add to sync list
        foreach (int tile in tempWall)
        {
            wall.Add(tile);
        }
        
        Debug.Log($"[GameManager] Wall built with {wall.Count} tiles (including 8 flowers)");
    }

    [Server]
    private void ShuffleWall()
    {
        System.Random rng = new System.Random();
        List<int> tempList = wall.ToList();
        
        int n = tempList.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            int value = tempList[k];
            tempList[k] = tempList[n];
            tempList[n] = value;
        }
        
        wall.Clear();
        foreach (int tile in tempList)
        {
            wall.Add(tile);
        }
        
        Debug.Log("[GameManager] Wall shuffled");
    }

    [Server]
    private void DealInitialHands()
    {
        Debug.Log("[GameManager] Dealing near-winning initial hands via HandGenerator");

        for (int seat = 0; seat < 4; seat++)
        {
            // Generate a guaranteed 14-tile winning hand, then remove one tile so the
            // seat starts with 13 tiles that are exactly one draw away from winning.
            List<int> winningHand = HandGenerator.GenerateRandomWinningHandSortValues();
            int waitingTile = HandGenerator.SelectOneTileToDiscard(winningHand);
            // winningHand now has exactly 13 sort values — the near-winning starting hand.
            List<int> startingHand = winningHand;

            Debug.Log($"[GameManager] Seat {seat}: dealing {startingHand.Count} tiles, waiting for {waitingTile}");

            SyncList<uint> hand = GetHandForSeat(seat);
            if (hand == null)
            {
                Debug.LogError($"[GameManager] DealInitialHands: null hand list for seat {seat}");
                continue;
            }

            // Spawn a networked tile object for each sort value and add it to the hand.
            foreach (int sortValue in startingHand)
            {
                SpawnTileIntoHand(seat, sortValue);
            }

            // Sort and position the finished hand.
            SortHandForSeat(seat);

            Debug.Log($"[GameManager] Seat {seat} ready: {hand.Count} tiles, waiting tile = {waitingTile}");
        }

        Debug.Log("[GameManager] Initial deal complete — all seats have 13-tile near-winning hands");
    }

    /// <summary>
    /// Spawns a single networked tile with the given sort value and adds it directly to
    /// the specified seat's hand list. Does NOT consume a tile from the wall.
    /// Used during the pre-built HandGenerator deal so the wall remains intact for
    /// normal turn-by-turn drawing.
    /// </summary>
    [Server]
    private void SpawnTileIntoHand(int seat, int sortValue)
    {
        GameObject tilePrefab = GetTilePrefab(sortValue);
        if (tilePrefab == null)
        {
            Debug.LogError($"[GameManager] SpawnTileIntoHand: no prefab found for sort value {sortValue} (seat {seat}). Skipping tile.");
            return;
        }

        GameObject tileObj = Instantiate(tilePrefab, Vector3.zero, Quaternion.identity);
        NetworkServer.Spawn(tileObj);

        uint netId = tileObj.GetComponent<NetworkIdentity>().netId;
        spawnedTiles[netId] = tileObj;

        SyncList<uint> hand = GetHandForSeat(seat);
        if (hand != null)
        {
            hand.Add(netId);
        }
    }

    [Server]
    private void DrawTileForSeat(int seatIndex)
    {
        if (wallIndex >= wall.Count)
        {
            Debug.LogError("[GameManager] No more tiles in wall!");
            return;
        }
        
        int tileValue = wall[wallIndex];
        wallIndex++;
        
        // Spawn the tile as a networked object
        GameObject tilePrefab = GetTilePrefab(tileValue);
        if (tilePrefab == null)
        {
            Debug.LogError($"[GameManager] No prefab found for tile value {tileValue}");
            return;
        }
        
        GameObject tileObj = Instantiate(tilePrefab, Vector3.zero, Quaternion.identity);
        NetworkServer.Spawn(tileObj);
        
        uint netId = tileObj.GetComponent<NetworkIdentity>().netId;
        spawnedTiles[netId] = tileObj;
        
        // Check if it's a flower
        if (IsFlowerTile(tileValue))
        {
            // Add to flower collection
            SyncList<uint> flowers = GetFlowersForSeat(seatIndex);
            if (flowers != null)
            {
                flowers.Add(netId);
                Debug.Log($"[GameManager] Seat {seatIndex} drew flower {tileValue} -> replacing");
                
                // Position flower
                PositionFlowerTiles(seatIndex);
                
                // Draw replacement tile (recursive)
                DrawTileForSeat(seatIndex);
            }
        }
        else
        {
            // Add normal tile to drawn slot
            SetDrawnTileForSeat(seatIndex, netId);
            Debug.Log($"[GameManager] Drew tile {tileValue} for seat {seatIndex} -> drawn slot");
        }
    }
    
    /// <summary>
    /// Draw tile directly to hand (for initial dealing)
    /// Handles flower tiles automatically
    /// </summary>
    [Server]
    private void DrawTileToHand(int seatIndex)
    {
        if (wallIndex >= wall.Count)
        {
            Debug.LogError("[GameManager] No more tiles in wall!");
            return;
        }
        
        int tileValue = wall[wallIndex];
        wallIndex++;
        
        // Spawn the tile
        GameObject tilePrefab = GetTilePrefab(tileValue);
        if (tilePrefab == null)
        {
            Debug.LogError($"[GameManager] No prefab found for tile value {tileValue}");
            return;
        }
        
        GameObject tileObj = Instantiate(tilePrefab, Vector3.zero, Quaternion.identity);
        NetworkServer.Spawn(tileObj);
        
        uint netId = tileObj.GetComponent<NetworkIdentity>().netId;
        spawnedTiles[netId] = tileObj;
        
        // Check if it's a flower
        if (IsFlowerTile(tileValue))
        {
            // Add to flower collection
            SyncList<uint> flowers = GetFlowersForSeat(seatIndex);
            if (flowers != null)
            {
                flowers.Add(netId);
                Debug.Log($"[GameManager] Seat {seatIndex} drew flower {tileValue} -> replacing");
                
                // Position flower
                PositionFlowerTiles(seatIndex);
                
                // Draw replacement tile (recursive)
                DrawTileToHand(seatIndex);
            }
        }
        else
        {
            // Add normal tile to hand
            SyncList<uint> hand = GetHandForSeat(seatIndex);
            if (hand != null)
            {
                hand.Add(netId);
            }
        }
    }

    /// <summary>
    /// Check if a tile is a flower (601-604 or 701-704)
    /// </summary>
    private bool IsFlowerTile(int sortValue)
    {
        return (sortValue >= 601 && sortValue <= 604) || (sortValue >= 701 && sortValue <= 704);
    }

    /// <summary>
    /// Get flower list for a seat
    /// </summary>
    private SyncList<uint> GetFlowersForSeat(int seat)
    {
        switch (seat)
        {
            case 0: return seat0Flowers;
            case 1: return seat1Flowers;
            case 2: return seat2Flowers;
            case 3: return seat3Flowers;
            default: return null;
        }
    }

    [Server]
    private GameObject GetTilePrefab(int sortValue)
    {
        // Find the prefab that matches this sort value
        foreach (GameObject prefab in tilePrefabs)
        {
            TileData data = prefab.GetComponent<TileData>();
            if (data != null && data.GetSortValue() == sortValue)
            {
                return prefab;
            }
        }
        return null;
    }

    [Server]
    private void SetDrawnTileForSeat(int seat, uint netId)
    {
        switch (seat)
        {
            case 0: seat0DrawnTile = netId; break;
            case 1: seat1DrawnTile = netId; break;
            case 2: seat2DrawnTile = netId; break;
            case 3: seat3DrawnTile = netId; break;
        }
    }

    private uint GetDrawnTileForSeat(int seat)
    {
        switch (seat)
        {
            case 0: return seat0DrawnTile;
            case 1: return seat1DrawnTile;
            case 2: return seat2DrawnTile;
            case 3: return seat3DrawnTile;
            default: return 0;
        }
    }

    [Server]
    private void SortHandForSeat(int seat)
    {
        SyncList<uint> hand = GetHandForSeat(seat);
        if (hand == null) return;
        
        // Get all tiles and sort by value
        List<(uint netId, int sortValue)> tiles = new List<(uint, int)>();
        
        foreach (uint netId in hand)
        {
            if (spawnedTiles.TryGetValue(netId, out GameObject tileObj))
            {
                TileData data = tileObj.GetComponent<TileData>();
                if (data != null)
                {
                    tiles.Add((netId, data.GetSortValue()));
                }
            }
        }
        
        // Sort by sort value
        tiles.Sort((a, b) => a.sortValue.CompareTo(b.sortValue));
        
        // Rebuild the hand list
        hand.Clear();
        foreach (var tile in tiles)
        {
            hand.Add(tile.netId);
        }
        
        // Position tiles visually
        PositionHandTiles(seat);
    }

    [Server]
    private void PositionHandTiles(int seat)
    {
        Transform container = GetHandContainer(seat);
        if (container == null)
        {
            Debug.LogError($"[GameManager] Hand container for seat {seat} is NULL! Assign HandPosition_Seat{seat} in inspector!");
            return;
        }
        
        SyncList<uint> hand = GetHandForSeat(seat);
        if (hand == null) return;
        
        int tileCount = hand.Count;
        float startX = -(tileCount - 1) * tileSpacing * 0.5f;
        
        Debug.Log($"[GameManager] Positioning {tileCount} tiles for seat {seat} at container: {container.name}");
        
        for (int i = 0; i < tileCount; i++)
        {
            uint netId = hand[i];
            if (spawnedTiles.TryGetValue(netId, out GameObject tileObj))
            {
                // Calculate position in container's local space
                Vector3 localPos = new Vector3(startX + i * tileSpacing, 0f, 0f);
                
                // Convert to world space by using container's right vector (for X offset)
                Vector3 worldPos = container.position + container.right * localPos.x;
                
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = container.rotation * handRotation;
                
                Debug.Log($"[GameManager] Positioned tile {netId} at {worldPos}");
                
                RpcUpdateTileTransform(netId, worldPos, tileObj.transform.rotation);
            }
            else
            {
                Debug.LogError($"[GameManager] Could not find tile GameObject for netId {netId}");
            }
        }
        
        // Position drawn tile separately if exists
        uint drawnNetId = GetDrawnTileForSeat(seat);
        if (drawnNetId != 0 && spawnedTiles.TryGetValue(drawnNetId, out GameObject drawnObj))
        {
            Vector3 drawnLocalPos = new Vector3(startX + tileCount * tileSpacing + drawnTileOffset, 0f, 0f);
            Vector3 drawnWorldPos = container.position + container.right * drawnLocalPos.x;
            
            drawnObj.transform.position = drawnWorldPos;
            drawnObj.transform.rotation = container.rotation * handRotation;
            
            Debug.Log($"[GameManager] Positioned drawn tile {drawnNetId} at {drawnWorldPos}");
            
            RpcUpdateTileTransform(drawnNetId, drawnWorldPos, drawnObj.transform.rotation);
        }
    }

    /// <summary>
    /// Position flower tiles in a 2 row x 4 column grid
    /// </summary>
    [Server]
    private void PositionFlowerTiles(int seat)
    {
        Transform container = GetHandContainer(seat);
        if (container == null) return;
        
        SyncList<uint> flowers = GetFlowersForSeat(seat);
        if (flowers == null) return;
        
        int flowerCount = flowers.Count;
        if (flowerCount == 0) return;
        
        // Position flowers in 2x4 grid to the right of hand
        // Each flower is 0.1 units wide and 0.15 units tall
        float flowerWidth = 0.12f;
        float flowerHeight = 0.16f;
        float gridStartX = -0.64f + 13 * .12f + 0.05f; // Offset to right of hand
        float gridStartY = 0.29f;
        
        for (int i = 0; i < flowerCount; i++)
        {
            uint netId = flowers[i];
            if (spawnedTiles.TryGetValue(netId, out GameObject flowerObj))
            {
                int col = i % 4; // Column (0-3)
                int row = i / 4; // Row (0-1)
                
                Vector3 localPos = new Vector3(
                    gridStartX + col * flowerWidth,
                    0f,
                    row * - flowerHeight + gridStartY
                );
                
                Vector3 worldPos = container.position + 
                                 container.right * localPos.x + 
                                 container.forward * localPos.z;
                
                flowerObj.transform.position = worldPos;
                flowerObj.transform.rotation = container.rotation * Quaternion.Euler(0, 0, 0); // Face up
                
                // Disable collider so flower tiles cannot be clicked
                Collider collider = flowerObj.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                }
                
                RpcUpdateTileTransform(netId, worldPos, flowerObj.transform.rotation);
                RpcDisableFlowerCollider(netId); // Disable on all clients too
            }
        }
        
        Debug.Log($"[GameManager] Positioned {flowerCount} flowers for seat {seat}");
    }

    [ClientRpc]
    private void RpcUpdateTileTransform(uint netId, Vector3 position, Quaternion rotation)
    {
        // Find the tile by network identity
        if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity))
        {
            identity.transform.position = position;
            identity.transform.rotation = rotation;
        }
    }

    [ClientRpc]
    private void RpcDisableFlowerCollider(uint netId)
    {
        // Disable collider on flower tiles so they can't be clicked
        if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity))
        {
            Collider collider = identity.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }
    }

    [ClientRpc]
    private void RpcDisableTileCollider(uint netId)
    {
        // Disable collider on discarded tiles so they can't be clicked
        if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity))
        {
            Collider collider = identity.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
        }
    }

    /// <summary>
    /// Sends directly to the specific client whose turn it is.
    /// Using TargetRpc eliminates all PlayerIndex-based filtering — the message
    /// only arrives on exactly the right client, with no timing races.
    /// </summary>
    [TargetRpc]
    private void TargetNotifyTileDrawn(NetworkConnection target)
    {
        if (NetworkClient.localPlayer == null) return;
        NetworkedPlayerHand hand = NetworkClient.localPlayer.GetComponentInChildren<NetworkedPlayerHand>();
        hand?.OnTileDrawn();
    }

    [TargetRpc]
    private void TargetNotifyTileDrawnDelayed(NetworkConnection target)
    {
        if (NetworkClient.localPlayer == null) return;
        NetworkedPlayerHand hand = NetworkClient.localPlayer.GetComponentInChildren<NetworkedPlayerHand>();
        hand?.OnTileDrawnDelayed();
    }

    /// <summary>
    /// Called on ALL clients after a Kong replacement tile is drawn.
    /// Each client self-filters by comparing seatIndex to their own mySeatIndex.
    /// Using ClientRpc here avoids TargetRpc routing which requires players[seat] to be set.
    /// </summary>
    [ClientRpc]
    private void RpcNotifyKongReplacementDrawn(int seatIndex)
    {
        if (NetworkClient.localPlayer == null) return;
        NetworkedPlayerHand hand = NetworkClient.localPlayer.GetComponentInChildren<NetworkedPlayerHand>();
        hand?.OnKongReplacementDrawn(seatIndex);
    }

    // Helper: get the NetworkConnection for a seat, or null if it's a bot or host
    private NetworkConnection GetConnectionForSeat(int seat)
    {
        if (isBot[seat] || players[seat] == null) return null;
        return players[seat].connectionToClient;
    }

    /// <summary>
    /// Called on ALL clients automatically by Mirror whenever currentPlayerIndex changes.
    /// This is the most reliable way to notify clients of a turn change — it uses Mirror's
    /// own SyncVar machinery with zero custom routing or timing dependencies.
    /// Each client checks if it is now the active player and runs the Kong check if so.
    /// </summary>
    private void OnCurrentPlayerIndexChanged(int oldIndex, int newIndex)
    {
        // Only care on clients (server handles its own flow via AdvanceTurn)
        if (isServer) return;
        if (NetworkClient.localPlayer == null) return;

        NetworkedPlayerHand hand = NetworkClient.localPlayer.GetComponentInChildren<NetworkedPlayerHand>();
        if (hand == null) return;

        // The hand component knows its own seat index — let it decide if it's now active
        hand.OnTurnChanged(newIndex);
    }

    // ---- Drawn tile SyncVar hooks ----
    // Each fires on all clients the moment that seat's drawn tile changes.
    // We only act when the value becomes non-zero (tile arriving, not being cleared).
    // This is the definitive signal that both the tile AND the turn update have landed
    // because AdvanceTurn draws the tile first, then sets currentPlayerIndex — so by
    // the time the drawn tile hook fires the turn index is already correct on the client.
    private void OnSeat0DrawnTileChanged(uint oldVal, uint newVal) => OnDrawnTileArrived(0, newVal);
    private void OnSeat1DrawnTileChanged(uint oldVal, uint newVal) => OnDrawnTileArrived(1, newVal);
    private void OnSeat2DrawnTileChanged(uint oldVal, uint newVal) => OnDrawnTileArrived(2, newVal);
    private void OnSeat3DrawnTileChanged(uint oldVal, uint newVal) => OnDrawnTileArrived(3, newVal);

    private void OnDrawnTileArrived(int seat, uint newVal)
    {
        // Ignore clears (tile removed from drawn slot) and server-side calls
        if (newVal == 0) return;
        if (isServer) return;
        if (NetworkClient.localPlayer == null) return;

        NetworkedPlayerHand hand = NetworkClient.localPlayer.GetComponentInChildren<NetworkedPlayerHand>();
        if (hand == null) return;

        // Tell the hand a tile just arrived for this seat — it self-filters by mySeatIndex
        hand.OnDrawnTileArrived(seat);
    }

    // No [Server] — these just read SyncLists/SyncVars which are already replicated to all clients
    private SyncList<uint> GetHandForSeat(int seat)
    {
        switch (seat)
        {
            case 0: return seat0HandTiles;
            case 1: return seat1HandTiles;
            case 2: return seat2HandTiles;
            case 3: return seat3HandTiles;
            default: return null;
        }
    }

    private SyncList<uint> GetDiscardsForSeat(int seat)
    {
        switch (seat)
        {
            case 0: return seat0Discards;
            case 1: return seat1Discards;
            case 2: return seat2Discards;
            case 3: return seat3Discards;
            default: return null;
        }
    }

    private Transform GetHandContainer(int seat)
    {
        switch (seat)
        {
            case 0: return handPositionSeat0;
            case 1: return handPositionSeat1;
            case 2: return handPositionSeat2;
            case 3: return handPositionSeat3;
            default: return null;
        }
    }

    private Transform GetDiscardContainer(int seat)
    {
        switch (seat)
        {
            case 0: return discardPositionSeat0;
            case 1: return discardPositionSeat1;
            case 2: return discardPositionSeat2;
            case 3: return discardPositionSeat3;
            default: return null;
        }
    }

    [Server]
    private void ClearAllHands()
    {
        // Destroy all existing tiles
        foreach (var tileObj in spawnedTiles.Values)
        {
            if (tileObj != null)
            {
                NetworkServer.Destroy(tileObj);
            }
        }
        spawnedTiles.Clear();
        
        // Clear all hand lists
        seat0HandTiles.Clear();
        seat1HandTiles.Clear();
        seat2HandTiles.Clear();
        seat3HandTiles.Clear();
        
        // Clear drawn tiles
        seat0DrawnTile = 0;
        seat1DrawnTile = 0;
        seat2DrawnTile = 0;
        seat3DrawnTile = 0;
        
        // Clear discards
        seat0Discards.Clear();
        seat1Discards.Clear();
        seat2Discards.Clear();
        seat3Discards.Clear();
        
        // Clear flowers
        seat0Flowers.Clear();
        seat1Flowers.Clear();
        seat2Flowers.Clear();
        seat3Flowers.Clear();
        
        // Clear meld sets
        for (int i = 0; i < 4; i++)
        {
            seatMeldSets[i].Clear();
        }
    }

    /// <summary>
    /// Player discards a tile and ends their turn
    /// </summary>
    [Server]
    public void DiscardTile(int seatIndex, uint tileNetId)
    {
        if (currentPlayerIndex != seatIndex)
        {
            Debug.LogWarning($"[GameManager] Seat {seatIndex} tried to discard but it's seat {currentPlayerIndex}'s turn");
            return;
        }
        
        Debug.Log($"[GameManager] Seat {seatIndex} discarding tile {tileNetId}");
        
        // Remove from hand or drawn tile
        SyncList<uint> hand = GetHandForSeat(seatIndex);
        uint drawnTile = GetDrawnTileForSeat(seatIndex);
        
        bool wasDrawnTile = (drawnTile == tileNetId);
        
        if (wasDrawnTile)
        {
            // Discarding the drawn tile - just clear the drawn slot
            SetDrawnTileForSeat(seatIndex, 0);
            Debug.Log($"[GameManager] Discarded drawn tile, hand size remains: {hand.Count}");
        }
        else
        {
            // Discarding from hand - remove it
            hand.Remove(tileNetId);
            
            // Merge drawn tile into hand so there's no gap/offset
            if (drawnTile != 0)
            {
                hand.Add(drawnTile);
                SetDrawnTileForSeat(seatIndex, 0);
                Debug.Log($"[GameManager] Merged drawn tile into hand, new hand size: {hand.Count}");
            }
        }
        
        // Add to discard pile
        SyncList<uint> discards = GetDiscardsForSeat(seatIndex);
        discards.Add(tileNetId);
        
        // Position in discard area
        PositionDiscardTile(seatIndex, tileNetId, discards.Count - 1);
        
        // Sort and reposition hand (no gaps, drawn tile merged in)
        SortHandForSeat(seatIndex);
        PositionHandTiles(seatIndex);
        
        // Move to next player
        AdvanceTurn();
    }

    [Server]
    private void PositionDiscardTile(int seat, uint netId, int discardIndex)
    {
        Transform container = GetDiscardContainer(seat);
        if (container == null) return;
        
        if (!spawnedTiles.TryGetValue(netId, out GameObject tileObj)) return;
        
        int row = discardIndex / discardColumns;
        int col = discardIndex % discardColumns;
        
        Vector3 localPos = new Vector3(col * discardSpacing, 0f, row * -.16f);
        tileObj.transform.position = container.position + container.TransformDirection(localPos);
        tileObj.transform.rotation = container.rotation * discardRotation;
        
        RpcUpdateTileTransform(netId, tileObj.transform.position, tileObj.transform.rotation);

        // Disable collider so the discarded tile can no longer be clicked
        Collider col2 = tileObj.GetComponent<Collider>();
        if (col2 != null) col2.enabled = false;
        RpcDisableTileCollider(netId);
    }

    [Server]
    private void AdvanceTurn()
    {
        int nextPlayer = (currentPlayerIndex + 1) % 4;
        Debug.Log($"[GameManager] Turn advancing to seat {nextPlayer}");
        
        // Draw and position the tile BEFORE updating currentPlayerIndex.
        // The SyncVar hook (OnCurrentPlayerIndexChanged) fires on clients when
        // currentPlayerIndex changes — at that point the drawn tile must already
        // be in the SyncList so CheckForKongOpportunity sees a complete 14-tile hand.
        DrawTileForSeat(nextPlayer);
        PositionHandTiles(nextPlayer);

        // Now update the turn index — this triggers OnCurrentPlayerIndexChanged on all clients
        currentPlayerIndex = nextPlayer;

        // TargetRpc as a secondary path (e.g. for host whose SyncVar hook is skipped)
        NetworkConnection conn = GetConnectionForSeat(currentPlayerIndex);
        if (conn != null)
            TargetNotifyTileDrawn(conn);
        else if (!isBot[currentPlayerIndex])
            TargetNotifyTileDrawn(null);
        
        // If it's a bot's turn, make them discard automatically
        if (isBot[currentPlayerIndex])
        {
            StartCoroutine(BotDiscardAfterDelay(currentPlayerIndex));
        }
    }

    private System.Collections.IEnumerator BotDiscardAfterDelay(int seat)
    {
        yield return new WaitForSeconds(0f); // Bot thinks for 1.5 seconds
        
        if (currentPlayerIndex == seat) // Make sure it's still this bot's turn
        {
            BotDiscard(seat);
        }
    }

    /// <summary>
    /// Waits for all tile spawn messages from the initial deal to reach clients,
    /// then notifies the dealer's client to check for a Kong opportunity.
    /// Only the dealer (current player) can act on a Kong at round start.
    /// Non-dealer seats will get their notification when their turn begins via AdvanceTurn.
    /// </summary>
    private System.Collections.IEnumerator NotifySeatsAfterDeal()
    {
        yield return new WaitForSeconds(0.5f);

        if (!isBot[dealerSeat])
        {
            NetworkConnection conn = GetConnectionForSeat(dealerSeat);
            if (conn != null)
                TargetNotifyTileDrawnDelayed(conn);
            else
                TargetNotifyTileDrawnDelayed(null); // host
        }
    }

    // Called only at round start where 50+ tile spawns need time to propagate.
    // Kept for compatibility — now routed via TargetNotifyTileDrawnDelayed.
    [ClientRpc]
    private void RpcNotifyTileDrawnDelayed(int seatIndex)
    {
        if (NetworkClient.localPlayer == null) return;
        NetworkedPlayerHand hand = NetworkClient.localPlayer.GetComponentInChildren<NetworkedPlayerHand>();
        hand?.OnTileDrawnDelayed();
    }

    [Server]
    private void BotDiscard(int seat)
    {
        Debug.Log($"[GameManager] Bot at seat {seat} is discarding");
        
        // Bot always discards the drawn tile (simplest strategy)
        uint drawnTile = GetDrawnTileForSeat(seat);
        
        if (drawnTile != 0)
        {
            DiscardTile(seat, drawnTile);
        }
        else
        {
            Debug.LogError($"[GameManager] Bot at seat {seat} has no drawn tile!");
        }
    }

    /// <summary>
    /// Called from NetworkedPlayerHand when a real player wants to discard
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdDiscardTile(uint tileNetId, NetworkConnectionToClient sender = null)
    {
        // Find which seat this player occupies
        int seatIndex = -1;
        for (int i = 0; i < 4; i++)
        {
            if (players[i] != null && players[i].connectionToClient == sender)
            {
                seatIndex = i;
                break;
            }
        }
        
        if (seatIndex == -1)
        {
            Debug.LogError("[GameManager] Could not find seat for player");
            return;
        }
        
        DiscardTile(seatIndex, tileNetId);
    }

    // ========== KONG (SELF-DRAWN) FUNCTIONALITY ==========

    /// <summary>
    /// Called from NetworkedPlayerHand when a player declares a self-drawn Kong.
    /// Only valid on the player's own turn.
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdDeclareKong(int sortValue, NetworkConnectionToClient sender = null)
    {
        // Identify the seat
        int seatIndex = -1;
        for (int i = 0; i < 4; i++)
        {
            if (players[i] != null && players[i].connectionToClient == sender)
            {
                seatIndex = i;
                break;
            }
        }

        if (seatIndex == -1)
        {
            Debug.LogError("[GameManager] CmdDeclareKong: Could not find seat for player");
            return;
        }

        if (currentPlayerIndex != seatIndex)
        {
            Debug.LogWarning($"[GameManager] Seat {seatIndex} tried to Kong but it's seat {currentPlayerIndex}'s turn");
            return;
        }

        // Validate: player must have 4 tiles of this value in hand (including drawn tile)
        SyncList<uint> hand = GetHandForSeat(seatIndex);
        uint drawnNetId = GetDrawnTileForSeat(seatIndex);

        List<uint> matchingNetIds = new List<uint>();

        foreach (uint netId in hand)
        {
            if (spawnedTiles.TryGetValue(netId, out GameObject tileObj))
            {
                TileData data = tileObj.GetComponent<TileData>();
                if (data != null && data.GetSortValue() == sortValue)
                {
                    matchingNetIds.Add(netId);
                }
            }
        }

        // Check drawn tile too
        if (drawnNetId != 0 && spawnedTiles.TryGetValue(drawnNetId, out GameObject drawnObj))
        {
            TileData drawnData = drawnObj.GetComponent<TileData>();
            if (drawnData != null && drawnData.GetSortValue() == sortValue)
            {
                matchingNetIds.Add(drawnNetId);
            }
        }

        if (matchingNetIds.Count < 4)
        {
            Debug.LogWarning($"[GameManager] Seat {seatIndex} tried to Kong {sortValue} but only has {matchingNetIds.Count} copies");
            return;
        }

        // Take exactly 4 tiles
        List<uint> kongNetIds = matchingNetIds.Take(4).ToList();

        // If the drawn tile exists but is NOT one of the kong tiles, merge it into the
        // hand now. Without this, the drawn tile slot gets overwritten by the replacement
        // tile but the old drawn tile GameObject stays in the scene at the drawn position,
        // causing two tiles to visually overlap there.
        if (drawnNetId != 0 && !kongNetIds.Contains(drawnNetId))
        {
            hand.Add(drawnNetId);
            SetDrawnTileForSeat(seatIndex, 0);
            Debug.Log($"[GameManager] Merged non-kong drawn tile {drawnNetId} into hand before Kong");
        }

        // Remove from hand and drawn slot
        foreach (uint netId in kongNetIds)
        {
            if (hand.Contains(netId))
            {
                hand.Remove(netId);
            }
            else if (GetDrawnTileForSeat(seatIndex) == netId)
            {
                SetDrawnTileForSeat(seatIndex, 0);
            }
        }

        // Track the meld set server-side
        seatMeldSets[seatIndex].Add(new List<uint>(kongNetIds));
        int meldSetIndex = seatMeldSets[seatIndex].Count - 1;

        // Broadcast to ALL clients: position and show the Kong meld
        // Local player (kong declarer) sees face-up; others see face-down (middle 2 flipped)
        RpcShowKongMeld(seatIndex, sortValue, kongNetIds, meldSetIndex);

        // Sort and reposition remaining hand
        SortHandForSeat(seatIndex);
        PositionHandTiles(seatIndex);

        // Draw a replacement tile for the player
        DrawTileForSeat(seatIndex);
        PositionHandTiles(seatIndex);

        // Broadcast to all clients — each client's OnKongReplacementDrawn self-filters
        // by seat index. Using ClientRpc here avoids relying on players[seatIndex].connectionToClient
        // which may be null if player registration was incomplete.
        RpcNotifyKongReplacementDrawn(seatIndex);

        Debug.Log($"[GameManager] Seat {seatIndex} successfully declared Kong of {sortValue}, drew replacement tile");
    }

    /// <summary>
    /// Tell all clients to display a Kong meld for the given seat.
    /// The declaring player sees tiles face-up; opponents see the middle two tiles face-down
    /// (standard concealed Kong display).
    /// </summary>
    [ClientRpc]
    private void RpcShowKongMeld(int declaringSeat, int sortValue, List<uint> kongNetIds, int meldSetIndex)
    {
        Debug.Log($"[GameManager] RpcShowKongMeld: Seat {declaringSeat} declared Kong of {sortValue}, meld set {meldSetIndex}");

        // Determine if the local client is the declaring player
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        bool isDeclaringPlayer = (localPlayer != null && localPlayer.PlayerIndex == declaringSeat);

        // Find the meld container for this seat
        Transform meldContainer = GetMeldContainer(declaringSeat);
        if (meldContainer == null)
        {
            Debug.LogError($"[GameManager] No meld container found for seat {declaringSeat}! Assign MeldPosition_Seat{declaringSeat} in inspector.");
            return;
        }

        // Calculate position offset for this meld set (based on prior melds)
        // We need to count existing children to find the offset
        float tileSpacing = meldTileSpacing;
        float setSpacing = meldSetSpacing;
        float directionMultiplier = (declaringSeat == 0) ? 1f : -1f;

        // Count total tiles already in the meld container to find offset
        float meldStartX = CalculateMeldStartX(meldContainer, meldSetIndex, tileSpacing, setSpacing, directionMultiplier);

        // Rotations
        Quaternion faceUpRotation = (declaringSeat == 0) ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
        Quaternion faceDownRotation;
        if (declaringSeat == 0)
        {
            faceDownRotation = Quaternion.Euler(180f, 0f, 0f); // Flip upside down
        }
        else
        {
            faceDownRotation = Quaternion.Euler(180f, 180f, 0f);
        }

        // Position each Kong tile
        for (int i = 0; i < kongNetIds.Count && i < 4; i++)
        {
            uint netId = kongNetIds[i];

            if (!NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity))
            {
                Debug.LogWarning($"[GameManager] Could not find spawned tile with netId {netId}");
                continue;
            }

            GameObject tileObj = identity.gameObject;
            tileObj.transform.SetParent(meldContainer, false);

            float xPos = meldStartX + (directionMultiplier * i * tileSpacing);
            tileObj.transform.localPosition = new Vector3(xPos, 0f, 0f);

            // Self-drawn Kong display rules:
            // - Declaring player: all 4 face-up
            // - Opponent viewers: tiles 0 and 3 (outer) face-up, tiles 1 and 2 (inner) face-down
            bool showFaceUp;
            if (isDeclaringPlayer)
            {
                showFaceUp = true;
            }
            else
            {
                showFaceUp = (i == 99 || i == 999); // Only outer tiles face-up for opponents
            }

            tileObj.transform.localRotation = showFaceUp ? faceUpRotation : faceDownRotation;

            // Disable collider — meld tiles are not clickable
            Collider col = tileObj.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        Debug.Log($"[GameManager] Kong meld displayed for seat {declaringSeat} (face-up: {isDeclaringPlayer})");
    }

    /// <summary>
    /// Calculate the X starting position for a new meld set based on existing meld content.
    /// Uses child count of the meld container grouped in sets of 4.
    /// </summary>
    private float CalculateMeldStartX(Transform meldContainer, int meldSetIndex, float tileSpacing, float setSpacing, float directionMultiplier)
    {
        // Count how many tiles (children) already exist before this new set
        // Each set has exactly 4 tiles, so the offset is meldSetIndex * (4 * tileSpacing + setSpacing)
        float startX = 0f;
        for (int i = 0; i < meldSetIndex; i++)
        {
            startX += directionMultiplier * (4 * tileSpacing + setSpacing);
        }
        return startX;
    }

    /// <summary>
    /// Get the meld/kong area container for a seat.
    /// Falls back to searching the scene for "KongArea_Seat{n}" if not assigned.
    /// </summary>
    private Transform GetMeldContainer(int seat)
    {
        // First try the directly assigned references
        Transform direct = null;
        switch (seat)
        {
            case 0: direct = meldPositionSeat0; break;
            case 1: direct = meldPositionSeat1; break;
            case 2: direct = meldPositionSeat2; break;
            case 3: direct = meldPositionSeat3; break;
        }
        if (direct != null) return direct;

        // Fallback: search by name (supports legacy "KongArea_Seat{n}" naming)
        GameObject found = GameObject.Find($"KongArea_Seat{seat}");
        if (found != null) return found.transform;

        found = GameObject.Find($"MeldPosition_Seat{seat}");
        if (found != null) return found.transform;

        return null;
    }

    /// <summary>
    /// Returns all meld sets (Kong/Pon/Chi) tracked for a seat (server-side list of netId groups).
    /// </summary>
    public List<List<uint>> GetMeldSetsForSeat(int seat)
    {
        if (seatMeldSets.TryGetValue(seat, out List<List<uint>> sets))
            return sets;
        return new List<List<uint>>();
    }

    /// <summary>
    /// Get the seat index for a specific player
    /// </summary>
    public int GetSeatForPlayer(NetworkPlayer player)
    {
        for (int i = 0; i < 4; i++)
        {
            if (players[i] == player) return i;
        }
        return -1;
    }

    /// <summary>
    /// Check if a seat is controlled by a bot
    /// </summary>
    public bool IsBotSeat(int seat)
    {
        return seat >= 0 && seat < 4 && isBot[seat];
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ========== HELPER METHODS FOR PLAYER HAND ==========

    /// <summary>
    /// Check if a tile belongs to a specific seat's hand
    /// </summary>
    /// <summary>
    /// Check if a tile belongs to a specific seat's hand
    /// Excludes flower tiles which are never clickable
    /// </summary>
    public bool IsTileInSeatHand(int seat, uint netId)
    {
        // First check if it's a flower tile - flowers are NEVER clickable
        GameObject tileObj = FindTileObject(netId);
        if (tileObj != null)
        {
            TileData data = tileObj.GetComponent<TileData>();
            if (data != null && IsFlowerTile(data.GetSortValue()))
            {
                return false; // Flower tiles cannot be clicked/discarded
            }
        }
        
        SyncList<uint> hand = GetHandForSeat(seat);
        if (hand == null) return false;
        
        return hand.Contains(netId) || GetDrawnTileForSeat(seat) == netId;
    }

    /// <summary>
    /// Get all tiles in a seat's hand (returns TileData components).
    /// Works on both server (uses spawnedTiles) and clients (uses NetworkClient.spawned).
    /// </summary>
    public List<TileData> GetTilesForSeat(int seat)
    {
        List<TileData> result = new List<TileData>();
        
        SyncList<uint> hand = GetHandForSeat(seat);
        if (hand == null) return result;

        foreach (uint netId in hand)
        {
            GameObject tileObj = FindTileObject(netId);
            if (tileObj != null)
            {
                TileData data = tileObj.GetComponent<TileData>();
                if (data != null) result.Add(data);
            }
        }
        
        // Add drawn tile
        uint drawnNetId = GetDrawnTileForSeat(seat);
        if (drawnNetId != 0)
        {
            GameObject drawnObj = FindTileObject(drawnNetId);
            if (drawnObj != null)
            {
                TileData data = drawnObj.GetComponent<TileData>();
                if (data != null) result.Add(data);
            }
        }
        
        return result;
    }

    /// <summary>
    /// Finds a spawned tile GameObject by netId on both server and client.
    /// </summary>
    private GameObject FindTileObject(uint netId)
    {
        // Server path
        if (isServer && spawnedTiles.TryGetValue(netId, out GameObject serverObj))
            return serverObj;

        // Client path — use Mirror's spawned object registry
        if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity))
            return identity.gameObject;

        return null;
    }

    /// <summary>
    /// Get all tile GameObjects in a seat's hand
    /// </summary>
    public List<GameObject> GetTileObjectsForSeat(int seat)
    {
        List<GameObject> result = new List<GameObject>();
        
        SyncList<uint> hand = GetHandForSeat(seat);
        if (hand == null) return result;
        
        foreach (uint netId in hand)
        {
            if (spawnedTiles.TryGetValue(netId, out GameObject tileObj))
            {
                result.Add(tileObj);
            }
        }
        
        // Add drawn tile
        uint drawnNetId = GetDrawnTileForSeat(seat);
        if (drawnNetId != 0 && spawnedTiles.TryGetValue(drawnNetId, out GameObject drawnObj))
        {
            result.Add(drawnObj);
        }
        
        return result;
    }

    /// <summary>
    /// Get count of specific tile value in a seat's hand
    /// </summary>
    public int CountTileValueInHand(int seat, int sortValue)
    {
        List<TileData> tiles = GetTilesForSeat(seat);
        return tiles.Count(t => t.GetSortValue() == sortValue);
    }
}