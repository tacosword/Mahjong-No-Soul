using System.Collections.Generic;
using System.Linq; 
using UnityEngine; 

// NOTE: MahjongSuit enum and TileData class are assumed to be defined elsewhere.
// NOTE: HandAnalysisResult class is assumed to be defined with public properties:
// IsWinningHand, IsTraditionalWin, Is13OrphansWin, Is7PairsWin, IsPureHand, IsHalfHand,
// SequencesCount, TripletsCount, PairSortValue, TripletSortValues, SequenceRootSortValues.

// =================================================================
// DEFINITION FOR PLAYER HAND CLASS
// =================================================================
public class PlayerHand : MonoBehaviour
{
    // --- Hand State ---
    private List<TileData> handTiles = new List<TileData>();
    private TileData drawnTile = null;
    private List<TileData> flowerTiles = new List<TileData>();

    // ... (Existing Constants and Hand Management Methods remain the same) ...
    public const int PLAYER_WIND_SORT_VALUE = 402; 
    public const int ROUND_WIND_SORT_VALUE = 401; 
    public readonly int[] DRAGON_SORT_VALUES = { 501, 502, 503 };
    
    public List<TileData> HandTiles => handTiles;
    public List<TileData> FlowerTiles => flowerTiles;
    public TileData DrawnTile => drawnTile;
    
    public void AddToHand(TileData tile) { if (tile != null) handTiles.Add(tile); }
    public void CollectFlower(TileData tile) { if (tile != null) flowerTiles.Add(tile); }
    public void SetDrawnTile(TileData tile) { drawnTile = tile; }
    public void MoveDrawnTileToHand() { if (drawnTile != null) { handTiles.Add(drawnTile); drawnTile = null; } }
    public TileData DiscardFromHand(TileData tileToDiscard) { if (handTiles.Remove(tileToDiscard)) return tileToDiscard; return null; }
    public TileData DiscardDrawnTile() { TileData discarded = drawnTile; drawnTile = null; return discarded; }
    public void SortHand() { handTiles.Sort((a, b) => a.GetSortValue().CompareTo(b.GetSortValue())); flowerTiles.Sort((a, b) => a.GetSortValue().CompareTo(b.GetSortValue())); }
    
    private List<TileData> meldedKongs = new List<TileData>();
    public List<TileData> MeldedKongs => meldedKongs; // <<< NEW ACCESSOR

    public bool IsValidMahjongHand(List<TileData> tiles, int numberOfKongs)
{
    // 1. Filter out flowers
    List<TileData> functionalTiles = tiles.Where(t => 
        t.suit != MahjongSuit.RedFlowers && t.suit != MahjongSuit.BlueFlower).ToList();

    // 2. Validate the Physical Count (Correct: 15 for 1 Kong)
    int expectedCount = 14 + numberOfKongs;
    if (functionalTiles.Count != expectedCount) return false;

    Dictionary<int, int> tileCounts = GetTileCounts(functionalTiles);

    // 3. Check Special Hands (Only if no Kongs)
    if (numberOfKongs == 0)
    {
        if (CheckFor13Orphans(tileCounts)) return true;
        if (CheckFor7Pairs(tileCounts)) return true;
    }

    // 4. Traditional Structure (4 melds + 1 pair)
    List<int> tileValues = functionalTiles.Select(t => t.GetSortValue()).OrderBy(v => v).ToList();
    
    // We try every possible pair first
    HashSet<int> potentialPairs = new HashSet<int>(tileValues);
    foreach (int pairValue in potentialPairs)
    {
        if (tileValues.Count(v => v == pairValue) >= 2)
        {
            List<int> workingHand = new List<int>(tileValues);
            workingHand.Remove(pairValue);
            workingHand.Remove(pairValue);

            // Pass the number of kongs we expect to find in the remaining tiles
            if (CanFormMeldsWithKongs(workingHand, numberOfKongs)) return true;
        }
    }

    // 5. Check Pure Hand as a fallback
    if (IsPureHandCustom(functionalTiles)) return true;

    return false;
}

private bool CanFormMeldsWithKongs(List<int> tiles, int kongsRemaining)
{
    if (tiles.Count == 0) return true;

    int firstTile = tiles[0];

    // OPTION 1: Try to treat as a Kong (4 of a kind)
    if (tiles.Count(v => v == firstTile) == 4)
    {
        List<int> nextHand = new List<int>(tiles);
        for(int i=0; i<4; i++) nextHand.Remove(firstTile);
        if (CanFormMeldsWithKongs(nextHand, kongsRemaining - 1)) return true;
    }

    // OPTION 2: Try to treat as a Triplet (3 of a kind)
    if (tiles.Count(v => v == firstTile) >= 3)
    {
        List<int> nextHand = new List<int>(tiles);
        for(int i=0; i<3; i++) nextHand.Remove(firstTile);
        if (CanFormMeldsWithKongs(nextHand, kongsRemaining)) return true;
    }

    // OPTION 3: Try to treat as a Sequence (1, 2, 3)
    // Sequences can't be formed with Honors (Winds/Dragons) - check your logic if needed
    if (tiles.Contains(firstTile + 1) && tiles.Contains(firstTile + 2))
    {
        List<int> nextHand = new List<int>(tiles);
        nextHand.Remove(firstTile);
        nextHand.Remove(firstTile + 1);
        nextHand.Remove(firstTile + 2);
        if (CanFormMeldsWithKongs(nextHand, kongsRemaining)) return true;
    }

    return false;
}

private Dictionary<int, int> GetTileCounts(List<TileData> tiles)
{
    Dictionary<int, int> counts = new Dictionary<int, int>();
    foreach (var tile in tiles)
    {
        int val = tile.GetSortValue();
        if (counts.ContainsKey(val)) counts[val]++;
        else counts[val] = 1;
    }
    return counts;
}

// Logic for your "Pure Hand" rule (all one numbered suit, no meld structure needed)
private bool IsPureHandCustom(List<TileData> tiles)
{
    if (tiles.Count == 0) return false;

    MahjongSuit firstSuit = tiles[0].suit;
    bool isNumbered = (int)firstSuit >= 1 && (int)firstSuit <= 3;
    
    // If it's a numbered suit and every tile in the hand matches it, it's a Pure Hand.
    return isNumbered && tiles.All(t => t.suit == firstSuit);
}

private bool CanFormMelds(List<int> tiles)
{
    if (tiles.Count == 0) return true;

    int first = tiles[0];

    // 1. Try to form a Triplet (3 of a kind)
    if (tiles.Count(v => v == first) >= 3)
    {
        List<int> nextHand = new List<int>(tiles);
        for(int i=0; i<3; i++) nextHand.Remove(first);
        if (CanFormMelds(nextHand)) return true;
    }

    // 2. Try to form a Sequence (Straight)
    // We only try this if the tile belongs to a suit (Circles, Bamboos, Characters)
    // AND the next two values in the sequence exist in the list.
    bool isNumberedSuit = (first / 100) >= 1 && (first / 100) <= 3;
    if (isNumberedSuit && tiles.Contains(first + 1) && tiles.Contains(first + 2))
    {
        List<int> nextHand = new List<int>(tiles);
        nextHand.Remove(first);
        nextHand.Remove(first + 1);
        nextHand.Remove(first + 2);
        if (CanFormMelds(nextHand)) return true;
    }

    return false; 
}

private bool IsSuitTile(int value)
{
    // Adjust this logic based on your SortValue system
    // Usually: Bamboo 1-9, Characters 10-18, Dots 19-27. 
    // Anything higher (Winds/Dragons) cannot be sequences.
    return value < 30; 
}
    public void AddMeldedKong(List<TileData> kongSet)
    {
        if (kongSet == null || kongSet.Count != 4) return;

        Debug.Log($"[PlayerHand] Logical sync: Adding {kongSet.Count} tiles to meldedKongs.");

        foreach (var tile in kongSet)
        {
            handTiles.Remove(tile);
        }
        
        meldedKongs.AddRange(kongSet);
    }
    // ... (CalculateFlowerScore, IsPureHand, IsHalfHand, CheckFor7Pairs, CheckFor13Orphans methods remain the same) ...

