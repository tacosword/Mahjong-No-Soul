using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Networked version of TileManager - controls 4-player Mahjong game.
/// </summary>
public class NetworkedGameManager : NetworkBehaviour
{
    // Helper struct to track discard tiles with both player and seat info
    public class DiscardTileEntry
    {
        public GameObject tileObject;
        public int playerIndex;
        public int seatPosition;
    }
    
    // Store discards by SEAT POSITION (visual location) 
    // Key = seatPosition, Value = list of tiles at that seat
    private Dictionary<int, List<DiscardTileEntry>> spawnedDiscardTiles = new Dictionary<int, List<DiscardTileEntry>>();

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
    
    // STATIC variables persist across scene reloads (not destroyed with GameObject)
    private static int[] persistentPlayerSeats = new int[4] { 0, 1, 2, 3 };
    private static int[] persistentInitialSeats = new int[4] { 0, 1, 2, 3 };  // Track initial seats
    private static int persistentRoundWind = 401;
    private static int persistentConsecutiveEastWins = 0;
    private static bool hasPersistedData = false;
    
    // Instance variables (reset on scene reload)
    private int[] playerSeats = new int[4];
    private int[] initialPlayerSeats = new int[4];  // Track starting seats for full rotation detection
    private bool isReloadingForNewRound = false;

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
        Debug.Log($"hasPersistedData: {hasPersistedData}");
        
        players = new List<NetworkPlayer>(gamePlayers);
        
        // CRITICAL FIX: Restore from static variables if this is a reload
        if (hasPersistedData)
        {
            Debug.Log("[Game] *** RESTORING FROM STATIC PERSISTENCE ***");
            System.Array.Copy(persistentPlayerSeats, playerSeats, 4);
            System.Array.Copy(persistentInitialSeats, initialPlayerSeats, 4);
            roundWind = persistentRoundWind;
            consecutiveEastWins = persistentConsecutiveEastWins;
            Debug.Log($"[Game] Restored seat assignments: [{string.Join(", ", playerSeats.Take(players.Count))}]");
            Debug.Log($"[Game] Restored initial seats: [{string.Join(", ", initialPlayerSeats.Take(players.Count))}]");
            Debug.Log($"[Game] Restored round wind: {roundWind}, consecutive East wins: {consecutiveEastWins}");
        }
        else
        {
            // First game ever - initialize seats based on player count
            Debug.Log("[Game] First game - initializing seats");
            
            if (players.Count == 2)
            {
                // FOR TESTING: 2-player game starts at seats 2 and 3
                playerSeats[0] = 2;  // Player 0 at Seat 2 (North)
                playerSeats[1] = 3;  // Player 1 at Seat 3 (East)
                Debug.Log("[Game] 2-player game - using seats 2 and 3");
            }
            else
            {
                // 3 or 4 players - use sequential seats starting from 0
                for (int i = 0; i < players.Count; i++)
                {
                    playerSeats[i] = i;
                }
            }
            
            // CRITICAL: Store the initial seat assignments for full rotation detection
            System.Array.Copy(playerSeats, initialPlayerSeats, 4);
            
            roundWind = 401;
            consecutiveEastWins = 0;
            Debug.Log($"[Game] First game - initialized seat assignments: [{string.Join(", ", playerSeats.Take(players.Count))}]");
        }
        
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
        
        // CRITICAL FIX: Find which player is at the LOWEST seat number (East position)
        // For 2-player at seats 2,3: Seat 2 is East
        // For 4-player at seats 0,1,2,3: Seat 0 is East
        int eastSeat = playerSeats.Take(players.Count).Min();
        int eastPlayerIndex = -1;
        
        for (int i = 0; i < players.Count; i++)
        {
            if (playerSeats[i] == eastSeat)
            {
                eastPlayerIndex = i;
                break;
            }
        }
        
        if (eastPlayerIndex == -1)
        {
            Debug.LogError($"[Game] Could not find player at East seat {eastSeat}! Defaulting to Player 0.");
            eastPlayerIndex = 0;
        }
        
        currentPlayerIndex = eastPlayerIndex;
        Debug.Log($"[Game] Starting game. East player is PlayerIndex {eastPlayerIndex} at Seat {eastSeat}");

        Debug.Log("[Game] Sending RpcGameStarted...");
        RpcGameStarted();
        RpcBroadcastRoundWind(roundWind);
        
        Debug.Log($"[Game] Starting turn for PlayerIndex {currentPlayerIndex} (East/Seat 0)...");
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

