using UnityEngine;
using System.Linq;

/// <summary>
/// Add this script to any GameObject in your Game scene to debug why tiles aren't visible.
/// It will log detailed information about tile positions, camera, and rendering.
/// </summary>
public class TileVisibilityDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool runOnStart = true;
    [SerializeField] private KeyCode debugKey = KeyCode.F1;

    void Start()
    {
        if (runOnStart)
        {
            Invoke(nameof(RunDiagnostics), 3f); // Wait 3 seconds for tiles to spawn
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(debugKey))
        {
            RunDiagnostics();
        }
    }

    [ContextMenu("Run Diagnostics")]
    public void RunDiagnostics()
    {
        Debug.Log("=== TILE VISIBILITY DIAGNOSTICS ===");
        
        CheckCamera();
        CheckTiles();
        CheckHandContainer();
        
        Debug.Log("=== END DIAGNOSTICS ===");
    }

    void CheckCamera()
    {
        Debug.Log("\n--- CAMERA CHECK ---");
        
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("❌ NO MAIN CAMERA FOUND!");
            return;
        }

        Debug.Log($"✓ Camera found: {mainCam.name}");
        Debug.Log($"  Position: {mainCam.transform.position}");
        Debug.Log($"  Rotation: {mainCam.transform.eulerAngles}");
        Debug.Log($"  Field of View: {mainCam.fieldOfView}");
        Debug.Log($"  Near Clip: {mainCam.nearClipPlane}");
        Debug.Log($"  Far Clip: {mainCam.farClipPlane}");
        Debug.Log($"  Culling Mask: {LayerMask.LayerToName(0)} (Default layer visible: {mainCam.cullingMask == -1 || ((mainCam.cullingMask & (1 << 0)) != 0)})");
    }

    void CheckHandContainer()
    {
        Debug.Log("\n--- HAND CONTAINER CHECK ---");
        
        GameObject container = GameObject.Find("HandPosition_Seat0");
        if (container == null)
        {
            Debug.LogError("❌ HandPosition_Seat0 not found!");
            
            // Try to find alternatives
            var allContainers = FindObjectsOfType<Transform>()
                .Where(t => t.name.Contains("Hand"))
                .ToList();
                
            if (allContainers.Any())
            {
                Debug.Log($"  Found {allContainers.Count} objects with 'Hand' in name:");
                foreach (var c in allContainers)
                {
                    Debug.Log($"    - {c.name} at {c.position}");
                }
            }
            return;
        }

        Debug.Log($"✓ Container found: {container.name}");
        Debug.Log($"  World Position: {container.transform.position}");
        Debug.Log($"  Local Position: {container.transform.localPosition}");
        Debug.Log($"  Child Count: {container.transform.childCount}");
    }

    void CheckTiles()
    {
        Debug.Log("\n--- TILE CHECK ---");
        
        // Find all tiles in the scene
        var allTiles = FindObjectsOfType<TileData>();
        
        if (allTiles.Length == 0)
        {
            Debug.LogWarning("❌ NO TILES WITH TileData COMPONENT FOUND!");
            
            // Check for ANY GameObjects with "Tile" in the name
            var allObjects = FindObjectsOfType<GameObject>()
                .Where(go => go.name.ToLower().Contains("tile"))
                .ToList();
                
            if (allObjects.Any())
            {
                Debug.Log($"  Found {allObjects.Count} objects with 'Tile' in name (but no TileData)");
            }
            return;
        }

        Debug.Log($"✓ Found {allTiles.Length} tiles with TileData component");
        
        // Check first 5 tiles in detail
        for (int i = 0; i < Mathf.Min(5, allTiles.Length); i++)
        {
            var tile = allTiles[i];
            CheckSingleTile(tile.gameObject, i);
        }
    }

    void CheckSingleTile(GameObject tile, int index)
    {
        Debug.Log($"\n  TILE {index}: {tile.name}");
        Debug.Log($"    Active: {tile.activeInHierarchy}");
        Debug.Log($"    World Position: {tile.transform.position}");
        Debug.Log($"    Local Position: {tile.transform.localPosition}");
        Debug.Log($"    Scale: {tile.transform.localScale}");
        Debug.Log($"    Rotation: {tile.transform.eulerAngles}");
        Debug.Log($"    Layer: {LayerMask.LayerToName(tile.layer)}");
        
        // Check for Renderer
        var renderer = tile.GetComponentInChildren<Renderer>();
        if (renderer == null)
        {
            Debug.LogError($"    ❌ NO RENDERER FOUND!");
        }
        else
        {
            Debug.Log($"    ✓ Renderer: {renderer.GetType().Name}");
            Debug.Log($"      Enabled: {renderer.enabled}");
            Debug.Log($"      Visible: {renderer.isVisible}");
            Debug.Log($"      Bounds Center: {renderer.bounds.center}");
            Debug.Log($"      Bounds Size: {renderer.bounds.size}");
            
            if (renderer.sharedMaterial == null)
            {
                Debug.LogWarning($"      ⚠ No material assigned!");
            }
            else
            {
                Debug.Log($"      Material: {renderer.sharedMaterial.name}");
            }
        }
        
        // Check for MeshFilter
        var meshFilter = tile.GetComponentInChildren<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogWarning($"    ⚠ No MeshFilter found (might be using Sprite)");
        }
        else
        {
            if (meshFilter.sharedMesh == null)
            {
                Debug.LogError($"    ❌ MeshFilter has no mesh!");
            }
            else
            {
                Debug.Log($"    ✓ Mesh: {meshFilter.sharedMesh.name} ({meshFilter.sharedMesh.vertexCount} verts)");
            }
        }
        
        // Check for SpriteRenderer
        var spriteRenderer = tile.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            Debug.Log($"    ✓ SpriteRenderer found");
            Debug.Log($"      Sprite: {(spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "NULL")}");
            Debug.Log($"      Sorting Layer: {spriteRenderer.sortingLayerName}");
            Debug.Log($"      Order in Layer: {spriteRenderer.sortingOrder}");
        }
        
        // Calculate distance from camera
        Camera cam = Camera.main;
        if (cam != null)
        {
            float distance = Vector3.Distance(cam.transform.position, tile.transform.position);
            Debug.Log($"    Distance from camera: {distance:F2} units");
            
            // Check if in view frustum
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            bool inFrustum = false;
            
            if (renderer != null)
            {
                inFrustum = GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
                Debug.Log($"    In Camera Frustum: {inFrustum}");
            }
        }
    }
}
