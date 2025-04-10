using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Meta.XR.MRUtilityKit;
using TMPro;
using Fusion;
using M2MqttUnity;
using UnityEngine.UI;

public class PassthroughDrawPointer : NetworkBehaviour
{
    [Header("Prefab Settings")]
    public GameObject linePrefab;
    public GameObject pathSegmentPrefab;
    public float segment_Y_Offset = 0.05f;
    public float segment_width = 2f;

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
    private GameObject startPointMarkerInstance;
    private Vector3 lastMarkerFollowPosition;
    private float markerUpdateThreshold = 0.1f;

    [Header("Hit Marker Settings")]
    public GameObject hitMarkerPrefab;
    private GameObject hitMarkerInstance;

    [Header("Marker Appearance")]
    public Material greenArrow;
    public Material redArrow;

    private bool hasStartedDrawingFromMarker = false;

    private List<Vector3> finalPathPoints = new();

    public BaseClient baseClient;


    void Start()
    {
        SetStatus("Please Click a Button to Continue!", Color.red);
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioClips = new List<AudioClip> { offPathClip, tooFarClip, walkingClip };
    }


    //General Function to Toggle Button Interactivity
    public void SetButtonsInteractable(bool state, params Button[] buttons)
    {
        foreach (var button in buttons)
        {
            if (button != null)
            {
                button.interactable = state;
            }
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

        ShowStartPointMarker(remotePlayer.position);
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

        //NEW: Destroy marker instance
        if (startPointMarkerInstance != null)
        {
            Destroy(startPointMarkerInstance);
            startPointMarkerInstance = null;
        }
    }


    void Update()
    {
        if (!isDrawing) return;

        UpdateStartMarkerPosition();

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

        if (startPointMarkerInstance != null)
        {
            markerRenderer = startPointMarkerInstance.GetComponent<Renderer>();

            if (Physics.Raycast(ray, out RaycastHit markerHitInfo, rayLength))
            {
                if (markerHitInfo.transform == startPointMarkerInstance.transform)
                {
                    markerHit = true;
                }
            }
        }

        if (!hasStartedDrawingFromMarker && isTriggerHeld && hitMarkerInstance != null && startPointMarkerInstance != null)
        {
            Collider hitCol = hitMarkerInstance.GetComponent<Collider>();
            Collider markerCol = startPointMarkerInstance.GetComponent<Collider>();

            if (hitCol != null && markerCol != null && hitCol.bounds.Intersects(markerCol.bounds))
            {
                hasStartedDrawingFromMarker = true;

                foreach (var rend in startPointMarkerInstance.GetComponentsInChildren<Renderer>())
                    rend.material = greenArrow;

                HandleDrawing(startPointMarkerInstance.transform.position);
            }
        }


        // Only draw when hit label is FLOOR
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

        // Finish drawing on trigger release
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

        // If hovering over nothing, reset to red (only if not drawing)
        if (!hasStartedDrawingFromMarker && !markerHit)
        {
            if (markerRenderer != null && markerRenderer.material != redArrow)
                markerRenderer.material = redArrow;
        }

        // Floor feedback
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

            // Detect if hit marker is intersecting the start marker collider
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
        if (Vector3.Distance(points[^1], newPosition) > 0.01f)
        {
            points.Add(newPosition);
            currentLine.positionCount = points.Count;
            currentLine.SetPositions(points.ToArray());
        }
    }

    private void ConvertToPathSegments()
    {
        Debug.Log("Converting LineRenderer to Networked Road Segments");
        // Store a copy of the original points for MQTT before clearing
        finalPathPoints = new List<Vector3>(points);

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
            }
        }

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

        StartCoroutine(WalkAvatar());
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


    private void ShowStartPointMarker(Vector3 remotePlayerPosition)
    {
        if (startPointMarkerPrefab == null || startPointMarkerInstance != null)
            return; // Only spawn once

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        Ray ray = new Ray(remotePlayerPosition, Vector3.down);
        float rayDistance = 5f;

        Vector3 markerPosition;

        if (room.Raycast(ray, rayDistance, LabelFilter.FromEnum(MRUKAnchor.SceneLabels.FLOOR), out RaycastHit hit, out MRUKAnchor anchor))
        {
            markerPosition = hit.point;
            Debug.Log("[Marker] Marker spawned directly on FLOOR at: " + markerPosition);
        }
        else
        {
            markerPosition = remotePlayerPosition + Vector3.down * 1.5f;
            SetStatus("Floor not found. Marker placed approximately.", Color.yellow);
            Debug.LogWarning("[Marker] No FLOOR detected, using fallback position.");
        }

        Quaternion markerRotation = Quaternion.identity;

        Transform remotePlayer = GetRemotePlayerTransform();
        if (remotePlayer != null && remotePlayer.TryGetComponent(out NetworkedRigTracker rig))
        {
            Vector3 forward = rig.FlatForward;
            if (forward.sqrMagnitude > 0.001f)
            {
                markerRotation = Quaternion.Euler(90f, Quaternion.LookRotation(forward).eulerAngles.y, 0f);
            }
        }

        startPointMarkerInstance = Instantiate(startPointMarkerPrefab, markerPosition, markerRotation);
        lastMarkerFollowPosition = remotePlayerPosition;
    }

    private void UpdateStartMarkerPosition()
    {
        if (startPointMarkerPrefab == null || startPointMarkerInstance == null) return;

        Transform remotePlayer = GetRemotePlayerTransform();
        if (remotePlayer == null) return;

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        Ray ray = new Ray(remotePlayer.position, Vector3.down);
        float rayDistance = 5f;

        if (room.Raycast(ray, rayDistance, LabelFilter.FromEnum(MRUKAnchor.SceneLabels.FLOOR), out RaycastHit hit, out MRUKAnchor anchor))
        {
            Vector3 markerPosition = hit.point;
            Quaternion targetRotation = startPointMarkerInstance.transform.rotation;

            if (remotePlayer.TryGetComponent(out NetworkedRigTracker rig))
            {
                Vector3 forward = rig.FlatForward;
                if (forward.sqrMagnitude > 0.001f)
                {
                    targetRotation = Quaternion.Euler(90f, Quaternion.LookRotation(forward).eulerAngles.y, 0f);
                }
            }

            // Instantly snap to updated marker position
            startPointMarkerInstance.transform.position = markerPosition;
            startPointMarkerInstance.transform.rotation = targetRotation;

            Debug.Log("[Marker] Instantly updated to: " + markerPosition);
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
        if (startPointMarkerInstance == null) return;

        // Disable/enable all renderers to hide/show the marker visually
        foreach (var renderer in startPointMarkerInstance.GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = isVisible;
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

        baseClient.PublishJson(finalPathPoints);
        baseClient.PublishBinary(finalPathPoints);
      

        SetStatus("Path sent (JSON + Binary)", Color.cyan);
    }

}

