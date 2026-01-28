using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Networked version of TileManager - controls 4-player Mahjong game.
/// </summary>
public class NetworkedGameManager : NetworkBehaviour
{
    // Add this field at the top with other fields
    private Dictionary<int, List<GameObject>> spawnedDiscardTiles = new Dictionary<int, List<GameObject>>();

    // Store Chi tiles from clients
    private Dictionary<int, List<int>> lastChiTilesPerPlayer = new Dictionary<int, List<int>>();

    public static NetworkedGameManager Instance { get; private set; }

    [Header("Tile Prefabs")]
    [SerializeField] public GameObject[] tilePrefabs;
    [SerializeField] public GameObject[] allUniqueTilePrefabs;

    [Header("Player Positions")]
    [SerializeField] private Transform[] playerHandPositions = new Transform[4];
    [SerializeField] private Transform[] playerDiscardPositions = new Transform[4];
    [SerializeField] private Transform[] playerKongPositions = new Transform[4];

    [Header("Table Settings")]
    [SerializeField] private Transform tableCenter;

    // Game State
    [SyncVar(hook = nameof(OnCurrentPlayerChanged))]
    private int currentPlayerIndex = 0;

    [SyncVar]
    private int wallTilesRemaining = 0;

    [SyncVar]
    private bool gameStarted = false;

    // Round management
    [SyncVar]
    private int roundWind = 401; // East=401, South=402, West=403, North=404
    
    [SyncVar]
    private int consecutiveEastWins = 0;
    
    private int[] playerSeats = new int[4]; // Maps player index to seat position

    // Server-only data
    private List<int> wallTiles = new List<int>();
    private Dictionary<int, NetworkedPlayerHand> playerHands = new Dictionary<int, NetworkedPlayerHand>();
    private List<NetworkPlayer> players = new List<NetworkPlayer>();

