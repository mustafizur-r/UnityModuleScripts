using Fusion;
using UnityEngine;

public class NetworkedRigTracker : NetworkBehaviour
{
    [Header("Tracked child")]
    public Transform rotationSource; // Assign in inspector or auto-find

    // 🔹 Public accessor for position
    public Vector3 RemotePosition => transform.position;

    // 🔹 Public accessor for remote rotation
    public Quaternion RemoteRotation => rotationSource != null ? rotationSource.rotation : transform.rotation;

    public Vector3 FlatForward
    {
        get
        {
            Vector3 forward = rotationSource != null ? rotationSource.forward : transform.forward;
            forward.y = 0f;

            // Avoid zero-length vector errors
            return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        }
    }


    public override void Spawned()
    {
        if (!HasInputAuthority)
        {
            Debug.Log($"[REMOTE SPAWNED] Remote NetworkedRig at {transform.position}");
            RemotePlayerTracker.Instance?.AddRemote(Object);

            if (rotationSource == null)
            {
                rotationSource = transform.Find("NameTag"); // change as needed
            }
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
            Debug.Log($"[REMOTE TRACKING] Pos: {RemotePosition} | Rot: {RemoteRotation.eulerAngles}");
        }
    }
}
