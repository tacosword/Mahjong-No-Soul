using UnityEngine;

/// <summary>
/// Emergency tile alignment - bypasses all the NetworkedPlayerHand logic
/// and just forces tiles into a row.
/// </summary>
public class ForceAlignTilesNow : MonoBehaviour
{
    [ContextMenu("FORCE ALIGN TILES NOW")]
    public void ForceAlign()
    {
        Debug.Log("=== FORCING TILE ALIGNMENT ===");

        GameObject container = GameObject.Find("HandPosition_Seat0");
        if (container == null)
        {
            Debug.LogError("Container not found!");
            return;
        }

        int childCount = container.transform.childCount;
        Debug.Log($"Found {childCount} tiles in container");

        if (childCount == 0)
        {
            Debug.LogError("Container is empty!");
            return;
        }

        // NUCLEAR OPTION: Just arrange them in a simple row
        float spacing = 1.2f;
        float startX = -(childCount - 1) * spacing / 2f; // Center the row

        for (int i = 0; i < childCount; i++)
        {
            Transform tile = container.transform.GetChild(i);
            
            // Set position directly
            Vector3 newPos = new Vector3(startX + i * spacing, 0f, 0f);
            tile.localPosition = newPos;
            tile.localRotation = Quaternion.Euler(-45, 0, 0);
            
            Debug.Log($"Tile {i} ({tile.name}) positioned at local: {newPos}");
        }

        Debug.Log("=== ALIGNMENT COMPLETE ===");
        Debug.Log("Check Scene view now - they should be in a perfect row!");
    }
}
