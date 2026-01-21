using UnityEngine;
using Mirror;

/// <summary>
/// Add this to your Player prefab to debug spawning issues.
/// This will tell us exactly when each Unity/Mirror lifecycle method is called.
/// </summary>
public class PlayerSpawnDebugger : MonoBehaviour
{
    void Awake()
    {
        Debug.Log($"[PlayerDebug] Awake() - GameObject: {gameObject.name}");
    }

    void Start()
    {
        Debug.Log($"[PlayerDebug] Start() - GameObject: {gameObject.name}");
        
        // Check components
        var identity = GetComponent<NetworkIdentity>();
        var player = GetComponent<NetworkPlayer>();
        
        Debug.Log($"[PlayerDebug] Has NetworkIdentity: {identity != null}");
        Debug.Log($"[PlayerDebug] Has NetworkPlayer: {player != null}");
        
        if (identity != null)
        {
            Debug.Log($"[PlayerDebug] NetworkIdentity.netId: {identity.netId}");
            Debug.Log($"[PlayerDebug] NetworkIdentity.isServer: {identity.isServer}");
            Debug.Log($"[PlayerDebug] NetworkIdentity.isClient: {identity.isClient}");
            Debug.Log($"[PlayerDebug] NetworkIdentity.isOwned: {identity.isOwned}");
            Debug.Log($"[PlayerDebug] NetworkIdentity.isLocalPlayer: {identity.isLocalPlayer}");
        }
    }

    void OnEnable()
    {
        Debug.Log($"[PlayerDebug] OnEnable() - GameObject: {gameObject.name}");
    }

    void OnDisable()
    {
        Debug.Log($"[PlayerDebug] OnDisable() - GameObject: {gameObject.name}");
    }
}