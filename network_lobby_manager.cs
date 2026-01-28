using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

/// <summary>
/// Manages the game lobby, player list, and ready status.
/// </summary>
public class NetworkLobbyManager : NetworkBehaviour
{
    public static NetworkLobbyManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int minPlayersToStart = 2;
    [SerializeField] private int maxPlayers = 4;

    // Track all players in the lobby
    private List<NetworkPlayer> lobbyPlayers = new List<NetworkPlayer>();

    // SyncVar for tracking game state
    [SyncVar(hook = nameof(OnGameStateChanged))]
    private bool gameStarted = false;

    void Awake()
    {
        Debug.Log($"NetworkLobbyManager Awake() called. Current Instance: {Instance}");
        
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate NetworkLobbyManager found! Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        // IMPORTANT: Only call DontDestroyOnLoad if we're keeping this instance
        DontDestroyOnLoad(gameObject);
        Debug.Log($"NetworkLobbyManager instance set and marked DontDestroyOnLoad. GameObject: {gameObject.name}");
    }
    
    void Start()
    {
        Debug.Log($"NetworkLobbyManager Start() called. Instance is: {Instance != null}");
    }
    
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
{
    Debug.Log($"[Lobby] Scene loaded: {scene.name}");
    Debug.Log($"[Lobby] Current lobbyPlayers count: {lobbyPlayers.Count}");
    
    // Verify instance is still valid after scene load
    if (Instance == null)
    {
        Debug.LogError("[Lobby] Instance became NULL after scene load!");
        Instance = this;
    }
    
    // Clear players when returning to main menu
    if (scene.name == "MainMenu")
    {
        Debug.Log("[Lobby] Returned to MainMenu. Clearing lobby players.");
        lobbyPlayers.Clear();
    }
    else if (scene.name == "Lobby")
    {
        Debug.Log("[Lobby] Now in Lobby scene.");
        UpdatePlayerList();
    }
    else if (scene.name == "Game")
    {
        Debug.Log("[Lobby] âœ“ Game scene loaded!");
        // Just call it - let the method handle the server check
        OnGameSceneLoaded();
    }
}

// Initialize game after scene load by checking for the game manager
public void OnGameSceneLoaded()
{
    // IMPORTANT: Check if we're on the server
    // Only initialize game if we are the server (which includes host)
    if (!NetworkServer.active)
    {
        Debug.Log("[Lobby] Not the server. Skipping game initialization. (Client-only)");
        return;
    }
    
    Debug.Log("[Lobby] We are the server/host. Starting game initialization...");
    Debug.Log($"[Lobby] Players available: {lobbyPlayers.Count}");
    
    StartCoroutine(InitializeGameAfterSceneLoad());
}

    /// <summary>
    /// Register a player when they connect to the lobby.
    /// </summary>
    public void RegisterPlayer(NetworkPlayer player)
    {
        if (player == null)
        {
            Debug.LogWarning("Tried to register null player!");
            return;
        }
        
        if (!lobbyPlayers.Contains(player))
        {
            lobbyPlayers.Add(player);
            
            Debug.Log($"Player registered: {player.Username} (Total players: {lobbyPlayers.Count})");
            
            // Assign player index (seat position) - Use NetworkServer.active instead of isServer
            if (NetworkServer.active)
            {
                player.SetPlayerIndex(lobbyPlayers.Count - 1);
                Debug.Log($"Assigned seat index {lobbyPlayers.Count - 1} to {player.Username}");
            }

            UpdatePlayerList();
        }
        else
        {
            Debug.LogWarning($"Player {player.Username} already registered!");
        }
    }

    /// <summary>
    /// Unregister a player when they disconnect.
    /// </summary>
    public void UnregisterPlayer(NetworkPlayer player)
    {
        if (lobbyPlayers.Contains(player))
        {
            lobbyPlayers.Remove(player);
            UpdatePlayerList();
            Debug.Log($"Player unregistered: {player.Username}");
        }
    }

