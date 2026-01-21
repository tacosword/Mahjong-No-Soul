using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Enhanced debugging to trace exactly what happens during tile spawning.
/// Add to NetworkedGameManager GameObject.
/// </summary>
public class TileSpawnTracer : MonoBehaviour
{
    void Start()
    {
        Invoke(nameof(TraceTileSpawning), 4f);
    }

    [ContextMenu("Trace Tile Spawning")]
    public void TraceTileSpawning()
    {
        Debug.Log("=== TILE SPAWN TRACE ===");

        // Find all spawned tiles in the scene
        TileData[] allTileData = FindObjectsOfType<TileData>();
        
        Debug.Log($"\n--- SPAWNED TILES IN SCENE ---");
        Debug.Log($"Total TileData components found: {allTileData.Length}");

        if (allTileData.Length == 0)
        {
            Debug.LogError("❌ NO TILES SPAWNED! The spawning code isn't working.");
            Debug.LogError("   Check if TargetReceiveInitialHand is being called.");
            return;
        }

        // Group by parent to see where they are
        var grouped = allTileData.GroupBy(t => t.transform.parent);
        
        foreach (var group in grouped)
        {
            Transform parent = group.Key;
            string parentName = parent != null ? parent.name : "NO PARENT";
            Debug.Log($"\nParent: {parentName} ({group.Count()} tiles)");
            
            foreach (var tile in group.Take(3)) // Show first 3 from each parent
            {
                Debug.Log($"  - {tile.gameObject.name}");
                Debug.Log($"    Position: {tile.transform.position}");
                Debug.Log($"    LocalPosition: {tile.transform.localPosition}");
                Debug.Log($"    Active: {tile.gameObject.activeInHierarchy}");
                
                // Check for mesh/renderer
                MeshFilter mf = tile.GetComponentInChildren<MeshFilter>();
                MeshRenderer mr = tile.GetComponentInChildren<MeshRenderer>();
                
                if (mf == null && mr == null)
                {
                    Debug.LogError($"    ❌ NO MESH OR RENDERER! This is an empty GameObject!");
                    
                    // Check how many children it has
                    Debug.Log($"    Child count: {tile.transform.childCount}");
                    
                    if (tile.transform.childCount > 0)
                    {
                        Debug.Log($"    Children:");
                        for (int i = 0; i < tile.transform.childCount && i < 3; i++)
                        {
                            Transform child = tile.transform.GetChild(i);
                            Debug.Log($"      [{i}] {child.name}");
                            
                            MeshFilter childMf = child.GetComponent<MeshFilter>();
                            MeshRenderer childMr = child.GetComponent<MeshRenderer>();
                            
                            if (childMf != null || childMr != null)
                            {
                                Debug.Log($"        ✓ Child has mesh/renderer!");
                            }
                        }
                    }
                }
                else
                {
                    Debug.Log($"    ✓ Has MeshFilter: {mf != null}, MeshRenderer: {mr != null}");
                    
                    if (mr != null)
                    {
                        Debug.Log($"    Renderer enabled: {mr.enabled}");
                        Debug.Log($"    Renderer visible: {mr.isVisible}");
                        
                        if (mr.sharedMaterial != null)
                        {
                            Debug.Log($"    Material: {mr.sharedMaterial.name}");
                        }
                        else
                        {
                            Debug.LogWarning($"    ⚠ No material assigned!");
                        }
                    }
                }
            }
        }

        // Check what the prefabs look like
        Debug.Log($"\n--- PREFAB STRUCTURE CHECK ---");
        NetworkedGameManager gm = NetworkedGameManager.Instance;
        
        if (gm != null && gm.TilePrefabs != null && gm.TilePrefabs.Length > 0)
        {
            GameObject samplePrefab = gm.TilePrefabs[0];
            Debug.Log($"Checking prefab structure: {samplePrefab.name}");
            
            MeshFilter prefabMf = samplePrefab.GetComponentInChildren<MeshFilter>();
            MeshRenderer prefabMr = samplePrefab.GetComponentInChildren<MeshRenderer>();
            
            Debug.Log($"  Prefab has MeshFilter: {prefabMf != null}");
            Debug.Log($"  Prefab has MeshRenderer: {prefabMr != null}");
            Debug.Log($"  Prefab child count: {samplePrefab.transform.childCount}");
            
            if (samplePrefab.transform.childCount > 0)
            {
                Debug.Log($"  Prefab children:");
                for (int i = 0; i < samplePrefab.transform.childCount && i < 5; i++)
                {
                    Transform child = samplePrefab.transform.GetChild(i);
                    Debug.Log($"    [{i}] {child.name}");
                }
            }
            
            // Check if the prefab itself or a child has the mesh
            if (prefabMf == null)
            {
                Debug.LogWarning("  ⚠ Prefab has no MeshFilter on root or children!");
                Debug.LogWarning("    This means your GLB import might have issues.");
            }
        }

        Debug.Log("=== END TRACE ===");
    }
}
