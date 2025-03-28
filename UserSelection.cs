using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserSelection : MonoBehaviour
{
    public Button therapistButton;
    public Button patientButton;

    // These objects will be deactivated based on user selection
    public GameObject MRUKSceneObject;
    public GameObject EffectMesh;
    public GameObject TherapistPanel;
    public GameObject PatientPanel;
    public GameObject DebuglText;
    public TMP_Text connectingStatusText;

    // Reference to the UserSelection UI (the panel or parent object that contains the buttons)
    public GameObject userSelectionUI;

    private const string UserSelectionKey = "UserSelection"; // Key for saving user choice in PlayerPrefs

    void Start()
    {
        // Initially deactivate all objects
        if (MRUKSceneObject != null) MRUKSceneObject.SetActive(false);
        if (EffectMesh != null) EffectMesh.SetActive(false);
        if (TherapistPanel != null) TherapistPanel.SetActive(false);
        if (PatientPanel != null) PatientPanel.SetActive(false);
        if (DebuglText != null) DebuglText.SetActive(false);
        // Set Button false until connection is established.
        therapistButton.GetComponent<Button>().interactable = false;
        patientButton.GetComponent<Button>().interactable = false;


        // Ensure the UserSelection UI is enabled at the start
        if (userSelectionUI != null)
        {
            userSelectionUI.SetActive(true); // Make sure the UI is active initially
        }
        else
        {
            Debug.LogWarning("UserSelection UI is not assigned.");
        }

        // Load the previously saved user selection
        string savedSelection = PlayerPrefs.GetString(UserSelectionKey, "None");

        // Update the scene based on the saved user
        UpdateUserSelection(savedSelection);

        // Add listeners to buttons
        therapistButton.onClick.AddListener(() => SelectUser("Therapist"));
        patientButton.onClick.AddListener(() => SelectUser("Patient"));
    }

    void SelectUser(string userType)
    {
        // Save the selected user for future use in PlayerPrefs
        PlayerPrefs.SetString(UserSelectionKey, userType);
        PlayerPrefs.Save();

        // Update the objects based on user selection
        UpdateUserSelection(userType);

        // Deactivate the UserSelection UI after a selection is made
        if (userSelectionUI != null)
        {
            userSelectionUI.SetActive(false); // Deactivate the UI after selection
        }
    }

    void UpdateUserSelection(string userType)
    {
        if (userType == "Therapist")
        {
            MRUKSceneObject.SetActive(true);
            EffectMesh.SetActive(true);
            TherapistPanel.SetActive(true);
            DebuglText.SetActive(true);
            PatientPanel.SetActive(false);
        }
        else if (userType == "Patient")
        {
            MRUKSceneObject.SetActive(false);
            EffectMesh.SetActive(false);
            TherapistPanel.SetActive(false);
            DebuglText.SetActive(false);
            PatientPanel.SetActive(true);
        }
    }

    // Clear the user selection when the application is closed
    void OnApplicationQuit()
    {
        // Delete the saved user selection from PlayerPrefs
        PlayerPrefs.DeleteKey(UserSelectionKey);
        PlayerPrefs.Save();
    }

    public void SetText()
    {
        if (connectingStatusText != null)
        {
            connectingStatusText.text = "Connected !!";
            connectingStatusText.color = Color.green;
        }
        else
        {
            Debug.LogWarning("TMP_Text is not set.");
        }
    }
    public void SetButtonActive()
    {
        therapistButton.GetComponent<Button>().interactable = true;
        patientButton.GetComponent<Button>().interactable = true;
    }


}
