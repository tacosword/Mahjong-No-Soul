using UnityEngine;

public class ClickableTile : MonoBehaviour
{
    private TileManager tileManager;

    void Start()
    {
        // 1. Explicitly try to find the TileManager object in the scene
        tileManager = FindObjectOfType<TileManager>();

        // 2. CRUCIAL DEBUG CHECK: If the manager is not found, log a loud error.
        if (tileManager == null)
        {
            //Debug.LogError($"TileManager not found in the scene! Click events will fail for tile: {gameObject.name}.");
            enabled = false;
        } else {
            //Debug.Log($"TileManager successfully found by: {gameObject.name}", this);
        }
    }

    // Unity function called when the user clicks the Collider of this object
    void OnMouseDown()
{
    if (tileManager != null)
    {
        // Check if the manager is currently waiting for a Kong selection
        if (tileManager.IsSelectingKong) 
        {
            // Get the value of this specific tile
            int tileValue = GetComponent<TileData>().GetSortValue();

            // Check if this tile's value is one of the valid Kong options
            if (tileManager.AvailableKongValues.Contains(tileValue))
            {
                tileManager.ExecuteKong(tileValue);
                return; // STOP HERE so we don't discard the tile!
            }
        }

        // If not in Kong mode, or clicked an invalid tile, perform normal discard
        tileManager.DiscardAndDrawTile(transform.position, gameObject);
    }
}

// Inside ClickableTile.cs
void OnMouseEnter()
{
    if (tileManager != null)
    {
        // Ask TileManager to check if discarding THIS tile puts us in Tenpai
        tileManager.RequestTenpaiCheck(gameObject);
    }
}

void OnMouseExit()
{
    if (tileManager != null)
    {
        tileManager.HideTenpaiVisuals();
    }
}


}