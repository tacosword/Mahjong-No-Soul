using System;
using System.Collections.Generic;

// =================================================================
// IMPORTANT: Must be marked [Serializable] for network transmission
// =================================================================

/// <summary>
/// Stores the outcome of the hand analysis, including the set types found.
/// Serializable for network transmission.
/// </summary>
[Serializable]
public class HandAnalysisResult
{
    // --- Winning Hand Types ---
    public bool IsWinningHand = false;
    public bool IsTraditionalWin = false;
    public bool Is13OrphansWin = false;
    public bool Is7PairsWin = false;

    // --- Hand Attributes ---
    public bool IsPureHand = false;
    public bool IsHalfHand = false;

    // --- Bonus Tile Tracking ---
    public int FlowerCount = 0;

    // --- Traditional Win Decomposition Details ---
    public int SequencesCount = 0;
    public int TripletsCount = 0;

    // --- Traditional Win Decomposition Details (Specific Tile Values) ---
    public int PairSortValue = 0;
    public List<int> TripletSortValues = new List<int>();
    public List<int> SequenceRootSortValues = new List<int>();
}