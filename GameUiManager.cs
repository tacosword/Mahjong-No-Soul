using UnityEngine;
using Mirror;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    [Header("Action Buttons")]
    public GameObject winButton;
    public GameObject kongButton;
    
    [Header("Turn Indicator")]
    public GameObject turnIndicatorText;
    
    [Header("Tenpai UI")]
    public GameObject tenpaiUIPanel;
    public Transform tenpaiIconsContainer;
    public GameObject tenpaiIconPrefab;

    private NetworkedPlayerHand localPlayerHand;

    void Start()
    {
        Debug.Log("[GameUIManager] Start() called");
        StartCoroutine(InitializeUIAfterPlayerSpawns());
    }

    private System.Collections.IEnumerator InitializeUIAfterPlayerSpawns()
    {
        float timeout = 5f;
        float elapsed = 0f;
        
        while (elapsed < timeout)
        {
            NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
            
            if (localPlayer != null)
            {
                Debug.Log("[GameUIManager] âœ“ Found local player");
                localPlayerHand = localPlayer.GetComponent<NetworkedPlayerHand>();
                
                if (localPlayerHand != null)
                {
                    Debug.Log("[GameUIManager] âœ“ Found NetworkedPlayerHand");
                    
                    // Set up Win/Kong buttons
                    localPlayerHand.winButtonUI = winButton;
                    localPlayerHand.kongButtonUI = kongButton;

                    // Set up Win button callback
                    if (winButton != null)
                    {
                        Button btn = winButton.GetComponent<Button>();
                        if (btn != null)
                        {
                            btn.onClick.RemoveAllListeners();
                            btn.onClick.AddListener(OnWinButtonClicked);
                            Debug.Log("[GameUIManager] Win button connected");
                        }
                    }

                    // Set up Kong button callback
                    if (kongButton != null)
                    {
                        Button btn = kongButton.GetComponent<Button>();
                        if (btn != null)
                        {
                            btn.onClick.RemoveAllListeners();
                            btn.onClick.AddListener(OnKongButtonClicked);
                            Debug.Log("[GameUIManager] Kong button connected");
                        }
                    }
                    
                    // Set up Tenpai UI
                    Debug.Log("[GameUIManager] Setting up Tenpai UI...");
                    localPlayerHand.tenpaiUIPanel = tenpaiUIPanel;
                    localPlayerHand.tenpaiIconsContainer = tenpaiIconsContainer;
                    localPlayerHand.tenpaiIconPrefab = tenpaiIconPrefab;
                    Debug.Log("[GameUIManager] âœ“ Tenpai UI assigned");
                    
                    // Set up Interrupt UI (FIXED: use new Unity API)
                    InterruptUIManager interruptUI = FindFirstObjectByType<InterruptUIManager>(FindObjectsInactive.Include);
                    if (interruptUI != null)
                    {
                        Debug.Log("[GameUIManager] âœ“âœ“âœ“ Found InterruptUIManager, connecting...");
                        interruptUI.Initialize(localPlayerHand);
                        localPlayerHand.interruptUI = interruptUI;
                        Debug.Log("[GameUIManager] âœ“âœ“âœ“ InterruptUI connected!");
                    }
                    else
                    {
                        Debug.LogError("[GameUIManager] âœ—âœ—âœ— InterruptUIManager NOT FOUND in scene!");
                    }

                    Debug.Log("[GameUIManager] Game UI fully connected");
                    yield break;
                }
            }
            
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }
        
        Debug.LogError("[GameUIManager] Timeout waiting for local player!");
    }

    void Update()
    {
        UpdateTurnIndicator();
    }

    private void OnWinButtonClicked()
    {
        if (localPlayerHand != null)
        {
            localPlayerHand.ShowResults();
        }
    }

    private void OnKongButtonClicked()
    {
        if (localPlayerHand != null)
        {
            localPlayerHand.StartKongSelection();
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
}