using UnityEngine;

public class RemotePlayerPositionPrinter : MonoBehaviour
{
    void Update()
    {
        // Make sure tracker exists
        if (RemotePlayerTracker.Instance == null) return;

        var remotePlayers = RemotePlayerTracker.Instance.GetRemotePlayers();

        if (remotePlayers.Count == 0)
        {
            // Optional: log if no remote players found
            // Debug.Log("No remote players tracked yet.");
            return;
        }

        foreach (var remote in remotePlayers)
        {
            if (remote != null)
            {
                Vector3 pos = remote.transform.position;
                Debug.Log($"[RemotePlayer] Position: {pos}");
            }
        }
    }
}
