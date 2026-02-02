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
        // Find local player and assign UI
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        if (localPlayer != null)
        {
            localPlayerHand = localPlayer.GetComponent<NetworkedPlayerHand>();
            if (localPlayerHand != null)
            {
                localPlayerHand.winButtonUI = winButton;
                localPlayerHand.kongButtonUI = kongButton;

                // Set up Win button callback
                if (winButton != null)
                {
                    Button btn = winButton.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.AddListener(OnWinButtonClicked);
                    }
                }

                // Set up Kong button callback
                if (kongButton != null)
                {
                    Button btn = kongButton.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.AddListener(OnKongButtonClicked);
                    }
                }

                Debug.Log("Game UI connected to local player hand");
            }
        }
        else
        {
            Debug.LogWarning("Local player not found - UI callbacks not set up");
        }
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
            // Start Kong selection mode
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
}