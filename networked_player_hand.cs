using Mirror;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Networked version of PlayerHand - manages one player's tiles with full game logic.
/// </summary>
public class NetworkedPlayerHand : NetworkBehaviour
{
    // Chi selection
    private List<ChiOption> currentChiOptions = new List<ChiOption>();
    private ChiOption selectedChiOption = null;

    // Near the top of the class with other public properties
    public bool IsSelectingKong => isSelectingKong;
    public List<int> AvailableKongValues => availableKongValues;

    // DELETED: Duplicate field - use _isInChiSelectionMode instead

    private bool isSelectingChi = false;
    private System.Action<ChiOption> onChiSelected = null;
    private System.Action<ChiOption> onChiHovered = null;
    private System.Action<ChiOption> onChiClickCallback = null;  // For Chi selection confirmation

    // For broadcasting Chi tiles to server
    [HideInInspector] public List<int> lastChiTiles = new List<int>();

    // Track the last discarded tile for interrupt detection
    private int lastDiscardedTileSortValue = 0;
    private int pendingRonTile = -1; // The discarded tile that can complete our hand

    /// <summary>
    /// Get the interrupt UI manager reference.
    /// </summary>
    public InterruptUIManager InterruptUI => interruptUI;

    // Core hand data (private to each player)
    private PlayerHand logicHand; // Your existing PlayerHand class for game logic
    private List<GameObject> spawnedTiles = new List<GameObject>();
    private GameObject drawnTile = null;
    private List<GameObject> flowerTiles = new List<GameObject>();
    private List<GameObject> meldedKongTiles = new List<GameObject>();

    private List<int> localMeldSizes = new List<int>();

    private int seatIndex = -1;
    private Transform handContainer;
    private bool canDeclareWin = false;
    
    // Public accessor methods for GameManager
    public Transform GetHandContainer() => handContainer;
    public void SetHandContainerDirect(Transform container) => handContainer = container;
    public GameObject DrawnTile => drawnTile; // Public read-only access to drawn tile
    
    private bool canDeclareKong = false;
    private List<int> availableKongValues = new List<int>();

    [Header("Visual Settings")]
    public Vector3 handStartPosition = new Vector3(0f, 0f, -3f);
    public float spacing = 1.2f;
    public float drawnTileSpacing = 1.8f;
    public float flowerTileOffsetZ = -0.5f;
    public float flowerTileSpacing = 0.8f;
    public Vector3 kongAreaStartPosition = new Vector3(-6f, 0f, 1f);
    public float kongTileSpacingX = 1.0f;

    private readonly Quaternion HandRotation = Quaternion.Euler(-45f, 0f, 0f);
    private readonly Quaternion DiscardRotation = Quaternion.Euler(0f, 0f, 0f);
    private int nextKongSetIndex = 0;

    [Header("UI References")]
    public GameObject winButtonUI;
    public GameObject kongButtonUI;
    
    [Header("Tenpai UI")]
    public GameObject tenpaiUIPanel;
    public Transform tenpaiIconsContainer;
    public GameObject tenpaiIconPrefab;

    void Awake()
    {
        logicHand = gameObject.AddComponent<PlayerHand>();
    }

    /// <summary>
    /// Assign Tenpai UI references (called by GameUIManager).
    /// </summary>
    public void AssignTenpaiUI(GameObject panel, Transform container, GameObject iconPrefab)
    {
        Debug.Log($"[NetworkedPlayerHand] AssignTenpaiUI called - Panel: {panel?.name}, Container: {container?.name}, Prefab: {iconPrefab?.name}");
        
        tenpaiUIPanel = panel;
        tenpaiIconsContainer = container;
        tenpaiIconPrefab = iconPrefab;
        
        Debug.Log($"[NetworkedPlayerHand] Tenpai UI assigned successfully - Panel: {tenpaiUIPanel != null}, Container: {tenpaiIconsContainer != null}, Prefab: {tenpaiIconPrefab != null}");
        
        if (tenpaiUIPanel != null)
        {
            tenpaiUIPanel.SetActive(false);
            Debug.Log("[NetworkedPlayerHand] TenpaiPanel set to inactive");
        }
        else
        {
            Debug.LogError("[NetworkedPlayerHand] TenpaiPanel is NULL after assignment!");
        }
    }

    void Start()
    {
        Debug.Log("[NetworkedPlayerHand] Start() called");
        
        if (winButtonUI != null) winButtonUI.SetActive(false);
        if (kongButtonUI != null) kongButtonUI.SetActive(false);
        
        // Tenpai UI will be assigned by GameUIManager - just hide it if already assigned
        if (tenpaiUIPanel != null)
        {
            tenpaiUIPanel.SetActive(false);
            Debug.Log("[NetworkedPlayerHand] TenpaiPanel set to inactive");
        }
        else
        {
            Debug.Log("[NetworkedPlayerHand] TenpaiPanel not yet assigned (will be assigned by GameUIManager)");
        }
        
        Debug.Log($"[NetworkedPlayerHand] Start() complete");
    }

    public void SetSeatIndex(int index)
    {
        seatIndex = index;
    }

    /// <summary>
    /// Set the hand container (sent by server).
    /// </summary>
    [TargetRpc]
    public void TargetSetHandContainer(NetworkConnection target, string containerPath)
    {
        // Try multiple ways to find the container
        GameObject container = GameObject.Find(containerPath);
        
        if (container == null)
        {
            // Try without path (just the name)
            string[] pathParts = containerPath.Split('/');
            string objectName = pathParts[pathParts.Length - 1];
            container = GameObject.Find(objectName);
        }
        
        if (container != null)
        {
            handContainer = container.transform;
            Debug.Log($"âœ“ Hand container set: {containerPath}");
        }
        else
        {
            Debug.LogError($"âœ— Could not find hand container: {containerPath}");
        }
    }

    /// <summary>
    /// Receive initial 13 tiles.
    /// </summary>
    [TargetRpc]
    public void TargetReceiveInitialHand(NetworkConnection target, List<int> tiles)
    {
        Debug.Log($"Received {tiles.Count} initial tiles");
        
        // Set logic hand
        logicHand.HandTiles.Clear();
        foreach (int sortValue in tiles)
        {
            TileData tileData = CreateTileDataFromSortValue(sortValue);
            logicHand.AddToHand(tileData);
        }

        logicHand.SortHand();
        
        // Spawn visuals
        DrawInitialHand(tiles);
    }

    /// <summary>
    /// Draw a new tile (current player only - knows the value).
    /// </summary>
    [TargetRpc]
    public void TargetDrawTile(NetworkConnection target, int tileValue)
    {
        Debug.Log($"Drew tile: {tileValue}");
        
        TileData tileData = CreateTileDataFromSortValue(tileValue);
        logicHand.SetDrawnTile(tileData);

        DrawSingleTile(tileValue);
        CheckForKongOpportunity();
        CheckForMahjongWin();
        
        // Tell all other clients to show a face-down drawn tile
        if (isServer)
        {
            RpcShowDrawnTileForOthers(seatIndex);
        }
    }
    
    /// <summary>
    /// Show drawn tile to all players (face-down for opponents).
    /// </summary>
    [ClientRpc]
    private void RpcShowDrawnTileForOthers(int playerSeat)
    {
        // Skip if this is the owning player (they already have the real tile)
        if (isOwned) return;
        
        // Show a placeholder/face-down tile for opponents
        ShowOpponentDrawnTile();
    }
    
    /// <summary>
    /// Direct method for server to initialize hand without TargetRpc (for testing/alternate flows).
    /// </summary>
    public void ReceiveInitialHandDirect(List<int> tiles, bool isLocalPlayer)
    {
        Debug.Log($"Received {tiles.Count} initial tiles (direct) - isLocal: {isLocalPlayer}");
        
        // Set logic hand (only for local player to prevent cheating)
        if (isLocalPlayer)
        {
            logicHand.HandTiles.Clear();
            foreach (int sortValue in tiles)
            {
                TileData tileData = CreateTileDataFromSortValue(sortValue);
                logicHand.AddToHand(tileData);
            }

            logicHand.SortHand();
        }
        
        // Spawn visuals (all players see tiles, but non-local players see face-down)
        DrawInitialHand(tiles);
    }
    
    /// <summary>
    /// Direct method for drawing a tile without TargetRpc (for testing/alternate flows).
    /// </summary>
    public void DrawTileDirect(int tileValue, bool isLocalPlayer)
    {
        Debug.Log($"[DrawTileDirect] Seat {seatIndex}: Drew tile {tileValue} - isLocal: {isLocalPlayer}");
        
        // Set logic hand (only for local player to prevent cheating)
        if (isLocalPlayer)
        {
            Debug.Log($"[DrawTileDirect] LOCAL PLAYER - Drawing real tile");
            TileData tileData = CreateTileDataFromSortValue(tileValue);
            logicHand.SetDrawnTile(tileData);

            DrawSingleTile(tileValue);
            CheckForKongOpportunity();
            CheckForMahjongWin();
        }
        else
        {
            // For opponents, just show the face-down placeholder
            Debug.Log($"[DrawTileDirect] OPPONENT - Showing placeholder tile");
            ShowOpponentDrawnTile();
        }
    }

    /// <summary>
    /// Draw initial hand visuals.
    /// </summary>
    private void DrawInitialHand(List<int> tiles)
    {
        spawnedTiles.Clear();
        
        if (handContainer == null)
        {
            Debug.LogError("âœ— CRITICAL: Hand container is NULL! Cannot spawn tiles!");
            Debug.LogError($"   Player Seat: {seatIndex}");
            Debug.LogError($"   Tiles to spawn: {tiles.Count}");
            
            // Try to find container by seat index as fallback
            GameObject container = GameObject.Find($"HandPosition_Seat{seatIndex}");
            if (container != null)
            {
                handContainer = container.transform;
                Debug.Log($"âœ“ Found hand container by seat index: {container.name}");
            }
            else
            {
                Debug.LogError($"âœ— Could not find HandPosition_Seat{seatIndex}!");
                return;
            }
        }

        Debug.Log($"Spawning {tiles.Count} tiles in container: {handContainer.name}");

        if (NetworkedGameManager.Instance == null)
        {
            Debug.LogError("âœ— NetworkedGameManager.Instance is NULL!");
            return;
        }

        if (NetworkedGameManager.Instance.TilePrefabs == null || NetworkedGameManager.Instance.TilePrefabs.Length == 0)
        {
            Debug.LogError("âœ— NetworkedGameManager has NO tile prefabs assigned!");
            return;
        }

        int tilesSpawned = 0;
        foreach (int sortValue in tiles)
        {
            GameObject tilePrefab = FindTilePrefabBySortValue(sortValue);
            if (tilePrefab == null)
            {
                Debug.LogWarning($"Could not find tile prefab for sort value: {sortValue}");
                continue;
            }

            GameObject tile = Instantiate(tilePrefab, handContainer);
            tile.transform.SetParent(handContainer);
            
            // Make sure it has the clickable component
            NetworkedClickableTile clickable = tile.GetComponent<NetworkedClickableTile>();
            if (clickable == null)
            {
                tile.AddComponent<NetworkedClickableTile>();
            }
            
            spawnedTiles.Add(tile);
            tilesSpawned++;
        }

        Debug.Log($"âœ“ Successfully spawned {tilesSpawned} tiles!");

        RepositionTiles();
    }

    /// <summary>
    /// Draw single tile visual.
    /// </summary>
    private void DrawSingleTile(int tileValue)
    {
        if (drawnTile != null)
        {
            Destroy(drawnTile);
        }

        if (handContainer == null) return;

        GameObject tilePrefab = FindTilePrefabBySortValue(tileValue);
        if (tilePrefab == null) return;

        drawnTile = Instantiate(tilePrefab, handContainer);
        drawnTile.transform.SetParent(handContainer);

        RepositionTiles();
    }

