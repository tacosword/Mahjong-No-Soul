using Mirror;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Custom Network Manager for the Mahjong game.
/// Handles connection, player spawning, and lobby management.
/// </summary>
public class MahjongNetworkManager : NetworkManager
{
    [Header("Mahjong Game Settings")]
    [SerializeField] private int maxPlayers = 4;
    
    [Header("Tile Prefabs - CRITICAL")]
    [Tooltip("Assign all 136 tile prefabs here")]
    [SerializeField] private GameObject[] tilePrefabs;
    
    [Header("Scene References")]
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private string gameSceneName = "Game";

    // Track connected players
    private Dictionary<int, NetworkConnectionToClient> connectedPlayers = new Dictionary<int, NetworkConnectionToClient>();

    public static MahjongNetworkManager Instance { get; private set; }

    public override void Awake()
    {
        Debug.Log($"MahjongNetworkManager Awake() called. Current Instance: {Instance}");
        
        // Set instance BEFORE calling base.Awake()
        if (Instance == null)
        {
            Instance = this;
            Debug.Log($"MahjongNetworkManager Instance set to: {gameObject.name}");
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"Duplicate MahjongNetworkManager detected! Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        
        base.Awake();
        
        // CRITICAL: Register tile prefabs for spawning
        RegisterTilePrefabs();
        
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Register all tile prefabs. Runs on both server and client.
    /// </summary>
    private void RegisterTilePrefabs()
    {
        if (tilePrefabs == null || tilePrefabs.Length == 0)
        {
            Debug.LogWarning("[NetworkManager] No tile prefabs assigned!");
            return;
        }

        int count = 0;
        foreach (GameObject prefab in tilePrefabs)
        {
            if (prefab != null && !spawnPrefabs.Contains(prefab))
            {
                spawnPrefabs.Add(prefab);
                count++;
            }
        }
        
        Debug.Log($"[NetworkManager] Registered {count} tile prefabs. Total: {spawnPrefabs.Count}");
    }

    #region Server Callbacks

    /// <summary>
    /// Called on the server when it starts.
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("Server started!");
        
        // Tell ServerBrowser to start advertising
        if (ServerBrowser.Instance != null)
        {
            ServerBrowser.Instance.OnServerStarted();
        }
    }

    /// <summary>
    /// Called on the server when a client connects.
    /// </summary>
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        
        // Check if lobby is full
        if (numPlayers >= maxPlayers)
        {
            conn.Disconnect();
            Debug.Log($"Connection rejected: Lobby full ({numPlayers}/{maxPlayers})");
            return;
        }

        Debug.Log($"Player connected: {conn.connectionId}");
    }

    /// <summary>
    /// Called on the server when a player is added (after authentication).
    /// </summary>
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
{
    // CRITICAL: Check if player already exists for this connection
    if (conn.identity != null)
    {
        Debug.LogWarning($"Connection {conn.connectionId} already has a player (netId: {conn.identity.netId}). Skipping duplicate spawn.");
        return;
    }
    
    Debug.Log($"Spawning player for connection {conn.connectionId}...");
    
    base.OnServerAddPlayer(conn);
    
    if (conn.identity != null)
    {
        GameObject playerObj = conn.identity.gameObject;
        NetworkPlayer netPlayer = playerObj.GetComponent<NetworkPlayer>();
        
        if (netPlayer != null)
        {
            connectedPlayers[conn.connectionId] = conn;
            Debug.Log($"Player spawned successfully for connection: {conn.connectionId}");
            
            // Force registration with lobby after a small delay
            StartCoroutine(RegisterPlayerAfterDelay(netPlayer, 1f));
        }
        else
        {
            Debug.LogError("Spawned player has no NetworkPlayer component!");
        }
    }
}

    /// <summary>
    /// Called on the server when a client disconnects.
    /// </summary>
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (connectedPlayers.ContainsKey(conn.connectionId))
        {
            connectedPlayers.Remove(conn.connectionId);
            Debug.Log($"Player removed from game: {conn.connectionId}");
        }

