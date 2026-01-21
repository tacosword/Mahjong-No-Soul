using UnityEngine;
using Mirror;

/// <summary>
/// Detailed diagnostic for tile positioning issues.
/// Add to any GameObject in Game scene.
/// </summary>
public class TilePositionDiagnostic : MonoBehaviour
{
    void Start()
    {
        Invoke(nameof(CheckTilePositions), 6f);
    }

    [ContextMenu("Check Tile Positions")]
    public void CheckTilePositions()
    {
        Debug.Log("=== TILE POSITION DIAGNOSTIC ===");

        // Find the local player
        NetworkPlayer localPlayer = null;
        foreach (NetworkPlayer player in FindObjectsOfType<NetworkPlayer>())
        {
            if (player.isOwned)
            {
                localPlayer = player;
                break;
            }
        }

        if (localPlayer == null)
        {
            Debug.LogError("Could not find local player!");
            return;
        }

        int yourSeat = localPlayer.PlayerIndex;
        Debug.Log($"\n*** YOU ARE SEAT {yourSeat} ***");

        // Find your hand container
        GameObject yourContainer = GameObject.Find($"HandPosition_Seat{yourSeat}");
        if (yourContainer == null)
        {
            Debug.LogError($"Could not find HandPosition_Seat{yourSeat}!");
            return;
        }

        Debug.Log($"\nYour container: {yourContainer.name}");
        Debug.Log($"  World Position: {yourContainer.transform.position}");
        Debug.Log($"  Child count: {yourContainer.transform.childCount}");

        if (yourContainer.transform.childCount == 0)
        {
            Debug.LogError("  ❌ Your container is EMPTY! Tiles were not spawned for you.");
            Debug.LogError("     Check which container actually has 13 children:");
            
            for (int i = 0; i < 4; i++)
            {
                GameObject container = GameObject.Find($"HandPosition_Seat{i}");
                if (container != null)
                {
                    int childCount = container.transform.childCount;
                    if (childCount > 0)
                    {
                        Debug.LogWarning($"     Seat{i} has {childCount} tiles (these were meant for you!)");
                    }
                }
            }
            return;
        }

        // Check the positions of tiles in your container
        Debug.Log($"\n--- TILES IN YOUR CONTAINER ---");
        for (int i = 0; i < Mathf.Min(5, yourContainer.transform.childCount); i++)
        {
            Transform tile = yourContainer.transform.GetChild(i);
            Debug.Log($"Tile {i}: {tile.name}");
            Debug.Log($"  World Position: {tile.position}");
            Debug.Log($"  Local Position: {tile.localPosition}");
            Debug.Log($"  Local Rotation: {tile.localEulerAngles}");
        }

        // Calculate what positions SHOULD be
        Debug.Log($"\n--- EXPECTED POSITIONS ---");
        int tileCount = yourContainer.transform.childCount;
        float spacing = 1.2f;
        Vector3 handStartPos = new Vector3(0f, 0f, -3f);
        
        float handWidth = (tileCount - 1) * spacing;
        float centerOffset = -handWidth / 2f;
        float startX = handStartPos.x + centerOffset;

        Debug.Log($"Expected tile positions (local):");
        for (int i = 0; i < Mathf.Min(5, tileCount); i++)
        {
            float xPos = startX + i * spacing;
            Vector3 expectedPos = new Vector3(xPos, handStartPos.y, handStartPos.z);
            Debug.Log($"  Tile {i}: {expectedPos}");
        }

        // Check if NetworkedPlayerHand component exists
        NetworkedPlayerHand yourHand = localPlayer.GetComponent<NetworkedPlayerHand>();
        if (yourHand == null)
        {
            Debug.LogError("❌ NetworkedPlayerHand component is MISSING from local player!");
        }
        else
        {
            Debug.Log("✓ NetworkedPlayerHand component found");
        }

        Debug.Log("\n=== END DIAGNOSTIC ===");
    }
}
