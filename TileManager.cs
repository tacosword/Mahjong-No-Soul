using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;

// NOTE: I am assuming TileData and MahjongSuit exist elsewhere in your project and are correct.

public enum GameState
{
    Playing,
    GameOver,
    ResultScreen // New State
}

public class TileManager : MonoBehaviour
{
    // ==========================================================
    // !!! CRITICAL ADDITION: PLAYER HAND INSTANCE !!!
    // ==========================================================
    private PlayerHand playerHand;

    private bool isSelectingKong = false;
    private List<int> availableKongValues = new List<int>();

    [SerializeField] private GameObject[] allUniqueTilePrefabs;

    // Logic state for the selection system

    // ==========================================================
    // !!! WIN LOGIC ADDITIONS - MINIMAL VARIABLES !!!
    // ==========================================================
    private bool gameIsOver = false;
    private bool canDeclareWin = false; // Flag: Win condition met, but player has not confirmed
    public string winMessage = "TSUMO! You won with a winning hand!";
    
    // --- NEW VARIABLE: Stored Analysis for Scoring ---
    private HandAnalysisResult currentHandAnalysis = new HandAnalysisResult();

    // Public properties so ClickableTile can read them
    public bool IsSelectingKong => isSelectingKong;
    public List<int> AvailableKongValues => availableKongValues;

    // ==========================================================
    // !!! UI REFERENCES !!!
    // ==========================================================
    [Header("UI References")]
    public GameObject winButtonUI; 
    [Tooltip("Assign the Kong/Kan button here.")]
    public GameObject kongButtonUI; // <<< NEW UI REFERENCE

    // ==========================================================
    // !!! KONG LOGIC VARIABLES !!!
    // ==========================================================
    private List<GameObject> meldedKongSets = new List<GameObject>(); // Tiles moved out of the hand
    private bool canDeclareKong = false; // Flag to enable the Kong button
    private int potentialKongTileValue = -1; // Stores the sort value of the 4-tile set

    // New offset for the Kong area (adjust these in the Inspector)
    // ==========================================================
// !!! KONG LOGIC VARIABLES !!!
 // ==========================================================

// New offset for the Kong area (adjust these in the Inspector)
[Header("Kong Display Settings")]
// >>> NEW VARIABLE: Use this to control the absolute start position in the scene.
[Tooltip("The world position where the first Kong set will start.")]
public Vector3 kongAreaStartPosition = new Vector3(-6.0f, 0f, 1.0f); // Default for testing
public float kongTileOffsetZ = 1.0f; 
public float kongTileSpacingX = 1.0f;
private int nextKongSetIndex = 0; // Tracks the position of the next Kong set

// ==========================================================
    // !!! TILE/GRID VARIABLES !!!
    // ==========================================================
    private List<GameObject> spawnedTiles = new List<GameObject>(); // The 13 sorted tiles (GameObject references)
    private GameObject drawnTile;
    private List<GameObject> flowerTiles = new List<GameObject>();
    public int numberOfTilesToDraw = 13;
    
    // >>> NEW VARIABLE: Use this to control the absolute start position of the player's hand.
    [Header("Hand Display Settings")]
    [Tooltip("The center world position for the player's sorted hand.")]
    public Vector3 handStartPosition = new Vector3(0f, 0f, -3.0f); // Recommended default
    
    public float spacing = 1.2f;
    public float drawnTileSpacing = 1.8f;
// ... (rest of the TILE/GRID VARIABLES)

    // ==========================================================
    // !!! TILE/GRID VARIABLES !!!
    // ==========================================================

    public float flowerTileOffsetZ = -0.5f;
    public float flowerTileSpacing = 0.8f;
    public float flowerTileOffsetX = 0.5f;
    public GameObject[] tilePrefabs;
    public int gridRows = 5;
    public int gridCols = 6;
    public float gridSpacingX = .12f;
    public float gridSpacingZ = .8f;
    public Vector3 gridStartPosition = new Vector3(-1f, 0f, 1f);
    private bool[,] gridOccupancy;
    private int placedTileCount = 0;

    // ==========================================================
    // !!! ROTATION CONSTANTS !!!
    // ==========================================================
    private readonly Quaternion HandRotation = Quaternion.Euler(-45f, 0f, 0f);
    private readonly Quaternion DiscardRotation = Quaternion.Euler(0f, 0f, 0f);

