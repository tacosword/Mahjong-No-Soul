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
    public WinningHandDisplay2D tileDisplay2D; // Drag the WinningHandDisplay2D component here

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
        Debug.Log($"[ResultScreen] Score details content:\n{details}");

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
            // Pass tile sort values if available, otherwise use winner's GameObjects
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
        Debug.Log("[BuildScoreDetails] Starting score breakdown...");
        Debug.Log($"[BuildScoreDetails] IsPureHand: {analysis.IsPureHand}");
        Debug.Log($"[BuildScoreDetails] IsTraditionalWin: {analysis.IsTraditionalWin}");
        Debug.Log($"[BuildScoreDetails] Is13OrphansWin: {analysis.Is13OrphansWin}");
        Debug.Log($"[BuildScoreDetails] Is7PairsWin: {analysis.Is7PairsWin}");
        Debug.Log($"[BuildScoreDetails] IsHalfHand: {analysis.IsHalfHand}");
        Debug.Log($"[BuildScoreDetails] SequencesCount: {analysis.SequencesCount}");
        Debug.Log($"[BuildScoreDetails] TripletsCount: {analysis.TripletsCount}");
        Debug.Log($"[BuildScoreDetails] FlowerCount: {analysis.FlowerCount}");
        
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
            Debug.Log("[BuildScoreDetails] Added Pure Hand bonus");
        }
        // Traditional Win bonuses
        else if (analysis.IsTraditionalWin)
        {
            Debug.Log("[BuildScoreDetails] Processing traditional win bonuses...");
            
            if (analysis.IsPureHand)
            {
                details += "True Pure Hand: +4\n";
                Debug.Log("[BuildScoreDetails] Added True Pure Hand bonus");
            }
            else if (analysis.IsHalfHand)
            {
                details += "Half Hand: +2\n";
                Debug.Log("[BuildScoreDetails] Added Half Hand bonus");
            }

            if (analysis.TripletsCount == 0 && analysis.SequencesCount == 4)
            {
                details += "All Sequences: +1\n";
                Debug.Log("[BuildScoreDetails] Added All Sequences bonus");
            }

            if (analysis.SequencesCount == 0 && analysis.TripletsCount == 4)
            {
                details += "All Triplets: +2\n";
                Debug.Log("[BuildScoreDetails] Added All Triplets bonus");
            }

            // Honor tile bonuses
            string honorBonuses = BuildHonorTileBonuses(analysis);
            if (!string.IsNullOrEmpty(honorBonuses))
            {
                details += honorBonuses;
                Debug.Log($"[BuildScoreDetails] Added honor bonuses:\n{honorBonuses}");
            }
        }

        // Special hands
        if (analysis.Is13OrphansWin)
        {
            details += "Thirteen Orphans: +8\n";
            Debug.Log("[BuildScoreDetails] Added Thirteen Orphans bonus");
        }

        if (analysis.Is7PairsWin)
        {
            details += "Seven Pairs: +3\n";
            Debug.Log("[BuildScoreDetails] Added Seven Pairs bonus");
        }

        // Flowers
        if (analysis.FlowerCount > 0)
        {
            details += $"Flowers ({analysis.FlowerCount}): +{analysis.FlowerCount}\n";
            Debug.Log($"[BuildScoreDetails] Added Flower bonus: {analysis.FlowerCount}");
        }

        Debug.Log($"[BuildScoreDetails] Final details:\n{details}");
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
    /// Called when the Reset button is pressed.
    /// </summary>
    public void OnRestartPressed()
    {
        Debug.Log("[ResultScreen] Restart button pressed");
        
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