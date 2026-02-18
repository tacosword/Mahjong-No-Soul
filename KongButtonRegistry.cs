using UnityEngine;

/// <summary>
/// Attach this component to your Kong Button GameObject in the scene.
/// It registers itself as the global Kong button so that the player prefab
/// (which cannot hold scene references) can always find it — even when inactive.
///
/// Setup: Add this script to your KongButton GameObject in the Canvas.
/// No other configuration needed.
/// </summary>
public class KongButtonRegistry : MonoBehaviour
{
    public static GameObject Instance { get; private set; }

    void Awake()
    {
        Instance = gameObject;
        // Start hidden — NetworkedPlayerHand will show/hide it as needed
        gameObject.SetActive(false);
        Debug.Log("[KongButtonRegistry] Kong button registered.");
    }

    void OnDestroy()
    {
        if (Instance == gameObject)
            Instance = null;
    }
}
