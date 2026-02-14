using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using Microsoft.VisualBasic;
using UnityEngine;

// NOTE: MahjongSuit enum and TileData class are assumed to be defined elsewhere.
// NOTE: CompletedMeld class is assumed to be defined elsewhere.

// =================================================================
// NEW PLAYER HAND CLASS - REBUILT FROM SCRATCH
// =================================================================
public class PlayerHand : MonoBehaviour
{
    // --- Hand State ---
    private List<TileData> handTiles = new List<TileData>();
    private TileData drawnTile = null;
    private List<TileData> flowerTiles = new List<TileData>();
    private List<TileData> meldedKongs = new List<TileData>();
    
    // Completed melds (Chi/Pon/Kong from discards)
    public List<CompletedMeld> CompletedMelds { get; set; } = new List<CompletedMeld>();
    
    // --- Public Properties ---
    public List<TileData> HandTiles => handTiles;
    public List<TileData> FlowerTiles => flowerTiles;
    public TileData DrawnTile => drawnTile;
    public List<TileData> MeldedKongs => meldedKongs;
    
    // --- Constants for Wind/Dragon identification ---
    public static int PLAYER_WIND_SORT_VALUE = 402;
    public static int ROUND_WIND_SORT_VALUE = 401;
    public readonly int[] DRAGON_SORT_VALUES = { 501, 502, 503 };

    
    public static int MY_FLOWER_NUMBER = 1;
    public bool isHandPure = false;

    public List<List<TileData>> ValidMelds = new List<List<TileData>>();
    
    // --- Hand Management Methods ---
    public void AddToHand(TileData tile)
    {
        if (tile != null) handTiles.Add(tile);
    }
    
    public void CollectFlower(TileData tile)
    {
        if (tile != null) flowerTiles.Add(tile);
    }
    
    public void SetDrawnTile(TileData tile)
    {
        drawnTile = tile;
    }
    
    public void MoveDrawnTileToHand()
    {
        if (drawnTile != null)
        {
            handTiles.Add(drawnTile);
            drawnTile = null;
        }
    }
    
    public TileData DiscardFromHand(TileData tileToDiscard)
    {
        if (handTiles.Remove(tileToDiscard))
            return tileToDiscard;
        return null;
    }
    
    public TileData DiscardDrawnTile()
    {
        TileData discarded = drawnTile;
        drawnTile = null;
        return discarded;
    }
    
    public void SortHand()
    {
        handTiles.Sort((a, b) => a.GetSortValue().CompareTo(b.GetSortValue()));
        flowerTiles.Sort((a, b) => a.GetSortValue().CompareTo(b.GetSortValue()));
    }
    
    public void AddMeldedKong(List<TileData> kongSet)
    {
        if (kongSet == null || kongSet.Count != 4) return;
        Debug.Log($"[PlayerHand] Adding {kongSet.Count} tiles to meldedKongs.");
        meldedKongs.AddRange(kongSet);
    }
    
    // =================================================================
    // NEW TRADITIONAL MAHJONG STRUCTURE VALIDATION
    // =================================================================
    
