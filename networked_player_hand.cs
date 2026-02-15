using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Complete rewrite of NetworkedPlayerHand for synchronized multiplayer Mahjong.
/// Handles player interactions with their hand tiles.
/// </summary>
public class NetworkedPlayerHand : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform handContainer;
    
    // Visual feedback
    [SerializeField] private Material highlightMaterial;
    [SerializeField] private Material normalMaterial;
    
    // State for special actions
    private bool isInChiSelectionMode = false;
    private bool isSelectingKong = false;
    private List<int> availableKongValues = new List<int>();
    
    // Tenpai UI references
    [SerializeField] private GameObject tenpaiUIPanel;
    [SerializeField] private Transform tenpaiIconsContainer;
    [SerializeField] private GameObject iconPrefab;
    
    // Track which tiles belong to this player
    private NetworkPlayer networkPlayer;
    private int mySeatIndex = -1;
    
    // Public properties for NetworkedClickableTile
    public bool IsInChiSelectionMode => isInChiSelectionMode;
    public bool IsSelectingKong => isSelectingKong;
    public List<int> AvailableKongValues => availableKongValues;

    void Start()
    {
        // Get our network player component
        networkPlayer = GetComponent<NetworkPlayer>();
        
        if (networkPlayer == null)
        {
            Debug.LogError("[PlayerHand] No NetworkPlayer component found!");
            return;
        }
        
        // Find our seat index
        if (NetworkedGameManager.Instance != null)
        {
            mySeatIndex = NetworkedGameManager.Instance.GetSeatForPlayer(networkPlayer);
            Debug.Log($"[PlayerHand] Initialized for seat {mySeatIndex}");
        }
        
        if (tenpaiUIPanel != null)
        {
            tenpaiUIPanel.SetActive(false);
        }
        
        // AUTO-SYNC seat index from NetworkPlayer
        NetworkPlayer netPlayer = GetComponent<NetworkPlayer>();
        if (netPlayer != null)
        {
            mySeatIndex = netPlayer.PlayerIndex;
            Debug.Log($"[NetworkedPlayerHand] Auto-synced seat index from NetworkPlayer: {mySeatIndex}");
        }
        else
        {
            Debug.LogError("[NetworkedPlayerHand] No NetworkPlayer component found!");
        }
        
        // If seat is still -1, try again after a delay
        if (mySeatIndex == -1)
        {
            StartCoroutine(TryGetSeatIndexLater());
        }
    }

    private IEnumerator TryGetSeatIndexLater()
    {
        yield return new WaitForSeconds(0.5f);
        
        NetworkPlayer netPlayer = GetComponent<NetworkPlayer>();
        if (netPlayer != null && netPlayer.PlayerIndex != -1)
        {
            mySeatIndex = netPlayer.PlayerIndex;
            Debug.Log($"[NetworkedPlayerHand] Delayed sync - seat index is now: {mySeatIndex}");
        }
        else
        {
            Debug.LogError($"[NetworkedPlayerHand] Still can't get seat index! NetworkPlayer.PlayerIndex = {(netPlayer != null ? netPlayer.PlayerIndex : -999)}");
        }
    }

    /// <summary>
    /// Called when player clicks a tile to discard it
    /// </summary>
    public void DiscardAndDrawTile(Vector3 tilePosition, GameObject tileObject)
    {
        if (!isOwned)
        {
            Debug.LogWarning("[PlayerHand] Not owned - cannot discard");
            return;
        }
        
        if (NetworkedGameManager.Instance == null)
        {
            Debug.LogError("[PlayerHand] GameManager not found!");
            return;
        }
        
        // Check if it's our turn
        if (NetworkedGameManager.Instance.CurrentPlayerIndex != mySeatIndex)
        {
            Debug.LogWarning($"[PlayerHand] Not our turn (current: {NetworkedGameManager.Instance.CurrentPlayerIndex}, us: {mySeatIndex})");
            return;
        }
        
        NetworkIdentity tileIdentity = tileObject.GetComponent<NetworkIdentity>();
        if (tileIdentity == null)
        {
            Debug.LogError("[PlayerHand] Tile has no NetworkIdentity!");
            return;
        }
        
        uint tileNetId = tileIdentity.netId;
        Debug.Log($"[PlayerHand] Discarding tile {tileNetId}");
        
        // Send discard command to server
        NetworkedGameManager.Instance.CmdDiscardTile(tileNetId);
        
        // Hide any UI
        HideTenpaiVisuals();
        ClearAllHighlights();
    }

    /// <summary>
    /// Request tenpai check for a hovered tile
    /// </summary>
    public void RequestTenpaiCheck(GameObject hoveredTile)
    {
        if (!isOwned) return;
        
        if (isSelectingKong || isInChiSelectionMode) return;
        
        if (NetworkedGameManager.Instance == null) return;
        if (NetworkedGameManager.Instance.CurrentPlayerIndex != mySeatIndex) return;
        
        // For now, we'll implement a simple version
        // In a full implementation, you'd analyze the hand to see if discarding
        // this tile would leave you in tenpai (ready to win)
        
        // Hide tenpai UI for now since full implementation requires hand analysis
        HideTenpaiVisuals();
    }

    /// <summary>
    /// Highlight all tiles in hand that match the given sort value
    /// </summary>
    public void HighlightMatchingTiles(int sortValue)
    {
        if (!isOwned) return;
        
        // Find all tile objects in the scene
        GameObject[] allTiles = GameObject.FindGameObjectsWithTag("Tile");
        
        foreach (GameObject tile in allTiles)
        {
            TileData data = tile.GetComponent<TileData>();
            if (data == null) continue;
            
            NetworkIdentity identity = tile.GetComponent<NetworkIdentity>();
            if (identity == null) continue;
            
            // Check if this tile is in our hand
            if (!IsTileInMyHand(identity.netId)) continue;
            
            // Highlight if it matches
            if (data.GetSortValue() == sortValue)
            {
                HighlightTile(tile, true);
            }
        }
    }

    /// <summary>
    /// Clear all tile highlights
    /// </summary>
    public void ClearAllHighlights()
    {
        if (!isOwned) return;
        
        GameObject[] allTiles = GameObject.FindGameObjectsWithTag("Tile");
        
        foreach (GameObject tile in allTiles)
        {
            NetworkIdentity identity = tile.GetComponent<NetworkIdentity>();
            if (identity == null) continue;
            
            if (IsTileInMyHand(identity.netId))
            {
                HighlightTile(tile, false);
            }
        }
    }

    private void HighlightTile(GameObject tile, bool highlight)
    {
        Renderer renderer = tile.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            if (highlight && highlightMaterial != null)
            {
                renderer.material = highlightMaterial;
            }
            else if (!highlight && normalMaterial != null)
            {
                renderer.material = normalMaterial;
            }
            else
            {
                // Fallback to color tinting
                renderer.material.color = highlight ? Color.yellow : Color.white;
            }
        }
    }

    /// <summary>
    /// Check if a tile belongs to this player's hand
    /// </summary>
    private bool IsTileInMyHand(uint netId)
    {
        if (NetworkedGameManager.Instance == null) return false;
        if (mySeatIndex < 0) return false;
        
        return NetworkedGameManager.Instance.IsTileInSeatHand(mySeatIndex, netId);
    }

    /// <summary>
    /// Hide tenpai visual UI
    /// </summary>
    public void HideTenpaiVisuals()
    {
        if (tenpaiUIPanel != null)
        {
            tenpaiUIPanel.SetActive(false);
        }
    }

    // ========== CHI SELECTION MODE ==========
    // These methods are for when the player is selecting tiles for a Chi (sequence)
    
    public void OnChiTileClicked(int tileValue)
    {
        Debug.Log($"[PlayerHand] Chi tile clicked: {tileValue}");
        // Implementation depends on your Chi/Pong/Kong interrupt system
        // For now, just exit Chi mode
        isInChiSelectionMode = false;
    }

    public void OnChiTileHovered(int sortValue, Vector3 position)
    {
        Debug.Log($"[PlayerHand] Chi tile hovered: {sortValue}");
        // Show preview of Chi combination
    }

    public void EnterChiSelectionMode()
    {
        isInChiSelectionMode = true;
        Debug.Log("[PlayerHand] Entered Chi selection mode");
    }

    public void ExitChiSelectionMode()
    {
        isInChiSelectionMode = false;
        ClearAllHighlights();
        Debug.Log("[PlayerHand] Exited Chi selection mode");
    }

    // ========== KONG SELECTION MODE ==========
    
    public void CheckForKongOpportunity()
    {
        if (!isOwned) return;
        if (NetworkedGameManager.Instance == null) return;
        if (mySeatIndex < 0) return;
        
        availableKongValues.Clear();
        
        // Get all tiles in our hand
        List<TileData> allTiles = NetworkedGameManager.Instance.GetTilesForSeat(mySeatIndex);
        
        // Find tiles that appear 4 times
        var groups = allTiles
            .GroupBy(t => t.GetSortValue())
            .Where(g => g.Count() == 4)
            .Select(g => g.Key)
            .ToList();
        
        availableKongValues = groups;
        
        Debug.Log($"[PlayerHand] Checked for Kong opportunities: {availableKongValues.Count} found");
    }

    public void ExecuteKong(int targetValue)
    {
        if (!isOwned) return;
        
        Debug.Log($"[PlayerHand] Executing Kong for value {targetValue}");
        
        // Send Kong command to server
        // This would require adding a CmdExecuteKong method to the game manager
        
        isSelectingKong = false;
        availableKongValues.Clear();
        ClearAllHighlights();
    }

    public void DeclareKong()
    {
        if (!isOwned) return;
        
        isSelectingKong = !isSelectingKong;
        
        if (isSelectingKong)
        {
            CheckForKongOpportunity();
            
            // Highlight valid Kong tiles
            HighlightKongTiles();
        }
        else
        {
            ClearAllHighlights();
        }
    }

    private void HighlightKongTiles()
    {
        GameObject[] allTiles = GameObject.FindGameObjectsWithTag("Tile");
        
        foreach (GameObject tile in allTiles)
        {
            TileData data = tile.GetComponent<TileData>();
            if (data == null) continue;
            
            NetworkIdentity identity = tile.GetComponent<NetworkIdentity>();
            if (identity == null) continue;
            
            if (!IsTileInMyHand(identity.netId)) continue;
            
            int sortValue = data.GetSortValue();
            
            Renderer renderer = tile.GetComponentInChildren<Renderer>();
            if (renderer == null) continue;
            
            if (availableKongValues.Contains(sortValue))
            {
                // Highlight valid Kong tiles
                renderer.material.color = Color.white;
            }
            else
            {
                // Dim non-Kong tiles
                renderer.material.color = new Color(0.3f, 0.3f, 0.3f, 1.0f);
            }
        }
    }
}