    // =================================================================
    // FIX 1: UPDATE Honor Tile Bonus to also check melded Kongs
    // =================================================================

    public int CalculateHonorTileBonus(HandAnalysisResult analysis)
    {
        // Honor bonuses (Dragons, Winds) only apply to 4 sets + 1 pair wins.
        if (!analysis.IsTraditionalWin) return 0; 
        
        int bonus = 0;
        
        // All sort values that form Triplets (Pungs) or Kongs in the winning hand
        // This list now includes the sort values of the melded Kongs (added in CheckForWinAndAnalyze)
        List<int> allTripletSortValues = analysis.TripletSortValues;
        
        // --- Dragon Triplet Bonus ---
        foreach (int dragonSortValue in DRAGON_SORT_VALUES)
        {
            if (allTripletSortValues.Contains(dragonSortValue)) 
            {
                bonus += 1;
                // Note: No need to worry about a tile counting twice (Pung and Kong) because
                // TripletSortValues should only contain one entry per distinct tile value
                // that formed a set (either Pung, Kong, or the set found in CanFormRemainingSetsAndPairAndAnalyze).
                Debug.Log($"Hand Bonus: Dragon Triplet ({dragonSortValue}) (+1 point)");
            }
        }
        
        // --- Player Wind (Seat Wind) Triplet Bonus ---
        if (allTripletSortValues.Contains(PLAYER_WIND_SORT_VALUE)) 
        {
            bonus += 1;
            Debug.Log("Hand Bonus: Seat Wind Triplet (+1 point)");
        }
        
        // --- Round Wind Triplet Bonus ---
        if (allTripletSortValues.Contains(ROUND_WIND_SORT_VALUE)) 
        {
            bonus += 1;
            Debug.Log("Hand Bonus: Round Wind Triplet (+1 point)");
        }
        
        return bonus;
    }

    // ... (CalculateHandBonusScore and CalculateTotalScore methods remain the same) ...

    // =================================================================
    // FIX 2: UPDATE LogHandComposition to include melded Kongs
    // =================================================================
    
    public void LogHandComposition(HandAnalysisResult analysis)
    {
        if (!analysis.IsTraditionalWin)
        {
            Debug.Log($"Hand Decomposition: Non-Traditional Win (e.g., Pure Hand or Seven Pairs).");
            return;
        }

        Debug.Log("--- Traditional Winning Hand Composition ---");
        
        // Log the Pair
        string pairTile = analysis.PairSortValue > 0 ? $"Tile Sort Value: {analysis.PairSortValue}" : "Not Found";
        Debug.Log($"Pair (Eye): {pairTile}");

        // Log Triplets (Pungs/Kongs)
        // The TripletsCount and TripletSortValues now include the melded Kongs (see CheckForWinAndAnalyze)
        Debug.Log($"Total Triplets/Kongs: {analysis.TripletsCount}");
        
        // Log the Melded Kongs separately for clarity
        int meldedKongCount = meldedKongs.Count / 4;
        for (int i = 0; i < meldedKongCount; i++)
        {
            // Assuming all 4 tiles in a melded kong are the same, use the first tile's sort value
            int kongSortValue = meldedKongs[i * 4].GetSortValue();
            Debug.Log($"  - Melded Kong Root Sort Value: {kongSortValue}");
        }

        // Log the Triplets (Pungs) found in the hand
        var unmeldedTripletValues = analysis.TripletSortValues
                                        .Where(v => !meldedKongs.Any(t => t.GetSortValue() == v)).ToList();

        foreach (var sortValue in unmeldedTripletValues)
        {
            Debug.Log($"  - Hand Triplet Root Sort Value: {sortValue}");
        }

        // Log Sequences
        Debug.Log($"Sequences (Chows): {analysis.SequencesCount}");
        foreach (var rootSortValue in analysis.SequenceRootSortValues)
        {
            Debug.Log($"  - Sequence Start Sort Value: {rootSortValue} (Tiles: {rootSortValue}, {rootSortValue + 1}, {rootSortValue + 2})");
        }
        
        Debug.Log("------------------------------------------");
    }

    // ... (InitializeWithKongForTesting method remains the same) ...
    
    // =================================================================
    // FIX 3: CORE WIN CHECK METHOD MODIFICATION
    // =================================================================

