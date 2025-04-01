using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using System.Text;

public class MqttPathSender : MonoBehaviour
{
    private MqttClient client;
    public string brokerAddress = "127.0.0.1"; // Replace with your broker IP
    public int brokerPort = 8000;
    public string topic = "robot/path";

    void Start()
    {
        ConnectToBroker();
    }

    private void ConnectToBroker()
    {
        try
        {
            client = new MqttClient(brokerAddress, brokerPort, false, MqttSslProtocols.None, null, null);
            string clientId = System.Guid.NewGuid().ToString();
            client.Connect(clientId);
            Debug.Log("MQTT connected!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("MQTT connection failed: " + ex.Message);
        }
    }

    public void PublishPath(string jsonPayload)
    {
        if (client == null || !client.IsConnected)
        {
            Debug.LogWarning("MQTT client not connected.");
            return;
        }

        client.Publish(topic, Encoding.UTF8.GetBytes(jsonPayload));
        Debug.Log("Path published to MQTT topic: " + topic);
    }
}
