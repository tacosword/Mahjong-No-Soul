using System.Collections.Generic;

/// <summary>
/// Types of actions a player can take when another player discards.
/// </summary>
public enum InterruptActionType
{
    None,  // No action / default state
    Pass,  // Explicitly pass on interrupt
    Chi,   // Sequence (chow)
    Pon,   // Triplet (pung)
    Kong,  // Quad (kan)
    Ron    // Win on opponent's discard (not implemented yet, but enum needs it)
}

/// <summary>
/// Data for a Chi option (which tiles to use for the sequence).
/// </summary>
[System.Serializable]
public class ChiOption
{
    public int discardedTile;           // The tile that was discarded
    public int tile1SortValue;          // First tile from hand
    public int tile2SortValue;          // Second tile from hand
    
    public ChiOption(int discarded, int t1, int t2)
    {
        discardedTile = discarded;
        tile1SortValue = t1;
        tile2SortValue = t2;
    }
    
    /// <summary>
    /// Get all three tiles in the sequence in sorted order.
    /// </summary>
    public List<int> GetSequenceSorted()
    {
        List<int> tiles = new List<int> { discardedTile, tile1SortValue, tile2SortValue };
        tiles.Sort();
        return tiles;
    }
}

/// <summary>
/// Represents a completed meld (Pon, Chi, or Kong).
/// </summary>
[System.Serializable]
public class CompletedMeld
{
    public InterruptActionType Type;
    public List<int> TileSortValues; // All tiles in the meld
    public int CalledTileSortValue;  // Which tile was called from discard
    
    public CompletedMeld(InterruptActionType type, List<int> tiles, int calledTile)
    {
        Type = type;
        TileSortValues = new List<int>(tiles);
        CalledTileSortValue = calledTile;
    }
}