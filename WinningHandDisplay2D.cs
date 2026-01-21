using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Mirror;

/// <summary>
/// Displays the winning hand as 2D sprite images in the UI.
/// Much simpler than 3D objects - uses the tileSprite from each TileData.
/// </summary>
public class WinningHandDisplay2D : MonoBehaviour
{
    [Header("UI Container")]
    public Transform spriteContainer; // HorizontalLayoutGroup recommended
    
    [Header("Display Settings")]
    public float tileWidth = 80f;
    public float tileHeight = 120f;
    
    private List<GameObject> displayedImages = new List<GameObject>();

    /// <summary>
    /// Display the winning hand as 2D sprites.
    /// </summary>
    /// <param name="winnerSeatIndex">Seat index of winner (-1 = local player)</param>
    /// <param name="tileSortValues">Optional: Use these tile values instead of GameObject tiles</param>
    public void DisplayWinningHand(int winnerSeatIndex = -1, List<int> tileSortValues = null)
    {
        ClearDisplay();
        
        if (spriteContainer == null)
        {
            Debug.LogError("[WinningHandDisplay2D] spriteContainer is null!");
            return;
        }
        
        // If we have tile sort values, use those directly (most reliable for network sync)
        if (tileSortValues != null && tileSortValues.Count > 0)
        {
            Debug.Log($"[WinningHandDisplay2D] Using provided tile sort values: {tileSortValues.Count} tiles");
            DisplayTilesFromSortValues(tileSortValues);
            return;
        }
        
        // Otherwise, fall back to finding player's GameObjects
        Debug.Log("[WinningHandDisplay2D] No tile values provided, falling back to GameObject lookup");
        
        // Find the winner's player object
        NetworkPlayer winnerPlayer = null;
        
        if (winnerSeatIndex >= 0)
        {
            // Find player by seat index
            NetworkPlayer[] allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            foreach (NetworkPlayer player in allPlayers)
            {
                if (player.PlayerIndex == winnerSeatIndex)
                {
                    winnerPlayer = player;
                    Debug.Log($"[WinningHandDisplay2D] Found winner: Seat {winnerSeatIndex}, Name: {player.Username}");
                    break;
                }
            }
        }
        else
        {
            // Default to local player
            winnerPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
            Debug.Log("[WinningHandDisplay2D] Using local player as winner");
        }
        
        if (winnerPlayer == null)
        {
            Debug.LogError("[WinningHandDisplay2D] Cannot find winner player!");
            return;
        }
        
        NetworkedPlayerHand playerHand = winnerPlayer.GetComponent<NetworkedPlayerHand>();
        if (playerHand == null)
        {
            Debug.LogError("[WinningHandDisplay2D] Cannot find NetworkedPlayerHand!");
            return;
        }
        
        // Get all tiles INCLUDING kongs
        List<GameObject> handTiles = playerHand.GetAllHandTilesIncludingKongs();
        
        if (handTiles.Count == 0)
        {
            Debug.LogWarning("[WinningHandDisplay2D] No tiles to display!");
            return;
        }
        
        Debug.Log($"[WinningHandDisplay2D] Displaying {handTiles.Count} tiles as sprites (including Kongs)");
        
        // Display each tile as a 2D image
        foreach (GameObject tile in handTiles)
        {
            if (tile == null) continue;
            
            TileData tileData = tile.GetComponent<TileData>();
            if (tileData == null || tileData.tileSprite == null)
            {
                Debug.LogWarning($"[WinningHandDisplay2D] Tile {tile.name} has no sprite!");
                continue;
            }
            
            CreateTileImage(tileData.tileSprite, tileData.GetSortValue());
        }
        
        Debug.Log($"[WinningHandDisplay2D] Successfully displayed {displayedImages.Count} tile sprites");
    }
    
    /// <summary>
    /// Display tiles from sort values (network-synced method).
    /// </summary>
    private void DisplayTilesFromSortValues(List<int> sortValues)
    {
        // Find the NetworkedGameManager to access tile prefabs
        NetworkedGameManager gameManager = FindFirstObjectByType<NetworkedGameManager>();
        if (gameManager == null || gameManager.TilePrefabs == null)
        {
            Debug.LogError("[WinningHandDisplay2D] Cannot find NetworkedGameManager or tile prefabs!");
            return;
        }
        
        Debug.Log($"[WinningHandDisplay2D] Displaying {sortValues.Count} tiles from sort values");
        
        foreach (int sortValue in sortValues)
        {
            // Find the prefab for this tile
            GameObject tilePrefab = FindTilePrefabBySortValue(sortValue, gameManager.TilePrefabs);
            if (tilePrefab == null)
            {
                Debug.LogWarning($"[WinningHandDisplay2D] Could not find prefab for sort value {sortValue}");
                continue;
            }
            
            TileData tileData = tilePrefab.GetComponent<TileData>();
            if (tileData == null || tileData.tileSprite == null)
            {
                Debug.LogWarning($"[WinningHandDisplay2D] Prefab for {sortValue} has no sprite!");
                continue;
            }
            
            CreateTileImage(tileData.tileSprite, sortValue);
        }
        
        Debug.Log($"[WinningHandDisplay2D] Successfully displayed {displayedImages.Count} tiles from sort values");
    }
    
    /// <summary>
    /// Create a UI Image for a tile sprite.
    /// </summary>
    private void CreateTileImage(Sprite sprite, int sortValue)
    {
        GameObject imageObj = new GameObject($"TileImage_{sortValue}");
        imageObj.transform.SetParent(spriteContainer, false);
        
        Image image = imageObj.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        
        RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(tileWidth, tileHeight);
        
        displayedImages.Add(imageObj);
    }
    
    /// <summary>
    /// Find a tile prefab by its sort value.
    /// </summary>
    private GameObject FindTilePrefabBySortValue(int sortValue, GameObject[] prefabs)
    {
        foreach (GameObject prefab in prefabs)
        {
            TileData data = prefab.GetComponent<TileData>();
            if (data != null && data.GetSortValue() == sortValue)
            {
                return prefab;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Clear all displayed tile images.
    /// </summary>
    public void ClearDisplay()
    {
        foreach (GameObject img in displayedImages)
        {
            if (img != null) Destroy(img);
        }
        displayedImages.Clear();
    }
    
    void OnDestroy()
    {
        ClearDisplay();
    }
}