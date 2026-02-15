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

    // Game state
    [SyncVar]
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
    [SyncVar]
    private uint seat0DrawnTile = 0;
    [SyncVar]
    private uint seat1DrawnTile = 0;
    [SyncVar]
    private uint seat2DrawnTile = 0;
    [SyncVar]
    private uint seat3DrawnTile = 0;
    
    // Track discarded tiles for each seat
    private readonly SyncList<uint> seat0Discards = new SyncList<uint>();
    private readonly SyncList<uint> seat1Discards = new SyncList<uint>();
    private readonly SyncList<uint> seat2Discards = new SyncList<uint>();
    private readonly SyncList<uint> seat3Discards = new SyncList<uint>();
    
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
        
        // Add to sync list
        foreach (int tile in tempWall)
        {
            wall.Add(tile);
        }
        
        Debug.Log($"[GameManager] Wall built with {wall.Count} tiles");
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
        Debug.Log("[GameManager] Dealing initial hands (13 tiles each)");
        
        // Deal 13 tiles to each seat
        for (int round = 0; round < 13; round++)
        {
            for (int seat = 0; seat < 4; seat++)
            {
                DrawTileForSeat(seat);
            }
        }
        
        // Sort all hands
        for (int seat = 0; seat < 4; seat++)
        {
            SortHandForSeat(seat);
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
        
        // During a turn, put in drawn slot. During initial deal, put in hand.
        uint currentDrawn = GetDrawnTileForSeat(seatIndex);

        if (currentDrawn != 0)
        {
            // Drawn slot occupied, add to hand
            SyncList<uint> hand = GetHandForSeat(seatIndex);
            if (hand != null)
            {
                hand.Add(netId);
            }
        }
        else
        {
            // Drawn slot empty, use it
            SetDrawnTileForSeat(seatIndex, netId);
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

    [Server]
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

    [Server]
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

    [Server]
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
    }

    [Server]
    private void AdvanceTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % 4;
        Debug.Log($"[GameManager] Turn advanced to seat {currentPlayerIndex}");
        
        // Draw tile for next player
        DrawTileForSeat(currentPlayerIndex);
        PositionHandTiles(currentPlayerIndex);
        
        // If it's a bot's turn, make them discard automatically
        if (isBot[currentPlayerIndex])
        {
            StartCoroutine(BotDiscardAfterDelay(currentPlayerIndex));
        }
    }

    private System.Collections.IEnumerator BotDiscardAfterDelay(int seat)
    {
        yield return new WaitForSeconds(1.5f); // Bot thinks for 1.5 seconds
        
        if (currentPlayerIndex == seat) // Make sure it's still this bot's turn
        {
            BotDiscard(seat);
        }
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
    public bool IsTileInSeatHand(int seat, uint netId)
    {
        SyncList<uint> hand = GetHandForSeat(seat);
        if (hand == null) return false;
        
        return hand.Contains(netId) || GetDrawnTileForSeat(seat) == netId;
    }

    /// <summary>
    /// Get all tiles in a seat's hand (returns TileData components)
    /// </summary>
    public List<TileData> GetTilesForSeat(int seat)
    {
        List<TileData> result = new List<TileData>();
        
        SyncList<uint> hand = GetHandForSeat(seat);
        if (hand == null) return result;
        
        foreach (uint netId in hand)
        {
            if (spawnedTiles.TryGetValue(netId, out GameObject tileObj))
            {
                TileData data = tileObj.GetComponent<TileData>();
                if (data != null)
                {
                    result.Add(data);
                }
            }
        }
        
        // Add drawn tile
        uint drawnNetId = GetDrawnTileForSeat(seat);
        if (drawnNetId != 0 && spawnedTiles.TryGetValue(drawnNetId, out GameObject drawnObj))
        {
            TileData data = drawnObj.GetComponent<TileData>();
            if (data != null)
            {
                result.Add(data);
            }
        }
        
        return result;
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