            // CRITICAL: Use seat position from playerSeats array, not player index
            int seatPosition = playerSeats[i];
            hand.SetSeatIndex(seatPosition);
            
            // Update the player's current seat position (syncs to all clients)
            player.SetCurrentSeatPosition(seatPosition);
            
            Debug.Log($"[Game] Player {i} ({player.Username}) assigned to Seat {seatPosition}");
            
            // Check hand positions - use SEAT POSITION for container
            if (seatPosition < playerHandPositions.Length && playerHandPositions[seatPosition] != null)
            {
                string containerPath = GetPathToTransform(playerHandPositions[seatPosition]);
                Debug.Log($"[Game] Setting hand container for Seat {seatPosition}: {containerPath}");
                hand.TargetSetHandContainer(player.connectionToClient, containerPath);
            }
            else
            {
                Debug.LogError($"[Game] ✗ Hand position {seatPosition} is NULL or out of bounds!");
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
            Debug.Log($"[Game] About to call RpcReceiveInitialHand - PlayerIndex={i}, SeatPosition={seatPosition}");
            Debug.Log($"[Game] Tiles being sent: {string.Join(",", initialTiles)}");
            
            // CRITICAL: Pass SEAT POSITION (not player index) so tiles spawn at correct location
            RpcReceiveInitialHand(seatPosition, i, initialTiles);
            
            Debug.Log($"[Game] RpcReceiveInitialHand called for PlayerIndex={i} at Seat={seatPosition}");
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
        Debug.Log($"========================================");
        Debug.Log($"[StartPlayerTurn] ENTRY");
        Debug.Log($"  playerIndex: {playerIndex}");
        Debug.Log($"  players.Count: {players.Count}");
        Debug.Log($"========================================");
        
        if (playerIndex < 0 || playerIndex >= players.Count) 
        {
            Debug.LogError($"[StartPlayerTurn] INVALID playerIndex: {playerIndex}!");
            return;
        }

        NetworkPlayer targetPlayer = players[playerIndex];
        NetworkedPlayerHand targetHand = playerHands[playerIndex];
        
        // CRITICAL: Get the actual seat position for this player
        int seatPosition = playerSeats[playerIndex];
        
        Debug.Log($"[StartPlayerTurn] Player {playerIndex} ({targetPlayer.Username}) at Seat {seatPosition}");

        if (wallTiles.Count > 0)
        {
            int drawnTile = DrawTileFromWall();
            
            // Check if the drawn tile is a flower
            if (IsFlowerTile(drawnTile))
            {
                Debug.Log($"[GameManager] Player {playerIndex} (Seat {seatPosition}) drew FLOWER: {drawnTile}");
                
                // Show the flower to ALL clients - use SEAT POSITION
                RpcShowFlowerTile(seatPosition, playerIndex, drawnTile);
                
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
                            RpcShowFlowerTile(seatPosition, playerIndex, replacementTile);
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
                    RpcDrawTile(seatPosition, playerIndex, replacementTile);
                }
            }
            else
            {
                // Normal tile - send to all clients - use SEAT POSITION
                RpcDrawTile(seatPosition, playerIndex, drawnTile);
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
    private void RpcShowFlowerTile(int seatPosition, int playerIndex, int flowerTileValue)
    {
        Debug.Log($"==========================================");
        Debug.Log($"[RpcShowFlowerTile] Player {playerIndex} at Seat {seatPosition} revealed FLOWER: {flowerTileValue}");
        
        // Find the player by their permanent PlayerIndex
        NetworkPlayer targetPlayer = null;
        foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player.PlayerIndex == playerIndex)
            {
                targetPlayer = player;
                break;
            }
        }
        
        if (targetPlayer == null)
        {
            Debug.LogError($"[RpcShowFlowerTile] Could not find player with PlayerIndex {playerIndex}");
            Debug.Log($"==========================================");
            return;
        }
        
        NetworkedPlayerHand hand = targetPlayer.GetComponent<NetworkedPlayerHand>();
        if (hand == null)
        {
            Debug.LogError($"[RpcShowFlowerTile] Player {playerIndex} has no NetworkedPlayerHand!");
            Debug.Log($"==========================================");
            return;
        }
        
        Debug.Log($"[RpcShowFlowerTile] Calling ShowFlowerTileToAll on player {playerIndex}");
        hand.ShowFlowerTileToAll(flowerTileValue);
        Debug.Log($"==========================================");
    }

