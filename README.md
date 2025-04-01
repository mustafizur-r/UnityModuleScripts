# UnityModuleScripts

## 1. PassthroughDrawPointer

✅ Press "Create Path" Button → Enables drawing mode (but doesn’t start drawing).

✅ Press & Hold Trigger → Shows LineRenderer dynamically as you move.

✅ Release Trigger → Instantly replaces the LineRenderer with path segment prefabs.

✅ Press "Clear Path" Button → Removes all paths (both LineRenderer and prefabs).

✅ Continue Drawing → You can keep adding more segments to the same path seamlessly.



## 2. SpawnBall

✅ Press & Hold Left Trigger → Shows SpawnBall dynamically as you move.

## 3. Display the MRUK room label
✅ Hit with the raycast → Shows Room Labels(i.e Floor, Wall)

mosquitto -p 8000
mosquitto_sub -h 127.0.0.1 -p 8000 -t robot/path
# Edit the Mosquitto Config file 
listener 8000 0.0.0.0
allow_anonymous true
protocol mqtt