    /// <summary>
    /// Update the player list UI.
    /// </summary>
    public void UpdatePlayerList()
    {
        Debug.Log($"UpdatePlayerList() called. Total players: {lobbyPlayers.Count}");
        
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.RefreshPlayerList(lobbyPlayers);
        }
        else
        {
            Debug.LogWarning("LobbyUI.Instance is NULL! Cannot update player list.");
        }
    }

    /// <summary>
    /// Called by server to check if all players are ready.
    /// </summary>
    [Server]
    public void CheckAllPlayersReady()
    {
        if (lobbyPlayers.Count < minPlayersToStart)
        {
            Debug.Log($"Not enough players to start ({lobbyPlayers.Count}/{minPlayersToStart})");
            return;
        }

        // Check if all players are ready
        bool allReady = lobbyPlayers.All(p => p.IsReady);

        if (allReady)
        {
            Debug.Log("All players ready! Starting game...");
            StartGame();
        }
        else
        {
            Debug.Log("Waiting for all players to ready up...");
        }
    }

    /// <summary>
    /// Server starts the game when all players are ready.
    /// </summary>
    /// <summary>
    /// Server starts the game when all players are ready.
    /// </summary>
    [Server]
    private void StartGame()
    {
        gameStarted = true;
        
        Debug.Log("All players ready! Starting game...");
        
        // Load the game scene for all clients
        NetworkManager.singleton.ServerChangeScene("Game");
    }

    // Initialize game after scene load by checking for the game manager

    [Server]

private System.Collections.IEnumerator InitializeGameAfterSceneLoad()
{
    Debug.Log("[Lobby] Waiting for Game scene to fully load...");
    Debug.Log($"[Lobby] Players available to start game: {lobbyPlayers.Count}");
    
    // Log player details
    for (int i = 0; i < lobbyPlayers.Count; i++)
    {
        if (lobbyPlayers[i] != null)
        {
            Debug.Log($"[Lobby] Player {i}: {lobbyPlayers[i].Username}, Seat: {lobbyPlayers[i].PlayerIndex}");
        }
        else
        {
            Debug.LogWarning($"[Lobby] Player {i} is NULL!");
        }
    }
    
    // Wait for scene to stabilize
    yield return new WaitForSeconds(2f);
    
    // Try to find NetworkedGameManager
    NetworkedGameManager gameManager = null;
    int attempts = 0;
    int maxAttempts = 5;
    
    while (gameManager == null && attempts < maxAttempts)
    {
        gameManager = FindFirstObjectByType<NetworkedGameManager>();
        
        if (gameManager == null)
        {
            Debug.LogWarning($"[Lobby] Attempt {attempts + 1}/{maxAttempts}: NetworkedGameManager not found. Retrying...");
            yield return new WaitForSeconds(0.5f);
        }
        
        attempts++;
    }
    
    if (gameManager != null)
    {
        Debug.Log("[Lobby] âœ“ Found NetworkedGameManager!");
        
        // Double-check we still have players
        if (lobbyPlayers.Count > 0)
        {
            Debug.Log($"[Lobby] Starting game with {lobbyPlayers.Count} players");
            gameManager.StartGame(GetPlayers());
        }
        else
        {
            Debug.LogError("[Lobby] âœ— CRITICAL: No players in lobby! Cannot start game.");
        }
    }
    else
    {
        Debug.LogError("[Lobby] âœ— CRITICAL: NetworkedGameManager not found in Game scene!");
    }
}
    private void OnGameStateChanged(bool oldState, bool newState)
    {
        if (newState)
        {
            Debug.Log("Game is starting!");
            
            // Hide lobby UI
            if (LobbyUI.Instance != null)
            {
                LobbyUI.Instance.HideLobby();
            }
        }
    }

    /// <summary>
    /// Get a list of all players in the lobby.
    /// </summary>
    public List<NetworkPlayer> GetPlayers()
    {
        return new List<NetworkPlayer>(lobbyPlayers);
    }

    /// <summary>
    /// Get the number of players in the lobby.
    /// </summary>
    public int GetPlayerCount()
    {
        return lobbyPlayers.Count;
    }

    /// <summary>
    /// Check if the lobby is full.
    /// </summary>
    public bool IsLobbyFull()
    {
        return lobbyPlayers.Count >= maxPlayers;
    }

    /// <summary>
    /// Called when Game scene loads during a round reload.
    /// </summary>
    public void OnGameSceneReloaded()
    {
        Debug.Log("[Lobby] Game scene reloaded for new round");
        
        if (!NetworkServer.active)
        {
            Debug.Log("[Lobby] Not server - skipping reload initialization");
            return;
        }
        
        // The game manager will handle the reload automatically
        // We just need to call OnGameSceneLoaded again
        OnGameSceneLoaded();
    }
}