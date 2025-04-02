using Fusion;
using UnityEngine;

public class NetworkedRigTracker : NetworkBehaviour
{
    public override void Spawned()
    {
        if (!HasInputAuthority)
        {
            Debug.Log($"[REMOTE SPAWNED] Remote NetworkedRig at {transform.position}");
            RemotePlayerTracker.Instance?.AddRemote(Object);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (!HasInputAuthority)
        {
            Debug.Log("[REMOTE DESPAWNED] Remote NetworkedRig despawned");
            RemotePlayerTracker.Instance?.RemoveRemote(Object);
        }
    }

    void Update()
    {
        if (!HasInputAuthority)
        {
            Debug.Log($"[REMOTE POSITION] Remote player at: {transform.position}");
        }
    }
}
