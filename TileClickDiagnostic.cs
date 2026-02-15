using UnityEngine;
using Mirror;

/// <summary>
/// Attach this to ANY GameObject to diagnose tile clicking issues
/// </summary>
public class TileClickDiagnostic : MonoBehaviour
{
    void Start()
    {
        Debug.Log("========== TILE CLICK DIAGNOSTIC ==========");
        
        // Check NetworkClient state
        Debug.Log($"NetworkClient.active: {NetworkClient.active}");
        Debug.Log($"NetworkClient.isConnected: {NetworkClient.isConnected}");
        Debug.Log($"NetworkClient.localPlayer: {(NetworkClient.localPlayer != null ? NetworkClient.localPlayer.name : "NULL")}");
        
        if (NetworkClient.localPlayer != null)
        {
            Debug.Log($"  - localPlayer netId: {NetworkClient.localPlayer.netId}");
            Debug.Log($"  - localPlayer isOwned: {NetworkClient.localPlayer.isOwned}");
            
            NetworkPlayer np = NetworkClient.localPlayer.GetComponent<NetworkPlayer>();
            if (np != null)
            {
                Debug.Log($"  - Has NetworkPlayer component");
                Debug.Log($"  - Username: {np.Username}");
                Debug.Log($"  - PlayerIndex: {np.PlayerIndex}");
            }
            else
            {
                Debug.LogError($"  - ❌ Missing NetworkPlayer component!");
            }
            
            NetworkedPlayerHand hand = NetworkClient.localPlayer.GetComponent<NetworkedPlayerHand>();
            if (hand != null)
            {
                Debug.Log($"  - ✓ Has NetworkedPlayerHand component");
            }
            else
            {
                Debug.LogError($"  - ❌ Missing NetworkedPlayerHand component!");
            }
        }
        else
        {
            Debug.LogError("❌ NetworkClient.localPlayer is NULL!");
            
            // Try to find player manually
            NetworkPlayer[] allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            Debug.Log($"Found {allPlayers.Length} NetworkPlayer objects in scene:");
            foreach (var p in allPlayers)
            {
                Debug.Log($"  - {p.gameObject.name} (netId: {p.netId}, isOwned: {p.isOwned})");
            }
        }
        
        // Find all tiles in the scene
        NetworkedClickableTile[] tiles = FindObjectsByType<NetworkedClickableTile>(FindObjectsSortMode.None);
        Debug.Log($"\nFound {tiles.Length} NetworkedClickableTile components in scene");
        
        if (tiles.Length == 0)
        {
            Debug.LogError("❌ NO TILES FOUND! Make sure tiles have NetworkedClickableTile component");
            return;
        }
        
        // Check first tile as sample
        NetworkedClickableTile sampleTile = tiles[0];
        GameObject tileObj = sampleTile.gameObject;
        
        Debug.Log($"\nChecking sample tile: {tileObj.name}");
        
        // Check for collider
        Collider col = tileObj.GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("❌ TILE HAS NO COLLIDER! Add BoxCollider or similar");
        }
        else
        {
            Debug.Log($"✓ Collider found: {col.GetType().Name}");
            Debug.Log($"  - Enabled: {col.enabled}");
            Debug.Log($"  - IsTrigger: {col.isTrigger}");
            
            if (col is BoxCollider box)
            {
                Debug.Log($"  - Size: {box.size}");
                Debug.Log($"  - Center: {box.center}");
            }
        }
        
        Debug.Log("========== END DIAGNOSTIC ==========");
    }
    
    void Update()
    {
        // Log mouse clicks
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            Debug.Log($"[Click] Mouse clicked at screen pos: {Input.mousePosition}");
            
            if (Physics.Raycast(ray, out hit, 1000f))
            {
                Debug.Log($"[Click] Hit object: {hit.collider.gameObject.name} at {hit.point}");
                
                NetworkedClickableTile tile = hit.collider.GetComponent<NetworkedClickableTile>();
                if (tile != null)
                {
                    Debug.Log($"[Click] ✓ HIT A TILE!");
                }
                else
                {
                    Debug.Log($"[Click] Hit object has no NetworkedClickableTile component");
                }
            }
            else
            {
                Debug.Log($"[Click] Raycast hit NOTHING");
            }
        }
    }
}