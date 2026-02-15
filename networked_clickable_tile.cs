using UnityEngine;
using Mirror;
using System.Linq;

public class NetworkedClickableTile : MonoBehaviour
{
    // Cache the player hand once found
    private NetworkedPlayerHand cachedPlayerHand = null;
    private bool hasLoggedSearchAttempt = false;
    
    void Start()
    {
        TryFindPlayerHand();
    }
    
    /// <summary>
    /// Try EVERY possible way to find NetworkedPlayerHand
    /// </summary>
    private bool TryFindPlayerHand()
    {
        if (cachedPlayerHand != null)
        {
            return true;
        }
        
        // METHOD 1: Try NetworkClient.localPlayer
        if (NetworkClient.localPlayer != null)
        {
            // Try GetComponent first
            cachedPlayerHand = NetworkClient.localPlayer.GetComponent<NetworkedPlayerHand>();
            if (cachedPlayerHand != null)
            {
                Debug.Log($"[ClickableTile] ✓ Found via GetComponent");
                return true;
            }
            
            // Try GetComponentInChildren
            cachedPlayerHand = NetworkClient.localPlayer.GetComponentInChildren<NetworkedPlayerHand>(true);
            if (cachedPlayerHand != null)
            {
                Debug.Log($"[ClickableTile] ✓ Found via GetComponentInChildren");
                return true;
            }
            
            // Try finding by searching all components
            var allComponents = NetworkClient.localPlayer.GetComponents<Component>();
            Debug.Log($"[ClickableTile] LocalPlayer has {allComponents.Length} components:");
            foreach (var comp in allComponents)
            {
                if (comp != null)
                {
                    Debug.Log($"[ClickableTile]   - {comp.GetType().Name}");
                    if (comp.GetType().Name.Contains("PlayerHand"))
                    {
                        cachedPlayerHand = comp as NetworkedPlayerHand;
                        if (cachedPlayerHand != null)
                        {
                            Debug.Log($"[ClickableTile] ✓ Found by type name matching!");
                            return true;
                        }
                    }
                }
            }
        }
        
        // METHOD 2: Search all NetworkBehaviours for NetworkedPlayerHand with isOwned
        var allBehaviours = FindObjectsByType<NetworkBehaviour>(FindObjectsSortMode.None);
        foreach (var behaviour in allBehaviours)
        {
            if (behaviour.isOwned && behaviour is NetworkedPlayerHand)
            {
                cachedPlayerHand = behaviour as NetworkedPlayerHand;
                Debug.Log($"[ClickableTile] ✓ Found by searching all NetworkBehaviours");
                return true;
            }
        }
        
        if (!hasLoggedSearchAttempt)
        {
            Debug.LogError($"[ClickableTile] EXHAUSTED ALL SEARCH METHODS - CANNOT FIND NetworkedPlayerHand!");
            hasLoggedSearchAttempt = true;
        }
        
        return false;
    }
    
    private NetworkedPlayerHand GetLocalPlayerHand()
    {
        if (cachedPlayerHand != null)
        {
            return cachedPlayerHand;
        }
        
        TryFindPlayerHand();
        return cachedPlayerHand;
    }

    void OnMouseDown()
    {
        NetworkedPlayerHand playerHand = GetLocalPlayerHand();
        
        if (playerHand == null)
        {
            Debug.LogError($"[ClickableTile] Cannot interact - player hand not found");
            return;
        }

        TileData tileData = GetComponent<TileData>();
        if (tileData == null)
        {
            Debug.LogError($"[ClickableTile] TileData is NULL!");
            return;
        }
        
        int tileValue = tileData.GetSortValue();

        // CHECK CHI SELECTION MODE
        bool inChiMode = playerHand.IsInChiSelectionMode;

        // PRIORITY 1: CHI SELECTION MODE
        if (inChiMode)
        {
            playerHand.OnChiTileClicked(tileValue);
            return;
        }

        // PRIORITY 2: CHECK IF IT'S OUR TURN
        if (NetworkedGameManager.Instance != null)
        {
            NetworkPlayer localPlayer = null;
            if (NetworkClient.localPlayer != null)
            {
                localPlayer = NetworkClient.localPlayer.GetComponent<NetworkPlayer>();
            }
            else
            {
                NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
                foreach (var p in players)
                {
                    if (p.isOwned)
                    {
                        localPlayer = p;
                        break;
                    }
                }
            }
            
            if (localPlayer == null)
            {
                Debug.LogWarning("[ClickableTile] Cannot find local player to check turn");
                return;
            }

            int localSeat = localPlayer.PlayerIndex;
            int currentTurn = NetworkedGameManager.Instance.CurrentPlayerIndex;

            if (localSeat != currentTurn)
            {
                Debug.Log($"[ClickableTile] Not your turn (Seat {localSeat}, Turn {currentTurn})");
                return;
            }
        }

        // PRIORITY 3: KONG SELECTION MODE
        if (playerHand.IsSelectingKong)
        {
            if (playerHand.AvailableKongValues.Contains(tileValue))
            {
                playerHand.ExecuteKong(tileValue);
                return;
            }
            else
            {
                return;
            }
        }

        // PRIORITY 4: NORMAL DISCARD
        Debug.Log($"[ClickableTile] Discarding tile {tileValue}");
        playerHand.DiscardAndDrawTile(transform.position, gameObject);
    }
    
    void OnMouseEnter()
    {
        NetworkedPlayerHand playerHand = GetLocalPlayerHand();
        
        if (playerHand == null)
        {
            return;
        }

        TileData tileData = GetComponent<TileData>();
        if (tileData == null)
        {
            return;
        }
        
        int sortValue = tileData.GetSortValue();

        playerHand.HighlightMatchingTiles(sortValue);

        bool inChiMode = playerHand.IsInChiSelectionMode;

        if (inChiMode)
        {
            playerHand.OnChiTileHovered(sortValue, transform.position);
            return;
        }

        if (NetworkedGameManager.Instance != null)
        {
            NetworkPlayer localPlayer = null;
            if (NetworkClient.localPlayer != null)
            {
                localPlayer = NetworkClient.localPlayer.GetComponent<NetworkPlayer>();
            }
            
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
            playerHand.ClearAllHighlights();
        }
    }
}