    /// <summary>
    /// Show a face-down/placeholder tile for opponents to see the drawn tile position.
    /// </summary>
    private void ShowOpponentDrawnTile()
    {
        Debug.Log($"[ShowOpponentDrawnTile] START - Seat {seatIndex}, Container={handContainer != null}, GameMgr={NetworkedGameManager.Instance != null}");
        
        try
        {
            if (drawnTile != null)
            {
                Destroy(drawnTile);
            }

            if (handContainer == null)
            {
                Debug.LogError($"[ShowOpponentDrawnTile] ABORT - handContainer is null for seat {seatIndex}!");
                return;
            }

            // Use any tile prefab as a placeholder
            if (NetworkedGameManager.Instance == null || NetworkedGameManager.Instance.TilePrefabs == null || NetworkedGameManager.Instance.TilePrefabs.Length == 0)
            {
                Debug.LogError($"[ShowOpponentDrawnTile] ABORT - No tile prefabs available!");
                return;
            }
            
            GameObject tilePrefab = NetworkedGameManager.Instance.TilePrefabs[0];

            drawnTile = Instantiate(tilePrefab, handContainer);
            drawnTile.transform.SetParent(handContainer);
            drawnTile.name = $"OpponentDrawnTile_Seat{seatIndex}";
            
            // Make it look different (BRIGHT RED for debugging)
            Renderer renderer = drawnTile.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.red; // BRIGHT RED instead of gray
            }
            
            // Disable clicking
            Collider collider = drawnTile.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            RepositionTiles();
            Debug.Log($"[ShowOpponentDrawnTile] SUCCESS - Created at {drawnTile.transform.position}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ShowOpponentDrawnTile] EXCEPTION: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// Update opponent hand display to show correct number of concealed tiles.
    /// Only runs for opponent views (not the local player).
    /// </summary>
    private void UpdateOpponentHandDisplay(int concealedTileCount)
    {
        // Only update if this is an opponent's hand (not mine)
        if (isOwned) 
        {
            Debug.Log($"[UpdateOpponentHandDisplay] Skipping - this is my own hand");
            return;
        }
        
        Debug.Log($"[UpdateOpponentHandDisplay] Seat {seatIndex}: Updating to {concealedTileCount} tiles");
        
        if (handContainer == null)
        {
            Debug.LogError($"[UpdateOpponentHandDisplay] Hand container is null!");
            return;
        }
        
        // Save the drawn tile (if it exists)
        GameObject savedDrawnTile = drawnTile;
        
        // Destroy all current hand tiles
        foreach (GameObject tile in spawnedTiles)
        {
            if (tile != null) 
            {
                Destroy(tile);
            }
        }
        spawnedTiles.Clear();
        
        // Get a tile prefab to use as face-down placeholder
        if (NetworkedGameManager.Instance == null || 
            NetworkedGameManager.Instance.TilePrefabs == null || 
            NetworkedGameManager.Instance.TilePrefabs.Length == 0)
        {
            Debug.LogError("[UpdateOpponentHandDisplay] No tile prefabs available!");
            return;
        }
        
        GameObject placeholderPrefab = NetworkedGameManager.Instance.TilePrefabs[0];
        
        // Create the correct number of face-down tiles
        for (int i = 0; i < concealedTileCount; i++)
        {
            GameObject tile = Instantiate(placeholderPrefab, handContainer);
            tile.name = $"OpponentTile_{i}_Seat{seatIndex}";
            
            // Disable clicking for opponent tiles
            Collider col = tile.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            
            spawnedTiles.Add(tile);
        }
        
        // Restore drawn tile
        drawnTile = savedDrawnTile;
        
        // Reposition everything
        RepositionTiles();
        
        Debug.Log($"[UpdateOpponentHandDisplay] ✓ Updated seat {seatIndex} to {concealedTileCount} tiles");
    }

    /// <summary>
    /// Discard a tile (player clicks on it).
    /// </summary>
    public void DiscardAndDrawTile(Vector3 handPosition, GameObject discardedTile)
    {
        if (!isOwned) return;

        // Check if it's our turn
        if (NetworkedGameManager.Instance != null)
        {
            if (NetworkedGameManager.Instance.CurrentPlayerIndex != seatIndex)
            {
                Debug.Log("Not your turn!");
                return;
            }
        }

        TileData discardedData = discardedTile.GetComponent<TileData>();
        if (discardedData == null) return;

        int tileValue = discardedData.GetSortValue();

        // Update logic
        if (discardedTile == drawnTile)
        {
            logicHand.DiscardDrawnTile();
            drawnTile = null;
        }
        else if (spawnedTiles.Contains(discardedTile))
        {
            if (drawnTile != null)
            {
                spawnedTiles.Add(drawnTile);
                drawnTile = null;
            }
            
            spawnedTiles.Remove(discardedTile);
            logicHand.DiscardFromHand(discardedData);
            SortHand();
        }

        // Destroy the tile object
        Destroy(discardedTile);

        // Reset win/kong states
        canDeclareWin = false;
        canDeclareKong = false;
        if (winButtonUI != null) winButtonUI.SetActive(false);
        if (kongButtonUI != null) kongButtonUI.SetActive(false);

        // Tell server
        CmdDiscardTile(seatIndex, tileValue, handPosition);
    }

    /// <summary>
    /// Tell all clients to update the visual display of an opponent's hand.
    /// Called when a player's concealed hand size changes due to melds.
    /// </summary>
    [ClientRpc]
    private void RpcUpdateOpponentHandSize(int playerSeatIndex, int newConcealedCount)
    {
        Debug.Log($"[RpcUpdateOpponentHandSize] Player {playerSeatIndex} now has {newConcealedCount} concealed tiles");
        
        // Find the player at this seat
        NetworkPlayer[] allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        NetworkPlayer targetPlayer = null;
        
        foreach (NetworkPlayer player in allPlayers)
        {
            if (player.PlayerIndex == playerSeatIndex)
            {
                targetPlayer = player;
                break;
            }
        }
        
        if (targetPlayer == null)
        {
            Debug.LogWarning($"[RpcUpdateOpponentHandSize] Could not find player at seat {playerSeatIndex}");
            return;
        }
        
        // Update their hand display
        NetworkedPlayerHand hand = targetPlayer.GetComponent<NetworkedPlayerHand>();
        if (hand != null)
        {
            hand.UpdateOpponentHandDisplay(newConcealedCount);
        }
        else
        {
            Debug.LogWarning($"[RpcUpdateOpponentHandSize] Player at seat {playerSeatIndex} has no NetworkedPlayerHand!");
        }
    }
    
    /// <summary>
    /// Hide drawn tile on all clients after discard.
    /// </summary>
    [ClientRpc]
    private void RpcHideDrawnTileForOthers(int playerSeat)
    {
        // Skip if this is the owning player (they already handled it locally)
        if (isOwned) return;
        
        // Remove the placeholder drawn tile
        if (drawnTile != null)
        {
            Destroy(drawnTile);
            drawnTile = null;
        }
        
        RepositionTiles();
    }
    
    /// <summary>
    /// Public method for GameManager to hide drawn tile for all players.
    /// </summary>
    public void HideDrawnTileForDiscard()
    {
        Debug.Log($"[HideDrawnTileForDiscard] Seat {seatIndex}: Hiding drawn tile. isOwned={isOwned}");
        
        if (drawnTile != null)
        {
            Debug.Log($"[HideDrawnTileForDiscard] Destroying drawn tile at {drawnTile.transform.position}");
            Destroy(drawnTile);
            drawnTile = null;
            RepositionTiles();
        }
        else
        {
            Debug.Log($"[HideDrawnTileForDiscard] No drawn tile to hide");
        }
    }

    [Command]
    private void CmdDiscardTile(int playerIndex, int tileValue, Vector3 position)
    {
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.PlayerDiscardedTile(playerIndex, tileValue, position);
        }
    }

    /// <summary>
    /// Synchronize GameObjects (visual tiles) to the logical PlayerHand for analysis.
    /// </summary>
    private void SyncGameObjectsToPlayerHand()
    {
        // 1. Reset and populate the HandTiles
        logicHand.HandTiles.Clear();
        foreach (GameObject tileGO in spawnedTiles)
        {
            TileData data = tileGO.GetComponent<TileData>();
            if (data != null) logicHand.AddToHand(data);
        }
        
        // 2. Set the DrawnTile
        if (drawnTile != null)
        {
            logicHand.SetDrawnTile(drawnTile.GetComponent<TileData>());
        }
        else
        {
            logicHand.SetDrawnTile(null);
        }
        
        // 3. Reset and populate the FlowerTiles (Required to exclude flowers from win check)
        logicHand.FlowerTiles.Clear();
        foreach (GameObject tileGO in flowerTiles)
        {
            TileData data = tileGO.GetComponent<TileData>();
            if (data != null) logicHand.CollectFlower(data);
        }
    }

    /// <summary>
    /// Check for Kong opportunity.
    /// </summary>
    private void CheckForKongOpportunity()
    {
        canDeclareKong = false;
        availableKongValues.Clear();
        
        if (kongButtonUI != null) kongButtonUI.SetActive(false);
        
        // Debug: Log the check
        Debug.Log("[CheckForKongOpportunity] Starting check...");
        
        // Must have a drawn tile to declare Kong
        if (drawnTile == null)
        {
            Debug.Log("[CheckForKongOpportunity] No drawn tile - cannot check for Kong");
            return;
        }
        
        // Sync logic hand first
        SyncGameObjectsToPlayerHand();

        List<TileData> allTiles = new List<TileData>(logicHand.HandTiles);
        if (logicHand.DrawnTile != null) allTiles.Add(logicHand.DrawnTile);
        
        Debug.Log($"[CheckForKongOpportunity] Checking {allTiles.Count} tiles for Kongs");

        var groups = allTiles
            .GroupBy(t => t.GetSortValue())
            .Where(g => g.Count() == 4)
            .Select(g => g.Key)
            .ToList();
        
        Debug.Log($"[CheckForKongOpportunity] Found {groups.Count} possible Kong groups");
        foreach (int val in groups)
        {
            Debug.Log($"[CheckForKongOpportunity]   - Tile value {val} has 4 copies");
        }

        if (groups.Any())
        {
            availableKongValues = groups;
            canDeclareKong = true;
            if (kongButtonUI != null)
            {
                kongButtonUI.SetActive(true);
                Debug.Log("[CheckForKongOpportunity] Kong button activated!");
            }
        }
        else
        {
            Debug.Log("[CheckForKongOpportunity] No Kong opportunities found");
        }
    }

    /// <summary>
    /// Start Kong selection mode (called by Kong button).
    /// </summary>
    public void StartKongSelection()
    {
        if (!canDeclareKong)
        {
            Debug.Log("No Kong available");
            return;
        }

        if (availableKongValues.Count == 1)
        {
            // Only one Kong option - execute immediately
            ExecuteKong(availableKongValues[0]);
        }
        else
        {
            // Multiple Kong options - player must click a tile
            Debug.Log($"Select one of {availableKongValues.Count} Kong options by clicking a tile");
            isSelectingKong = true;
            HighlightKongTiles(true);
        }
    }

    private bool isSelectingKong = false;

    /// <summary>
    /// Highlight tiles that can be used for Kong.
    /// </summary>
    private void HighlightKongTiles(bool highlight)
    {
        var allTiles = spawnedTiles.ToList();
        if (drawnTile != null) allTiles.Add(drawnTile);

        foreach (GameObject tile in allTiles)
        {
            if (tile == null) continue;
            
            Renderer tileRenderer = tile.GetComponentInChildren<Renderer>();
            if (tileRenderer == null) continue;

            int val = tile.GetComponent<TileData>()?.GetSortValue() ?? -1;

            if (highlight)
            {
                if (availableKongValues.Contains(val))
                {
                    tileRenderer.material.color = Color.white; // Bright
                }
                else
                {
                    tileRenderer.material.color = new Color(0.3f, 0.3f, 0.3f, 1.0f); // Dim
                }
            }
            else
            {
                tileRenderer.material.color = Color.white; // Reset all
            }
        }
    }

    /// <summary>
    /// Execute Kong declaration.
    /// </summary>
    public void ExecuteKong(int targetValue)
    {
        Debug.Log($"[ExecuteKong] START - targetValue: {targetValue}, canDeclareKong: {canDeclareKong}");
        Debug.Log($"[ExecuteKong] availableKongValues: {string.Join(", ", availableKongValues)}");
        
        if (!canDeclareKong)
        {
            Debug.LogWarning("[ExecuteKong] BLOCKED - canDeclareKong is false");
            return;
        }
        
        if (!availableKongValues.Contains(targetValue))
        {
            Debug.LogWarning($"[ExecuteKong] BLOCKED - {targetValue} not in available Kong values");
            return;
        }

        Debug.Log($"[ExecuteKong] PROCEEDING with Kong of {targetValue}");
        isSelectingKong = false;
        HighlightKongTiles(false);

        List<TileData> kongData = new List<TileData>();
        List<GameObject> tilesToMove = new List<GameObject>();

        int drawnValue = logicHand.DrawnTile?.GetSortValue() ?? -1;

        // Collect 4 tiles
        if (drawnValue == targetValue)
        {
            var matchingTiles = spawnedTiles.Where(t => t.GetComponent<TileData>().GetSortValue() == targetValue).Take(3).ToList();
            foreach (var tile in matchingTiles)
            {
                spawnedTiles.Remove(tile);
                tilesToMove.Add(tile);
                kongData.Add(tile.GetComponent<TileData>());
            }
            tilesToMove.Add(drawnTile);
            kongData.Add(drawnTile.GetComponent<TileData>());
            drawnTile = null;
        }
        else
        {
            var matchingTiles = spawnedTiles.Where(t => t.GetComponent<TileData>().GetSortValue() == targetValue).Take(4).ToList();
            foreach (var tile in matchingTiles)
            {
                spawnedTiles.Remove(tile);
                tilesToMove.Add(tile);
                kongData.Add(tile.GetComponent<TileData>());
            }

            if (drawnTile != null)
            {
                spawnedTiles.Add(drawnTile);
                drawnTile = null;
            }
        }

        // Update logic
        logicHand.AddMeldedKong(kongData);
        
        // ADD Kong tiles to visual collection
        meldedKongTiles.AddRange(tilesToMove);
        
        // POSITION THE KONG TILES
        PositionKongSet(tilesToMove);
        
        // NEW: Track self-Kong size for future meld positioning
        localMeldSizes.Add(4);
        Debug.Log($"[ExecuteKong] Tracked self-Kong (size 4). Total melds: {localMeldSizes.Count}");
        
        SyncGameObjectsToPlayerHand(); 
        SortHand();

        // Reset states
        canDeclareKong = false;
        if (kongButtonUI != null) kongButtonUI.SetActive(false);

        // Tell server
        List<int> kongTileSortValues = new List<int> { targetValue, targetValue, targetValue, targetValue };
        int newConcealedCount = spawnedTiles.Count;
        CmdDeclareKong(seatIndex, targetValue, kongData.Select(t => t.GetSortValue()).ToList(), newConcealedCount);
    }

    [Command]
    private void CmdDeclareKong(int playerIndex, int kongValue, List<int> kongTiles, int newConcealedCount)
    {
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.PlayerDeclaredKong(playerIndex, kongValue, kongTiles);
        }
        
        // Broadcast hand size update
        RpcUpdateOpponentHandSize(seatIndex, newConcealedCount);
    }

    /// <summary>
    /// Check for winning hand.
    /// </summary>
    private void CheckForMahjongWin()
    {
        Debug.Log($"[CheckForMahjongWin] ===== STARTING WIN CHECK =====");
        Debug.Log($"[CheckForMahjongWin] Seat: {seatIndex}");
        Debug.Log($"[CheckForMahjongWin] Hand Tiles: {logicHand.HandTiles.Count}");
        Debug.Log($"[CheckForMahjongWin] Drawn Tile: {(logicHand.DrawnTile != null ? logicHand.DrawnTile.GetSortValue().ToString() : "NULL")}");
        Debug.Log($"[CheckForMahjongWin] Melded Kongs: {logicHand.MeldedKongs.Count}");
        Debug.Log($"[CheckForMahjongWin] Completed Melds (Chi/Pon/Kong from discards): {completedMelds.Count}");
        
        // Log all tile values for debugging
        string handTilesList = string.Join(", ", logicHand.HandTiles.Select(t => t.GetSortValue()));
        Debug.Log($"[CheckForMahjongWin] Hand tile values: {handTilesList}");
        
        if (logicHand.MeldedKongs.Count > 0)
        {
            string kongTilesList = string.Join(", ", logicHand.MeldedKongs.Select(t => t.GetSortValue()));
            Debug.Log($"[CheckForMahjongWin] Kong tile values: {kongTilesList}");
        }
        
        if (logicHand.DrawnTile == null)
        {
            Debug.Log($"[CheckForMahjongWin] No drawn tile - cannot check for win");
            canDeclareWin = false;
            if (winButtonUI != null) winButtonUI.SetActive(false);
            return;
        }

        // CRITICAL FIX: Pass completed melds so they're counted in win analysis
        HandAnalysisResult analysis = logicHand.CheckForWinAndAnalyze(completedMelds);
        bool hasWinningStructure = analysis.IsWinningHand;

        Debug.Log($"[CheckForMahjongWin] Analysis complete - IsWinningHand: {hasWinningStructure}");
        Debug.Log($"[CheckForMahjongWin] Is13Orphans: {analysis.Is13OrphansWin}, Is7Pairs: {analysis.Is7PairsWin}, IsTraditional: {analysis.IsTraditionalWin}, IsPure: {analysis.IsPureHand}");

        // CRITICAL: Check if score would be > 0 after all bonuses
        if (hasWinningStructure)
        {
            // Calculate Kong counts
            int selfKongCount = logicHand.MeldedKongs.Count / 4;
            int discardKongCount = 0;
            if (completedMelds != null)
            {
                discardKongCount = completedMelds.Count(m => m.Type == InterruptActionType.Kong);
            }
            
            List<string> flowerMessages;
            bool isTsumo = true; // Tsumo check
            int completedMeldCount = completedMelds?.Count ?? 0;
            int finalScore = logicHand.CalculateTotalScore(analysis, seatIndex, isTsumo, selfKongCount, discardKongCount, completedMeldCount, out flowerMessages, 1);
            canDeclareWin = finalScore > 0;
            
            Debug.Log($"[CheckForMahjongWin] Final score: {finalScore}, Can declare win: {canDeclareWin}");
            if (flowerMessages.Count > 0)
            {
                Debug.Log($"[CheckForMahjongWin] Flower messages: {string.Join("; ", flowerMessages)}");
            }
            
            if (!canDeclareWin)
            {
                Debug.Log($"[CheckForMahjongWin] Win BLOCKED - score is {finalScore} (must be > 0)");
            }
        }
        else
        {
            canDeclareWin = false;
        }

        if (winButtonUI != null)
        {
            winButtonUI.SetActive(canDeclareWin);
            Debug.Log($"[CheckForMahjongWin] Win button set to: {canDeclareWin}");
        }
        else
        {
            Debug.LogError($"[CheckForMahjongWin] winButtonUI is NULL! Cannot show win button!");
        }

        if (canDeclareWin)
        {
            Debug.Log("You have a winning hand! Press Tsumo to win.");
        }
    }

