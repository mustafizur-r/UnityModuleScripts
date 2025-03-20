using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class DisplayLabel : MonoBehaviour
{
    public Transform rayStartPoint;
    public float rayLength = 5;
    public MRUKAnchor.SceneLabels labelFilter;
    public TMPro.TextMeshPro debugText;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Ray ray = new Ray(rayStartPoint.position, rayStartPoint.forward);

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        bool hasHit = room.Raycast(ray, rayLength, LabelFilter.FromEnum(labelFilter), out RaycastHit hit, out MRUKAnchor anchor);

        if (hasHit)
        {
            Vector3 hitPoint = hit.point;
            Vector3 hitNormal = hit.normal;
            string label = anchor.AnchorLabels[0];

            debugText.transform.position = hitPoint;
            debugText.transform.rotation = Quaternion.LookRotation(-hitNormal);

            debugText.text = "ANCHOR : " + label;
            if(label == "FLOOR")
            {
                Debug.Log("Floor is detected");
            }
        }
    }
}