    // Discard tracking (per player)
    private Dictionary<int, List<int>> playerDiscards = new Dictionary<int, List<int>>();
    private Dictionary<int, int> playerDiscardCounts = new Dictionary<int, int>();

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
        Debug.Log("NetworkedGameManager: Server started");
    }

    /// <summary>
    /// Start game with players from lobby.
    /// </summary>
    [Server]
    public void StartGame(List<NetworkPlayer> gamePlayers)
    {
        Debug.Log("=== NetworkedGameManager.StartGame() CALLED ===");
        Debug.Log($"gameStarted flag: {gameStarted}");
        Debug.Log($"Players received: {gamePlayers?.Count ?? 0}");
        
        if (gameStarted)
        {
            Debug.LogWarning("Game already started!");
            return;
        }

        players = new List<NetworkPlayer>(gamePlayers);

        // Initialize seat assignments (1:1 mapping initially)
        for (int i = 0; i < players.Count; i++)
        {
            playerSeats[i] = i;
        }
        roundWind = 401; // Start at East
        consecutiveEastWins = 0;
        Debug.Log("[Game] Seat assignments initialized - all players at their starting seats");

        Debug.Log($"Starting Mahjong game with {players.Count} players");
        
        // Verify tile prefabs are assigned
        if (tilePrefabs == null || tilePrefabs.Length == 0)
        {
            Debug.LogError("âœ— CRITICAL: tilePrefabs array is empty! Cannot spawn tiles!");
            return;
        }
        
        Debug.Log($"âœ“ Tile prefabs loaded: {tilePrefabs.Length} prefabs");


        // Initialize discard tracking
        for (int i = 0; i < 4; i++)
        {
            playerDiscards[i] = new List<int>();
            playerDiscardCounts[i] = 0;
        }

        Debug.Log("[Game] Initializing wall...");
        InitializeWall();
        
        Debug.Log("[Game] Dealing initial hands...");
        DealInitialHands();
        
        gameStarted = true;
        currentPlayerIndex = 0;

        Debug.Log("[Game] Sending RpcGameStarted...");
        RpcGameStarted();
        
        Debug.Log("[Game] Starting first player turn...");
        StartPlayerTurn(currentPlayerIndex);
        
        Debug.Log("[Game] StartGame COMPLETE");
    }

    [Server]
    private void InitializeWall()
    {
        wallTiles.Clear();

        // Numbered tiles: 1-9 in three suits, 4 copies each
        int[] suits = { 100, 200, 300 };
        foreach (int suitBase in suits)
        {
            for (int value = 1; value <= 9; value++)
            {
                for (int copy = 0; copy < 4; copy++)
                {
                    wallTiles.Add(suitBase + value);
                }
            }
        }

        // Honor tiles: Winds and Dragons, 4 copies each
        int[] honors = { 401, 402, 403, 404, 501, 502, 503 };
        foreach (int honor in honors)
        {
            for (int copy = 0; copy < 4; copy++)
            {
                wallTiles.Add(honor);
            }
        }

        // === FLOWER TILES (8 total) ===
        // Blue Flowers: 601, 602, 603, 604 (suit = 6)
        // Red Flowers: 701, 702, 703, 704 (suit = 7)
        int[] flowers = { 601, 602, 603, 604, 701, 702, 703, 704 };
        foreach (int flower in flowers)
        {
            wallTiles.Add(flower);
        }
        
        Debug.Log($"[GameManager] Wall initialized with {flowers.Length} flower tiles");

        // Shuffle
        System.Random rng = new System.Random();
        wallTiles = wallTiles.OrderBy(x => rng.Next()).ToList();
        wallTilesRemaining = wallTiles.Count;

        Debug.Log($"Wall initialized: {wallTilesRemaining} tiles (including flowers)");
    }

    /// <summary>
    /// Check if a tile value is a flower tile
    /// </summary>
    [Server]
    private bool IsFlowerTile(int tileValue)
    {
        int suit = tileValue / 100;
        return suit == 6 || suit == 7; // Blue Flowers (6xx) or Red Flowers (7xx)
    }

    [Server]
    private void DealInitialHands()
    {
        Debug.Log($"[Game] DealInitialHands() called. Players: {players.Count}");
        
        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayer player = players[i];
            Debug.Log($"[Game] Dealing to Player {i}: {player.Username}");
            
            NetworkedPlayerHand hand = player.GetComponent<NetworkedPlayerHand>();
            
            if (hand == null)
            {
                Debug.Log($"[Game] Creating NetworkedPlayerHand for {player.Username}");
                hand = player.gameObject.AddComponent<NetworkedPlayerHand>();
            }

            hand.SetSeatIndex(i);
            
            // Check hand positions
            if (i < playerHandPositions.Length && playerHandPositions[i] != null)
            {
                string containerPath = GetPathToTransform(playerHandPositions[i]);
                Debug.Log($"[Game] Setting hand container for Player {i}: {containerPath}");
                hand.TargetSetHandContainer(player.connectionToClient, containerPath);
            }
            else
            {
                Debug.LogError($"[Game] âœ— Hand position {i} is NULL or out of bounds!");
            }

            playerHands[i] = hand;

            // Generate a COMPLETE 14-tile winning hand
            List<int> completeWinningHand = HandGenerator.GenerateRandomWinningHandSortValues();
            
            // The complete hand has 14 tiles - we need to give 13 to start
            // Remove one tile that will make this an "iishanten" (1-away) hand
            // IMPORTANT: We pick a tile that's part of a complete set, so removing it breaks the win
            
            // Strategy: Remove a tile from the first triplet or sequence we find
            int tileToRemove = FindTileToRemoveForIishanten(completeWinningHand);
            
            if (tileToRemove > 0)
            {
                completeWinningHand.Remove(tileToRemove);
                Debug.Log($"[Game] Player {i} waiting tile: {tileToRemove}");
            }
            else
            {
                // Fallback: just remove the last tile
                tileToRemove = completeWinningHand[completeWinningHand.Count - 1];
                completeWinningHand.RemoveAt(completeWinningHand.Count - 1);
                Debug.Log($"[Game] Player {i} waiting tile (fallback): {tileToRemove}");
            }
            
            // Now completeWinningHand has 13 tiles
            List<int> initialTiles = new List<int>(completeWinningHand);
            
            // Sort tiles before sending (for better UI display)
            initialTiles.Sort();

            Debug.Log($"[Game] Sending {initialTiles.Count} tiles to {player.Username}");
            Debug.Log($"[Game] About to call RpcReceiveInitialHand for seat {i}");
            Debug.Log($"[Game] Tiles being sent: {string.Join(",", initialTiles)}");
            
            // Use ClientRpc instead of TargetRpc - let clients filter by seat
            RpcReceiveInitialHand(i, initialTiles);
            
            Debug.Log($"[Game] RpcReceiveInitialHand called for seat {i}");
        }
        
        Debug.Log("[Game] âœ“ Initial dealing complete");
    }

    [Server]
    private int DrawTileFromWall()
    {
        if (wallTiles.Count == 0) return 0;
        int tile = wallTiles[0];
        wallTiles.RemoveAt(0);
        wallTilesRemaining = wallTiles.Count;
        return tile;
    }

    [Server]
    private void StartPlayerTurn(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= players.Count) return;

        NetworkPlayer targetPlayer = players[playerIndex];
        NetworkedPlayerHand targetHand = playerHands[playerIndex];

        if (wallTiles.Count > 0)
        {
            int drawnTile = DrawTileFromWall();
            
            // Check if the drawn tile is a flower
            if (IsFlowerTile(drawnTile))
            {
                Debug.Log($"[GameManager] Player {playerIndex} drew FLOWER: {drawnTile}");
                
                // Show the flower to ALL clients
                RpcShowFlowerTile(playerIndex, drawnTile);
                
                // Draw replacement tile(s) until we get a non-flower
                int replacementTile = drawnTile;
                int flowerCount = 0;
                
                while (IsFlowerTile(replacementTile) && wallTiles.Count > 0 && flowerCount < 10)
                {
                    flowerCount++;
                    
                    if (wallTiles.Count > 0)
                    {
                        replacementTile = DrawTileFromWall();
                        
                        if (IsFlowerTile(replacementTile))
                        {
                            Debug.Log($"[GameManager] Player {playerIndex} drew ANOTHER flower: {replacementTile}");
                            RpcShowFlowerTile(playerIndex, replacementTile);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                
                // Now send the actual tile (non-flower) to draw
                if (!IsFlowerTile(replacementTile))
                {
                    Debug.Log($"[GameManager] Player {playerIndex} finally drew normal tile: {replacementTile}");
                    RpcDrawTile(playerIndex, replacementTile);
                }
            }
            else
            {
                // Normal tile - send to all clients
                RpcDrawTile(playerIndex, drawnTile);
            }
        }
        else
        {
            RpcGameEndedDraw();
        }
    }

    /// <summary>
    /// Show a flower tile to all clients
    /// </summary>
    [ClientRpc]
    private void RpcShowFlowerTile(int seatIndex, int flowerTileValue)
    {
        Debug.Log($"==========================================");
        Debug.Log($"[RpcShowFlowerTile] Player {seatIndex} revealed FLOWER: {flowerTileValue}");
        
        // Find the player at this seat
        NetworkPlayer targetPlayer = null;
        foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player.PlayerIndex == seatIndex)
            {
                targetPlayer = player;
                break;
            }
        }
        
        if (targetPlayer == null)
        {
            Debug.LogError($"[RpcShowFlowerTile] Could not find player at seat {seatIndex}");
            Debug.Log($"==========================================");
            return;
        }
        
        NetworkedPlayerHand hand = targetPlayer.GetComponent<NetworkedPlayerHand>();
        if (hand == null)
        {
            Debug.LogError($"[RpcShowFlowerTile] Player at seat {seatIndex} has no NetworkedPlayerHand!");
            Debug.Log($"==========================================");
            return;
        }
        
        Debug.Log($"[RpcShowFlowerTile] Calling ShowFlowerTileToAll on player {seatIndex}");
        hand.ShowFlowerTileToAll(flowerTileValue);
        Debug.Log($"==========================================");
    }

    /// <summary>
    /// Tell all clients that a player drew a tile (everyone can see it)
    /// </summary>
    [ClientRpc]
    private void RpcDrawTile(int seatIndex, int tileValue)
    {
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        int localSeat = localPlayer?.PlayerIndex ?? -1;
        
        Debug.Log($"[RpcDrawTile] LOCAL SEAT: {localSeat}, Drawing for SEAT: {seatIndex}, Tile: {tileValue}");
        
        // Find the player at this seat
        NetworkPlayer targetPlayer = null;
        foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player.PlayerIndex == seatIndex)
            {
                targetPlayer = player;
                break;
            }
        }

        if (targetPlayer == null)
        {
            Debug.LogWarning($"[RpcDrawTile] Could not find player at seat {seatIndex}");
            return;
        }

        NetworkedPlayerHand hand = targetPlayer.GetComponent<NetworkedPlayerHand>();
        if (hand == null)
        {
            Debug.LogError($"[RpcDrawTile] No hand component for seat {seatIndex}");
            return;
        }

        // CRITICAL: Ensure container is set before drawing
        if (hand.GetHandContainer() == null)
        {
            Debug.LogWarning($"[RpcDrawTile] Hand container not set for seat {seatIndex}, setting it now");
            GameObject container = GameObject.Find($"HandPosition_Seat{seatIndex}");
            if (container != null)
            {
                hand.SetHandContainerDirect(container.transform);
                Debug.Log($"[RpcDrawTile] Container set: {container.name}");
            }
            else
            {
                Debug.LogError($"[RpcDrawTile] Could not find HandPosition_Seat{seatIndex}");
                return;
            }
        }

        // Check if this is the local player
        bool isLocalPlayer = (localPlayer != null && localPlayer.PlayerIndex == seatIndex);
        
        Debug.Log($"[RpcDrawTile] isLocalPlayer={isLocalPlayer} (local={localSeat}, target={seatIndex})");

        // NEW: Check if this is a flower tile for the LOCAL player only
        if (isLocalPlayer && IsFlowerTile(tileValue))
        {
            Debug.Log($"[RpcDrawTile] LOCAL player drew FLOWER {tileValue} - setting aside immediately");
            hand.DrawFlowerTile(tileValue);
            // Server will handle drawing replacement tile
            return;
        }

        hand.DrawTileDirect(tileValue, isLocalPlayer);
    }
    
    /// <summary>
    /// Tell all clients to hide a player's drawn tile after discard.
    /// </summary>
    [ClientRpc]
    private void RpcHideDrawnTile(int playerSeat)
    {
        // Find the player at this seat
        NetworkPlayer targetPlayer = null;
        foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player.PlayerIndex == playerSeat)
            {
                targetPlayer = player;
                break;
            }
        }

        if (targetPlayer == null) return;

        NetworkedPlayerHand hand = targetPlayer.GetComponent<NetworkedPlayerHand>();
        if (hand != null)
        {
            hand.HideDrawnTileForDiscard();
        }
    }

    /// <summary>
    /// Player declared Kong.
    /// </summary>
    [Server]
    public void PlayerDeclaredKong(int playerIndex, int kongValue, List<int> kongTiles)
    {
        Debug.Log($"[GameManager] Server: Player {playerIndex} declared self-Kong with {kongValue}");
        
        NetworkPlayer player = players[playerIndex];
        
        // Draw replacement tile
        if (wallTiles.Count > 0)
        {
            int replacementTile = DrawTileFromWall();
            NetworkedPlayerHand hand = playerHands[playerIndex];
            hand.TargetDrawTile(player.connectionToClient, replacementTile);
            
            Debug.Log($"[GameManager] Gave replacement tile {replacementTile} to Player {playerIndex}");
        }
        
        // Broadcast the Kong to OTHER players (not the player who did it)
        RpcShowSelfKong(playerIndex, kongValue, kongTiles);
        
        // Turn continues - player must still discard
        Debug.Log($"[GameManager] Player {playerIndex} keeps turn after Kong");
    }

    [ClientRpc]
    private void RpcShowSelfKong(int playerIndex, int kongValue, List<int> kongTiles)
    {
        Debug.Log($"[GameManager] RPC: Player {playerIndex} declared self-Kong");
        
        // Find the LOCAL player's hand (the one viewing this)
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        if (localPlayer == null) return;
        
        NetworkedPlayerHand localHand = localPlayer.GetComponent<NetworkedPlayerHand>();
        if (localHand == null) return;
        
        int localSeat = localPlayer.PlayerIndex;
        
        // Don't show to the player who did the Kong (they already see it locally)
        if (localSeat == playerIndex)
        {
            Debug.Log($"[GameManager] Skipping self-Kong RPC - this is our Kong");
            return;
        }
        
        // Show the opponent's Kong
        Debug.Log($"[GameManager] Showing Player {playerIndex}'s self-Kong to local Player {localSeat}");
        localHand.ShowOpponentSelfKong(playerIndex, kongValue, kongTiles);
    }

    /// <summary>
    /// Show Kong tiles to all clients (spawned in the declaring player's Kong area).
    /// </summary>
    [ClientRpc]
    private void RpcShowKongTiles(int playerIndex, List<int> kongTileSortValues)
    {
        Debug.Log($"[RpcShowKongTiles] Showing Kong for Player {playerIndex}: {string.Join(", ", kongTileSortValues)}");
        
        if (playerIndex < 0 || playerIndex >= playerKongPositions.Length)
        {
            Debug.LogError($"[RpcShowKongTiles] Invalid player index: {playerIndex}");
            return;
        }
        
        Transform kongPosition = playerKongPositions[playerIndex];
        if (kongPosition == null)
        {
            Debug.LogError($"[RpcShowKongTiles] Kong position for player {playerIndex} is null!");
            return;
        }
        
        Debug.Log($"[RpcShowKongTiles] Kong parent rotation: {kongPosition.rotation.eulerAngles}");
        
        // Count existing Kong sets for this player (for spacing)
        int existingKongs = 0;
        foreach (Transform child in kongPosition)
        {
            if (child.name.Contains("KongSet")) existingKongs++;
        }
        
        // Create a parent object for this Kong set
        GameObject kongSetParent = new GameObject($"KongSet_{existingKongs}");
        
        // CRITICAL: SetParent with worldPositionStays=false to use local coordinates
        kongSetParent.transform.SetParent(kongPosition, false);
        
        // Spacing between Kong sets: 0.05 units
        // Each Kong set is 4 tiles × 0.12 spacing = 0.48 units wide
        // Plus 0.05 gap between sets
        float setOffset = existingKongs * (4 * 0.12f + 0.05f);
        kongSetParent.transform.localPosition = new Vector3(setOffset, 0, 0);
        kongSetParent.transform.localRotation = Quaternion.identity; // Identity in local space = inherit parent rotation
        
        Debug.Log($"[RpcShowKongTiles] Kong set parent created at local position: {setOffset}");
        
        // Spawn the 4 Kong tiles
        for (int i = 0; i < kongTileSortValues.Count && i < 4; i++)
        {
            int sortValue = kongTileSortValues[i];
            
            // Find the tile prefab
            GameObject tilePrefab = FindTilePrefabBySortValue(sortValue);
            if (tilePrefab == null)
            {
                Debug.LogWarning($"[RpcShowKongTiles] Could not find prefab for sort value {sortValue}");
                continue;
            }
            
            // CRITICAL: Instantiate with parent and worldPositionStays=false
            GameObject kongTile = Instantiate(tilePrefab, kongSetParent.transform);
            
            // Position: 0.12 spacing between tiles (matches discard tile spacing)
            kongTile.transform.localPosition = new Vector3(i * 0.12f, 0, 0);
            
            // CRITICAL: Set localRotation to identity AFTER parenting
            // This makes it inherit the parent's world rotation
            kongTile.transform.localRotation = Quaternion.identity;
            
            // Disable collider so it can't be clicked
            Collider collider = kongTile.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }
        
        Debug.Log($"[RpcShowKongTiles] Successfully spawned {kongTileSortValues.Count} Kong tiles for Player {playerIndex}");
    }

    /// <summary>
    /// Player declared Mahjong (Tsumo).
    /// </summary>
    [Server]
    public void PlayerDeclaredMahjong(int playerIndex, HandAnalysisResult analysis, int score, List<int> winningTiles)
    {
        NetworkPlayer winner = players[playerIndex];
        Debug.Log($"Player {playerIndex} ({winner.Username}) wins with {score} points!");

        // ADD THESE TWO LINES:
        int winnerSeatIndex = playerSeats[playerIndex];
        HandleWin(winnerSeatIndex);

        RpcShowWinScreen(playerIndex, winner.Username, score, analysis, winningTiles, new List<string>());
    }

    /// <summary>
    /// Player declared Ron (win on opponent's discard)
    /// </summary>
    [Server]
    public void PlayerDeclaredRon(int playerIndex, int ronTile, HandAnalysisResult analysis, int score, List<int> tileSortValues, List<string> flowerMessages)
    {
        Debug.Log($"[GameManager] ===== RON DECLARED =====");
        Debug.Log($"[GameManager] Player {playerIndex} won with Ron!");
        Debug.Log($"[GameManager] Ron tile: {ronTile}");
        Debug.Log($"[GameManager] Score: {score}");

        NetworkPlayer winner = players[playerIndex];
        Debug.Log($"[GameManager] Winner: {winner.Username} (Seat {playerIndex})");

        // Show win screen to all clients
        RpcShowWinScreen(playerIndex, winner.Username, score, analysis, tileSortValues, flowerMessages);
    }

    // ===== CLIENT RPCs =====

    [ClientRpc]
    private void RpcGameStarted()
    {
        Debug.Log("Game started! All players received tiles.");
    }

    /// <summary>
    /// Send initial hand to all clients - all clients spawn tiles for all players
    /// </summary>
    [ClientRpc]
    private void RpcReceiveInitialHand(int seatIndex, List<int> tiles)
    {
        Debug.Log($"[RpcReceiveInitialHand] Received for seat {seatIndex}, tiles: {tiles.Count}");
        
        // Find the player at this seat
        NetworkPlayer targetPlayer = null;
        foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player.PlayerIndex == seatIndex)
            {
                targetPlayer = player;
                break;
            }
        }

        if (targetPlayer == null)
        {
            Debug.LogWarning($"[RpcReceiveInitialHand] Could not find player at seat {seatIndex}");
            return;
        }

        Debug.Log($"[RpcReceiveInitialHand] Spawning tiles for seat {seatIndex} (all players can see)");

        // Get or create hand component
        NetworkedPlayerHand hand = targetPlayer.GetComponent<NetworkedPlayerHand>();
        if (hand == null)
        {
            Debug.Log($"[RpcReceiveInitialHand] Creating NetworkedPlayerHand component for seat {seatIndex}");
            hand = targetPlayer.gameObject.AddComponent<NetworkedPlayerHand>();
        }

        hand.SetSeatIndex(seatIndex);

        // Set container
        GameObject container = GameObject.Find($"HandPosition_Seat{seatIndex}");
        if (container != null)
        {
            hand.SetHandContainerDirect(container.transform);
            Debug.Log($"[RpcReceiveInitialHand] Container set for seat {seatIndex}: {container.name}");
        }
        else
        {
            Debug.LogError($"[RpcReceiveInitialHand] Could not find HandPosition_Seat{seatIndex}");
        }

        // Check if this is the local player to set up logic hand
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        bool isLocalPlayer = (localPlayer != null && localPlayer.PlayerIndex == seatIndex);

        // Call the receive method - all clients spawn visuals, but only owner gets logic
        hand.ReceiveInitialHandDirect(tiles, isLocalPlayer);
    }

    [ClientRpc]
    private void RpcSpawnDiscardTile(int playerIndex, int tileValue, int discardIndex)
    {
        if (playerIndex >= playerDiscardPositions.Length) return;
        
        Transform discardArea = playerDiscardPositions[playerIndex];
        if (discardArea == null) return;

        GameObject tilePrefab = FindTilePrefabBySortValue(tileValue);
        if (tilePrefab == null) return;

        // 8 tiles per row
        int row = discardIndex / 8;
        int col = discardIndex % 8;

        // ===== POSITIONING WITH ROTATION ADJUSTMENT =====
        Vector3 spawnPos;
        Quaternion tileRotation;
        
        if (playerIndex == 0)
        {
            // Player 0: Standard layout
            spawnPos = discardArea.position + new Vector3(col * 0.12f, 0.01f, -row * 0.16f);
            tileRotation = Quaternion.identity;
        }
        else
        {
            // Players 1, 2, 3: Rotated 90° CCW
            // Keep visual layout similar but rotated
            spawnPos = discardArea.position + new Vector3(row * 0.16f, 0.01f, col * 0.12f);
            tileRotation = Quaternion.Euler(0f, -90f, 0f);
        }
        
        GameObject discardTile = Instantiate(tilePrefab, spawnPos, tileRotation);
        
        // Disable collider so it can't be clicked
        Collider collider = discardTile.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        
        // STORE THE DISCARD TILE REFERENCE
        if (!spawnedDiscardTiles.ContainsKey(playerIndex))
        {
            spawnedDiscardTiles[playerIndex] = new List<GameObject>();
        }
        spawnedDiscardTiles[playerIndex].Add(discardTile);
        
        Debug.Log($"[GameManager] Spawned discard {tileValue} for player {playerIndex} at row {row}, col {col}");
    }

    [ClientRpc]
    private void RpcShowWinScreen(int winnerIndex, string winnerName, int score, HandAnalysisResult analysis, List<int> tileSortValues, List<string> flowerMessages)
    {
        Debug.Log($"WINNER: {winnerName} (Seat {winnerIndex}) - {score} points");
        Debug.Log($"Tile values count: {tileSortValues?.Count ?? 0}");
        Debug.Log($"Flower messages count: {flowerMessages?.Count ?? 0}");
        
        // Find and show result screen
        ResultScreenUI resultScreen = FindFirstObjectByType<ResultScreenUI>();
        if (resultScreen != null)
        {
            resultScreen.ShowResult(analysis, score, winnerIndex, tileSortValues, flowerMessages);
        }
    }

    [ClientRpc]
    private void RpcGameEndedDraw()
    {
        Debug.Log("Game ended in a draw - wall exhausted");
    }

    // ===== HELPERS =====

    private void OnCurrentPlayerChanged(int oldIndex, int newIndex)
    {
        Debug.Log($"Turn: Player {newIndex}");
        // Update UI to show current turn
    }

    private GameObject FindTilePrefabBySortValue(int sortValue)
    {
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

    private string GetPathToTransform(Transform t)
    {
        if (t == null) return "";
        
        string path = t.name;
        Transform parent = t.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
    
    /// <summary>
    /// Find a tile to remove from a complete winning hand to make it 1-away (iishanten).
    /// Removes from the middle of a sequence or one from a triplet/pair.
    /// </summary>
    private int FindTileToRemoveForIishanten(List<int> completeHand)
    {
        if (completeHand.Count != 14) return 0;
        
        // Count occurrences of each tile
        var counts = new Dictionary<int, int>();
        foreach (int tile in completeHand)
        {
            if (!counts.ContainsKey(tile))
                counts[tile] = 0;
            counts[tile]++;
        }
        
        // Strategy 1: Remove from a triplet (leaves a pair)
        foreach (var kvp in counts)
        {
            if (kvp.Value == 3)
            {
                return kvp.Key; // Remove one tile from this triplet
            }
        }
        
        // Strategy 2: Remove the middle tile of a sequence (breaks the sequence)
        List<int> sorted = new List<int>(completeHand);
        sorted.Sort();
        
        foreach (int tile in sorted)
        {
            int suit = tile / 100;
            // Only numbered tiles can form sequences
            if (suit >= 1 && suit <= 3)
            {
                int value = tile % 100;
                // Check if this is the middle of a sequence (e.g., 2 in 1-2-3)
                if (value >= 2 && value <= 8)
                {
                    int prev = tile - 1;
                    int next = tile + 1;
                    
                    if (sorted.Contains(prev) && sorted.Contains(next))
                    {
                        return tile; // Remove the middle tile
                    }
                }
            }
        }
        
        // Strategy 3: Remove from a pair (leaves a single)
        foreach (var kvp in counts)
        {
            if (kvp.Value == 2)
            {
                return kvp.Key; // Remove one tile from this pair
            }
        }
        
        // Fallback: return 0 (caller will remove last tile)
        return 0;
    }

    // ===== PUBLIC ACCESSORS =====

    public int CurrentPlayerIndex => currentPlayerIndex;
    public int WallTilesRemaining => wallTilesRemaining;
    public Transform[] PlayerHandPositions => playerHandPositions;
    public Transform[] PlayerDiscardPositions => playerDiscardPositions;
    public Transform[] PlayerKongPositions => playerKongPositions;
    public GameObject[] TilePrefabs => tilePrefabs;

    private bool waitingForInterruptResponses = false;
    private int lastDiscardedTile = 0;
    private int lastDiscardingPlayer = -1;
    private Dictionary<int, InterruptActionType> interruptResponses = new Dictionary<int, InterruptActionType>();
    private List<int> playersWhoCanInterrupt = new List<int>();

    // --- NEW METHOD: Check for interrupts after discard ---

    /// <summary>
    /// Called after a player discards. Checks if any other players can Chi/Pon/Kong.
    /// </summary>
    [Server]
    private void CheckForInterrupts(int discardingPlayer, int discardedTile)
    {
        Debug.Log($"[GameManager] Checking for interrupts on tile {discardedTile} from Player {discardingPlayer}");
        
        playersWhoCanInterrupt.Clear();
        interruptResponses.Clear();
        lastDiscardedTile = discardedTile;
        lastDiscardingPlayer = discardingPlayer;
        waitingForInterruptResponses = false;
        
        // Check each other player for interrupt options
        for (int i = 0; i < players.Count; i++)
        {
            if (i == discardingPlayer) continue; // Can't call on your own discard
            
            NetworkPlayer checkPlayer = players[i];
            NetworkedPlayerHand checkHand = playerHands[i];
            
            // Calculate what this player can do
            bool canChi = CanPlayerChi(i, discardedTile, discardingPlayer);
            bool canPon = CanPlayerPon(i, discardedTile);
            bool canKong = CanPlayerKong(i, discardedTile);
            
            if (canChi || canPon || canKong)
            {
                Debug.Log($"[GameManager] Player {i} has interrupt options - Chi:{canChi}, Pon:{canPon}, Kong:{canKong}");
                
                int canChiFlag = canChi ? 1 : 0;
                int canPonFlag = canPon ? 1 : 0;
                int canKongFlag = canKong ? 1 : 0;
                
                // CORRECTED CALL - 4 parameters in correct order
                checkHand.TargetCheckInterruptOptions(
                    checkPlayer.connectionToClient, 
                    canChiFlag, 
                    canPonFlag, 
                    canKongFlag,
                    discardedTile);
                
                playersWhoCanInterrupt.Add(i);
                waitingForInterruptResponses = true;
            }
        }
        
        if (waitingForInterruptResponses)
        {
            Debug.Log($"[GameManager] Waiting for {playersWhoCanInterrupt.Count} players to respond");
            // DISABLED: Don't auto-pass anymore
            // StartCoroutine(InterruptResponseTimeout());
        }
        else
        {
            Debug.Log($"[GameManager] No interrupts possible, advancing turn");
            currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
            StartPlayerTurn(currentPlayerIndex);
        }
    }

    /// <summary>
    /// Called by clients when they choose to interrupt (or pass).
    /// </summary>
    [Server]
    public void PlayerRespondedToInterrupt(int playerIndex, InterruptActionType action)
    {
        if (!waitingForInterruptResponses) return;
        
        Debug.Log($"[GameManager] Player {playerIndex} responded: {action}");
        
        interruptResponses[playerIndex] = action;
        
        // Check if all players have responded
        bool allResponded = playersWhoCanInterrupt.All(p => interruptResponses.ContainsKey(p));
        
        if (allResponded)
        {
            ProcessInterruptResponses();
        }
    }

    /// <summary>
    /// Process all interrupt responses and determine priority.
    /// Priority: Ron > Kong = Pon > Chi
    /// </summary>
    [Server]
    private void ProcessInterruptResponses()
    {
        waitingForInterruptResponses = false;
        StopAllCoroutines(); // Stop timeout
        
        Debug.Log($"[GameManager] Processing {interruptResponses.Count} interrupt responses");
        
        // Find highest priority action
        int winningPlayer = -1;
        InterruptActionType winningAction = InterruptActionType.Pass;
        
        // Check for Ron (highest priority)
        foreach (var response in interruptResponses)
        {
            if (response.Value == InterruptActionType.Ron)
            {
                winningPlayer = response.Key;
                winningAction = InterruptActionType.Ron;
                break;
            }
        }
        
        // Check for Kong/Pon (same priority, first come first serve)
        if (winningAction == InterruptActionType.Pass)
        {
            foreach (var response in interruptResponses)
            {
                if (response.Value == InterruptActionType.Kong || response.Value == InterruptActionType.Pon)
                {
                    winningPlayer = response.Key;
                    winningAction = response.Value;
                    break;
                }
            }
        }
        
        // Check for Chi (lowest priority, only from next player)
        if (winningAction == InterruptActionType.Pass)
        {
            int nextPlayer = (lastDiscardingPlayer + 1) % players.Count;
            
            if (interruptResponses.ContainsKey(nextPlayer) && 
                interruptResponses[nextPlayer] == InterruptActionType.Chi)
            {
                winningPlayer = nextPlayer;
                winningAction = InterruptActionType.Chi;
            }
        }
        
        // Execute the winning action or continue turn
        if (winningAction != InterruptActionType.Pass && winningPlayer != -1)
        {
            Debug.Log($"[GameManager] Player {winningPlayer} interrupts with {winningAction}");
            ExecuteInterruptAction(winningPlayer, winningAction);
        }
        else
        {
            Debug.Log($"[GameManager] No interrupts, advancing turn");
            // Advance turn
            currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
            StartPlayerTurn(currentPlayerIndex);
        }
    }

    /// <summary>
    /// Execute the interrupt action (player takes the discarded tile).
    /// </summary>
    [Server]
    private void ExecuteInterruptAction(int interruptingPlayer, InterruptActionType action)
    {
        Debug.Log($"[GameManager] Executing {action} for Player {interruptingPlayer}");
        
        NetworkPlayer player = players[interruptingPlayer];
        NetworkedPlayerHand hand = playerHands[interruptingPlayer];
        
        // Remove the discarded tile from discard area
        RpcRemoveLastDiscardTile(lastDiscardingPlayer);
        
        // CRITICAL: Decrement the discard count so next discard fills the gap
        playerDiscardCounts[lastDiscardingPlayer]--;
        
        Debug.Log($"[GameManager] Player {lastDiscardingPlayer}'s discard count: {playerDiscardCounts[lastDiscardingPlayer]}");
        
        // Tell the interrupting player to execute locally
        hand.TargetExecuteInterrupt(player.connectionToClient, action, lastDiscardedTile);
        
        // NEW: If Kong from discard, draw a replacement tile
        if (action == InterruptActionType.Kong)
        {
            Debug.Log($"[GameManager] Kong from discard - drawing replacement tile");
            
            if (wallTiles.Count > 0)
            {
                int replacementTile = DrawTileFromWall();
                
                // Check if it's a flower
                while (IsFlowerTile(replacementTile) && wallTiles.Count > 0)
                {
                    Debug.Log($"[GameManager] Drew flower {replacementTile}, showing and replacing");
                    RpcShowFlowerTile(interruptingPlayer, replacementTile);
                    replacementTile = DrawTileFromWall();
                }
                
                Debug.Log($"[GameManager] Sending replacement tile {replacementTile} to Player {interruptingPlayer}");
                RpcDrawTile(interruptingPlayer, replacementTile);
            }
            else
            {
                Debug.LogWarning($"[GameManager] No tiles left in wall for Kong replacement");
            }
        }
        
        // Give turn to interrupting player
        currentPlayerIndex = interruptingPlayer;
        
        // NOTE: Don't call StartPlayerTurn here - player needs to discard first
        // For Pon/Chi, they just discard
        // For Kong, they draw (above) then discard
    }

    [Server]
    private System.Collections.IEnumerator BroadcastMeldAfterDelay(int playerIndex, InterruptActionType action, int calledTile)
    {
        // Wait for client to execute locally
        yield return new WaitForSeconds(0.3f);
        
        // Determine what tiles to broadcast
        List<int> meldTiles = new List<int>();
        
        if (action == InterruptActionType.Pon)
        {
            meldTiles = new List<int> { calledTile, calledTile, calledTile };
        }
        else if (action == InterruptActionType.Kong)
        {
            meldTiles = new List<int> { calledTile, calledTile, calledTile, calledTile };
        }
        else if (action == InterruptActionType.Chi)
        {
            // Try to get Chi tiles from client
            if (lastChiTilesPerPlayer.ContainsKey(playerIndex) && 
                lastChiTilesPerPlayer[playerIndex].Count == 3)
            {
                meldTiles = new List<int>(lastChiTilesPerPlayer[playerIndex]);
                Debug.Log($"[GameManager] Using Chi tiles from client: {string.Join(", ", meldTiles)}");
            }
            else
            {
                // Fallback: construct from called tile
                int suit = lastDiscardedTile / 100;
                int value = lastDiscardedTile % 100;
                
                meldTiles = new List<int> { 
                    suit * 100 + (value - 1), 
                    lastDiscardedTile, 
                    suit * 100 + (value + 1) 
                };
                Debug.LogWarning($"[GameManager] Using fallback Chi tiles: {string.Join(", ", meldTiles)}");
            }
        }
        
        Debug.Log($"[GameManager] Broadcasting {action} meld with {meldTiles.Count} tiles to all clients");
        
        // Broadcast to ALL clients
        RpcShowMeld(playerIndex, action, calledTile, meldTiles);
    }

    /// <summary>
    /// Timeout for interrupt responses (10 seconds).
    /// </summary>
    private System.Collections.IEnumerator InterruptResponseTimeout()
    {
        yield return new WaitForSeconds(10f);
        
        if (waitingForInterruptResponses)
        {
            Debug.Log("[GameManager] Interrupt timeout - treating missing responses as 'Pass'");
            
            // Fill in missing responses as "None"
            foreach (int playerIndex in playersWhoCanInterrupt)
            {
                if (!interruptResponses.ContainsKey(playerIndex))
                {
                    interruptResponses[playerIndex] = InterruptActionType.Pass;
                }
            }
            
            ProcessInterruptResponses();
        }
    }

    // --- MODIFY PlayerDiscardedTile ---
    // Replace the existing method with this version:

    [Server]
    public void PlayerDiscardedTile(int playerIndex, int tileValue, Vector3 localHandPosition)
    {
        if (playerIndex != currentPlayerIndex)
        {
            Debug.LogWarning($"Player {playerIndex} discarded out of turn!");
            return;
        }

        playerDiscards[playerIndex].Add(tileValue);
        int discardIndex = playerDiscardCounts[playerIndex];
        playerDiscardCounts[playerIndex]++;

        Debug.Log($"Player {playerIndex} discarded {tileValue}");

        // Spawn discard tile for all clients
        RpcSpawnDiscardTile(playerIndex, tileValue, discardIndex);

        // NEW: Check for interrupts instead of immediately advancing
        CheckForInterrupts(playerIndex, tileValue);
    }

    public int GetPlayerCount()
    {
        return players != null ? players.Count : 0;
    }

    /// <summary>
    /// Show a completed meld to all clients.
    /// </summary>
    [ClientRpc]
    private void RpcShowMeld(int playerIndex, InterruptActionType meldType, int calledTile, List<int> allTileSortValues)
    {
        Debug.Log($"[GameManager] ═══════════════════════════════");
        Debug.Log($"[GameManager] RPC ShowMeld START");
        Debug.Log($"[GameManager] Player {playerIndex} declared {meldType}");
        Debug.Log($"[GameManager] Tiles: {string.Join(", ", allTileSortValues)}");
        
        // Find the LOCAL player who is viewing this RPC
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        if (localPlayer == null)
        {
            Debug.LogError($"[GameManager] Cannot find local player!");
            return;
        }
        
        int localSeat = localPlayer.PlayerIndex;
        Debug.Log($"[GameManager] I am Player {localSeat}");
        
        // Skip if this is our own meld (already shown locally in ExecuteChi/ExecutePon/ExecuteKong)
        if (localSeat == playerIndex)
        {
            Debug.Log($"[GameManager] This is MY meld - already shown locally, skipping RPC");
            Debug.Log($"[GameManager] ═══════════════════════════════");
            return;
        }
        
        // Get the LOCAL player's hand component
        NetworkedPlayerHand localHand = localPlayer.GetComponent<NetworkedPlayerHand>();
        if (localHand == null)
        {
            Debug.LogError($"[GameManager] Local player has no NetworkedPlayerHand!");
            return;
        }
        
        // Tell the LOCAL hand to display the OPPONENT's meld
        Debug.Log($"[GameManager] → Calling ShowOpponentMeld on MY hand (Player {localSeat})");
        Debug.Log($"[GameManager] → To display Player {playerIndex}'s {meldType}");
        
        localHand.ShowOpponentMeld(playerIndex, meldType, calledTile, allTileSortValues);
        
        Debug.Log($"[GameManager] ═══════════════════════════════");
    }

    /// <summary>
    /// Check if a player can Chi (call for sequence) on the discarded tile.
    /// Chi can only be called from the player to the left of the discarding player.
    /// </summary>
    /// <summary>
    /// Check if a player can Chi (call for sequence) on the discarded tile.
    /// Chi can only be called from the player to the left of the discarding player.
    /// </summary>
    [Server]
    private bool CanPlayerChi(int playerIndex, int discardedTile, int discardingPlayer)
    {
        // Chi rule: Only the NEXT player (to the left) can Chi
        int playerCount = players.Count;
        int nextPlayer = (discardingPlayer + 1) % playerCount;
        
        if (playerIndex != nextPlayer)
        {
            return false; // Not the next player
        }
        
        // Check if the tile is a numbered tile (Chi only works on numbered tiles)
        int suit = discardedTile / 100;
        if (suit < 1 || suit > 3) return false; // Not a numbered suit
        
        int value = discardedTile % 100;
        if (value < 1 || value > 9) return false; // Invalid value
        
        // Tile is valid for Chi, let client determine if they have the tiles
        return true;
    }

    /// <summary>
    /// Check if a player can Pon (call for triplet) on the discarded tile.
    /// Server always asks, client validates.
    /// </summary>
    [Server]
    private bool CanPlayerPon(int playerIndex, int discardedTile)
    {
        // Let all players check - client will validate if they have 2 matching tiles
        return true;
    }

    /// <summary>
    /// Check if a player can Kong (call for quad) on the discarded tile.
    /// Server always asks, client validates.
    /// </summary>
    [Server]
    private bool CanPlayerKong(int playerIndex, int discardedTile)
    {
        // Let all players check - client will validate if they have 3 matching tiles
        return true;
    }

    // Add these fields at the top of the class
    private Dictionary<int, MeldBroadcastData> pendingMeldBroadcasts = new Dictionary<int, MeldBroadcastData>();

    // Helper struct
    private struct MeldBroadcastData
    {
        public InterruptActionType meldType;
        public int calledTile;
        public List<int> tileSortValues;
    }

    // Add this method
    [Server]
    public void StoreMeldForBroadcast(int playerIndex, InterruptActionType meldType, int calledTile, List<int> tileSortValues)
    {
        Debug.Log($"[GameManager] Storing {meldType} meld for Player {playerIndex}: {string.Join(", ", tileSortValues)}");
        
        pendingMeldBroadcasts[playerIndex] = new MeldBroadcastData
        {
            meldType = meldType,
            calledTile = calledTile,
            tileSortValues = new List<int>(tileSortValues)
        };
        
        // Broadcast immediately
        RpcShowMeld(playerIndex, meldType, calledTile, tileSortValues);
    }

    [Server]
    public void UpdateLastChiTiles(int playerIndex, List<int> chiTiles)
    {
        Debug.Log($"[GameManager] Received Chi tiles from Player {playerIndex}: {string.Join(", ", chiTiles)}");
        lastChiTilesPerPlayer[playerIndex] = new List<int>(chiTiles);
    }

    [ClientRpc]
    private void RpcRemoveLastDiscardTile(int playerIndex)
    {
        if (!spawnedDiscardTiles.ContainsKey(playerIndex)) return;
        
        List<GameObject> playerDiscards = spawnedDiscardTiles[playerIndex];
        if (playerDiscards.Count == 0) return;
        
        // Remove and destroy the LAST tile (most recent discard)
        GameObject lastDiscard = playerDiscards[playerDiscards.Count - 1];
        playerDiscards.RemoveAt(playerDiscards.Count - 1);
        
        if (lastDiscard != null)
        {
            Destroy(lastDiscard);
            Debug.Log($"[GameManager] Removed last discard tile from player {playerIndex}");
        }
        
        // REPOSITION ALL REMAINING TILES to fill the gap
        RepositionDiscardTiles(playerIndex);
    }

    /// <summary>
    /// Reposition all discard tiles for a player to remove gaps
    /// </summary>
    private void RepositionDiscardTiles(int playerIndex)
    {
        if (!spawnedDiscardTiles.ContainsKey(playerIndex)) return;
        
        List<GameObject> playerDiscards = spawnedDiscardTiles[playerIndex];
        Transform discardArea = playerDiscardPositions[playerIndex];
        
        if (discardArea == null) return;
        
        Debug.Log($"[RepositionDiscardTiles] Repositioning {playerDiscards.Count} tiles for player {playerIndex}");
        
        // Reposition each tile based on its index
        for (int i = 0; i < playerDiscards.Count; i++)
        {
            GameObject tile = playerDiscards[i];
            if (tile == null) continue;
            
            // ===== UPDATED: 8 tiles per row (was 6) =====
            int row = i / 8;
            int col = i % 8;
            
            // ===== UPDATED: Position and rotation based on player =====
            Vector3 newPos;
            Quaternion tileRotation;
            
            if (playerIndex == 0)
            {
                // Player 0: No rotation, standard grid
                // Row spacing 0.16 (was 0.8)
                newPos = discardArea.position + new Vector3(col * 0.12f, 0.01f, -row * 0.16f);
                tileRotation = Quaternion.identity;
            }
            else
            {
                // Players 1, 2, 3: 90° CCW rotation with swapped axes
                // Tiles still generate left-to-right visually
                // After 90° CCW rotation:
                // - Columns (left-to-right) use Z axis (positive Z = right)
                // - Rows (top-to-bottom) use X axis (positive X = down)
                
                // CORRECTED: No negative on row, positive on col
                newPos = discardArea.position + new Vector3(row * 0.16f, 0.01f, col * 0.12f);
                tileRotation = Quaternion.Euler(0f, -90f, 0f);
            }
            
            tile.transform.position = newPos;
            tile.transform.rotation = tileRotation;
            
            Debug.Log($"[RepositionDiscardTiles]   Player {playerIndex}, tile {i}: row={row}, col={col}, pos={newPos}");
        }
        
        Debug.Log($"[GameManager] Repositioned {playerDiscards.Count} discard tiles for player {playerIndex}");
    }

    // ===== PUBLIC ACCESSORS =====
    
    /// <summary>
    /// Get all spawned discard tiles for highlighting purposes.
    /// </summary>
    public Dictionary<int, List<GameObject>> GetSpawnedDiscardTiles()
    {
        return spawnedDiscardTiles;
    }

    /// <summary>
    /// Draw a replacement tile for a flower (recursive until non-flower is drawn).
    /// </summary>
    [Server]
    public void DrawFlowerReplacement(int playerIndex)
    {
        Debug.Log($"[GameManager] Drawing flower replacement for Player {playerIndex}");
        
        if (playerIndex < 0 || playerIndex >= players.Count)
        {
            Debug.LogError($"[GameManager] Invalid player index: {playerIndex}");
            return;
        }
        
        if (wallTiles.Count == 0)
        {
            Debug.LogWarning("[GameManager] Wall is empty! Cannot draw replacement tile.");
            return;
        }
        
        NetworkPlayer player = players[playerIndex];
        int replacementTile = DrawTileFromWall();
        
        Debug.Log($"[GameManager] Drew replacement tile {replacementTile} for Player {playerIndex}");
        
        // Send to player (TargetDrawTile will handle if it's another flower recursively)
        NetworkedPlayerHand hand = playerHands[playerIndex];
        hand.TargetDrawTile(player.connectionToClient, replacementTile);
    }

    // ===== ROUND-ROBIN SYSTEM =====
    
    /// <summary>
    /// Start a new round after a win.
    /// </summary>
    [Server]
    public void StartNewRound()
    {
        Debug.Log("[Game] ========== STARTING NEW ROUND ==========");
        
        // Clear previous round data
        ClearRoundData();
        
        // Reinitialize game state
        InitializeWall();
        DealInitialHands();
        
        currentPlayerIndex = 0;
        gameStarted = true;
        
        // Notify all clients
        RpcHideResultScreen();
        RpcGameStarted();
        StartPlayerTurn(currentPlayerIndex);
        
        Debug.Log("[Game] ========== NEW ROUND STARTED ==========");
    }
    
    /// <summary>
    /// Handle win and determine if seats should rotate.
    /// </summary>
    [Server]
    private void HandleWin(int winnerSeatIndex)
    {
        Debug.Log($"[Game] HandleWin called - Winner seat: {winnerSeatIndex}");
        
        bool needsRotation = false;
        
        if (winnerSeatIndex == 0)
        {
            consecutiveEastWins++;
            Debug.Log($"[Game] East (Seat 0) wins - consecutive count: {consecutiveEastWins}/3");
            
            if (consecutiveEastWins >= 3)
            {
                Debug.Log("[Game] East won 3 times in a row - forcing rotation");
                needsRotation = true;
                consecutiveEastWins = 0;
            }
        }
        else
        {
            Debug.Log($"[Game] Non-East player (Seat {winnerSeatIndex}) wins - rotation required");
            needsRotation = true;
            consecutiveEastWins = 0;
        }
        
        if (needsRotation)
        {
            RotatePlayerSeats();
        }
        else
        {
            Debug.Log("[Game] No rotation - East retains position");
        }
    }
    
    /// <summary>
    /// Rotate all players clockwise by one seat.
    /// </summary>
    [Server]
    private void RotatePlayerSeats()
    {
        Debug.Log("[Game] ===== ROTATING PLAYER SEATS =====");
        
        // Log current seats
        for (int i = 0; i < players.Count; i++)
        {
            Debug.Log($"[Game] BEFORE: Player {i} ({players[i].Username}) -> Seat {playerSeats[i]}");
        }
        
        // Create new seat assignments (everyone moves up one seat)
        int[] newSeats = new int[4];
        for (int i = 0; i < players.Count; i++)
        {
            int currentSeat = playerSeats[i];
            newSeats[i] = (currentSeat + 1) % players.Count;
        }
        playerSeats = newSeats;
        
        // Log new seats
        for (int i = 0; i < players.Count; i++)
        {
            Debug.Log($"[Game] AFTER: Player {i} ({players[i].Username}) -> Seat {playerSeats[i]}");
        }
        
        // Check if we've completed a full rotation
        bool fullRotation = true;
        for (int i = 0; i < players.Count; i++)
        {
            if (playerSeats[i] != i)
            {
                fullRotation = false;
                break;
            }
        }
        
        if (fullRotation)
        {
            Debug.Log("[Game] FULL ROTATION COMPLETED - advancing round wind");
            AdvanceRoundWind();
        }
        
        // Update all players with their new seats and winds
        for (int i = 0; i < players.Count; i++)
        {
            int newSeat = playerSeats[i];
            players[i].SetPlayerIndex(newSeat);
            
            // Calculate winds: East=401, South=402, West=403, North=404
            int seatWind = 401 + newSeat;
            
            Debug.Log($"[Game] Updating Player {i}: Seat={newSeat}, SeatWind={seatWind}, RoundWind={roundWind}");
            RpcUpdatePlayerWind(players[i].connectionToClient, seatWind, roundWind);
        }
        
        Debug.Log("[Game] ===== SEAT ROTATION COMPLETE =====");
    }
    
    /// <summary>
    /// Advance round wind after full rotation.
    /// </summary>
    [Server]
    private void AdvanceRoundWind()
    {
        int oldWind = roundWind;
        
        switch (roundWind)
        {
            case 401: roundWind = 402; break; // East -> South
            case 402: roundWind = 403; break; // South -> West
            case 403: roundWind = 404; break; // West -> North
            case 404: roundWind = 401; break; // North -> East
        }
        
        Debug.Log($"[Game] Round wind advanced: {oldWind} -> {roundWind}");
    }
    
    /// <summary>
    /// Clear all data from previous round.
    /// </summary>
    [Server]
    private void ClearRoundData()
    {
        Debug.Log("[Game] Clearing round data...");
        
        // Clear discard tracking
        foreach (var kvp in playerDiscards)
        {
            kvp.Value.Clear();
        }
        playerDiscardCounts.Clear();
        
        // Reinitialize discard tracking
        for (int i = 0; i < 4; i++)
        {
            if (!playerDiscards.ContainsKey(i))
            {
                playerDiscards[i] = new List<int>();
            }
            playerDiscardCounts[i] = 0;
        }
        
        // Clear player hands
        foreach (var hand in playerHands.Values)
        {
            if (hand != null)
            {
                hand.ClearHand();
            }
        }
        
        // Clear spawned discard tiles
        foreach (var kvp in spawnedDiscardTiles)
        {
            foreach (GameObject tile in kvp.Value)
            {
                if (tile != null)
                {
                    NetworkServer.Destroy(tile);
                }
            }
        }
        spawnedDiscardTiles.Clear();
        
        Debug.Log("[Game] Round data cleared");
    }
    
    /// <summary>
    /// Update player wind values on client.
    /// </summary>
    [TargetRpc]
    private void RpcUpdatePlayerWind(NetworkConnection target, int seatWind, int roundWindValue)
    {
        PlayerHand.PLAYER_WIND_SORT_VALUE = seatWind;
        PlayerHand.ROUND_WIND_SORT_VALUE = roundWindValue;
        Debug.Log($"[Client] Winds updated - Seat: {seatWind}, Round: {roundWindValue}");
    }
    
    /// <summary>
    /// Hide result screen on all clients.
    /// </summary>
    [ClientRpc]
    private void RpcHideResultScreen()
    {
        ResultScreenUI resultScreen = FindFirstObjectByType<ResultScreenUI>();
        if (resultScreen != null)
        {
            resultScreen.Hide();
        }
    }
}