    /// <summary>
    /// Declare Mahjong (Tsumo) - Show results and notify server.
    /// </summary>
    public void ShowResults()
    {
        Debug.Log($"[ShowResults] Called - canDeclareWin: {canDeclareWin}");
        
        if (!canDeclareWin)
        {
            Debug.LogWarning("[ShowResults] Cannot declare win - not in winning state");
            return;
        }

        // 1. Analyze the hand WITH completed melds and calculate score
        HandAnalysisResult analysis = logicHand.CheckForWinAndAnalyze(completedMelds);
        
        // Calculate Kong counts
        int selfKongCount = logicHand.MeldedKongs.Count / 4;
        int discardKongCount = 0;
        if (completedMelds != null)
        {
            discardKongCount = completedMelds.Count(m => m.Type == InterruptActionType.Kong);
        }
        
        Debug.Log($"[ShowResults] Self-Kongs: {selfKongCount}, Discard-Kongs: {discardKongCount}");
        
        // Calculate score with Tsumo bonus and Kong bonuses
        List<string> flowerMessages;
        bool isTsumo = true; // This is always Tsumo (self-drawn win)
        int completedMeldCount = completedMelds?.Count ?? 0;
        int score = logicHand.CalculateTotalScore(analysis, seatIndex, isTsumo, selfKongCount, discardKongCount, completedMeldCount, out flowerMessages, 1);
        
        // 2. Get ALL tile sort values (hand + drawn + kongs + melds) for network transmission
        List<int> allTileSortValues = new List<int>();
        
        // Add hand tiles
        foreach (GameObject tile in spawnedTiles)
        {
            if (tile != null)
            {
                TileData data = tile.GetComponent<TileData>();
                if (data != null) allTileSortValues.Add(data.GetSortValue());
            }
        }
        
        // Add drawn tile
        if (drawnTile != null)
        {
            TileData data = drawnTile.GetComponent<TileData>();
            if (data != null) allTileSortValues.Add(data.GetSortValue());
        }
        
        // Add self-declared Kong tiles
        foreach (GameObject kongTile in meldedKongTiles)
        {
            if (kongTile != null)
            {
                bool wasActive = kongTile.activeSelf;
                if (!wasActive) kongTile.SetActive(true);
                
                TileData data = kongTile.GetComponent<TileData>();
                if (data != null) allTileSortValues.Add(data.GetSortValue());
                
                if (!wasActive) kongTile.SetActive(false);
            }
        }
        
        // Add completed melds (Chi/Pon/Kong from discards)
        if (completedMelds != null)
        {
            foreach (var meld in completedMelds)
            {
                allTileSortValues.AddRange(meld.TileSortValues);
            }
        }
        
        // Sort for consistent display
        allTileSortValues.Sort();
        
        Debug.Log($"[ShowResults] ===== ANALYSIS BEFORE SENDING =====");
        Debug.Log($"[ShowResults] Score: {score}");
        Debug.Log($"[ShowResults] Total tiles: {allTileSortValues.Count}");
        Debug.Log($"[ShowResults] Self-declared Kongs: {logicHand.MeldedKongs.Count / 4}");
        Debug.Log($"[ShowResults] Completed Melds: {completedMelds?.Count ?? 0}");

        // 3. Show result screen locally
        ResultScreenUI resultScreen = FindFirstObjectByType<ResultScreenUI>();
        if (resultScreen != null)
        {
            Debug.Log($"[ShowResults] Showing local result screen");
            resultScreen.ShowResult(analysis, score, seatIndex, allTileSortValues);
        }

        // 4. Merge drawn tile into hand for display
        if (drawnTile != null)
        {
            spawnedTiles.Add(drawnTile);
            drawnTile = null;
        }
        SortHand();

        // 5. Hide UI buttons
        canDeclareWin = false;
        if (winButtonUI != null) winButtonUI.SetActive(false);
        if (kongButtonUI != null) kongButtonUI.SetActive(false);

        // 6. Tell server
        Debug.Log($"[ShowResults] Sending to server via CmdDeclareMahjong...");
        // Pass both tile values (for display) AND flower messages (for scoring breakdown)
        // (allTileSortValues already created earlier in this method around line 870)
        CmdDeclareMahjong(seatIndex, analysis, score, allTileSortValues, flowerMessages);
    }

