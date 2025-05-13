import paho.mqtt.client as mqtt
import struct
import matplotlib.pyplot as plt
import threading
import time
import logging

# === Logging Setup ===
logging.basicConfig(
    filename="vehicle_pose_log.txt",
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S"
)

# Also print to console
console = logging.StreamHandler()
console.setLevel(logging.INFO)
formatter = logging.Formatter("%(asctime)s [%(levelname)s] %(message)s", datefmt="%H:%M:%S")
console.setFormatter(formatter)
logging.getLogger('').addHandler(console)

# === Data Storage ===
x_refs = []
y_refs = []
lock = threading.Lock()


def on_message(client, userdata, msg):
    payload = msg.payload
    expected_length = 14 * 8

    if len(payload) == expected_length:
        unpacked = struct.unpack('<14d', payload)
        log_msg = ["✅ Valid 14-double payload:"]
        for i, val in enumerate(unpacked):
            log_msg.append(f"  [{i}] = {val:.6f}")

        x_ref = unpacked[11]
        y_ref = unpacked[12]
        log_msg.append(f"Extracted x_ref: {x_ref:.3f}, y_ref: {y_ref:.3f}")

        logging.info("\n".join(log_msg))

        with lock:
            x_refs.append(x_ref)
            y_refs.append(y_ref)
    else:
        logging.warning(f"❌ Invalid payload. Length: {len(payload)} bytes")
        logging.debug("Raw payload (hex): %s", payload.hex())


def plot_loop(interval=5):
    while True:
        time.sleep(interval)
        with lock:
            if x_refs and y_refs:
                plt.clf()
                plt.plot(x_refs, y_refs, marker='o')
                plt.xlabel("x_ref")
                plt.ylabel("y_ref")
                plt.title("Vehicle x_ref vs y_ref Path")
                plt.grid(True)
                plt.pause(0.01)  # Non-blocking plot update

# === MQTT Setup ===
broker_address = "192.168.153.2"
topic = "Vehicle/Info/Poses"

client = mqtt.Client()
client.on_message = on_message
client.connect(broker_address)
client.subscribe(topic)
client.loop_start()

# === Start Plotting Thread ===
plt.ion()
threading.Thread(target=plot_loop, daemon=True).start()

# === Keep Script Running ===
try:
    while True:
        time.sleep(1)
except KeyboardInterrupt:
    logging.info("Exiting...")
    client.loop_stop()
    client.disconnect()
