using Fusion;
using UnityEngine;

public class ColocationFusionHook : MonoBehaviour
{
    public void RegisterRemoteTracker()
    {
        Debug.Log("[ColocationFusionHook] Colocation is ready. Registering RemotePlayerTracker...");

        NetworkRunner runner = FindObjectOfType<NetworkRunner>();
        if (runner != null && RemotePlayerTracker.Instance != null)
        {
            runner.AddCallbacks(RemotePlayerTracker.Instance);
            Debug.Log("[ColocationFusionHook] RemotePlayerTracker registered to runner.");
        }
        else
        {
            Debug.LogWarning("[ColocationFusionHook] Failed to register RemotePlayerTracker (runner or tracker missing).");
        }
    }
}
