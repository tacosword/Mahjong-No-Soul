using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Makes a tile clickable so players can discard it.
/// </summary>
public class MahjongTileButton : MonoBehaviour, IPointerClickHandler
{
    private MahjongPlayerHand playerHand;
    private int tileValue;

    public void Initialize(MahjongPlayerHand hand, int value)
    {
        playerHand = hand;
        tileValue = value;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (playerHand != null)
        {
            // Check if it's this player's turn
            if (GameManager.Instance != null)
            {
                NetworkPlayer localPlayer = Mirror.NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
                if (localPlayer != null && localPlayer.PlayerIndex == GameManager.Instance.CurrentPlayerIndex)
                {
                    Debug.Log($"Discarding tile: {tileValue}");
                    playerHand.DiscardTile(tileValue);
                }
                else
                {
                    Debug.Log("It's not your turn!");
                }
            }
        }
    }
}