    /// <summary>
    /// Main function to check if the player's hand follows traditional mahjong structure.
    /// Returns true if the hand is valid with exactly one pair and the rest in pons/chis.
    /// </summary>
    public bool CheckTraditionalMahjongStructure(List<TileData> tiles)
    {
        // Filter out flower tiles
        List<TileData> functionalTiles = tiles.Where(t => 
            t.suit != MahjongSuit.RedFlowers && t.suit != MahjongSuit.BlueFlower).ToList();
        
        Debug.Log($"[CheckTraditional] Starting validation with {functionalTiles.Count} functional tiles");
        
        List<List<TileData>> pons = new List<List<TileData>>();

        HashSet<TileData> UniqueTiles = new HashSet<TileData>(functionalTiles);

        var groupedBySortValue = functionalTiles.GroupBy(s => s.GetSortValue());
        List<List<TileData>> pairs = new List<List<TileData>>();

        foreach (var group in groupedBySortValue)
        {
            List<TileData> tilesInGroup = group.ToList();
            
            if (tilesInGroup.Count >= 2)
            {
                pairs.Add(new List<TileData> { tilesInGroup[0], tilesInGroup[1] });
                
                if (tilesInGroup.Count >= 3)
                {
                    pons.Add(new List<TileData> { tilesInGroup[0], tilesInGroup[1], tilesInGroup[2] });
                }
            }
        } 

        List<List<TileData>> chis = new List<List<TileData>>();

        List<TileData> sortedTiles = tiles.OrderBy(t => t.GetSortValue()).ToList();

        foreach (TileData uniqueTile in UniqueTiles)
        {
            if (uniqueTile.GetSortValue() >= 400)
            {
                continue;
            }

            int count = 0;
            foreach (TileData tile in functionalTiles)
            {
                if (uniqueTile.GetSortValue() == tile.GetSortValue())
                {
                    count += 1;
                }
            }
            
            List<TileData> tilesCopy = new List<TileData>(functionalTiles);

            for (int i = 0; i < count; i++)
            {
                int startValue = uniqueTile.GetSortValue();

                TileData secondTile = tilesCopy.FirstOrDefault(t => t.GetSortValue() == startValue + 1);
                TileData thirdTile = tilesCopy.FirstOrDefault(t => t.GetSortValue() == startValue + 2);
                
                if (secondTile != null && thirdTile != null)
                {
                    List<TileData> chi = new List<TileData> { uniqueTile, secondTile, thirdTile };
                    chis.Add(chi);
                    tilesCopy.Remove(secondTile);
                    tilesCopy.Remove(thirdTile);
                }
                else
                {
                    continue;
                }
            }      
        }   

        List<List<TileData>> ponsAndChis = new List<List<TileData>>(pons);
        ponsAndChis.AddRange(chis);
        if (ponsAndChis.Count >= 4)
        {
            IEnumerable<IEnumerable<List<TileData>>> ponAndChiCombinations = ponsAndChis.Combinations(4);

            foreach(List<List<TileData>> combination in ponAndChiCombinations)
            {
                foreach(List<TileData> pair in pairs)
                {
                    List<List<TileData>> ListOfPonsChisPair = new List<List<TileData>>(combination);
                    ListOfPonsChisPair.Add(pair);
                    List<TileData> potentialHand = new List<TileData>();
                    ListOfPonsChisPair.ForEach(list => potentialHand.AddRange(list));
                    if (AreSameTiles(functionalTiles, potentialHand))
                    {
                        ValidMelds = ListOfPonsChisPair;
                        return true;
                    }
                }
            }
        }
        if (IsPureHand(tiles))
        {
            isHandPure = true;
            foreach(List<List<TileData>> combination in ponAndChiCombinations)
            {
                List<TileData> potentialHand = new List<TileData>();
                combination.ForEach(list => potentialHand.AddRange(list));

                if (ListChecker().ContainsAllElements(functionalTiles, potentialHand))
                {
                    ValidMelds = combination;
                }
            }
        }
        else
        {
            isHandPure = false;
            ValidMelds = new List<List<TileData>>();
        }
        return false;
    }
    
