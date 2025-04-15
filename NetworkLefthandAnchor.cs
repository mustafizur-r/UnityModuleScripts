using Fusion;
using UnityEngine;

public class NetworkLefthandAnchor : NetworkBehaviour
{
    [Header("Auto-Assigned")]
    public LeftHandProvider localHandSource; // Will be assigned at runtime

    public Transform remoteHandVisual;

    [Networked]
    public Vector3 SyncedPosition { get; private set; }

    [Networked]
    public Quaternion SyncedRotation { get; private set; }

    public override void Spawned()
    {
        // Only for the local user — assign the hand provider
        if (HasInputAuthority && localHandSource == null)
        {
            // Try to find the XR rig’s left hand in the scene
            localHandSource = FindFirstObjectByType<LeftHandProvider>();

            if (localHandSource != null)
            {
                Debug.Log("[HandSync] LeftHandProvider assigned at runtime.");
            }
            else
            {
                Debug.LogWarning("[HandSync] Could not find LeftHandProvider in scene.");
            }
        }
    }

    void Update()
    {
        if (HasInputAuthority)
        {
            if (localHandSource == null) return;

            SyncedPosition = localHandSource.Position;
            SyncedRotation = localHandSource.Rotation;
        }
        else
        {
            if (remoteHandVisual != null)
            {
                remoteHandVisual.position = SyncedPosition;
                remoteHandVisual.rotation = SyncedRotation;
            }
        }
    }

    public Vector3 Position => SyncedPosition;
    public Quaternion Rotation => SyncedRotation;
}
