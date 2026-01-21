using UnityEngine;

public class InputDebugger : MonoBehaviour
{
    void Update()
    {
        // Check if the left mouse button was clicked down this frame
        if (Input.GetMouseButtonDown(0))
        {
            // Cast a ray from the camera through the mouse position
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Check if the ray hits anything with a Collider
            if (Physics.Raycast(ray, out hit))
            {
                // This message proves the raycast hit SOMETHING!
                // Debug.Log($"RAYCAST HIT detected on: {hit.collider.gameObject.name} (Layer: {hit.collider.gameObject.layer})", hit.collider.gameObject);
                
                // Now try to get the TileManager component from the hit object's hierarchy
                ClickableTile tile = hit.collider.GetComponent<ClickableTile>();

                if (tile != null)
                {
                    // Debug.LogWarning($"SUCCESS! Tile component found on object: {hit.collider.gameObject.name}. Click should have worked!");
                    // If this runs, your logic is good, but OnMouseDown is being blocked.
                }
                else
                {
                    // Debug.Log($"Hit an object, but it wasn't a Mahjong tile. Object name: {hit.collider.gameObject.name}");
                }
            }
            else
            {
                // This message proves the raycast hit NOTHING
                // Debug.Log("Raycast missed all colliders in the scene.");
            }
        }
    }
}