    public CalculateScore(List<TileData> tiles)
    {
        score = 1;
        bonusMessage = "Base Score: 1\n";

        List<TileData> flowerTiles = tiles.Where(t => 
            t.suit != MahjongSuit.Characters && t.suit != MahjongSuit.Circles && t.suit != MahjongSuit.Bamboos).ToList();

        numOfMyFlowerTiles = 0;
        numOfWrongFlowerTiles = 0;
        foreach (TileData tile in flowerTiles)
        {
            if (tile.GetSortValue() % 100 == MY_FLOWER_NUMBER)
            {
                numOfMyFlowerTiles += 1;
            }
            else
            {
                numOfWrongFlowerTiles += 1;
            }
        }
        if (numOfMyFlowerTiles == 2)
        {
            score += 1;
            bonusMessage += $"Good Flowers: +1\n";
        }
        else if (numOfMyFlowerTiles == 0 && numOfWrongFlowerTiles > 0)
        {
            score -= 1;
            bonusMessage += $"Bad Flower(s): -1\n";
        }

        List<TileData> functionalTiles = tiles.Where(t => 
            t.suit != MahjongSuit.RedFlowers && t.suit != MahjongSuit.BlueFlower).ToList();

        var groupedBySortValue = tiles.GroupBy(s => s.GetSortValue());
        List<List<TileData>> pons = new List<List<TileData>>();
        List<List<TileData>> chis = new List<List<TileData>>();
        List<TileData> pair = new List<TileData>();
        HashSet<MahjongSuit> suits = new HashSet<MahjongSuit>();
        foreach (var meld in ValidMelds)
        {
            if (meld.Count() >= 3 )
            {
                if (meld[0].GetSortValue() == meld[1].GetSortValue())
                {
                    pons.Add(new List<TileData> { meld[0], meld[1], meld[2] });
                }
                else
                {
                    chis.Add(new List<TileData> { meld[0], meld[1], meld[2] });
                }
            }
            else
            {
                pair = meld;
            }
            suits.Add(meld[0].suit);
        } 

        foreach (List<TileData> pon in pons)
        {
            if (pon[0].GetSortValue() == PLAYER_WIND_SORT_VALUE)
            {
                score += 1;
                bonusMessage += "Seat Wind: +1\n";
            }
            if (pon[0].GetSortValue() == ROUND_WIND_SORT_VALUE)
            {
                score += 1;
                bonusMessage += "Round Wind: +1\n";
            }
            foreach (int value in DRAGON_SORT_VALUES)
            {
                if (pon[0].GetSortValue() == value)
                {
                    score += 1;
                    bonusMessage += "Dragon Triplet: +1\n";
                }
            }
        } 
        if (pons.Count == 4)
            {
                score += 2;
                bonusMessage += "All Triples: +2\n";
            }
        if (chis.Count == 4)
            {
                score += 1;
                bonusMessage += "All Straights: +1\n";
            }
        if (IsPureHand(functionalTiles))
        {
            if (CheckTraditionalMahjongStructure(tiles))
            {
                score += 4;
                bonusMessage += "True Pure Hand: +4\n";
            }
            else
            {
                score += 3;
                bonusMessage += "Pure Hand: +3\n";
            }
        }

        bool isHalfHand = true;
        if (suits.Count == 2)
        {
            List<HashSet<MahjongSuit>> InvalidSuitCombinations = new List<HashSet<MahjongSuit>>( HashSet<MahjongSuit.Bamboos, MahjongSuit.Characters>, HashSet<MahjongSuit.Characters, MahjongSuit.Circles>, HashSet<MahjongSuit.Bamboos, MahjongSuit.Circles>, HashSet<MahjongSuit.Dragons, MahjongSuit.Winds> );
            foreach (var set in InvalidSuitCombinations)
            {
                if (suits == set)
                {
                    isHalfHand = false;
                }
            }
            if (isHalfHand)
            {
                score += 2;
                bonusMessage += "Half Hand: +2\n";
            }
        }
        HashSet<TileData> UniqueTiles = new HashSet<TileData>(functionalTiles);
        handSortValues = new List<int>();

        foreach (TileData tile in UniqueTiles)
        {
            handSortValues.Add(tile.GetSortValue());
        }
        List<HashSet<int>> validDragonSortValues = new List<HashSet<int>>( HashSet< 101, 102, 103, 104, 105, 106, 107, 108, 109 >, HashSet< 201, 202, 203, 204, 205, 206, 207, 208, 209 >, HashSet< 301, 302, 303, 304, 305, 306, 307, 308, 309 > );
        foreach (var validSet in validDragonSortValues)
        {
            if (validSet.IsSubsetOf(handSortValues))
            {
                score += 2;
                bonusMessage += "Dragon (Full Straight): +2\n";
            }
        }
        return (score, bonusMessage);
    }

    public bool IsPureHand(List<TileData> tiles)
    {
        MahjongSuit suit = tiles[0].suit;
        foreach (var tile in tiles)
            {
                if (suit != tile.suit)
                {
                    return false;
                }
            }
        return true;
    }

    public bool IsSevenpairs(List<TileData> tiles)
    {
        var groupedBySortValue = functionalTiles.GroupBy(s => s.GetSortValue());
        foreach (var group in groupedBySortValue)
        {
            List<TileData> tilesInGroup = group.ToList();
            
            if (tilesInGroup.Count != 2 && tilesInGroup.Count != 4)
            {
                return false;
            }
        }
        return true;
    }