    private bool IsPureHand(List<CompletedMeld> completedMelds = null)
    {
        // 1. Combine ALL tiles: concealed hand, drawn tile, melded kongs, AND open melds
        List<TileData> allTiles = new List<TileData>(handTiles);
        if (drawnTile != null) allTiles.Add(drawnTile);
        allTiles.AddRange(meldedKongs);
    
    // CRITICAL FIX: Include open melds (Chi/Pon/Kong from discards) in Pure Hand check
    if (completedMelds != null)
    {
        foreach (var meld in completedMelds)
        {
            foreach (int sortValue in meld.TileSortValues)
            {
                TileData tileData = CreateTileDataFromSortValue(sortValue);
                allTiles.Add(tileData);
            }
        }
        Debug.Log($"[IsPureHand] Added {completedMelds.Count} open melds to purity check");
    }

    Debug.Log("--- Pure Hand Check Debug ---");
    Debug.Log($"Total Functional Tiles (Hand + Drawn + ALL Melds): {allTiles.Count}");
        
        // ADD: Include tiles from completed melds (Chi/Pon/Kong from discards)
        if (completedMelds != null)
        {
            foreach (var meld in completedMelds)
            {
                foreach (int sortValue in meld.TileSortValues)
                {
                    TileData tileData = CreateTileDataFromSortValue(sortValue);
                    allTiles.Add(tileData);
                }
            }
            Debug.Log($"[IsPureHand] Added {completedMelds.Count} completed melds to check");
        }

    Debug.Log("--- Pure Hand Check Debug ---");
    Debug.Log($"Total Functional Tiles (Hand + Drawn + Melds): {allTiles.Count}");

    // 2. Filter out non-functional tiles (Flowers)
    List<TileData> functionalTiles = allTiles.Where(t => 
        t.suit != MahjongSuit.RedFlowers && t.suit != MahjongSuit.BlueFlower).ToList();
    
    if (functionalTiles.Count == 0) 
    {
        Debug.Log("Pure Hand Check: No functional tiles, returning false.");
        return false;
    } 

    // 3. Determine all unique functional suits present
    var uniqueSuits = functionalTiles.Select(t => t.suit).Distinct().ToList();

    Debug.Log($"Unique Functional Suits Found: {string.Join(", ", uniqueSuits.Select(s => s.ToString()))}");

    // 4. Separate the numbered suits from the honor suits
    var numberedSuits = uniqueSuits.Where(suit => 
        suit == MahjongSuit.Circles || 
        suit == MahjongSuit.Bamboos || 
        suit == MahjongSuit.Characters).ToList();

    Debug.Log($"Numbered Suits Count: {numberedSuits.Count}");
    
    // --- Core Logic FIX (Strict Single Suit) ---
    // A Pure Hand must meet two strict criteria:
    // 1. It must contain only one type of numbered suit (Count == 1).
    // 2. The total number of unique functional suits must be exactly one (uniqueSuits.Count == 1).
    //    If uniqueSuits.Count > 1, it means Honors or a second numbered suit is present.

    if (numberedSuits.Count == 1 && uniqueSuits.Count == 1)
    {
        MahjongSuit primarySuit = numberedSuits.First();

        // Final check result
        Debug.Log($"Pure Hand Final Check: Strict Single Suit ({primarySuit}). Result: True");
        return true;
    }
    
    // --- Failure Cases ---
    
    // If numberedSuits.Count == 0, it is an All Honors hand (not a Pure Hand by your definition).
    if (numberedSuits.Count == 0)
    {
         Debug.Log("Pure Hand Final Check: Found 0 Numbered Suits (All Honors). Not a Pure Hand. Returning false.");
         return false;
    }

    // If uniqueSuits.Count > 1, it means a second numbered suit OR an Honor tile is present.
    Debug.Log($"Pure Hand Final Check: Found {uniqueSuits.Count} unique suits. Must be 1. Returning false.");
    return false;
    }

    /// <summary>
    /// Helper to create TileData from sort value (for checking completed melds).
    /// </summary>
    private TileData CreateTileDataFromSortValue(int sortValue)
    {
        TileData data = new TileData();
        int suitValue = sortValue / 100;
        int numberValue = sortValue % 100;
        data.suit = (MahjongSuit)suitValue;
        data.value = numberValue;
        return data;
    }

    private bool IsHalfHand(List<CompletedMeld> completedMelds = null)
        {
            List<TileData> tilesToCheck = new List<TileData>(handTiles);
            if (drawnTile != null) tilesToCheck.Add(drawnTile);
            tilesToCheck.AddRange(meldedKongs);
            
            // ADD: Include tiles from completed melds
            if (completedMelds != null)
            {
                foreach (var meld in completedMelds)
                {
                    foreach (int sortValue in meld.TileSortValues)
                    {
                        TileData tileData = CreateTileDataFromSortValue(sortValue);
                        tilesToCheck.Add(tileData);
                    }
                }
            }
        
        List<TileData> functionalTiles = tilesToCheck.Where(t => 
            t.suit != MahjongSuit.RedFlowers && t.suit != MahjongSuit.BlueFlower).ToList();
        if (functionalTiles.Count < 14) return false;
        var uniqueSuits = functionalTiles.Select(t => t.suit).Distinct().ToList();
        int numberedSuitCount = uniqueSuits.Count(suit => 
            suit == MahjongSuit.Circles || 
            suit == MahjongSuit.Bamboos || 
            suit == MahjongSuit.Characters);

        bool hasWinds = uniqueSuits.Contains(MahjongSuit.Winds);
        bool hasDragons = uniqueSuits.Contains(MahjongSuit.Dragons);

        if (numberedSuitCount == 1)
        {
            if (uniqueSuits.All(suit => 
                suit == MahjongSuit.Winds || 
                suit == MahjongSuit.Dragons || 
                suit == uniqueSuits.First(s => s == MahjongSuit.Circles || s == MahjongSuit.Bamboos || s == MahjongSuit.Characters)))
            {
                if (hasWinds || hasDragons)
                {
                    return true;
                }
            }
        }
        return false;
    }

private bool CheckFor13Orphans(Dictionary<int, int> tileCounts)
    {
        // Sort values for all 13 Orphans tiles:
        // 1 and 9 of Circles (101, 109), Bamboos (201, 209), Characters (301, 309) (6 tiles)
        // East, South, West, North (401, 402, 403, 404) (4 tiles)
        // Red, Green, White Dragons (501, 502, 503) (3 tiles)
        // Total = 13 unique tiles
        
        // Note: The sort values 401 and 402 are defined as constants. We will use the full range.
        
        List<int> requiredSortValues = new List<int>
        {
            101, 109, // Circles 1, 9
            201, 209, // Bamboos 1, 9
            301, 309, // Characters 1, 9
            401, 402, 403, 404, // East, South, West, North
            501, 502, 503 // Red, Green, White Dragons
        };

        // The hand must contain exactly one of each of the 13 unique tiles,
        // and one extra tile (the 'eye' or pair) which must be a duplicate of one of them.
        
        int singleCount = 0;
        int pairCount = 0;

        foreach (int sortValue in requiredSortValues)
        {
            if (tileCounts.ContainsKey(sortValue))
            {
                if (tileCounts[sortValue] == 1)
                {
                    singleCount++;
                }
                else if (tileCounts[sortValue] == 2)
                {
                    pairCount++;
                }
                else
                {
                    // Any tile having 3 or 4 copies means it's not 13 Orphans
                    return false;
                }
            }
        }
        
        // To be a winning 13 Orphans hand:
        // 1. It must contain all 13 required sort values (singleCount == 13).
        // 2. Exactly one of them must be a pair (pairCount == 1).
        return singleCount == 12 && pairCount == 1; // 12 tiles are singles, 1 is a pair, total 14 tiles
    }

