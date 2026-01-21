using UnityEngine;
using Mirror;

public class NetworkedClickableTile : MonoBehaviour
{
    // REMOVE the cached playerHand field - we'll get it fresh each time
    // private NetworkedPlayerHand playerHand;  ← DELETE THIS
    
    void Start()
    {
        // We no longer cache the reference in Start()
        // Just verify we can find it
        NetworkedPlayerHand hand = GetLocalPlayerHand();
        if (hand == null)
        {
            Debug.LogError($"[ClickableTile] Could not find local player hand in Start!");
        }
        else
        {
            Debug.Log($"[ClickableTile] Found local player hand: {hand.gameObject.name}");
        }
    }
    
    /// <summary>
    /// Get the local player's hand FRESH every time
    /// </summary>
    private NetworkedPlayerHand GetLocalPlayerHand()
    {
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        if (localPlayer == null)
        {
            return null;
        }
        
        return localPlayer.GetComponent<NetworkedPlayerHand>();
    }

    void OnMouseDown()
    {
        Debug.Log($"[ClickableTile] ========================================");
        Debug.Log($"[ClickableTile] OnMouseDown called");
        
        // GET FRESH REFERENCE
        NetworkedPlayerHand playerHand = GetLocalPlayerHand();
        
        if (playerHand == null)
        {
            Debug.LogError($"[ClickableTile] playerHand is NULL!");
            Debug.Log($"[ClickableTile] ========================================");
            return;
        }
        
        Debug.Log($"[ClickableTile] Found playerHand: {playerHand.gameObject.name}");

        TileData tileData = GetComponent<TileData>();
        if (tileData == null)
        {
            Debug.LogError($"[ClickableTile] TileData is NULL!");
            Debug.Log($"[ClickableTile] ========================================");
            return;
        }
        
        int tileValue = tileData.GetSortValue();
        Debug.Log($"[ClickableTile] Tile value: {tileValue}");

        // CHECK CHI SELECTION MODE
        bool inChiMode = playerHand.IsInChiSelectionMode;
        Debug.Log($"[ClickableTile] IsInChiSelectionMode: {inChiMode}");

        // PRIORITY 1: CHI SELECTION MODE
        if (inChiMode)
        {
            Debug.Log($"[ClickableTile] IN CHI SELECTION MODE");
            Debug.Log($"[ClickableTile] Calling OnChiTileClicked({tileValue})");
            playerHand.OnChiTileClicked(tileValue);
            Debug.Log($"[ClickableTile] ========================================");
            return;
        }
        
        Debug.Log($"[ClickableTile] Not in Chi selection mode");

        // PRIORITY 2: CHECK IF IT'S OUR TURN
        if (NetworkedGameManager.Instance != null)
        {
            NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
            if (localPlayer == null)
            {
                Debug.LogError($"[ClickableTile] Local player not found!");
                Debug.Log($"[ClickableTile] ========================================");
                return;
            }

            int localSeat = localPlayer.PlayerIndex;
            int currentTurn = NetworkedGameManager.Instance.CurrentPlayerIndex;
            
            Debug.Log($"[ClickableTile] My seat: {localSeat}, Current turn: {currentTurn}");

            if (localSeat != currentTurn)
            {
                Debug.Log($"[ClickableTile] Not your turn - ignoring click");
                Debug.Log($"[ClickableTile] ========================================");
                return;
            }
            
            Debug.Log($"[ClickableTile] It IS my turn, proceeding...");
        }

        // PRIORITY 3: KONG SELECTION MODE
        if (playerHand.IsSelectingKong)
        {
            Debug.Log($"[ClickableTile] IN KONG SELECTION MODE");
            
            if (playerHand.AvailableKongValues.Contains(tileValue))
            {
                Debug.Log($"[ClickableTile] Valid Kong tile - executing Kong");
                playerHand.ExecuteKong(tileValue);
                Debug.Log($"[ClickableTile] ========================================");
                return;
            }
            else
            {
                Debug.Log($"[ClickableTile] Not a valid Kong tile - ignoring");
                Debug.Log($"[ClickableTile] ========================================");
                return;
            }
        }

        // PRIORITY 4: NORMAL DISCARD
        Debug.Log($"[ClickableTile] Normal discard mode");
        Debug.Log($"[ClickableTile] Calling DiscardAndDrawTile");
        playerHand.DiscardAndDrawTile(transform.position, gameObject);
        Debug.Log($"[ClickableTile] ========================================");
    }
    
    void OnMouseEnter()
    {
        Debug.Log($"[ClickableTile] OnMouseEnter called");
        
        // GET FRESH REFERENCE
        NetworkedPlayerHand playerHand = GetLocalPlayerHand();
        
        if (playerHand == null)
        {
            Debug.LogError($"[ClickableTile] playerHand is NULL!");
            return;
        }

        TileData tileData = GetComponent<TileData>();
        if (tileData == null)
        {
            Debug.LogError($"[ClickableTile] TileData is NULL!");
            return;
        }
        
        int sortValue = tileData.GetSortValue();
        Debug.Log($"[ClickableTile] Tile sort value: {sortValue}");

        // CHECK CHI SELECTION MODE
        bool inChiMode = playerHand.IsInChiSelectionMode;
        Debug.Log($"[ClickableTile] IsInChiSelectionMode: {inChiMode}");

        // PRIORITY 1: Chi selection mode
        if (inChiMode)
        {
            Debug.Log($"[ClickableTile] IN CHI SELECTION MODE - calling OnChiTileHovered");
            playerHand.OnChiTileHovered(sortValue, transform.position);
            return;
        }

        // PRIORITY 2: Normal Tenpai check
        if (NetworkedGameManager.Instance != null)
        {
            NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
            if (localPlayer == null) return;
            
            int localSeat = localPlayer.PlayerIndex;
            int currentTurn = NetworkedGameManager.Instance.CurrentPlayerIndex;
            
            if (localSeat != currentTurn)
            {
                return;
            }
        }
        
        playerHand.RequestTenpaiCheck(gameObject);
    }
    
    void OnMouseExit()
    {
        NetworkedPlayerHand playerHand = GetLocalPlayerHand();
        if (playerHand != null)
        {
            playerHand.HideTenpaiVisuals();
        }
    }
}