    public bool IsThirteenOrphans(List<TileData> tiles)
    {
        HashSet<int> requiredSortValues = new HashSet<int> { 101, 109, 201, 209, 301, 309, 401, 402, 501, 502, 503 };
        HashSet<int> handSortValues = new HashSet<int>(tiles.Select(t => t.GetSortValue()));
        if (requiredSortValues.IsSubsetOf(handSortValues) && handSortValues.Count == 13)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Separates tiles into 5 suit groups: Circles, Bamboos, Characters, Winds, Dragons
    /// </summary>
    private Dictionary<MahjongSuit, List<TileData>> SeparateTilesBySuit(List<TileData> tiles)
    {
        Dictionary<MahjongSuit, List<TileData>> separated = new Dictionary<MahjongSuit, List<TileData>>();
        
        foreach (TileData tile in tiles)
        {
            MahjongSuit suit = tile.suit;
            
            if (!separated.ContainsKey(suit))
            {
                separated[suit] = new List<TileData>();
            }
            
            separated[suit].Add(tile);
        }
        
        return separated;
    }
    
    /// <summary>
    /// Checks if a single suit's tiles can form valid combinations (pons/chis/pair).
    /// Updates hasPair if this suit uses a pair in its valid combination.
    /// </summary>
    private bool CheckSuitCombinations(MahjongSuit suit, List<TileData> tilesInSuit, ref bool hasPair)
    {
        int tileCount = tilesInSuit.Count;
        
        // Check if tile count is valid (must be divisible by 3, or divisible by 3 with remainder 2 for pair)
        int remainder = tileCount % 3;
        
        if (remainder != 0 && remainder != 2)
        {
            Debug.Log($"[CheckSuit] Invalid tile count {tileCount} for suit {suit}. Remainder: {remainder}");
            return false; // Impossible to form valid combinations
        }
        
        // Determine if this suit can form sequences (only numbered suits)
        bool canFormSequences = IsNumberedSuit(suit);
        
        // Generate all possible pons
        List<List<TileData>> allPons = GenerateAllPons(tilesInSuit);
        Debug.Log($"[CheckSuit] Generated {allPons.Count} possible pons for {suit}");
        
        // Generate all possible chis (only for numbered suits)
        List<List<TileData>> allChis = new List<List<TileData>>();
        if (canFormSequences)
        {
            allChis = GenerateAllChis(tilesInSuit);
            Debug.Log($"[CheckSuit] Generated {allChis.Count} possible chis for {suit}");
        }
        
        // Generate all possible pairs
        List<List<TileData>> allPairs = GenerateAllPairs(tilesInSuit);
        Debug.Log($"[CheckSuit] Generated {allPairs.Count} possible pairs for {suit}");
        
        // Determine how many sets we need
        int setsNeeded = tileCount / 3;
        bool needsPair = (remainder == 2);
        
        Debug.Log($"[CheckSuit] Need {setsNeeded} sets, needsPair: {needsPair}, hasPair already: {hasPair}");
        
        // If we need a pair but already have one from a previous suit, return false
        if (needsPair && hasPair)
        {
            Debug.Log($"[CheckSuit] Already have a pair from previous suit, but {suit} needs a pair too!");
            return false;
        }
        
        // Try to find a valid combination
        if (needsPair)
        {
            // Need to find (setsNeeded) pons/chis + 1 pair
            bool foundValid = TryFindCombinationWithPair(tilesInSuit, allPons, allChis, allPairs, setsNeeded);
            
            if (foundValid)
            {
                hasPair = true; // Mark that we've used the pair for this suit
                Debug.Log($"[CheckSuit] Found valid combination with pair for {suit}");
                return true;
            }
            else
            {
                Debug.Log($"[CheckSuit] No valid combination with pair found for {suit}");
                return false;
            }
        }
        else
        {
            // Need to find exactly (setsNeeded) pons/chis, no pair
            bool foundValid = TryFindCombinationWithoutPair(tilesInSuit, allPons, allChis, setsNeeded);
            
            if (foundValid)
            {
                Debug.Log($"[CheckSuit] Found valid combination without pair for {suit}");
                return true;
            }
            else
            {
                Debug.Log($"[CheckSuit] No valid combination without pair found for {suit}");
                return false;
            }
        }
    }
    
    /// <summary>
    /// Generates all possible Pons (3 identical tiles) from the tile list
    /// </summary>
    private List<List<TileData>> GenerateAllPons(List<TileData> tiles)
    {
        List<List<TileData>> pons = new List<List<TileData>>();

        HashSet<TileData> UniqueTiles = new HashSet<TileData>(tiles);

        foreach (TileData uniqueTile in UniqueTiles)
        {
            int count = 0;
            foreach (TileData tile in tiles)
            {
                if (uniqueTile.GetSortValue() == tile.GetSortValue())
                {
                    count += 1;
                }
            }
            if (count >= 3)
            {
                pons.Add(new List<TileData> { uniqueTile, uniqueTile, uniqueTile });
            }
        }      
        return pons;
    }
    
    /// <summary>
    /// Generates all possible Chis (3 consecutive tiles) from the tile list
    /// Only works for numbered suits
    /// </summary>
    private List<List<TileData>> GenerateAllChis(List<TileData> tiles)
    {
        List<List<TileData>> chis = new List<List<TileData>>();

        HashSet<TileData> UniqueTiles = new HashSet<TileData>(tiles);

        List<TileData> sortedTiles = tiles.OrderBy(t => t.GetSortValue()).ToList();

        foreach (TileData uniqueTile in UniqueTiles)
        {
            if (uniqueTile.GetSortValue() >= 400)
            {
                continue;
            }

            int count = 0;
            foreach (TileData tile in tiles)
            {
                if (uniqueTile.GetSortValue() == tile.GetSortValue())
                {
                    count += 1;
                }
            }
            
            List<List<TileData>> tilesCopy = new List<string>(tiles);

            for (int i = 0; i < count; i++)
            {
                int startValue = uniqueTile.GetSortValue();

                TileData secondTile = tilesCopy.FirstOrDefault(t => t.GetSortValue() == startValue + 1);
                TileData thirdTile = tilesCopy.FirstOrDefault(t => t.GetSortValue() == startValue + 2);
                
                if (secondTile != null && thirdTile != null)
                {
                    List<TileData> chi = new List<TileData> { uniqueTile, secondTile, thirdTile };
                    chis.Add(chi);
                    tilesCopy.Remove(t => t.GetSortValue() == startValue + 1);
                    tilesCopy.Remove(t => t.GetSortValue() == startValue + 2);
                }
                else
                {
                    continue;
                }
            }      
        }
        return chis;
    }
    
    /// <summary>
    /// Generates all possible Pairs (2 identical tiles) from the tile list
    /// </summary>
    private List<List<TileData>> GenerateAllPairs(List<TileData> tiles)
    {
        List<List<TileData>> pairs = new List<List<TileData>>();

        HashSet<TileData> UniqueTiles = new HashSet<TileData>(tiles);

        foreach (TileData uniqueTile in UniqueTiles)
        {
            int count = 0;
            foreach (TileData tile in tiles)
            {
                if (uniqueTile.GetSortValue() == tile.GetSortValue())
                {
                    count += 1;
                }
            }
            if (count >= 2)
            {
                pairs.Add(new List<TileData> { uniqueTile, uniqueTile });
            }
        }      
        return pairs;
    }
    
    /// <summary>
    /// Tries to find a valid combination using pons/chis that exactly matches the tiles, with a pair
    /// </summary>
    private bool TryFindCombinationWithPair(List<TileData> originalTiles, List<List<TileData>> allPons, 
                                           List<List<TileData>> allChis, List<List<TileData>> allPairs, 
                                           int setsNeeded)
    {
        // Try each possible pair
        foreach (List<TileData> pair in allPairs)
        {
            // Try to find (setsNeeded) sets from the remaining tiles after using this pair
            List<TileData> remainingTiles = new List<TileData>(originalTiles);
            
            // Remove the pair tiles
            foreach (TileData tile in pair)
            {
                remainingTiles.Remove(tile);
            }
            
            // Now try to form exactly setsNeeded sets from remaining tiles
            if (TryFormExactSets(remainingTiles, allPons, allChis, setsNeeded, new List<List<TileData>>()))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Tries to find a valid combination using only pons/chis that exactly matches the tiles, without a pair
    /// </summary>
    private bool TryFindCombinationWithoutPair(List<TileData> originalTiles, List<List<TileData>> allPons, 
                                               List<List<TileData>> allChis, int setsNeeded)
    {
        return TryFormExactSets(originalTiles, allPons, allChis, setsNeeded, new List<List<TileData>>());
    }
    
    /// <summary>
    /// Recursive function to try forming exactly N sets from the given tiles using available pons and chis
    /// </summary>
    private bool TryFormExactSets(List<TileData> remainingTiles, List<List<TileData>> allPons, 
                                 List<List<TileData>> allChis, int setsNeeded, List<List<TileData>> currentCombination)
    {
        // Base case: if we've formed all needed sets
        if (setsNeeded == 0)
        {
            // Check if we've used all tiles
            return remainingTiles.Count == 0;
        }
        
        // If no tiles left but still need sets, fail
        if (remainingTiles.Count == 0)
        {
            return false;
        }
        
        // Try each pon
        foreach (List<TileData> pon in allPons)
        {
            // Check if this pon can be formed from remaining tiles
            if (CanFormSet(remainingTiles, pon))
            {
                // Create a new remaining tiles list without this pon
                List<TileData> newRemaining = new List<TileData>(remainingTiles);
                foreach (TileData tile in pon)
                {
                    newRemaining.Remove(tile);
                }
                
                // Create new combination list with this pon
                List<List<TileData>> newCombination = new List<List<TileData>>(currentCombination);
                newCombination.Add(pon);
                
                // Recurse
                if (TryFormExactSets(newRemaining, allPons, allChis, setsNeeded - 1, newCombination))
                {
                    return true;
                }
            }
        }
        
        // Try each chi
        foreach (List<TileData> chi in allChis)
        {
            // Check if this chi can be formed from remaining tiles
            if (CanFormSet(remainingTiles, chi))
            {
                // Create a new remaining tiles list without this chi
                List<TileData> newRemaining = new List<TileData>(remainingTiles);
                foreach (TileData tile in chi)
                {
                    newRemaining.Remove(tile);
                }
                
                // Create new combination list with this chi
                List<List<TileData>> newCombination = new List<List<TileData>>(currentCombination);
                newCombination.Add(chi);
                
                // Recurse
                if (TryFormExactSets(newRemaining, allPons, allChis, setsNeeded - 1, newCombination))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Checks if a set (pon/chi) can be formed from the available tiles
    /// </summary>
    private bool CanFormSet(List<TileData> availableTiles, List<TileData> set)
    {
        List<TileData> tempAvailable = new List<TileData>(availableTiles);
        
        foreach (TileData tileInSet in set)
        {
            // Try to find a matching tile in available tiles
            TileData matchingTile = tempAvailable.FirstOrDefault(t => 
                t.GetSortValue() == tileInSet.GetSortValue());
            
            if (matchingTile == null)
            {
                return false; // Can't form this set
            }
            
            tempAvailable.Remove(matchingTile);
        }
        
        return true; // All tiles in the set were found
    }
    
    /// <summary>
    /// Checks if two tile lists contain the same tiles (by reference)
    /// </summary>
    private bool AreSameTiles(List<TileData> list1, List<TileData> list2)
    {
        if (list1.Count != list2.Count) return false;
        
        foreach (TileData tile in list1)
        {
            if (!list2.Contains(tile)) return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Determines if a suit is a numbered suit (can form sequences)
    /// </summary>
    private bool IsNumberedSuit(MahjongSuit suit)
    {
        return suit == MahjongSuit.Circles || 
               suit == MahjongSuit.Bamboos || 
               suit == MahjongSuit.Characters;
    }

    public static class CombinationExtensions
    {
        public static IEnumerable<IEnumerable<T>> Combinations<T>(this IEnumerable<T> elements, int k)
        {
            return k == 0 ? new[] { Enumerable.Empty<T>() } :
            elements.SelectMany((e, i) =>
                elements.Skip(i + 1).Combinations(k - 1).Select(c => new[] { e }.Concat(c)));
        }
    }

    public class ListChecker
    {
        public static bool ContainsAllElements<T>(List<T> supersetList, List<T> subsetList)
        {
            // Check if all items in subsetList are present in supersetList
            return subsetList.All(item => supersetList.Contains(item));
        }
    }
}
