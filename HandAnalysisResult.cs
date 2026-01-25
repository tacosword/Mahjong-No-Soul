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
    public bool IsPureHand { get; set; } = false; 
    public bool IsHalfHand { get; set; } = false;

    // --- NEW: Win Condition Bonuses ---
    public bool IsAllHidden { get; set; } = false; // All tiles self-drawn (Menzen Tsumo)
    public bool IsAllShown { get; set; } = false;  // All tiles from melds (Toitoi Hoitei)

    // --- NEW: Bonus Tile Tracking ---
    public int FlowerCount { get; set; } = 0; // Added for ResultScreenUI

    // --- Traditional Win Decomposition Details ---
    public int SequencesCount = 0;
    public int TripletsCount = 0;

    // --- Traditional Win Decomposition Details (Specific Tile Values) ---
    public int PairSortValue = 0;
    public List<int> TripletSortValues = new List<int>();
    public List<int> SequenceRootSortValues = new List<int>();
}