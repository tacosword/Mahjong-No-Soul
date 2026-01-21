using UnityEngine;
using Mirror;

/// <summary>
/// Comprehensive diagnostic to find why tiles aren't visible.
/// Add to any GameObject in Game scene.
/// </summary>
public class ComprehensiveVisibilityCheck : MonoBehaviour
{
    void Start()
    {
        Invoke(nameof(RunFullCheck), 6f);
    }

    [ContextMenu("Run Full Check")]
    public void RunFullCheck()
    {
        Debug.Log("=== COMPREHENSIVE VISIBILITY CHECK ===\n");

        // 1. Find local player
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
            Debug.LogError("❌ Could not find local player!");
            return;
        }

        int yourSeat = localPlayer.PlayerIndex;
        Debug.Log($"*** YOU ARE SEAT {yourSeat} ***\n");

        // 2. Check camera
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("❌ No main camera!");
            return;
        }

        Debug.Log("--- CAMERA ---");
        Debug.Log($"Position: {cam.transform.position}");
        Debug.Log($"Rotation: {cam.transform.eulerAngles}");
        Debug.Log($"Forward: {cam.transform.forward}");
        Debug.Log("");

        // 3. Check your container
        GameObject yourContainer = GameObject.Find($"HandPosition_Seat{yourSeat}");
        if (yourContainer == null)
        {
            Debug.LogError($"❌ HandPosition_Seat{yourSeat} not found!");
            return;
        }

        Debug.Log("--- YOUR CONTAINER ---");
        Debug.Log($"Name: {yourContainer.name}");
        Debug.Log($"Position: {yourContainer.transform.position}");
        Debug.Log($"Children: {yourContainer.transform.childCount}");
        Debug.Log($"Distance from camera: {Vector3.Distance(cam.transform.position, yourContainer.transform.position):F2}");
        Debug.Log("");

        if (yourContainer.transform.childCount == 0)
        {
            Debug.LogError("❌ YOUR CONTAINER IS EMPTY!");
            Debug.LogError("   Tiles were not spawned. Check server logs for 'Sending X tiles'");
            return;
        }

        // 4. Check first few tiles in detail
        Debug.Log("--- TILE DETAILS (First 3) ---");
        for (int i = 0; i < Mathf.Min(3, yourContainer.transform.childCount); i++)
        {
            Transform tile = yourContainer.transform.GetChild(i);
            
            Debug.Log($"\nTile {i}: {tile.name}");
            Debug.Log($"  World Position: {tile.position}");
            Debug.Log($"  Local Position: {tile.localPosition}");
            Debug.Log($"  Local Rotation: {tile.localEulerAngles}");
            Debug.Log($"  Local Scale: {tile.localScale}");
            Debug.Log($"  Active: {tile.gameObject.activeInHierarchy}");

            // Check renderer
            MeshRenderer renderer = tile.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = tile.GetComponentInChildren<MeshRenderer>();
            }

            if (renderer != null)
            {
                Debug.Log($"  Renderer enabled: {renderer.enabled}");
                Debug.Log($"  Renderer visible: {renderer.isVisible}");
                
                // Check frustum
                Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);
                bool inFrustum = GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
                Debug.Log($"  In camera frustum: {inFrustum}");
                
                if (!inFrustum)
                {
                    Debug.LogWarning($"  ⚠ TILE IS OUTSIDE CAMERA VIEW!");
                }
            }
            else
            {
                Debug.LogError($"  ❌ NO RENDERER FOUND!");
            }

            // Check distance and direction from camera
            float distance = Vector3.Distance(cam.transform.position, tile.position);
            Vector3 directionFromCam = tile.position - cam.transform.position;
            float dotProduct = Vector3.Dot(cam.transform.forward, directionFromCam.normalized);
            
            Debug.Log($"  Distance from camera: {distance:F2} units");
            Debug.Log($"  Direction alignment: {dotProduct:F2} (1.0 = directly in front, -1.0 = behind)");
            
            if (dotProduct < 0)
            {
                Debug.LogError($"  ❌ TILE IS BEHIND THE CAMERA!");
            }
        }

        // 5. Calculate what the positions SHOULD be
        Debug.Log("\n--- EXPECTED POSITIONS ---");
        NetworkedPlayerHand hand = localPlayer.GetComponent<NetworkedPlayerHand>();
        if (hand != null)
        {
            // These are the default values from the script
            Vector3 handStartPos = new Vector3(0f, 0f, -3f);
            float spacing = 1.2f;
            
            int tileCount = yourContainer.transform.childCount;
            float handWidth = (tileCount - 1) * spacing;
            float centerOffset = -handWidth / 2f;
            float startX = handStartPos.x + centerOffset;

            Debug.Log($"Expected LOCAL positions for {tileCount} tiles:");
            Debug.Log($"  Start X: {startX:F2}");
            Debug.Log($"  Tile 0 should be at LOCAL: ({startX:F2}, 0, -3)");
            Debug.Log($"  Tile 1 should be at LOCAL: ({startX + spacing:F2}, 0, -3)");
            Debug.Log($"  Tile 2 should be at LOCAL: ({startX + spacing * 2:F2}, 0, -3)");
            
            Debug.Log($"\nExpected WORLD positions:");
            Vector3 containerPos = yourContainer.transform.position;
            Debug.Log($"  Tile 0 should be at WORLD: ({containerPos.x + startX:F2}, {containerPos.y:F2}, {containerPos.z - 3:F2})");
        }

        // 6. Summary
        Debug.Log("\n--- DIAGNOSIS ---");
        Transform firstTile = yourContainer.transform.GetChild(0);
        Vector3 expectedLocalPos = new Vector3(-(yourContainer.transform.childCount - 1) * 0.6f, 0f, -3f);
        
        if (Vector3.Distance(firstTile.localPosition, expectedLocalPos) > 1f)
        {
            Debug.LogError("❌ TILES ARE NOT POSITIONED CORRECTLY!");
            Debug.LogError($"   Tile 0 local pos: {firstTile.localPosition}");
            Debug.LogError($"   Expected around: {expectedLocalPos}");
            Debug.LogError("   → RepositionTiles() did not run or failed!");
        }
        else
        {
            Debug.Log("✓ Tiles are positioned correctly (locally)");
        }

        MeshRenderer firstRenderer = firstTile.GetComponentInChildren<MeshRenderer>();
        if (firstRenderer != null)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            bool inView = GeometryUtility.TestPlanesAABB(planes, firstRenderer.bounds);
            
            if (!inView)
            {
                Debug.LogError("❌ TILES ARE OUTSIDE CAMERA FRUSTUM!");
                Debug.LogError("   SOLUTION: Adjust HandPosition_Seat0 position or camera position");
                
                // Suggest fix
                Vector3 idealContainerPos = cam.transform.position + cam.transform.forward * 10f - new Vector3(0, 3, 0);
                Debug.LogWarning($"   Try setting HandPosition_Seat{yourSeat} position to: {idealContainerPos}");
            }
            else
            {
                Debug.Log("✓ Tiles ARE in camera frustum");
                Debug.LogWarning("⚠ Tiles are positioned correctly but still not visible!");
                Debug.LogWarning("   → Check for: obstructing objects, render layers, or lighting issues");
            }
        }

        Debug.Log("\n=== END CHECK ===");
    }
}
