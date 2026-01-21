using UnityEngine;
using Mirror;

/// <summary>
/// Helper script to debug network state in the UI.
/// Add this to your MainMenu scene to see connection status.
/// </summary>
public class NetworkDebugHelper : MonoBehaviour
{
    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;
        
        int y = 10;
        int lineHeight = 25;
        
        GUI.Label(new Rect(10, y, 400, 20), $"Network Server Active: {NetworkServer.active}", style);
        y += lineHeight;
        
        GUI.Label(new Rect(10, y, 400, 20), $"Network Client Connected: {NetworkClient.isConnected}", style);
        y += lineHeight;
        
        GUI.Label(new Rect(10, y, 400, 20), $"Network Client Active: {NetworkClient.active}", style);
        y += lineHeight;
        
        if (NetworkManager.singleton != null)
        {
            GUI.Label(new Rect(10, y, 400, 20), $"Network Manager Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}", style);
            y += lineHeight;
            
            GUI.Label(new Rect(10, y, 400, 20), $"Network Address: {NetworkManager.singleton.networkAddress}", style);
            y += lineHeight;
            
            GUI.Label(new Rect(10, y, 400, 20), $"Transport: {NetworkManager.singleton.transport.GetType().Name}", style);
            y += lineHeight;
        }
        
        if (ServerBrowser.Instance != null)
        {
            var servers = ServerBrowser.Instance.GetDiscoveredServers();
            GUI.Label(new Rect(10, y, 400, 20), $"Discovered Servers: {servers.Count}", style);
            y += lineHeight;
            
            foreach (var server in servers)
            {
                GUI.Label(new Rect(10, y, 600, 20), $"  - {server.hostUsername} ({server.currentPlayers}/{server.maxPlayers})", style);
                y += lineHeight;
            }
        }
    }
}