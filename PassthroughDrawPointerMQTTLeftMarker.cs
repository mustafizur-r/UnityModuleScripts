using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Meta.XR.MRUtilityKit;
using TMPro;
using Fusion;
using M2MqttUnity;
using UnityEngine.UI;
using System.Linq;

public class PassthroughDrawPointer : NetworkBehaviour
{
    [Header("Prefab Settings")]
    public GameObject linePrefab;
    public GameObject pathSegmentPrefab;
    public float segment_Y_Offset = 0.05f;
    public float segment_width = 2f;
    public float pointDistance = 0.05f; // Minimum distance between points to add a new one

    [Header("Ray Settings")]
    public Transform rayStartPoint;
    public float rayLength = 40f;
    public MRUKAnchor.SceneLabels labelFilter;

    [Header("UI Display")]
    public TextMeshPro debugText;
    public TextMeshProUGUI statusText;

    [Header("Avatar Settings")]
    public NetworkObject avatarPrefab;        // Networked avatar prefab
    private NetworkObject activeAvatar;       // Instance of the avatar
    public float avatarSpeed = 1.5f;
    private Coroutine walkAvatarRoutine;

    [Header("Audio Clips")]
    public AudioClip offPathClip;
    public AudioClip tooFarClip;
    public AudioClip walkingClip;
    private AudioSource audioSource;
    private AudioClip lastPlayedClip;
    private List<AudioClip> audioClips;
    private int lastPlayedIndex = -1;


    [Networked]
    private bool avatarSpawned { get; set; }  // Tracks if the avatar was already spawned

    // Internal state
    private LineRenderer currentLine;
    private List<Vector3> points = new();
    private List<GameObject> pathSegments = new();
    private bool isDrawing = false;

    // Marker for starting point
    [Header("For Marker Settings")]
    public GameObject startPointMarkerPrefab;
    private GameObject startMarkerFront;
    private GameObject startMarkerBack;
    private GameObject startPointMarkerInstance;
    private Vector3 lastMarkerFollowPosition;
    private float markerUpdateThreshold = 0.1f;
    private float markerDistance = 0.75f; // Distance from the hand to the marker

    [Header("Hit Marker Settings")]
    public GameObject hitMarkerPrefab;
    private GameObject hitMarkerInstance;

    [Header("Marker Appearance")]
    public Material greenArrow;
    public Material redArrow;

    private bool hasStartedDrawingFromMarker = false;

    private List<Vector3> finalPathPoints = new();

    public BaseClient baseClient;

    //Buttons for interactivity
    [Header("All Button")]
    public GameObject startButton;
    public GameObject clearButton;
    public GameObject addPath;

    private GameObject usedStartMarker = null;


    void Start()
    {
        
        SetStatus("Please Click a Button to Continue!", Color.red);
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioClips = new List<AudioClip> { offPathClip, tooFarClip, walkingClip };
        // Set all buttons to be interactable
        SetButtonsInteractable(false, startButton,clearButton);
    }


    //General Function to Toggle Button Interactivity
    public void SetButtonsInteractable(bool state, params GameObject[] buttonObjects)
    {
        foreach (var obj in buttonObjects)
        {
            if (obj == null) continue;

            // Look for UnityEngine.UI.Toggle in children
            Toggle toggle = obj.GetComponentInChildren<Toggle>();
            if (toggle != null)
            {
                toggle.interactable = state;
                continue;
            }

            // Look for Unity UI Button (fallback)
            Button button = obj.GetComponentInChildren<Button>();
            if (button != null)
            {
                button.interactable = state;
                continue;
            }

            Debug.LogWarning($"No interactable UI component found on: {obj.name}");
        }
    }

    public void StartCreatingPath()
    {
        if (isDrawing) return;

        Transform remotePlayer = GetRemotePlayerTransform();
        if (remotePlayer == null)
        {
            SetStatus("Remote player not found. Cannot start path!", Color.red);
            Debug.LogWarning("[Path] Remote player not available.");
            return;
        }

        Debug.Log("Path Drawing Mode Enabled");
        SetStatus("Path Drawing Mode Enabled!", Color.green);
        isDrawing = true;

        ShowStartPointMarkers(remotePlayer.position);
        //Ensure the marker is visible again
        SetMarkerVisible(true);
        


    }


