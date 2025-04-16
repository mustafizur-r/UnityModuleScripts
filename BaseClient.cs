using System;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using M2MqttUnity;
using System.Collections;

namespace M2MqttUnity
{
    public class BaseClient : M2MqttUnityClient
    {
        void Start()
        {
            autoConnect = false; // disable auto-connect on Start if needed
            StartAutoReconnect();
        }
        [Serializable]
        public class PathPoint
        {
            public double x, y, z;
            public PathPoint(Vector3 vec) { x = vec.x; y = vec.y; z = vec.z; }
        }

        [Serializable]
        public class PathData
        {
            public List<PathPoint> points = new();
        }

        public delegate void MessageReceivedDelegate(string topic, string message);
        private Dictionary<string, MessageReceivedDelegate> m_messageHandlers = new();
        public float reconnectInterval = 1f;
        private Coroutine reconnectRoutine;
        public double[] relativeTrajectoryDouble;

        public string topic = "M2MQTT_Unity/test";

        [Header("MQTT Topics")]
        public string topic4json = "robot/path/json";
        public string topic4binary = "Vehicle/CMD/PathFollowing/Path";

        public void RegisterTopicHandler(string topic, MessageReceivedDelegate messageReceivedDelegate)
        {
            if (!m_messageHandlers.ContainsKey(topic))
            {
                m_messageHandlers.Add(topic, null);
            }
            m_messageHandlers[topic] += messageReceivedDelegate;
        }

        public void UnregisterTopicHandler(string topic, MessageReceivedDelegate messageReceivedDelegate)
        {
            if (m_messageHandlers.ContainsKey(topic))
            {
                m_messageHandlers[topic] -= messageReceivedDelegate;
            }
        }

        public bool IsBrokerConnected { get; private set; }
        protected override void Update()
        {
            base.Update();

            if (client != null && IsBrokerConnected && !client.IsConnected)
            {
                IsBrokerConnected = false;
                Debug.LogWarning("[MQTT] Detected broker disconnect via polling.");
            }
        }

        protected override void OnConnected()
        {
            base.OnConnected();
            IsBrokerConnected = true;
            Debug.Log("[MQTT] Connected to broker.");
        }

        protected override void OnDisconnected()
        {
            base.OnDisconnected();
            IsBrokerConnected = false;
            Debug.LogWarning("[MQTT] Disconnected from broker.");
        }

        protected override void OnConnectionFailed(string errorMessage)
        {
            base.OnConnectionFailed(errorMessage);
            IsBrokerConnected = false;
            Debug.LogError($"[MQTT] Connection failed: {errorMessage}");
        }



        public void StartAutoReconnect()
        {
            if (reconnectRoutine == null)
            {
                reconnectRoutine = StartCoroutine(AutoReconnectLoop());
            }
        }

        private IEnumerator AutoReconnectLoop()
        {
            while (true)
            {
                if (!IsBrokerConnected)
                {
                    Debug.Log("[MQTT] Trying to reconnect to broker...");
                    Connect(); // attempt reconnect
                }

                yield return new WaitForSeconds(reconnectInterval);
            }
        }

        protected override void SubscribeTopics()
        {
            client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
        }

        protected override void UnsubscribeTopics()
        {
            client.Unsubscribe(new string[] { topic });
        }


        public void PublishBinary(List<Vector3> pathPoints)
        {
            if (pathPoints == null || pathPoints.Count == 0) return;

            double[] relativeTrajectoryDouble = new double[pathPoints.Count * 3];
            int j = 0;

            Vector3 origin = pathPoints[0];
            Debug.Log($"[MQTT] Origin (Unity World): X={origin.x:F3}, Y={origin.y:F3}, Z={origin.z:F3}");

            for (int k = 0; k + 2 < (pathPoints.Count * 3) - 1; k += 3)
            {
                Vector3 pt = pathPoints[j];
                Vector3 relative = pt - origin;

                // Assign for MQTT buffer
                relativeTrajectoryDouble[k] = (double)relative.x;
                relativeTrajectoryDouble[k + 1] = (double)relative.y;
                relativeTrajectoryDouble[k + 2] = 1;

                j++;
            }

            byte[] pathBytes = GetBytesBlock(relativeTrajectoryDouble);
            client.Publish(topic4binary, pathBytes, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            Debug.Log($"[MQTT] Binary path (robot-aligned frame) published. Total Points: {pathPoints.Count}");
        }



        static byte[] GetBytesBlock(double[] values)
        {
            var result = new byte[values.Length * sizeof(double)];
            Buffer.BlockCopy(values, 0, result, 0, result.Length);
            return result;
        }
        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
