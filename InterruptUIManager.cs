using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages the UI for Chi/Pon/Kong interrupt buttons
/// </summary>
public class InterruptUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject interruptPanel;
    public Button chiButton;
    public Button ponButton;
    public Button kongButton;
    public Button passButton; // X button
    public Button ronButton;
    
    [Header("Chi Selection UI")]
    public GameObject chiSelectionPanel;
    public Transform chiOptionsContainer;
    public GameObject chiOptionPrefab; // Shows the 3 tiles for each option
    
    [Header("Chi Preview Panel")]
    public GameObject chiPreviewPanel;
    public Transform chiPreviewContainer;
    public GameObject chiTilePrefab; // For showing tiles

    private NetworkedPlayerHand playerHand;
    private List<ChiOption> availableChiOptions = new List<ChiOption>();
    private System.Action<InterruptActionType> onInterruptDecision;
    private System.Action<ChiOption> onChiOptionSelected;
    
    private bool isChiSelectionMode = false;

    void Awake()
    {
        Debug.Log("╔════════════════════════════════════════════════════════════╗");
        Debug.Log("║  [InterruptUI] AWAKE - Component Initializing              ║");
        Debug.Log("╚════════════════════════════════════════════════════════════╝");
        Debug.Log($"[InterruptUI] GameObject: '{gameObject.name}'");
        Debug.Log($"[InterruptUI] Active in Hierarchy: {gameObject.activeInHierarchy}");
        Debug.Log($"[InterruptUI] Component Enabled: {enabled}");
    }

    void Start()
    {
        Debug.Log("╔════════════════════════════════════════════════════════════╗");
        Debug.Log("║  [InterruptUI] START - Validating References              ║");
        Debug.Log("╚════════════════════════════════════════════════════════════╝");
        
        bool allValid = true;
        
        if (interruptPanel != null)
            Debug.Log($"[InterruptUI] ✓ interruptPanel assigned: '{interruptPanel.name}'");
        else { Debug.LogError("[InterruptUI] ✗ interruptPanel is NULL!"); allValid = false; }
        
        if (chiButton != null)
            Debug.Log($"[InterruptUI] ✓ chiButton assigned");
        else { Debug.LogWarning("[InterruptUI] ⚠ chiButton is NULL"); allValid = false; }
        
        if (ponButton != null)
            Debug.Log($"[InterruptUI] ✓ ponButton assigned");
        else { Debug.LogWarning("[InterruptUI] ⚠ ponButton is NULL"); allValid = false; }
        
        if (kongButton != null)
            Debug.Log($"[InterruptUI] ✓ kongButton assigned");
        else { Debug.LogWarning("[InterruptUI] ⚠ kongButton is NULL"); allValid = false; }
        
        if (ronButton != null)
            Debug.Log($"[InterruptUI] ✓ ronButton assigned");
        else { Debug.LogWarning("[InterruptUI] ⚠ ronButton is NULL"); allValid = false; }
        
        if (passButton != null)
            Debug.Log($"[InterruptUI] ✓ passButton assigned");
        else { Debug.LogWarning("[InterruptUI] ⚠ passButton is NULL"); allValid = false; }
        
        if (allValid)
            Debug.Log("[InterruptUI] ✓✓✓ ALL REFERENCES VALID ✓✓✓");
        else
            Debug.LogError("[InterruptUI] ✗✗✗ MISSING REFERENCES - CHECK INSPECTOR ✗✗✗");
        
        Debug.Log("╔════════════════════════════════════════════════════════════╗");
        Debug.Log("║  [InterruptUI] START COMPLETE                              ║");
        Debug.Log("╚════════════════════════════════════════════════════════════╝");
    }

    public void Initialize(NetworkedPlayerHand hand)
    {
        Debug.Log("╔════════════════════════════════════════════════════════════╗");
        Debug.Log("║  [InterruptUI] INITIALIZE Called                           ║");
        Debug.Log("╚════════════════════════════════════════════════════════════╝");
        
        playerHand = hand;
        Debug.Log($"[InterruptUI] PlayerHand assigned: {playerHand != null}");
        
        // Setup button listeners
        if (chiButton != null)
        {
            chiButton.onClick.AddListener(OnChiButtonClicked);
            Debug.Log("[InterruptUI] ✓ Chi button listener added");
        }
        
        if (ponButton != null)
        {
            ponButton.onClick.AddListener(OnPonButtonClicked);
            Debug.Log("[InterruptUI] ✓ Pon button listener added");
        }
        
        if (kongButton != null)
        {
            kongButton.onClick.AddListener(OnKongButtonClicked);
            Debug.Log("[InterruptUI] ✓ Kong button listener added");
        }
        
        if (ronButton != null)
        {
            ronButton.onClick.AddListener(OnRonButtonClicked);
            Debug.Log("[InterruptUI] ✓ Ron button listener added");
        }
        
        if (passButton != null)
        {
            passButton.onClick.AddListener(OnPassButtonClicked);
            Debug.Log("[InterruptUI] ✓ Pass button listener added");
        }
        
        HideAll();
        
        Debug.Log("╔════════════════════════════════════════════════════════════╗");
        Debug.Log("║  [InterruptUI] INITIALIZE COMPLETE                         ║");
        Debug.Log("╚════════════════════════════════════════════════════════════╝");
    }

    /// <summary>
    /// Show interrupt options to the player
    /// </summary>
    public void ShowInterruptOptions(
        bool canChi, 
        bool canPon, 
        bool canKong,
        bool canRon,  // NEW PARAMETER
        List<ChiOption> chiOptions,
        System.Action<InterruptActionType> onDecision)
    {
        Debug.Log("╔════════════════════════════════════════════════════════════╗");
        Debug.Log("║  [InterruptUI] SHOWING INTERRUPT OPTIONS                   ║");
        Debug.Log("╚════════════════════════════════════════════════════════════╝");
        Debug.Log($"[InterruptUI] Can Chi: {canChi} | Can Pon: {canPon} | Can Kong: {canKong} | Can Ron: {canRon}");
        Debug.Log($"[InterruptUI] Chi Options Count: {chiOptions?.Count ?? 0}");
        
        availableChiOptions = chiOptions;
        onInterruptDecision = onDecision;
        
        if (interruptPanel != null)
        {
            interruptPanel.SetActive(true);
            Debug.Log("[InterruptUI] ✓ Interrupt panel activated");
            Debug.Log($"[InterruptUI]     Panel active in hierarchy: {interruptPanel.activeInHierarchy}");
        }
        else
        {
            Debug.LogError("[InterruptUI] ✗✗✗ CRITICAL: interruptPanel is NULL! Cannot show options!");
            return;
        }
        
        // Enable/disable buttons
        if (chiButton != null)
        {
            chiButton.gameObject.SetActive(canChi);
            Debug.Log($"[InterruptUI] Chi button set to: {canChi}");
        }
        
        if (ponButton != null)
        {
            ponButton.gameObject.SetActive(canPon);
            Debug.Log($"[InterruptUI] Pon button set to: {canPon}");
        }
        
        if (kongButton != null)
        {
            kongButton.gameObject.SetActive(canKong);
            Debug.Log($"[InterruptUI] Kong button set to: {canKong}");
        }
        
        if (ronButton != null)
        {
            ronButton.gameObject.SetActive(canRon);
            Debug.Log($"[InterruptUI] Ron button set to: {canRon}");
        }
        
        if (passButton != null)
        {
            passButton.gameObject.SetActive(true);
            Debug.Log("[InterruptUI] Pass button set to: true (always visible)");
        }
        
        Debug.Log("╔════════════════════════════════════════════════════════════╗");
        Debug.Log("║  [InterruptUI] SHOW COMPLETE - UI SHOULD BE VISIBLE        ║");
        Debug.Log("╚════════════════════════════════════════════════════════════╝");
    }

    /// <summary>
    /// Hide all interrupt UI
    /// </summary>
    public void HideAll()
    {
        if (interruptPanel != null)
            interruptPanel.SetActive(false);
        
        if (chiSelectionPanel != null)
            chiSelectionPanel.SetActive(false);
        
        if (chiPreviewPanel != null)
            chiPreviewPanel.SetActive(false);
        
        isChiSelectionMode = false;
    }

    // === BUTTON CALLBACKS ===

    private void OnChiButtonClicked()
    {
        Debug.Log($"==========================================");
        Debug.Log($"[InterruptUI] Chi button clicked - {availableChiOptions.Count} options");
        
        if (availableChiOptions.Count == 1)
        {
            // Only one option - execute immediately
            ChiOption singleOption = availableChiOptions[0];
            
            Debug.Log($"[InterruptUI] Single Chi option: " +
                    $"{singleOption.tile1SortValue}, " +
                    $"{singleOption.tile2SortValue}, " +
                    $"{singleOption.discardedTile}");
            
            if (playerHand != null)
            {
                // Store and send
                playerHand.SetSelectedChiOption(singleOption);
                onInterruptDecision?.Invoke(InterruptActionType.Chi);
            }
            else
            {
                Debug.LogError($"[InterruptUI] playerHand is NULL!");
            }
            
            HideAll();
        }
        else if (availableChiOptions.Count > 1)
        {
            // Multiple options - enter selection mode
            Debug.Log($"[InterruptUI] Multiple options - entering selection mode");
            EnterChiSelectionMode();
        }
        else
        {
            Debug.LogWarning($"[InterruptUI] No Chi options available!");
        }
        
        Debug.Log($"==========================================");
    }
    private void OnPonButtonClicked()
    {
        Debug.Log("[InterruptUI] Pon clicked");
        onInterruptDecision?.Invoke(InterruptActionType.Pon);
        HideAll();
    }

    private void OnKongButtonClicked()
    {
        Debug.Log("[InterruptUI] Kong clicked");
        onInterruptDecision?.Invoke(InterruptActionType.Kong);
        HideAll();
    }

    private void OnRonButtonClicked()
    {
        Debug.Log("[InterruptUI] Ron clicked - declaring win on opponent's discard!");
        onInterruptDecision?.Invoke(InterruptActionType.Ron);
        HideAll();
    }

    private void OnPassButtonClicked()
    {
        Debug.Log("[InterruptUI] Pass clicked");
        onInterruptDecision?.Invoke(InterruptActionType.None);
        HideAll();
    }

    // === CHI SELECTION MODE ===

    /// <summary>
    /// Enter chi selection mode - player must choose which chi option to use
    /// </summary>
    public void EnterChiSelectionMode()
    {
        Debug.Log("[InterruptUI] Entering Chi selection mode");
        
        isChiSelectionMode = true;
        
        // Hide main interrupt panel
        if (interruptPanel != null)
            interruptPanel.SetActive(false);
        
        // Show chi selection panel
        if (chiSelectionPanel != null)
            chiSelectionPanel.SetActive(true);
        
        // Tell player hand to highlight Chi tiles
        if (playerHand != null)
        {
            // NetworkedPlayerHand will call ShowChiPreview directly,
            // so we don't need a hover callback
            playerHand.EnterChiSelectionMode(
                availableChiOptions, 
                null,  // No hover callback needed
                OnChiOptionConfirmed
            );
        }
    }

    // REMOVED: This callback was passing Vector3.zero which prevented the panel from following tiles
    // NetworkedPlayerHand now calls ShowChiPreview directly with the actual tile position

    /// <summary>
    /// Called when player clicks a Chi tile to confirm
    /// </summary>
    private void OnChiOptionConfirmed(ChiOption option)
    {
        Debug.Log($"==========================================");
        Debug.Log($"[InterruptUI] OnChiOptionConfirmed CALLED!");
        Debug.Log($"[InterruptUI] Chi option: {option.tile1SortValue}, {option.tile2SortValue}, {option.discardedTile}");
        
        // CRITICAL: Store the selected option in playerHand
        if (playerHand != null)
        {
            playerHand.SetSelectedChiOption(option);
            Debug.Log($"[InterruptUI] Stored Chi option in playerHand");
        }
        
        // Now invoke the decision callback
        Debug.Log($"[InterruptUI] Invoking onInterruptDecision with Chi");
        onInterruptDecision?.Invoke(InterruptActionType.Chi);
        
        Debug.Log($"[InterruptUI] Hiding UI");
        HideAll();
        Debug.Log($"==========================================");
    }

    /// <summary>
    /// Show preview of a Chi option at the specified world position
    /// </summary>
    /// <param name="option">The Chi option to preview</param>
    /// <param name="worldPosition">World position of the hovered tile</param>
    public void ShowChiPreview(ChiOption option, Vector3 worldPosition)
    {
        Debug.Log($"[InterruptUI] ShowChiPreview at world position: {worldPosition}");
        
        if (option == null || chiPreviewPanel == null || 
            chiPreviewContainer == null || chiTilePrefab == null)
        {
            Debug.LogError($"[InterruptUI] Missing references!");
            return;
        }
        
        // Clear existing preview
        foreach (Transform child in chiPreviewContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Create preview icons
        List<int> sequence = option.GetSequenceSorted();
        foreach (int sortValue in sequence)
        {
            GameObject tilePrefab = FindTilePrefab(sortValue);
            if (tilePrefab == null) continue;
            
            TileData tileData = tilePrefab.GetComponent<TileData>();
            if (tileData == null || tileData.tileSprite == null) continue;
            
            GameObject icon = Instantiate(chiTilePrefab, chiPreviewContainer);
            Image img = icon.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = tileData.tileSprite;
            }
        }
        
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // FIX: POSITION THE PANEL CORRECTLY (Both X and Y)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        
        // Convert world position to screen position
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
        Debug.Log($"[InterruptUI] World position: {worldPosition}");
        Debug.Log($"[InterruptUI] Screen position: {screenPos}");
        
        // Get the RectTransform
        RectTransform panelRect = chiPreviewPanel.GetComponent<RectTransform>();
        if (panelRect == null)
        {
            Debug.LogError($"[InterruptUI] ChiPreviewPanel has no RectTransform!");
            chiPreviewPanel.SetActive(true);
            return;
        }
        
        // CRITICAL FIX: Set position directly (not anchoredPosition)
        // This ensures both X and Y are updated
        panelRect.position = screenPos;
        
        Debug.Log($"[InterruptUI] Panel position set to: {panelRect.position}");
        
        // Now offset upward using anchoredPosition
        // This adds to the current position without affecting X
        Vector2 currentAnchored = panelRect.anchoredPosition;
        panelRect.anchoredPosition = new Vector2(currentAnchored.x, currentAnchored.y + 100f);
        
        Debug.Log($"[InterruptUI] Panel anchored position: {panelRect.anchoredPosition}");
        Debug.Log($"[InterruptUI] Final panel position: {panelRect.position}");
        
        // Optional: Keep panel on screen
        KeepPanelOnScreen(panelRect);
        
        // Activate panel
        chiPreviewPanel.SetActive(true);
    }

    /// <summary>
    /// Keep the panel on screen by clamping position to screen bounds
    /// </summary>
    private void KeepPanelOnScreen(RectTransform panelRect)
    {
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        
        float panelWidth = panelRect.rect.width;
        float panelHeight = panelRect.rect.height;
        
        Vector3 pos = panelRect.position;
        
        Debug.Log($"[InterruptUI] KeepPanelOnScreen - Before: {pos}");
        Debug.Log($"[InterruptUI] Screen size: {screenWidth}x{screenHeight}");
        Debug.Log($"[InterruptUI] Panel size: {panelWidth}x{panelHeight}");
        
        // Calculate safe margins
        float leftMargin = 10f;
        float rightMargin = screenWidth - 10f;
        float topMargin = screenHeight - 10f;
        float bottomMargin = 10f;
        
        // Only clamp if panel is going off screen
        // Allow panel to move freely as long as it's mostly visible
        
        // Clamp X (only if too far left or right)
        if (pos.x < leftMargin)
        {
            pos.x = leftMargin;
            Debug.Log($"[InterruptUI] Clamped LEFT to {pos.x}");
        }
        else if (pos.x > rightMargin)
        {
            pos.x = rightMargin;
            Debug.Log($"[InterruptUI] Clamped RIGHT to {pos.x}");
        }
        
        // Clamp Y (only if too far up or down)
        if (pos.y < bottomMargin)
        {
            pos.y = bottomMargin;
            Debug.Log($"[InterruptUI] Clamped BOTTOM to {pos.y}");
        }
        else if (pos.y > topMargin)
        {
            pos.y = topMargin;
            Debug.Log($"[InterruptUI] Clamped TOP to {pos.y}");
        }
        
        panelRect.position = pos;
        
        Debug.Log($"[InterruptUI] KeepPanelOnScreen - After: {pos}");
    }

    /// <summary>
    /// Hide Chi preview
    /// </summary>
    public void HideChiPreview()
    {
        Debug.Log($"[InterruptUI] HideChiPreview called");
        
        if (chiPreviewPanel != null)
        {
            chiPreviewPanel.SetActive(false);
            Debug.Log($"[InterruptUI] Preview panel hidden");
        }
    }

    // === HELPER METHODS ===

    /// <summary>
    /// Find a tile prefab by sort value from the game manager
    /// </summary>
    private GameObject FindTilePrefab(int sortValue)
    {
        if (NetworkedGameManager.Instance == null)
        {
            Debug.LogError($"[InterruptUI] NetworkedGameManager.Instance is NULL!");
            return null;
        }
        
        if (NetworkedGameManager.Instance.TilePrefabs == null)
        {
            Debug.LogError($"[InterruptUI] TilePrefabs array is NULL!");
            return null;
        }
        
        foreach (GameObject prefab in NetworkedGameManager.Instance.TilePrefabs)
        {
            if (prefab == null) continue;
            
            TileData data = prefab.GetComponent<TileData>();
            if (data != null && data.GetSortValue() == sortValue)
            {
                return prefab;
            }
        }
        
        Debug.LogError($"[InterruptUI] No prefab found for sort value {sortValue}");
        return null;
    }
}