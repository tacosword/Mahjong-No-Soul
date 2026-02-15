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
    
    [Header("Action Buttons")]
    public Button continueButton;
    public Button backButton;
    
    private bool actionInProgress = false; // PREVENT DOUBLE-CLICKS

    void Start()
    {
        Debug.Log("[ResultScreen] Start() called");
        
        // Hide result panel at start
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
        
        // Setup button listeners - CLEAR ALL LISTENERS FIRST
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinuePressed);
            Debug.Log("[ResultScreen] ✓ Continue button → OnContinuePressed ONLY");
        }
        
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnRestartPressed);
            Debug.Log("[ResultScreen] ✓ Back button → OnRestartPressed ONLY");
        }
    }

    public void ShowResult(HandAnalysisResult analysis, int totalScore, int winnerSeatIndex = -1, List<int> winningTileSortValues = null, List<string> flowerMessages = null)
    {
        if (resultPanel == null) return;
        
        resultPanel.SetActive(true);
        actionInProgress = false; // RESET FLAG
        
        // Show/hide buttons based on server status
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(NetworkServer.active);
        }
        
        if (backButton != null)
        {
            backButton.gameObject.SetActive(true);
        }

        // Set win type
        if (winTypeText != null)
        {
            winTypeText.text = "Mahjong!";
        }

        // Build score details
        string details = BuildScoreDetails(analysis, flowerMessages);

        if (scoreDetailsText != null)
        {
            scoreDetailsText.text = details;
        }

        if (totalScoreText != null)
        {
            totalScoreText.text = $"Total Score: {totalScore}";
        }
        
    }

    private string BuildScoreDetails(HandAnalysisResult analysis, List<string> flowerMessages = null)
    {
        string details = "Base Win: +1\n";
        
        if (flowerMessages != null && flowerMessages.Count > 0)
        {
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
            
            foreach (string msg in winTypeMessages)
            {
                details += $"{msg}\n";
            }
            
            foreach (string msg in kongMessages)
            {
                details += $"{msg}\n";
            }
            
            if (flowerOnlyMessages.Count > 0)
            {
                foreach (string msg in flowerOnlyMessages)
                {
                    details += $"{msg}\n";
                }
            }
            
            if (flowerMessages.Count > 0)
            {
                details += "\n";
            }
        }

        if (analysis.IsPureHand && !analysis.IsTraditionalWin)
        {
            details += "Pure Hand (Non-Traditional): +3\n";
        }
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

            string honorBonuses = BuildHonorTileBonuses(analysis);
            if (!string.IsNullOrEmpty(honorBonuses))
            {
                details += honorBonuses;
            }
        }

        if (analysis.Is13OrphansWin)
        {
            details += "Thirteen Orphans: +8\n";
        }

        if (analysis.Is7PairsWin)
        {
            details += "Seven Pairs: +3\n";
        }

        if (analysis.FlowerCount > 0)
        {
            details += $"Flowers ({analysis.FlowerCount}): +{analysis.FlowerCount}\n";
        }

        return details;
    }

    private string BuildHonorTileBonuses(HandAnalysisResult analysis)
    {
        string bonuses = "";
        List<int> allTripletSortValues = analysis.TripletSortValues;

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
                bonuses = bonuses.Replace("Seat Wind Triplet: +1\n", "Double Wind (Seat/Round): +2\n");
            }
        }

        return bonuses;
    }

    public void Hide()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }

    public void OnContinuePressed()
    {
        // PREVENT DOUBLE-CLICK
        if (actionInProgress)
        {
            Debug.LogWarning("[ResultScreen] Action already in progress - ignoring");
            return;
        }
        actionInProgress = true;
        
        Debug.Log("[ResultScreen] ======== CONTINUE PRESSED ========");
        
        if (!NetworkServer.active || !NetworkClient.isConnected)
        {
            Debug.LogError("[ResultScreen] Not host! Cannot continue.");
            actionInProgress = false;
            return;
        }
        
        Debug.Log("[ResultScreen] Starting new round...");
        Hide();
        
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.StartNewRound();
        }
        else
        {
            Debug.LogError("[ResultScreen] NetworkedGameManager is NULL!");
            actionInProgress = false;
        }
    }

    public void OnRestartPressed()
    {
        // PREVENT DOUBLE-CLICK
        if (actionInProgress)
        {
            Debug.LogWarning("[ResultScreen] Action already in progress - ignoring");
            return;
        }
        actionInProgress = true;
        
        Debug.Log("[ResultScreen] ======== BACK PRESSED ========");
        Hide();
        
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
        
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}