    [SerializeField] private GameObject tenpaiUIPanel; // Assign a UI panel in inspector
[SerializeField] private Transform tenpaiIconsContainer; // Where icons go
[SerializeField] private GameObject iconPrefab; // Image prefab

public void ShowTenpaiUI(Vector3 tilePosition, List<TileData> tiles)
{
    if (iconPrefab == null || tenpaiUIPanel == null) return;

    tenpaiUIPanel.SetActive(true);

    // 1. Position the Panel
    Vector3 screenPos = Camera.main.WorldToScreenPoint(tilePosition);
    RectTransform rect = tenpaiUIPanel.GetComponent<RectTransform>();
    rect.position = screenPos;
    rect.anchoredPosition += new Vector2(0, 50f); 

    // 2. Clear old icons
    foreach (Transform child in tenpaiIconsContainer) Destroy(child.gameObject);

    // 3. Populate Icons with Score Data
    foreach (TileData data in tiles)
    {
        // Calculate what the hand WOULD be worth if this tile completed it
        int potentialScore = CalculatePotentialScore(data);

        // Instantiate the icon
        GameObject icon = Instantiate(iconPrefab, tenpaiIconsContainer);
        icon.GetComponent<Image>().sprite = data.tileSprite;

        // --- NEW: Update the Score Text ---
        // Assumes your iconPrefab has a child with a TextMeshProUGUI component
        TMPro.TextMeshProUGUI scoreText = icon.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (scoreText != null)
        {
            scoreText.text = $"{potentialScore} pts";
            
            // Optional: Color code high-value wins (e.g., 8+ points is gold)
            if (potentialScore >= 8) scoreText.color = Color.yellow; 
        }
    }
}

/// <summary>
/// Helper to simulate a win with a specific candidate tile to get the point value.
/// </summary>
private int CalculatePotentialScore(TileData candidate)
{
    // Create a temporary list for analysis so we don't touch the real playerHand lists
    List<TileData> simulationHand = new List<TileData>(playerHand.HandTiles);
    if (playerHand.DrawnTile != null) simulationHand.Add(playerHand.DrawnTile);
    
    // Note: We don't remove the hovered tile here because RequestTenpaiCheck 
    // should ideally pass the "already reduced" hand or handle the swap.
    
    // Use the existing analysis logic but pass the candidate as the 'DrawnTile'
    // It is safer to modify a local copy of HandAnalysisResult
    playerHand.SetDrawnTile(candidate); 
    HandAnalysisResult analysis = playerHand.CheckForWinAndAnalyze();
    
    int score = playerHand.CalculateTotalScore(analysis, 1);

    // Restore the actual drawn tile
    playerHand.SetDrawnTile(drawnTile != null ? drawnTile.GetComponent<TileData>() : null);

    return score;
}

public void HideTenpaiVisuals()
{
    if (tenpaiUIPanel != null) tenpaiUIPanel.SetActive(false);
}

    void Awake()
    {
        // ADD THIS: Get the component properly
        playerHand = GetComponent<PlayerHand>();
        if (playerHand == null) playerHand = gameObject.AddComponent<PlayerHand>();

        gridOccupancy = new bool[gridRows, gridCols];
        
        if (winButtonUI != null) winButtonUI.SetActive(false);
        if (kongButtonUI != null) kongButtonUI.SetActive(false);
    }
    // ... rest of code