 private bool CheckFor7Pairs(Dictionary<int, int> tileCounts)
    {
        // Must have 14 functional tiles.
        if (tileCounts.Values.Sum() != 14) return false;

        int pairsCount = 0;
        
        foreach (var count in tileCounts.Values)
        {
            if (count == 2)
            {
                pairsCount++;
            }
            else if (count != 4) 
            {
                // A Seven Pairs hand can contain quads (two identical pairs) 
                // in some rule sets, but strictly *not* counts of 1 or 3.
                return false; 
            }
        }

        // Standard Seven Pairs: Exactly 7 distinct pairs (no quads allowed).
        return pairsCount == 7; 
        
        // Note: If you allow 'Seven Pairs with Quad' (often a higher score), 
        // you would adjust the logic here. This implementation assumes the standard 7 distinct pairs.
    }

    /// <summary>
    /// Calculate flower score based on comprehensive rules.
    /// </summary>
    /// <param name="startingScore">Base score before flower bonuses</param>
    /// <param name="playerSeat">Player's seat (0-3) to determine their flower number</param>
    /// <param name="flowerMessages">Output list of flower bonus messages for result screen</param>
    /// <returns>Score after flower bonuses/penalties</returns>
    public int CalculateFlowerScore(int startingScore, int playerSeat, out List<string> flowerMessages)
    {
        flowerMessages = new List<string>();
        int currentScore = startingScore;
        
        if (FlowerTiles.Count == 0) 
        {
            return startingScore;
        }
        
        // Determine player's flower number (1-4 based on seat 0-3)
        int ownFlowerNumber = playerSeat + 1;
        
        Debug.Log($"[FlowerScore] Player seat {playerSeat}, own flower: {ownFlowerNumber}");
        Debug.Log($"[FlowerScore] Total flowers: {FlowerTiles.Count}");
        
        // Group flowers by suit (Red=7, Blue=6) and value (1-4)
        var flowersByValue = FlowerTiles
            .GroupBy(t => t.value)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        var redFlowers = FlowerTiles.Where(t => t.suit == MahjongSuit.RedFlowers).ToList();
        var blueFlowers = FlowerTiles.Where(t => t.suit == MahjongSuit.BlueFlower).ToList();
        
        Debug.Log($"[FlowerScore] Red flowers: {redFlowers.Count}, Blue flowers: {blueFlowers.Count}");
        
        // Check if player has their own flower number
        bool hasOwnFlower = flowersByValue.ContainsKey(ownFlowerNumber);
        int ownFlowerCount = hasOwnFlower ? flowersByValue[ownFlowerNumber].Count : 0;
        
        Debug.Log($"[FlowerScore] Has own flower ({ownFlowerNumber}): {hasOwnFlower}, Count: {ownFlowerCount}");
        
        // === RULE 1 & 2: Wrong Flower Penalty (unless protected by own flower) ===
        if (!hasOwnFlower)
        {
            // Count all non-matching flowers
            int wrongFlowerCount = FlowerTiles.Count(t => t.value != ownFlowerNumber);
            if (wrongFlowerCount > 0)
            {
                // FIXED: Always -1 regardless of how many wrong flowers
                currentScore -= 1;
                flowerMessages.Add($"Wrong Flowers ({wrongFlowerCount}): -1 point");
                Debug.Log($"[FlowerScore] Wrong flower penalty: -1 (had {wrongFlowerCount} wrong flowers, no protection)");
            }
        }
        else
        {
            Debug.Log($"[FlowerScore] Has own flower - protected from wrong flower penalty");
        }
        
        // === RULE 3: Double Own Flower Bonus ===
        if (ownFlowerCount >= 2)
        {
            currentScore += 1;
            flowerMessages.Add($"Double Own Flower (#{ownFlowerNumber}): +1 point");
            Debug.Log($"[FlowerScore] Double own flower bonus: +1");
        }
        
        // === RULE 4 & 5: Complete Set (1-4) Bonuses ===
        bool hasAll1234 = flowersByValue.ContainsKey(1) && 
                          flowersByValue.ContainsKey(2) && 
                          flowersByValue.ContainsKey(3) && 
                          flowersByValue.ContainsKey(4);
        
        if (hasAll1234)
        {
            // Check if all 1-4 are same color
            var flowers1to4 = FlowerTiles.Where(t => t.value >= 1 && t.value <= 4).ToList();
            bool allSameColor = flowers1to4.All(t => t.suit == MahjongSuit.RedFlowers) ||
                               flowers1to4.All(t => t.suit == MahjongSuit.BlueFlower);
            
            if (allSameColor)
            {
                currentScore += 2;
                string colorName = flowers1to4[0].suit == MahjongSuit.RedFlowers ? "Red" : "Blue";
                flowerMessages.Add($"Complete {colorName} Set (1-4): +2 points");
                Debug.Log($"[FlowerScore] Complete same-color set: +2");
            }
            else
            {
                currentScore += 1;
                flowerMessages.Add("Complete Mixed Set (1-4): +1 point");
                Debug.Log($"[FlowerScore] Complete mixed-color set: +1");
            }
        }
        
        // === RULE 6: All 8 Flowers Bonus ===
        if (FlowerTiles.Count == 8)
        {
            currentScore += 3;
            flowerMessages.Add("All 8 Flowers: +3 points");
            Debug.Log($"[FlowerScore] All 8 flowers bonus: +3");
        }
        
        Debug.Log($"[FlowerScore] Final score: {currentScore} (started at {startingScore})");
        return currentScore;
    }

public int CalculateHandBonusScore(HandAnalysisResult analysis)
    {
        // --- SCENARIO 1: PURE HAND (Non-Traditional Win) - Scores +3 and exits. ---
        if (analysis.IsPureHand && !analysis.IsTraditionalWin)
        {
            Debug.Log("Hand Bonus: Pure Hand (Non-Traditional Win) (+3 points)");
            return 3;
        }

        // --- SCENARIO 2: NON-WINNING or NON-SCORING TRADITIONAL HAND ---
        if (!analysis.IsTraditionalWin) 
        {
            return 0;
        }

        int bonus = 0;

        // --- 2.1. TRUE PURE / Half Hand Bonuses (Stacked) ---
        if (analysis.IsPureHand)
        {
            bonus += 4;
            Debug.Log("Hand Bonus: True Pure Hand (Pure Suit + Traditional Win) (+4 points)");
        }
        else if (analysis.IsHalfHand)
        {
            bonus += 2;
            Debug.Log("Hand Bonus: Half Hand (One Suit + Honors) (+2 points)");
        }

        // --- 2.2. All Sequences / All Triplets Bonuses (Stacked) ---

        if (analysis.TripletsCount == 0 && analysis.SequencesCount == 4) 
        { 
            bonus += 1; 
            Debug.Log("Hand Bonus: All Sequences (+1 point)"); 
        }
        
        if (analysis.SequencesCount == 0 && analysis.TripletsCount == 4) 
        { 
            bonus += 2; 
            Debug.Log("Hand Bonus: All Triplets (+2 points)"); 
        }
        
        return bonus;
    }

