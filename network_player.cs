using Mirror;
using UnityEngine;
using System.Collections;

/// <summary>
/// Represents a networked player in the Mahjong game.
/// Syncs player data across all clients.
/// </summary>
public class NetworkPlayer : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnUsernameChanged))]
    private string username = "";

    [SyncVar]
    private int playerIndex = -1;

    [SyncVar]
    private bool isReady = false;
    
    // Current seat position (changes with rotation)
    [SyncVar(hook = nameof(OnSeatPositionChanged))]
    private int currentSeatPosition = -1;

    // Public accessors
    public string Username => username;
    public int PlayerIndex => playerIndex;
    public bool IsReady => isReady;
    public int CurrentSeatPosition => currentSeatPosition;
    
    // In network_player.cs - Awake() method
    void Awake()
    {
        Debug.Log($"NetworkPlayer Awake() called on {gameObject.name}");
        
        // REMOVED: DontDestroyOnLoad(gameObject);
        // NetworkPlayer should NOT persist across scenes
        // Mirror handles this automatically
    }
    
    void Start()
    {
        Debug.Log($"NetworkPlayer Start() called on {gameObject.name}");
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        
        Debug.Log($"NetworkPlayer OnStartAuthority() called for {gameObject.name}");
        
        // Send local player's username to server
        if (PlayerProfile.Instance != null && PlayerProfile.Instance.HasValidUsername())
        {
            Debug.Log($"Sending username to server: {PlayerProfile.Instance.Username}");
            CmdSetUsername(PlayerProfile.Instance.Username);
        }
        else
        {
            Debug.LogWarning("NetworkPlayer: No valid username found in profile!");
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        
        Debug.Log($"[NetworkPlayer] ✓ OnStartLocalPlayer() called for {gameObject.name}");
        Debug.Log($"[NetworkPlayer] ✓ NetworkClient.localPlayer is now SET!");
        
        // CRITICAL: This callback tells Mirror to set NetworkClient.localPlayer
        // Without this method, NetworkClient.localPlayer stays NULL!
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        Debug.Log($"[NetworkPlayer] OnStartClient - netId={netId}, isOwned={isOwned}, username={username}");
        
        // On server, register immediately
        if (NetworkServer.active && netId != 0)
        {
            if (NetworkLobbyManager.Instance != null)
            {
                Debug.Log($"[Server] Registering player '{username}' with lobby");
                NetworkLobbyManager.Instance.RegisterPlayer(this);
            }
        }
    }
    
    // Remove the old RegisterWithLobby method

    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unregister this player from the lobby
        if (NetworkLobbyManager.Instance != null)
        {
            NetworkLobbyManager.Instance.UnregisterPlayer(this);
        }
    }

    #region Commands (Client -> Server)

    /// <summary>
    /// Client sends their username to the server.
    /// </summary>
    [Command]
    private void CmdSetUsername(string newUsername)
    {
        username = newUsername;
        Debug.Log($"Player {netId} set username to: {username}");
    }

    /// <summary>
    /// Client toggles their ready status.
    /// </summary>
    [Command]
    public void CmdSetReady(bool ready)
    {
        isReady = ready;
        Debug.Log($"Player {username} ready status: {isReady}");
        
        // Check if all players are ready to start game
        if (NetworkServer.active && NetworkLobbyManager.Instance != null)
        {
            NetworkLobbyManager.Instance.CheckAllPlayersReady();
        }
    }

    /// <summary>
    /// Server assigns a player index (seat position) to this player.
    /// </summary>
    [Server]
    public void SetPlayerIndex(int index)
    {
        playerIndex = index;
    }
    
    [Server]
    public void SetCurrentSeatPosition(int seat)
    {
        currentSeatPosition = seat;
        Debug.Log($"[Server] Set {Username}'s seat position to {seat}");
    }

    #endregion

    #region SyncVar Hooks

    /// <summary>
    /// Called when username changes (on all clients).
    /// </summary>
    private void OnUsernameChanged(string oldUsername, string newUsername)
    {
        Debug.Log($"Player username changed: {oldUsername} -> {newUsername}");
        
        // Update UI if needed
        if (NetworkLobbyManager.Instance != null)
        {
            NetworkLobbyManager.Instance.UpdatePlayerList();
        }
    }
    
    private void OnSeatPositionChanged(int oldSeat, int newSeat)
    {
        Debug.Log($"[NetworkPlayer] {Username} seat changed: {oldSeat} â†’ {newSeat}");
        
        // If this is the local player and we're in the Game scene, update camera
        if (isOwned && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Game")
        {
            UpdateCameraForSeat(newSeat);
        }
    }
    
    private void UpdateCameraForSeat(int seat)
    {
        CameraController camController = FindFirstObjectByType<CameraController>();
        if (camController != null)
        {
            camController.SetupCameraForSeat(seat);
        }
    }

    #endregion

    void OnEnable()
{
    UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
}

void OnDisable()
{
    UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
}

private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
{
    if (scene.name == "Game" && isOwned)
    {
        SetupPlayerHandInGame();
    }
}

private void SetupPlayerHandInGame()
{
    // Find hand container for our seat
    GameObject handContainer = GameObject.Find($"HandPosition_Seat{PlayerIndex}");
    
    if (handContainer != null)
    {
        MahjongPlayerHand playerHand = GetComponent<MahjongPlayerHand>();
        if (playerHand == null)
        {
            playerHand = gameObject.AddComponent<MahjongPlayerHand>();
        }
        
        // Assign container to player hand
        playerHand.handContainer = handContainer.transform;
    }
}

    /// <summary>
    /// Client requests server to start a new round (Command).
    /// </summary>
    [Command]
    public void CmdRequestNewRound()
    {
        // Only host can start new rounds
        if (!NetworkServer.active)
        {
            Debug.LogWarning($"[NetworkPlayer] Player {Username} tried to start round but not host");
            return;
        }
        
        Debug.Log($"[NetworkPlayer] {Username} requested new round");
        
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.StartNewRound();
        }
    }
}