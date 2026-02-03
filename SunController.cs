using UnityEngine;
using Mirror;

/// <summary>
/// Rotates the Sun object so it faces the correct direction relative to the
/// local player's seat. Seats 0 and 1 share one angle; seats 2 and 3 share
/// the opposite angle (180° flipped on Y).
/// 
/// Attach this script to the Sun object in the Game scene.
/// </summary>
public class SunController : MonoBehaviour
{
    // Rotation for seats 0 and 1
    [SerializeField] private Vector3 rotationSeats01 = new Vector3(9f, 315f, 0f);

    // Rotation for seats 2 and 3
    [SerializeField] private Vector3 rotationSeats23 = new Vector3(9f, 135f, 0f);

    private bool oriented = false;

    void Update()
    {
        // Already done — nothing more to do
        if (oriented) return;

        // Wait until Mirror has spawned the local player
        if (NetworkClient.localPlayer == null) return;

        NetworkPlayer localPlayer = NetworkClient.localPlayer.GetComponent<NetworkPlayer>();
        if (localPlayer == null) return;

        int seat = localPlayer.CurrentSeatPosition;

        if (seat == 0 || seat == 1)
        {
            transform.eulerAngles = rotationSeats01;
        }
        else // seat 2 or 3
        {
            transform.eulerAngles = rotationSeats23;
        }

        oriented = true;
        Debug.Log($"[SunController] Sun oriented for Seat {seat}: {transform.eulerAngles}");
    }
}