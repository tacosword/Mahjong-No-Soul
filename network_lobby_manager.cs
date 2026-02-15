using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

/// <summary>
/// Manages the game lobby, player list, and ready status.
/// Compatible with the new NetworkedGameManager system.
/// </summary>
public class NetworkLobbyManager : NetworkBehaviour
{
    public static NetworkLobbyManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int minPlayersToStart = 1; // Changed to 1 so you can test solo with bots
    [SerializeField] private int maxPlayers = 4;

    // Track all players in the lobby
    private List<NetworkPlayer> lobbyPlayers = new List<NetworkPlayer>();

    // SyncVar for tracking game state
    [SyncVar(hook = nameof(OnGameStateChanged))]
    private bool gameStarted = false;

    void Awake()
    {
        Debug.Log($"[LobbyManager] Awake() called. Current Instance: {Instance}");
        
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[LobbyManager] Duplicate NetworkLobbyManager found! Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log($"[LobbyManager] Instance set and marked DontDestroyOnLoad. GameObject: {gameObject.name}");
    }
    
    void Start()
    {
        Debug.Log($"[LobbyManager] Start() called. Instance is: {Instance != null}");
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
        Debug.Log($"[LobbyManager] Scene loaded: {scene.name}");
        Debug.Log($"[LobbyManager] Current lobbyPlayers count: {lobbyPlayers.Count}");
        
        // Verify instance is still valid after scene load
        if (Instance == null)
        {
            Debug.LogError("[LobbyManager] Instance became NULL after scene load!");
            Instance = this;
        }
        
        // Clear players when returning to main menu
        if (scene.name == "MainMenu")
        {
            Debug.Log("[LobbyManager] Returned to MainMenu. Clearing lobby players.");
            lobbyPlayers.Clear();
            gameStarted = false;
        }
        else if (scene.name == "Lobby")
        {
            Debug.Log("[LobbyManager] Now in Lobby scene.");
            UpdatePlayerList();
        }
        else if (scene.name == "Game")
        {
            Debug.Log("[LobbyManager] ✓ Game scene loaded!");
            OnGameSceneLoaded();
        }
    }

    /// <summary>
    /// Initialize game after scene load by checking for the game manager
    /// </summary>
    public void OnGameSceneLoaded()
    {
        // IMPORTANT: Check if we're on the server
        if (!NetworkServer.active)
        {
            Debug.Log("[LobbyManager] Not the server. Skipping game initialization. (Client-only)");
            return;
        }
        
        Debug.Log("[LobbyManager] We are the server/host. Starting game initialization...");
        Debug.Log($"[LobbyManager] Players available: {lobbyPlayers.Count}");
        
        StartCoroutine(InitializeGameAfterSceneLoad());
    }

    /// <summary>
    /// Register a player when they connect to the lobby.
    /// </summary>
    public void RegisterPlayer(NetworkPlayer player)
    {
        if (player == null)
        {
            Debug.LogWarning("[LobbyManager] Tried to register null player!");
            return;
        }
        
        if (!lobbyPlayers.Contains(player))
        {
            lobbyPlayers.Add(player);
            
            Debug.Log($"[LobbyManager] Player registered: {player.Username} (Total players: {lobbyPlayers.Count})");
            
            // Assign player index (seat position)
            if (NetworkServer.active)
            {
                player.SetPlayerIndex(lobbyPlayers.Count - 1);
                Debug.Log($"[LobbyManager] Assigned seat index {lobbyPlayers.Count - 1} to {player.Username}");
            }

            UpdatePlayerList();
        }
        else
        {
            Debug.LogWarning($"[LobbyManager] Player {player.Username} already registered!");
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
            Debug.Log($"[LobbyManager] Player unregistered: {player.Username}");
        }
    }

