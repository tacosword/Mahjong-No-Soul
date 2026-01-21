using UnityEngine;
using Mirror;
using Mirror.Discovery;

/// <summary>
/// Diagnoses LAN server discovery issues.
/// Add to a GameObject in MainMenu scene.
/// </summary>
public class DiscoveryDiagnostic : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private KeyCode testKey = KeyCode.F2;

    void Update()
    {
        if (Input.GetKeyDown(testKey))
        {
            RunDiagnostic();
        }
    }

    [ContextMenu("Run Network Discovery Diagnostic")]
    public void RunDiagnostic()
    {
        Debug.Log("=== NETWORK DISCOVERY DIAGNOSTIC ===");

        // Check if we're host or client
        bool isHost = NetworkServer.active && NetworkClient.active;
        bool isServer = NetworkServer.active && !NetworkClient.active;
        bool isClient = NetworkClient.active && !NetworkServer.active;

        Debug.Log($"\n--- NETWORK STATE ---");
        Debug.Log($"Is Host: {isHost}");
        Debug.Log($"Is Server Only: {isServer}");
        Debug.Log($"Is Client Only: {isClient}");
        Debug.Log($"NetworkServer.active: {NetworkServer.active}");
        Debug.Log($"NetworkClient.active: {NetworkClient.active}");
        Debug.Log($"NetworkClient.isConnected: {NetworkClient.isConnected}");

        // Check Network Manager
        Debug.Log($"\n--- NETWORK MANAGER ---");
        if (NetworkManager.singleton != null)
        {
            Debug.Log($"✓ NetworkManager exists: {NetworkManager.singleton.GetType().Name}");
            Debug.Log($"  Network Address: {NetworkManager.singleton.networkAddress}");
            Debug.Log($"  Max Connections: {NetworkManager.singleton.maxConnections}");
            
            if (NetworkManager.singleton.transport != null)
            {
                Debug.Log($"  Transport: {NetworkManager.singleton.transport.GetType().Name}");
            }
            else
            {
                Debug.LogError($"  ❌ Transport is NULL!");
            }
        }
        else
        {
            Debug.LogError("❌ NetworkManager.singleton is NULL!");
        }

        // Check ServerBrowser
        Debug.Log($"\n--- SERVER BROWSER ---");
        if (ServerBrowser.Instance != null)
        {
            Debug.Log($"✓ ServerBrowser exists");

            // Check if advertising (for host)
            if (NetworkServer.active)
            {
                Debug.Log($"  We are hosting - checking advertising status...");
                
                // Try to check if AdvertiseServer was called
                if (ServerBrowser.Instance.transport != null)
                {
                    Debug.Log($"  ServerBrowser transport: {ServerBrowser.Instance.transport.GetType().Name}");
                    Debug.Log($"  ServerBrowser ServerId: {ServerBrowser.Instance.ServerId}");
                }
                else
                {
                    Debug.LogError($"  ❌ ServerBrowser transport is NULL!");
                }
            }
            // Check if discovering (for client)
            else if (isClient || (!isHost && !isServer))
            {
                Debug.Log($"  We are client/searching - checking discovery...");
                
                var discovered = ServerBrowser.Instance.GetDiscoveredServers();
                Debug.Log($"  Discovered servers: {discovered.Count}");
                
                if (discovered.Count > 0)
                {
                    foreach (var server in discovered)
                    {
                        Debug.Log($"    - {server.hostUsername} ({server.currentPlayers}/{server.maxPlayers}) at {server.uri}");
                    }
                }
                else
                {
                    Debug.LogWarning($"  ⚠ No servers discovered yet. Make sure:");
                    Debug.LogWarning($"    1. Host has called StartHostGame()");
                    Debug.LogWarning($"    2. Client has called StartServerDiscovery()");
                    Debug.LogWarning($"    3. Both are on the same network");
                    Debug.LogWarning($"    4. Firewall isn't blocking UDP broadcast");
                }
            }
        }
        else
        {
            Debug.LogError("❌ ServerBrowser.Instance is NULL!");
            Debug.LogError("   Make sure ServerBrowser component exists in the scene and uses DontDestroyOnLoad");
        }

        // Check MahjongNetworkManager
        Debug.Log($"\n--- MAHJONG NETWORK MANAGER ---");
        if (MahjongNetworkManager.Instance != null)
        {
            Debug.Log($"✓ MahjongNetworkManager exists");
            Debug.Log($"  Player Count: {MahjongNetworkManager.Instance.GetPlayerCount()}");
        }
        else
        {
            Debug.LogError("❌ MahjongNetworkManager.Instance is NULL!");
        }

        // Platform-specific checks
        Debug.Log($"\n--- PLATFORM INFO ---");
        Debug.Log($"Platform: {Application.platform}");
        Debug.Log($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

        // Common issues checklist
        Debug.Log($"\n--- COMMON ISSUES CHECKLIST ---");
        
        bool issue1 = ServerBrowser.Instance == null;
        bool issue2 = NetworkManager.singleton == null;
        bool issue3 = NetworkManager.singleton != null && NetworkManager.singleton.transport == null;
        
        if (NetworkServer.active)
        {
            Debug.Log("HOST CHECKLIST:");
            Debug.Log($"  [ {(ServerBrowser.Instance != null ? "✓" : "❌")} ] ServerBrowser instance exists");
            Debug.Log($"  [ {(NetworkServer.active ? "✓" : "❌")} ] NetworkServer is active");
            Debug.Log($"  [ {(!issue3 ? "✓" : "❌")} ] Transport is assigned");
            
            if (ServerBrowser.Instance != null && ServerBrowser.Instance.transport != null)
            {
                Debug.Log($"  [ ✓ ] ServerBrowser can advertise");
            }
            else
            {
                Debug.LogError($"  [ ❌ ] ServerBrowser CANNOT advertise (transport issue)");
            }
        }
        else
        {
            Debug.Log("CLIENT CHECKLIST:");
            Debug.Log($"  [ {(ServerBrowser.Instance != null ? "✓" : "❌")} ] ServerBrowser instance exists");
            Debug.Log($"  [ {(!issue3 ? "✓" : "❌")} ] Transport is assigned");
            
            if (ServerBrowser.Instance != null)
            {
                var servers = ServerBrowser.Instance.GetDiscoveredServers();
                Debug.Log($"  [ {(servers.Count > 0 ? "✓" : "❌")} ] Servers discovered: {servers.Count}");
            }
        }

        Debug.Log("\n=== END DIAGNOSTIC ===");
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        
        int y = Screen.height - 80;
        
        GUI.Label(new Rect(10, y, 500, 25), $"Press {testKey} for Network Discovery Diagnostic", style);
        
        if (ServerBrowser.Instance != null && !NetworkServer.active)
        {
            var servers = ServerBrowser.Instance.GetDiscoveredServers();
            GUI.Label(new Rect(10, y + 25, 500, 25), $"Discovered Servers: {servers.Count}", style);
        }
    }
}
