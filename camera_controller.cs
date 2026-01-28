using UnityEngine;
using Mirror;

public class CameraController : MonoBehaviour
{
    private bool cameraSetup = false;
    private int currentSeat = -1;

    void Update()
    {
        if (cameraSetup) return;

        // Try to setup camera each frame until successful
        TrySetupCamera();
    }

    private void TrySetupCamera()
    {
        // Find local player
        NetworkPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkPlayer>();
        if (localPlayer == null) return;

        int seat = localPlayer.CurrentSeatPosition;
        if (seat < 0) return; // Wait for valid seat assignment

        SetupCameraForSeat(seat);
    }

    /// <summary>
    /// Public method to reposition camera when seat changes
    /// </summary>
    public void SetupCameraForSeat(int seat)
    {
        if (seat == currentSeat && cameraSetup) return; // Already set up for this seat

        Camera cam = Camera.main;
        if (cam == null) return;

        // Camera positions for each seat around the table
        Vector3[] positions = { 
            new Vector3(0, 1.77f, -.88f),      // Seat 0 (South) - Faces North 
            new Vector3(2.47f, 1.77f, 1.41f),  // Seat 1 (West) - Faces East 
            new Vector3(0.18f, 1.77f, 3.885f),      // Seat 2 (North) - Faces South 
            new Vector3(-2.295f, 1.77f, 1.59f)  // Seat 3 (East) - Faces West 
        };
        
        Vector3[] rotations = {
            new Vector3(45, 0, 0),      // Seat 0
            new Vector3(45, -90, 0),    // Seat 1
            new Vector3(45, 180, 0),    // Seat 2
            new Vector3(45, 90, 0)      // Seat 3
        };
        
        if (seat >= 0 && seat < positions.Length)
        {
            cam.transform.position = positions[seat];
            cam.transform.eulerAngles = rotations[seat];
            Debug.Log($"[Camera] ✓ Positioned for Seat {seat} at {positions[seat]}");
            currentSeat = seat;
            cameraSetup = true;
        }
    }
    
    /// <summary>
    /// Reset camera setup flag (called when scene reloads)
    /// </summary>
    public void ResetCamera()
    {
        cameraSetup = false;
        currentSeat = -1;
    }
}