    /// <summary>
    /// Update the player list UI.
    /// </summary>
    public void UpdatePlayerList()
    {
        Debug.Log($"[LobbyManager] UpdatePlayerList() called. Total players: {lobbyPlayers.Count}");
        
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.RefreshPlayerList(lobbyPlayers);
        }
        else
        {
            Debug.LogWarning("[LobbyManager] LobbyUI.Instance is NULL! Cannot update player list.");
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
            Debug.Log($"[LobbyManager] Not enough players to start ({lobbyPlayers.Count}/{minPlayersToStart})");
            return;
        }

        // Check if all players are ready
        bool allReady = lobbyPlayers.All(p => p.IsReady);

        if (allReady)
        {
            Debug.Log("[LobbyManager] All players ready! Starting game...");
            StartGame();
        }
        else
        {
            Debug.Log("[LobbyManager] Waiting for all players to ready up...");
        }
    }

    /// <summary>
    /// Server starts the game when all players are ready.
    /// </summary>
    [Server]
    private void StartGame()
    {
        gameStarted = true;
        
        Debug.Log("[LobbyManager] ✓ All players ready! Starting game...");
        
        // Load the game scene for all clients
        NetworkManager.singleton.ServerChangeScene("Game");
    }

    /// <summary>
    /// Wait for Game scene to load, then initialize the NetworkedGameManager
    /// </summary>
    [Server]
    private System.Collections.IEnumerator InitializeGameAfterSceneLoad()
    {
        Debug.Log("[LobbyManager] Waiting for Game scene to fully load...");
        Debug.Log($"[LobbyManager] Players available to start game: {lobbyPlayers.Count}");
        
        // Log player details
        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            if (lobbyPlayers[i] != null)
            {
                Debug.Log($"[LobbyManager] Player {i}: {lobbyPlayers[i].Username}, Seat: {lobbyPlayers[i].PlayerIndex}");
            }
            else
            {
                Debug.LogWarning($"[LobbyManager] Player {i} is NULL!");
            }
        }
        
        // Wait for scene to stabilize
        yield return new WaitForSeconds(1f);
        
        // Try to find NetworkedGameManager
        NetworkedGameManager gameManager = null;
        int attempts = 0;
        int maxAttempts = 10;
        
        while (gameManager == null && attempts < maxAttempts)
        {
            gameManager = NetworkedGameManager.Instance;
            
            // Fallback: try FindFirstObjectByType if Instance is null
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<NetworkedGameManager>();
            }
            
            if (gameManager == null)
            {
                Debug.LogWarning($"[LobbyManager] Attempt {attempts + 1}/{maxAttempts}: NetworkedGameManager not found. Retrying...");
                yield return new WaitForSeconds(0.5f);
            }
            
            attempts++;
        }
        
        if (gameManager != null)
        {
            Debug.Log("[LobbyManager] ✓ Found NetworkedGameManager!");
            
            // Double-check we still have players
            if (lobbyPlayers.Count > 0)
            {
                Debug.Log($"[LobbyManager] ✓ Calling InitializeGame() with {lobbyPlayers.Count} players");
                
                // FIXED: Call InitializeGame instead of StartGame
                gameManager.InitializeGame(GetPlayers());
                
                Debug.Log("[LobbyManager] ✓ Game initialization complete!");
            }
            else
            {
                Debug.LogError("[LobbyManager] ✗ CRITICAL: No players in lobby! Cannot start game.");
            }
        }
        else
        {
            Debug.LogError("[LobbyManager] ✗ CRITICAL: NetworkedGameManager not found in Game scene!");
            Debug.LogError("[LobbyManager] Make sure you have a GameObject with NetworkedGameManager component in the Game scene!");
        }
    }
    
    private void OnGameStateChanged(bool oldState, bool newState)
    {
        if (newState)
        {
            Debug.Log("[LobbyManager] Game is starting!");
            
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
        Debug.Log("[LobbyManager] Game scene reloaded for new round");
        
        if (!NetworkServer.active)
        {
            Debug.Log("[LobbyManager] Not server - skipping reload initialization");
            return;
        }
        
        // The game manager will handle the reload automatically
        OnGameSceneLoaded();
    }
    
    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}