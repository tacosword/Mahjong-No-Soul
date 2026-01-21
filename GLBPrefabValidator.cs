using UnityEngine;

/// <summary>
/// Validates that your GLB tile prefabs are set up correctly.
/// Add to any GameObject and run to check prefab structure.
/// </summary>
public class GLBPrefabValidator : MonoBehaviour
{
    [Header("Test Settings")]
    public GameObject testPrefab; // Drag one tile prefab here to test it

    [ContextMenu("Test Spawn Prefab")]
    public void TestSpawnPrefab()
    {
        if (testPrefab == null)
        {
            Debug.LogError("❌ No test prefab assigned! Drag a tile prefab into the 'Test Prefab' field.");
            return;
        }

        Debug.Log($"=== TESTING PREFAB: {testPrefab.name} ===");

        // Check prefab structure
        Debug.Log("\n--- PREFAB STRUCTURE ---");
        Debug.Log($"Prefab name: {testPrefab.name}");
        Debug.Log($"Child count: {testPrefab.transform.childCount}");

        // Check for TileData
        TileData tileData = testPrefab.GetComponent<TileData>();
        if (tileData == null)
        {
            Debug.LogError("❌ No TileData component on prefab root!");
        }
        else
        {
            Debug.Log($"✓ TileData: suit={tileData.suit}, value={tileData.value}, sortValue={tileData.GetSortValue()}");
        }

        // Check for mesh/renderer
        MeshFilter mf = testPrefab.GetComponentInChildren<MeshFilter>();
        MeshRenderer mr = testPrefab.GetComponentInChildren<MeshRenderer>();

        Debug.Log($"MeshFilter: {(mf != null ? "✓ Found" : "❌ NOT FOUND")}");
        Debug.Log($"MeshRenderer: {(mr != null ? "✓ Found" : "❌ NOT FOUND")}");

        if (mf != null)
        {
            Debug.Log($"  Mesh: {(mf.sharedMesh != null ? mf.sharedMesh.name : "NULL")}");
            if (mf.sharedMesh != null)
            {
                Debug.Log($"  Vertices: {mf.sharedMesh.vertexCount}");
            }
        }

        if (mr != null)
        {
            Debug.Log($"  Material: {(mr.sharedMaterial != null ? mr.sharedMaterial.name : "NULL")}");
        }

        // List all components
        Debug.Log("\n--- ALL COMPONENTS ON ROOT ---");
        Component[] components = testPrefab.GetComponents<Component>();
        foreach (var comp in components)
        {
            Debug.Log($"  - {comp.GetType().Name}");
        }

        // List children
        if (testPrefab.transform.childCount > 0)
        {
            Debug.Log("\n--- CHILDREN ---");
            for (int i = 0; i < testPrefab.transform.childCount; i++)
            {
                Transform child = testPrefab.transform.GetChild(i);
                Debug.Log($"  [{i}] {child.name}");

                MeshFilter childMf = child.GetComponent<MeshFilter>();
                MeshRenderer childMr = child.GetComponent<MeshRenderer>();

                if (childMf != null) Debug.Log($"      ✓ Has MeshFilter");
                if (childMr != null) Debug.Log($"      ✓ Has MeshRenderer");
            }
        }

        // Now try to actually spawn it
        Debug.Log("\n--- SPAWN TEST ---");
        GameObject spawned = Instantiate(testPrefab);
        spawned.name = "TEST_SPAWNED_TILE";
        spawned.transform.position = new Vector3(0, 0, -10);

        Debug.Log($"Spawned: {spawned.name}");
        Debug.Log($"Position: {spawned.transform.position}");
        Debug.Log($"Active: {spawned.activeInHierarchy}");

        MeshFilter spawnedMf = spawned.GetComponentInChildren<MeshFilter>();
        MeshRenderer spawnedMr = spawned.GetComponentInChildren<MeshRenderer>();

        Debug.Log($"Spawned has MeshFilter: {spawnedMf != null}");
        Debug.Log($"Spawned has MeshRenderer: {spawnedMr != null}");

        if (spawnedMr != null)
        {
            Debug.Log($"Renderer enabled: {spawnedMr.enabled}");
            Debug.Log($"Renderer bounds: {spawnedMr.bounds}");
        }

        Debug.Log("\n✓ Test spawn complete. Check Scene view to see if you can see the spawned tile!");
        Debug.Log($"  Look for GameObject named '{spawned.name}' in hierarchy.");
        Debug.Log($"  Select it and press 'F' to focus on it in Scene view.");

        Debug.Log("=== END TEST ===");
    }
}
