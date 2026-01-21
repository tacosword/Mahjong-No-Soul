using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages a player's hand of tiles in the networked Mahjong game.
/// Each player has their own instance of this component.
/// </summary>
public class MahjongPlayerHand : NetworkBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] public Transform handContainer;
    [SerializeField] private float tileSpacing = 1.2f;

    // Private hand data (only visible to the owning player)
    private List<int> handTiles = new List<int>(); // Sort values of tiles in hand
    private int drawnTile = 0; // The tile just drawn (separate from hand)
    private List<GameObject> tileObjects = new List<GameObject>(); // Visual tiles
    private GameObject drawnTileObject = null;

    private int playerIndex = -1;

    void Start()
    {
        // Find our player index
        NetworkPlayer netPlayer = GetComponent<NetworkPlayer>();
        if (netPlayer != null)
        {
            playerIndex = netPlayer.PlayerIndex;
        }
    }

    /// <summary>
    /// Receive initial 13-tile hand from server (only sent to owning player).
    /// </summary>
    [TargetRpc]
    public void TargetReceiveInitialHand(NetworkConnection target, List<int> tiles)
    {
        Debug.Log($"Received initial hand with {tiles.Count} tiles.");
        
        handTiles = new List<int>(tiles);
        handTiles.Sort(); // Sort tiles for display

        SpawnHandVisuals();
    }

    /// <summary>
    /// Draw a new tile (sent only to the current player).
    /// </summary>
    [TargetRpc]
    public void TargetDrawTile(NetworkConnection target, int tileValue)
    {
        Debug.Log($"Drew tile: {tileValue}");
        
        drawnTile = tileValue;
        SpawnDrawnTileVisual();

        // Check for win condition
        CheckForWin();
    }

    /// <summary>
    /// Discard a tile from hand.
    /// </summary>
    public void DiscardTile(int tileValue)
    {
        if (!isOwned)
        {
            Debug.LogWarning("Cannot discard tile - not owned!");
            return;
        }

        bool discardedFromHand = false;

        // Check if discarding the drawn tile
        if (drawnTile == tileValue)
        {
            drawnTile = 0;
            Destroy(drawnTileObject);
            drawnTileObject = null;
        }
        // Otherwise discard from hand
        else if (handTiles.Contains(tileValue))
        {
            handTiles.Remove(tileValue);
            discardedFromHand = true;
            
            // Move drawn tile into hand
            if (drawnTile != 0)
            {
                handTiles.Add(drawnTile);
                handTiles.Sort();
                drawnTile = 0;
            }
            
            SpawnHandVisuals();
        }
        else
        {
            Debug.LogError($"Tile {tileValue} not found in hand!");
            return;
        }

        // Tell server about the discard
        CmdDiscardTile(playerIndex, tileValue);
    }

    /// <summary>
    /// Command: Player discards a tile (Client -> Server).
    /// </summary>
    [Command]
    private void CmdDiscardTile(int pIndex, int tileValue)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayerDiscardedTile(pIndex, tileValue);
        }
    }

    /// <summary>
    /// Declare Mahjong (win).
    /// </summary>
    public void DeclareMahjong()
    {
        if (!isOwned) return;

        Debug.Log("Declaring Mahjong!");
        CmdDeclareMahjong(playerIndex);
    }

    [Command]
    private void CmdDeclareMahjong(int pIndex)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayerDeclaredMahjong(pIndex);
        }
    }

    /// <summary>
    /// Check if current hand is a winning hand.
    /// </summary>
    private void CheckForWin()
    {
        // Combine hand + drawn tile
        List<int> fullHand = new List<int>(handTiles);
        if (drawnTile != 0)
        {
            fullHand.Add(drawnTile);
        }

        if (fullHand.Count != 14)
        {
            return; // Not a full hand yet
        }

        // Use your existing PlayerHand logic to check for win
        // For now, simplified check
        bool isWinningHand = IsBasicWinningHand(fullHand);

        if (isWinningHand)
        {
            Debug.Log("You have a winning hand! You can declare Mahjong.");
            // Show Mahjong button in UI
        }
    }

    /// <summary>
    /// Simplified winning hand check (4 sets + 1 pair).
    /// </summary>
    private bool IsBasicWinningHand(List<int> tiles)
    {
        if (tiles.Count != 14) return false;

        // Sort tiles
        List<int> sorted = new List<int>(tiles);
        sorted.Sort();

        // Try to find a pair and 4 sets
        var counts = new Dictionary<int, int>();
        foreach (int tile in sorted)
        {
            if (!counts.ContainsKey(tile))
                counts[tile] = 0;
            counts[tile]++;
        }

        // Try each tile as the pair
        foreach (var kvp in counts)
        {
            if (kvp.Value >= 2)
            {
                // Remove pair
                List<int> remaining = new List<int>(sorted);
                remaining.Remove(kvp.Key);
                remaining.Remove(kvp.Key);

                if (CanFormSets(remaining))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Check if tiles can form 4 sets (triplets or sequences).
    /// </summary>
    private bool CanFormSets(List<int> tiles)
    {
        if (tiles.Count == 0) return true;
        if (tiles.Count % 3 != 0) return false;

        int first = tiles[0];

        // Try triplet
        if (tiles.Count(t => t == first) >= 3)
        {
            List<int> next = new List<int>(tiles);
            for (int i = 0; i < 3; i++)
                next.Remove(first);
            if (CanFormSets(next)) return true;
        }

        // Try sequence
        if (tiles.Contains(first + 1) && tiles.Contains(first + 2))
        {
            List<int> next = new List<int>(tiles);
            next.Remove(first);
            next.Remove(first + 1);
            next.Remove(first + 2);
            if (CanFormSets(next)) return true;
        }

        return false;
    }

    /// <summary>
    /// Spawn visual tiles for the hand.
    /// </summary>
    private void SpawnHandVisuals()
    {
        // Clear existing tiles
        foreach (GameObject obj in tileObjects)
        {
            Destroy(obj);
        }
        tileObjects.Clear();

        if (handContainer == null || tilePrefab == null)
        {
            Debug.LogWarning("Cannot spawn tiles - missing prefab or container!");
            return;
        }

        // Spawn new tiles
        for (int i = 0; i < handTiles.Count; i++)
        {
            GameObject tile = Instantiate(tilePrefab, handContainer);
            tile.transform.localPosition = new Vector3(i * tileSpacing, 0, 0);
            
            // Set tile data
            TileData tileData = tile.GetComponent<TileData>();
            if (tileData != null)
            {
                SetTileDataFromSortValue(tileData, handTiles[i]);
            }

            // Make tile clickable for discarding
            MahjongTileButton button = tile.GetComponent<MahjongTileButton>();
            if (button == null)
            {
                button = tile.AddComponent<MahjongTileButton>();
            }
            button.Initialize(this, handTiles[i]);

            tileObjects.Add(tile);
        }
    }

    /// <summary>
    /// Spawn visual tile for the drawn tile.
    /// </summary>
    private void SpawnDrawnTileVisual()
    {
        if (drawnTileObject != null)
        {
            Destroy(drawnTileObject);
        }

        if (handContainer == null || tilePrefab == null)
        {
            return;
        }

        drawnTileObject = Instantiate(tilePrefab, handContainer);
        drawnTileObject.transform.localPosition = new Vector3(handTiles.Count * tileSpacing + 0.5f, 0, 0);

        TileData tileData = drawnTileObject.GetComponent<TileData>();
        if (tileData != null)
        {
            SetTileDataFromSortValue(tileData, drawnTile);
        }

        MahjongTileButton button = drawnTileObject.GetComponent<MahjongTileButton>();
        if (button == null)
        {
            button = drawnTileObject.AddComponent<MahjongTileButton>();
        }
        button.Initialize(this, drawnTile);
    }

    /// <summary>
    /// Set TileData based on sort value.
    /// </summary>
    private void SetTileDataFromSortValue(TileData tileData, int sortValue)
    {
        int suitValue = sortValue / 100;
        int numberValue = sortValue % 100;

        tileData.suit = (MahjongSuit)suitValue;
        tileData.value = numberValue;
    }
}
