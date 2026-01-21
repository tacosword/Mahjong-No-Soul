using Mirror;
using Mirror.Discovery;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net;

/// <summary>
/// Handles LAN server discovery for finding available games.
/// Players can see all hosted games without entering IP addresses.
/// </summary>
public class ServerBrowser : NetworkDiscoveryBase<ServerRequest, ServerResponse>
{
    public static ServerBrowser Instance { get; private set; }

    [Header("Discovery Settings")]
    [Tooltip("How long to wait between discovery broadcasts (in seconds)")]
    [SerializeField] private float discoveryInterval = 2f;

    // Store discovered servers
    private Dictionary<long, ServerResponse> discoveredServers = new Dictionary<long, ServerResponse>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Debug.Log("ServerBrowser instance created and set to DontDestroyOnLoad");
    }

    #region Server Discovery (Broadcasting)

    /// <summary>
    /// Called on the server to process a discovery request from a client.
    /// Responds with server information.
    /// </summary>
    protected override ServerResponse ProcessRequest(ServerRequest request, IPEndPoint endpoint)
    {
        // Only respond if we're actually hosting
        if (!NetworkServer.active)
            return default;

        // Get host player info
        string hostUsername = "Unknown Host";
        if (PlayerProfile.Instance.HasValidUsername())
        {
            hostUsername = PlayerProfile.Instance.Username;
        }

        // Get current player count
        int currentPlayers = 0;
        int maxPlayers = 4;
        
        if (NetworkLobbyManager.Instance != null)
        {
            currentPlayers = NetworkLobbyManager.Instance.GetPlayerCount();
        }

        // Create response with server info
        ServerResponse response = new ServerResponse
        {
            serverId = ServerId,
            hostUsername = hostUsername,
            currentPlayers = currentPlayers,
            maxPlayers = maxPlayers,
            uri = transport.ServerUri()
        };

        Debug.Log($"Responding to discovery request from {endpoint}");
        return response;
    }

    #endregion

    #region Client Discovery (Finding Servers)

    /// <summary>
    /// Called on the client when a server responds to discovery.
    /// </summary>
    protected override void ProcessResponse(ServerResponse response, IPEndPoint endpoint)
    {
        // Don't add our own server if we're the host
        if (response.serverId == ServerId)
            return;

        // Store or update server info
        discoveredServers[response.serverId] = response;

        Debug.Log($"Discovered server: {response.hostUsername} ({response.currentPlayers}/{response.maxPlayers}) at {response.uri}");

        // Update the UI
        if (ServerBrowserUI.Instance != null)
        {
            ServerBrowserUI.Instance.UpdateServerList(GetDiscoveredServers());
        }
    }

    /// <summary>
    /// Start broadcasting discovery requests to find servers.
    /// Called from UI when player wants to find games.
    /// </summary>
    public void StartServerDiscovery()
    {
        discoveredServers.Clear();
        
        if (NetworkServer.active)
        {
            Debug.Log("Cannot search for servers while hosting.");
            return;
        }

        Debug.Log("Starting server discovery...");
        
        // Start the discovery client
        StartDiscovery();
    }

    /// <summary>
    /// Stop broadcasting discovery requests.
    /// </summary>
    public void StopServerDiscovery()
    {
        StopDiscovery();
        Debug.Log("Stopped server discovery.");
    }

    /// <summary>
    /// Get the list of discovered servers.
    /// </summary>
    public List<ServerResponse> GetDiscoveredServers()
    {
        return new List<ServerResponse>(discoveredServers.Values);
    }

    /// <summary>
    /// Clear the list of discovered servers.
    /// </summary>
    public void ClearDiscoveredServers()
    {
        discoveredServers.Clear();
    }

    /// <summary>
    /// Connect to a specific discovered server.
    /// </summary>
    public void ConnectToServer(ServerResponse server)
    {
        StopServerDiscovery();

        if (MahjongNetworkManager.Instance != null)
        {
            MahjongNetworkManager.Instance.StartClient(server.uri);
            Debug.Log($"Connecting to {server.hostUsername}'s game...");
        }
    }

    #endregion

    #region Lifecycle

    public override void Start()
    {
        base.Start();
        
        Debug.Log("ServerBrowser Start() called");
        
        // If we're hosting, start advertising immediately
        if (NetworkServer.active)
        {
            Debug.Log("NetworkServer is active. Starting to advertise server...");
            AdvertiseServer();
        }
        else
        {
            Debug.Log("Not hosting yet. Will advertise when server starts.");
        }
    }
    
    /// <summary>
    /// Called when the server starts. Start advertising.
    /// </summary>
    public void OnServerStarted()
    {
        if (NetworkServer.active && transport != null)
        {
            Debug.Log("Server started! Beginning to advertise on LAN...");
            
            // Small delay to ensure transport is fully ready
            Invoke(nameof(StartAdvertising), 0.5f);
        }
        else
        {
            Debug.LogWarning("Cannot advertise: NetworkServer.active=" + NetworkServer.active + ", transport=" + (transport != null));
        }
    }
    
    private void StartAdvertising()
    {
        try
        {
            AdvertiseServer();
            Debug.Log("Server advertising started successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to start advertising: {e.Message}");
        }
    }

    void OnDestroy()
    {
        StopServerDiscovery();
    }

    #endregion
}

#region Discovery Message Structures

/// <summary>
/// Request sent by clients looking for servers.
/// </summary>
public struct ServerRequest : NetworkMessage
{
    // Empty struct - we don't need to send any data in the request
}

/// <summary>
/// Response sent by servers with their information.
/// </summary>
public struct ServerResponse : NetworkMessage
{
    public long serverId;
    public string hostUsername;
    public int currentPlayers;
    public int maxPlayers;
    public Uri uri;
}

#endregion