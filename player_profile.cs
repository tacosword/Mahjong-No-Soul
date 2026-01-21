using UnityEngine;

/// <summary>
/// Stores and persists player profile data locally.
/// </summary>
public class PlayerProfile : MonoBehaviour
{
    private static PlayerProfile _instance;
    public static PlayerProfile Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("PlayerProfile");
                _instance = go.AddComponent<PlayerProfile>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private const string USERNAME_KEY = "PlayerUsername";
    
    public string Username { get; private set; }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadProfile();
    }

    /// <summary>
    /// Load the player's username from PlayerPrefs.
    /// </summary>
    private void LoadProfile()
    {
        Username = PlayerPrefs.GetString(USERNAME_KEY, "");
        Debug.Log($"Profile loaded. Username: {Username}");
    }

    /// <summary>
    /// Save the player's username to PlayerPrefs.
    /// </summary>
    public void SaveUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            Debug.LogWarning("Cannot save empty username.");
            return;
        }

        Username = username.Trim();
        PlayerPrefs.SetString(USERNAME_KEY, Username);
        PlayerPrefs.Save();
        Debug.Log($"Username saved: {Username}");
    }

    /// <summary>
    /// Check if the player has a valid username.
    /// </summary>
    public bool HasValidUsername()
    {
        return !string.IsNullOrWhiteSpace(Username);
    }

    /// <summary>
    /// Clear the saved profile (for logout/reset).
    /// </summary>
    public void ClearProfile()
    {
        Username = "";
        PlayerPrefs.DeleteKey(USERNAME_KEY);
        PlayerPrefs.Save();
        Debug.Log("Profile cleared.");
    }
}