    public void ClearPath()
    {
        Debug.Log("Clear Path");
        SetStatus("Path Cleared Successfully!", Color.green);

        isDrawing = false;

        if (currentLine != null)
        {
            Destroy(currentLine.gameObject);
            currentLine = null;
        }

        foreach (var segment in pathSegments)
        {
            Destroy(segment);
        }

        pathSegments.Clear();
        points.Clear();
        finalPathPoints.Clear();

        if (debugText != null)
        {
            debugText.text = "";
        }

        if (activeAvatar != null && Runner != null)
        {
            Runner.Despawn(activeAvatar);
            activeAvatar = null;
        }

        avatarSpawned = false;

        if (startMarkerFront != null) Destroy(startMarkerFront);
        if (startMarkerBack != null) Destroy(startMarkerBack);
        startMarkerFront = null;
        startMarkerBack = null;
        startPointMarkerInstance = null;
        usedStartMarker = null;


        if (walkAvatarRoutine != null)
        {
            StopCoroutine(walkAvatarRoutine);
            walkAvatarRoutine = null;

            RPC_PlayLoopingSound(false, -1); // Ensure audio also stops
        }
        //Button interactivity
        SetButtonsInteractable(false, startButton);
    }

    void Update()
    {
        if (!isDrawing) return;

        UpdateStartMarkerPosition();
        UpdateMarkerRotationWithRemoteLeftHand();

        Ray ray = new(rayStartPoint.position, rayStartPoint.forward);
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();

        bool hasHit = room.Raycast(ray, rayLength, LabelFilter.FromEnum(labelFilter), out RaycastHit hit, out MRUKAnchor anchor);
        Vector3 hitPoint = hit.point;
        Vector3 hitNormal = hit.normal;

        debugText.transform.position = hitPoint;
        debugText.transform.rotation = Quaternion.LookRotation(-hitNormal);

        // Trigger state
        bool isTriggerHeld = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);

        // Marker detection
        bool markerHit = false;
        Renderer markerRenderer = null;

        GameObject[] markers = { startMarkerFront, startMarkerBack };
        foreach (var marker in markers)
        {
            if (marker == null || (usedStartMarker != null && marker != usedStartMarker)) continue;

            Renderer rend = marker.GetComponent<Renderer>();
            if (Physics.Raycast(ray, out RaycastHit markerHitInfo, rayLength))
            {
                if (markerHitInfo.transform == marker.transform)
                {
                    markerHit = true;
                    startPointMarkerInstance = marker;
                    markerRenderer = rend;
                    break;
                }
            }
        }

    

        // START DRAWING from one marker only (if not already started)
        if (!hasStartedDrawingFromMarker && isTriggerHeld && hitMarkerInstance != null && usedStartMarker == null)
        {
            Collider hitCol = hitMarkerInstance.GetComponent<Collider>();

            foreach (var marker in markers)
            {
                if (marker == null) continue;

                Collider markerCol = marker.GetComponent<Collider>();
                if (hitCol != null && markerCol != null && hitCol.bounds.Intersects(markerCol.bounds))
                {
                    // Raycast downward from marker to check for FLOOR
                    Ray markerRay = new Ray(marker.transform.position + Vector3.up * 0.1f, Vector3.down);
                    if (room.Raycast(markerRay, 0.3f, LabelFilter.FromEnum(MRUKAnchor.SceneLabels.FLOOR), out RaycastHit Hit, out MRUKAnchor markerAnchor))
                    {
                        hasStartedDrawingFromMarker = true;
                        startPointMarkerInstance = marker;
                        usedStartMarker = marker;

                        foreach (var rend in marker.GetComponentsInChildren<Renderer>())
                            rend.material = greenArrow;

                        HandleDrawing(Hit.point); // use FLOOR snapped point
                    }
                    else
                    {
                        SetStatus("Cannot start drawing — marker not above FLOOR!", Color.red);
                        hasStartedDrawingFromMarker = false;
                        usedStartMarker = null;
                        startPointMarkerInstance = null;
                    }
                    break;
                }
            }
        }


