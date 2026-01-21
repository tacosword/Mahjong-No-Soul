using UnityEngine;

/// <summary>
/// Diagnoses why tiles are spawning but not visible in camera.
/// </summary>
public class CameraViewDiagnostic : MonoBehaviour
{
    [ContextMenu("Check Camera View")]
    public void CheckCameraView()
    {
        Debug.Log("=== CAMERA VIEW DIAGNOSTIC ===");

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("❌ No main camera found!");
            return;
        }

        Debug.Log($"\n--- CAMERA INFO ---");
        Debug.Log($"Camera: {mainCam.name}");
        Debug.Log($"Position: {mainCam.transform.position}");
        Debug.Log($"Rotation: {mainCam.transform.eulerAngles}");
        Debug.Log($"FOV: {mainCam.fieldOfView}");

        // Find tiles
        MeshRenderer[] allRenderers = FindObjectsOfType<MeshRenderer>();
        Debug.Log($"\n--- CHECKING {allRenderers.Length} RENDERERS ---");

        int visibleCount = 0;
        int inFrustumCount = 0;

        foreach (var renderer in allRenderers)
        {
            if (renderer.name.Contains("Prefab"))
            {
                bool isVisible = renderer.isVisible;
                
                // Check if in frustum
                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCam);
                bool inFrustum = GeometryUtility.TestPlanesAABB(planes, renderer.bounds);

                if (isVisible) visibleCount++;
                if (inFrustum) inFrustumCount++;

                Debug.Log($"Tile: {renderer.name}");
                Debug.Log($"  Position: {renderer.transform.position}");
                Debug.Log($"  Visible: {isVisible}");
                Debug.Log($"  In Frustum: {inFrustum}");
                Debug.Log($"  Distance from camera: {Vector3.Distance(mainCam.transform.position, renderer.transform.position):F2}");

                // Check what's between camera and tile
                Vector3 directionToTile = renderer.transform.position - mainCam.transform.position;
                Ray ray = new Ray(mainCam.transform.position, directionToTile.normalized);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, directionToTile.magnitude))
                {
                    if (hit.collider.gameObject != renderer.gameObject)
                    {
                        Debug.LogWarning($"  ⚠ Something blocking view: {hit.collider.name}");
                    }
                }

                break; // Just check first tile
            }
        }

        Debug.Log($"\nSummary:");
        Debug.Log($"  Visible tiles: {visibleCount}");
        Debug.Log($"  In frustum: {inFrustumCount}");

        // Check HandPosition containers
        Debug.Log($"\n--- HAND CONTAINERS ---");
        for (int i = 0; i < 4; i++)
        {
            GameObject container = GameObject.Find($"HandPosition_Seat{i}");
            if (container != null)
            {
                Debug.Log($"Seat{i}: Position={container.transform.position}, Children={container.transform.childCount}");
                
                // Check if this is the local player's seat
                Mirror.NetworkIdentity localPlayer = Mirror.NetworkClient.localPlayer?.GetComponent<Mirror.NetworkIdentity>();
                if (localPlayer != null)
                {
                    Debug.Log($"  *** THIS IS YOUR SEAT (local player) ***");
                    
                    // This container should be positioned for your camera
                    float distance = Vector3.Distance(mainCam.transform.position, container.transform.position);
                    Debug.Log($"  Distance from camera: {distance:F2} units");
                    
                    if (distance > 30)
                    {
                        Debug.LogError($"  ❌ TOO FAR! Your tiles are {distance:F2} units away!");
                        Debug.LogError($"     Tiles should be 10-20 units from camera.");
                    }
                }
            }
        }

        Debug.Log("\n=== END DIAGNOSTIC ===");
    }
}