    public void RequestTenpaiCheck(GameObject hoveredTile)
{
    if (isSelectingKong || gameIsOver) return;
    if (tenpaiUIPanel != null) tenpaiUIPanel.SetActive(false);

    SyncGameObjectsToPlayerHand();

    int currentKongs = playerHand.MeldedKongs.Count / 4; 
    
    // DEBUG 1: Verify state after Kong
    Debug.Log($"[Tenpai] Starting Check. HandTiles: {playerHand.HandTiles.Count}, MeldedKongs: {playerHand.MeldedKongs.Count}, CalculatedKongs: {currentKongs}");

    List<TileData> currentHand = new List<TileData>(playerHand.HandTiles);
    if (playerHand.DrawnTile != null) currentHand.Add(playerHand.DrawnTile);

    TileData hoveredData = hoveredTile.GetComponent<TileData>();
    
    // Robust removal
    var match = currentHand.FirstOrDefault(t => t.GetSortValue() == hoveredData.GetSortValue());
    if (match != null) 
    {
        currentHand.Remove(match);
    }
    else 
    {
        Debug.LogWarning("[Tenpai] Could not find hovered tile in currentHand list!");
    }

    List<TileData> winningTiles = new List<TileData>();
    int scanCount = 0;

    foreach (GameObject prefab in allUniqueTilePrefabs)
    {
        TileData candidate = prefab.GetComponent<TileData>();
        List<TileData> testHand = new List<TileData>(currentHand);
        testHand.Add(candidate);
        testHand.AddRange(playerHand.MeldedKongs);

        // DEBUG 2: Log once per scan to check math
        if (scanCount == 0)
        {
            Debug.Log($"[Tenpai] Sample TestHand Size: {testHand.Count}. Expected for {currentKongs} Kongs: {14 + currentKongs}");
        }
        scanCount++;

        if (playerHand.IsValidMahjongHand(testHand, currentKongs))
        {
            winningTiles.Add(candidate);
            Debug.Log($"[Tenpai] FOUND WINNING TILE: {candidate.name}");
        }
    }

    if (winningTiles.Count > 0)
    {
        Debug.Log($"[Tenpai] Showing UI with {winningTiles.Count} tiles.");
        ShowTenpaiUI(hoveredTile.transform.position, winningTiles);
    }
    else
    {
        // If this logs, the logic scanned all tiles and IsValidMahjongHand returned false for all of them
        Debug.Log("[Tenpai] Scan complete. No winning tiles found.");
    }
}

private bool CheckHypotheticalWin(List<TileData> hypotheticalHand, TileData candidateTile)
{
    if (playerHand == null) return false;

    List<TileData> testHand = new List<TileData>(hypotheticalHand);
    testHand.Add(candidateTile);
    
    // Ensure you are including melds in the testHand list before calling this
    testHand.AddRange(playerHand.MeldedKongs);

    return playerHand.IsValidMahjongHand(testHand, playerHand.MeldedKongs.Count / 4);
}

private List<TileData> FindWinningTiles(List<TileData> hand)
{
    List<TileData> solutions = new List<TileData>();
    
    // We need the kong count here too
    int kongCount = nextKongSetIndex;

    foreach (GameObject prefab in allUniqueTilePrefabs) 
    {
        if (prefab == null) continue;

        TileData candidate = prefab.GetComponent<TileData>();
        List<TileData> testHand = new List<TileData>(hand);
        testHand.Add(candidate);
        
        // FIX: Added kongCount parameter
        if (playerHand.IsValidMahjongHand(testHand, kongCount))
        {
            solutions.Add(candidate);
        }
    }
    return solutions;
}

    void Start()
{
    // 1. Generate 14 tile sort values for a COMPLETE random winning hand
    // (Ensure this method exists in your HandGenerator class)
    List<int> winningHandValues = HandGenerator.GenerateRandomWinningHandSortValues();

    if (winningHandValues == null || winningHandValues.Count != 14)
    {
        Debug.LogError("HandGenerator failed to return 14 tiles. Check the generator logic.");
        return;
    }

    // 2. Separate the list: 13 for the hand, 1 for the drawn tile
    // We take the last tile in the list as the "Win Trigger" tile
    int winningTileValue = winningHandValues[13];
    List<int> initialHandValues = winningHandValues.GetRange(0, 13);

    // 3. Draw and spawn the 13 hand tiles
    DrawInitialHand(initialHandValues);

    // 4. Force the 14th tile to be the winning tile instead of drawing a random one
    DrawSpecificTile(winningTileValue);

    // 5. Sync and Sort for the visual layout
    SyncGameObjectsToPlayerHand();
    SortHand();

    // 6. Log for testing
    Debug.Log($"Game started with a COMPLETE winning hand. Final tile was: {winningTileValue}");
}
    
    // =================================================================
    // START: HAND SYNCHRONIZATION AND WIN CHECK
    // =================================================================

    /// <summary>
    /// Synchronizes the TileData objects from GameObjects (the visual layer) 
    /// into the logical PlayerHand object (the rule engine).
    /// </summary>
    private void SyncGameObjectsToPlayerHand()
    {
        // 1. Reset and populate the HandTiles
        playerHand.HandTiles.Clear();
        foreach (GameObject tileGO in spawnedTiles)
        {
            TileData data = tileGO.GetComponent<TileData>();
            if (data != null) playerHand.AddToHand(data);
        }
        
        // 2. Set the DrawnTile
        if (drawnTile != null)
        {
            playerHand.SetDrawnTile(drawnTile.GetComponent<TileData>());
        }
        else
        {
            playerHand.SetDrawnTile(null);
        }
        
        // 3. Reset and populate the FlowerTiles (Required to exclude flowers from win check)
        playerHand.FlowerTiles.Clear();
        foreach (GameObject tileGO in flowerTiles)
        {
            TileData data = tileGO.GetComponent<TileData>();
            if (data != null) playerHand.CollectFlower(data);
        }
    }


