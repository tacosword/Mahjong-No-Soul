using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Mirror;

/// <summary>
/// Displays the result screen when a player wins with Mahjong.
/// Shows win type, score breakdown, and total score.
/// </summary>
public class ResultScreenUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject resultPanel;
    public TextMeshProUGUI winTypeText;
    public TextMeshProUGUI scoreDetailsText;
    public TextMeshProUGUI totalScoreText;
    
    [Header("2D Tile Display")]
    public WinningHandDisplay2D tileDisplay2D;
    
    [Header("Action Buttons")]
    public Button continueButton;
    public Button backButton;

    void Start()
    {
        Debug.Log("[ResultScreen] Start() called");
        
        // Hide result panel at start
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
            Debug.Log($"[ResultScreen] Result panel hidden at start: {resultPanel.name}");
        }
        else
        {
            Debug.LogError("[ResultScreen] resultPanel is NOT ASSIGNED in Inspector!");
        }
        
        // Check all required references
        Debug.Log($"[ResultScreen] winTypeText assigned: {winTypeText != null}");
        Debug.Log($"[ResultScreen] scoreDetailsText assigned: {scoreDetailsText != null}");
        Debug.Log($"[ResultScreen] totalScoreText assigned: {totalScoreText != null}");
        Debug.Log($"[ResultScreen] tileDisplay2D assigned: {tileDisplay2D != null}");
        
        // Setup button listeners - REMOVE ALL EXISTING LISTENERS FIRST
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinuePressed);
            Debug.Log("[ResultScreen] ✓ Continue button listener added → OnContinuePressed");
        }
        else
        {
            Debug.LogWarning("[ResultScreen] continueButton is NULL!");
        }
        
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnRestartPressed);
            Debug.Log("[ResultScreen] ✓ Back button listener added → OnRestartPressed");
        }
        else
        {
            Debug.LogWarning("[ResultScreen] backButton is NULL!");
        }
    }

    /// <summary>
    /// Display the result screen with the winning hand analysis and score.
    /// </summary>
    /// <param name="analysis">Hand analysis result</param>
    /// <param name="totalScore">Total score</param>
    /// <param name="winnerSeatIndex">Seat index of the winner</param>
    /// <param name="winningTileSortValues">The actual tile sort values to display</param>
    public void ShowResult(HandAnalysisResult analysis, int totalScore, int winnerSeatIndex = -1, List<int> winningTileSortValues = null, List<string> flowerMessages = null)
    {
        Debug.Log($"[ResultScreen] ===== ShowResult called =====");
        Debug.Log($"[ResultScreen] Score: {totalScore}, IsWinning: {analysis.IsWinningHand}");
        Debug.Log($"[ResultScreen] Winner Seat Index: {winnerSeatIndex}");
        Debug.Log($"[ResultScreen] Tile values count: {winningTileSortValues?.Count ?? 0}");
        if (winningTileSortValues != null)
        {
            Debug.Log($"[ResultScreen] Tiles: {string.Join(", ", winningTileSortValues)}");
        }

        if (resultPanel == null)
        {
            Debug.LogError("[ResultScreen] resultPanel is NULL! Cannot show result screen!");
            Debug.LogError("[ResultScreen] Make sure ResultScreenUI component has resultPanel assigned in Inspector!");
            return;
        }

        Debug.Log($"[ResultScreen] Activating result panel: {resultPanel.name}");
        resultPanel.SetActive(true);
        
        // Show/hide buttons based on server status
        if (continueButton != null)
        {
            bool isServer = NetworkServer.active;
            continueButton.gameObject.SetActive(isServer);
            Debug.Log($"[ResultScreen] Continue button visibility: {isServer} (NetworkServer.active={NetworkServer.active})");
            
            if (isServer)
            {
                Debug.Log($"[ResultScreen] ✓✓✓ CONTINUE BUTTON IS VISIBLE ✓✓✓");
            }
        }
        
        if (backButton != null)
        {
            backButton.gameObject.SetActive(true);
            Debug.Log("[ResultScreen] Back button visible: true");
        }
        
        Debug.Log($"[ResultScreen] Result panel active: {resultPanel.activeSelf}");

        // Set win type
        if (winTypeText != null)
        {
            winTypeText.text = "Mahjong!";
            Debug.Log("[ResultScreen] Win type text set");
        }
        else
        {
            Debug.LogWarning("[ResultScreen] winTypeText is null - cannot set win type");
        }

        // Build score details
        string details = BuildScoreDetails(analysis, flowerMessages);
        Debug.Log($"[ResultScreen] Score details built: {details.Length} characters");

        // Set score details text
        if (scoreDetailsText != null)
        {
            scoreDetailsText.text = details;
            Debug.Log("[ResultScreen] Score details text set");
        }
        else
        {
            Debug.LogWarning("[ResultScreen] scoreDetailsText is null");
        }

        // Set total score
        if (totalScoreText != null)
        {
            totalScoreText.text = $"Total Score: {totalScore}";
            Debug.Log("[ResultScreen] Total score text set");
        }
        else
        {
            Debug.LogWarning("[ResultScreen] totalScoreText is null");
        }
        
        // Display the winning hand tiles as 2D sprites
        if (tileDisplay2D != null)
        {
            tileDisplay2D.DisplayWinningHand(winnerSeatIndex, winningTileSortValues);
        }
        else
        {
            Debug.LogWarning("[ResultScreen] tileDisplay2D not assigned - tiles won't display");
        }

        Debug.Log($"[ResultScreen] ===== Result screen fully displayed =====");
    }

    /// <summary>
    /// Build the score breakdown text.
    /// </summary>
    private string BuildScoreDetails(HandAnalysisResult analysis, List<string> flowerMessages = null)
    {
        string details = "Base Win: +1\n";
        
        // === WIN TYPE & KONG BONUSES (Display these first) ===
        if (flowerMessages != null && flowerMessages.Count > 0)
        {
            // Separate messages by category
            List<string> winTypeMessages = new List<string>();
            List<string> kongMessages = new List<string>();
            List<string> flowerOnlyMessages = new List<string>();
            
            foreach (string msg in flowerMessages)
            {
                if (msg.Contains("Tsumo"))
                {
                    winTypeMessages.Add(msg);
                }
                else if (msg.Contains("Kong"))
                {
                    kongMessages.Add(msg);
                }
                else
                {
                    flowerOnlyMessages.Add(msg);
                }
            }
            
            // Add win type bonuses first
            foreach (string msg in winTypeMessages)
            {
                details += $"{msg}\n";
            }
            
            // Add Kong bonuses
            foreach (string msg in kongMessages)
            {
                details += $"{msg}\n";
            }
            
            // Add flower bonuses last (before hand composition bonuses)
            if (flowerOnlyMessages.Count > 0)
            {
                foreach (string msg in flowerOnlyMessages)
                {
                    details += $"{msg}\n";
                }
            }
            
            // Add spacing
            if (flowerMessages.Count > 0)
            {
                details += "\n";
            }
        }

        // Non-Traditional Pure Hand
        if (analysis.IsPureHand && !analysis.IsTraditionalWin)
        {
            details += "Pure Hand (Non-Traditional): +3\n";
        }
        // Traditional Win bonuses
        else if (analysis.IsTraditionalWin)
        {
            if (analysis.IsPureHand)
            {
                details += "True Pure Hand: +4\n";
            }
            else if (analysis.IsHalfHand)
            {
                details += "Half Hand: +2\n";
            }

            if (analysis.TripletsCount == 0 && analysis.SequencesCount == 4)
            {
                details += "All Sequences: +1\n";
            }

            if (analysis.SequencesCount == 0 && analysis.TripletsCount == 4)
            {
                details += "All Triplets: +2\n";
            }

            // Honor tile bonuses
            string honorBonuses = BuildHonorTileBonuses(analysis);
            if (!string.IsNullOrEmpty(honorBonuses))
            {
                details += honorBonuses;
            }
        }

        // Special hands
        if (analysis.Is13OrphansWin)
        {
            details += "Thirteen Orphans: +8\n";
        }

        if (analysis.Is7PairsWin)
        {
            details += "Seven Pairs: +3\n";
        }

        // Flowers
        if (analysis.FlowerCount > 0)
        {
            details += $"Flowers ({analysis.FlowerCount}): +{analysis.FlowerCount}\n";
        }

        return details;
    }

    /// <summary>
    /// Build the honor tile bonus text.
    /// </summary>
    private string BuildHonorTileBonuses(HandAnalysisResult analysis)
    {
        string bonuses = "";
        List<int> allTripletSortValues = analysis.TripletSortValues;

        // Find PlayerHand to get dragon values
        PlayerHand playerHand = FindFirstObjectByType<PlayerHand>();
        if (playerHand != null)
        {
            foreach (int dragonSortValue in playerHand.DRAGON_SORT_VALUES)
            {
                if (allTripletSortValues.Contains(dragonSortValue))
                {
                    bonuses += "Dragon Triplet: +1\n";
                }
            }
        }

        // Wind bonuses
        if (allTripletSortValues.Contains(PlayerHand.PLAYER_WIND_SORT_VALUE))
        {
            bonuses += "Seat Wind Triplet: +1\n";
        }

        if (allTripletSortValues.Contains(PlayerHand.ROUND_WIND_SORT_VALUE))
        {
            if (PlayerHand.ROUND_WIND_SORT_VALUE != PlayerHand.PLAYER_WIND_SORT_VALUE)
            {
                bonuses += "Round Wind Triplet: +1\n";
            }
            else
            {
                // Replace the seat wind line if it's both
                bonuses = bonuses.Replace("Seat Wind Triplet: +1\n", "Double Wind (Seat/Round): +2\n");
            }
        }

        return bonuses;
    }

    /// <summary>
    /// Hide the result screen.
    /// </summary>
    public void Hide()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
        
        if (tileDisplay2D != null)
        {
            tileDisplay2D.ClearDisplay();
        }
    }

    /// <summary>
    /// Called when Continue button is pressed (Server only).
    /// </summary>
    public void OnContinuePressed()
    {
        Debug.Log("[ResultScreen] ========================================");
        Debug.Log("[ResultScreen] CONTINUE BUTTON PRESSED!!!");
        Debug.Log("[ResultScreen] ========================================");
        
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[ResultScreen] Only server can continue!");
            return;
        }
        
        Debug.Log("[ResultScreen] Server check passed - starting new round");
        Hide();
        
        if (NetworkedGameManager.Instance != null)
        {
            Debug.Log("[ResultScreen] Calling NetworkedGameManager.Instance.StartNewRound()");
            NetworkedGameManager.Instance.StartNewRound();
            Debug.Log("[ResultScreen] StartNewRound() called successfully");
        }
        else
        {
            Debug.LogError("[ResultScreen] NetworkedGameManager.Instance is NULL!");
        }
        
        Debug.Log("[ResultScreen] OnContinuePressed() complete");
    }

    /// <summary>
    /// Called when the Back/Restart button is pressed.
    /// </summary>
    public void OnRestartPressed()
    {
        Debug.Log("[ResultScreen] ========================================");
        Debug.Log("[ResultScreen] BACK BUTTON PRESSED!!!");
        Debug.Log("[ResultScreen] ========================================");
        
        Hide();
        
        // Disconnect from network and return to main menu
        if (NetworkClient.isConnected)
        {
            if (NetworkServer.active)
            {
                NetworkManager.singleton.StopHost();
            }
            else
            {
                NetworkManager.singleton.StopClient();
            }
        }
        
        // Load main menu scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}