    /// <summary>
    /// Calculate total score including flower bonuses.
    /// </summary>
    /// <param name="analysis">Hand analysis result</param>
    /// <param name="playerSeat">Player's seat (0-3)</param>
    /// <param name="flowerMessages">Output list of flower messages</param>
    /// <param name="startingScore">Base score</param>
    /// <returns>Total score</returns>
    public int CalculateTotalScore(HandAnalysisResult analysis, int playerSeat, out List<string> flowerMessages, int startingScore = 1)
    {
        flowerMessages = new List<string>();
        
        Debug.Log($"Scoring Check: IsWinningHand={analysis.IsWinningHand}, Is13Orphans={analysis.Is13OrphansWin}");
        if (!analysis.IsWinningHand) return 0; 

        // --- EXCLUSIVE CASE 1: 13 ORPHANS (8 Points) ---
        if (analysis.Is13OrphansWin)
        {
            Debug.Log("WIN CONDITION: Thirteen Orphans! (8 points)");
            flowerMessages.Add("Thirteen Orphans: 8 points (flowers not applied)");
            return 8;
        }
        
        // CALCULATE FLOWER SCORE WITH NEW SYSTEM
        List<string> tempFlowerMessages;
        int scoreAfterFlowers = CalculateFlowerScore(startingScore, playerSeat, out tempFlowerMessages);
        flowerMessages.AddRange(tempFlowerMessages);
        
        // --- EXCLUSIVE CASE 2: SEVEN PAIRS (+2 Points) ---
        if (analysis.Is7PairsWin)
        {
            // The previous code had +3 points. Use the value consistent with your scoring rules.
            Debug.Log("WIN CONDITION: Seven Pairs! (+3 points)"); 
            return scoreAfterFlowers + 3; // Keep this as +3 based on the Debug.Log message

        }
        
        // Get the hand bonus for all other winning cases (Pure, Half, Traditional)
        int handBonus = CalculateHandBonusScore(analysis);
        
        // --- SCENARIO 1: Non-Traditional Pure Hand (Score is Exclusive, previously calculated) ---
        if (analysis.IsPureHand && !analysis.IsTraditionalWin)
        {
            // We reuse the already calculated scoreAfterFlowers
            return scoreAfterFlowers + handBonus;
        }

        // --- Standard Case: All Traditional Wins (Stack Flowers + Hand Bonuses + Honor Bonuses) ---
        // If we reach here, it must be a Traditional win (4 sets + 1 pair)
        int honorBonus = CalculateHonorTileBonus(analysis); 
        
        // Sum the flower score, hand bonus (Pure/Half/All Chows/All Pungs), and honor bonus (Winds/Dragons)
        int finalScore = scoreAfterFlowers + handBonus + honorBonus;
        Debug.Log($"[TotalScore] Final: {finalScore} = {scoreAfterFlowers} (flowers) + {handBonus} (hand) + {honorBonus} (honors)");
        return finalScore;
    }

    /// <summary>
    /// Calculate total score including all bonuses (flowers, Tsumo, Kongs).
    /// </summary>
    /// <param name="analysis">Hand analysis result</param>
    /// <param name="playerSeat">Player's seat (0-3)</param>
    /// <param name="isTsumo">True if self-drawn win, false if Ron</param>
    /// <param name="selfKongCount">Number of self-declared Kongs</param>
    /// <param name="discardKongCount">Number of Kongs from discards</param>
    /// <param name="flowerMessages">Output list of all bonus messages</param>
    /// <param name="startingScore">Base score</param>
    /// <returns>Total score</returns>
    public int CalculateTotalScore(
        HandAnalysisResult analysis, 
        int playerSeat, 
        bool isTsumo,
        int selfKongCount,
        int discardKongCount,
        int completedMeldCount,
        out List<string> flowerMessages, 
        int startingScore = 1)
    {
        flowerMessages = new List<string>();
        
        Debug.Log($"[TotalScore] Scoring Check: IsWinningHand={analysis.IsWinningHand}, Is13Orphans={analysis.Is13OrphansWin}");
        Debug.Log($"[TotalScore] Win Type: {(isTsumo ? "Tsumo (self-drawn)" : "Ron (discard)")}");
        Debug.Log($"[TotalScore] Self-Kongs: {selfKongCount}, Discard-Kongs: {discardKongCount}");
        
        if (!analysis.IsWinningHand) return 0; 

        // --- EXCLUSIVE CASE 1: 13 ORPHANS (8 Points) ---
        if (analysis.Is13OrphansWin)
        {
            Debug.Log("[TotalScore] WIN CONDITION: Thirteen Orphans! (8 points)");
            flowerMessages.Add("Thirteen Orphans: 8 points (flowers not applied)");
            return 8;
        }
        
        // CALCULATE FLOWER SCORE
        List<string> tempFlowerMessages;
        int scoreAfterFlowers = CalculateFlowerScore(startingScore, playerSeat, out tempFlowerMessages);
        flowerMessages.AddRange(tempFlowerMessages);
        
        // --- EXCLUSIVE CASE 2: SEVEN PAIRS (+3 Points) ---
        if (analysis.Is7PairsWin)
        {
            Debug.Log("[TotalScore] WIN CONDITION: Seven Pairs! (+3 points)"); 
            return scoreAfterFlowers + 3;
        }
        
        // Get the hand bonus for all other winning cases (Pure, Half, Traditional)
        int handBonus = CalculateHandBonusScore(analysis);
        
        // --- SCENARIO 1: Non-Traditional Pure Hand ---
        if (analysis.IsPureHand && !analysis.IsTraditionalWin)
        {
            return scoreAfterFlowers + handBonus;
        }

        // --- DETECT SPECIAL WIN CONDITIONS ---
        DetectSpecialWinConditions(analysis, isTsumo, completedMeldCount, selfKongCount);

        // --- Standard Case: All Traditional Wins ---
        int honorBonus = CalculateHonorTileBonus(analysis);
        
        // NEW: Calculate All Hidden bonus (includes Tsumo)
        int allHiddenBonus = 0;
        if (analysis.IsAllHidden)
        {
            allHiddenBonus = 3;
            flowerMessages.Add("All Hidden Hand (Menzen Tsumo): +3 points");
            Debug.Log("[TotalScore] All Hidden bonus: +3 (includes Tsumo)");
        }
        // NEW: Calculate Tsumo bonus (only if NOT All Hidden)
        else if (isTsumo)
        {
            allHiddenBonus = 1;
            flowerMessages.Add("Tsumo (Self-Drawn Win): +1 point");
            Debug.Log("[TotalScore] Tsumo bonus: +1");
        }
        
        // NEW: Calculate All Shown bonus
        int allShownBonus = 0;
        if (analysis.IsAllShown)
        {
            allShownBonus = 2;
            flowerMessages.Add("All Shown Hand: +2 points");
            Debug.Log("[TotalScore] All Shown bonus: +2");
        }
        
        // NEW: Calculate Kong bonuses
        int kongBonus = 0;
        if (selfKongCount > 0)
        {
            kongBonus += selfKongCount * 2;
            flowerMessages.Add($"Self-Drawn Kong{(selfKongCount > 1 ? "s" : "")} ({selfKongCount}): +{selfKongCount * 2} point{(selfKongCount * 2 > 1 ? "s" : "")}");
            Debug.Log($"[TotalScore] Self-Kong bonus: +{selfKongCount * 2} ({selfKongCount} kongs x 2)");
        }
        
        if (discardKongCount > 0)
        {
            kongBonus += discardKongCount;
            flowerMessages.Add($"Kong{(discardKongCount > 1 ? "s" : "")} from Discard ({discardKongCount}): +{discardKongCount} point{(discardKongCount > 1 ? "s" : "")}");
            Debug.Log($"[TotalScore] Discard-Kong bonus: +{discardKongCount} ({discardKongCount} kongs x 1)");
        }
        
        // Sum all bonuses
        int finalScore = scoreAfterFlowers + handBonus + honorBonus + allHiddenBonus + allShownBonus + kongBonus;
        Debug.Log($"[TotalScore] Final: {finalScore} = {scoreAfterFlowers} (flowers) + {handBonus} (hand) + {honorBonus} (honors) + {allHiddenBonus} (hidden/tsumo) + {allShownBonus} (shown) + {kongBonus} (kongs)");
        
        return finalScore;
    }
    
