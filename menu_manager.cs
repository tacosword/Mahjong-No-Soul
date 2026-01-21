using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the main menu UI and navigation.
/// </summary>
public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    [Header("Main Menu UI")]
    [SerializeField] private Button mainMenuHostButton;
    [SerializeField] private Button mainMenuJoinButton;
    [SerializeField] private Button mainMenuProfileButton;
    [SerializeField] private Button mainMenuQuitButton;

    [Header("Menu Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private GameObject hostGamePanel;
    [SerializeField] private GameObject joinGamePanel;
    [SerializeField] private GameObject serverBrowserPanel;

    [Header("Profile UI")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TextMeshProUGUI welcomeText;
    [SerializeField] private Button confirmUsernameButton;

    [Header("Host Game UI")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button cancelHostButton;

    [Header("Join Game UI")]
    [SerializeField] private Button findGamesButton;
    [SerializeField] private Button directConnectButton;
    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private Button joinGameBackButton;
    
    [Header("Server Browser UI")]
    [SerializeField] private Button serverBrowserBackButton;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        // Don't use DontDestroyOnLoad for MenuManager - let it reload with scene
        // DontDestroyOnLoad(gameObject);
    }
    
    void OnEnable()
    {
        Debug.Log("MenuManager OnEnable() called");
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Debug.Log($"Scene loaded: {scene.name}");
        
        if (scene.name == "MainMenu")
        {
            Debug.Log("MainMenu scene loaded. Re-initializing MenuManager...");
            
            // Small delay to ensure scene is fully loaded
            StartCoroutine(ReinitializeAfterSceneLoad());
        }
    }
    
    private System.Collections.IEnumerator ReinitializeAfterSceneLoad()
    {
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("Reinitializing MenuManager...");
        
        // Re-setup buttons in case references were lost
        SetupButtons();
        
        // Show main menu
        if (PlayerProfile.Instance != null && PlayerProfile.Instance.HasValidUsername())
        {
            ShowMainMenu();
        }
        else
        {
            ShowProfileSetup();
        }
    }

    void Start()
    {
        Debug.Log("MenuManager Start() called");
        SetupButtons();
        
        // Check if player has a profile
        if (PlayerProfile.Instance.HasValidUsername())
        {
            Debug.Log($"Valid username found: {PlayerProfile.Instance.Username}. Showing main menu.");
            ShowMainMenu();
        }
        else
        {
            Debug.Log("No valid username. Showing profile setup.");
            ShowProfileSetup();
        }
    }

    private void SetupButtons()
    {
        Debug.Log("Setting up buttons...");
        
        // CRITICAL: Remove all listeners first to prevent duplicates
        if (mainMenuHostButton != null)
        {
            mainMenuHostButton.onClick.RemoveAllListeners();
            mainMenuHostButton.onClick.AddListener(ShowHostGameMenu);
        }
        
        if (mainMenuJoinButton != null)
        {
            mainMenuJoinButton.onClick.RemoveAllListeners();
            mainMenuJoinButton.onClick.AddListener(ShowJoinGameMenu);
        }
        
        if (mainMenuProfileButton != null)
        {
            mainMenuProfileButton.onClick.RemoveAllListeners();
            mainMenuProfileButton.onClick.AddListener(ShowProfileSetup);
        }
        
        if (mainMenuQuitButton != null)
        {
            mainMenuQuitButton.onClick.RemoveAllListeners();
            mainMenuQuitButton.onClick.AddListener(OnQuitGame);
        }
        
        // Host Game Panel
        if (hostButton != null)
        {
            hostButton.onClick.RemoveAllListeners();
            hostButton.onClick.AddListener(OnHostGame);
        }
        
        if (cancelHostButton != null)
        {
            cancelHostButton.onClick.RemoveAllListeners();
            cancelHostButton.onClick.AddListener(ShowMainMenu);
        }
        
        // Main Menu Buttons
        if (mainMenuHostButton != null)
        {
            mainMenuHostButton.onClick.AddListener(ShowHostGameMenu);
            Debug.Log("Main menu host button setup");
        }
        else
        {
            Debug.LogWarning("mainMenuHostButton is NULL!");
        }
        
        if (mainMenuJoinButton != null)
        {
            mainMenuJoinButton.onClick.AddListener(ShowJoinGameMenu);
            Debug.Log("Main menu join button setup");
        }
        else
        {
            Debug.LogWarning("mainMenuJoinButton is NULL!");
        }
        
        if (mainMenuProfileButton != null)
        {
            mainMenuProfileButton.onClick.AddListener(ShowProfileSetup);
            Debug.Log("Main menu profile button setup");
        }
        
        if (mainMenuQuitButton != null)
        {
            mainMenuQuitButton.onClick.AddListener(OnQuitGame);
            Debug.Log("Main menu quit button setup");
        }
        
        // Profile Panel
        if (confirmUsernameButton != null)
        {
            confirmUsernameButton.onClick.AddListener(OnConfirmUsername);
            Debug.Log("Profile confirm button setup");
        }
        else
        {
            Debug.LogWarning("confirmUsernameButton is NULL!");
        }

        // Host Game Panel
        if (hostButton != null)
        {
            hostButton.onClick.AddListener(OnHostGame);
            Debug.Log("Host button setup");
        }
        else
        {
            Debug.LogWarning("hostButton is NULL!");
        }
        
        if (cancelHostButton != null)
        {
            cancelHostButton.onClick.AddListener(ShowMainMenu);
            Debug.Log("Cancel host button setup");
        }

        // Join Game Panel
        if (findGamesButton != null)
        {
            findGamesButton.onClick.AddListener(ShowServerBrowser);
            Debug.Log("Find games button setup");
        }
        else
        {
            Debug.LogWarning("findGamesButton is NULL!");
        }
        
        if (directConnectButton != null)
        {
            directConnectButton.onClick.AddListener(OnDirectConnect);
            Debug.Log("Direct connect button setup");
        }
        else
        {
            Debug.LogWarning("directConnectButton is NULL!");
        }
        
        if (joinGameBackButton != null)
        {
            joinGameBackButton.onClick.AddListener(ShowMainMenu);
            Debug.Log("Join game back button setup");
        }
        else
        {
            Debug.LogWarning("joinGameBackButton is NULL!");
        }
        
        // Server Browser Panel
        if (serverBrowserBackButton != null)
        {
            serverBrowserBackButton.onClick.AddListener(ShowJoinGameMenu);
            Debug.Log("Server browser back button setup");
        }
        else
        {
            Debug.LogWarning("serverBrowserBackButton is NULL!");
        }
        
        Debug.Log("Button setup complete");
    }

    #region Panel Navigation

    public void ShowMainMenu()
    {
        Debug.Log("ShowMainMenu() called");
        isHosting = false;  // Reset hosting flag
        HideAllPanels();
        
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            Debug.Log("Main menu panel activated");
        }
        else
        {
            Debug.LogError("mainMenuPanel is NULL! Please assign it in the Inspector!");
        }

        // Update welcome message
        if (welcomeText != null && PlayerProfile.Instance.HasValidUsername())
        {
            welcomeText.text = $"Welcome, {PlayerProfile.Instance.Username}!";
            Debug.Log($"Welcome text updated: {welcomeText.text}");
        }
        else if (welcomeText == null)
        {
            Debug.LogWarning("welcomeText is NULL!");
        }
    }

    public void ShowProfileSetup()
    {
        HideAllPanels();
        
        if (profilePanel != null)
            profilePanel.SetActive(true);

        // Load existing username if available
        if (usernameInput != null && PlayerProfile.Instance.HasValidUsername())
        {
            usernameInput.text = PlayerProfile.Instance.Username;
        }
    }

    public void ShowHostGameMenu()
    {
        Debug.Log("ShowHostGameMenu() called");
        HideAllPanels();
        
        if (hostGamePanel != null)
        {
            hostGamePanel.SetActive(true);
            Debug.Log("Host game panel activated");
        }
        else
        {
            Debug.LogError("hostGamePanel is NULL!");
        }
    }

    public void ShowJoinGameMenu()
    {
        Debug.Log("ShowJoinGameMenu() called");
        HideAllPanels();
        
        if (joinGamePanel != null)
        {
            joinGamePanel.SetActive(true);
            Debug.Log("Join game panel activated");
        }
        else
        {
            Debug.LogError("joinGamePanel is NULL!");
        }
    }

    public void ShowServerBrowser()
    {
        Debug.Log("ShowServerBrowser() called");
        HideAllPanels();
        
        if (serverBrowserPanel != null)
        {
            serverBrowserPanel.SetActive(true);
            Debug.Log("Server browser panel activated");
        }
        else
        {
            Debug.LogError("serverBrowserPanel is NULL!");
        }

        // Refresh server list
        if (ServerBrowser.Instance != null)
        {
            ServerBrowser.Instance.StartServerDiscovery();
        }
        else
        {
            Debug.LogWarning("ServerBrowser.Instance is NULL!");
        }
    }

    private void HideAllPanels()
    {
        Debug.Log("Hiding all panels...");
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (profilePanel != null) profilePanel.SetActive(false);
        if (hostGamePanel != null) hostGamePanel.SetActive(false);
        if (joinGamePanel != null) joinGamePanel.SetActive(false);
        if (serverBrowserPanel != null) serverBrowserPanel.SetActive(false);
        Debug.Log("All panels hidden");
    }

    #endregion

    #region Button Callbacks

    private void OnConfirmUsername()
    {
        if (usernameInput == null) return;

        string username = usernameInput.text.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            Debug.LogWarning("Username cannot be empty!");
            return;
        }

        if (username.Length < 3)
        {
            Debug.LogWarning("Username must be at least 3 characters!");
            return;
        }

        if (username.Length > 20)
        {
            Debug.LogWarning("Username must be 20 characters or less!");
            return;
        }

        // Save the username
        PlayerProfile.Instance.SaveUsername(username);
        
        // Go to main menu
        ShowMainMenu();
    }

    private bool isHosting = false;  // Add this field at the top of the class

    private void OnHostGame()
    {
        Debug.Log("OnHostGame() called");
        
        // Prevent duplicate calls
        if (isHosting)
        {
            Debug.LogWarning("Already hosting! Ignoring duplicate call.");
            return;
        }
        
        if (!PlayerProfile.Instance.HasValidUsername())
        {
            Debug.LogWarning("Cannot host game without a valid username!");
            ShowProfileSetup();
            return;
        }

        isHosting = true;
        Debug.Log("Starting to host game...");
        
        // Start hosting
        if (MahjongNetworkManager.Instance != null)
        {
            MahjongNetworkManager.Instance.StartHostGame();
            Debug.Log("Host game started");
        }
        else
        {
            Debug.LogError("MahjongNetworkManager.Instance is NULL!");
            isHosting = false;  // Reset flag on error
        }
    }

    private void OnDirectConnect()
    {
        Debug.Log("OnDirectConnect() called");
        
        if (!PlayerProfile.Instance.HasValidUsername())
        {
            Debug.LogWarning("Cannot join game without a valid username!");
            ShowProfileSetup();
            return;
        }

        if (ipAddressInput == null)
        {
            Debug.LogError("ipAddressInput is NULL!");
            return;
        }

        string address = ipAddressInput.text.Trim();

        if (string.IsNullOrWhiteSpace(address))
        {
            address = "localhost";
            Debug.Log("No IP entered, using localhost");
        }

        Debug.Log($"Attempting direct connect to: {address}");

        // Join the game
        if (MahjongNetworkManager.Instance != null)
        {
            MahjongNetworkManager.Instance.JoinGame(address);
        }
        else
        {
            Debug.LogError("MahjongNetworkManager.Instance is NULL!");
        }
    }

    public void OnBackToMainMenu()
    {
        ShowMainMenu();
    }

    public void OnQuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    #endregion
}