    /// <summary>
    /// Calls the fully implemented Mahjong win check in PlayerHand and stores the analysis.
    /// </summary>
    // --- Existing logic for regular game flow ---
private bool CheckForMahjongWin()
{
    SyncGameObjectsToPlayerHand();
    
    // Only allow win check if we actually have 14 functional tiles
    // (13 in hand + 1 drawn)
    if (playerHand.DrawnTile == null) {
        canDeclareWin = false;
        if (winButtonUI != null) winButtonUI.SetActive(false);
        return false;
    }

    currentHandAnalysis = playerHand.CheckForWinAndAnalyze();
    canDeclareWin = currentHandAnalysis.IsWinningHand;

    if (winButtonUI != null) winButtonUI.SetActive(canDeclareWin);
    
    return canDeclareWin;
}
    

    /// <summary>
    /// Halts the game and displays a button for the player to confirm the win.
    /// </summary>
    private void WinReady()
    {
        canDeclareWin = true; // SET NEW FLAG
        
        Debug.Log("MAHJONG opportunity! Press the 'TSUMO' button or discard the drawn tile.");

        if (winButtonUI != null)
        {
            winButtonUI.SetActive(true);
        }
    }



    [Header("Result UI")]
public ResultScreenUI resultScreenUI; 

public void ShowResults()
{
    // 1. Guard
    if (!canDeclareWin || gameIsOver) 
    {
        if (winButtonUI != null) winButtonUI.SetActive(false);
        return; 
    }

    // 2. Refresh Analysis FIRST (while hand and drawnTile are separate)
    // This ensures CheckForWinAndAnalyze sees exactly 13 hand tiles + 1 drawn tile = 14 tiles.
    currentHandAnalysis = playerHand.CheckForWinAndAnalyze();

    // Safety Debug: If this says False, the issue is in CheckFor13Orphans logic
    Debug.Log($"Scoring Check: IsWinningHand={currentHandAnalysis.IsWinningHand}, Is13Orphans={currentHandAnalysis.Is13OrphansWin}");

    // 3. Set State
    gameIsOver = true; 
    canDeclareWin = false; 

    // 4. Visual Cleanup (Merge the tile for the end-game display)
    if (drawnTile != null)
    {
        if (!spawnedTiles.Contains(drawnTile))
        {
            spawnedTiles.Add(drawnTile);
        }
        // IMPORTANT: We null this now because the analysis is already done.
        // If we leave it, CalculateTotalScore might re-analyze and see a 15th tile.
        drawnTile = null; 
    }
    SortHand(); 

    // 5. Hide UI
    if (winButtonUI != null) winButtonUI.SetActive(false);
    if (kongButtonUI != null) kongButtonUI.SetActive(false);
    
    // 6. Calculate Final Score using the analysis object we just created
    int finalScore = playerHand.CalculateTotalScore(currentHandAnalysis, startingScore: 1);

    // 7. Display Results
    if (resultScreenUI != null)
    {
        resultScreenUI.ShowResult(currentHandAnalysis, finalScore);
    }
    else
    {
        Debug.LogWarning("ResultScreenUI reference missing!");
    }
}

    // =================================================================
    // END: HAND SYNCHRONIZATION AND WIN CHECK
    // =================================================================


    // =================================================================
    // START: FLOWER TILE LOGIC / DRAW MODIFICATIONS
    // =================================================================


    // --- FIXED HELPER FUNCTION FOR TILE TYPE CHECKING ---
    private bool IsFlowerTile(GameObject tile)
    {
        TileData data = tile.GetComponent<TileData>();
        
        if (data != null)
        {
            // Assuming flower tiles have names containing "Flower"
            return data.name.Contains("Flower");
        }
        return false;
    }


