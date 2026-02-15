using UnityEngine;
using Mirror;
using System.Collections;

/// <summary>
/// Delayed diagnostic that runs AFTER tiles and players spawn
/// </summary>
public class DelayedDiagnostic : MonoBehaviour
{
    IEnumerator Start()
    {
        // Wait 3 seconds for everything to spawn
        yield return new WaitForSeconds(3f);
        
        Debug.Log("========== DELAYED DIAGNOSTIC (3 seconds after load) ==========");
        
        // Check NetworkClient state
        Debug.Log($"NetworkClient.localPlayer: {(NetworkClient.localPlayer != null ? NetworkClient.localPlayer.name : "NULL")}");
        
        if (NetworkClient.localPlayer != null)
        {
            Debug.Log($"  - localPlayer netId: {NetworkClient.localPlayer.netId}");
            
            NetworkPlayer np = NetworkClient.localPlayer.GetComponent<NetworkPlayer>();
            if (np != null)
            {
                Debug.Log($"  - ✓ Has NetworkPlayer component");
                Debug.Log($"  - Username: {np.Username}");
                Debug.Log($"  - PlayerIndex: {np.PlayerIndex}");
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
            Debug.LogError("❌ NetworkClient.localPlayer is STILL NULL after 3 seconds!");
        }
        
        // Find all players
        NetworkPlayer[] allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        Debug.Log($"\nFound {allPlayers.Length} NetworkPlayer objects:");
        foreach (var p in allPlayers)
        {
            Debug.Log($"  - {p.gameObject.name}: isOwned={p.isOwned}, PlayerIndex={p.PlayerIndex}");
            NetworkedPlayerHand h = p.GetComponent<NetworkedPlayerHand>();
            Debug.Log($"    Has NetworkedPlayerHand: {h != null}");
        }
        
        // Find all tiles
        NetworkedClickableTile[] tiles = FindObjectsByType<NetworkedClickableTile>(FindObjectsSortMode.None);
        Debug.Log($"\nFound {tiles.Length} tiles");
        
        Debug.Log("========== END DELAYED DIAGNOSTIC ==========");
        Debug.Log("\n>>> NOW CLICK A TILE <<<\n");
    }
}