    // ADDED: New method to log the composition of the winning hand

    public HandAnalysisResult CheckForWinAndAnalyze(List<CompletedMeld> completedMelds = null)
    {
        HandAnalysisResult result = new HandAnalysisResult();

        // 1. Count ALL completed sets (Self-declared Kongs + Chi/Pon/Kong from discards)
        int meldedKongCount = meldedKongs.Count / 4;
        int completedMeldCount = completedMelds?.Count ?? 0;
        int totalCompletedSets = meldedKongCount + completedMeldCount;
        int setsNeededFromHand = 4 - totalCompletedSets;
        
        Debug.Log($"[CheckForWinAndAnalyze] Self-declared Kongs: {meldedKongCount}, Completed Melds: {completedMeldCount}, Total: {totalCompletedSets}, Sets needed from hand: {setsNeededFromHand}");

        // Create the full set of tiles currently in play
        List<TileData> fullHand = new List<TileData>(handTiles); 
        if (drawnTile != null) fullHand.Add(drawnTile); 

        Debug.Log($"[CheckForWinAndAnalyze] Hand tiles: {handTiles.Count}");
        Debug.Log($"[CheckForWinAndAnalyze] Drawn tile: {(drawnTile != null ? drawnTile.GetSortValue().ToString() : "NULL")}");
        Debug.Log($"[CheckForWinAndAnalyze] Full hand size: {fullHand.Count}");
        Debug.Log($"[CheckForWinAndAnalyze] Melded kongs count: {meldedKongs.Count}");

        // --- NEW: Count Flowers before filtering ---
        result.FlowerCount = fullHand.Count(t => 
            t.suit == MahjongSuit.RedFlowers || t.suit == MahjongSuit.BlueFlower);

        // --- Pre-Analysis Checks ---
        result.IsPureHand = IsPureHand(completedMelds);
        result.IsHalfHand = IsHalfHand(completedMelds);

        // 2. Prepare tiles for structural analysis (Filter out Flowers)
        fullHand.Sort((a, b) => a.GetSortValue().CompareTo(b.GetSortValue()));

        Dictionary<int, int> tileCounts = fullHand
            .Where(t => 
                t.suit != MahjongSuit.RedFlowers && t.suit != MahjongSuit.BlueFlower) 
            .GroupBy(t => t.GetSortValue())
            .ToDictionary(g => g.Key, g => g.Count());

        // CRITICAL FIX: Validate tile count BEFORE checking special hands
        // Expected: (sets needed * 3) + pair (2)
        // Self-declared kongs are NOT in tileCounts (they're separate), so we don't add them here
        int expectedConcealedTiles = (setsNeededFromHand * 3) + 2;
        int actualConcealedTiles = tileCounts.Values.Sum();
        
        Debug.Log($"[CheckForWinAndAnalyze] Expected concealed tiles: {expectedConcealedTiles}");
        Debug.Log($"[CheckForWinAndAnalyze] Actual concealed tiles: {actualConcealedTiles}");
        
        if (actualConcealedTiles != expectedConcealedTiles)
        {
            Debug.LogWarning($"[CheckForWinAndAnalyze] Tile count mismatch: expected {expectedConcealedTiles}, got {actualConcealedTiles}");
            // TESTING: Continue anyway to see if structure is valid
            // result.IsWinningHand = false;
            // return result;
        }

        // 2b. Check Non-traditional structural wins (only if no melds at all)
        if (totalCompletedSets == 0)
        {
            result.Is13OrphansWin = CheckFor13Orphans(tileCounts);
            if (!result.Is13OrphansWin)
            {
                result.Is7PairsWin = CheckFor7Pairs(tileCounts);
            }
        }
        
        // 3. Check for traditional 4 sets and 1 pair 
        result.IsTraditionalWin = HandChecker.CanFormRemainingSetsAndPairAndAnalyze(tileCounts, setsNeededFromHand, result);

        // 3b. Finalize Traditional Win analysis (Include ALL Melds)
        if (result.IsTraditionalWin)
        {
            // Add self-declared Kongs to the analysis
            result.TripletsCount += meldedKongCount; 
            
            foreach (var group in meldedKongs.GroupBy(t => t.GetSortValue()))
            {
                if (group.Count() == 4) 
                {
                    if (!result.TripletSortValues.Contains(group.Key))
                    {
                        result.TripletSortValues.Add(group.Key);
                    }
                }
            }
            
            // ADD: Include completed melds (Chi/Pon/Kong from discards)
            if (completedMelds != null)
            {
                foreach (var meld in completedMelds)
                {
                    Debug.Log($"[CheckForWinAndAnalyze] Adding completed meld: {meld.Type} with tile {meld.CalledTileSortValue}");
                    
                    if (meld.Type == InterruptActionType.Chi)
                    {
                        // Chi is a sequence
                        result.SequencesCount++;
                        // Add the root tile (lowest value in sequence)
                        int rootValue = meld.TileSortValues.Min();
                        if (!result.SequenceRootSortValues.Contains(rootValue))
                        {
                            result.SequenceRootSortValues.Add(rootValue);
                        }
                    }
                    else if (meld.Type == InterruptActionType.Pon || meld.Type == InterruptActionType.Kong)
                    {
                        // Pon/Kong is a triplet
                        result.TripletsCount++;
                        if (!result.TripletSortValues.Contains(meld.CalledTileSortValue))
                        {
                            result.TripletSortValues.Add(meld.CalledTileSortValue);
                        }
                    }
                }
            }
            
            LogHandComposition(result);
        }
        
        // 4. Final Winning Determination
        bool isNonTraditionalPureHandWin = result.IsPureHand && 
                                        !result.IsTraditionalWin && 
                                        !result.Is13OrphansWin && 
                                        !result.Is7PairsWin;

        result.IsWinningHand = result.IsTraditionalWin || 
                            result.Is13OrphansWin || 
                            result.Is7PairsWin || 
                            isNonTraditionalPureHandWin;

        return result;
    }