    /// <summary>
    /// Loads the initial hand using a provided list of Sort Values, handling flowers.
    /// </summary>
    void DrawInitialHand(List<int> requiredSortValues)
    {
        spawnedTiles.Clear();
        flowerTiles.Clear();
        
        // 2. Iterate through the required tile values
        foreach (int sortValue in requiredSortValues)
        {
            // Find the correct prefab based on the required sort value
            GameObject selectedPrefab = FindPrefabBySortValue(sortValue);

            if (selectedPrefab == null)
            {
                Debug.LogError($"Could not find tile prefab for SortValue: {sortValue}. Skipping.");
                continue;
            }

            GameObject newTile = Instantiate(selectedPrefab, Vector3.zero, Quaternion.identity);
            newTile.transform.SetParent(this.transform);

            // 3. Check if it's a Flower Tile
            if (IsFlowerTile(newTile))
            {
                flowerTiles.Add(newTile);
                continue;
            }

            // 4. If it's a regular tile, add it to the hand (GameObject list)
            spawnedTiles.Add(newTile);
        }

        // Handle drawing replacement tiles for any flowers drawn here:
        while (spawnedTiles.Count < numberOfTilesToDraw)
        {
            // Draw random replacement tiles until we reach 13
            int randomIndex = Random.Range(0, tilePrefabs.Length);
            GameObject selectedPrefab = tilePrefabs[randomIndex];
            GameObject replacementTile = Instantiate(selectedPrefab, Vector3.zero, Quaternion.identity);
            replacementTile.transform.SetParent(this.transform);

            if (IsFlowerTile(replacementTile))
            {
                // Put the replacement flower aside and loop again for another replacement
                flowerTiles.Add(replacementTile);
            }
            else
            {
                // Add the non-flower replacement tile
                spawnedTiles.Add(replacementTile);
            }
        }

        RepositionTiles();
        Debug.Log($"Successfully loaded a generated hand with {spawnedTiles.Count} tiles and {flowerTiles.Count} flowers.");
    }


    /// <summary>
    /// Spawns the winning 14th tile by its exact sort value.
    /// </summary>
    private void DrawSpecificTile(int sortValue)
    {
        if (drawnTile != null || gameIsOver) return;

        GameObject selectedPrefab = FindPrefabBySortValue(sortValue);

        if (selectedPrefab == null)
        {
            Debug.LogError($"Could not find tile prefab for the winning drawn tile SortValue: {sortValue}.");
            return;
        }

        GameObject newDrawnTile = Instantiate(selectedPrefab, Vector3.zero, Quaternion.identity);
        newDrawnTile.transform.SetParent(this.transform);

        // Check for Flower Tile
        if (IsFlowerTile(newDrawnTile))
        {
            flowerTiles.Add(newDrawnTile);
            RepositionTiles();
            DrawSingleTile(); // draw replacement
            return;
        }
        
        drawnTile = newDrawnTile;

        RepositionTiles();
        
        CheckForKongOpportunity(); // Check for Kong (shouldn't happen on a specific draw, but safe to check)

        // CRITICAL STEP: Trigger Win Check (TSUMO)
        SyncGameObjectsToPlayerHand();
        if (CheckForMahjongWin())
        {

            WinReady(); // CALL THE NEW FUNCTION
        }
    }


    /// <summary>
    /// Searches the tilePrefabs array for a prefab matching the given SortValue.
    /// </summary>
    private GameObject FindPrefabBySortValue(int sortValue)
    {
        // Simple linear search for the prefab
        foreach (GameObject prefab in tilePrefabs)
        {
            TileData data = prefab.GetComponent<TileData>();
            if (data != null && data.GetSortValue() == sortValue)
            {
                return prefab;
            }
        }
        return null;
    }


    /// <summary>
    /// Draws a random tile, handles flowers, checks for Kong, and checks for Tsumo win.
    /// </summary>
    private void DrawSingleTile()
{
    // 1. Guard
    if (drawnTile != null || gameIsOver) return;
    
    // 2. Instantiate
    int randomIndex = Random.Range(0, tilePrefabs.Length);
    GameObject selectedPrefab = tilePrefabs[randomIndex];
    GameObject newDrawnTile = Instantiate(selectedPrefab, Vector3.zero, Quaternion.identity);
    newDrawnTile.transform.SetParent(this.transform);

    // 3. Flower Handling
    if (IsFlowerTile(newDrawnTile))
    {
        flowerTiles.Add(newDrawnTile);
        RepositionTiles();
        DrawSingleTile(); // Recursive call for replacement
        return;
    }
    
    // 4. SET THE GLOBAL VARIABLE FIRST
    // This must happen before any logic checks so DeclareKong's guard clause passes.
    drawnTile = newDrawnTile;

    // 5. SYNC LOGIC IMMEDIATELY
    // This ensures playerHand.DrawnTile is no longer null for the checks below.
    SyncGameObjectsToPlayerHand();

    // 6. VISUAL UPDATE
    RepositionTiles();

    // 7. CHECKS (Now both have access to the correctly synced hand)
    CheckForKongOpportunity();

    if (CheckForMahjongWin())
    {
        Debug.Log("Win condition met. Calling WinReady().");
        WinReady();
    }
}
    
    // =================================================================
    // END: FLOWER TILE LOGIC / DRAW MODIFICATIONS
    // =================================================================


