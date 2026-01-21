using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Main game manager that controls the Mahjong game flow.
/// Runs on the server and syncs state to all clients.
/// </summary>
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private int tilesPerPlayer = 13;
    [SerializeField] private float turnTimeLimit = 60f;

    [Header("Prefabs")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private Transform tableCenter;

    [Header("Player Positions")]
    [SerializeField] private Transform[] playerHandPositions = new Transform[4];
    [SerializeField] private Transform[] discardPilePositions = new Transform[4];

    // Game State (synced to all clients)
    [SyncVar(hook = nameof(OnCurrentPlayerChanged))]
    private int currentPlayerIndex = 0;

    [SyncVar]
    private int wallTilesRemaining = 0;

    [SyncVar]
    private bool gameStarted = false;

    // Server-only data
    private List<int> wallTiles = new List<int>(); // Remaining tiles in the wall
    private Dictionary<int, MahjongPlayerHand> playerHands = new Dictionary<int, MahjongPlayerHand>();
    private List<NetworkPlayer> players = new List<NetworkPlayer>();

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
        Debug.Log("GameManager: Server started. Waiting for players...");
    }

    /// <summary>
    /// Called by NetworkLobbyManager when all players are ready and game starts.
    /// </summary>
    [Server]
    public void StartGame(List<NetworkPlayer> gamePlayers)
    {
        if (gameStarted)
        {
            Debug.LogWarning("Game already started!");
            return;
        }

        players = new List<NetworkPlayer>(gamePlayers);
        
        if (players.Count < 2)
        {
            Debug.LogError("Not enough players to start game!");
            return;
        }

        Debug.Log($"Starting game with {players.Count} players...");

        // Initialize the game
        InitializeWall();
        DealInitialHands();
        
        gameStarted = true;
        currentPlayerIndex = 0; // East player starts

        // Tell all clients the game has started
        RpcGameStarted();
        
        // Start first turn
        StartPlayerTurn(currentPlayerIndex);
    }

    /// <summary>
    /// Initialize the wall with all tiles (shuffled).
    /// </summary>
    [Server]
    private void InitializeWall()
    {
        wallTiles.Clear();

        // Add numbered tiles (Circles, Bamboos, Characters: 1-9, 4 of each)
        int[] suits = { 100, 200, 300 }; // Circles, Bamboos, Characters
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

        // Add honor tiles (Winds: 4 of each, Dragons: 4 of each)
        int[] honors = { 401, 402, 403, 404, 501, 502, 503 };
        foreach (int honor in honors)
        {
            for (int copy = 0; copy < 4; copy++)
            {
                wallTiles.Add(honor);
            }
        }

        // Shuffle the wall
        System.Random rng = new System.Random();
        wallTiles = wallTiles.OrderBy(x => rng.Next()).ToList();

        wallTilesRemaining = wallTiles.Count;
        Debug.Log($"Wall initialized with {wallTilesRemaining} tiles.");
    }

    /// <summary>
    /// Deal 13 tiles to each player.
    /// </summary>
    [Server]
    private void DealInitialHands()
    {
        for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
        {
            NetworkPlayer player = players[playerIndex];
            
            // Get or create player hand component
            MahjongPlayerHand playerHand = player.GetComponent<MahjongPlayerHand>();
            if (playerHand == null)
            {
                playerHand = player.gameObject.AddComponent<MahjongPlayerHand>();
            }

            playerHands[playerIndex] = playerHand;

            // Deal 13 tiles
            List<int> handTiles = new List<int>();
            for (int i = 0; i < tilesPerPlayer; i++)
            {
                if (wallTiles.Count > 0)
                {
                    handTiles.Add(DrawTileFromWall());
                }
            }

            // Send tiles to player
            playerHand.TargetReceiveInitialHand(player.connectionToClient, handTiles);
            
            Debug.Log($"Dealt {handTiles.Count} tiles to {player.Username} (Seat {playerIndex})");
        }
    }

    /// <summary>
    /// Draw a tile from the wall.
    /// </summary>
    [Server]
    private int DrawTileFromWall()
    {
        if (wallTiles.Count == 0)
        {
            Debug.LogError("Wall is empty!");
            return 0;
        }

        int tile = wallTiles[0];
        wallTiles.RemoveAt(0);
        wallTilesRemaining = wallTiles.Count;
        return tile;
    }

    /// <summary>
    /// Start a player's turn by drawing a tile.
    /// </summary>
    [Server]
    private void StartPlayerTurn(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= players.Count)
        {
            Debug.LogError($"Invalid player index: {playerIndex}");
            return;
        }

        NetworkPlayer player = players[playerIndex];
        Debug.Log($"Starting turn for {player.Username} (Seat {playerIndex})");

        // Draw a tile for the current player
        if (wallTiles.Count > 0)
        {
            int drawnTile = DrawTileFromWall();
            
            MahjongPlayerHand playerHand = playerHands[playerIndex];
            playerHand.TargetDrawTile(player.connectionToClient, drawnTile);
            
            Debug.Log($"{player.Username} drew tile {drawnTile}. Wall: {wallTilesRemaining} remaining.");
        }
        else
        {
            Debug.Log("Wall is empty! Game should end in a draw.");
            RpcGameEndedDraw();
        }
    }

    /// <summary>
    /// Called by player when they discard a tile.
    /// </summary>
    [Server]
    public void PlayerDiscardedTile(int playerIndex, int tileValue)
    {
        if (playerIndex != currentPlayerIndex)
        {
            Debug.LogWarning($"Player {playerIndex} tried to discard but it's not their turn!");
            return;
        }

        Debug.Log($"Player {playerIndex} discarded tile {tileValue}");

        // Show the discard to all players
        RpcShowDiscard(playerIndex, tileValue);

        // Move to next player
        AdvanceTurn();
    }

    /// <summary>
    /// Advance to the next player's turn.
    /// </summary>
    [Server]
    private void AdvanceTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
        StartPlayerTurn(currentPlayerIndex);
    }

    /// <summary>
    /// Called when a player declares Mahjong.
    /// </summary>
    [Server]
    public void PlayerDeclaredMahjong(int playerIndex)
    {
        NetworkPlayer winner = players[playerIndex];
        Debug.Log($"{winner.Username} declared Mahjong!");

        RpcGameEndedWin(playerIndex, winner.Username);
    }

    // ===== CLIENT RPCs (Server -> All Clients) =====

    [ClientRpc]
    private void RpcGameStarted()
    {
        Debug.Log("Game started!");
        // Hide lobby UI, show game UI, etc.
    }

    [ClientRpc]
    private void RpcShowDiscard(int playerIndex, int tileValue)
    {
        Debug.Log($"Player {playerIndex} discarded tile {tileValue}");
        // Spawn visual tile in discard pile
    }

    [ClientRpc]
    private void RpcGameEndedWin(int winnerIndex, string winnerName)
    {
        Debug.Log($"Game Over! {winnerName} (Seat {winnerIndex}) wins!");
        // Show victory screen
    }

    [ClientRpc]
    private void RpcGameEndedDraw()
    {
        Debug.Log("Game Over! Wall is empty - Draw game.");
        // Show draw screen
    }

    // ===== SYNCVAR HOOKS =====

    private void OnCurrentPlayerChanged(int oldIndex, int newIndex)
    {
        Debug.Log($"Turn changed from Player {oldIndex} to Player {newIndex}");
        // Update UI to show whose turn it is
    }

    // ===== PUBLIC ACCESSORS =====

    public int CurrentPlayerIndex => currentPlayerIndex;
    public int WallTilesRemaining => wallTilesRemaining;
    public bool IsGameStarted => gameStarted;
}