    internal int CalculateTotalScore(HandAnalysisResult analysis, int v)
    {
        throw new System.NotImplementedException();
    }

    // --- HandChecker Utility Class ---

    /// <summary>
    /// Detect All Hidden and All Shown hand conditions.
    /// Must be called AFTER basic win analysis is complete.
    /// </summary>
    /// <param name="analysis">Hand analysis result to update</param>
    /// <param name="isTsumo">Whether this is a Tsumo win</param>
    /// <param name="completedMeldCount">Number of Chi/Pon/Kong from discards</param>
    /// <param name="selfKongCount">Number of self-declared Kongs</param>
    private void DetectSpecialWinConditions(HandAnalysisResult analysis, bool isTsumo, int completedMeldCount, int selfKongCount)
    {
        // --- ALL HIDDEN HAND (Menzen Tsumo) ---
        // Conditions:
        // 1. Win by Tsumo (self-drawn)
        // 2. No melds from discards (no Chi/Pon/Kong from opponents)
        // 3. Self-drawn Kongs ARE allowed
        if (isTsumo && completedMeldCount == 0)
        {
            analysis.IsAllHidden = true;
            Debug.Log("[DetectSpecialWins] All Hidden Hand detected!");
            Debug.Log($"[DetectSpecialWins]   - Tsumo: Yes");
            Debug.Log($"[DetectSpecialWins]   - Completed melds from discards: 0");
            Debug.Log($"[DetectSpecialWins]   - Self-drawn Kongs: {selfKongCount} (allowed)");
        }
        
        // --- ALL SHOWN HAND (Toitoi Hoitei) ---
        // Conditions:
        // 1. Win by Ron (opponent's discard)
        // 2. All 4 sets are from completed melds (Chi/Pon/Kong from discards)
        // 3. No concealed sets in hand (except the pair)
        // 
        // Note: With self-Kongs, this becomes impossible because:
        // - Self-Kongs are concealed (not from discards)
        // - So we need: completedMeldCount == 4 AND selfKongCount == 0
        if (!isTsumo && completedMeldCount == 4 && selfKongCount == 0)
        {
            analysis.IsAllShown = true;
            Debug.Log("[DetectSpecialWins] All Shown Hand detected!");
            Debug.Log($"[DetectSpecialWins]   - Ron: Yes");
            Debug.Log($"[DetectSpecialWins]   - Completed melds from discards: 4");
            Debug.Log($"[DetectSpecialWins]   - Self-drawn Kongs: 0");
            Debug.Log($"[DetectSpecialWins]   - Concealed tiles: Only the pair + Ron tile");
        }
        
        Debug.Log($"[DetectSpecialWins] IsAllHidden: {analysis.IsAllHidden}, IsAllShown: {analysis.IsAllShown}");
    }
    private static class HandChecker
    {
        // Renamed and modified to accept the number of sets needed from the hand.
        public static bool CanFormRemainingSetsAndPairAndAnalyze(
            Dictionary<int, int> tileCounts, 
            int setsToFormInHand, // New parameter
            HandAnalysisResult analysis)
        {
            // Validate sets needed is reasonable
            if (setsToFormInHand < 0 || setsToFormInHand > 4) return false;
            
            // SPECIAL CASE: If we need 0 sets, we only need to find a pair
            if (setsToFormInHand == 0)
            {
                // All 4 sets are already complete (from melds)
                // We just need to verify we have exactly 1 pair (2 tiles)
                if (tileCounts.Values.Sum() != 2)
                {
                    Debug.Log($"[CanFormRemainingSetsAndPairAndAnalyze] Need only pair but have {tileCounts.Values.Sum()} tiles");
                    return false;
                }
                
                // Check if we have a valid pair (2 of same tile)
                foreach (var kvp in tileCounts)
                {
                    if (kvp.Value == 2)
                    {
                        analysis.PairSortValue = kvp.Key;
                        Debug.Log($"[CanFormRemainingSetsAndPairAndAnalyze] Found pair: {kvp.Key}");
                        return true;
                    }
                }
                
                Debug.Log($"[CanFormRemainingSetsAndPairAndAnalyze] No valid pair found");
                return false;
            }
            
            foreach (int sortValue in tileCounts.Keys.ToList())
            {
                if (tileCounts.ContainsKey(sortValue) && tileCounts[sortValue] >= 2)
                {
                    // ... (Logic remains the same as your original CanFormRemainingSetsAndPairAndAnalyze) ...
                    // 1. Try to remove this tile as the 'Pair'
                    Dictionary<int, int> remainingCounts = new Dictionary<int, int>(tileCounts);
                    remainingCounts[sortValue] -= 2;
                    
                    if (remainingCounts[sortValue] == 0)
                    {
                        remainingCounts.Remove(sortValue);
                    }

                    // 2. Prepare local counters and the Set SortValue lists for this path
                    int tempSequences = 0;
                    int tempTriplets = 0;
                    List<int> successfulTriplets = new List<int>(); 
                    List<int> successfulSequenceRoots = new List<int>();

                    // 3. Check if the remaining tiles can be decomposed into the required number of sets
                    if (CanFormSetsAndAnalyze(
                        remainingCounts, 
                        setsToFormInHand, // Use the new parameter
                        ref tempSequences, 
                        ref tempTriplets, 
                        successfulTriplets,
                        successfulSequenceRoots))
                    {
                        // Success: A winning decomposition was found. Update the result object.
                        analysis.SequencesCount = tempSequences;
                        analysis.TripletsCount = tempTriplets;
                        analysis.TripletSortValues = successfulTriplets; 
                        analysis.SequenceRootSortValues = successfulSequenceRoots;
                        analysis.PairSortValue = sortValue; 
                        return true;
                    }
                }
            }
            return false;
        }

        

        // REMOVED: HandChecker.CanForm4SetsAnd1PairAndAnalyze since CanFormRemainingSetsAndPairAndAnalyze is the unified method now.

