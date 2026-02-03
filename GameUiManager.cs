using UnityEngine;
using Mirror;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    public GameObject winButton;
    public GameObject kongButton;
    public GameObject turnIndicatorText;
    public GameObject roundWindText;

    private NetworkedPlayerHand localPlayerHand;
    private static GameUIManager _instance;

    void Start()
    {
        _instance = this;
        Debug.Log("╔════════════════════════════════════════════════════════════╗");
        Debug.Log("║  [GameUIManager] START - SCENE DIAGNOSTIC CHECK           ║");
        Debug.Log("╚════════════════════════════════════════════════════════════╝");
        
        // STEP 1: Check for InterruptUIManager in scene
        Debug.Log("[GameUIManager] STEP 1: Searching for InterruptUIManager...");
        InterruptUIManager[] allInterruptManagers = FindObjectsOfType<InterruptUIManager>();
        Debug.Log($"[GameUIManager] Found {allInterruptManagers.Length} InterruptUIManager(s) in scene");
        
        if (allInterruptManagers.Length > 0)
        {
            for (int i = 0; i < allInterruptManagers.Length; i++)
            {
                InterruptUIManager mgr = allInterruptManagers[i];
                Debug.Log($"[GameUIManager] [{i}] InterruptUIManager found:");
                Debug.Log($"[GameUIManager]     GameObject: '{mgr.gameObject.name}'");
                Debug.Log($"[GameUIManager]     Path: {GetGameObjectPath(mgr.gameObject)}");
                Debug.Log($"[GameUIManager]     Active in Hierarchy: {mgr.gameObject.activeInHierarchy}");
                Debug.Log($"[GameUIManager]     Active Self: {mgr.gameObject.activeSelf}");
                Debug.Log($"[GameUIManager]     Component Enabled: {mgr.enabled}");
                Debug.Log($"[GameUIManager]     Has interruptPanel: {mgr.interruptPanel != null}");
                
                if (mgr.interruptPanel != null)
                {
                    Debug.Log($"[GameUIManager]       Panel Name: '{mgr.interruptPanel.name}'");
                    Debug.Log($"[GameUIManager]       Panel Active: {mgr.interruptPanel.activeInHierarchy}");
                }
                
                Debug.Log($"[GameUIManager]     Has chiButton: {mgr.chiButton != null}");
                Debug.Log($"[GameUIManager]     Has ponButton: {mgr.ponButton != null}");
                Debug.Log($"[GameUIManager]     Has kongButton: {mgr.kongButton != null}");
                Debug.Log($"[GameUIManager]     Has ronButton: {mgr.ronButton != null}");
                Debug.Log($"[GameUIManager]     Has passButton: {mgr.passButton != null}");
            }
            Debug.Log("[GameUIManager] ✓ InterruptUIManager component(s) found!");
        }
        else
        {
            Debug.LogError("╔════════════════════════════════════════════════════════════╗");
            Debug.LogError("║         ✗✗✗ CRITICAL ERROR ✗✗✗                            ║");
            Debug.LogError("║  InterruptUIManager NOT FOUND in Game scene!              ║");
            Debug.LogError("╚════════════════════════════════════════════════════════════╝");
            Debug.LogError("");
            Debug.LogError("TO FIX THIS PROBLEM:");
            Debug.LogError("1. Open your 'Game' scene in Unity");
            Debug.LogError("2. In the Hierarchy, locate or create a Canvas");
            Debug.LogError("3. Right-click Canvas → Create Empty");
            Debug.LogError("4. Name it 'InterruptPanel'");
            Debug.LogError("5. Select 'InterruptPanel' in Hierarchy");
            Debug.LogError("6. In Inspector, click 'Add Component'");
            Debug.LogError("7. Type 'InterruptUIManager' and add it");
            Debug.LogError("8. Create child buttons (ChiButton, PonButton, etc.)");
            Debug.LogError("9. Drag references to the Inspector fields");
            Debug.LogError("10. SAVE THE SCENE (Ctrl+S or File → Save)");
            Debug.LogError("");
            Debug.LogError("Without this, Chi/Pon/Kong options won't appear!");
            Debug.LogError("════════════════════════════════════════════════════════════");
        }
        
        // STEP 2: Check for local player
        Debug.Log("");
        Debug.Log("[GameUIManager] STEP 2: Searching for local player...");
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        
        if (localPlayer != null)
        {
            Debug.Log($"[GameUIManager] ✓ Found local player: '{localPlayer.Username}'");
            Debug.Log($"[GameUIManager]     PlayerIndex (Seat): {localPlayer.PlayerIndex}");
            Debug.Log($"[GameUIManager]     IsOwned: {localPlayer.isOwned}");
            Debug.Log($"[GameUIManager]     GameObject: '{localPlayer.gameObject.name}'");
            
            // STEP 3: Check for NetworkedPlayerHand component
            Debug.Log("");
            Debug.Log("[GameUIManager] STEP 3: Checking for NetworkedPlayerHand...");
            localPlayerHand = localPlayer.GetComponent<NetworkedPlayerHand>();
            
            if (localPlayerHand != null)
            {
                Debug.Log("[GameUIManager] ✓ Found NetworkedPlayerHand component");
                Debug.Log($"[GameUIManager]     Component enabled: {localPlayerHand.enabled}");
                
                localPlayerHand.winButtonUI = winButton;
                localPlayerHand.kongButtonUI = kongButton;

                // Set up Win button callback
                if (winButton != null)
                {
                    Button btn = winButton.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.AddListener(OnWinButtonClicked);
                        Debug.Log("[GameUIManager] ✓ Win button callback registered");
                    }
                    else
                    {
                        Debug.LogWarning("[GameUIManager] ⚠ Win button has no Button component!");
                    }
                }
                else
                {
                    Debug.LogWarning("[GameUIManager] ⚠ winButton is null!");
                }

                // Set up Kong button callback
                if (kongButton != null)
                {
                    Button btn = kongButton.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.AddListener(OnKongButtonClicked);
                        Debug.Log("[GameUIManager] ✓ Kong button callback registered");
                    }
                    else
                    {
                        Debug.LogWarning("[GameUIManager] ⚠ Kong button has no Button component!");
                    }
                }
                else
                {
                    Debug.LogWarning("[GameUIManager] ⚠ kongButton is null!");
                }

                Debug.Log("[GameUIManager] ✓ Game UI fully connected to local player hand");
            }
            else
            {
                Debug.LogError("[GameUIManager] ✗ NetworkedPlayerHand component NOT FOUND!");
                Debug.LogError("[GameUIManager]     This component should be added automatically");
                Debug.LogError("[GameUIManager]     Check if player prefab has NetworkedPlayerHand");
            }
        }
        else
        {
            Debug.LogWarning("[GameUIManager] ⚠ Local player not found yet");
            Debug.LogWarning("[GameUIManager]     This is normal if called before player spawn");
            Debug.LogWarning("[GameUIManager]     Player will be available after network spawn");
        }
        
        Debug.Log("");
        Debug.Log("╔════════════════════════════════════════════════════════════╗");
        Debug.Log("║  [GameUIManager] START COMPLETE                            ║");
        Debug.Log("╚════════════════════════════════════════════════════════════╝");
    }

    void Update()
    {
        UpdateTurnIndicator();
    }

    private void OnWinButtonClicked()
    {
        Debug.Log("[GameUIManager] Win button clicked!");
        if (localPlayerHand != null)
        {
            localPlayerHand.ShowResults();
        }
        else
        {
            Debug.LogError("[GameUIManager] Cannot show results - localPlayerHand is null!");
        }
    }

    private void OnKongButtonClicked()
    {
        Debug.Log("[GameUIManager] Kong button clicked!");
        if (localPlayerHand != null)
        {
            localPlayerHand.StartKongSelection();
        }
        else
        {
            Debug.LogError("[GameUIManager] Cannot start Kong - localPlayerHand is null!");
        }
    }

    private void UpdateTurnIndicator()
    {
        if (turnIndicatorText == null) return;
        if (NetworkedGameManager.Instance == null) return;

        int currentTurn = NetworkedGameManager.Instance.CurrentPlayerIndex;
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        
        if (localPlayer != null)
        {
            int localSeat = localPlayer.PlayerIndex;
            TMPro.TextMeshProUGUI text = turnIndicatorText.GetComponent<TMPro.TextMeshProUGUI>();
            
            if (text != null)
            {
                if (currentTurn == localSeat)
                {
                    text.text = "YOUR TURN - Draw and Discard";
                    text.color = Color.green;
                }
                else
                {
                    text.text = $"Player {currentTurn}'s Turn";
                    text.color = Color.white;
                }
            }
        }
    }

    /// <summary>
    /// Called by NetworkedGameManager.RpcBroadcastRoundWind to update the round wind label.
    /// </summary>
    public static void UpdateRoundWindText(int roundWindValue)
    {
        if (_instance == null || _instance.roundWindText == null) return;

        string windName = roundWindValue switch
        {
            401 => "East",
            402 => "South",
            403 => "West",
            404 => "North",
            _ => "?"
        };

        TMPro.TextMeshProUGUI text = _instance.roundWindText.GetComponent<TMPro.TextMeshProUGUI>();
        if (text != null)
        {
            text.text = $"Round Wind: {windName}";
        }
    }
    
    /// <summary>
    /// Get the full hierarchy path to a GameObject
    /// </summary>
    private string GetGameObjectPath(GameObject obj)
    {
        if (obj == null) return "null";
        
        string path = obj.name;
        Transform current = obj.transform.parent;
        
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        
        return path;
    }
}