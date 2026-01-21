using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Mirror;

/// <summary>
/// UI for displaying discovered servers in a scrollable list.
/// </summary>
public class ServerBrowserUI : MonoBehaviour
{
    public static ServerBrowserUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Transform serverListContainer;
    [SerializeField] private GameObject serverEntryPrefab;
    [SerializeField] private TextMeshProUGUI noServersText;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button backButton;

    private List<GameObject> serverEntries = new List<GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }

    void Start()
    {
        SetupButtons();
        ShowNoServersMessage();
    }

    private void SetupButtons()
    {
        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnRefresh);

        if (backButton != null)
            backButton.onClick.AddListener(OnBack);
    }

    /// <summary>
    /// Update the displayed server list.
    /// </summary>
    public void UpdateServerList(List<ServerResponse> servers)
    {
        // Clear existing entries
        ClearServerList();

        if (servers == null || servers.Count == 0)
        {
            ShowNoServersMessage();
            return;
        }

        HideNoServersMessage();

        // Create an entry for each server
        foreach (ServerResponse server in servers)
        {
            CreateServerEntry(server);
        }
    }

    /// <summary>
    /// Create a UI entry for a discovered server.
    /// </summary>
    private void CreateServerEntry(ServerResponse server)
    {
        if (serverEntryPrefab == null || serverListContainer == null)
        {
            Debug.LogError("ServerBrowserUI: Missing prefab or container reference!");
            return;
        }

        GameObject entry = Instantiate(serverEntryPrefab, serverListContainer);
        serverEntries.Add(entry);

        // Set server info text
        TextMeshProUGUI hostNameText = entry.transform.Find("HostNameText")?.GetComponent<TextMeshProUGUI>();
        if (hostNameText != null)
        {
            hostNameText.text = server.hostUsername;
        }

        TextMeshProUGUI playerCountText = entry.transform.Find("PlayerCountText")?.GetComponent<TextMeshProUGUI>();
        if (playerCountText != null)
        {
            playerCountText.text = $"{server.currentPlayers}/{server.maxPlayers}";
        }

        // Setup join button
        Button joinButton = entry.transform.Find("JoinButton")?.GetComponent<Button>();
        if (joinButton != null)
        {
            // Check if server is full
            bool isFull = server.currentPlayers >= server.maxPlayers;
            joinButton.interactable = !isFull;

            if (isFull)
            {
                TextMeshProUGUI buttonText = joinButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = "Full";
                }
            }
            else
            {
                joinButton.onClick.AddListener(() => OnJoinServer(server));
            }
        }
    }

    /// <summary>
    /// Clear all server entries from the list.
    /// </summary>
    private void ClearServerList()
    {
        foreach (GameObject entry in serverEntries)
        {
            Destroy(entry);
        }
        serverEntries.Clear();
    }

    /// <summary>
    /// Show the "no servers found" message.
    /// </summary>
    private void ShowNoServersMessage()
    {
        if (noServersText != null)
            noServersText.gameObject.SetActive(true);
    }

    /// <summary>
    /// Hide the "no servers found" message.
    /// </summary>
    private void HideNoServersMessage()
    {
        if (noServersText != null)
            noServersText.gameObject.SetActive(false);
    }

    #region Button Callbacks

    private void OnRefresh()
    {
        Debug.Log("Refreshing server list...");
        
        ClearServerList();
        ShowNoServersMessage();

        if (ServerBrowser.Instance != null)
        {
            ServerBrowser.Instance.ClearDiscoveredServers();
            ServerBrowser.Instance.StartServerDiscovery();
        }
    }

    private void OnJoinServer(ServerResponse server)
    {
        Debug.Log($"Joining {server.hostUsername}'s game...");

        if (ServerBrowser.Instance != null)
        {
            ServerBrowser.Instance.ConnectToServer(server);
        }
    }

    private void OnBack()
    {
        if (ServerBrowser.Instance != null)
        {
            ServerBrowser.Instance.StopServerDiscovery();
        }

        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.ShowJoinGameMenu();
        }
    }

    #endregion

    void OnDestroy()
    {
        ClearServerList();
    }
}