using Fusion;
using UnityEngine;
using System.Collections.Generic;
using Fusion.Sockets;

public class RemotePlayerTracker : MonoBehaviour, INetworkRunnerCallbacks
{
    public static RemotePlayerTracker Instance;

    private List<NetworkObject> remotePlayers = new();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // So it persists across scenes
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddRemote(NetworkObject obj)
    {
        if (!remotePlayers.Contains(obj))
        {
            remotePlayers.Add(obj);
            Debug.Log($"[RemoteTracker] Remote player added: {obj.name}");
        }
    }

    public void RemoveRemote(NetworkObject obj)
    {
        if (remotePlayers.Contains(obj))
        {
            remotePlayers.Remove(obj);
            Debug.Log($"[RemoteTracker] Remote player removed: {obj.name}");
        }
    }

    public List<NetworkObject> GetRemotePlayers()
    {
        return remotePlayers;
    }

    // Called when a player disconnects
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        var obj = remotePlayers.Find(o => o.InputAuthority == player);
        if (obj != null)
        {
            RemoveRemote(obj);
        }
    }

    // Empty required callbacks
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
