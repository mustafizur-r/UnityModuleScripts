using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Text;
using System;
using System.Collections.Generic;

public class MqttPathSender : MonoBehaviour
{
    private MqttClient client;
    public string brokerAddress = "127.0.0.1";
    public int brokerPort = 1883;
    public string topic4jsonData = "robot/path/json";
    public string topic4binaryData = "robot/path/bin";

    [Serializable]
    public class PathPoint
    {
        public float x, y, z;
        public PathPoint(Vector3 vec)
        {
            x = vec.x;
            y = vec.y;
            z = vec.z;
        }
    }

    [Serializable]
    public class PathData
    {
        public List<PathPoint> points = new();
    }


    void Start()
    {
        ConnectToBroker();
    }

    private void ConnectToBroker()
    {
        try
        {
            client = new MqttClient(brokerAddress, brokerPort, false, MqttSslProtocols.None, null, null);
            string clientId = Guid.NewGuid().ToString();
            client.Connect(clientId);
            Debug.Log("MQTT connected!");
        }
        catch (Exception ex)
        {
            Debug.LogError("MQTT connection failed: " + ex.Message);
        }
    }

    //public void PublishPath(string jsonPayload)
    //{
    //    if (client == null || !client.IsConnected)
    //    {
    //        Debug.LogWarning("MQTT client not connected.");
    //        return;
    //    }

    //    client.Publish(topic, Encoding.UTF8.GetBytes(jsonPayload));
    //    Debug.Log("Path published to MQTT topic: " + topic);
    //}

    //for sending path points
    public void SendPath(List<Vector3> pathPoints)
    {
        if (client == null || !client.IsConnected)
        {
            Debug.LogWarning("MQTT client not connected.");
            return;
        }

        // ---- For JSON Message ----
        PathData data = new PathData();
        foreach (var pt in pathPoints)
            data.points.Add(new PathPoint(pt));

        string json = JsonUtility.ToJson(data);
        client.Publish(topic4jsonData, Encoding.UTF8.GetBytes(json));
        Debug.Log("JSON path sent.");

        // ---- For Binary Message ----
        List<byte> binaryBytes = new();

        foreach (var pt in pathPoints)
        {
            binaryBytes.AddRange(BitConverter.GetBytes(pt.x));
            binaryBytes.AddRange(BitConverter.GetBytes(pt.y)); // using x and y only
        }

        client.Publish(topic4binaryData, binaryBytes.ToArray());
        Debug.Log("Binary path sent.");
    }

   
}