    [Command]
    private void CmdDeclareMahjong(int playerIndex, HandAnalysisResult analysis, int score, List<int> tileSortValues, List<string> flowerMessages)
    {
        Debug.Log($"[CmdDeclareMahjong] SERVER RECEIVED:");
        Debug.Log($"  Player Index: {playerIndex}");
        Debug.Log($"  Score: {score}");
        Debug.Log($"  IsWinningHand: {analysis.IsWinningHand}");
        Debug.Log($"  IsTraditionalWin: {analysis.IsTraditionalWin}");
        Debug.Log($"  IsPureHand: {analysis.IsPureHand}");
        Debug.Log($"  IsHalfHand: {analysis.IsHalfHand}");
        Debug.Log($"  Is13Orphans: {analysis.Is13OrphansWin}");
        Debug.Log($"  Is7Pairs: {analysis.Is7PairsWin}");
        Debug.Log($"  SequencesCount: {analysis.SequencesCount}");
        Debug.Log($"  TripletsCount: {analysis.TripletsCount}");
        Debug.Log($"  FlowerCount: {analysis.FlowerCount}");
        Debug.Log($"  TripletSortValues count: {analysis.TripletSortValues?.Count ?? 0}");
        Debug.Log($"  Tile sort values count: {tileSortValues?.Count ?? 0}");
        Debug.Log($"  Flower messages count: {flowerMessages?.Count ?? 0}");
        
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.PlayerDeclaredMahjong(playerIndex, analysis, score, tileSortValues, flowerMessages);
        }
    }

    /// <summary>
    /// Sort and reposition tiles.
    /// </summary>
    private void SortHand()
    {
        spawnedTiles.Sort((a, b) => {
            TileData dataA = a.GetComponent<TileData>();
            TileData dataB = b.GetComponent<TileData>();
            if (dataA != null && dataB != null)
                return dataA.GetSortValue().CompareTo(dataB.GetSortValue());
            return 0;
        });

        RepositionTiles();
        logicHand.SortHand();
    }

    /// <summary>
    /// Reposition tiles visually.
    /// </summary>
    private void RepositionTiles()
    {
        if (handContainer == null) return;

        int handSize = spawnedTiles.Count;
        float handWidth = (handSize - 1) * spacing;
        float centerOffset = -handWidth / 2f;
        float startX = handStartPosition.x + centerOffset;

        for (int i = 0; i < handSize; i++)
        {
            float xPos = startX + i * spacing;
            Vector3 newPos = new Vector3(xPos, handStartPosition.y, handStartPosition.z);
            spawnedTiles[i].transform.localPosition = newPos;
            spawnedTiles[i].transform.localRotation = HandRotation;
        }

        if (drawnTile != null)
        {
            float drawnX = startX + (handSize - 1) * spacing + drawnTileSpacing;
            drawnTile.transform.localPosition = new Vector3(drawnX, handStartPosition.y, handStartPosition.z);
            drawnTile.transform.localRotation = HandRotation;
        }

        if (flowerTiles.Count > 0)
        {
            Debug.Log($"[PlayerHand] Positioning {flowerTiles.Count} flower tiles");
            
            // Position flower tiles
            if (flowerTiles.Count > 0)
            {
                Debug.Log($"[PlayerHand] Positioning {flowerTiles.Count} flowers");
                
                // ===== FIX: Always calculate based on 14-tile hand =====
                // Assume hand always has 13 sorted tiles + 1 drawn tile = 14 total
                // This ensures flowers are always at the same position
                
                // Position of 13th sorted tile (index 12)
                float tile13X = handStartPosition.x + (12 * spacing);
                
                // Position of 14th drawn tile (with extra spacing)
                float tile14X = tile13X + drawnTileSpacing;
                
                // Flower offset from the 14th tile position
                float flowerStartX = tile14X - 0.64f;
                float flowerZ = handStartPosition.z + 0.29f;
                
                for (int i = 0; i < flowerTiles.Count; i++)
                {
                    if (flowerTiles[i] == null) continue;
                    
                    float xPos = flowerStartX + i * flowerTileSpacing;
                    Vector3 flowerPos = new Vector3(xPos, handStartPosition.y, flowerZ);
                    
                    flowerTiles[i].transform.localPosition = flowerPos;
                    flowerTiles[i].transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                    
                    Debug.Log($"[PlayerHand] Positioned flower {i} at {flowerPos}");
                }
            }
        }
    }

    /// <summary>
    /// Position Kong tiles.
    /// </summary>
    private void PositionKongSet(List<GameObject> kongTiles)
    {
        if (kongTiles.Count != 4)
        {
            Debug.LogWarning($"[PositionKongSet] Expected 4 tiles, got {kongTiles.Count}");
            return;
        }
        
        Debug.Log($"[PositionKongSet] Positioning self-Kong for seat {seatIndex}");
        
        // CRITICAL FIX: Use same container and positioning logic as PositionMeld
        string containerName = $"KongArea_Seat{seatIndex}";
        GameObject containerObj = GameObject.Find(containerName);
        
        if (containerObj == null)
        {
            Debug.LogError($"[PositionKongSet] Could not find {containerName}!");
            return;
        }
        
        Transform meldContainer = containerObj.transform;
        
        float setSpacing = 0.05f;
        float tileSpacing = 0.12f;
        
        // Rotation based on seat
        Quaternion tileRotation = (seatIndex == 0) ? 
            Quaternion.identity : 
            Quaternion.Euler(0f, 180f, 0f);
        
        // Direction based on seat
        float directionMultiplier = (seatIndex == 0) ? 1f : -1f;
        
        // Calculate starting X using existing melds
        // NOTE: localMeldSizes doesn't include THIS Kong yet (added after positioning)
        float meldStartX = 0f;
        for (int i = 0; i < localMeldSizes.Count; i++)
        {
            int previousMeldSize = localMeldSizes[i];
            meldStartX += directionMultiplier * ((previousMeldSize * tileSpacing) + setSpacing);
            Debug.Log($"[PositionKongSet]   Previous meld {i}: {previousMeldSize} tiles, cumulative X: {meldStartX}");
        }
        
        Debug.Log($"[PositionKongSet] Kong start X: {meldStartX}");
        
        // Position each Kong tile
        for (int i = 0; i < 4; i++)
        {
            float xPos = meldStartX + (directionMultiplier * i * tileSpacing);
            
            kongTiles[i].transform.SetParent(meldContainer);
            kongTiles[i].transform.localPosition = new Vector3(xPos, 0f, 0f);
            kongTiles[i].transform.localRotation = tileRotation;
            
            Collider collider = kongTiles[i].GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            
            Debug.Log($"[PositionKongSet]   Tile {i} at local X: {xPos}");
        }
        
        Debug.Log($"[PositionKongSet] ✓ Kong positioned with {localMeldSizes.Count} previous melds");
    }
    
    /// <summary>
    /// Get all tile GameObjects in the player's hand (for display in result screen).
    /// </summary>
    public List<GameObject> GetAllHandTiles()
    {
        List<GameObject> allTiles = new List<GameObject>(spawnedTiles);
        
        // Include drawn tile if it exists
        if (drawnTile != null && !allTiles.Contains(drawnTile))
        {
            allTiles.Add(drawnTile);
        }
        
        // Sort by tile value for proper display
        allTiles.Sort((a, b) => {
            TileData dataA = a.GetComponent<TileData>();
            TileData dataB = b.GetComponent<TileData>();
            if (dataA != null && dataB != null)
                return dataA.GetSortValue().CompareTo(dataB.GetSortValue());
            return 0;
        });
        
        return allTiles;
    }
    
    /// <summary>
    /// Get all tile GameObjects INCLUDING melded Kongs (for result screen display).
    /// This ensures even hidden Kong tiles are included for the result display.
    /// </summary>
    public List<GameObject> GetAllHandTilesIncludingKongs()
    {
        List<GameObject> allTiles = new List<GameObject>();
        
        // Add all hand tiles
        allTiles.AddRange(spawnedTiles);
        
        // Add drawn tile if present
        if (drawnTile != null)
        {
            allTiles.Add(drawnTile);
        }
        
        // Add all Kong tiles (self-declared)
        allTiles.AddRange(meldedKongTiles);
        
        // ADD: Include tiles from completed melds (Chi/Pon/Kong from discards)
        // Note: These are stored as data, need to create visuals
        foreach (var meld in completedMelds)
        {
            foreach (int sortValue in meld.TileSortValues)
            {
                GameObject tilePrefab = FindTilePrefabBySortValue(sortValue);
                if (tilePrefab != null)
                {
                    // Create a temporary visual for display
                    GameObject tempTile = Instantiate(tilePrefab);
                    allTiles.Add(tempTile);
                }
            }
        }
        
        // Add flower tiles (optional, but good for display)
        allTiles.AddRange(flowerTiles);
        
        return allTiles;
    }

    /// <summary>
    /// Find tile prefab by sort value.
    /// </summary>
    private GameObject FindTilePrefabBySortValue(int sortValue)
    {
        if (NetworkedGameManager.Instance == null) return null;

        foreach (GameObject prefab in NetworkedGameManager.Instance.TilePrefabs)
        {
            TileData data = prefab.GetComponent<TileData>();
            if (data != null && data.GetSortValue() == sortValue)
            {
                return prefab;
            }
        }
        return null;
    }

    /// <summary>
    /// Create TileData from sort value.
    /// </summary>
    private TileData CreateTileDataFromSortValue(int sortValue)
    {
        GameObject temp = new GameObject("TempTile");
        TileData data = temp.AddComponent<TileData>();
        
        int suitValue = sortValue / 100;
        int numberValue = sortValue % 100;
        
        data.suit = (MahjongSuit)suitValue;
        data.value = numberValue;
        
        return data;
    }
    
    // ===== TENPAI (WAITING HAND) PREVIEW SYSTEM =====
    
    /// <summary>
    /// Check which tiles would complete the hand if this tile is discarded.
    /// Called when player hovers over a tile.
    /// </summary>
    public void RequestTenpaiCheck(GameObject hoveredTile)
    {
        Debug.Log($"[RequestTenpaiCheck] START - hoveredTile: {hoveredTile.name}");
        Debug.Log($"[RequestTenpaiCheck] isSelectingKong: {isSelectingKong}, canDeclareWin: {canDeclareWin}");
        
        // Don't show Tenpai during Kong selection or after game ends
        if (isSelectingKong || canDeclareWin)
        {
            Debug.Log("[RequestTenpaiCheck] BLOCKED - Kong selection or Win declared");
            return;
        }
        
        // Hide any existing Tenpai UI
        if (tenpaiUIPanel != null)
        {
            Debug.Log($"[RequestTenpaiCheck] Tenpai panel found, current state: {tenpaiUIPanel.activeSelf}");
            tenpaiUIPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("[RequestTenpaiCheck] tenpaiUIPanel is NULL! Assign it in Inspector!");
            return;
        }
        
        Debug.Log("[RequestTenpaiCheck] Syncing GameObjects to PlayerHand...");
        
        // Sync GameObjects to logic
        SyncGameObjectsToPlayerHand();
        
        int currentKongs = logicHand.MeldedKongs.Count / 4;
        
        // Build current hand (13 tiles)
        List<TileData> currentHand = new List<TileData>(logicHand.HandTiles);
        if (logicHand.DrawnTile != null) currentHand.Add(logicHand.DrawnTile);
        
        Debug.Log($"[RequestTenpaiCheck] Current hand size: {currentHand.Count} tiles (should be 14)");
        Debug.Log($"[RequestTenpaiCheck] Melded Kongs: {logicHand.MeldedKongs.Count} tiles ({currentKongs} sets)");
        
        // Remove the hovered tile (simulating discarding it)
        TileData hoveredData = hoveredTile.GetComponent<TileData>();
        if (hoveredData == null)
        {
            Debug.LogError($"[RequestTenpaiCheck] Hovered tile has no TileData component!");
            return;
        }
        
        var match = currentHand.FirstOrDefault(t => t.GetSortValue() == hoveredData.GetSortValue());
        if (match != null)
        {
            currentHand.Remove(match);
            Debug.Log($"[RequestTenpaiCheck] Removed tile {hoveredData.GetSortValue()}, hand now has {currentHand.Count} tiles");
        }
        else
        {
            Debug.LogWarning($"[RequestTenpaiCheck] Could not find tile {hoveredData.GetSortValue()} in hand!");
            return; // Tile not found in hand
        }
        
        // Find all tiles that would complete the hand
        List<TileData> winningTiles = new List<TileData>();
        
        if (NetworkedGameManager.Instance == null)
        {
            Debug.LogError("[RequestTenpaiCheck] NetworkedGameManager.Instance is NULL!");
            return;
        }
        
        if (NetworkedGameManager.Instance.TilePrefabs == null)
        {
            Debug.LogError("[RequestTenpaiCheck] TilePrefabs array is NULL!");
            return;
        }
        
        Debug.Log($"[RequestTenpaiCheck] Testing {NetworkedGameManager.Instance.TilePrefabs.Length} tile prefabs...");
        
        foreach (GameObject prefab in NetworkedGameManager.Instance.TilePrefabs)
        {
            TileData candidate = prefab.GetComponent<TileData>();
            if (candidate == null) continue;
            
            // Test if adding this tile would complete the hand
            List<TileData> testHand = new List<TileData>(currentHand);
            testHand.Add(candidate);
            testHand.AddRange(logicHand.MeldedKongs);
            
            if (logicHand.IsValidMahjongHand(testHand, currentKongs))
            {
                winningTiles.Add(candidate);
                Debug.Log($"[RequestTenpaiCheck] WINNING TILE FOUND: {candidate.GetSortValue()}");
            }
        }
        
        Debug.Log($"[RequestTenpaiCheck] Total winning tiles found: {winningTiles.Count}");
        
        // Show UI if we found winning tiles
        if (winningTiles.Count > 0)
        {
            Debug.Log("[RequestTenpaiCheck] Calling ShowTenpaiUI...");
            ShowTenpaiUI(hoveredTile.transform.position, winningTiles);
        }
        else
        {
            Debug.Log("[RequestTenpaiCheck] No winning tiles found - not in Tenpai");
        }
    }
    
    /// <summary>
    /// Display the Tenpai UI showing which tiles complete the hand.
    /// </summary>
    private void ShowTenpaiUI(Vector3 tileWorldPosition, List<TileData> winningTiles)
    {
        Debug.Log($"[ShowTenpaiUI] START - winningTiles count: {winningTiles.Count}");
        
        if (tenpaiIconPrefab == null)
        {
            Debug.LogError("[ShowTenpaiUI] tenpaiIconPrefab is NULL! Assign it in Inspector!");
            return;
        }
        
        if (tenpaiUIPanel == null)
        {
            Debug.LogError("[ShowTenpaiUI] tenpaiUIPanel is NULL! Assign it in Inspector!");
            return;
        }
        
        if (tenpaiIconsContainer == null)
        {
            Debug.LogError("[ShowTenpaiUI] tenpaiIconsContainer is NULL! Assign it in Inspector!");
            return;
        }
        
        Debug.Log("[ShowTenpaiUI] All references valid, activating panel...");
        
        tenpaiUIPanel.SetActive(true);
        
        Debug.Log($"[ShowTenpaiUI] Panel activated: {tenpaiUIPanel.activeSelf}");
        
        // Position the panel above the hovered tile
        Vector3 screenPos = Camera.main.WorldToScreenPoint(tileWorldPosition);
        Debug.Log($"[ShowTenpaiUI] Tile world position: {tileWorldPosition}");
        Debug.Log($"[ShowTenpaiUI] Tile screen position: {screenPos}");
        
        RectTransform rect = tenpaiUIPanel.GetComponent<RectTransform>();
        if (rect == null)
        {
            Debug.LogError("[ShowTenpaiUI] TenpaiUIPanel has no RectTransform!");
            return;
        }
        
        rect.position = screenPos;
        rect.anchoredPosition += new Vector2(0, 100f); // Offset above tile
        
        Debug.Log($"[ShowTenpaiUI] Panel positioned at: {rect.position}");
        
        // Clear old icons
        int childCount = tenpaiIconsContainer.childCount;
        Debug.Log($"[ShowTenpaiUI] Clearing {childCount} old icons...");
        
        foreach (Transform child in tenpaiIconsContainer)
        {
            Destroy(child.gameObject);
        }
        
        Debug.Log($"[ShowTenpaiUI] Creating {winningTiles.Count} new icons...");
        
        // Create an icon for each winning tile
        int iconsCreated = 0;
        foreach (TileData tileData in winningTiles)
        {
            if (tileData.tileSprite == null)
            {
                Debug.LogWarning($"[ShowTenpaiUI] Tile {tileData.GetSortValue()} has no sprite!");
                continue;
            }
            
            // Calculate potential score
            int potentialScore = CalculatePotentialScore(tileData);
            
            Debug.Log($"[ShowTenpaiUI] Creating icon for tile {tileData.GetSortValue()} - {potentialScore} pts");
            
            // Create icon
            GameObject icon = Instantiate(tenpaiIconPrefab, tenpaiIconsContainer);
            icon.name = $"TenpaiIcon_{tileData.GetSortValue()}";
            
            Debug.Log($"[ShowTenpaiUI] Icon created: {icon.name}, Active: {icon.activeSelf}, Parent: {icon.transform.parent.name}");
            
            // Set image
            UnityEngine.UI.Image image = icon.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                image.sprite = tileData.tileSprite;
                Debug.Log($"[ShowTenpaiUI] Icon image set, enabled: {image.enabled}");
            }
            else
            {
                Debug.LogWarning($"[ShowTenpaiUI] Icon prefab has no Image component!");
            }
            
            // Create score text as SIBLING (not child) so it renders on top
            GameObject textObj = new GameObject($"ScoreText_{tileData.GetSortValue()}");
            textObj.transform.SetParent(tenpaiIconsContainer, false); // Same parent as icon
            textObj.transform.SetSiblingIndex(icon.transform.GetSiblingIndex() + 1); // Render after icon
            
            // Add TextMeshProUGUI
            TMPro.TextMeshProUGUI scoreText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            
            // Get RectTransforms
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            
            // Position text at EXACT same position as icon
            textRect.anchorMin = iconRect.anchorMin;
            textRect.anchorMax = iconRect.anchorMax;
            textRect.pivot = iconRect.pivot;
            textRect.anchoredPosition = iconRect.anchoredPosition;
            textRect.sizeDelta = iconRect.sizeDelta;
            
            Debug.Log($"[ShowTenpaiUI] Icon position: {iconRect.anchoredPosition}, size: {iconRect.sizeDelta}");
            Debug.Log($"[ShowTenpaiUI] Text position: {textRect.anchoredPosition}, size: {textRect.sizeDelta}");
            
            // Text settings
            scoreText.fontSize = 48; // HUGE font to make absolutely sure it's visible
            scoreText.fontStyle = TMPro.FontStyles.Bold;
            scoreText.alignment = TMPro.TextAlignmentOptions.Center;
            scoreText.enableAutoSizing = false;
            
            // CRITICAL: Assign a font! TMP needs a font asset to render
            // Try to load the default TMP font
            TMPro.TMP_FontAsset font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null)
            {
                // Fallback: try to find any TMP font in the project
                font = Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>().FirstOrDefault();
            }
            
            if (font != null)
            {
                scoreText.font = font;
                Debug.Log($"[ShowTenpaiUI] Assigned font: {font.name}");
            }
            else
            {
                Debug.LogError("[ShowTenpaiUI] NO TMP FONT FOUND! Text will not render!");
            }
            
            // Make absolutely sure the text object is active and enabled
            scoreText.gameObject.SetActive(true);
            scoreText.enabled = true;
            
            // Set score text and color
            scoreText.text = $"{potentialScore}";
            
            // Add outline for visibility
            scoreText.outlineWidth = 0.3f;
            scoreText.outlineColor = Color.black;
            
            // Color code high scores - use BRIGHT colors
            if (potentialScore >= 8)
            {
                scoreText.color = new Color(1f, 1f, 0f, 1f); // Bright yellow
            }
            else if (potentialScore >= 4)
            {
                scoreText.color = new Color(0f, 1f, 0f, 1f); // Bright green
            }
            else
            {
                scoreText.color = new Color(1f, 1f, 1f, 1f); // White
            }
            
            // Force text to render
            scoreText.ForceMeshUpdate();
            
            Debug.Log($"[ShowTenpaiUI] Score text configured:");
            Debug.Log($"  - GameObject: {scoreText.gameObject.name}, Active: {scoreText.gameObject.activeSelf}");
            Debug.Log($"  - Component Enabled: {scoreText.enabled}");
            Debug.Log($"  - Text: '{scoreText.text}'");
            Debug.Log($"  - Font Size: {scoreText.fontSize}");
            Debug.Log($"  - Color: {scoreText.color}");
            Debug.Log($"  - Alignment: {scoreText.alignment}");
            Debug.Log($"  - Canvas: {scoreText.canvas != null}");
            Debug.Log($"  - RectTransform: {scoreText.rectTransform.rect}");
            
            iconsCreated++;
        }
        
        Debug.Log($"[ShowTenpaiUI] COMPLETE - Created {iconsCreated} icons");
    }
    
    /// <summary>
    /// Calculate what the score would be if this tile completed the hand.
    /// </summary>
    private int CalculatePotentialScore(TileData candidate)
    {
        // Save current drawn tile
        TileData originalDrawn = logicHand.DrawnTile;
        
        // Temporarily set the candidate as the drawn tile
        logicHand.SetDrawnTile(candidate);
        
        // Analyze and score
        HandAnalysisResult analysis = logicHand.CheckForWinAndAnalyze(completedMelds);
        
        // Calculate Kong counts for accurate scoring
        int selfKongCount = logicHand.MeldedKongs.Count / 4;
        int discardKongCount = 0;
        if (completedMelds != null)
        {
            discardKongCount = completedMelds.Count(m => m.Type == InterruptActionType.Kong);
        }
        
        // Calculate score (assume Tsumo for potential score display)
        List<string> unusedMessages;
        bool isTsumo = true; // Assume Tsumo for potential score
        int completedMeldCount = completedMelds?.Count ?? 0;
        int score = logicHand.CalculateTotalScore(analysis, seatIndex, isTsumo, selfKongCount, discardKongCount, completedMeldCount, out unusedMessages, 1);
        
        // Restore original drawn tile
        logicHand.SetDrawnTile(originalDrawn);
        
        return score;
    }
    
    /// <summary>
    /// Hide the Tenpai UI panel.
    /// </summary>
    public void HideTenpaiVisuals()
    {
        if (tenpaiUIPanel != null)
        {
            tenpaiUIPanel.SetActive(false);
        }
    }

    [Header("Interrupt UI")]
    public InterruptUIManager interruptUI;

    // Interrupt state
    private int pendingDiscardedTile = 0;
    private int pendingDiscardingPlayer = -1;
    // Chi selection callbacks
    private System.Action<ChiOption> onChiTileHovered;
    private System.Action<ChiOption> onChiTileConfirmed;
    // Melds (called sets)
    public List<CompletedMeld> completedMelds = new List<CompletedMeld>();

    // --- NEW METHOD: Check for interrupt options ---

    /// <summary>
    /// Server asks client: Can you Chi/Pon/Kong this discarded tile?
    /// </summary>
    [TargetRpc]
    public void TargetCheckInterruptOptions(NetworkConnection target, int canChiFlag, int canPonFlag, int canKongFlag, int discardedTileSortValue)
    {
        // Store the discarded tile
        lastDiscardedTileSortValue = discardedTileSortValue;
        
        Debug.Log($"[PlayerHand] Server asked about interrupt for tile {discardedTileSortValue}");

        // ACTUALLY VALIDATE if we can do these actions
        bool actuallyCanChi = false;
        bool actuallyCanPon = false;
        bool actuallyCanKong = false;
        
        // Check Chi (only if server said it's possible)
        if (canChiFlag == 1)
        {
            currentChiOptions = GenerateChiOptions(lastDiscardedTileSortValue);
            actuallyCanChi = currentChiOptions.Count > 0;
            Debug.Log($"[PlayerHand] Chi check: {actuallyCanChi} ({currentChiOptions.Count} options)");
        }
        else
        {
            currentChiOptions = new List<ChiOption>();
        }
        
        // Check Pon (only if server said it's possible)
        if (canPonFlag == 1)
        {
            int matchCount = CountTileInHand(lastDiscardedTileSortValue);
            actuallyCanPon = matchCount >= 2;
            Debug.Log($"[PlayerHand] Pon check: {actuallyCanPon} (have {matchCount} tiles)");
        }
        
        // Check Kong (only if server said it's possible)
        if (canKongFlag == 1)
        {
            int matchCount = CountTileInHand(lastDiscardedTileSortValue);
            actuallyCanKong = matchCount >= 3;
            Debug.Log($"[PlayerHand] Kong check: {actuallyCanKong} (have {matchCount} tiles)");
        }

        // CRITICAL FIX: Check Ron ALWAYS, not just when other interrupts are available
        bool actuallyCanRon = CheckRonOption(lastDiscardedTileSortValue);
        Debug.Log($"[PlayerHand] Ron check: {actuallyCanRon}");

        // Show UI if we have ANY interrupt option (Chi/Pon/Kong/Ron)
        if (actuallyCanChi || actuallyCanPon || actuallyCanKong || actuallyCanRon)
        {
            Debug.Log($"[PlayerHand] ✓ Showing interrupt UI - Chi:{actuallyCanChi}, Pon:{actuallyCanPon}, Kong:{actuallyCanKong}, Ron:{actuallyCanRon}");
            
            if (interruptUI != null)
            {
                interruptUI.ShowInterruptOptions(actuallyCanChi, actuallyCanPon, actuallyCanKong, actuallyCanRon, currentChiOptions, OnInterruptDecision);
            }
        }
        else
        {
            // Auto-pass only if we have NO valid options (including Ron)
            Debug.Log($"[PlayerHand] No valid interrupt options (including Ron), auto-passing");
            CmdRespondToInterrupt(InterruptActionType.Pass);
        }
    }

    public void OnInterruptDecision(InterruptActionType decision)
    {
        Debug.Log($"==========================================");
        Debug.Log($"[PlayerHand] OnInterruptDecision: {decision}");
        
        // For Chi with multiple options, InterruptUIManager handles selection
        // So we should NOT enter selection mode here - just send the response
        
        if (decision == InterruptActionType.Chi)
        {
            Debug.Log($"[PlayerHand] Chi decision received");
            Debug.Log($"[PlayerHand] selectedChiOption is null: {selectedChiOption == null}");
            
            // The Chi option should already be selected by InterruptUIManager
            if (selectedChiOption != null)
            {
                Debug.Log($"[PlayerHand] Sending Chi response to server");
                CmdRespondToInterrupt(decision);
            }
            else
            {
                Debug.LogError($"[PlayerHand] Chi decision but no selectedChiOption!");
            }
        }
        else if (decision == InterruptActionType.Ron)
        {
            Debug.Log($"[PlayerHand] Ron selected!");
            ExecuteRon();
        }
        else
        {
            Debug.Log($"[PlayerHand] Sending {decision} response to server");
            CmdRespondToInterrupt(decision);
        }
        
        Debug.Log($"==========================================");
    }

    /// <summary>
    /// Execute Ron - win with opponent's discard
    /// </summary>
    private void ExecuteRon()
    {
        Debug.Log($"[PlayerHand] ===== EXECUTING RON =====");
        Debug.Log($"[PlayerHand] Ron tile: {pendingRonTile}");
        
        // CRITICAL: DO NOT modify logicHand.DrawnTile here
        // The hand state should already be correct for win analysis
        
        // Create a temporary drawn tile for analysis WITHOUT modifying the actual hand
        TileData originalDrawn = logicHand.DrawnTile;
        TileData ronTileData = CreateTileDataFromSortValue(pendingRonTile);
        
        // Temporarily set the Ron tile
        logicHand.SetDrawnTile(ronTileData);
        
        // Analyze with completed melds (Chi/Pon/Kong from discards)
        HandAnalysisResult analysis = logicHand.CheckForWinAndAnalyze(completedMelds);
        
        Debug.Log($"[PlayerHand] Ron analysis - IsWinning: {analysis.IsWinningHand}");
        
        if (!analysis.IsWinningHand)
        {
            Debug.LogError($"[PlayerHand] ERROR: Ron tile does not create winning hand!");
            logicHand.SetDrawnTile(originalDrawn);
            return;
        }
        
        // Get all tile sort values for display (including Chi/Pon/Kong melds)
        List<int> allTileSortValues = new List<int>();
        
        // Add hand tiles
        allTileSortValues.AddRange(logicHand.HandTiles.Select(t => t.GetSortValue()));
        
        // Add Ron tile
        allTileSortValues.Add(pendingRonTile);
        
        // Add melded kongs
        allTileSortValues.AddRange(logicHand.MeldedKongs.Select(t => t.GetSortValue()));
        
        // Add completed melds (Chi/Pon/Kong from discards)
        if (completedMelds != null)
        {
            foreach (var meld in completedMelds)
            {
                allTileSortValues.AddRange(meld.TileSortValues);
            }
        }
        
        // Calculate Kong counts
        int selfKongCount = logicHand.MeldedKongs.Count / 4;
        int discardKongCount = 0;
        if (completedMelds != null)
        {
            discardKongCount = completedMelds.Count(m => m.Type == InterruptActionType.Kong);
        }
        
        Debug.Log($"[Ron] Self-Kongs: {selfKongCount}, Discard-Kongs: {discardKongCount}");
        
        
        // Calculate score with Ron (no Tsumo bonus) and Kong bonuses
        List<string> flowerMessages;
        bool isTsumo = false; // This is Ron (win on discard)
        int completedMeldCount = completedMelds?.Count ?? 0;
        int score = logicHand.CalculateTotalScore(analysis, seatIndex, isTsumo, selfKongCount, discardKongCount, completedMeldCount, out flowerMessages, 1);
        Debug.Log($"[PlayerHand] Ron score: {score}");
        Debug.Log($"[PlayerHand] Total tiles: {allTileSortValues.Count}");
        Debug.Log($"[PlayerHand] Flower messages count: {flowerMessages?.Count ?? 0}");
        
        // Restore original drawn tile
        logicHand.SetDrawnTile(originalDrawn);
        
        // Tell server we won with Ron
        CmdDeclareRon(seatIndex, pendingRonTile, analysis, score, allTileSortValues, flowerMessages);
        
        // Clear the pending Ron tile
        pendingRonTile = -1;
    }

    /// <summary>
    /// Check if we can win (Ron) with the discarded tile
    /// </summary>
    /// <summary>
    /// Check if we can win (Ron) with the discarded tile
    /// </summary>
    private bool CheckRonOption(int discardedTile)
    {
        // === COMPREHENSIVE DEBUGGING ===
        Debug.Log($"[CheckRonOption] ================================");
        Debug.Log($"[CheckRonOption] FULL HAND ANALYSIS FOR RON");
        Debug.Log($"[CheckRonOption] ================================");
        
        // Log all concealed tiles
        var concealedTiles = logicHand.HandTiles.Select(t => t.GetSortValue()).OrderBy(v => v).ToList();
        Debug.Log($"[CheckRonOption] Concealed tiles ({concealedTiles.Count}): {string.Join(", ", concealedTiles)}");
        
        // Log Ron tile
        Debug.Log($"[CheckRonOption] Ron tile: {discardedTile}");
        
        // Log all completed melds
        if (completedMelds != null && completedMelds.Count > 0)
        {
            Debug.Log($"[CheckRonOption] Completed melds ({completedMelds.Count}):");
            for (int i = 0; i < completedMelds.Count; i++)
            {
                var meld = completedMelds[i];
                string tiles = string.Join(", ", meld.TileSortValues);
                Debug.Log($"[CheckRonOption]   Meld {i}: {meld.Type} - [{tiles}] (called: {meld.CalledTileSortValue})");
            }
        }
        
        // Log melded Kongs
        if (logicHand.MeldedKongs.Count > 0)
        {
            var kongTiles = logicHand.MeldedKongs.Select(t => t.GetSortValue()).OrderBy(v => v).ToList();
            Debug.Log($"[CheckRonOption] Self-declared Kongs ({logicHand.MeldedKongs.Count / 4} sets): {string.Join(", ", kongTiles)}");
        }
        
        // Calculate total
        int totalTiles = concealedTiles.Count + 1 + logicHand.MeldedKongs.Count + 
                         (completedMelds?.Sum(m => m.TileSortValues.Count) ?? 0);
        Debug.Log($"[CheckRonOption] Total tiles in play: {totalTiles}");
        Debug.Log($"[CheckRonOption]   = {concealedTiles.Count} concealed + 1 Ron + {logicHand.MeldedKongs.Count} Kong + {completedMelds?.Sum(m => m.TileSortValues.Count) ?? 0} melds");
        Debug.Log($"[CheckRonOption] ================================");

        Debug.Log($"[CheckRonOption] ===== CHECKING RON =====");
        Debug.Log($"[CheckRonOption] Discarded tile: {discardedTile}");
        
        Debug.Log($"[CheckRonOption] Hand tiles count: {logicHand.HandTiles.Count}");
        Debug.Log($"[CheckRonOption] Hand tiles: {string.Join(", ", logicHand.HandTiles.Select(t => t.GetSortValue()))}");
        
        // CRITICAL FIX: Temporarily set the Ron tile as drawn tile
        // CheckForWinAndAnalyze expects drawnTile to be set, NOT added to HandTiles
        TileData originalDrawnTile = logicHand.DrawnTile;
        TileData discardedTileData = CreateTileDataFromSortValue(discardedTile);
        
        // Set as drawn tile (NOT added to HandTiles)
        logicHand.SetDrawnTile(discardedTileData);
        
        // Calculate expected tile count
        int completedMeldCount = completedMelds?.Count ?? 0;
        int selfDeclaredKongCount = logicHand.MeldedKongs.Count / 4;
        int setsNeededFromHand = 4 - completedMeldCount - selfDeclaredKongCount;
        
        Debug.Log($"[CheckRonOption] Completed melds: {completedMeldCount}");
        Debug.Log($"[CheckRonOption] Self-declared Kongs: {selfDeclaredKongCount}");
        Debug.Log($"[CheckRonOption] Sets needed from hand: {setsNeededFromHand}");
        Debug.Log($"[CheckRonOption] Hand tiles: {logicHand.HandTiles.Count}");
        Debug.Log($"[CheckRonOption] Drawn tile (Ron tile): {discardedTile}");
        
        // Use CheckForWinAndAnalyze which properly handles completed melds
        Debug.Log($"[CheckRonOption] Calling CheckForWinAndAnalyze...");
        Debug.Log($"[CheckRonOption] Hand structure:");
        Debug.Log($"[CheckRonOption]   - Hand tiles: {logicHand.HandTiles.Count}");
        Debug.Log($"[CheckRonOption]   - Drawn tile: {(logicHand.DrawnTile != null ? logicHand.DrawnTile.GetSortValue().ToString() : "NULL")}");
        Debug.Log($"[CheckRonOption]   - Melded Kongs: {logicHand.MeldedKongs.Count / 4} sets");
        Debug.Log($"[CheckRonOption]   - Completed melds: {completedMeldCount} sets");
        
        // Create a detailed tile list
        List<int> allTileValues = logicHand.HandTiles.Select(t => t.GetSortValue()).ToList();
        if (logicHand.DrawnTile != null) allTileValues.Add(logicHand.DrawnTile.GetSortValue());
        allTileValues.Sort();
        Debug.Log($"[CheckRonOption]   - All tiles (sorted): {string.Join(", ", allTileValues)}");
        
        HandAnalysisResult analysis = logicHand.CheckForWinAndAnalyze(completedMelds);
        bool canWin = analysis.IsWinningHand;
        
        Debug.Log($"[CheckRonOption] Analysis result:");
        Debug.Log($"[CheckRonOption]   - IsWinningHand: {canWin}");
        Debug.Log($"[CheckRonOption]   - IsTraditionalWin: {analysis.IsTraditionalWin}");
        Debug.Log($"[CheckRonOption]   - Is13Orphans: {analysis.Is13OrphansWin}");
        Debug.Log($"[CheckRonOption]   - Is7Pairs: {analysis.Is7PairsWin}");
        Debug.Log($"[CheckRonOption]   - IsPureHand: {analysis.IsPureHand}");
        
        // Restore original drawn tile
        logicHand.SetDrawnTile(originalDrawnTile);
        
        if (canWin)
        {
            // CRITICAL: Check if score would be > 0 after all bonuses
            // Calculate Kong counts
            int selfKongCount = logicHand.MeldedKongs.Count / 4;
            int discardKongCount = 0;
            if (completedMelds != null)
            {
                discardKongCount = completedMelds.Count(m => m.Type == InterruptActionType.Kong);
            }
            
            List<string> flowerMessages;
            bool isTsumo = false; // Ron check (no Tsumo bonus)
            // NOTE: completedMeldCount already declared at line 1850, reuse it
            int finalScore = logicHand.CalculateTotalScore(analysis, seatIndex, isTsumo, selfKongCount, discardKongCount, completedMeldCount, out flowerMessages, 1);
            
            if (finalScore > 0)
            {
                // Store the pending Ron tile
                pendingRonTile = discardedTile;
                Debug.Log($"[CheckRonOption] ✓✓✓ RON AVAILABLE with tile {discardedTile}! Score: {finalScore}");
                Debug.Log($"[CheckRonOption] Win type: 13Orphans={analysis.Is13OrphansWin}, 7Pairs={analysis.Is7PairsWin}, Traditional={analysis.IsTraditionalWin}, Pure={analysis.IsPureHand}");
                Debug.Log($"[CheckRonOption] Flower messages: {string.Join("; ", flowerMessages)}");
                return true;
            }
            else
            {
                Debug.Log($"[CheckRonOption] ✗ Ron BLOCKED - score would be {finalScore} (must be > 0)");
                Debug.Log($"[CheckRonOption] Flower messages: {string.Join("; ", flowerMessages)}");
                return false;
            }
        }
        else
        {
            Debug.Log($"[CheckRonOption] ✗ Ron NOT available - hand analysis failed");
            return false;
        }
    }

    [Command]
    private void CmdRespondToInterrupt(InterruptActionType action)
    {
        Debug.Log($"[PlayerHand] Sending response: {action}");
        
        if (action == InterruptActionType.Chi && selectedChiOption != null)
        {
            // Store the Chi option on the server
            CmdStoreChiOption(selectedChiOption.discardedTile, selectedChiOption.tile1SortValue, selectedChiOption.tile2SortValue);
        }
        
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.PlayerRespondedToInterrupt(seatIndex, action);
        }
    }

