using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// =================================================================
// !!! NEW STATIC CLASS: HAND GENERATOR !!!
// (This should be placed in a separate C# file named HandGenerator.cs)
// =================================================================
public static class HandGenerator
{
    // WARNING: These constants MUST match the sort values defined in your TileData/Mahjong system.
    private const int WIND_EAST = 401;
    private const int WIND_SOUTH = 402;
    private const int DRAGON_RED = 501;
    private const int DRAGON_GREEN = 502;
    private const int DRAGON_WHITE = 503;
    
    // Base values for the three suited tiles (Man, Pin, So)
    private static readonly int[] SUITS = { 100, 200, 300 };
    // Includes all Winds (401-404) and Dragons (501-503)
    private static readonly int[] HONOR_TILES = { 401, 402, 403, 404, DRAGON_RED, DRAGON_GREEN, DRAGON_WHITE };
    
    // All possible functional tiles (101-109, 201-209, 301-309, 401-404, 501-503)
    private static readonly List<int> ALL_FUNCTIONAL_TILES = GetAllFunctionalTiles();
    
    // Tiles that can form a 13 Orphans hand (Terminals & Honors)
    private static readonly List<int> TERMINAL_AND_HONOR_TILES = new List<int>
    {
        101, 109, 201, 209, 301, 309, // Terminals
        401, 402, 403, 404, // Winds
        501, 502, 503 // Dragons
    };

    // Helper to initialize all possible tiles, used for the 7 Pairs method.
    private static List<int> GetAllFunctionalTiles()
    {
        List<int> allTiles = new List<int>();
        foreach (var suitBase in SUITS)
        {
            for (int i = 1; i <= 9; i++)
            {
                allTiles.Add(suitBase + i);
            }
        }
        allTiles.AddRange(HONOR_TILES);
        return allTiles;
    }

    /// <summary>
    /// Generates a list of 14 tile sort values that form a valid winning hand.
    /// </summary>
    public static List<int> GenerateRandomWinningHandSortValues()
    {
        // ... (Existing implementation remains the same) ...
        List<int> hand = new List<int>();
        System.Random random = new System.Random();
        
        // 1. Choose the pair (2 tiles)
        int pairSortValue = GetRandomSortValue(random);
        hand.Add(pairSortValue);
        hand.Add(pairSortValue);
        
        // 2. Generate the remaining 4 sets (12 tiles)
        for (int i = 0; i < 4; i++)
        {
            int setType = random.Next(2); // 0 = Triplet (Pung), 1 = Sequence (Chow)
            
            if (setType == 0)
            {
                // Generate a Triplet (Pung) - Three identical tiles
                int tripletSortValue = GetRandomSortValue(random);
                hand.Add(tripletSortValue);
                hand.Add(tripletSortValue);
                hand.Add(tripletSortValue);
            }
            else
            {
                // Generate a Sequence (Chow) - Three consecutive numbered tiles in the same suit
                int suitIndex = random.Next(SUITS.Length);
                int baseSuit = SUITS[suitIndex];
                int startValue = random.Next(1, 8);
                
                hand.Add(baseSuit + startValue);
                hand.Add(baseSuit + startValue + 1);
                hand.Add(baseSuit + startValue + 2);
            }
        }

        if (hand.Count != 14)
        {
            Debug.LogError($"Hand Generator error: Generated {hand.Count} tiles, expected 14. Retrying.");
            return GenerateRandomWinningHandSortValues();
        }
        
        return hand.OrderBy(x => random.Next()).ToList();
    }
    
    // =================================================================
    // EXISTING: PURE HAND GENERATOR
    // =================================================================
    public static List<int> GeneratePureHandSortValues()
    {
        // ... (Existing implementation remains the same) ...
        List<int> hand = new List<int>();
        System.Random random = new System.Random();

        // 1. Randomly choose one of the three numbered suits (100, 200, or 300 base)
        int suitIndex = random.Next(SUITS.Length);
        int baseSuit = SUITS[suitIndex];

        // 2. Choose the pair (2 tiles) - must be a numbered tile (1-9) within the chosen suit
        int pairValue = random.Next(1, 10);
        int pairSortValue = baseSuit + pairValue;
        hand.Add(pairSortValue);
        hand.Add(pairSortValue);
        
        // 3. Generate the remaining 4 sets (12 tiles)
        for (int i = 0; i < 4; i++)
        {
            int setType = random.Next(2); // 0 = Triplet (Pung), 1 = Sequence (Chow)
            
            if (setType == 0)
            {
                // Generate a Triplet (Pung) - must be a numbered tile (1-9)
                int tripletValue = random.Next(1, 10);
                int tripletSortValue = baseSuit + tripletValue;
                
                hand.Add(tripletSortValue);
                hand.Add(tripletSortValue);
                hand.Add(tripletSortValue);
            }
            else
            {
                // Generate a Sequence (Chow) - must be a valid sequence (1-7 start)
                int startValue = random.Next(1, 8);
                
                hand.Add(baseSuit + startValue);
                hand.Add(baseSuit + startValue + 1);
                hand.Add(baseSuit + startValue + 2);
            }
        }

        // 4. Ensure exactly 14 tiles
        if (hand.Count != 14)
        {
            Debug.LogError($"Pure Hand Generator error: Generated {hand.Count} tiles, expected 14. Retrying.");
            return GeneratePureHandSortValues(); // Recurse on failure
        }
        
        // 5. Randomly shuffle the final 14 tiles and return
        return hand.OrderBy(x => random.Next()).ToList();
    }
    
