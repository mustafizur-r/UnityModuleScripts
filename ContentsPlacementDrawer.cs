using UnityEngine;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using TMPro;
using Fusion;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;

public class ContentsPlacementDrawer : NetworkBehaviour
{
    [Header("Prefab Settings")]
    public GameObject linePrefab;              // Prefab for LineRenderer
    public GameObject[] contentsPrefabs;       // Prefab for path segments (e.g., cylinders)
    public float segment_Y_Offset = 0f;              // Y offset for the path segments
    //public float segment_width = 2f;                // Width of the road segments

    [Header("Ray Settings")]
    public Transform rayStartPoint;            // Starting point of the ray (usually controller position)
    public float rayLength = 40f;               // Maximum ray length
    public MRUKAnchor.SceneLabels labelFilter; // Scene label to be detected (e.g., FLOOR)

    [Header("UI Display")]
    public TextMeshPro debugText;              // 3D text for debug display
    public TextMeshProUGUI statusText;         // UI text for status messages

    // Internal state
    private LineRenderer currentLine;
    private List<Vector3> points = new();              // Points of the LineRenderer
    private List<GameObject> contentsSegments = new();     // List of spawned path segment GameObjects
    private bool isDrawing = false;                    // Whether currently in path drawing mode

    // selected content
    private GameObject selectedContent = null;
    public float fillSpacing = 0.05f;
    Vector3 selectedScale;

    [Header("Marker Settings")]
    public GameObject markerPrefab;
    private GameObject markerObject;

    private bool isErase = false;
    private GameObject lastTimeHitObj = null;
    private Material lastTimeMaterial = null;
    private Color originalEmissionColor;

    void Start()
    {
        // Initial UI message
        SetStatus("Please Click a Button to Continue!", Color.red);

        // Marker用オブジェクトを初期化して非表示にしておく
        markerObject = Instantiate(markerPrefab);
        markerObject.SetActive(false);
    }

    // Called when "Create Path" button is pressed
    public void StartCreatingPath()
    {
        this.isErase = false;

        if (isDrawing) return;

        Debug.Log("Contents Placement Mode Enabled");
        SetStatus("Contents Placement Mode Enabled!", Color.green);
        isDrawing = true;
        
        Debug.Log($"Erase: {isErase}");
    }

    // Called when "Clear" button is pressed
    public void ClearContents()
    {
        Debug.Log("Clear Contents");
        SetStatus("Contents Cleared Successfully!", Color.green);

        isDrawing = false;

        // Destroy current LineRenderer
        if (currentLine != null)
        {
            Destroy(currentLine.gameObject);
            currentLine = null;
        }

        // Destroy all path segment GameObjects
        foreach (var segment in contentsSegments)
        {
            Destroy(segment);
        }

        contentsSegments.Clear();
        points.Clear();

        if (debugText != null)
        {
            debugText.text = "";
        }
    }

    void Update()
    {
        // Cast a ray forward from the rayStartPoint
        Ray ray = new(rayStartPoint.position, rayStartPoint.forward);
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();

        // Perform raycast using Scene API
        bool hasHit = room.Raycast(ray, rayLength, LabelFilter.FromEnum(labelFilter), out RaycastHit hit, out MRUKAnchor anchor);
        bool forEraseHit = Physics.Raycast(ray, out RaycastHit hitErase, rayLength);
        //Debug.Log($"HitName: {hit.collider.name}");

        Vector3 hitPoint = hit.point;
        Vector3 hitNormal = hit.normal;
        string label = anchor.AnchorLabels[0];

        // --- Marker表示処理（常時）
        if (markerObject != null)
        {
            markerObject.SetActive(true);
            markerObject.transform.position = hitPoint;
            markerObject.transform.rotation = Quaternion.LookRotation(hitNormal); // 向きを合わせるなら
        }

        if (isErase && forEraseHit)
        {
            DeleteContentsAtRayHit(hitErase.collider.gameObject);
            return;
        }

        if (!isDrawing) return;

        if (hasHit)
        {
            // Set debug text position and orientation
            //debugText.transform.position = hitPoint;
            //debugText.transform.rotation = Quaternion.LookRotation(-hitNormal);

            if (label == "FLOOR")
            {
                //debugText.text = "ANCHOR : Floor is Detected You can Draw now!";
                //debugText.color = Color.green;

                if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
                {
                    HandleDrawing(hitPoint); // Handle drawing while trigger is pressed
                }
                else if (currentLine != null && points.Count > 1)
                {
                    //ConvertToContentsSegments(); // Convert the line into prefabs when trigger is released
                    CreateContentsInTheArea();
                }
            }
            else
            {
                //debugText.text = $"ANCHOR : {label}\nError: Draw is not possible!";
                //debugText.color = Color.red;
            }
        }
        else
        {
            //debugText.text = "Error: No valid surface detected!";
            //debugText.color = Color.red;

            if (markerObject != null)
            {
                markerObject.SetActive(false);
            }
        }
    }

    // Handle drawing based on current state
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

    // Initialize a new LineRenderer
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

    // Update the line while trigger is being held
    private void UpdateLine(Vector3 newPosition)
    {
        if (Vector3.Distance(points[^1], newPosition) > 0.01f)
        {
            points.Add(newPosition);
            currentLine.positionCount = points.Count;
            currentLine.SetPositions(points.ToArray());
        }
    }

    // Update status UI text
    private void SetStatus(string message, Color color)
    {
        statusText.text = message;
        statusText.color = color;
    }

