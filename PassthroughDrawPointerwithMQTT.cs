using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Meta.XR.MRUtilityKit;
using TMPro;
using Fusion;

public class PassthroughDrawPointer : NetworkBehaviour
{
    [Header("Prefab Settings")]
    public GameObject linePrefab;
    public GameObject pathSegmentPrefab;
    public float segment_Y_Offset = 0.05f;
    public float segment_width = 2f;

    [Header("Ray Settings")]
    public Transform rayStartPoint;
    public float rayLength = 5f;
    public MRUKAnchor.SceneLabels labelFilter;

    [Header("UI Display")]
    public TextMeshPro debugText;
    public TextMeshProUGUI statusText;

    [Header("Avatar Settings")]
    public NetworkObject avatarPrefab;        // Networked avatar prefab
    private NetworkObject activeAvatar;       // Instance of the avatar
    public float avatarSpeed = 1.5f;

    [Header("Torus Marker")]
    public GameObject torusMarkerPrefab;
    private GameObject torusMarkerInstance;

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

    public MqttPathSender mqttSender;
    private List<Vector3> finalPathPoints = new();

    void Start()
    {
        SetStatus("Please Click a Button to Continue!", Color.red);
    }

    public void StartCreatingPath()
    {
        if (isDrawing) return;

        Debug.Log("Path Drawing Mode Enabled");
        SetStatus("Path Drawing Mode Enabled!", Color.green);
        isDrawing = true;
        ShowStartPointMarker(GetRemotePlayerTransform()?.position ?? Vector3.zero);
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
    }


    void Update()
    {
        if (!isDrawing) return;

        UpdateStartMarkerPosition();

        Ray ray = new(rayStartPoint.position, rayStartPoint.forward);
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();

        bool hasHit = room.Raycast(ray, rayLength, LabelFilter.FromEnum(labelFilter), out RaycastHit hit, out MRUKAnchor anchor);

        if (hasHit)
        {
            Vector3 hitPoint = hit.point;
            Vector3 hitNormal = hit.normal;
            string label = anchor.AnchorLabels[0];

            debugText.transform.position = hitPoint;
            debugText.transform.rotation = Quaternion.LookRotation(-hitNormal);

            // Check if user is pointing near the marker
            if (startPointMarkerInstance != null)
            {
                float distToMarker = Vector3.Distance(hitPoint, startPointMarkerInstance.transform.position);
                bool markerHit = distToMarker < 0.5f;

                if (startPointMarkerInstance.TryGetComponent(out MarkerTextController textCtrl))
                {
                    textCtrl.SetSelected(markerHit);
                }
            }

            // Floor anchor detection
            if (label == "FLOOR")
            {
                debugText.text = "ANCHOR : Floor is Detected. You can draw now!";
                debugText.color = Color.green;

                if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
                {
                    HandleDrawing(hitPoint);
                }
                else if (currentLine != null && points.Count > 1)
                {
                    ConvertToPathSegments();
                }
            }
            else
            {
                debugText.text = $"ANCHOR : {label}\n Draw is not possible!";
                debugText.color = Color.red;
            }
        }
        else
        {
            debugText.text = "No valid surface detected!";
            debugText.color = Color.red;

            // Reset marker text if ray misses everything
            if (startPointMarkerInstance != null &&
                startPointMarkerInstance.TryGetComponent(out MarkerTextController textCtrl))
            {
                textCtrl.SetSelected(false);
            }
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

    // Called from the UI Start Button
    public void StartAvatarWalking()
    {
        if (avatarSpawned) return; // Already started

        //if (!Object.HasStateAuthority) return; // Only host/client with authority can trigger

        avatarSpawned = true; // Set networked flag

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
                        yield return new WaitForEndOfFrame();
                        continue;
                    }

                    if (distanceToAvatar > 1.0f)
                    {
                        SetStatus("Player is too far behind!", Color.yellow);
                        yield return new WaitForEndOfFrame();
                        continue;
                    }

                 

                    //Both conditions met
                    SetStatus("Avatar walking... (Player nearby & on path)", Color.green);
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
        float rayDistance = 3f;

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
        Quaternion userYaw = Quaternion.Euler(0f, remotePlayerPosition.y, 0f);
        Quaternion rotation = Quaternion.Euler(-90f, 0f, 0f) * userYaw;
        startPointMarkerInstance = Instantiate(startPointMarkerPrefab, markerPosition, rotation);
      


        //startPointMarkerInstance = Instantiate(startPointMarkerPrefab, markerPosition, Quaternion.identity);
        lastMarkerFollowPosition = remotePlayerPosition;
    }




    private void UpdateStartMarkerPosition()
    {
        if (startPointMarkerPrefab == null) return;

        Transform remotePlayer = GetRemotePlayerTransform();
        if (remotePlayer == null) return;

        Vector3 remotePos = remotePlayer.position;

        // Only update if moved more than threshold
        if (Vector3.Distance(remotePos, lastMarkerFollowPosition) < markerUpdateThreshold)
            return;

        lastMarkerFollowPosition = remotePos;

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        Ray ray = new Ray(remotePos, Vector3.down);
        float rayDistance = 3f;

        if (room.Raycast(ray, rayDistance, LabelFilter.FromEnum(MRUKAnchor.SceneLabels.FLOOR), out RaycastHit hit, out MRUKAnchor anchor))
        {
            Vector3 markerPosition = hit.point;

            if (startPointMarkerInstance == null)
            {
                startPointMarkerInstance = Instantiate(startPointMarkerPrefab, markerPosition, Quaternion.identity);
            }
            else
            {
                startPointMarkerInstance.transform.position = markerPosition;
                Quaternion userYaw = Quaternion.Euler(0f, remotePlayer.rotation.eulerAngles.y, 0f);
                startPointMarkerInstance.transform.rotation = Quaternion.Euler(-90f, 0f, 0f) * userYaw;
            }

            Debug.Log("[Marker] Marker updated to floor at: " + markerPosition);
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

    //for sending data to the server

    [System.Serializable]
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

    [System.Serializable]
    public class PathData
    {
        public List<PathPoint> points = new();
    }

    public string GetPathAsJson()
    {
        PathData data = new PathData();
        foreach (var point in finalPathPoints)
            data.points.Add(new PathPoint(point));

        return JsonUtility.ToJson(data);
    }


    //public void SendPathToRobot()
    //{
    //    if (mqttSender != null)
    //    {
    //        string json = GetPathAsJson();
    //        mqttSender.PublishPath(json);
    //        SetStatus("Path sent to robot!", Color.cyan);
    //    }
    //    else
    //    {
    //        Debug.LogWarning("No MQTT sender attached!");
    //    }
    //}

    public void SendPathToRobot()
    {
        if (mqttSender != null)
        {
            mqttSender.SendPath(finalPathPoints); // use saved path
            SetStatus("Path sent (JSON + Binary)", Color.cyan);
        }
        else
        {
            Debug.LogWarning("MQTT sender is missing.");
        }
    }



}
