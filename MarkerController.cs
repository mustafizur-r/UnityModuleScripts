using TMPro;
using UnityEngine;

public class MarkerController : MonoBehaviour
{
    public Material greenArrow;
    public Material redArrow;
    public GameObject avatarObject;

    private bool isSelected = false;

    void Start()
    {
        // Set initial color to red
        if (avatarObject != null && redArrow != null)
        {
            SetMaterial(redArrow);
        }
    }

    public void SetSelected(bool selected)
    {
        if (avatarObject == null || greenArrow == null || redArrow == null) return;

        if (selected != isSelected)
        {
            isSelected = selected;

            // Change material based on selection
            SetMaterial(isSelected ? greenArrow : redArrow);
        }
    }

    private void SetMaterial(Material mat)
    {
        Renderer renderer = avatarObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = mat;
        }
    }
}