        base.OnServerDisconnect(conn);
    }

    #endregion

    #region Client Callbacks

    /// <summary>
    /// Called on the client when successfully connected to server.
    /// </summary>
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("Successfully connected to server!");
        
        // If we're not the host, we're a client joining
        if (!NetworkServer.active)
        {
            Debug.Log("Client connected. Waiting for server to load lobby scene...");
        }
    }

    /// <summary>
    /// Called on the client when disconnected from server.
    /// </summary>
    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("Disconnected from server.");
        
        // Return to main menu
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.ShowMainMenu();
        }
    }
    
    /// <summary>
    /// Called on clients when a scene change happens.
    /// </summary>
    public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
    {
        base.OnClientChangeScene(newSceneName, sceneOperation, customHandling);
        Debug.Log($"Client changing to scene: {newSceneName}");
    }
    
    /// <summary>
    /// Called after the client scene finishes loading.
    /// </summary>
    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();
        Debug.Log($"Client scene changed to: {SceneManager.GetActiveScene().name}");
        
        // If we're in the lobby scene, make sure UI is set up
        if (SceneManager.GetActiveScene().name == lobbySceneName)
        {
            Debug.Log("Now in Lobby scene. Setting up lobby UI...");
        }
    }

    #endregion

    #region Custom Methods

    /// <summary>
    /// Start hosting a game as both server and client.
    /// </summary>
    public void StartHostGame()
    {
        Debug.Log("StartHostGame() called");
        
        // CRITICAL FIX: Stop any existing server/client first
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("Server or Client already active! Stopping before restarting...");
            
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                StopHost();
            }
            else if (NetworkClient.active)
            {
                StopClient();
            }
            else if (NetworkServer.active)
            {
                StopServer();
            }
            
            // Wait a frame for cleanup
            StartCoroutine(StartHostAfterCleanup());
            return;
        }
        
        networkAddress = "localhost";
        
        // Start as host (server + client)
        StartHost();
        
        // Check if host started successfully
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            Debug.Log("Host started successfully.");
            
            // Only change scene if we're not already in the lobby scene
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene != lobbySceneName)
            {
                Debug.Log($"Changing from {currentScene} to {lobbySceneName}...");
                ServerChangeScene(lobbySceneName);
            }
            else
            {
                Debug.Log("Already in lobby scene, skipping scene change.");
            }
        }
        else
        {
            Debug.LogError("Failed to start host!");
        }
    }
    
    private System.Collections.IEnumerator StartHostAfterCleanup()
    {
        // Wait for network cleanup
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("Cleanup complete. Starting host now...");
        
        networkAddress = "localhost";
        StartHost();
        
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            Debug.Log("Host started successfully after cleanup.");
            
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene != lobbySceneName)
            {
                ServerChangeScene(lobbySceneName);
            }
        }
        else
        {
            Debug.LogError("Failed to start host even after cleanup!");
        }
    }
    /// <summary>
    /// Join a game as a client.
    /// </summary>
    public void JoinGame(string address)
    {
        Debug.Log($"JoinGame() called with address: {address}");
        networkAddress = address;
        
        StartClient();
        
        Debug.Log($"Client start requested. Connecting to: {address}");
    }

    /// <summary>
    /// Stop hosting or disconnect from game.
    /// </summary>
    public void LeaveGame()
    {
        Debug.Log("LeaveGame() called");
        
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            Debug.Log("Stopping host...");
            StopHost();
        }
        else if (NetworkClient.isConnected)
        {
            Debug.Log("Stopping client...");
            StopClient();
        }
        else if (NetworkServer.active)
        {
            Debug.Log("Stopping server...");
            StopServer();
        }

        connectedPlayers.Clear();
        
        // Small delay before scene change to ensure cleanup
        StartCoroutine(ReturnToMenuAfterCleanup());
    }
    
    private System.Collections.IEnumerator ReturnToMenuAfterCleanup()
    {
        yield return new WaitForSeconds(0.3f);
        
        // Load the main menu scene
        SceneManager.LoadScene("MainMenu");
        
        Debug.Log("Left the game. Returning to main menu.");
    }

    /// <summary>
    /// Get the number of currently connected players.
    /// </summary>
    public int GetPlayerCount()
    {
        return connectedPlayers.Count;
    }

    /// <summary>
    /// Check if the lobby/game is full.
    /// </summary>
    public bool IsLobbyFull()
    {
        return numPlayers >= maxPlayers;
    }

    #endregion

    #region Helper Methods
    
    /// <summary>
    /// Force register the player with the lobby after a delay to ensure everything is set up.
    /// </summary>
    private System.Collections.IEnumerator RegisterPlayerAfterDelay(NetworkPlayer player, float delay)
{
    yield return new WaitForSeconds(delay);
    
    if (NetworkLobbyManager.Instance == null)
    {
        Debug.LogError("[Force Register] NetworkLobbyManager.Instance is NULL! Make sure it exists in the Game scene!");
        yield break;
    }
}
    
    #endregion
}