    // =================================================================
    // NEW: SEVEN PAIRS IISHANTEN GENERATOR
    // =================================================================
    /// <summary>
    /// Generates a 13-tile hand that is one tile away from a Seven Pairs win (6 pairs and 1 single).
    /// </summary>
    /// <param name="waitingTileValue">Outputs the sort value of the tile needed to complete the win.</param>
    /// <returns>A list of 13 tile sort values.</returns>
    public static List<int> Generate7PairsIishantenSortValues(out int waitingTileValue)
    {
        System.Random random = new System.Random();
        List<int> availableTiles = new List<int>(ALL_FUNCTIONAL_TILES);
        List<int> hand = new List<int>();

        // 1. Generate 6 distinct pairs (12 tiles)
        for (int i = 0; i < 6; i++)
        {
            // Select a tile that hasn't been used for a pair yet
            if (availableTiles.Count == 0) 
            {
                 // This should only happen if the ALL_FUNCTIONAL_TILES list is smaller than expected.
                 Debug.LogError("Not enough unique tiles to form 6 pairs.");
                 waitingTileValue = 0;
                 return new List<int>();
            }
            
            int tileIndex = random.Next(availableTiles.Count);
            int pairSortValue = availableTiles[tileIndex];
            
            hand.Add(pairSortValue);
            hand.Add(pairSortValue);

            // Remove the selected tile from the pool of available tiles to ensure distinct pairs
            availableTiles.RemoveAt(tileIndex); 
        }

        // 2. Add the 13th tile (the single tile)
        if (availableTiles.Count == 0)
        {
            // If all 34 tile types were used, this would happen. In standard Mahjong, it won't.
            Debug.LogError("No tiles left for the single tile.");
            waitingTileValue = 0;
            return new List<int>();
        }

        int singleTileIndex = random.Next(availableTiles.Count);
        int singleTileValue = availableTiles[singleTileIndex];
        
        hand.Add(singleTileValue); // The single tile

        // 3. The waiting tile is the duplicate of the single tile
        waitingTileValue = singleTileValue;
        
        // 4. Ensure exactly 13 tiles and shuffle
        if (hand.Count != 13)
        {
            Debug.LogError($"7 Pairs Iishanten Generator error: Generated {hand.Count} tiles, expected 13. Retrying.");
            return Generate7PairsIishantenSortValues(out waitingTileValue);
        }
        
        return hand.OrderBy(x => random.Next()).ToList();
    }


    // =================================================================
    // NEW: 13 ORPHANS IISHANTEN GENERATOR
    // =================================================================
    /// <summary>
    /// Generates a 13-tile hand that is one tile away from a Thirteen Orphans win.
    /// It consists of 12 required unique tiles and 1 pair, OR all 13 unique tiles and no pair.
    /// This implementation uses the 12 singles + 1 incomplete set approach for simplicity (13 tiles total).
    /// </summary>
    /// <param name="waitingTileValue">Outputs the sort value of the tile needed to complete the win.</param>
    /// <returns>A list of 13 tile sort values.</returns>
    public static List<int> Generate13OrphansIishantenSortValues(out int waitingTileValue)
    {
        System.Random random = new System.Random();
        List<int> requiredTiles = new List<int>(TERMINAL_AND_HONOR_TILES);
        List<int> hand = new List<int>();

        // 1. Choose the tile that will be MISSING its pair/single.
        // This tile will be the 'waiting' tile.
        int waitingTileIndex = random.Next(requiredTiles.Count);
        waitingTileValue = requiredTiles[waitingTileIndex];

        // 2. Generate the 13 tiles that are NOT the final winning tile.
        // We will generate the 13 required tiles, then make one a pair and the waiting tile a single.
        
        // Approach: 
        // a. Start with all 13 unique tiles.
        // b. Choose one random tile (not the waiting tile) to be the "extra" tile (the 14th tile of the perfect hand).
        // c. The hand will contain:
        //    - 1 copy of the 11 "other" tiles. (11 tiles)
        //    - 2 copies of the "extra" tile (to form the pair). (2 tiles)
        //    - 0 copies of the "waiting" tile. (0 tiles)
        
        List<int> uniqueTilesForHand = new List<int>(requiredTiles);
        uniqueTilesForHand.Remove(waitingTileValue); // 12 tiles left

        // Choose which of the remaining 12 unique tiles will be the pair-starter (the 13th tile in the hand)
        int extraTileIndex = random.Next(uniqueTilesForHand.Count);
        int extraTileValue = uniqueTilesForHand[extraTileIndex];

        // 3. Construct the 13-tile Iishanten hand
        
        // Add all 12 unique tiles (11 singles + 1 pair)
        foreach (int tileValue in requiredTiles)
        {
            if (tileValue != waitingTileValue)
            {
                hand.Add(tileValue);
                // Add the extra copy for the pair
                if (tileValue == extraTileValue)
                {
                    hand.Add(tileValue);
                }
            }
        }

        // 4. Final check and shuffle
        if (hand.Count != 13)
        {
            Debug.LogError($"13 Orphans Iishanten Generator error: Generated {hand.Count} tiles, expected 13. Retrying.");
            // Recurse until successful
            return Generate13OrphansIishantenSortValues(out waitingTileValue); 
        }

        Debug.Log($"13 Orphans Iishanten generated. Waiting for tile: {waitingTileValue}. Extra tile (pair): {extraTileValue}.");
        return hand.OrderBy(x => random.Next()).ToList();
    }

