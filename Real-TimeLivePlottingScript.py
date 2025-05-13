import json
import paho.mqtt.client as mqtt
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation

# Data storage
positions = []

# MQTT settings
MQTT_BROKER = "192.168.153.2"      # Change to your broker IP if needed
MQTT_PORT = 1883
MQTT_TOPIC = "robot/pose/realtime"

# MQTT callbacks
def on_connect(client, userdata, flags, rc):
    print("✅ Connected to MQTT broker with result code", rc)
    client.subscribe(MQTT_TOPIC)
    print(f"📡 Subscribed to topic: '{MQTT_TOPIC}'")

def on_message(client, userdata, msg):
    print(f"📥 Message received on topic: {msg.topic}")
    try:
        data = json.loads(msg.payload.decode())
        x = data['x']
        z = data['z']
        positions.append((x, z))
        print(f"   → Position: x = {x:.2f}, z = {z:.2f}")
    except Exception as e:
        print("❌ Error parsing message:", e)

# MQTT setup
client = mqtt.Client()
client.on_connect = on_connect
client.on_message = on_message
client.connect(MQTT_BROKER, MQTT_PORT, 3000)
client.loop_start()

# Matplotlib setup
fig, ax = plt.subplots()
scat, = plt.plot([], [], 'ro-', markersize=4)
ax.set_title("Live Robot Position (Unity World Space)")
ax.set_xlabel("World X")
ax.set_ylabel("World Z")
ax.grid(True)

def update(frame):
    if not positions:
        return scat,
    xs, zs = zip(*positions)
    scat.set_data(xs, zs)
    ax.set_xlim(min(xs) - 1, max(xs) + 1)
    ax.set_ylim(min(zs) - 1, max(zs) + 1)
    return scat,

# Animate and show plot until manually closed
ani = FuncAnimation(fig, update, interval=500)

try:
    print("📊 Plot running... Close the window to stop.")
    plt.show()
finally:
    print("🛑 Plot closed. Disconnecting MQTT.")
    client.loop_stop()
    client.disconnect()