    /// <summary>
    /// Sorts the tiles currently in the 'spawnedTiles' list based on suit and value.
    /// </summary>
    public void SortHand()
    {
        // 1. Sort the list of GameObjects using the GetSortValue from TileData.
        spawnedTiles.Sort((a, b) => {
            TileData dataA = a.GetComponent<TileData>();
            TileData dataB = b.GetComponent<TileData>();

            if (dataA != null && dataB != null)
            {
                return dataA.GetSortValue().CompareTo(dataB.GetSortValue());
            }
            return 0;
        });

        // 2. Reposition the GameObjects in the scene based on the new order.
        RepositionTiles();

        // 3. Keep the logical hand sorted as well (optional, but good practice)
        SyncGameObjectsToPlayerHand();
        playerHand.SortHand();
    }


    /// <summary>
    /// Re-draws the tiles in the scene in their current list order and positions Flowers.
    /// </summary>
    /// <summary>
    /// Re-draws the tiles in the scene in their current list order and positions Flowers.
    /// </summary>
    private void RepositionTiles()
    {
        // --- 1. Position the 13 Sorted Tiles ---
        int sortedHandSize = spawnedTiles.Count;
        
        // Calculate the total width of the hand to center it
        float handWidth = (sortedHandSize - 1) * spacing;
        float centerOffset = -handWidth / 2f;
        
        // Use the Hand Start Position as the base anchor
        float baseY = handStartPosition.y; 
        float baseZ = handStartPosition.z;
        
        // Determine the X position of the first tile by applying the center offset to the start position's X
        float startX = handStartPosition.x + centerOffset;
        
        for (int i = 0; i < sortedHandSize; i++)
        {
            // Calculate X relative to the new startX
            float xPos = startX + i * spacing;
            Vector3 newPosition = new Vector3(xPos, baseY, baseZ);
            
            spawnedTiles[i].transform.position = newPosition;
            // APPLY 45-DEGREE ROTATION AROUND THE X-AXIS
            spawnedTiles[i].transform.rotation = HandRotation;
        }
        
        // --- 2. Position the Drawn Tile (The 14th Tile) ---
        // X of the last sorted tile (if any)
        float endOfHandX = startX + (sortedHandSize - 1) * spacing; 
        
        if (drawnTile != null)
        {
            float drawnXPos = endOfHandX + drawnTileSpacing;
            drawnTile.transform.position = new Vector3(drawnXPos, baseY, baseZ);
            // APPLY 45-DEGREE ROTATION AROUND THE X-AXIS
            drawnTile.transform.rotation = HandRotation;

            endOfHandX = drawnXPos; // The right-most boundary is now the drawn tile
        }
        else if (sortedHandSize > 0)
        {
            // If drawnTile is null, use the last sorted tile's X as the boundary
            endOfHandX = endOfHandX;
        }
        else
        {
            // If both hand and drawn tile are empty, use the anchor X
            endOfHandX = handStartPosition.x;
        }


        // --- 3. Position the Flower Tiles (The set-aside row) ---
        if (flowerTiles.Count > 0)
        {
            // Position flowers relative to the end of the hand, maintaining the base Y position.
            float flowerStartX = endOfHandX + flowerTileSpacing + flowerTileOffsetX;
            float flowerZ = baseZ + flowerTileOffsetZ;

            for (int i = 0; i < flowerTiles.Count; i++)
            {
                float xPos = flowerStartX + i * flowerTileSpacing;
                
                Vector3 flowerPosition = new Vector3(xPos, baseY, flowerZ);
                
                flowerTiles[i].transform.position = flowerPosition;
                // Set flower tile rotation to flat 
                flowerTiles[i].transform.rotation = DiscardRotation;
            }
        }
    }


