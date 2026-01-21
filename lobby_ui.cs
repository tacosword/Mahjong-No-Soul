using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Mirror;

/// <summary>
/// Manages the lobby UI where players wait before the game starts.
/// </summary>
public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerEntryPrefab;
    [SerializeField] private TextMeshProUGUI lobbyTitleText;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private TextMeshProUGUI readyButtonText;

    private List<GameObject> playerEntries = new List<GameObject>();
    private bool isReady = false;

    void Awake()
    {
        // Don't use singleton pattern - let each scene have its own instance
        Instance = this;
        Debug.Log("LobbyUI Awake() called");
    }
    
    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        ClearPlayerList();
    }

    void Start()
    {
        SetupButtons();
        
        if (lobbyPanel != null)
            lobbyPanel.SetActive(true);

        UpdateLobbyTitle();
    }

    private void SetupButtons()
    {
        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyToggle);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnLeaveLobby);
    }

    /// <summary>
    /// Update the lobby title based on whether we're hosting or joining.
    /// </summary>
    private void UpdateLobbyTitle()
    {
        if (lobbyTitleText == null) return;

        if (NetworkServer.active)
        {
            lobbyTitleText.text = "Lobby (Hosting)";
        }
        else
        {
            lobbyTitleText.text = "Lobby";
        }
    }

    /// <summary>
    /// Refresh the player list display.
    /// </summary>
    public void RefreshPlayerList(List<NetworkPlayer> players)
    {
        Debug.Log($"RefreshPlayerList() called with {players?.Count ?? 0} players");
        
        // Clear existing entries
        ClearPlayerList();

        if (players == null || players.Count == 0)
        {
            Debug.Log("No players to display");
            return;
        }

        // Create an entry for each player
        for (int i = 0; i < players.Count; i++)
        {
            Debug.Log($"Creating entry for player {i}: {players[i].Username}");
            CreatePlayerEntry(players[i], i);
        }
        
        Debug.Log($"Player list refreshed. Displaying {playerEntries.Count} entries.");
    }

    /// <summary>
    /// Create a UI entry for a player in the lobby.
    /// </summary>
    private void CreatePlayerEntry(NetworkPlayer player, int seatIndex)
    {
        if (playerEntryPrefab == null || playerListContainer == null)
        {
            Debug.LogError("LobbyUI: Missing prefab or container reference!");
            return;
        }

        GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);
        playerEntries.Add(entry);

        // Set seat number
        TextMeshProUGUI seatText = entry.transform.Find("SeatText")?.GetComponent<TextMeshProUGUI>();
        if (seatText != null)
        {
            seatText.text = $"Seat {seatIndex + 1}";
        }

        // Set player name
        TextMeshProUGUI nameText = entry.transform.Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = player.Username;
        }

        // Set ready status
        TextMeshProUGUI statusText = entry.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
        if (statusText != null)
        {
            if (player.IsReady)
            {
                statusText.text = "Ready";
                statusText.color = Color.green;
            }
            else
            {
                statusText.text = "Not Ready";
                statusText.color = Color.yellow;
            }
        }

        // Highlight local player
        if (player.isOwned)
        {
            Image background = entry.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.3f, 0.5f, 1f, 0.3f); // Light blue tint
            }
        }
    }

    /// <summary>
    /// Clear all player entries from the list.
    /// </summary>
    private void ClearPlayerList()
    {
        foreach (GameObject entry in playerEntries)
        {
            Destroy(entry);
        }
        playerEntries.Clear();
    }

    /// <summary>
    /// Hide the lobby UI (called when game starts).
    /// </summary>
    public void HideLobby()
    {
        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);
    }

    /// <summary>
    /// Show the lobby UI.
    /// </summary>
    public void ShowLobby()
    {
        if (lobbyPanel != null)
            lobbyPanel.SetActive(true);
    }

    #region Button Callbacks

    private void OnReadyToggle()
    {
        isReady = !isReady;

        // Update button text
        if (readyButtonText != null)
        {
            readyButtonText.text = isReady ? "Not Ready" : "Ready";
        }

        // Send ready status to server
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        if (localPlayer != null)
        {
            localPlayer.CmdSetReady(isReady);
        }

        Debug.Log($"Ready status changed to: {isReady}");
    }

    private void OnLeaveLobby()
    {
        Debug.Log("OnLeaveLobby() called");

        if (MahjongNetworkManager.Instance != null)
        {
            Debug.Log("Calling LeaveGame()...");
            MahjongNetworkManager.Instance.LeaveGame();
        }
        else
        {
            Debug.LogError("MahjongNetworkManager.Instance is NULL!");
        }

        // Return to main menu
        if (MenuManager.Instance != null)
        {
            Debug.Log("Showing main menu...");
            MenuManager.Instance.ShowMainMenu();
        }
        else
        {
            Debug.LogError("MenuManager.Instance is NULL!");
        }
    }

    #endregion
}