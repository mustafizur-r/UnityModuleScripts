#!/bin/bash

# === CONFIGURATION ===
INTERFACE="enp1s0"                     # Your active network interface (from ip a)
STATIC_LAST_OCTET="2"                  # Desired static IP suffix
DNS1="8.8.8.8"                         # Google DNS
DNS2="8.8.4.4"                         # Google secondary DNS

# === STEP 0: Release any existing DHCP lease (if necessary) ===
sudo dhclient -r $INTERFACE  # Release the current DHCP lease, if any
sudo dhclient $INTERFACE  # Get a new DHCP lease for the interface

# === STEP 1: Get current gateway ===
GATEWAY=$(ip route | grep default | awk '{print $3}')
EXPECTED_GATEWAY="192.168.1.1"         # Gateway we are waiting for

while true; do
# Retry until we find the gateway or timeout occurs
while [[ -z "$GATEWAY" || "$GATEWAY" == "$EXPECTED_GATEWAY" ]]; do
   echo "DEFAULT"
   GATEWAY=$(ip route | grep default | awk '{print $3}')
   sleep 1
done
# === STEP 2: Extract subnet ===
SUBNET=$(echo "$GATEWAY" | cut -d '.' -f 1-3)
STATIC_IP="$SUBNET.$STATIC_LAST_OCTET"

# === STEP 3: Flush DHCP address and apply static IP ===
sudo ip addr flush dev "$INTERFACE"
sudo ip addr add "$STATIC_IP/24" dev "$INTERFACE"
sudo ip route add default via "$GATEWAY" dev "$INTERFACE"

# === STEP 4: Set static DNS (Google DNS) ===
sudo bash -c "echo -e 'nameserver $DNS1\nnameserver $DNS2' > /etc/resolv.conf"

while [[ "$GATEWAY" != "$EXPECTED_GATEWAY" ]]; do
   echo "CONNECTED"
   GATEWAY=$(ip route | grep default | awk '{print $3}')
   sleep 1
done
done
