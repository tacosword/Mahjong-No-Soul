using UnityEngine;
using Mirror;

/// <summary>
/// Add this to NetworkedGameManager to debug discard issues.
/// </summary>
public class DiscardDebugger : MonoBehaviour
{
    [ContextMenu("Check Discard Setup")]
    public void CheckDiscardSetup()
    {
        Debug.Log("=== DISCARD SYSTEM DEBUG ===\n");

        NetworkedGameManager manager = GetComponent<NetworkedGameManager>();
        if (manager == null)
        {
            Debug.LogError("No NetworkedGameManager found!");
            return;
        }

        Debug.Log($"Network Status:");
        Debug.Log($"  Server Active: {NetworkServer.active}");
        Debug.Log($"  Client Active: {NetworkClient.active}");
        Debug.Log($"  Current Player Index: {manager.CurrentPlayerIndex}");
        Debug.Log("");

        // Check discard positions
        Transform[] discardPositions = manager.PlayerDiscardPositions;
        
        if (discardPositions == null || discardPositions.Length == 0)
        {
            Debug.LogError("PlayerDiscardPositions array is NULL or empty!");
            return;
        }

        Debug.Log($"Discard Positions Array Length: {discardPositions.Length}");
        
        for (int i = 0; i < discardPositions.Length; i++)
        {
            if (discardPositions[i] == null)
            {
                Debug.LogError($"  Seat {i}: NULL - NOT ASSIGNED!");
            }
            else
            {
                Debug.Log($"  Seat {i}: {discardPositions[i].name}");
                Debug.Log($"    Position: {discardPositions[i].position}");
                Debug.Log($"    Active: {discardPositions[i].gameObject.activeInHierarchy}");
            }
        }

        Debug.Log("\n=== TEST DISCARD ===");
        Debug.Log("If you're testing, discard should:");
        Debug.Log("1. Remove tile from your hand");
        Debug.Log("2. Call CmdDiscardTile (client -> server)");
        Debug.Log("3. Server calls RpcSpawnDiscardTile (server -> all clients)");
        Debug.Log("4. Tile appears in discard area");
        Debug.Log("5. Turn advances to next player");
        
        Debug.Log("\nCheck console for these messages when you discard:");
        Debug.Log("  'Player X discarded Y' (from server)");
        Debug.Log("  '[RpcSpawnDiscardTile] Player X...' (from client)");
        Debug.Log("  'Spawning discard tile at...' (from client)");
        Debug.Log("  'Turn: Player X' (from server)");
    }

    [ContextMenu("Force Test Discard")]
    public void ForceTestDiscard()
    {
        if (!NetworkServer.active)
        {
            Debug.LogError("Must be server/host to test!");
            return;
        }

        NetworkedGameManager manager = GetComponent<NetworkedGameManager>();
        if (manager == null) return;

        Debug.Log("=== FORCING TEST DISCARD ===");
        Debug.Log("Simulating Player 0 discarding tile 101...");
        
        // Simulate a discard
        manager.PlayerDiscardedTile(0, 101, Vector3.zero);
        
        Debug.Log("Check if:");
        Debug.Log("1. RpcSpawnDiscardTile was called");
        Debug.Log("2. Tile appeared in Seat0's discard area");
        Debug.Log("3. Turn changed to Player 1");
    }
}