    // ... (SelectOneTileToDiscard and GetRandomSortValue remain the same) ...

    /// <summary>
    /// Randomly selects one tile from the list, removes it, and returns its value.
    /// The remaining list becomes the 13-tile 'Iishanten' hand.
    /// </summary>
    /// <param name="perfectHand">A list of 14 sort values that form a winning hand.</param>
    /// <returns>The sort value of the tile that was removed (the 'waiting' tile).</returns>
    public static int SelectOneTileToDiscard(List<int> perfectHand)
    {
        if (perfectHand.Count != 14)
        {
            Debug.LogError($"SelectOneTileToDiscard error: Expected 14 tiles, got {perfectHand.Count}.");
            return 0;
        }

        System.Random random = new System.Random();
        
        // Select a random index from 0 to 13
        int randomIndex = random.Next(perfectHand.Count);

        // Get the value of the tile at that index
        int removedTileValue = perfectHand[randomIndex];

        // Remove the tile from the list
        perfectHand.RemoveAt(randomIndex);
        
        // The remaining 13 tiles in the list are the initial hand
        return removedTileValue;
    }

    /// <summary>
    /// Gets a random valid tile sort value (1-9 in circles/bamboo/chars, or Honors).
    /// Flower tiles are intentionally excluded.
    /// </summary>
    private static int GetRandomSortValue(System.Random random)
    {
        // 50% chance for a numbered tile, 50% chance for an Honor tile
        if (random.Next(2) == 0)
        {
            // Numbered Tile (1xx, 2xx, or 3xx)
            int suitIndex = random.Next(SUITS.Length);
            int baseSuit = SUITS[suitIndex];
            int value = random.Next(1, 10); // 1 to 9
            return baseSuit + value;
        }
        else
        {
            // Honor Tile (4xx or 5xx)
            int honorIndex = random.Next(HONOR_TILES.Length);
            return HONOR_TILES[honorIndex];
        }
    }

    // --- INSIDE a static HandGenerator or GameSetupHelper class ---

/// <summary>
/// Generates an initial hand setup that includes a 4-tile Kong set.
/// </summary>
/// <param name="kongTileSortValue">The sort value of the tile used for the Kong (e.g., Red Dragon: 501).</param>
/// <param name="remainingHandSortValues">Returns the sort values of the remaining 10 tiles.</param>
/// <returns>A list containing the sort values of the 4 Kong tiles.</returns>
    public static List<int> GenerateHandWithForcedKong(int kongTileSortValue, out List<int> remainingHandSortValues)
    {
        // 1. Define the Kong set
        List<int> kongTiles = new List<int>
        {
            kongTileSortValue, kongTileSortValue, kongTileSortValue, kongTileSortValue
        };
        
        // 2. Generate the remaining 10 tiles for the hand (must be 10!)
        remainingHandSortValues = new List<int>();
        
        // Example: Generate 10 random, non-winning tiles.
        // In a real game, you would draw these from the 'wall' and ensure they don't match the Kong tile.
        
        // Example Hand (10 tiles, simple set): 1,1,2,3 Circles; 1,2,3 Bamboos; North, South, West Winds
        remainingHandSortValues.Add(101); remainingHandSortValues.Add(101); // Pair
        remainingHandSortValues.Add(102); remainingHandSortValues.Add(103); // Sequence
        remainingHandSortValues.Add(201); remainingHandSortValues.Add(202); remainingHandSortValues.Add(203); // Sequence
        remainingHandSortValues.Add(401); remainingHandSortValues.Add(402); remainingHandSortValues.Add(403); // Triplet

        // IMPORTANT: Remove the Kong tile value from the remaining list if it accidentally exists
        remainingHandSortValues = remainingHandSortValues
            .Where(val => val != kongTileSortValue)
            .ToList();
            
        // Ensure we have exactly 10 tiles for the hand
        if (remainingHandSortValues.Count > 10)
        {
            remainingHandSortValues = remainingHandSortValues.Take(10).ToList();
        }
        while (remainingHandSortValues.Count < 10)
        {
            // Add a random tile until we reach 10 (needs to be safe, e.g., Green Dragon: 502)
            remainingHandSortValues.Add(502); 
        }

        return kongTiles;
    }
}