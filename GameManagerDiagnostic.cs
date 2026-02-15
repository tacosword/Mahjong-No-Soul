using UnityEngine;
using Mirror;

/// <summary>
/// Diagnostic script to verify NetworkedGameManager setup.
/// Attach this to any GameObject in your Game scene and it will check for common issues.
/// </summary>
public class GameManagerDiagnostic : MonoBehaviour
{
    void Start()
    {
        Debug.Log("========== GAME MANAGER DIAGNOSTIC ==========");
        
        // Check for NetworkedGameManager
        NetworkedGameManager gameManager = FindFirstObjectByType<NetworkedGameManager>();
        if (gameManager == null)
        {
            Debug.LogError("❌ NetworkedGameManager NOT FOUND in scene!");
            Debug.LogError("   → Add a GameObject with NetworkedGameManager component to your Game scene");
        }
        else
        {
            Debug.Log("✓ NetworkedGameManager found");
            CheckGameManager(gameManager);
        }
        
        // Check for hand containers
        CheckContainer("HandPosition_Seat0");
        CheckContainer("HandPosition_Seat1");
        CheckContainer("HandPosition_Seat2");
        CheckContainer("HandPosition_Seat3");
        
        // Check for discard containers
        CheckContainer("DiscardPosition_Seat0");
        CheckContainer("DiscardPosition_Seat1");
        CheckContainer("DiscardPosition_Seat2");
        CheckContainer("DiscardPosition_Seat3");
        
        Debug.Log("========== END DIAGNOSTIC ==========");
    }
    
    void CheckContainer(string name)
    {
        GameObject container = GameObject.Find(name);
        if (container == null)
        {
            Debug.LogError($"❌ {name} NOT FOUND in scene!");
            Debug.LogError($"   → Create an empty GameObject named '{name}'");
        }
        else
        {
            Debug.Log($"✓ {name} found at position: {container.transform.position}");
        }
    }
    
    void CheckGameManager(NetworkedGameManager gm)
    {
        // Use reflection to check private fields
        var type = typeof(NetworkedGameManager);
        
        // Check tile prefabs
        var tilePrefabsField = type.GetField("tilePrefabs", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (tilePrefabsField != null)
        {
            GameObject[] tilePrefabs = (GameObject[])tilePrefabsField.GetValue(gm);
            
            if (tilePrefabs == null || tilePrefabs.Length == 0)
            {
                Debug.LogError("❌ Tile Prefabs array is empty!");
                Debug.LogError("   → Assign all 136 tile prefabs in NetworkedGameManager inspector");
            }
            else
            {
                Debug.Log($"✓ {tilePrefabs.Length} tile prefabs assigned");
                
                // Check for required tile values
                CheckTilePrefabs(tilePrefabs);
            }
        }
        
        // Check container assignments
        CheckSerializedContainer(gm, "handPositionSeat0");
        CheckSerializedContainer(gm, "handPositionSeat1");
        CheckSerializedContainer(gm, "handPositionSeat2");
        CheckSerializedContainer(gm, "handPositionSeat3");
        CheckSerializedContainer(gm, "discardPositionSeat0");
        CheckSerializedContainer(gm, "discardPositionSeat1");
        CheckSerializedContainer(gm, "discardPositionSeat2");
        CheckSerializedContainer(gm, "discardPositionSeat3");
    }
    
    void CheckSerializedContainer(NetworkedGameManager gm, string fieldName)
    {
        var field = typeof(NetworkedGameManager).GetField(fieldName, 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            Transform container = (Transform)field.GetValue(gm);
            if (container == null)
            {
                Debug.LogError($"❌ {fieldName} is not assigned in inspector!");
                Debug.LogError($"   → Drag the {fieldName.Replace("handPosition", "HandPosition_").Replace("discardPosition", "DiscardPosition_")} GameObject to this field");
            }
            else
            {
                Debug.Log($"✓ {fieldName} assigned: {container.name}");
            }
        }
    }
    
    void CheckTilePrefabs(GameObject[] prefabs)
    {
        // Check for some critical tile values
        int[] criticalValues = new int[] { 
            101, 102, 103, 104, 105, 106, 107, 108, 109, // Characters 1-9
            201, 202, 203, 204, 205, 206, 207, 208, 209, // Circles 1-9
            301, 302, 303, 304, 305, 306, 307, 308, 309, // Bamboo 1-9
            401, 402, 403, 404, // Winds
            501, 502, 503  // Dragons
        };
        
        int missingCount = 0;
        foreach (int value in criticalValues)
        {
            bool found = false;
            foreach (GameObject prefab in prefabs)
            {
                if (prefab != null)
                {
                    TileData data = prefab.GetComponent<TileData>();
                    if (data != null && data.GetSortValue() == value)
                    {
                        found = true;
                        break;
                    }
                }
            }
            
            if (!found)
            {
                if (missingCount < 5) // Only show first 5 missing
                {
                    Debug.LogWarning($"⚠ Missing tile prefab for value {value}");
                }
                missingCount++;
            }
        }
        
        if (missingCount > 0)
        {
            Debug.LogError($"❌ {missingCount} tile prefabs missing!");
            Debug.LogError("   → Make sure your tile prefabs have TileData components with correct suit/value");
        }
        else
        {
            Debug.Log("✓ All critical tile prefabs found");
        }
    }
}
