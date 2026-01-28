using UnityEngine;
using Mirror;

/// <summary>
/// Controls the sun rotation based on the local player's seat position.
/// Seats 0/1 (South side): Sun from North-West (9, 315, 0)
/// Seats 2/3 (North side): Sun from South-East (9, 135, 0)
/// </summary>
public class SunController : MonoBehaviour
{
    [Header("Sun Object")]
    [Tooltip("The directional light or sun object to rotate")]
    public GameObject sunObject;

    [Header("Rotation Settings")]
    [Tooltip("Sun rotation for Seats 0 and 1 (South side players)")]
    public Vector3 southSideRotation = new Vector3(9f, 315f, 0f);
    
    [Tooltip("Sun rotation for Seats 2 and 3 (North side players)")]
    public Vector3 northSideRotation = new Vector3(9f, 135f, 0f);

    void Start()
    {
        // Wait a frame to ensure NetworkClient.localPlayer is set
        StartCoroutine(InitializeSunRotation());
    }

    private System.Collections.IEnumerator InitializeSunRotation()
    {
        // Wait for local player to be ready
        int attempts = 0;
        int maxAttempts = 10;
        
        while (NetworkClient.localPlayer == null && attempts < maxAttempts)
        {
            yield return new WaitForSeconds(0.5f);
            attempts++;
        }

        if (NetworkClient.localPlayer == null)
        {
            Debug.LogError("[SunController] Local player not found after waiting!");
            yield break;
        }

        // Get the local player's seat index
        NetworkPlayer localPlayer = NetworkClient.localPlayer.GetComponent<NetworkPlayer>();
        
        if (localPlayer == null)
        {
            Debug.LogError("[SunController] NetworkPlayer component not found!");
            yield break;
        }

        int seatIndex = localPlayer.PlayerIndex;
        SetSunRotationForSeat(seatIndex);
    }

    /// <summary>
    /// Set sun rotation based on player's seat.
    /// </summary>
    private void SetSunRotationForSeat(int seatIndex)
    {
        if (sunObject == null)
        {
            Debug.LogError("[SunController] Sun object not assigned!");
            return;
        }

        Vector3 targetRotation;
        string sideName;

        // Seats 0 and 1 = South side (sun from North-West)
        // Seats 2 and 3 = North side (sun from South-East)
        if (seatIndex == 0 || seatIndex == 1)
        {
            targetRotation = southSideRotation;
            sideName = "South (Seats 0-1)";
        }
        else // seatIndex == 2 || seatIndex == 3
        {
            targetRotation = northSideRotation;
            sideName = "North (Seats 2-3)";
        }

        sunObject.transform.rotation = Quaternion.Euler(targetRotation);
        
        Debug.Log($"[SunController] Player in Seat {seatIndex} ({sideName}). Sun rotation set to {targetRotation}");
    }

    // Optional: Public method to manually set sun rotation
    public void SetSunForSeat(int seatIndex)
    {
        SetSunRotationForSeat(seatIndex);
    }
}
