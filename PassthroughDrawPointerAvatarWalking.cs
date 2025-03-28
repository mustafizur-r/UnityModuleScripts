using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Meta.XR.MRUtilityKit;
using TMPro;
using Fusion;

public class PassthroughDrawPointerAvatarWalking : NetworkBehaviour
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

    [Networked]
    private bool avatarSpawned { get; set; }  // Tracks if the avatar was already spawned

    // Internal state
    private LineRenderer currentLine;
    private List<Vector3> points = new();
    private List<GameObject> pathSegments = new();
    private bool isDrawing = false;

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

            if (label == "FLOOR")
            {
                debugText.text = "ANCHOR : Floor is Detected You can Draw now!";
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
                debugText.text = $"ANCHOR : {label}\nError: Draw is not possible!";
                debugText.color = Color.red;
            }
        }
        else
        {
            debugText.text = "Error: No valid surface detected!";
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

    private void StartNewLine(Vector3 startPosition)
    {
        GameObject newLine = Instantiate(linePrefab, startPosition, Quaternion.identity);
        currentLine = newLine.GetComponent<LineRenderer>();
        currentLine.useWorldSpace = true;

        points.Clear();
        points.Add(startPosition);
        currentLine.positionCount = 1;
        currentLine.SetPosition(0, startPosition);
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

                activeAvatar.transform.position = Vector3.MoveTowards(
                    activeAvatar.transform.position,
                    targetPos,
                    avatarSpeed * Time.deltaTime
                );

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

        SetStatus("Avatar Finished Path!", Color.cyan);
    }

    private void SetStatus(string message, Color color)
    {
        statusText.text = message;
        statusText.color = color;
    }
}