    /// <summary>
    /// Handles discarding a tile to the grid AND drawing a replacement tile for the hand.
    /// </summary>
    public void DiscardAndDrawTile(Vector3 handPosition, GameObject discardedTile)
{
    // 1. Guard Clause
    if (gameIsOver) return;

    // --- FIX START: Reset Win/Kong states for ANY discard ---
    canDeclareWin = false;
    canDeclareKong = false;
    if (winButtonUI != null) winButtonUI.SetActive(false);
    if (kongButtonUI != null) kongButtonUI.SetActive(false);
    // --- FIX END ---

    // Discard Logic
    TileData discardedTileData = discardedTile.GetComponent<TileData>();

    if (discardedTile == drawnTile)
    {
        // 3a. Discarding the 14th tile
        drawnTile = null;
        playerHand.DiscardDrawnTile(); 
    }
    else if (spawnedTiles.Contains(discardedTile))
    {
        // 3b. Discarding a sorted tile
        if (drawnTile != null)
        {
            spawnedTiles.Add(drawnTile); 
            drawnTile = null;
        }
        
        SyncGameObjectsToPlayerHand();
        spawnedTiles.Remove(discardedTile);
        playerHand.DiscardFromHand(discardedTileData);
        SortHand();
    }
    else
    {
        return;
    }
    
    // --- 4. Grid Placement ---
    if (FindNextGridPosition(out int row, out int col))
    {
        Vector3 gridWorldPosition = CalculateGridWorldPosition(row, col);
        discardedTile.transform.position = gridWorldPosition;
        discardedTile.transform.rotation = DiscardRotation;

        Collider collider = discardedTile.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;

        gridOccupancy[row, col] = true;
        placedTileCount++;
        
        // --- 5. Draw NEW tile ---
        // (Wait for next draw to re-enable Tsumo/Kong buttons)
        int expectedHandSize = numberOfTilesToDraw - (meldedKongSets.Count / 4) * 3;
        
        if (spawnedTiles.Count == expectedHandSize)
        {
            DrawSingleTile(); 
        }
    }
}


    // --- HELPER FUNCTIONS FOR GRID LOGIC ---
    private bool FindNextGridPosition(out int nextRow, out int nextCol)
    {
        for (int r = 0; r < gridRows; r++)
        {
            for (int c = 0; c < gridCols; c++)
            {
                if (!gridOccupancy[r, c])
                {
                    nextRow = r;
                    nextCol = c;
                    return true;
                }
            }
        }
        nextRow = -1;
        nextCol = -1;
        return false;
    }

    private Vector3 CalculateGridWorldPosition(int row, int col)
    {
        float x = gridStartPosition.x + (col * gridSpacingX);
        float z = gridStartPosition.z - (row * gridSpacingZ);
        // FIX: Add a small Y offset (0.01f) so tiles don't clip into the floor
        float y = gridStartPosition.y + 0.01f; 

        return new Vector3(x, y, z);
    }
    
    // =================================================================
    // START: KONG LOGIC
    // =================================================================

    /// <summary>
    /// Checks the current hand (13 tiles + drawn tile) for a four-of-a-kind.
    /// </summary>
    private void CheckForKongOpportunity()
{
    canDeclareKong = false;
    availableKongValues.Clear();
    if (kongButtonUI != null) kongButtonUI.SetActive(false);

    if (gameIsOver || canDeclareWin || drawnTile == null) return;

    SyncGameObjectsToPlayerHand(); 
    List<TileData> allTilesForCheck = new List<TileData>(playerHand.HandTiles);
    if (playerHand.DrawnTile != null) allTilesForCheck.Add(playerHand.DrawnTile);

    // Find ALL groups of 4
    var groups = allTilesForCheck
        .GroupBy(t => t.GetSortValue())
        .Where(g => g.Count() == 4)
        .Select(g => g.Key)
        .ToList();

    if (groups.Any())
    {
        availableKongValues = groups;
        canDeclareKong = true;
        if (kongButtonUI != null) kongButtonUI.SetActive(true);
    }
}

    /// <summary>
    /// PUBLIC function called by the UI button when the player declares Kong.
    /// </summary>
    public void DeclareKong()
{
    if (!canDeclareKong || gameIsOver) return;

    isSelectingKong = !isSelectingKong; // Toggle mode

    // 1. Determine which tiles to check (Hand + Drawn)
    var allTiles = spawnedTiles.ToList();
    if (drawnTile != null) allTiles.Add(drawnTile);

    foreach (GameObject tile in allTiles)
    {
        if (tile == null) continue;
        
        Renderer tileRenderer = tile.GetComponentInChildren<Renderer>();
        if (tileRenderer == null) continue;

        int val = tile.GetComponent<TileData>().GetSortValue();

        if (isSelectingKong)
        {
            // Highlight logic: Brighten valid tiles, Dim invalid tiles
            if (availableKongValues.Contains(val))
            {
                // Set to full brightness (White)
                tileRenderer.material.color = Color.white;
            }
            else
            {
                // Dim non-selectable tiles (Grey/Translucent look)
                tileRenderer.material.color = new Color(0.3f, 0.3f, 0.3f, 1.0f);
            }
        }
        else
        {
            // If toggled off, reset everyone to normal
            tileRenderer.material.color = Color.white;
        }
    }

    Debug.Log(isSelectingKong ? "Select a bright tile to Kong." : "Kong selection cancelled.");
}
    