        // Continue drawing if on FLOOR
        if (hasStartedDrawingFromMarker && isTriggerHeld && hasHit)
        {
            string label = anchor.AnchorLabels[0];
            if (label == "FLOOR")
            {
                HandleDrawing(hit.point);
            }
            else
            {
                SetStatus("Draw only allowed on FLOOR!", Color.red);
            }
        }

        // Release trigger = finish drawing
        if (hasStartedDrawingFromMarker && !isTriggerHeld)
        {
            if (currentLine != null && points.Count > 1)
            {
                ConvertToPathSegments();
            }

            hasStartedDrawingFromMarker = false;

            if (markerRenderer != null)
                markerRenderer.material = redArrow;
        }

        // Reset marker if not drawing and not hovering
        if (!hasStartedDrawingFromMarker && !markerHit)
        {
            foreach (var marker in markers)
            {
                if (marker == null || (usedStartMarker != null && marker != usedStartMarker)) continue;

                foreach (var rend in marker.GetComponentsInChildren<Renderer>())
                {
                    if (rend.material != redArrow)
                        rend.material = redArrow;
                }
            }
        }

        // Floor feedback + hit marker
        if (hasHit)
        {
            if (hitMarkerInstance == null && hitMarkerPrefab != null)
            {
                hitMarkerInstance = Instantiate(hitMarkerPrefab, hitPoint, Quaternion.identity);
            }
            else if (hitMarkerInstance != null)
            {
                hitMarkerInstance.transform.position = hitPoint;
                hitMarkerInstance.transform.rotation = Quaternion.LookRotation(hitNormal);
                if (!hitMarkerInstance.activeSelf)
                    hitMarkerInstance.SetActive(true);
            }

            // Detect highlight over selected marker
            if (hitMarkerInstance != null && startPointMarkerInstance != null)
            {
                Collider hitCol = hitMarkerInstance.GetComponent<Collider>();
                Collider markerCol = startPointMarkerInstance.GetComponent<Collider>();

                if (hitCol != null && markerCol != null && hitCol.bounds.Intersects(markerCol.bounds))
                {
                    foreach (var rend in startPointMarkerInstance.GetComponentsInChildren<Renderer>())
                        rend.material = greenArrow;
                }
                else
                {
                    foreach (var rend in startPointMarkerInstance.GetComponentsInChildren<Renderer>())
                        rend.material = redArrow;
                }
            }

            string label = anchor.AnchorLabels[0];
            if (label == "FLOOR")
            {
                debugText.text = "";
                debugText.color = Color.green;
            }
            else
            {
                debugText.text = $"ANCHOR : {label}\nDraw is not possible!";
                debugText.color = Color.red;
            }
        }
        else
        {
            if (hitMarkerInstance != null)
                hitMarkerInstance.SetActive(false);

            debugText.text = "No valid surface detected!";
            debugText.color = Color.red;
        }
    }



    private void HandleDrawing(Vector3 hitPoint)
    {
        if (currentLine == null)
        {
            StartNewLine(hitPoint);
        }
        else
        {
            UpdateLine(hitPoint);
        }
    }

    private void StartNewLine(Vector3 hitPoint)
    {
        if (startPointMarkerInstance == null)
        {

            SetStatus("Start marker not found!", Color.red);
            Debug.LogWarning("[Path] Marker not available.");
            return;
        }

        Vector3 markerPosition = startPointMarkerInstance.transform.position;
        float allowedRadius = 0.2f; // .4m diameter = 0.2m radius
        float distanceToMarker = Vector3.Distance(hitPoint, markerPosition);

        if (distanceToMarker > allowedRadius)
        {
            SetStatus("Draw must start near the visible marker!", Color.red);
            Debug.LogWarning($"[Path] Hit point is {distanceToMarker:F2}m away from marker.");
            return;
        }

        // Valid point — snap to marker center
        Vector3 startPosition = markerPosition;

        GameObject newLine = Instantiate(linePrefab, startPosition, Quaternion.identity);
        currentLine = newLine.GetComponent<LineRenderer>();
        currentLine.useWorldSpace = true;

        points.Clear();
        points.Add(startPosition);
        currentLine.positionCount = 1;
        currentLine.SetPosition(0, startPosition);
        debugText.text = "Path started from marker area.!";
        debugText.color = Color.green;
        SetStatus("Path started from marker area.", Color.green);
        Debug.Log("[Path] Drawing started at marker zone.");
    }

    private void UpdateLine(Vector3 newPosition)
    {
        if (points.Count == 0)
            return;

        Vector3 lastPoint = points[^1];

        // Use MRUK raycast to detect if there's anything *other than FLOOR* between points
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        Vector3 direction = newPosition - lastPoint;
        float distance = direction.magnitude;

        if (room.Raycast(new Ray(lastPoint, direction.normalized), distance, out RaycastHit hit, out MRUKAnchor anchor))
        {
            // If label is not FLOOR, block drawing
            string label = anchor.AnchorLabels[0];
            if (label != "FLOOR")
            {
                SetStatus("Drawing blocked: you're pointing at a " + label.ToLower() + ". Please stay on the floor.", Color.red);
                Debug.LogWarning($"[Path] MRUK blocked path by {label} anchor.");
                return;
            }
        }

        // If safe, draw
        if (Vector3.Distance(lastPoint, newPosition) > pointDistance)
        {
            points.Add(newPosition);
            currentLine.positionCount = points.Count;
            currentLine.SetPositions(points.ToArray());
        }
    }



    private void ConvertToPathSegments()
    {
        Debug.Log("Converting LineRenderer to Networked Road Segments");


        for (int i = 1; i < points.Count; i++)
        {
            Vector3 start = points[i - 1];
            Vector3 end = points[i];
            Vector3 direction = (end - start).normalized;
            float distance = Vector3.Distance(start, end);

            Vector3 position = start + direction * (distance / 2 * 0.95f);
            Quaternion rotation = Quaternion.LookRotation(direction);
            Vector3 scale = new Vector3(segment_width, 0.01f, distance * 2f);

            GameObject tmpPath = pathSegmentPrefab;
            tmpPath.transform.localScale = scale;

            if (Runner != null && pathSegmentPrefab != null)
            {
                NetworkObject segment = Runner.Spawn(
                    tmpPath,
                    position + new Vector3(0, segment_Y_Offset, 0),
                    rotation,
                    Object.InputAuthority
                );

                if (segment.TryGetComponent(out Collider col))
                {
                    col.enabled = false;
                }

                
                pathSegments.Add(segment.gameObject);

                // Set the path segment to be a child of the current line
                Vector3 localPos = startPointMarkerInstance.transform.InverseTransformPoint(segment.transform.position);
                finalPathPoints.Add(localPos);
                //Debug.Log($"sending data points: x = {finalPathPoints[i].x}, y = {finalPathPoints[i].y})");

            }
        }
        //Button interactivity
        SetButtonsInteractable(true, startButton, clearButton);

        Destroy(currentLine.gameObject);
        currentLine = null;
        points.Clear();
    }

    

    //Called from the UI Start Button
    public void StartAvatarWalking()
    {
        if (avatarSpawned) return; // Already started
        // Check MQTT broker status before starting
        if (baseClient == null || !baseClient.IsBrokerConnected)
        {
            SetStatus("MQTT Broker not connected. Cannot start avatar.", Color.red);
            Debug.LogWarning("[MQTT] Broker not connected — aborting avatar start.");
            return;
        }

        avatarSpawned = true; // Set networked flag

        SetMarkerVisible(false);

        if (pathSegments.Count < 2 || avatarPrefab == null || Runner == null)
            return;

        Vector3 startPos = pathSegments[0].transform.position;
        startPos.y += 0.01f;

        activeAvatar = Runner.Spawn(
            avatarPrefab,
            startPos,
            Quaternion.identity,
            Object.InputAuthority
        );
        //button interactivity
        SetButtonsInteractable(false, startButton,addPath,clearButton);
        //StartCoroutine(WalkAvatar());
        walkAvatarRoutine = StartCoroutine(WalkAvatar());
    }



    private IEnumerator<WaitForEndOfFrame> WalkAvatar()
    {
        int targetIndex = 0;

        while (targetIndex < pathSegments.Count)
        {
            Vector3 targetPos = pathSegments[targetIndex].transform.position;
            targetPos.y += 0.01f;

            while (Vector3.Distance(activeAvatar.transform.position, targetPos) > 0.05f)
            {
                if (activeAvatar == null) yield break;

                if (startPointMarkerInstance != null)
                {

                    Vector3 markerPos = startPointMarkerInstance.transform.position;
                    float distanceToAvatar = Vector3.Distance(activeAvatar.transform.position, markerPos);

                    if (!IsMarkerOnPath(0.3f)) // Check if marker is near any path segment
                    {
                        SetStatus("Player is off the path!", Color.red);
                        //PlayClipLoopUntilChange(offPathClip);
                        RPC_PlayLoopingSound(true, 0); // Play offPathClip
                        yield return new WaitForEndOfFrame();
                        continue;
                    }

                    if (distanceToAvatar > 1.0f)
                    {
                        SetStatus("Player is too far behind!", Color.yellow);
                        RPC_PlayLoopingSound(true, 1); // Play tooFarClip
                        yield return new WaitForEndOfFrame();
                        continue;
                    }



                    //Both conditions met
                    SetStatus("Avatar walking... (Player nearby & on path)", Color.green);
                    RPC_PlayLoopingSound(true, 2); // Play walkingClip

                }

                // Move avatar toward target
                activeAvatar.transform.position = Vector3.MoveTowards(
                    activeAvatar.transform.position,
                    targetPos,
                    avatarSpeed * Time.deltaTime
                );

                // Rotate avatar smoothly
                Vector3 dir = (targetPos - activeAvatar.transform.position).normalized;
                if (dir != Vector3.zero)
                {
                    Quaternion rot = Quaternion.LookRotation(dir);
                    activeAvatar.transform.rotation = Quaternion.Slerp(
                        activeAvatar.transform.rotation,
                        rot,
                        Time.deltaTime * 5f
                    );
                }

                yield return new WaitForEndOfFrame();
            }

            targetIndex++;
            yield return null;
        }

        SetStatus("Avatar finished walking path!", Color.cyan);
        RPC_PlayLoopingSound(false, -1); // Stop playback
        // Reset button interactivity
        SetButtonsInteractable(true, addPath, clearButton);

    }



    private void SetStatus(string message, Color color)
    {
        statusText.text = message;
        statusText.color = color;
    }

    private Transform GetRemotePlayerTransform()
    {
        if (RemotePlayerTracker.Instance == null) return null;

        var remotes = RemotePlayerTracker.Instance.GetRemotePlayers();
        if (remotes.Count > 0)
        {
            return remotes[0].transform; // You could expand this for multiple players
        }

        return null;
    }

    private void ShowStartPointMarkers(Vector3 _unused)
    {
        if (startPointMarkerPrefab == null) return;

        if (startMarkerFront != null) Destroy(startMarkerFront);
        if (startMarkerBack != null) Destroy(startMarkerBack);

        var remotes = RemotePlayerTracker.Instance?.GetRemotePlayers();
        if (remotes == null || remotes.Count == 0) return;

        var remote = remotes[0];
        if (!remote.TryGetComponent(out NetworkedRigTracker rig) || rig.handProvider == null) return;

        Vector3 handPos = rig.handProvider.Position;
        Vector3 forward = rig.handProvider.Rotation * Vector3.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 frontPos = handPos + forward * markerDistance;
        Vector3 backPos = handPos - forward * markerDistance;

        var frontResult = SpawnMarkerOnFloor(frontPos, forward, "Front", 0f);
        startMarkerFront = frontResult.marker;

        Vector3 backAligned = new Vector3(backPos.x, frontResult.yPosition, backPos.z);
        Quaternion backRotation = startMarkerFront.transform.rotation;

        startMarkerBack = Instantiate(startPointMarkerPrefab, backAligned, backRotation);
        startMarkerBack.name = "StartMarker_Back";

        lastMarkerFollowPosition = handPos;
    }




    // Struct to return both marker object and Y position
    public struct MarkerResult
    {
        public GameObject marker;
        public float yPosition;

        public MarkerResult(GameObject obj, float y)
        {
            marker = obj;
            yPosition = y;
        }
    }

    private MarkerResult SpawnMarkerOnFloor(Vector3 guessPos, Vector3 forward, string label, float xRotation)
    {
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        Ray ray = new Ray(guessPos, Vector3.down);
        float rayDistance = 5f;

        Vector3 markerPosition;
        if (room.Raycast(ray, rayDistance, LabelFilter.FromEnum(MRUKAnchor.SceneLabels.FLOOR), out RaycastHit hit, out MRUKAnchor anchor))
        {
            markerPosition = hit.point;
            Debug.Log($"[Marker-{label}] Found FLOOR at: {markerPosition}");
        }
        else
        {
            markerPosition = guessPos + Vector3.down * 1.5f;
            SetStatus($"[Marker-{label}] Floor not found, using fallback.", Color.yellow);
            Debug.LogWarning($"[Marker-{label}] No FLOOR hit.");
        }

        float yRotation = Quaternion.LookRotation(forward).eulerAngles.y;
        Quaternion rotation = Quaternion.Euler(xRotation, yRotation, 0f);

        GameObject marker = Instantiate(startPointMarkerPrefab, markerPosition, rotation);
        marker.name = $"StartMarker_{label}";

        return new MarkerResult(marker, markerPosition.y);
    }

    private void UpdateStartMarkerPosition()
    {
        if (startMarkerFront == null || startMarkerBack == null) return;

        var remotes = RemotePlayerTracker.Instance?.GetRemotePlayers();
        if (remotes == null || remotes.Count == 0) return;

        var remote = remotes[0];
        if (!remote.TryGetComponent(out NetworkedRigTracker rig) || rig.handProvider == null) return;

        Vector3 handPos = rig.handProvider.Position;
        Vector3 forward = rig.handProvider.Rotation * Vector3.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 frontGuess = handPos + forward * markerDistance;
        Vector3 backGuess = handPos - forward * markerDistance;

        float frontY = UpdateMarkerOnFloor(startMarkerFront, frontGuess, forward, 0f);

        Vector3 backPosAligned = new Vector3(backGuess.x, frontY, backGuess.z);
        float backYRot = Quaternion.LookRotation(forward).eulerAngles.y;
        startMarkerBack.transform.position = backPosAligned;
        startMarkerBack.transform.rotation = Quaternion.Euler(0f, backYRot, 0f);

        lastMarkerFollowPosition = handPos;
    }


    private float UpdateMarkerOnFloor(GameObject marker, Vector3 guessPos, Vector3 forward, float xRotation)
    {
        if (marker == null) return guessPos.y;

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        Ray ray = new Ray(guessPos, Vector3.down);
        float rayDistance = 5f;

        Vector3 markerPosition;
        if (room.Raycast(ray, rayDistance, LabelFilter.FromEnum(MRUKAnchor.SceneLabels.FLOOR), out RaycastHit hit, out MRUKAnchor anchor))
        {
            markerPosition = hit.point;
        }
        else
        {
            markerPosition = guessPos + Vector3.down * 1.5f;
        }

        float yRotation = Quaternion.LookRotation(forward).eulerAngles.y;
        Quaternion rotation = Quaternion.Euler(xRotation, yRotation, 0f);

        marker.transform.position = markerPosition;
        marker.transform.rotation = rotation;

        return markerPosition.y; // Return the Y used
    }


    private void UpdateMarkerRotationWithRemoteLeftHand()
    {
        if (RemotePlayerTracker.Instance == null) return;
        var remotes = RemotePlayerTracker.Instance.GetRemotePlayers();
        if (remotes.Count == 0) return;

        var remote = remotes[0];
        if (!remote.TryGetComponent(out NetworkedRigTracker rig) || rig.handProvider == null) return;

        Vector3 forward = rig.handProvider.Rotation * Vector3.forward;
        forward.y = 0;

        if (forward.sqrMagnitude > 0.001f)
        {
            Quaternion rot = Quaternion.Euler(90f, Quaternion.LookRotation(forward).eulerAngles.y, 0f);
            Quaternion rot90 = Quaternion.Euler(-90f, Quaternion.LookRotation(forward).eulerAngles.y, 0f);
            if (startMarkerFront != null) startMarkerFront.transform.rotation = rot;
            if (startMarkerBack != null) startMarkerBack.transform.rotation = rot90;
        }
    }





    private bool IsMarkerOnPath(float threshold = 0.5f)
    {
        if (startPointMarkerInstance == null || pathSegments.Count == 0)
            return false;

        Vector3 markerPos = startPointMarkerInstance.transform.position;

        foreach (var segment in pathSegments)
        {
            Vector3 segmentPos = segment.transform.position;
            if (Vector3.Distance(markerPos, segmentPos) <= threshold)
            {
                return true;
            }
        }

        return false;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_PlayLoopingSound(NetworkBool play, int clipIndex)
    {
        if (play)
        {
            if (clipIndex != lastPlayedIndex)
            {
                Debug.Log($"[RPC] Switching to clip index {clipIndex}");
                AudioClip clip = GetClipByIndex(clipIndex);
                if (clip != null)
                {
                    audioSource.Stop();
                    audioSource.clip = clip;
                    audioSource.loop = true;
                    audioSource.Play();
                    lastPlayedIndex = clipIndex;
                }
            }
        }
        else
        {
            Debug.Log("[RPC] Stopping clip");
            audioSource.Stop();
            audioSource.loop = false;
            lastPlayedIndex = -1;
        }
    }


    private AudioClip GetClipByIndex(int index)
    {
        if (index >= 0 && index < audioClips.Count) return audioClips[index];
        return null;
    }



    private void SetMarkerVisible(bool isVisible)
    {
        GameObject[] markers = { startMarkerFront, startMarkerBack };
        foreach (var marker in markers)
        {
            if (marker == null) continue;
            foreach (var renderer in marker.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = isVisible;
            }
        }
    }

    public void SendPathToRobot()
    {
        if (baseClient == null)
        {
            Debug.LogWarning("BaseClient component is missing.");
            SetStatus("BaseClient component missing!", Color.red);
            return;
        }

        if (finalPathPoints == null || finalPathPoints.Count == 0)
        {
            Debug.LogWarning("No path data available to send.");
            SetStatus("No path data to send!", Color.red);
            return;
        }

        // Decide coordinate system based on which marker was used
        bool useXY = usedStartMarker == startMarkerFront;
        Debug.Log("[MQTT] Preparing to send " + (useXY ? "X,Y" : "X,-Y") + " coordinates based on marker used.");

        // Convert points accordingly
        List<Vector3> convertedPoints = new();
        foreach (var pt in finalPathPoints)
        {
            if (useXY)
                convertedPoints.Add(new Vector3(pt.x, pt.y, 0));      // Front: send X,Y
            else
                convertedPoints.Add(new Vector3(pt.x, -pt.y, 0));     // Back: send X,-Y
        }

        //baseClient.PublishJson(convertedPoints);
        baseClient.PublishBinary(convertedPoints);

        SetStatus("Path sent to robot (" + (useXY ? "X,Y" : "X,-Y") + ")", Color.cyan);
        Debug.Log("[MQTT] Final path published with coordinates: " + (useXY ? "X,Y" : "X,-Y"));
    }



}

