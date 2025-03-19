using UnityEngine;
using System.Collections.Generic;

public class PassthroughDrawPointer : MonoBehaviour
{
    public GameObject linePrefab; // Prefab for LineRenderer
    public GameObject pathSegmentPrefab; // Prefab for path segments (cylinders)
    public Material roadMaterial;

    private LineRenderer currentLine;
    private List<Vector3> points = new List<Vector3>();
    private bool isDrawing = false; // Flag to enable drawing mode

    private List<GameObject> pathSegments = new List<GameObject>(); // Stores all path segments

    // Method to start drawing when "Create Path" button is pressed
    public void StartCreatingPath()
    {
        if (isDrawing) return;  // Prevent multiple activations

        Debug.Log("Path Drawing Enabled");
        isDrawing = true;  // Set flag to allow drawing
    }

    public void ClearPath()
    {
        Debug.Log("Clear Path");

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
    }

    // Update function to check trigger press/release
    void Update()
    {
        if (!isDrawing) return; // Only allow drawing if "Create Path" was pressed

        // If trigger is pressed, start drawing
        if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            DrawPath();
        }
        // If trigger is released, convert to prefab segments
        else if (currentLine != null && points.Count > 1)
        {
            ConvertToPathSegments();
        }
    }

    // Draw the path with LineRenderer in real-time
    void DrawPath()
    {
        Vector3 rayOrigin = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        Vector3 rayDirection = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch) * Vector3.forward;
        RaycastHit hit;

        if (Physics.Raycast(rayOrigin, rayDirection, out hit, 10f))
        {
            if (currentLine == null)
            {
                StartNewLine(hit.point);
            }
            UpdateLine(hit.point);
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

            pathSegments.Add(roadSegment);
        }

        // Remove LineRenderer after replacing with prefabs
        Destroy(currentLine.gameObject);
        currentLine = null;
        points.Clear(); // Reset points for the next drawing
    }





}
