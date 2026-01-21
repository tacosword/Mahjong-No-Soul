using UnityEngine;

public class FindMyTiles : MonoBehaviour
{
    [ContextMenu("Find All Tiles")]
    public void FindTiles()
    {
        Debug.Log("=== FINDING ALL TILES IN SCENE ===\n");

        // Find ALL TileData components in scene
        TileData[] allTiles = FindObjectsOfType<TileData>();
        
        Debug.Log($"Total TileData components found: {allTiles.Length}\n");

        if (allTiles.Length == 0)
        {
            Debug.LogError("NO TILES FOUND IN SCENE!");
            Debug.LogError("They either:");
            Debug.LogError("1. Were never spawned");
            Debug.LogError("2. Were destroyed");
            Debug.LogError("3. Are inactive (disabled)");
            return;
        }

        // Group by parent
        System.Collections.Generic.Dictionary<string, int> parentCounts = new System.Collections.Generic.Dictionary<string, int>();
        
        foreach (TileData tile in allTiles)
        {
            string parentName = tile.transform.parent != null ? tile.transform.parent.name : "NO PARENT";
            
            if (!parentCounts.ContainsKey(parentName))
                parentCounts[parentName] = 0;
            
            parentCounts[parentName]++;
        }

        Debug.Log("Tiles grouped by parent:");
        foreach (var kvp in parentCounts)
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value} tiles");
        }
        
        Debug.Log("\n--- FIRST 10 TILES DETAILS ---");
        for (int i = 0; i < Mathf.Min(10, allTiles.Length); i++)
        {
            TileData tile = allTiles[i];
            string parentName = tile.transform.parent != null ? tile.transform.parent.name : "NONE";
            
            Debug.Log($"Tile {i}: {tile.name}");
            Debug.Log($"  Parent: {parentName}");
            Debug.Log($"  Position: {tile.transform.position}");
            Debug.Log($"  Active: {tile.gameObject.activeInHierarchy}");
            Debug.Log($"  Has Renderer: {tile.GetComponentInChildren<MeshRenderer>() != null}");
            Debug.Log("");
        }

        // Check specific containers
        Debug.Log("--- CHECKING HAND CONTAINERS ---");
        for (int i = 0; i < 4; i++)
        {
            GameObject container = GameObject.Find($"HandPosition_Seat{i}");
            if (container != null)
            {
                int childCount = container.transform.childCount;
                Debug.Log($"HandPosition_Seat{i}: {childCount} children");
                
                if (childCount > 0)
                {
                    Debug.Log($"  First child: {container.transform.GetChild(0).name}");
                }
            }
            else
            {
                Debug.LogWarning($"HandPosition_Seat{i}: NOT FOUND!");
            }
        }

        Debug.Log("\n=== END SEARCH ===");
    }
}