        // CanFormSetsAndAnalyze remains exactly the same since its job is only to find the
        // requested number of sets from the remaining tiles after the pair is removed.
        private static bool CanFormSetsAndAnalyze(
    Dictionary<int, int> counts, 
    int setsToFind, 
    ref int sequencesFound, 
    ref int tripletsFound, 
    List<int> currentTripletSortValues,
    List<int> currentSequenceRootSortValues)
{
    // --- DEBUG 1: Function Entry State ---
    string remainingTiles = string.Join(", ", counts.Select(kv => $"{kv.Key}x{kv.Value}"));
    Debug.Log($"[DECOMPOSE] Depth: {4 - setsToFind}. Looking for {setsToFind} sets. Tiles: {remainingTiles}");

    // Base Case 1: Success - All sets found and no tiles remain (counts.Count == 0)
    if (setsToFind == 0) 
    {
        bool success = counts.Count == 0;
        Debug.Log($"[DECOMPOSE] **BASE CASE 1**: setsToFind=0. Tiles remaining: {counts.Count == 0}. Result: {success}");
        return success;
    }

    // Base Case 2: Failure - Sets needed, but no tiles remain.
    if (counts.Count == 0) 
    {
        Debug.Log("[DECOMPOSE] **BASE CASE 2**: counts.Count=0. Cannot find remaining sets. Result: False");
        return false;
    }

    // Get the smallest sort value remaining to ensure correct, ordered processing
    int firstTileSortValue = counts.Keys.Min();
    
    MahjongSuit suit = (MahjongSuit)(firstTileSortValue / 100);
    int value = firstTileSortValue % 100;
    
    Debug.Log($"[DECOMPOSE] First Tile: {firstTileSortValue}. Trying Pung/Chow first.");

    // --- Path A: Try to form a Triplet (Pung) using the first tile (3 of the same) ---
    if (counts.ContainsKey(firstTileSortValue) && counts[firstTileSortValue] >= 3)
    {
        // 1. Prepare remaining counts for Path A (Deep Copy)
        Dictionary<int, int> countsAfterTriplet = new Dictionary<int, int>(counts);
        countsAfterTriplet[firstTileSortValue] -= 3;
        if (countsAfterTriplet[firstTileSortValue] == 0) 
        {
            countsAfterTriplet.Remove(firstTileSortValue);
        }
        
        // 2. Prepare local analysis variables for Path A
        int nextSequencesFound_A = sequencesFound; 
        int nextTripletsFound_A = tripletsFound + 1; 
        
        List<int> tripletsAfterTriplet = new List<int>(currentTripletSortValues); 
        tripletsAfterTriplet.Add(firstTileSortValue); 
        
        List<int> sequencesAfterTriplet = new List<int>(currentSequenceRootSortValues);
        
        // 3. Recurse down Path A
        Debug.Log($"[DECOMPOSE]    - PUNG PATH A: Attempting Pung of {firstTileSortValue}. Sets remaining: {setsToFind - 1}");

        if (CanFormSetsAndAnalyze(
            countsAfterTriplet, 
            setsToFind - 1, 
            ref nextSequencesFound_A, 
            ref nextTripletsFound_A, 
            tripletsAfterTriplet,
            sequencesAfterTriplet)) 
        {
            // --- DEBUG 2a: Triplet Path Success ---
            Debug.Log($"[DECOMPOSE]    - PUNG PATH A: SUCCESS. Found all sets.");

            // 4. Path A Success: Update the 'ref' parameters and lists of the parent call
            sequencesFound = nextSequencesFound_A;
            tripletsFound = nextTripletsFound_A;
            currentTripletSortValues.Clear(); 
            currentTripletSortValues.AddRange(tripletsAfterTriplet); 
            currentSequenceRootSortValues.Clear(); 
            currentSequenceRootSortValues.AddRange(sequencesAfterTriplet); 
            
            return true; // Found a complete win on this path
        }
        // --- DEBUG 2b: Triplet Path Failure ---
        Debug.Log($"[DECOMPOSE]    - PUNG PATH A: FAILED. Backtracking to try Sequence.");
    }
    
    // --- Path B: Try to form a Sequence (Chow) using the first tile (3 sequential) ---
    
    bool isNumberedTile = suit == MahjongSuit.Circles || suit == MahjongSuit.Bamboos || suit == MahjongSuit.Characters;

    // Sequences are only possible for numbered tiles 1 through 7
    if (isNumberedTile && value <= 7 && counts.ContainsKey(firstTileSortValue)) 
    {
        int nextSortValue = firstTileSortValue + 1;
        int thirdSortValue = firstTileSortValue + 2;

        // Check if the three tiles needed for the sequence exist
        if (counts.ContainsKey(nextSortValue) && counts[nextSortValue] >= 1 &&
            counts.ContainsKey(thirdSortValue) && counts[thirdSortValue] >= 1)
        {
            // 1. Prepare remaining counts for Path B (Deep Copy)
            Dictionary<int, int> countsAfterSequence = new Dictionary<int, int>(counts);
            
            countsAfterSequence[firstTileSortValue] -= 1;
            countsAfterSequence[nextSortValue] -= 1;
            countsAfterSequence[thirdSortValue] -= 1;

            if (countsAfterSequence[firstTileSortValue] == 0) countsAfterSequence.Remove(firstTileSortValue);
            if (countsAfterSequence[nextSortValue] == 0) countsAfterSequence.Remove(nextSortValue);
            if (countsAfterSequence[thirdSortValue] == 0) countsAfterSequence.Remove(thirdSortValue);
            
            // 2. Prepare local analysis variables for Path B
            int nextSequencesFound_B = sequencesFound + 1; 
            int nextTripletsFound_B = tripletsFound; 

            List<int> sequencesAfterSequence = new List<int>(currentSequenceRootSortValues);
            sequencesAfterSequence.Add(firstTileSortValue);
            
            List<int> tripletsAfterSequence = new List<int>(currentTripletSortValues);
            
            // 3. Recurse down Path B
            Debug.Log($"[DECOMPOSE]    - CHOW PATH B: Attempting Chow {firstTileSortValue}-{nextSortValue}-{thirdSortValue}. Sets remaining: {setsToFind - 1}");

            if (CanFormSetsAndAnalyze(
                countsAfterSequence, 
                setsToFind - 1, 
                ref nextSequencesFound_B, 
                ref nextTripletsFound_B, 
                tripletsAfterSequence,
                sequencesAfterSequence))
            {
                // --- DEBUG 3a: Sequence Path Success ---
                Debug.Log($"[DECOMPOSE]    - CHOW PATH B: SUCCESS. Found all sets.");

                // 4. Path B Success: Update the 'ref' parameters and lists of the parent call
                sequencesFound = nextSequencesFound_B;
                tripletsFound = nextTripletsFound_B;
                currentSequenceRootSortValues.Clear();
                currentSequenceRootSortValues.AddRange(sequencesAfterSequence);
                currentTripletSortValues.Clear();
                currentTripletSortValues.AddRange(tripletsAfterSequence); 

                return true; // Found a complete win on this path
            }
            // --- DEBUG 3b: Sequence Path Failure ---
            Debug.Log($"[DECOMPOSE]    - CHOW PATH B: FAILED.");
        }
    }
    
    // Failure: Neither path starting with firstTileSortValue led to a successful decomposition.
    Debug.Log($"[DECOMPOSE] FAILURE at {firstTileSortValue}. No path successful.");
    return false;
}
    }
}