using UnityEngine;

public class WhyAreTheyScattered : MonoBehaviour
{
    [ContextMenu("Debug Scattered Tiles")]
    public void DebugScatter()
    {
        Debug.Log("=== WHY ARE TILES SCATTERED? ===\n");

        GameObject container = GameObject.Find("HandPosition_Seat0");
        if (container == null)
        {
            Debug.LogError("Can't find container!");
            return;
        }

        Debug.Log($"Container: {container.name}");
        Debug.Log($"Container Position: {container.transform.position}");
        Debug.Log($"Container Rotation: {container.transform.eulerAngles}");
        Debug.Log($"Container has {container.transform.childCount} children\n");

        // Check first 5 tiles
        for (int i = 0; i < Mathf.Min(5, container.transform.childCount); i++)
        {
            Transform tile = container.transform.GetChild(i);
            
            Debug.Log($"=== TILE {i}: {tile.name} ===");
            Debug.Log($"GameObject Local Position: {tile.localPosition}");
            Debug.Log($"GameObject Local Rotation: {tile.localEulerAngles}");
            Debug.Log($"GameObject World Position: {tile.position}");
            
            // Check parent
            Debug.Log($"Parent: {tile.parent.name}");
            Debug.Log($"Same container? {tile.parent == container.transform}");
            
            // Check for mesh/renderer offset
            MeshRenderer renderer = tile.GetComponentInChildren<MeshRenderer>(true);
            MeshFilter meshFilter = tile.GetComponentInChildren<MeshFilter>(true);
            
            if (renderer != null)
            {
                Debug.Log($"Renderer GameObject: {renderer.gameObject.name}");
                Debug.Log($"Renderer Local Pos: {renderer.transform.localPosition}");
                Debug.Log($"Renderer World Pos: {renderer.transform.position}");
                
                if (renderer.transform != tile)
                {
                    Debug.LogWarning("  ⚠ RENDERER IS ON A CHILD OBJECT!");
                    Debug.LogWarning($"     Child offset: {renderer.transform.localPosition}");
                }
            }
            
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                Debug.Log($"Mesh Bounds Center: {meshBounds.center}");
                
                if (meshBounds.center.magnitude > 0.1f)
                {
                    Debug.LogError($"  ❌ MESH HAS OFFSET CENTER: {meshBounds.center}");
                    Debug.LogError("     This is why tiles appear scattered!");
                }
            }
            
            Debug.Log("");
        }

        Debug.Log("\n=== DIAGNOSIS ===");
        Debug.Log("If tiles have:");
        Debug.Log("- Same parent: ✓");
        Debug.Log("- Same local position: ✓");  
        Debug.Log("- Different world positions: ❌");
        Debug.Log("\nThen the problem is:");
        Debug.Log("1. Renderer is on child object with offset, OR");
        Debug.Log("2. Mesh bounds center is not at origin, OR");
        Debug.Log("3. Tile has individual rotation");
        
        Debug.Log("\n=== END DEBUG ===");
    }
    
    [ContextMenu("Force Align All Tiles")]
    public void ForceAlign()
    {
        GameObject container = GameObject.Find("HandPosition_Seat0");
        if (container == null) return;

        Debug.Log("Forcing all tiles to (0, 0, 0) local position...");
        
        for (int i = 0; i < container.transform.childCount; i++)
        {
            Transform tile = container.transform.GetChild(i);
            tile.localPosition = Vector3.zero;
            tile.localRotation = Quaternion.Euler(-45, 0, 0);
            
            Debug.Log($"{tile.name} set to (0,0,0) local. World: {tile.position}");
        }
        
        Debug.Log("\nIf world positions are different, it's the GLB mesh offset.");
    }
}
