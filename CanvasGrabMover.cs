using UnityEngine;

public class CanvasGrabMover : MonoBehaviour
{
    public Transform leftHandAnchor;   // Assign OVR LeftHandAnchor
    public Transform rightHandAnchor;  // Assign OVR RightHandAnchor

    private bool isBeingHeld = false;
    private Transform activeHand = null;
    private Vector3 grabOffset;

    void Update()
    {
        bool leftGrab = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
        bool rightGrab = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);

        if (!isBeingHeld && (leftGrab || rightGrab))
        {
            StartGrab(leftGrab ? leftHandAnchor : rightHandAnchor);
        }
        else if (isBeingHeld && !leftGrab && !rightGrab)
        {
            EndGrab();
        }

        if (isBeingHeld && activeHand != null)
        {
            transform.position = activeHand.position + grabOffset;
            transform.rotation = Quaternion.LookRotation(activeHand.forward);
        }
    }

    void StartGrab(Transform grabbingHand)
    {
        if (grabbingHand == null) return;

        activeHand = grabbingHand;
        grabOffset = transform.position - activeHand.position;
        isBeingHeld = true;
    }

    void EndGrab()
    {
        isBeingHeld = false;
        activeHand = null;
    }
}
