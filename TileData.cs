using UnityEngine;

// Define the different suits for Mahjong
public enum MahjongSuit {
    Invalid = 0, // Placeholder for errors
    Circles = 2,
    Bamboos = 3,
    Characters = 1,
    Winds = 4,
    Dragons = 5,
    RedFlowers = 7, // Optional: if you use Flower/Season tiles
    BlueFlower = 6  // Optional
}

public class TileData : MonoBehaviour
{
    public MahjongSuit suit = MahjongSuit.Invalid;
    public int value = 0; 

    // ADD THIS LINE TO FIX THE ERROR
    public Sprite tileSprite; 

    public int GetSortValue()
    {
        return ((int)suit * 100) + value;
    }

    // Helper to check if a tile can form a sequence (numbered tiles 1-9)
    public bool IsSuitTile()
    {
        return suit == MahjongSuit.Circles || 
               suit == MahjongSuit.Bamboos || 
               suit == MahjongSuit.Characters;
    }
}