    /// <summary>
    /// Positions the newly melded Kong set outside the main hand area.
    /// </summary>
    /// <summary>
    /// Positions the newly melded Kong set outside the main hand area.
    /// </summary>
    private void PositionKongSet(List<GameObject> kongTiles)
    {
        if (kongTiles.Count != 4) return;
        
        // Calculate the anchor point for the current Kong set based on the index
        // Each new Kong set will be offset further along the X-axis
        // >>> MODIFICATION: Use kongAreaStartPosition for the initial X, Y, and Z.
        float startX = kongAreaStartPosition.x + (nextKongSetIndex * (kongTileSpacingX * 4.5f)); 
        float startY = kongAreaStartPosition.y;
        // The Z-offset is calculated from the start position Z
        float startZ = kongAreaStartPosition.z + kongTileOffsetZ;

        for (int i = 0; i < 4; i++)
        {
            float xPos = startX + i * kongTileSpacingX;
            Vector3 newPosition = new Vector3(xPos, startY, startZ);
            
            kongTiles[i].transform.position = newPosition;
            // Kongs are usually laid flat 
            kongTiles[i].transform.rotation = DiscardRotation; 
        }
        
        // Advance the index for the next Kong declaration
        nextKongSetIndex++;
        Debug.Log($"Kong set positioned at index {nextKongSetIndex - 1}.");
    }

    public void ExecuteKong(int targetValue)
{
    isSelectingKong = false;
    canDeclareKong = false;

    // 1. RESET WIN STATE: The hand is about to change, so any previous 'Tsumo' is invalid
    canDeclareWin = false;
    if (winButtonUI != null) winButtonUI.SetActive(false);
    if (kongButtonUI != null) kongButtonUI.SetActive(false);

    ResetTileVisuals();

    try 
    {
        // 2. DECLARE THE LISTS (This fixes the 'does not exist' errors)
        List<TileData> kongDataForLogic = new List<TileData>();
        List<GameObject> tilesToMove = new List<GameObject>();
        
        int drawnValue = (drawnTile != null) ? drawnTile.GetComponent<TileData>().GetSortValue() : -1;

        // 3. IDENTIFY TILES (Case A: Using the drawn tile | Case B: Entirely from hand)
        if (drawnValue == targetValue)
        {
            // Find 3 matching tiles in hand to join the drawn tile
            List<GameObject> matchingHandTiles = spawnedTiles
                .Where(t => t.GetComponent<TileData>().GetSortValue() == targetValue)
                .Take(3).ToList();

            foreach (GameObject tileGO in matchingHandTiles) {
                spawnedTiles.Remove(tileGO);
                tilesToMove.Add(tileGO);
                kongDataForLogic.Add(tileGO.GetComponent<TileData>());
            }
            tilesToMove.Add(drawnTile);
            kongDataForLogic.Add(drawnTile.GetComponent<TileData>());
            drawnTile = null; // Clear the drawn slot
        }
        else
        {
            // Find 4 matching tiles entirely within the hand
            List<GameObject> matchingHandTiles = spawnedTiles
                .Where(t => t.GetComponent<TileData>().GetSortValue() == targetValue)
                .Take(4).ToList();

            foreach (GameObject tileGO in matchingHandTiles) {
                spawnedTiles.Remove(tileGO);
                tilesToMove.Add(tileGO);
                kongDataForLogic.Add(tileGO.GetComponent<TileData>());
            }

            // If we had a drawn tile that WASN'T part of the kong, move it into the hand
            if (drawnTile != null)
            {
                spawnedTiles.Add(drawnTile);
                drawnTile = null; 
            }
        }

        // 4. APPLY THE KONG
        if (playerHand != null) playerHand.AddMeldedKong(kongDataForLogic);
        meldedKongSets.AddRange(tilesToMove);
        PositionKongSet(tilesToMove);
        
        SyncGameObjectsToPlayerHand(); 
        SortHand();

        // 5. DRAW REPLACEMENT & RE-CHECK WIN
        // In Mahjong, you get one replacement tile from the 'Dead Wall' after a Kong
        DrawSingleTile(); 

        // This checks if the NEW replacement tile completes a winning hand
        CheckForMahjongWin(); 
    }
    catch (System.Exception e) 
    { 
        Debug.LogError($"[ExecuteKong] Error: {e.Message}"); 
    }
}

private void ResetTileVisuals()
{
    var allTiles = spawnedTiles.ToList();
    if (drawnTile != null) allTiles.Add(drawnTile);

    foreach (GameObject tile in allTiles)
    {
        if (tile == null) continue;
        Renderer r = tile.GetComponentInChildren<Renderer>();
        if (r != null) r.material.color = Color.white;
    }
}


}