[Command]
private void CmdStoreChiOption(int discarded, int tile1, int tile2)
{
    // Store on server so ExecuteInterrupt can use it
    selectedChiOption = new ChiOption(discarded, tile1, tile2);
    Debug.Log($"[PlayerHand] Server stored Chi option: {tile1}, {tile2}, {discarded}");
}

    // --- DETECTION METHODS ---

    /// <summary>
    /// Can this player Pon the discarded tile? (need 2 matching)
    /// </summary>
    private bool CanPon(int discardedTile)
    {
        int count = spawnedTiles.Count(t => t.GetComponent<TileData>().GetSortValue() == discardedTile);
        return count >= 2;
    }

    /// <summary>
    /// Can this player Kong the discarded tile? (need 3 matching)
    /// </summary>
    private bool CanKong(int discardedTile)
    {
        int count = spawnedTiles.Count(t => t.GetComponent<TileData>().GetSortValue() == discardedTile);
        return count >= 3;
    }

    /// <summary>
    /// Can this player Chi the discarded tile? (must be from previous player)
    /// </summary>
    private List<ChiOption> CanChi(int discardedTile, int discardingPlayer, int myPlayerIndex)
    {
        List<ChiOption> options = new List<ChiOption>();
        
        // Get actual player count from game manager
        int playerCount = NetworkedGameManager.Instance.GetPlayerCount();

        // Add safety check
        if (playerCount == 0)
        {
            Debug.LogError("[CanChi] Player count is 0! Using default of 2.");
            playerCount = 2;
        }
        
        // Chi only from player to your left (player before you in turn order)
        // In 2-player: Player 0's left is Player 1, Player 1's left is Player 0
        // In 4-player: Player 0's left is Player 3, Player 1's left is Player 0, etc.
        int playerToLeft = (myPlayerIndex - 1 + playerCount) % playerCount;
        
        Debug.Log($"[CanChi] My seat: {myPlayerIndex}, Discarding player: {discardingPlayer}, Player to my left: {playerToLeft}, Player count: {playerCount}");
        
        if (discardingPlayer != playerToLeft)
        {
            Debug.Log($"[CanChi] Cannot Chi - tile not from player to my left");
            return options; // Can't Chi from this player
        }
        
        // Chi only works with numbered tiles
        int suit = discardedTile / 100;
        if (suit < 1 || suit > 3)
        {
            Debug.Log($"[CanChi] Cannot Chi - not a numbered tile (suit: {suit})");
            return options;
        }
        
        int value = discardedTile % 100;
        
        // Get tile counts
        Dictionary<int, int> counts = new Dictionary<int, int>();
        foreach (GameObject tile in spawnedTiles)
        {
            int sv = tile.GetComponent<TileData>().GetSortValue();
            if (!counts.ContainsKey(sv)) counts[sv] = 0;
            counts[sv]++;
        }
        
        // Check 3 possible sequences
        
        // Option 1: [N-2][N-1][N]
        if (value >= 3)
        {
            int t1 = discardedTile - 2;
            int t2 = discardedTile - 1;
            if (counts.ContainsKey(t1) && counts[t1] >= 1 &&
                counts.ContainsKey(t2) && counts[t2] >= 1)
            {
                options.Add(new ChiOption(discardedTile, t1, t2));
                Debug.Log($"[CanChi] Option found: {t1}, {t2}, {discardedTile}");
            }
        }
        
        // Option 2: [N-1][N][N+1]
        if (value >= 2 && value <= 8)
        {
            int t1 = discardedTile - 1;
            int t2 = discardedTile + 1;
            if (counts.ContainsKey(t1) && counts[t1] >= 1 &&
                counts.ContainsKey(t2) && counts[t2] >= 1)
            {
                options.Add(new ChiOption(discardedTile, t1, t2));
                Debug.Log($"[CanChi] Option found: {t1}, {discardedTile}, {t2}");
            }
        }
        
        // Option 3: [N][N+1][N+2]
        if (value <= 7)
        {
            int t1 = discardedTile + 1;
            int t2 = discardedTile + 2;
            if (counts.ContainsKey(t1) && counts[t1] >= 1 &&
                counts.ContainsKey(t2) && counts[t2] >= 1)
            {
                options.Add(new ChiOption(discardedTile, t1, t2));
                Debug.Log($"[CanChi] Option found: {discardedTile}, {t1}, {t2}");
            }
        }
        
        Debug.Log($"[CanChi] Total Chi options: {options.Count}");
        return options;
    }

    [Command]
    private void CmdDeclareRon(int playerIndex, int ronTile, HandAnalysisResult analysis, int score, List<int> tileSortValues, List<string> flowerMessages)
    {
        Debug.Log($"[Server] CmdDeclareRon - Player {playerIndex}, Tile {ronTile}, Score {score}");
        Debug.Log($"[Server] Flower messages count: {flowerMessages?.Count ?? 0}");
        
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.PlayerDeclaredRon(playerIndex, ronTile, analysis, score, tileSortValues, flowerMessages);
        }
    }

    // --- EXECUTE INTERRUPT ---

    /// <summary>
    /// Server tells client: Execute your interrupt action
    /// </summary>
    [TargetRpc]
    public void TargetExecuteInterrupt(NetworkConnection target, InterruptActionType action, int discardedTile)
    {
        Debug.Log($"[PlayerHand] Executing {action} with tile {discardedTile}");
        
        if (action == InterruptActionType.Chi)
        {
            if (selectedChiOption != null)
            {
                ExecuteChi(discardedTile, selectedChiOption);
            }
        }
        else if (action == InterruptActionType.Pon)
        {
            ExecutePon(discardedTile);
        }
        else if (action == InterruptActionType.Kong)
        {
            // IMPORTANT: Use the interrupt version, not the self-declared version
            ExecuteKongFromDiscard(discardedTile);
        }
    }

    /// <summary>
    /// Execute Pon - take 2 from hand + discarded tile
    /// </summary>
    private void ExecutePon(int discardedTile)
    {
        Debug.Log($"[PlayerHand] Executing Pon with tile {discardedTile}");
        
        // Find 2 matching tiles in hand
        List<GameObject> matchingTiles = spawnedTiles
            .Where(t => t.GetComponent<TileData>().GetSortValue() == discardedTile)
            .Take(2)
            .ToList();
        
        if (matchingTiles.Count != 2)
        {
            Debug.LogError($"[PlayerHand] Could not find 2 tiles for Pon! Found {matchingTiles.Count}");
            return;
        }
        
        // Remove from hand
        foreach (GameObject tile in matchingTiles)
        {
            spawnedTiles.Remove(tile);
        }
        
        // Create meld with ALL 3 tiles
        List<int> meldTiles = new List<int> { discardedTile, discardedTile, discardedTile };
        CompletedMeld meld = new CompletedMeld(InterruptActionType.Pon, meldTiles, discardedTile);
        completedMelds.Add(meld);
        
        // Create visual for the called tile
        List<GameObject> meldObjects = new List<GameObject>(matchingTiles);
        GameObject calledTileObject = CreateTileVisual(discardedTile);
        if (calledTileObject != null)
        {
            meldObjects.Add(calledTileObject);
        }
        
        PositionMeld(meldObjects, discardedTile);
        RepositionTiles();

        // NEW: Send meld data to server for broadcasting
        int newConcealedCount = spawnedTiles.Count;
        CmdSendCompletedMeld(InterruptActionType.Pon, discardedTile, meldTiles, newConcealedCount);
        
        Debug.Log($"[PlayerHand] Pon complete - meld has {meldObjects.Count} tiles");
    }

    /// <summary>
    /// Execute Kong from discard
    /// </summary>
    private void ExecuteKongFromDiscard(int discardedTile)
    {
        Debug.Log($"[PlayerHand] Executing Kong (from discard) with tile {discardedTile}");
        
        // Find 3 matching tiles in hand
        List<GameObject> matchingTiles = spawnedTiles
            .Where(t => t != null && t.GetComponent<TileData>().GetSortValue() == discardedTile)
            .Take(3)
            .ToList();
        
        if (matchingTiles.Count != 3)
        {
            Debug.LogError($"[PlayerHand] Could not find 3 tiles for Kong! Found {matchingTiles.Count}");
            return;
        }
        
        // Remove from hand
        foreach (GameObject tile in matchingTiles)
        {
            spawnedTiles.Remove(tile);
        }
        
        // Create meld with ALL 4 tiles
        List<int> meldTiles = new List<int> { discardedTile, discardedTile, discardedTile, discardedTile };
        CompletedMeld meld = new CompletedMeld(InterruptActionType.Kong, meldTiles, discardedTile);
        completedMelds.Add(meld);
        
        // Create visual for the called tile
        List<GameObject> meldObjects = new List<GameObject>(matchingTiles);
        GameObject calledTileObject = CreateTileVisual(discardedTile);
        if (calledTileObject != null)
        {
            meldObjects.Add(calledTileObject);
        }
        
        // Position the meld
        PositionMeld(meldObjects, discardedTile);
        
        // Reposition remaining hand
        RepositionTiles();
        int newConcealedCount = spawnedTiles.Count;
        CmdSendCompletedMeld(InterruptActionType.Kong, discardedTile, meldTiles, newConcealedCount);
    
        Debug.Log($"[PlayerHand] Kong complete - meld has {meldObjects.Count} tiles");
    }

    /// <summary>
    /// Execute Chi
    /// </summary>
    private void ExecuteChi(int discardedTile, ChiOption option)
    {
        if (option == null)
        {
            Debug.LogError("[PlayerHand] Chi option is NULL!");
            return;
        }
        
        Debug.Log($"[PlayerHand] Executing Chi with option: tile1={option.tile1SortValue}, tile2={option.tile2SortValue}, discarded={option.discardedTile}");
        
        // CRITICAL FIX: Find EXACTLY one of each tile from the ChiOption
        List<GameObject> tilesToRemove = new List<GameObject>();
        GameObject tile1Object = null;
        GameObject tile2Object = null;
        
        // First pass: Find tile1
        foreach (GameObject tile in spawnedTiles)
        {
            if (tile == null) continue;
            int sv = tile.GetComponent<TileData>().GetSortValue();
            
            if (sv == option.tile1SortValue && tile1Object == null)
            {
                tile1Object = tile;
                Debug.Log($"[ExecuteChi] Found tile1: {sv}");
                break; // Found tile1, stop searching
            }
        }
        
        // Second pass: Find tile2 (must be different object from tile1)
        foreach (GameObject tile in spawnedTiles)
        {
            if (tile == null) continue;
            if (tile == tile1Object) continue; // Skip the tile we already selected
            
            int sv = tile.GetComponent<TileData>().GetSortValue();
            
            if (sv == option.tile2SortValue && tile2Object == null)
            {
                tile2Object = tile;
                Debug.Log($"[ExecuteChi] Found tile2: {sv}");
                break; // Found tile2, stop searching
            }
        }
        
        // Validate we found both tiles
        if (tile1Object == null || tile2Object == null)
        {
            Debug.LogError($"[ExecuteChi] ERROR: Could not find required tiles!");
            Debug.LogError($"[ExecuteChi] tile1Object ({option.tile1SortValue}): {(tile1Object != null ? "FOUND" : "NULL")}");
            Debug.LogError($"[ExecuteChi] tile2Object ({option.tile2SortValue}): {(tile2Object != null ? "FOUND" : "NULL")}");
            Debug.LogError($"[ExecuteChi] Hand contains: {string.Join(", ", spawnedTiles.Select(t => t.GetComponent<TileData>().GetSortValue()))}");
            return;
        }
        
        tilesToRemove.Add(tile1Object);
        tilesToRemove.Add(tile2Object);
        
        Debug.Log($"[ExecuteChi] Successfully selected 2 tiles for Chi");
        
        // Remove from hand
        foreach (GameObject tile in tilesToRemove)
        {
            spawnedTiles.Remove(tile);
        }
        
        // VALIDATION: Verify we're removing the correct tiles
        int removedTile1 = tilesToRemove[0].GetComponent<TileData>().GetSortValue();
        int removedTile2 = tilesToRemove[1].GetComponent<TileData>().GetSortValue();
        
        Debug.Log($"[ExecuteChi] Removing tiles: {removedTile1}, {removedTile2}");
        
        // Verify these match the ChiOption (order doesn't matter)
        bool hasCorrectTiles = 
            (removedTile1 == option.tile1SortValue && removedTile2 == option.tile2SortValue) ||
            (removedTile1 == option.tile2SortValue && removedTile2 == option.tile1SortValue);
        
        if (!hasCorrectTiles)
        {
            Debug.LogError($"[ExecuteChi] CRITICAL: Tiles don't match ChiOption!");
            Debug.LogError($"[ExecuteChi] Expected: {option.tile1SortValue}, {option.tile2SortValue}");
            Debug.LogError($"[ExecuteChi] Got: {removedTile1}, {removedTile2}");
            return;
        }
        
        // Create the meld INCLUDING the called tile
        List<int> meldTiles = option.GetSequenceSorted(); // All 3 tiles
        CompletedMeld meld = new CompletedMeld(InterruptActionType.Chi, meldTiles, discardedTile);
        completedMelds.Add(meld);
        
        // Position the meld
        List<GameObject> meldObjects = new List<GameObject>(tilesToRemove);
        GameObject calledTileObject = CreateTileVisual(discardedTile);
        if (calledTileObject != null)
        {
            meldObjects.Add(calledTileObject);
        }
        
        PositionMeld(meldObjects, discardedTile);
        RepositionTiles();
        
        // NEW: Send meld data to server for broadcasting
        int newConcealedCount = spawnedTiles.Count;
        CmdSendCompletedMeld(InterruptActionType.Chi, discardedTile, meldTiles, newConcealedCount);
        
        Debug.Log($"[PlayerHand] Chi complete - meld has {meldObjects.Count} tiles");

        // Store Chi tiles for server broadcasting
        lastChiTiles = new List<int>(meldTiles);

        // Send to server
        CmdNotifyChiComplete(meldTiles);
    }

    /// <summary>
    /// Position a completed meld in the meld area
    /// </summary>
    private void PositionMeld(List<GameObject> meldTiles, int calledTile)
    {
        // Find the KongArea for OUR seat
        string containerName = $"KongArea_Seat{seatIndex}";
        GameObject containerObj = GameObject.Find(containerName);
        
        if (containerObj == null)
        {
            Debug.LogError($"[PlayerHand] Could not find {containerName}!");
            return;
        }
        
        Transform meldContainer = containerObj.transform;
        
        Debug.Log($"[PlayerHand] Positioning meld in {containerName}");
        
        float setSpacing = 0.05f;
        float tileSpacing = 0.12f;
        
        // ===== FIX: Rotation based on seat =====
        // Player 0: No rotation (0°) - tiles face the player normally
        // Players 1, 2, 3: 180° rotation - tiles face backward toward player
        Quaternion tileRotation = (seatIndex == 0) ? 
        Quaternion.identity : 
        Quaternion.Euler(0f, 180f, 0f);
        
        // ===== FIX: Direction multiplier =====
        // Player 0: tiles go in positive direction (+1)
        // Players 1, 2, 3: tiles go in NEGATIVE direction (-1)
        float directionMultiplier = (seatIndex == 0) ? 1f : -1f;
        
        // CRITICAL FIX: Calculate starting X using ALL meld sizes (including self-Kongs)
        float meldStartX = 0f;
        for (int i = 0; i < localMeldSizes.Count; i++)
        {
            int previousMeldSize = localMeldSizes[i];
            // Apply direction to spacing
            meldStartX += directionMultiplier * ((previousMeldSize * tileSpacing) + setSpacing);
            Debug.Log($"[PlayerHand]   Previous meld {i}: {previousMeldSize} tiles, cumulative X: {meldStartX}");
        }
        
        Debug.Log($"[PlayerHand] Total existing melds: {localMeldSizes.Count}, Start X: {meldStartX}, Direction: {directionMultiplier}");
        
        // Position tiles with direction multiplier
        for (int i = 0; i < meldTiles.Count; i++)
        {
            GameObject tile = meldTiles[i];
            float xPos = meldStartX + (directionMultiplier * i * tileSpacing);
            
            tile.transform.SetParent(meldContainer);
            tile.transform.localPosition = new Vector3(xPos, 0f, 0f);
            tile.transform.localRotation = tileRotation;
            
            Collider collider = tile.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            
            Debug.Log($"[PlayerHand]   Tile {i} at local X: {xPos}");
        }
        
        // NEW: Track this meld's size
        localMeldSizes.Add(meldTiles.Count);
        Debug.Log($"[PlayerHand] ✓ Positioned {meldTiles.Count} tiles. Total melds tracked: {localMeldSizes.Count}");
    }

    /// <summary>
    /// Highlight tiles that can be used for Chi
    /// </summary>
    private void HighlightChiTiles(List<ChiOption> options)
    {
        Debug.Log($"[PlayerHand] HighlightChiTiles called with {options.Count} options");
        
        // Get all tiles that can be used for ANY Chi option
        HashSet<int> validTileValues = new HashSet<int>();
        
        foreach (ChiOption option in options)
        {
            List<int> sequence = option.GetSequenceSorted();
            foreach (int sortValue in sequence)
            {
                // Don't highlight the called tile (it's not in hand)
                if (sortValue != lastDiscardedTileSortValue)
                {
                    validTileValues.Add(sortValue);
                }
            }
        }
        
        Debug.Log($"[PlayerHand] Valid tiles to highlight: {string.Join(", ", validTileValues)}");
        
        // Highlight valid tiles, dim others
        var allTiles = spawnedTiles.ToList();
        if (drawnTile != null) allTiles.Add(drawnTile);

        foreach (GameObject tile in allTiles)
        {
            if (tile == null) continue;
            
            Renderer r = tile.GetComponentInChildren<Renderer>();
            if (r == null) continue;

            int val = tile.GetComponent<TileData>()?.GetSortValue() ?? -1;

            if (validTileValues.Contains(val))
            {
                r.material.color = Color.white; // Bright - can click
                Debug.Log($"[PlayerHand]   Highlighting tile {val}");
            }
            else
            {
                r.material.color = new Color(0.3f, 0.3f, 0.3f, 1.0f); // Dim - can't click
            }
        }
    }

    /// <summary>
    /// Exit Chi selection mode
    /// </summary>
    private void ExitChiSelectionMode()
    {
        _isInChiSelectionMode = false;
        currentChiOptions.Clear();
        
        // Reset tile colors
        foreach (GameObject tile in spawnedTiles)
        {
            Renderer renderer = tile.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.white;
            }
        }
    }

    private GameObject CreateTileVisual(int sortValue)
    {
        GameObject tilePrefab = FindTilePrefabBySortValue(sortValue);
        if (tilePrefab == null)
        {
            Debug.LogError($"[PlayerHand] Could not find tile prefab for {sortValue}");
            return null;
        }
        
        // Create the tile (parent will be set in PositionMeld)
        GameObject tileObject = Instantiate(tilePrefab);
        
        return tileObject;
    }

    private void OnChiOptionSelected(ChiOption option)
    {
        selectedChiOption = option;
        Debug.Log($"[PlayerHand] Chi option selected: {option.tile1SortValue}, {option.tile2SortValue}, {option.discardedTile}");
        
        // Exit selection mode and send response
        ExitChiSelectionMode();
        CmdRespondToInterrupt(InterruptActionType.Chi);
    }

    private void OnChiOptionHovered(ChiOption option)
    {
        if (option == null || interruptUI == null) return;
        
        List<int> sequence = option.GetSequenceSorted();
        Debug.Log($"[PlayerHand] Hovering over Chi option: {sequence[0]}, {sequence[1]}, {sequence[2]}");
        
        // Get the position of the first tile in the sequence
        Vector3 tilePosition = Vector3.zero;
        foreach (GameObject tile in spawnedTiles)
        {
            if (tile != null && tile.GetComponent<TileData>().GetSortValue() == sequence[0])
            {
                tilePosition = tile.transform.position;
                break;
            }
        }
        
        interruptUI.ShowChiPreview(option, tilePosition);
    }

    /// <summary>
    /// Display opponent's meld (called by RPC from server)
    /// </summary>
    public void ShowMeldFromServer(InterruptActionType meldType, int calledTile, List<int> allTileSortValues)
    {
        Debug.Log($"[PlayerHand] ShowMeldFromServer called");
        Debug.Log($"[PlayerHand]   - Seat: {seatIndex}");
        Debug.Log($"[PlayerHand]   - MeldType: {meldType}");
        Debug.Log($"[PlayerHand]   - isOwned: {isOwned}");
        Debug.Log($"[PlayerHand]   - Tiles: {string.Join(",", allTileSortValues)}");
        
        // Skip if this is OUR OWN hand (we already executed locally)
        if (isOwned)
        {
            Debug.Log($"[PlayerHand] ✓ This is our hand - meld already shown locally, skipping RPC");
            return;
        }
        
        Debug.Log($"[PlayerHand] ✓ This is an opponent's hand - creating meld visuals");
        
        // Create visual tiles
        List<GameObject> meldTiles = new List<GameObject>();
        
        foreach (int sortValue in allTileSortValues)
        {
            GameObject prefab = FindTilePrefabBySortValue(sortValue);
            if (prefab != null)
            {
                GameObject tile = Instantiate(prefab);
                meldTiles.Add(tile);
                Debug.Log($"[PlayerHand]   ✓ Created tile {sortValue}");
            }
            else
            {
                Debug.LogError($"[PlayerHand]   ✗ Could not find prefab for tile {sortValue}");
            }
        }
        
        if (meldTiles.Count == 0)
        {
            Debug.LogError($"[PlayerHand] ✗ CRITICAL: No tiles created for opponent meld!");
            return;
        }
        
        // Record the meld
        CompletedMeld meld = new CompletedMeld(meldType, allTileSortValues, calledTile);
        completedMelds.Add(meld);
        
        // Position it
        PositionMeld(meldTiles, calledTile);
        
        Debug.Log($"[PlayerHand] ✓✓✓ SUCCESS: Opponent meld displayed - {meldTiles.Count} tiles in KongArea_Seat{seatIndex}");
    }

    /// <summary>
    /// Generate all possible Chi (sequence) options using the discarded tile.
    /// </summary>
    private List<ChiOption> GenerateChiOptions(int discardedTile)
    {
        List<ChiOption> options = new List<ChiOption>();
        
        // Get tile counts from hand
        Dictionary<int, int> handCounts = new Dictionary<int, int>();
        foreach (GameObject tile in spawnedTiles)
        {
            if (tile == null) continue;
            int sortValue = tile.GetComponent<TileData>().GetSortValue();
            if (!handCounts.ContainsKey(sortValue))
                handCounts[sortValue] = 0;
            handCounts[sortValue]++;
        }
        
        // Can only Chi numbered tiles (not honors)
        int suitBase = discardedTile / 100;
        if (suitBase < 1 || suitBase > 3) return options; // Not a numbered suit
        
        // Pattern 1: [X-2][X-1][X] where X is discarded
        if (handCounts.ContainsKey(discardedTile - 2) && handCounts[discardedTile - 2] > 0 &&
            handCounts.ContainsKey(discardedTile - 1) && handCounts[discardedTile - 1] > 0)
        {
            options.Add(new ChiOption(discardedTile, discardedTile - 2, discardedTile - 1));
            Debug.Log($"[GenerateChiOptions] Option 1: {discardedTile - 2}, {discardedTile - 1}, {discardedTile}");
        }
        
        // Pattern 2: [X-1][X][X+1] where X is discarded
        if (handCounts.ContainsKey(discardedTile - 1) && handCounts[discardedTile - 1] > 0 &&
            handCounts.ContainsKey(discardedTile + 1) && handCounts[discardedTile + 1] > 0)
        {
            options.Add(new ChiOption(discardedTile, discardedTile - 1, discardedTile + 1));
            Debug.Log($"[GenerateChiOptions] Option 2: {discardedTile - 1}, {discardedTile}, {discardedTile + 1}");
        }
        
        // Pattern 3: [X][X+1][X+2] where X is discarded
        if (handCounts.ContainsKey(discardedTile + 1) && handCounts[discardedTile + 1] > 0 &&
            handCounts.ContainsKey(discardedTile + 2) && handCounts[discardedTile + 2] > 0)
        {
            options.Add(new ChiOption(discardedTile, discardedTile + 1, discardedTile + 2));
            Debug.Log($"[GenerateChiOptions] Option 3: {discardedTile}, {discardedTile + 1}, {discardedTile + 2}");
        }
        
        Debug.Log($"[GenerateChiOptions] Generated {options.Count} Chi options for tile {discardedTile}");
        return options;
    }

    /// <summary>
    /// Check if a specific tile (by sort value) exists in the player's hand.
    /// </summary>
    private bool HasTileInHand(int sortValue)
    {
        return spawnedTiles.Any(t => t != null && t.GetComponent<TileData>().GetSortValue() == sortValue);
    }

    /// <summary>
    /// Count how many tiles with this sort value exist in the player's hand.
    /// </summary>
    private int CountTileInHand(int sortValue)
    {
        if (spawnedTiles == null) return 0;
        
        int count = 0;
        foreach (GameObject tile in spawnedTiles)
        {
            if (tile != null)
            {
                TileData data = tile.GetComponent<TileData>();
                if (data != null && data.GetSortValue() == sortValue)
                {
                    count++;
                }
            }
        }
        
        return count;
    }

    [Command]
    private void CmdSendCompletedMeld(InterruptActionType meldType, int calledTile, List<int> tileSortValues, int newConcealedCount)
    {
        Debug.Log($"[PlayerHand] Server received completed {meldType} meld: {string.Join(", ", tileSortValues)}");
        Debug.Log($"[PlayerHand] Player {seatIndex} now has {newConcealedCount} concealed tiles");
        
        // Store for broadcasting
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.StoreMeldForBroadcast(seatIndex, meldType, calledTile, tileSortValues);
        }
        
        // Broadcast hand size update to all clients
        RpcUpdateOpponentHandSize(seatIndex, newConcealedCount);
    }

    [Command]
    private void CmdUpdateHandSize(int tilesRemoved)
    {
        // This runs on server - we need to get the actual concealed count
        // The player who made the meld needs to tell us their new count
        Debug.Log($"[CmdUpdateHandSize] Player {seatIndex} removed {tilesRemoved} tiles");
        
        // We'll handle this differently - have the client send the count directly
    }

    [Command]
    private void CmdNotifyChiComplete(List<int> chiTiles)
    {
        Debug.Log($"[PlayerHand->Server] Chi tiles: {string.Join(", ", chiTiles)}");
        
        // Tell GameManager to use these tiles for broadcasting
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.UpdateLastChiTiles(seatIndex, chiTiles);
        }
    }

    /// <summary>
    /// Find Chi option that contains the clicked tile
    /// </summary>
    public ChiOption FindChiOptionContaining(int tileValue)
    {
        Debug.Log($"[PlayerHand] FindChiOptionContaining: tile {tileValue}");
        Debug.Log($"[PlayerHand] Current Chi options: {currentChiOptions.Count}");
        
        foreach (ChiOption option in currentChiOptions)
        {
            List<int> sequence = option.GetSequenceSorted();
            Debug.Log($"[PlayerHand]   Option: {string.Join(", ", sequence)}");
            
            // Check if this tile is in the sequence
            if (sequence.Contains(tileValue))
            {
                // Make sure it's not the called tile (which is from discard)
                if (tileValue != lastDiscardedTileSortValue)
                {
                    Debug.Log($"[PlayerHand]   ✓ Match found!");
                    return option;
                }
                else
                {
                    Debug.Log($"[PlayerHand]   - Tile is the called tile, skipping");
                }
            }
        }
        
        Debug.Log($"[PlayerHand] No matching option found");
        return null;
    }

    /// <summary>
    /// Reset tile highlighting after Chi selection
    /// </summary>
    private void ResetChiHighlighting()
    {
        Debug.Log($"[PlayerHand] Resetting Chi highlighting");
        
        var allTiles = spawnedTiles.ToList();
        if (drawnTile != null) allTiles.Add(drawnTile);
        
        foreach (GameObject tile in allTiles)
        {
            if (tile == null) continue;
            
            Renderer r = tile.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                r.material.color = Color.white; // Reset to normal
            }
        }
    }

    public int GetSeatIndex()
    {
        return seatIndex;
    }

    // =================================================================
    // OPPONENT MELD RENDERING
    // =================================================================

    /// <summary>
    /// Track opponent melds (how many each opponent has shown)
    /// </summary>
    private Dictionary<int, int> opponentMeldCounts = new Dictionary<int, int>();

    private void IncrementOpponentMeldCount(int seatIndex)
    {
        if (!opponentMeldCounts.ContainsKey(seatIndex))
        {
            opponentMeldCounts[seatIndex] = 0;
        }
        opponentMeldCounts[seatIndex]++;
    }

    /// <summary>
    /// Display an opponent's meld in THEIR KongArea (viewed by local player)
    /// </summary>
    public void ShowOpponentMeld(int opponentSeatIndex, InterruptActionType meldType, int calledTile, List<int> allTileSortValues)
    {
        Debug.Log($"[PlayerHand] ═══════════════════════════════");
        Debug.Log($"[PlayerHand] ShowOpponentMeld START");
        Debug.Log($"[PlayerHand] My seat: {seatIndex}");
        Debug.Log($"[PlayerHand] Opponent seat: {opponentSeatIndex}");
        Debug.Log($"[PlayerHand] Meld type: {meldType}");
        Debug.Log($"[PlayerHand] Tiles: {string.Join(", ", allTileSortValues)}");
        
        // Create visual tiles
        List<GameObject> meldTiles = new List<GameObject>();
        
        foreach (int sortValue in allTileSortValues)
        {
            GameObject prefab = FindTilePrefabBySortValue(sortValue);
            if (prefab != null)
            {
                GameObject tile = Instantiate(prefab);
                meldTiles.Add(tile);
            }
        }
        
        if (meldTiles.Count == 0)
        {
            Debug.LogError($"[PlayerHand] FAILED: No tiles created!");
            return;
        }
        
        Debug.Log($"[PlayerHand] Created {meldTiles.Count} tile objects");
        
        // Find the opponent's KongArea
        string containerName = $"KongArea_Seat{opponentSeatIndex}";
        GameObject containerObj = GameObject.Find(containerName);
        
        if (containerObj == null)
        {
            Debug.LogError($"[PlayerHand] FAILED: Could not find {containerName}!");
            return;
        }
        
        Transform meldContainer = containerObj.transform;
        
        // Get opponent's existing melds
        if (!opponentMeldSizes.ContainsKey(opponentSeatIndex))
        {
            opponentMeldSizes[opponentSeatIndex] = new List<int>();
        }
        
        List<int> existingMeldSizes = opponentMeldSizes[opponentSeatIndex];
        Debug.Log($"[PlayerHand] Opponent has {existingMeldSizes.Count} existing melds");
        
        // Positioning settings
        float setSpacing = 0.05f;
        float tileSpacing = 0.12f;
        
        // ===== ROTATION =====
        Quaternion tileRotation;
        if (opponentSeatIndex == 0)
        {
            tileRotation = Quaternion.identity;  // 0° for Player 0
        }
        else
        {
            tileRotation = Quaternion.Euler(0f, 180f, 0f);  // 180° for others
        }

        // ===== DIRECTION =====
        // Player 0: POSITIVE direction (+1) matches their local left-to-right
        // Players 1-3: NEGATIVE direction (-1) for proper left-to-right view
        float directionMultiplier = (opponentSeatIndex == 0) ? 1f : -1f;

        // Calculate starting X position using ACTUAL previous meld sizes
        float meldStartX = 0f;
        for (int i = 0; i < existingMeldSizes.Count; i++)
        {
            int previousMeldSize = existingMeldSizes[i];
            // Apply direction multiplier to spacing
            meldStartX += directionMultiplier * ((previousMeldSize * tileSpacing) + setSpacing);
            Debug.Log($"[PlayerHand]   Previous meld {i}: {previousMeldSize} tiles, cumulative X: {meldStartX}");
        }
        
        Debug.Log($"[PlayerHand] Meld start X: {meldStartX}");
        
        // Position each tile
        for (int i = 0; i < meldTiles.Count; i++)
        {
            GameObject tile = meldTiles[i];
            
            // Apply direction multiplier to tile positioning
            float xPos = meldStartX + (directionMultiplier * i * tileSpacing);
            
            tile.transform.SetParent(meldContainer);
            tile.transform.localPosition = new Vector3(xPos, 0f, 0f);
            tile.transform.localRotation = tileRotation;
            
            Collider collider = tile.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            
            Debug.Log($"[PlayerHand]   Tile {i} at X: {xPos} (direction: {directionMultiplier})");
        }
        
        // Track this meld with its ACTUAL size
        AddOpponentMeld(opponentSeatIndex, meldTiles.Count);
        
        Debug.Log($"[PlayerHand] ✓✓✓ SUCCESS: Displayed {meldTiles.Count} tiles in {containerName}");
        Debug.Log($"[PlayerHand] ═══════════════════════════════");
    }

    /// Show an opponent's self-drawn Kong (Kan button)
    /// Opponents see face-down tiles, local player sees face-up
    /// </summary>
    public void ShowOpponentSelfKong(int opponentSeatIndex, int kongValue, List<int> kongTiles)
    {
        Debug.Log($"[PlayerHand] Viewing Player {opponentSeatIndex}'s self-Kong");
        
        // ===== DETERMINE IF WE'RE THE LOCAL PLAYER VIEWING OUR OWN KONG =====
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        bool isViewingOwnKong = (localPlayer != null && localPlayer.PlayerIndex == opponentSeatIndex);
        
        Debug.Log($"[PlayerHand] Is viewing own Kong: {isViewingOwnKong}");
        
        // Create 4 Kong tiles
        List<GameObject> kongTileObjects = new List<GameObject>();
        
        if (isViewingOwnKong)
        {
            // LOCAL PLAYER: Show face-up tiles (actual tile prefabs)
            foreach (int sortValue in kongTiles)
            {
                GameObject prefab = FindTilePrefabBySortValue(sortValue);
                if (prefab != null)
                {
                    GameObject tile = Instantiate(prefab);
                    kongTileObjects.Add(tile);
                }
            }
        }
        else
        {
            // OPPONENTS: Show face-down tiles (use face-down prefab)
            // Find a generic face-down tile prefab (you'll need to create this)
            GameObject faceDownPrefab = FindFaceDownTilePrefab();
            
            if (faceDownPrefab != null)
            {
                // Create 4 face-down tiles
                for (int i = 0; i < 4; i++)
                {
                    GameObject tile = Instantiate(faceDownPrefab);
                    kongTileObjects.Add(tile);
                }
            }
            else
            {
                // FALLBACK: If no face-down prefab, flip tiles 180° around X-axis
                Debug.LogWarning("[PlayerHand] No face-down tile prefab found, using flipped tiles");
                
                foreach (int sortValue in kongTiles)
                {
                    GameObject prefab = FindTilePrefabBySortValue(sortValue);
                    if (prefab != null)
                    {
                        GameObject tile = Instantiate(prefab);
                        kongTileObjects.Add(tile);
                    }
                }
            }
        }
        
        if (kongTileObjects.Count != 4)
        {
            Debug.LogError($"[PlayerHand] Failed to create 4 Kong tiles!");
            return;
        }
        
        // Find opponent's KongArea
        string containerName = $"KongArea_Seat{opponentSeatIndex}";
        GameObject containerObj = GameObject.Find(containerName);
        
        if (containerObj == null)
        {
            Debug.LogError($"[PlayerHand] Could not find {containerName}!");
            return;
        }
        
        Transform meldContainer = containerObj.transform;
        
        // Get existing melds
        if (!opponentMeldSizes.ContainsKey(opponentSeatIndex))
        {
            opponentMeldSizes[opponentSeatIndex] = new List<int>();
        }
        
        List<int> existingMeldSizes = opponentMeldSizes[opponentSeatIndex];
        
        float setSpacing = 0.05f;
        float tileSpacing = 0.12f;
        
        // ===== ROTATION =====
        Quaternion tileRotation;
        
        if (isViewingOwnKong)
        {
            // LOCAL PLAYER: Use appropriate rotation based on seat
            if (opponentSeatIndex == 0)
            {
                tileRotation = Quaternion.identity;  // 0° for Player 0
            }
            else
            {
                tileRotation = Quaternion.Euler(0f, 180f, 0f);  // 180° for others
            }
        }
        else
        {
            // OPPONENTS: Face-down tiles OR flipped tiles
            // If using face-down prefab: use standard rotation
            // If using flipped tiles: add 180° rotation around X-axis to flip face-down
            GameObject faceDownPrefab = FindFaceDownTilePrefab();
            
            if (faceDownPrefab != null)
            {
                // Using face-down prefab: standard rotation
                if (opponentSeatIndex == 0)
                {
                    tileRotation = Quaternion.identity;
                }
                else
                {
                    tileRotation = Quaternion.Euler(0f, 180f, 0f);
                }
            }
            else
            {
                // Using flipped tiles: add 180° X rotation to flip face-down
                if (opponentSeatIndex == 0)
                {
                    tileRotation = Quaternion.Euler(180f, 0f, 0f);  // Flipped face-down
                }
                else
                {
                    tileRotation = Quaternion.Euler(180f, 180f, 0f);  // Flipped + 180° Y
                }
            }
        }

        // ===== DIRECTION =====
        // FIXED: Use -1f for left-to-right (was +1f for right-to-left)
        float directionMultiplier = (opponentSeatIndex == 0) ? 1f : -1f;
        // Calculate start X based on existing melds
        float meldStartX = 0f;
        for (int i = 0; i < existingMeldSizes.Count; i++)
        {
            int previousMeldSize = existingMeldSizes[i];
            meldStartX += directionMultiplier * ((previousMeldSize * tileSpacing) + setSpacing);
        }

        // Position each Kong tile
        for (int i = 0; i < kongTileObjects.Count; i++)
        {
            GameObject tile = kongTileObjects[i];
            float xPos = meldStartX + (directionMultiplier * i * tileSpacing);
            
            tile.transform.SetParent(meldContainer);
            tile.transform.localPosition = new Vector3(xPos, 0f, 0f);
            tile.transform.localRotation = tileRotation;
            
            Collider collider = tile.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }
        
        // Track Kong with size 4
        AddOpponentMeld(opponentSeatIndex, 4);
        
        Debug.Log($"[PlayerHand] ✓ Displayed Player {opponentSeatIndex}'s self-Kong (face-up: {isViewingOwnKong})");
    }

    /// <summary>
    /// Find the face-down tile prefab (generic back of tile)
    /// </summary>
    private GameObject FindFaceDownTilePrefab()
    {
        // You'll need to add a face-down tile prefab to your TilePrefabs array
        // Name it something like "FaceDownTile" or give it a special sort value like 999
        
        if (NetworkedGameManager.Instance == null) return null;
        if (NetworkedGameManager.Instance.TilePrefabs == null) return null;
        
        // Try to find by name
        foreach (GameObject prefab in NetworkedGameManager.Instance.TilePrefabs)
        {
            if (prefab != null && prefab.name.Contains("FaceDown"))
            {
                return prefab;
            }
        }
        
        // Or try to find by a special sort value (e.g., 999 for face-down)
        foreach (GameObject prefab in NetworkedGameManager.Instance.TilePrefabs)
        {
            if (prefab != null)
            {
                TileData data = prefab.GetComponent<TileData>();
                if (data != null && data.GetSortValue() == 999)
                {
                    return prefab;
                }
            }
        }
        
        return null; // No face-down prefab found
    }

    // REPLACE the simple count dictionary with a list that stores meld sizes
    private Dictionary<int, List<int>> opponentMeldSizes = new Dictionary<int, List<int>>();

    private int GetOpponentMeldCount(int seatIndex)
    {
        if (!opponentMeldSizes.ContainsKey(seatIndex))
        {
            opponentMeldSizes[seatIndex] = new List<int>();
        }
        return opponentMeldSizes[seatIndex].Count;
    }

    private void AddOpponentMeld(int seatIndex, int meldSize)
    {
        if (!opponentMeldSizes.ContainsKey(seatIndex))
        {
            opponentMeldSizes[seatIndex] = new List<int>();
        }
        opponentMeldSizes[seatIndex].Add(meldSize);
    }

    // In NetworkedPlayerHand.cs

    // 1. DELETE any old field like this:
    // private bool isInChiSelectionMode = false;  ← DELETE THIS LINE

    // 2. KEEP ONLY the backing field and property:
    private bool _isInChiSelectionMode = false;

    public bool IsInChiSelectionMode 
    { 
        get 
        {
            return _isInChiSelectionMode;
        }
    }

    // 3. UPDATE EnterChiSelectionMode to use backing field:
    public void EnterChiSelectionMode(
        List<ChiOption> options, 
        System.Action<ChiOption> onHover,  // Ignored (can be null)
        System.Action<ChiOption> onClick)
    {
        Debug.Log($"[PlayerHand] Entering Chi selection mode with {options.Count} options");
        
        // FIX 1: Use backing field instead of property
        _isInChiSelectionMode = true;
        
        currentChiOptions = options;
        
        // FIX 2: Store the click callback
        onChiClickCallback = onClick;
        
        // Don't store onHover - we call ShowChiPreview directly
        
        HighlightChiTiles(options);
    }

    /// <summary>
    /// Called when hovering over a tile during Chi selection mode
    /// </summary>
    /// <param name="tileValue">Sort value of the hovered tile</param>
    /// <param name="tilePosition">World position of the hovered tile</param>
    public void OnChiTileHovered(int tileValue, Vector3 tilePosition)
    {
        Debug.Log($"[PlayerHand] OnChiTileHovered: tile={tileValue}, pos={tilePosition}");
        
        // Check if we're in Chi selection mode (use property for reading)
        if (!IsInChiSelectionMode)
        {
            Debug.Log($"[PlayerHand] Not in Chi selection mode");
            return;
        }
        
        if (interruptUI == null)
        {
            Debug.LogError($"[PlayerHand] interruptUI is NULL!");
            return;
        }
        
        // Find matching option
        ChiOption matchingOption = FindChiOptionContaining(tileValue);
        
        if (matchingOption != null)
        {
            List<int> seq = matchingOption.GetSequenceSorted();
            Debug.Log($"[PlayerHand] Match found! Sequence: {string.Join(", ", seq)}");
            Debug.Log($"[PlayerHand] Calling ShowChiPreview with position: {tilePosition}");
            
            // Call ShowChiPreview directly with actual position
            interruptUI.ShowChiPreview(matchingOption, tilePosition);
        }
        else
        {
            Debug.Log($"[PlayerHand] No matching option for tile {tileValue}");
            interruptUI.HideChiPreview();
        }
    }

    // 5. UPDATE OnChiTileClicked to use backing field:
    public void OnChiTileClicked(int tileValue)
    {
        Debug.Log($"[PlayerHand] OnChiTileClicked: {tileValue}");
        
        if (!IsInChiSelectionMode) return;
        
        // Find matching option
        ChiOption matchingOption = FindChiOptionContaining(tileValue);
        
        if (matchingOption != null)
        {
            Debug.Log($"[PlayerHand] Chi option confirmed!");
            
            // Call the stored callback
            onChiClickCallback?.Invoke(matchingOption);
            
            // Exit selection mode
            ExitAllSelectionModes();
        }
    }

    // 6. UPDATE ExitAllSelectionModes to use backing field:
    public void ExitAllSelectionModes()
    {
        Debug.Log($"[PlayerHand] ExitAllSelectionModes called");
        
        _isInChiSelectionMode = false;  // Use backing field
        isSelectingKong = false;
        
        ResetChiHighlighting();
        
        if (interruptUI != null)
        {
            interruptUI.HideAll();
        }
    }

    /// <summary>
    /// Store the selected Chi option (called by InterruptUIManager)
    /// </summary>
    public void SetSelectedChiOption(ChiOption option)
    {
        Debug.Log($"[PlayerHand] SetSelectedChiOption: {option.tile1SortValue}, {option.tile2SortValue}, {option.discardedTile}");
        selectedChiOption = option;
    }

    /// <summary>
    /// Show a flower tile to all players (called via ClientRpc from server)
    /// This is called on ALL clients, not just the owner
    /// </summary>
    public void ShowFlowerTileToAll(int flowerTileValue)
    {
        Debug.Log($"==========================================");
        Debug.Log($"[PlayerHand] ShowFlowerTileToAll called");
        Debug.Log($"[PlayerHand] Seat: {seatIndex}, Flower: {flowerTileValue}");
        Debug.Log($"[PlayerHand] handContainer null: {handContainer == null}");
        
        if (handContainer == null)
        {
            Debug.LogWarning($"[PlayerHand] No hand container to show flower!");
            Debug.Log($"==========================================");
            return;
        }
        
        // Find the tile prefab for this flower
        GameObject tilePrefab = FindTilePrefabBySortValue(flowerTileValue);
        if (tilePrefab == null)
        {
            Debug.LogError($"[PlayerHand] No prefab found for flower {flowerTileValue}!");
            Debug.Log($"==========================================");
            return;
        }
        
        Debug.Log($"[PlayerHand] Found flower prefab: {tilePrefab.name}");
        
        // Spawn the flower tile
        GameObject flowerTile = Instantiate(tilePrefab, handContainer);
        flowerTile.transform.SetParent(handContainer);
        
        Debug.Log($"[PlayerHand] Instantiated flower tile");
        
        // Disable collider so it can't be clicked
        Collider collider = flowerTile.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Debug.Log($"[PlayerHand] Disabled flower collider");
        }
        
        // Add to flower tiles list
        flowerTiles.Add(flowerTile);
        
        Debug.Log($"[PlayerHand] Total flower tiles: {flowerTiles.Count}");
        
        // If this is the local player, also add to logic hand
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        if (localPlayer != null && localPlayer.PlayerIndex == seatIndex)
        {
            Debug.Log($"[PlayerHand] This is the local player - adding to logic hand");
            TileData flowerData = CreateTileDataFromSortValue(flowerTileValue);
            logicHand.CollectFlower(flowerData);
        }
        
        // Reposition all tiles
        RepositionTiles();
        
        Debug.Log($"[PlayerHand] Flower tile shown successfully!");
        Debug.Log($"==========================================");
    }

    /// <summary>
    /// Draw a flower tile (immediately set it aside, don't add to hand)
    /// </summary>
    public void DrawFlowerTile(int flowerTileValue)
    {
        Debug.Log($"[PlayerHand] Drawing flower tile {flowerTileValue}");
        
        if (handContainer == null)
        {
            Debug.LogError($"[PlayerHand] No hand container!");
            return;
        }
        
        // Find the tile prefab
        GameObject tilePrefab = FindTilePrefabBySortValue(flowerTileValue);
        if (tilePrefab == null)
        {
            Debug.LogError($"[PlayerHand] No prefab found for flower {flowerTileValue}");
            return;
        }
        
        // Spawn the flower tile
        GameObject flowerTile = Instantiate(tilePrefab, handContainer);
        flowerTile.transform.SetParent(handContainer);
        
        // Disable collider so it can't be clicked
        Collider collider = flowerTile.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }
        
        // Add to flower tiles list
        flowerTiles.Add(flowerTile);
        
        // Add to logic hand
        TileData flowerData = CreateTileDataFromSortValue(flowerTileValue);
        logicHand.CollectFlower(flowerData);
        
        // Reposition all tiles
        RepositionTiles();
        
        Debug.Log($"[PlayerHand] Flower tile {flowerTileValue} set aside");
    }
}