    public void SetContents(string name)
    {
        int index = 0;
        this.selectedScale = new Vector3(0.1f, 0.1f, 0.1f);
        if (name == "magma")
        {
            index = 0;
            SetStatus("Select Magma", Color.green);
        }
        else if (name == "water")
        {
            index = 1;
            SetStatus("Select Water", Color.green);
        }
        else if (name == "sand")
        {
            index = 2;
            SetStatus("Select Sand", Color.green);
        }
        else if (name == "ice")
        {
            index = 3;
            SetStatus("Select Ice", Color.green);
        }
        else if (name == "stone")
        {
            index = 4;
            this.selectedScale.y = 1f;
            SetStatus("Select Stone", Color.green);
        }
        else if (name == "grass")
        {
            index = 5;
            this.selectedScale.y = 1f;
            SetStatus("Select Grass", Color.green);
        }

        this.selectedContent = contentsPrefabs[index];
    }

    public void CreateContentsInTheArea()
    {
        if (points.Count < 3 || selectedContent == null) return;

        Debug.Log("Filling area with contents...");

        // If the start and end points do not coincide, close at the start point
        if (points[^1] != points[0])
        {
            points.Add(points[0]);
        }

        // Find the polygon's bounding rectangle
        float minX = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxZ = float.MinValue;

        foreach (var pt in points)
        {
            if (pt.x < minX) minX = pt.x;
            if (pt.z < minZ) minZ = pt.z;
            if (pt.x > maxX) maxX = pt.x;
            if (pt.z > maxZ) maxZ = pt.z;
        }

        // オブジェクトの大きさに関する記述が必要（Prefabが毎回変化しているため）
        //// 外接矩形の範囲内で碁盤の目状に点を並べる
        //for (float x = minX; x <= maxX; x += fillSpacing)
        //{
        //    for (float z = minZ; z <= maxZ; z += fillSpacing)
        //    {
        //        Vector3 testPoint = new Vector3(x, points[0].y + segment_Y_Offset, z);
        //        if (IsPointInPolygonXZ(testPoint, points))
        //        {
        //            NetworkObject obj = Runner.Spawn(
        //            selectedContent,
        //            testPoint,
        //            Quaternion.identity,
        //            Object.InputAuthority
        //            );
        //            contentsSegments.Add(obj.gameObject);
        //        }
        //    }
        //}

        // Max Minに合わせて、一つの大きなオブジェクトを配置する
        GameObject tmpSelectedObject = selectedContent;
        //float height = Mathf.Min(maxX - minX, maxZ - minZ); 
        tmpSelectedObject.transform.localScale = new Vector3(maxX - minX, this.selectedScale.y, maxZ - minZ);
        NetworkObject obj = Runner.Spawn(
                    tmpSelectedObject,
                    new Vector3((minX + maxX) / 2.0f, points[0].y + segment_Y_Offset, (minZ + maxZ) / 2.0f),
                    Quaternion.identity,
                    Object.InputAuthority
                    );

        // 他のコンテンツと重なっていたら破壊する処理
        

        contentsSegments.Add(obj.gameObject);

        // Destroy the LineRenderer and reset state
        Destroy(currentLine.gameObject);
        currentLine = null;
        points.Clear();
    }

    // XZ平面上でポイントがポリゴン内にあるかを判定
    private bool IsPointInPolygonXZ(Vector3 testPoint, List<Vector3> polygon)
    {
        int intersectCount = 0;
        for (int i = 0; i < polygon.Count - 1; i++)
        {
            Vector3 a = polygon[i];
            Vector3 b = polygon[i + 1];

            if ((a.z > testPoint.z) != (b.z > testPoint.z))
            {
                float t = (testPoint.z - a.z) / (b.z - a.z);
                float xCross = a.x + t * (b.x - a.x);
                if (testPoint.x < xCross)
                    intersectCount++;
            }
        }

        return (intersectCount % 2) == 1; // 奇数なら内側
    }

    // Deactivate this code while the path is being drawn.
    public void SetThisObjcet(bool state)
    {
        this.gameObject.SetActive(state);
    }

    // isEraseがTrueで、Rayに何か当たっていれば毎回呼ばれる
    public void DeleteContentsAtRayHit(GameObject hitObj)
    {
        if (hitObj.CompareTag("Contents") || hitObj.CompareTag("OverlapContents"))
        {
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                Debug.Log($"Deleting Contents-tagged object: {hitObj.name}");
                SetStatus($"Deleting Contents-tagged object: {hitObj.name}", Color.green);
                Runner.Despawn(hitObj.GetComponent<NetworkObject>());
            }

            if (this.lastTimeHitObj == null)
            {
                this.lastTimeHitObj = hitObj;
                Renderer renderer = hitObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material material = renderer.material;
                    //Debug.Log("HitMaterialName: " + material.name);

                    this.lastTimeMaterial = material;
                    this.originalEmissionColor = material.GetColor("_EmissionColor");
                    Debug.Log($"EraseMaterialDebug 1: {originalEmissionColor}");
                    material.SetColor("_EmissionColor", Color.white * 0.2f);

                    this.lastTimeHitObj = hitObj;
                    return;
                }
                else
                {
                    Debug.Log("No material");
                }
                return;
            }
            if (hitObj == this.lastTimeHitObj)
            {
                return;
            }
        }

        // 前回と別のobjectが来た場合の処理
        if (this.lastTimeHitObj != null && this.lastTimeMaterial != null)
        {
            Debug.Log($"EraseMaterialDebug 2: {originalEmissionColor}");
            this.lastTimeMaterial.SetColor("_EmissionColor", this.originalEmissionColor);
            this.lastTimeHitObj = null;
            this.lastTimeMaterial = null;
        }
    }

    public void SetEraser()
    {
        this.isErase = true;
        this.isDrawing = false;
        Debug.Log($"Erase: {isErase}");
    }
}
