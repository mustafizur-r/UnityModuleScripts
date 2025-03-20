using UnityEngine;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using TMPro;

public class PassthroughDrawPointer : MonoBehaviour
{
    public GameObject linePrefab; // Prefab for LineRenderer
    public GameObject pathSegmentPrefab; // Prefab for path segments (cylinders)
    public Material roadMaterial;


    public Transform rayStartPoint;
    public float rayLength = 5;
    public MRUKAnchor.SceneLabels labelFilter;
    public TMPro.TextMeshPro debugText;
    public TextMeshProUGUI statusText;


    private LineRenderer currentLine;
    private List<Vector3> points = new List<Vector3>();
    private bool isDrawing = false; // Flag to enable drawing mode

    private List<GameObject> pathSegments = new List<GameObject>(); // Stores all path segments

    // Method to start drawing when "Create Path" button is pressed
    public void Start()
    {
        statusText.text = "Please Click a Button to Continue!";
        statusText.color = Color.red;
    }
    public void StartCreatingPath()
    {
        if (isDrawing) return;  // Prevent multiple activations

        Debug.Log("Path Drawing Mode Enabled");
        statusText.text = "Path Drawing Mode Enabled!";
        statusText.color = Color.green;
        isDrawing = true;  // Set flag to allow drawing
    }

    public void ClearPath()
    {
        Debug.Log("Clear Path");
        statusText.text = "Path Cleared Successfully!";
        statusText.color = Color.green;

        // Stop drawing
        isDrawing = false;

        // Destroy all line objects
        if (currentLine != null)
        {
            Destroy(currentLine.gameObject);
            currentLine = null;
        }

        // Destroy all segment prefabs
        foreach (var segment in pathSegments)
        {
            Destroy(segment);
        }
        pathSegments.Clear();
        points.Clear();  // Clear points list

        if (debugText != null)
        {

            debugText.text = "";
        }
    }
    // Update function to check trigger press/release
    void Update()
    {
        if (!isDrawing) return; // Only allow drawing if "Create Path" was pressed
        Ray ray = new Ray(rayStartPoint.position, rayStartPoint.forward);

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        bool hasHit = room.Raycast(ray, rayLength, LabelFilter.FromEnum(labelFilter), out RaycastHit hit, out MRUKAnchor anchor);

        // If the ray hits something, check if it's the floor or another object
        if (hasHit)
        {
            Vector3 hitPoint = hit.point;
            Vector3 hitNormal = hit.normal;
            string label = anchor.AnchorLabels[0];

            debugText.transform.position = hitPoint;
            debugText.transform.rotation = Quaternion.LookRotation(-hitNormal);

            //debugText.text = "ANCHOR : " + label;
            // If the ray hit an anchor with label "floor", proceed with drawing
            if (label == "FLOOR")
            {
                debugText.text = "ANCHOR : Floor is Detected You can Draw now!";
                debugText.color = Color.green;

                // If trigger is pressed, start drawing
                if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
                {
                    DrawPath(hitPoint);
                }
                // If trigger is released, convert to prefab segments
                else if (currentLine != null && points.Count > 1)
                {
                    ConvertToPathSegments();
                }
            }
            else
            {
                // If the ray hit something that isn't the floor, show an error message
                debugText.text = "ANCHOR : " + label + "\nError: Draw is not possible!";
                debugText.color = Color.red;
            }
        }
        else
        {
            // If the ray hits nothing, you can also show an error message (optional)
            debugText.text = "Error: No valid surface detected!";
            debugText.color = Color.red;
        }
    }

    // Draw the path with LineRenderer in real-time
    void DrawPath(Vector3 hitPoint)
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

    // Initialize a new LineRenderer at the hit position
    void StartNewLine(Vector3 startPosition)
    {
        GameObject newLine = Instantiate(linePrefab, startPosition, Quaternion.identity);
        currentLine = newLine.GetComponent<LineRenderer>();
        currentLine.useWorldSpace = true;

        points.Clear();
        points.Add(startPosition);
        currentLine.positionCount = 1;
        currentLine.SetPosition(0, startPosition);
    }

    // Update LineRenderer dynamically as the trigger is held
    void UpdateLine(Vector3 newPosition)
    {
        if (Vector3.Distance(points[points.Count - 1], newPosition) > 0.01f)
        {
            points.Add(newPosition);
            currentLine.positionCount = points.Count;
            currentLine.SetPositions(points.ToArray());
        }
    }

    //// Convert the drawn LineRenderer path to prefab segments
    //void ConvertToPathSegments()
    //{
    //    Debug.Log("Converting LineRenderer to Prefab Segments");

    //    for (int i = 1; i < points.Count; i++)
    //    {
    //        Vector3 startPoint = points[i - 1];
    //        Vector3 endPoint = points[i];
    //        Vector3 direction = (endPoint - startPoint).normalized;
    //        float distance = Vector3.Distance(startPoint, endPoint);

    //        GameObject segment = Instantiate(pathSegmentPrefab, startPoint, Quaternion.LookRotation(direction));
    //        segment.transform.localScale = new Vector3(segment.transform.localScale.x, segment.transform.localScale.y, distance);
    //        segment.transform.position = startPoint + direction * (distance / 2);

    //        pathSegments.Add(segment);
    //    }

    //    Destroy(currentLine.gameObject); // Remove LineRenderer after replacing with prefabs
    //    currentLine = null;
    //    points.Clear(); // Reset points for the next drawing
    //}

    void ConvertToPathSegments()
    {
        Debug.Log("Converting LineRenderer to Road Segments");

        for (int i = 1; i < points.Count; i++)
        {
            Vector3 startPoint = points[i - 1];
            Vector3 endPoint = points[i];
            Vector3 direction = (endPoint - startPoint).normalized;
            float distance = Vector3.Distance(startPoint, endPoint);

            // Instantiate the road segment
            GameObject roadSegment = Instantiate(pathSegmentPrefab, startPoint, Quaternion.identity);

            // Adjust scale: width = 2m, length = distance
            roadSegment.transform.localScale = new Vector3(2f, 0.01f, distance * 2f); // Small increase in length (1.1x)

            // Position: Shift slightly back to overlap with the previous segment
            roadSegment.transform.position = startPoint + direction * (distance / 2 * 0.95f);

            // Rotate to align with the road direction
            roadSegment.transform.rotation = Quaternion.LookRotation(direction);

            // Optionally disable colliders or set them to triggers
            Collider segmentCollider = roadSegment.GetComponent<Collider>();
            if (segmentCollider != null)
            {
                segmentCollider.enabled = false; // Disable the collider
            }

            pathSegments.Add(roadSegment);
        }

        // Remove LineRenderer after replacing with prefabs
        Destroy(currentLine.gameObject);
        currentLine = null;
        points.Clear(); // Reset points for the next drawing
    }





}