    /// <summary>
    /// Tell all clients that a player drew a tile (everyone can see it)
    /// </summary>
    [ClientRpc]
    private void RpcDrawTile(int seatPosition, int playerIndex, int tileValue)
    {
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        int localPlayerIndex = localPlayer?.PlayerIndex ?? -1;
        
        Debug.Log($"[RpcDrawTile] Local Player={localPlayerIndex}, Target Player={playerIndex} at Seat={seatPosition}, Tile={tileValue}");
        
        // Find the player by their permanent PlayerIndex
        NetworkPlayer targetPlayer = null;
        foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player.PlayerIndex == playerIndex)
            {
                targetPlayer = player;
                break;
            }
        }

        if (targetPlayer == null)
        {
            Debug.LogWarning($"[RpcDrawTile] Could not find player with PlayerIndex {playerIndex}");
            return;
        }

        NetworkedPlayerHand hand = targetPlayer.GetComponent<NetworkedPlayerHand>();
        if (hand == null)
        {
            Debug.LogError($"[RpcDrawTile] No hand component for player {playerIndex}");
            return;
        }

        // CRITICAL: Ensure container is set to the SEAT POSITION (not player index)
        if (hand.GetHandContainer() == null)
        {
            Debug.LogWarning($"[RpcDrawTile] Hand container not set for player {playerIndex}, setting to Seat {seatPosition}");
            GameObject container = GameObject.Find($"HandPosition_Seat{seatPosition}");
            if (container != null)
            {
                hand.SetHandContainerDirect(container.transform);
                Debug.Log($"[RpcDrawTile] ✓ Container set: {container.name}");
            }
            else
            {
                Debug.LogError($"[RpcDrawTile] ✗ Could not find HandPosition_Seat{seatPosition}");
                return;
            }
        }

        // Check if this is the local player
        bool isLocalPlayer = (localPlayer != null && localPlayer.PlayerIndex == playerIndex);
        
        Debug.Log($"[RpcDrawTile] isLocalPlayer={isLocalPlayer}");

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
            if (player.CurrentSeatPosition == playerSeat)
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
        // MUST convert playerIndex → seatPosition here because the RPC receiver
        // uses the value directly as a seat for KongArea_Seat{} container lookup.
        int kongSeatPosition = playerSeats[playerIndex];
        RpcShowSelfKong(kongSeatPosition, kongValue, kongTiles);
        
        // Turn continues - player must still discard
        Debug.Log($"[GameManager] Player {playerIndex} keeps turn after Kong");
    }

    [ClientRpc]
    private void RpcShowSelfKong(int seatPosition, int kongValue, List<int> kongTiles)
    {
        Debug.Log($"[GameManager] RPC: Seat {seatPosition} declared self-Kong");
        
        // Find the LOCAL player's hand (the one viewing this)
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        if (localPlayer == null) return;
        
        NetworkedPlayerHand localHand = localPlayer.GetComponent<NetworkedPlayerHand>();
        if (localHand == null) return;
        
        int localSeat = localPlayer.CurrentSeatPosition;
        
        // Don't show to the player who did the Kong (they already see it locally)
        if (localSeat == seatPosition)
        {
            Debug.Log($"[GameManager] Skipping self-Kong RPC - this is our Kong");
            return;
        }
        
        // Show the opponent's Kong
        Debug.Log($"[GameManager] Showing Seat {seatPosition}'s self-Kong to local Seat {localSeat}");
        localHand.ShowOpponentSelfKong(seatPosition, kongValue, kongTiles);
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

        // CRITICAL FIX: Trigger seat rotation based on winner's seat
        int winnerSeatIndex = playerSeats[playerIndex];
        HandleWin(winnerSeatIndex);

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
    /// Broadcast the current round wind to all clients so the UI can display it.
    /// </summary>
    [ClientRpc]
    private void RpcBroadcastRoundWind(int roundWindValue)
    {
        Debug.Log($"[Client] Round wind broadcast received: {roundWindValue}");
        GameUIManager.UpdateRoundWindText(roundWindValue);
    }

    /// <summary>
    /// Send initial hand to all clients - all clients spawn tiles for all players
    /// </summary>
    [ClientRpc]
    private void RpcReceiveInitialHand(int seatPosition, int playerIndex, List<int> tiles)
    {
        Debug.Log($"[RpcReceiveInitialHand] Player {playerIndex} at Seat {seatPosition}, tiles: {tiles.Count}");
        
        // Find the player by their permanent PlayerIndex
        NetworkPlayer targetPlayer = null;
        foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player.PlayerIndex == playerIndex)
            {
                targetPlayer = player;
                break;
            }
        }

        if (targetPlayer == null)
        {
            Debug.LogWarning($"[RpcReceiveInitialHand] Could not find player with PlayerIndex {playerIndex}");
            return;
        }

        Debug.Log($"[RpcReceiveInitialHand] Found player: {targetPlayer.Username}, spawning at Seat {seatPosition}");

        // Get or create hand component
        NetworkedPlayerHand hand = targetPlayer.GetComponent<NetworkedPlayerHand>();
        if (hand == null)
        {
            Debug.Log($"[RpcReceiveInitialHand] Creating NetworkedPlayerHand component");
            hand = targetPlayer.gameObject.AddComponent<NetworkedPlayerHand>();
        }

        // CRITICAL: Set the seat position (where tiles spawn), NOT player index
        hand.SetSeatIndex(seatPosition);

        // Set container to the SEAT POSITION container (rotated position)
        GameObject container = GameObject.Find($"HandPosition_Seat{seatPosition}");
        if (container != null)
        {
            hand.SetHandContainerDirect(container.transform);
            Debug.Log($"[RpcReceiveInitialHand] ✓ Container set for Seat {seatPosition}: {container.name}");
        }
        else
        {
            Debug.LogError($"[RpcReceiveInitialHand] ✗ Could not find HandPosition_Seat{seatPosition}");
        }

        // Check if this is the local player
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        bool isLocalPlayer = (localPlayer != null && localPlayer.PlayerIndex == playerIndex);

        // Call the receive method - all clients spawn visuals, but only owner gets logic
        hand.ReceiveInitialHandDirect(tiles, isLocalPlayer);
    }

    [ClientRpc]
    private void RpcSpawnDiscardTile(int seatPosition, int playerIndex, int tileValue, int discardIndex)
    {
        // Use SEAT POSITION for visual placement
        if (seatPosition >= playerDiscardPositions.Length) return;
        
        Transform discardArea = playerDiscardPositions[seatPosition];
        if (discardArea == null) return;

        GameObject tilePrefab = FindTilePrefabBySortValue(tileValue);
        if (tilePrefab == null) return;

        // 8 tiles per row
        int row = discardIndex / 8;
        int col = discardIndex % 8;

        // ===== POSITIONING WITH ROTATION ADJUSTMENT =====
        Vector3 spawnPos;
        Quaternion tileRotation;
        
        // Layout based on SEAT POSITION (visual position at table)
        switch (seatPosition)
        {
            case 0: // Seat 0 (South): No rotation (0°)
                spawnPos = discardArea.position + new Vector3(col * 0.12f, 0.01f, -row * 0.16f);
                tileRotation = Quaternion.identity;
                break;
                
            case 1: // Seat 1 (West): 90° CCW rotation
                // Grid flows left-to-right from West's perspective
                spawnPos = discardArea.position + new Vector3(row * 0.16f, 0.01f, col * 0.12f);
                tileRotation = Quaternion.Euler(0f, -90f, 0f);
                break;
                
            case 2: // Seat 2 (North): 180° rotation
                // 180° from Seat 0 baseline: X and Z both negate
                spawnPos = discardArea.position + new Vector3(-col * 0.12f, 0.01f, row * 0.16f);
                tileRotation = Quaternion.Euler(0f, 180f, 0f);
                break;
                
            case 3: // Seat 3 (East): 90° CW rotation (same as -270° or +90°)
                // Grid flows left-to-right from East's perspective  
                spawnPos = discardArea.position + new Vector3(-row * 0.16f, 0.01f, -col * 0.12f);
                tileRotation = Quaternion.Euler(0f, 90f, 0f);
                break;
                
            default:
                spawnPos = discardArea.position;
                tileRotation = Quaternion.identity;
                break;
        }
        
        GameObject discardTile = Instantiate(tilePrefab, spawnPos, tileRotation);
        
        // Disable collider so it can't be clicked
        Collider collider = discardTile.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        
        // STORE by SEAT POSITION (visual location) with player info for game logic
        if (!spawnedDiscardTiles.ContainsKey(seatPosition))
        {
            spawnedDiscardTiles[seatPosition] = new List<DiscardTileEntry>();
        }
        
        spawnedDiscardTiles[seatPosition].Add(new DiscardTileEntry
        {
            tileObject = discardTile,
            playerIndex = playerIndex,
            seatPosition = seatPosition
        });
        
        Debug.Log($"[GameManager] Spawned discard {tileValue} for player {playerIndex} at seat {seatPosition}, row {row}, col {col}");
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
    public int RoundWind => roundWind;
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
        Debug.Log($"========================================");
        Debug.Log($"[CheckForInterrupts] ENTRY");
        Debug.Log($"  discardingPlayer: {discardingPlayer}");
        Debug.Log($"  discardedTile: {discardedTile}");
        Debug.Log($"  currentPlayerIndex BEFORE: {currentPlayerIndex}");
        Debug.Log($"  players.Count: {players.Count}");
        Debug.Log($"========================================");
        
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
            StartCoroutine(InterruptResponseTimeout());
        }
        else
        {
            Debug.Log($"========================================");
            Debug.Log($"[CheckForInterrupts] *** NO INTERRUPTS - ADVANCING TURN ***");
            Debug.Log($"  currentPlayerIndex BEFORE advance: {currentPlayerIndex}");
            Debug.Log($"  players.Count: {players.Count}");
            
            int nextPlayer = (currentPlayerIndex + 1) % players.Count;
            Debug.Log($"  nextPlayer calculated: {nextPlayer}");
            
            currentPlayerIndex = nextPlayer;
            Debug.Log($"  currentPlayerIndex AFTER advance: {currentPlayerIndex}");
            Debug.Log($"  Calling StartPlayerTurn({currentPlayerIndex})...");
            
            StartPlayerTurn(currentPlayerIndex);
            
            Debug.Log($"  StartPlayerTurn returned.");
            Debug.Log($"========================================");
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
            
            // Get the interrupting player's seat position
            int interruptingSeat = playerSeats[interruptingPlayer];
            
            if (wallTiles.Count > 0)
            {
                int replacementTile = DrawTileFromWall();
                
                // Check if it's a flower
                while (IsFlowerTile(replacementTile) && wallTiles.Count > 0)
                {
                    Debug.Log($"[GameManager] Drew flower {replacementTile}, showing and replacing");
                    RpcShowFlowerTile(interruptingSeat, interruptingPlayer, replacementTile);
                    replacementTile = DrawTileFromWall();
                }
                
                Debug.Log($"[GameManager] Sending replacement tile {replacementTile} to Player {interruptingPlayer} at Seat {interruptingSeat}");
                RpcDrawTile(interruptingSeat, interruptingPlayer, replacementTile);
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
        Debug.Log($"========================================");
        Debug.Log($"[PlayerDiscardedTile] CALLED");
        Debug.Log($"  playerIndex: {playerIndex}");
        Debug.Log($"  currentPlayerIndex: {currentPlayerIndex}");
        Debug.Log($"  tileValue: {tileValue}");
        Debug.Log($"  playerSeats: [{string.Join(", ", playerSeats)}]");
        Debug.Log($"========================================");
        
        if (playerIndex != currentPlayerIndex)
        {
            Debug.LogWarning($"Player {playerIndex} discarded out of turn!");
            return;
        }

        playerDiscards[playerIndex].Add(tileValue);
        int discardIndex = playerDiscardCounts[playerIndex];
        playerDiscardCounts[playerIndex]++;

        Debug.Log($"[PlayerDiscardedTile] Player {playerIndex} discarded {tileValue}");

        // Get the seat position for visual placement
        int seatPosition = playerSeats[playerIndex];
        Debug.Log($"[PlayerDiscardedTile] Player {playerIndex} sits at Seat {seatPosition}");

        // Spawn discard tile at the correct SEAT POSITION
        RpcSpawnDiscardTile(seatPosition, playerIndex, tileValue, discardIndex);

        // Hide the drawn tile placeholder on all other clients now that the discard is done
        RpcHideDrawnTile(seatPosition);

        // NEW: Check for interrupts instead of immediately advancing
        Debug.Log($"[PlayerDiscardedTile] Calling CheckForInterrupts...");
        CheckForInterrupts(playerIndex, tileValue);
        Debug.Log($"[PlayerDiscardedTile] DONE");
    }

    public int GetPlayerCount()
    {
        return players != null ? players.Count : 0;
    }

    /// <summary>
    /// Show a completed meld to all clients.
    /// </summary>
    [ClientRpc]
    private void RpcShowMeld(int seatPosition, InterruptActionType meldType, int calledTile, List<int> allTileSortValues)
    {
        Debug.Log($"[GameManager] ═══════════════════════════════");
        Debug.Log($"[GameManager] RPC ShowMeld START");
        Debug.Log($"[GameManager] Seat {seatPosition} declared {meldType}");
        Debug.Log($"[GameManager] Tiles: {string.Join(", ", allTileSortValues)}");
        
        // Find the LOCAL player who is viewing this RPC
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        if (localPlayer == null)
        {
            Debug.LogError($"[GameManager] Cannot find local player!");
            return;
        }
        
        int localSeat = localPlayer.CurrentSeatPosition;
        Debug.Log($"[GameManager] Local player: CurrentSeat={localSeat}");
        Debug.Log($"[GameManager] Meld creator: Seat={seatPosition}");
        
        // Skip if this is our own meld (already shown locally in ExecuteChi/ExecutePon/ExecuteKong)
        if (localSeat == seatPosition)
        {
            Debug.Log($"[GameManager] ✓ This is MY meld (Seat match) - already shown locally, SKIPPING RPC");
            Debug.Log($"[GameManager] ═══════════════════════════════");
            return;
        }
        
        Debug.Log($"[GameManager] This is NOT my meld - will display opponent's meld");
        
        // Get the LOCAL player's hand component
        NetworkedPlayerHand localHand = localPlayer.GetComponent<NetworkedPlayerHand>();
        if (localHand == null)
        {
            Debug.LogError($"[GameManager] Local player has no NetworkedPlayerHand!");
            return;
        }
        
        // Tell the LOCAL hand to display the OPPONENT's meld at the correct seat
        Debug.Log($"[GameManager] → Calling ShowOpponentMeld for Seat {seatPosition}'s {meldType}");
        
        localHand.ShowOpponentMeld(seatPosition, meldType, calledTile, allTileSortValues);
        
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
        // Convert playerIndex → seatPosition before broadcast.
        // RpcShowMeld's receiver uses this value for KongArea_Seat{} container lookup
        // and for the skip check (comparing against local CurrentSeatPosition).
        int seatPosition = playerSeats[playerIndex];
        Debug.Log($"[GameManager] Storing {meldType} meld for PlayerIndex={playerIndex} (Seat={seatPosition}): {string.Join(", ", tileSortValues)}");
        
        pendingMeldBroadcasts[playerIndex] = new MeldBroadcastData
        {
            meldType = meldType,
            calledTile = calledTile,
            tileSortValues = new List<int>(tileSortValues)
        };
        
        // Broadcast immediately — pass seatPosition, not playerIndex
        RpcShowMeld(seatPosition, meldType, calledTile, tileSortValues);
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
        // Find the most recent discard by this player (search all seats)
        DiscardTileEntry mostRecentDiscard = null;
        int seatWithDiscard = -1;
        
        foreach (var kvp in spawnedDiscardTiles)
        {
            int seat = kvp.Key;
            List<DiscardTileEntry> tilesAtSeat = kvp.Value;
            
            // Search backwards to find most recent tile from this player
            for (int i = tilesAtSeat.Count - 1; i >= 0; i--)
            {
                if (tilesAtSeat[i].playerIndex == playerIndex)
                {
                    mostRecentDiscard = tilesAtSeat[i];
                    seatWithDiscard = seat;
                    break;
                }
            }
            
            if (mostRecentDiscard != null) break;
        }
        
        if (mostRecentDiscard == null)
        {
            Debug.LogWarning($"[RpcRemoveLastDiscardTile] Could not find discard from player {playerIndex}");
            return;
        }
        
        // Remove from list
        spawnedDiscardTiles[seatWithDiscard].Remove(mostRecentDiscard);
        
        // Destroy visual object
        if (mostRecentDiscard.tileObject != null)
        {
            Destroy(mostRecentDiscard.tileObject);
            Debug.Log($"[GameManager] Removed last discard from player {playerIndex} at seat {seatWithDiscard}");
        }
        
        // Reposition remaining tiles at that seat
        RepositionDiscardTiles(seatWithDiscard);
    }

    /// <summary>
    /// Reposition all discard tiles at a seat position to remove gaps
    /// </summary>
    private void RepositionDiscardTiles(int seatPosition)
    {
        if (!spawnedDiscardTiles.ContainsKey(seatPosition)) return;
        
        List<DiscardTileEntry> tilesAtSeat = spawnedDiscardTiles[seatPosition];
        Transform discardArea = playerDiscardPositions[seatPosition];
        
        if (discardArea == null) return;
        
        Debug.Log($"[RepositionDiscardTiles] Repositioning {tilesAtSeat.Count} tiles at seat {seatPosition}");
        
        // Reposition each tile based on its index
        for (int i = 0; i < tilesAtSeat.Count; i++)
        {
            GameObject tile = tilesAtSeat[i].tileObject;
            if (tile == null) continue;
            
            // 8 tiles per row
            int row = i / 8;
            int col = i % 8;
            
            // Position and rotation based on SEAT POSITION (must match RpcSpawnDiscardTile)
            Vector3 newPos;
            Quaternion tileRotation;
            
            switch (seatPosition)
            {
                case 0: // Seat 0 (South): No rotation (0°)
                    newPos = discardArea.position + new Vector3(col * 0.12f, 0.01f, -row * 0.16f);
                    tileRotation = Quaternion.identity;
                    break;
                    
                case 1: // Seat 1 (West): 90° CCW rotation
                    newPos = discardArea.position + new Vector3(row * 0.16f, 0.01f, col * 0.12f);
                    tileRotation = Quaternion.Euler(0f, -90f, 0f);
                    break;
                    
                case 2: // Seat 2 (North): 180° rotation
                    newPos = discardArea.position + new Vector3(-col * 0.12f, 0.01f, row * 0.16f);
                    tileRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                    
                case 3: // Seat 3 (East): 90° CW rotation
                    newPos = discardArea.position + new Vector3(-row * 0.16f, 0.01f, -col * 0.12f);
                    tileRotation = Quaternion.Euler(0f, 90f, 0f);
                    break;
                    
                default:
                    newPos = discardArea.position;
                    tileRotation = Quaternion.identity;
                    break;
            }
            
            tile.transform.position = newPos;
            tile.transform.rotation = tileRotation;
        }
        
        Debug.Log($"[GameManager] Repositioned {tilesAtSeat.Count} discard tiles at seat {seatPosition}");
    }

    // ===== PUBLIC ACCESSORS =====
    
    /// <summary>
    /// Get all spawned discard tiles for highlighting purposes.
    /// Returns dictionary keyed by seat position.
    /// </summary>
    public Dictionary<int, List<DiscardTileEntry>> GetSpawnedDiscardTiles()
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
        Debug.Log($"[Game] Current seat assignments: [{string.Join(", ", playerSeats)}]");
        Debug.Log($"[Game] Round wind: {roundWind}, Consecutive East wins: {consecutiveEastWins}");
        
        // CRITICAL: Save current state to static variables BEFORE scene reload destroys this object
        System.Array.Copy(playerSeats, persistentPlayerSeats, 4);
        System.Array.Copy(initialPlayerSeats, persistentInitialSeats, 4);
        persistentRoundWind = roundWind;
        persistentConsecutiveEastWins = consecutiveEastWins;
        hasPersistedData = true;
        
        Debug.Log($"[Game] *** SAVED TO STATIC PERSISTENCE ***");
        Debug.Log($"[Game] Persisted seats: [{string.Join(", ", persistentPlayerSeats)}]");
        Debug.Log($"[Game] Persisted initial seats: [{string.Join(", ", persistentInitialSeats)}]");
        
        // Notify all clients to hide result screen
        RpcHideResultScreen();
        
        // Reload the Game scene (this clears ALL tiles automatically)
        Debug.Log("[Game] Reloading Game scene to clear all assets...");
        NetworkManager.singleton.ServerChangeScene("Game");
        
        // Note: Game will reinitialize in OnSceneLoaded callback
        Debug.Log("[Game] ========== SCENE RELOAD INITIATED ==========");
    }
    
    /// <summary>
    /// Handle win and determine if seats should rotate.
    /// </summary>
    [Server]
    private void HandleWin(int winnerSeatIndex)
    {
        Debug.Log($"========================================");
        Debug.Log($"[Game] HandleWin called");
        Debug.Log($"[Game] Winner seat: {winnerSeatIndex}");
        Debug.Log($"[Game] Current playerSeats array: [{string.Join(", ", playerSeats)}]");
        
        // CRITICAL: Find which PLAYER won (not just seat)
        // We need to know if the East player won
        int winnerPlayerIndex = -1;
        for (int i = 0; i < players.Count; i++)
        {
            Debug.Log($"[Game] Checking: Player {i} ({players[i].Username}) is at seat {playerSeats[i]}");
            if (playerSeats[i] == winnerSeatIndex)
            {
                winnerPlayerIndex = i;
                break;
            }
        }
        
        if (winnerPlayerIndex < 0)
        {
            Debug.LogError($"[Game] ERROR: Could not find player at winning seat {winnerSeatIndex}!");
            return;
        }
        
        Debug.Log($"[Game] Winner: Player {winnerPlayerIndex} ({players[winnerPlayerIndex].Username}) at Seat {winnerSeatIndex}");
        
        // Check if East wind player won
        // East is ALWAYS at the LOWEST seat number among active players
        int eastSeat = playerSeats.Take(players.Count).Min();
        bool eastPlayerWon = (winnerSeatIndex == eastSeat);
        
        Debug.Log($"[Game] East seat (lowest): {eastSeat}");
        Debug.Log($"[Game] Did East player win? {eastPlayerWon}");
        
        bool needsRotation = false;
        
        if (eastPlayerWon)
        {
            consecutiveEastWins++;
            Debug.Log($"[Game] East (Seat {eastSeat}) wins - consecutive count: {consecutiveEastWins}/3");
            
            if (consecutiveEastWins >= 3)
            {
                Debug.Log("[Game] East won 3 times in a row - forcing rotation");
                needsRotation = true;
                consecutiveEastWins = 0;
            }
            else
            {
                Debug.Log($"[Game] East retains position ({consecutiveEastWins} consecutive wins so far)");
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
            Debug.Log("[Game] >>> ROTATION TRIGGERED <<<");
            RotatePlayerSeats();
        }
        else
        {
            Debug.Log("[Game] >>> NO ROTATION - East keeps position <<<");
        }
        
        // Debug: Show post-win state
        Debug.Log($"[Game] Post-win state:");
        Debug.Log($"[Game]   - Round wind: {roundWind}");
        Debug.Log($"[Game]   - Consecutive East wins: {consecutiveEastWins}");
        Debug.Log($"[Game]   - Seat assignments: [{string.Join(", ", playerSeats)}]");
        Debug.Log($"========================================");
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
        
        // CRITICAL FIX: Rotate through ALL 4 seats, not just active player count
        // Create new seat assignments (everyone moves up one seat, wrapping at 4)
        int[] newSeats = new int[4];
        for (int i = 0; i < players.Count; i++)
        {
            int currentSeat = playerSeats[i];
            newSeats[i] = (currentSeat + 1) % 4;  // CHANGED: % 4 instead of % players.Count
        }
        
        // Copy back only the active player seats
        for (int i = 0; i < players.Count; i++)
        {
            playerSeats[i] = newSeats[i];
        }
        
        Debug.Log($"[Game] >>> Rotation math complete <<<");
        Debug.Log($"[Game] New playerSeats array: [{string.Join(", ", playerSeats)}]");
        
        // Log new seats
        for (int i = 0; i < players.Count; i++)
        {
            Debug.Log($"[Game] AFTER: Player {i} ({players[i].Username}) -> Seat {playerSeats[i]}");
        }
        
        // Check if we've completed a full rotation back to starting positions
        bool fullRotation = true;
        for (int i = 0; i < players.Count; i++)
        {
            if (playerSeats[i] != initialPlayerSeats[i])
            {
                fullRotation = false;
                break;
            }
        }
        
        Debug.Log($"[Game] Full rotation check: Current={string.Join(",", playerSeats.Take(players.Count))}, Initial={string.Join(",", initialPlayerSeats.Take(players.Count))}, Match={fullRotation}");
        
        if (fullRotation)
        {
            Debug.Log("[Game] FULL ROTATION COMPLETED - advancing round wind");
            AdvanceRoundWind();
        }
        
        // Update all players with their new seats and winds
        for (int i = 0; i < players.Count; i++)
        {
            int newSeat = playerSeats[i];
            
            // CRITICAL: Update CurrentSeatPosition (NOT PlayerIndex - that's permanent!)
            players[i].SetCurrentSeatPosition(newSeat);
            
            // Calculate winds: East=401, South=402, West=403, North=404
            int seatWind = 401 + newSeat;
            
            Debug.Log($"[Game] Updating Player {i}: Seat={newSeat}, SeatWind={seatWind}, RoundWind={roundWind}");
            RpcUpdatePlayerWind(players[i].connectionToClient, seatWind, roundWind);
        }
        
        // Update the round wind UI for all clients (covers both no-change and AdvanceRoundWind cases)
        RpcBroadcastRoundWind(roundWind);
        
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
            foreach (DiscardTileEntry entry in kvp.Value)
            {
                if (entry.tileObject != null)
                {
                    NetworkServer.Destroy(entry.tileObject);
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