using UnityEngine;
using Mirror;

/// <summary>
/// Quick diagnostic to check if NetworkedGameManager has tile prefabs assigned.
/// Add this to any GameObject in your Game scene.
/// </summary>
public class QuickPrefabCheck : MonoBehaviour
{
    void Start()
    {
        Invoke(nameof(CheckPrefabs), 3f);
    }

    [ContextMenu("Check Prefabs Now")]
    public void CheckPrefabs()
    {
        Debug.Log("=== QUICK PREFAB CHECK ===");

        // Find NetworkedGameManager
        NetworkedGameManager gameManager = FindObjectOfType<NetworkedGameManager>();
        
        if (gameManager == null)
        {
            Debug.LogError("❌ CRITICAL: NetworkedGameManager NOT FOUND in scene!");
            Debug.LogError("   Make sure the Game scene has a GameObject with NetworkedGameManager component.");
            return;
        }

        Debug.Log($"✓ Found NetworkedGameManager on: {gameManager.gameObject.name}");

        // Check TilePrefabs array
        if (gameManager.TilePrefabs == null)
        {
            Debug.LogError("❌ CRITICAL: TilePrefabs array is NULL!");
            Debug.LogError("   Solution: Select the NetworkedGameManager GameObject in the hierarchy.");
            Debug.LogError("   In Inspector, find 'Tile Prefabs' field and assign your 42 tile prefabs to it.");
            return;
        }

        if (gameManager.TilePrefabs.Length == 0)
        {
            Debug.LogError("❌ CRITICAL: TilePrefabs array is EMPTY (length = 0)!");
            Debug.LogError("   Solution: Select the NetworkedGameManager GameObject in the hierarchy.");
            Debug.LogError("   In Inspector, find 'Tile Prefabs' field and assign your 42 tile prefabs to it.");
            Debug.LogError("   You need to drag all your GLB tile prefabs into this array.");
            return;
        }

        Debug.Log($"✓ TilePrefabs array has {gameManager.TilePrefabs.Length} slots");

        // Count how many are actually assigned vs null
        int nullCount = 0;
        int validCount = 0;
        int withTileData = 0;

        for (int i = 0; i < gameManager.TilePrefabs.Length; i++)
        {
            if (gameManager.TilePrefabs[i] == null)
            {
                nullCount++;
            }
            else
            {
                validCount++;
                
                TileData data = gameManager.TilePrefabs[i].GetComponent<TileData>();
                if (data != null)
                {
                    withTileData++;
                    if (i < 5) // Show first 5
                    {
                        Debug.Log($"  [{i}] {gameManager.TilePrefabs[i].name}: suit={data.suit}, value={data.value}, sortValue={data.GetSortValue()}");
                    }
                }
                else
                {
                    Debug.LogWarning($"  [{i}] {gameManager.TilePrefabs[i].name}: NO TileData component!");
                }
            }
        }

        Debug.Log($"\nSummary:");
        Debug.Log($"  Total slots: {gameManager.TilePrefabs.Length}");
        Debug.Log($"  Valid prefabs: {validCount}");
        Debug.Log($"  NULL prefabs: {nullCount}");
        Debug.Log($"  Prefabs with TileData: {withTileData}");

        if (validCount < 34)
        {
            Debug.LogError("❌ Not enough prefabs! You need at least 34 tile prefabs (42 including flowers).");
        }
        else if (withTileData < 34)
        {
            Debug.LogError("❌ Not enough prefabs with TileData! You need at least 34.");
        }
        else
        {
            Debug.Log("✓ Prefab array looks good!");
        }

        Debug.Log("=== END CHECK ===");
    }
}
