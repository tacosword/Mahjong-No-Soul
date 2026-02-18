using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Complete rewrite of NetworkedPlayerHand for synchronized multiplayer Mahjong.
/// Handles player interactions with their hand tiles, including self-drawn Kong declarations.
/// </summary>
public class NetworkedPlayerHand : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform handContainer;
    
    // Visual feedback
    [SerializeField] private Material highlightMaterial;
    [SerializeField] private Material normalMaterial;
    
    // ========== KONG BUTTON ==========
    // The Kong button lives in the scene Canvas and is assigned in the NetworkedGameManager
    // Inspector (a scene object that CAN hold scene references). This player prefab retrieves
    // it at runtime via the GameManager singleton — no serialized field needed here.
    [Header("Kong UI")]
    private GameObject kongButtonUI => NetworkedGameManager.Instance?.KongButtonUI;
    
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

    // Read PlayerIndex live from the NetworkPlayer SyncVar instead of caching it.
    // Caching causes bugs: SetPlayerIndex() is called on the server AFTER the spawn
    // message is sent to clients, so a cached value set at spawn time is always -1
    // for non-host players. Reading live is always correct.
    private int mySeatIndex => networkPlayer != null ? networkPlayer.PlayerIndex : -1;
    
    // Public properties for NetworkedClickableTile
    public bool IsInChiSelectionMode => isInChiSelectionMode;
    public bool IsSelectingKong => isSelectingKong;
    public List<int> AvailableKongValues => availableKongValues;

    void Start()
    {
        networkPlayer = GetComponent<NetworkPlayer>();

        if (tenpaiUIPanel != null)
            tenpaiUIPanel.SetActive(false);

        if (kongButtonUI != null)
            kongButtonUI.SetActive(false);
    }



    /// <summary>
    /// Called when player clicks a tile to discard it.
    /// Blocked if the player is currently in Kong selection mode.
    /// </summary>
    public void DiscardAndDrawTile(Vector3 tilePosition, GameObject tileObject)
    {
        if (!isOwned)
        {
            Debug.LogWarning("[PlayerHand] Not owned - cannot discard");
            return;
        }
        
        // Block discard while selecting a Kong tile
        if (isSelectingKong)
        {
            Debug.LogWarning("[PlayerHand] Cannot discard while selecting Kong tile. Click a highlighted tile to Kong, or cancel.");
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
        
        // Hide Kong button and clear state before discarding
        HideKongButton();
        HideTenpaiVisuals();
        ClearAllHighlights();
        
        // Send discard command to server
        NetworkedGameManager.Instance.CmdDiscardTile(tileNetId);
    }

    /// <summary>
    /// Called after a Kong replacement tile is drawn (via ClientRpc broadcast).
    /// Filters by seat index, then checks for a chained Kong opportunity.
    /// Also re-hides the button if no second Kong is available.
    /// </summary>
    public void OnKongReplacementDrawn(int seatIndex)
    {
        if (!isOwned) return;
        if (NetworkedGameManager.Instance == null) return;
        if (seatIndex != mySeatIndex) return;

        // Force-hide the button immediately — it should never be visible at this point
        // since the Kong was just executed.
        HideKongButton();
        isSelectingKong = false;
        availableKongValues.Clear();

        // Delay briefly to ensure Mirror has propagated the SyncList removals (the 4 kong
        // tiles removed from the hand) before we check for a chained Kong opportunity.
        // Without this, CheckForKongOpportunity may see the old hand with 4-of-a-kind still
        // present and incorrectly re-show the button.
        StartCoroutine(CheckForKongAfterDelay());
    }

    /// <summary>
    /// Called when a drawn tile SyncVar changes to non-zero for the given seat.
    /// This fires AFTER the tile is in the SyncVar, so CheckForKongOpportunity
    /// is guaranteed to see a complete 14-tile hand with no ordering race.
    /// </summary>
    public void OnDrawnTileArrived(int seat)
    {
        if (!isOwned) return;
        if (seat != mySeatIndex) return;
        if (NetworkedGameManager.Instance == null) return;
        // Only check on our own turn (guards against replacement tile draws mid-Kong)
        if (NetworkedGameManager.Instance.CurrentPlayerIndex != mySeatIndex) return;

        CheckForKongOpportunity();
    }

    /// <summary>
    /// Called on this client whenever currentPlayerIndex changes (via SyncVar hook).
    /// Kept as a fallback for cases where the drawn tile arrived before the turn index
    /// (e.g. round start, bot turns where no drawn tile hook fires for us).
    /// </summary>
    public void OnTurnChanged(int newPlayerIndex)
    {
        if (!isOwned) return;
        if (NetworkedGameManager.Instance == null) return;
        if (newPlayerIndex != mySeatIndex) return;

        // The drawn tile hook (OnDrawnTileArrived) is the primary trigger for mid-game turns.
        // OnTurnChanged fires immediately when currentPlayerIndex changes, which may be before
        // the drawn tile SyncVar has arrived on this client. Run the check anyway — if the
        // drawn tile hasn't arrived yet, CheckForKongOpportunity will find 13 tiles and show
        // nothing, but OnDrawnTileArrived will fire moments later and re-run the check with
        // the complete hand.
        CheckForKongOpportunity();
    }

    /// <summary>
    /// Called by the game to notify this hand that a new tile has been drawn on this client.
    /// Triggers Kong opportunity checking (only relevant on the player's own turn).
    /// </summary>
    public void OnTileDrawn()
    {
        if (!isOwned) return;
        if (NetworkedGameManager.Instance == null) return;
        // No seatIndex filtering needed — TargetRpc only fires on the correct client.
        CheckForKongOpportunity();
    }

    public void OnTileDrawnDelayed()
    {
        if (!isOwned) return;
        if (NetworkedGameManager.Instance == null) return;
        StartCoroutine(CheckForKongAfterDelay());
    }

    private IEnumerator CheckForKongAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        CheckForKongOpportunity();
    }

    // ========== KONG SELECTION MODE ==========

    /// <summary>
    /// Check if the current player has a Kong opportunity (4 identical tiles in hand,
    /// including the drawn tile). Shows or hides the Kong button accordingly.
    /// Only valid when it is this player's turn.
    /// </summary>
    public void CheckForKongOpportunity()
    {
        if (!isOwned) return;
        if (NetworkedGameManager.Instance == null) return;
        if (mySeatIndex < 0) return;
        
        // Can only declare a self-drawn Kong on your own turn
        if (NetworkedGameManager.Instance.CurrentPlayerIndex != mySeatIndex)
        {
            HideKongButton();
            return;
        }
        
        availableKongValues.Clear();
        
        // Get all tiles in our hand (hand tiles + drawn tile)
        List<TileData> allTiles = NetworkedGameManager.Instance.GetTilesForSeat(mySeatIndex);
        
        // Find tile values that appear exactly 4 times
        var groups = allTiles
            .GroupBy(t => t.GetSortValue())
            .Where(g => g.Count() == 4)
            .Select(g => g.Key)
            .ToList();
        
        availableKongValues = groups;
        
        if (availableKongValues.Count > 0)
        {
            Debug.Log($"[PlayerHand] Kong opportunity detected: {availableKongValues.Count} possible Kong(s) - values: {string.Join(", ", availableKongValues)}");
            ShowKongButton();
        }
        else
        {
            Debug.Log("[PlayerHand] No Kong opportunity found");
            HideKongButton();
        }
    }

    /// <summary>
    /// Called by the Kong button's OnClick event in the UI.
    /// - Single Kong available  → execute immediately (no tile selection needed)
    /// - Multiple Kongs available → highlight valid tiles and wait for player to click one
    /// </summary>
    public void OnKongButtonPressed()
    {
        if (!isOwned) return;
        
        // Refresh availability before acting
        CheckForKongOpportunity();
        
        if (availableKongValues.Count == 0)
        {
            Debug.LogWarning("[PlayerHand] Kong button pressed but no available Kong values");
            HideKongButton();
            return;
        }
        
        if (availableKongValues.Count == 1)
        {
            // Single Kong option – execute immediately, no tile selection needed
            Debug.Log($"[PlayerHand] Single Kong available, auto-executing Kong for value {availableKongValues[0]}");
            ExecuteKong(availableKongValues[0]);
        }
        else
        {
            // Multiple Kong options – enter tile selection mode
            Debug.Log($"[PlayerHand] Multiple Kongs available ({availableKongValues.Count}), entering selection mode");
            EnterKongSelectionMode();
        }
    }

    /// <summary>
    /// Highlight all tiles that are valid Kong targets; dim others.
    /// Sets isSelectingKong = true so tile click handlers know to call OnKongTileClicked().
    /// </summary>
    private void EnterKongSelectionMode()
    {
        isSelectingKong = true;
        HideKongButton(); // Hide button while in selection mode (player clicks a tile instead)
        HighlightKongTiles();
        Debug.Log("[PlayerHand] Kong selection mode active – click a highlighted tile to declare Kong");
    }

    /// <summary>
    /// Exit Kong selection mode without executing a Kong (e.g. player decided not to Kong).
    /// </summary>
    public void CancelKongSelection()
    {
        if (!isSelectingKong) return;
        isSelectingKong = false;
        availableKongValues.Clear();
        ClearAllHighlights();
        HideKongButton();
        Debug.Log("[PlayerHand] Kong selection cancelled");
    }

    /// <summary>
    /// Called from the tile click handler when a tile is clicked during Kong selection mode.
    /// </summary>
    public void OnKongTileClicked(int sortValue)
    {
        if (!isSelectingKong) return;
        
        if (!availableKongValues.Contains(sortValue))
        {
            Debug.Log($"[PlayerHand] Clicked tile {sortValue} is not a valid Kong target – try another tile");
            return;
        }
        
        Debug.Log($"[PlayerHand] Kong tile selected: {sortValue}, executing Kong");
        ExecuteKong(sortValue);
    }

    /// <summary>
    /// Execute a self-drawn Kong for the given tile sort value.
    /// </summary>
    public void ExecuteKong(int targetValue)
    {
        if (!isOwned) return;
        
        if (NetworkedGameManager.Instance == null)
        {
            Debug.LogError("[PlayerHand] Cannot declare Kong – GameManager not found!");
            return;
        }
        
        // Only allow Kong on this player's turn
        if (NetworkedGameManager.Instance.CurrentPlayerIndex != mySeatIndex)
        {
            Debug.LogWarning("[PlayerHand] Cannot declare Kong – not our turn");
            return;
        }
        
        Debug.Log($"[PlayerHand] Sending CmdDeclareKong for value {targetValue}");
        
        // Clean up local UI/state before the server processes
        isSelectingKong = false;
        availableKongValues.Clear();
        ClearAllHighlights();
        HideKongButton();
        HideTenpaiVisuals();
        
        // Tell the server to process the Kong.
        NetworkedGameManager.Instance.CmdDeclareKong(targetValue);
    }

    // ========== HIGHLIGHT HELPERS ==========

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
            bool isKongable = availableKongValues.Contains(sortValue);
            
            HighlightTileColored(tile, isKongable);
        }
    }

    public void HighlightMatchingTiles(int sortValue)
    {
        if (!isOwned) return;
        
        GameObject[] allTiles = GameObject.FindGameObjectsWithTag("Tile");
        
        foreach (GameObject tile in allTiles)
        {
            TileData data = tile.GetComponent<TileData>();
            if (data == null) continue;
            
            NetworkIdentity identity = tile.GetComponent<NetworkIdentity>();
            if (identity == null) continue;
            
            if (!IsTileInMyHand(identity.netId)) continue;
            
            if (data.GetSortValue() == sortValue)
                HighlightTile(tile, true);
        }
    }

    public void ClearAllHighlights()
    {
        if (!isOwned) return;
        
        GameObject[] allTiles = GameObject.FindGameObjectsWithTag("Tile");
        
        foreach (GameObject tile in allTiles)
        {
            NetworkIdentity identity = tile.GetComponent<NetworkIdentity>();
            if (identity == null) continue;
            
            if (IsTileInMyHand(identity.netId))
                HighlightTile(tile, false);
        }
    }

    private void HighlightTile(GameObject tile, bool highlight)
    {
        Renderer renderer = tile.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            if (highlight && highlightMaterial != null)
                renderer.material = highlightMaterial;
            else if (!highlight && normalMaterial != null)
                renderer.material = normalMaterial;
            else
                renderer.material.color = highlight ? Color.yellow : Color.white;
        }
    }

    private void HighlightTileColored(GameObject tile, bool selectable)
    {
        Renderer renderer = tile.GetComponentInChildren<Renderer>();
        if (renderer == null) return;
        
        renderer.material.color = selectable
            ? new Color(1f, 0.85f, 0f, 1f)
            : new Color(0.3f, 0.3f, 0.3f, 1f);
    }

    // ========== KONG BUTTON UI HELPERS ==========

    private void ShowKongButton()
    {
        if (kongButtonUI != null)
        {
            kongButtonUI.SetActive(true);
            Debug.Log("[PlayerHand] Kong button shown");
        }
        else
        {
            Debug.LogWarning("[PlayerHand] kongButtonUI is null — make sure kongButtonUI is assigned on NetworkedGameManager in the Inspector.");
        }
    }

    private void HideKongButton()
    {
        if (kongButtonUI != null)
            kongButtonUI.SetActive(false);
    }

    // ========== TILE MEMBERSHIP CHECK ==========

    private bool IsTileInMyHand(uint netId)
    {
        if (NetworkedGameManager.Instance == null) return false;
        if (mySeatIndex < 0) return false;
        
        return NetworkedGameManager.Instance.IsTileInSeatHand(mySeatIndex, netId);
    }

    // ========== TENPAI UI ==========

    public void RequestTenpaiCheck(GameObject hoveredTile)
    {
        if (!isOwned) return;
        if (isSelectingKong || isInChiSelectionMode) return;
        if (NetworkedGameManager.Instance == null) return;
        if (NetworkedGameManager.Instance.CurrentPlayerIndex != mySeatIndex) return;
        
        HideTenpaiVisuals();
    }

    public void HideTenpaiVisuals()
    {
        if (tenpaiUIPanel != null)
            tenpaiUIPanel.SetActive(false);
    }

    // ========== CHI SELECTION MODE ==========

    public void OnChiTileClicked(int tileValue)
    {
        Debug.Log($"[PlayerHand] Chi tile clicked: {tileValue}");
        isInChiSelectionMode = false;
    }

    public void OnChiTileHovered(int sortValue, Vector3 position)
    {
        Debug.Log($"[PlayerHand] Chi tile hovered: {sortValue}");
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

    // ========== PUBLIC ACCESSORS ==========

    public int GetSeatIndex()
    {
        return mySeatIndex;
    }
}