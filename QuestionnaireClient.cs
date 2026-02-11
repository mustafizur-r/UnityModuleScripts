using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[Serializable]
public class UserPhaseStatusDto
{
    public string user_id;
    public int phase;
}

[Serializable]
public class AnimSelectionDto
{
    public string model;
    public int animation_id;
}

[Serializable]
public class QuestionnaireResponseItemDto
{
    public string question_id;
    public AnswerWrapperDto answer;
}

[Serializable]
public class AnswerWrapperDto
{
    public string choice;
    public string[] locations;
    public int confidence;

    public string value;
    public string[] values;

    public int int_value;
}

[Serializable]
public class QuestionnaireSubmitPayloadDto
{
    public string participant_id;
    public int phase;
    public int animation_index;
    public string model;
    public List<QuestionnaireResponseItemDto> responses;
}

[Serializable]
public class SubmitResponseDto
{
    public string status;
    public bool saved;
    public string record_id;
}

[Serializable]
public class PhaseSchemaDto
{
    public int phase;
    public List<QuestionSchemaDto> questions;
}

[Serializable]
public class QuestionSchemaDto
{
    public string question_id;
    public string title;
    public string type;

    public string[] options;
    public string[] choice_options;
    public string[] location_options;
    public int[] confidence_options;
}

public class QuestionnaireClient : MonoBehaviour
{
    private static QuestionnaireClient Instance;

    [Header("Auto Dependency (no need to edit PreGenManager)")]
    [SerializeField] private PreGenManager preGenManager;

    [Header("UI Texts")]
    [SerializeField] private TMP_Text debugText;
    [SerializeField] private TMP_Text userIdText;
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text questionText;

    [Header("UI Buttons")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button doneButton;

    [Header("Root Answer Containers")]
    [SerializeField] private GameObject phase1Answer;
    [SerializeField] private GameObject phase2Answer;

    [Header("Answer Areas (Phase 1)")]
    [SerializeField] private GameObject answerAreaPhase1A;     // Q1..Q6
    [SerializeField] private GameObject answerAreaPhase1B7;    // Q7
    [SerializeField] private GameObject answerAreaPhase1B8;    // Q8
    [SerializeField] private GameObject answerAreaPhase1C910;  // Q9..Q10

    [Header("Answer Areas (Phase 1 - GT)")]
    [SerializeField] private GameObject answerButtons3Visible; // Q11..Q13
    [SerializeField] private GameObject answerButtons3YesNo;   // Q14
    [SerializeField] private GameObject answerLikert5;         // Q15

    [Header("Phase1A - Location Toggles (Q1..Q6)")]
    [SerializeField] private Toggle[] locationToggles;
    [SerializeField] private Text[] locationToggleLabels;

    [Header("Phase1B8 - Multi-select Toggles (Q8)")]
    [SerializeField] private Toggle[] q8Toggles;
    [SerializeField] private Text[] q8ToggleLabels;

    [Header("Button Groups")]
    [SerializeField] private Button[] groupButtons_Q1to6_Choice;        // Yes/Partially/No
    [SerializeField] private Button[] groupButtons_Q1to6_Confidence;    // 1..5
    [SerializeField] private Button[] groupButtons_Q7;
    [SerializeField] private Button[] groupButtons_Q11_Q13;
    [SerializeField] private Button[] groupButtons_Q14;

    [Tooltip("If you put 10 buttons here: first 5 are for Q9/Q10, last 5 are for Q15.")]
    [SerializeField] private Button[] groupButtons_Likert5;

    [SerializeField] private Button[] groupButtons_Phase2;

    [Header("Behavior")]
    [SerializeField] private bool requireAnswerBeforeNext = true;

    [Header("Startup Delay")]
    [SerializeField] private float startupDelaySeconds = 3.0f;

    [Header("Local Save")]
    [SerializeField] private string localFileName = "questionnaire_local.json";

    [Header("Save Debounce")]
    [SerializeField] private float saveDelaySeconds = 0.2f;

    [Header("Runtime")]
    [SerializeField] private int currentAnimationIndex = 1; // 1..5

    [Header("Selection Highlight Colors")]
    [SerializeField] private Color selectedColor = Color.yellow;

    [Tooltip("Normal color for unselected buttons. Default is #1E2A3D.")]
    [SerializeField] private Color normalColor = new Color(30f / 255f, 42f / 255f, 61f / 255f, 1f); // #1E2A3D

    [Header("Q1..Q6 Location Rule")]
    [Tooltip("If true: when choice == Yes, locations are required. For Partially/No, locations are optional.")]
    [SerializeField] private bool requireLocationOnlyWhenYes = true;

    [Tooltip("The button label text that counts as YES. Must match the UI button text.")]
    [SerializeField] private string yesLabel = "Yes";

    [Header("Submit Button (No validation mode)")]
    [Tooltip("If true: submit button will show on last question without any answer-count validation.")]
    [SerializeField] private bool submitWithoutValidation = true;

    [Header("After Submit Navigation")]
    [Tooltip("If true: Back button stays active after submit so user can review.")]
    [SerializeField] private bool allowBackAfterSubmit = true;

    [Tooltip("If true: after submit, answers are locked (buttons disabled). Back is still allowed if allowBackAfterSubmit=true.")]
    [SerializeField] private bool lockAnswersAfterSubmit = false;

    private string baseUrl;
    private string localSavePath;

    private string participantId = "";
    private int phase = 1;
    private string model = "pretrained";

    private List<QuestionnaireResponseItemDto> responses = new List<QuestionnaireResponseItemDto>();

    private PhaseSchemaDto activeSchema;
    private int currentQuestionIndex = 0;

    private bool savePending = false;
    private bool isInitializing = false;
    private bool isInitialized = false;

    private bool hasSubmitted = false;
    private bool isSubmitting = false;

    private string lastDebugMsg = "";

    private bool isApplyingLocationUI = false;
    private bool isApplyingQ8UI = false;

    private const int LikertCount = 5;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        localSavePath = Path.Combine(Application.persistentDataPath, localFileName);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (preGenManager == null)
            preGenManager = GetComponent<PreGenManager>();

        if (preGenManager == null)
            preGenManager = FindObjectOfType<PreGenManager>();

        if (preGenManager == null)
        {
            WriteDebug("PreGenManager not found. Add this script to the same GameObject or assign in Inspector.");
            enabled = false;
            return;
        }

        baseUrl = $"http://{preGenManager.ipaddress}:8000";

        HookButtons();
        HookAnswerButtonGroups();
        HookLocationTogglesForRealtimeValidation();
        HookQ8TogglesForRealtimeSave();

        LoadLocalIfExists();

        if (submitButton != null) submitButton.gameObject.SetActive(false);
        if (doneButton != null) doneButton.gameObject.SetActive(false);

        StartCoroutine(InitializeQuestionnaireFlow());
    }

    private void HookButtons()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnBack);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnNext);
        }

        if (submitButton != null)
        {
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(() =>
            {
                if (!isSubmitting)
                    StartCoroutine(SubmitToServer());
            });
            submitButton.gameObject.SetActive(false);
        }

        if (doneButton != null)
        {
            doneButton.onClick.RemoveAllListeners();
            doneButton.onClick.AddListener(OnDone);
            doneButton.gameObject.SetActive(false);
        }
    }

    private void HookAnswerButtonGroups()
    {
        HookSingleChoice_GroupButtons_Q1to6_Choice(groupButtons_Q1to6_Choice);
        HookLikert_GroupButtons_Q1to6_Confidence(groupButtons_Q1to6_Confidence);

        HookValueSingleChoice(groupButtons_Q7);
        HookValueSingleChoice(groupButtons_Q11_Q13);
        HookValueSingleChoice(groupButtons_Q14);

        HookValueLikert_Mapped(groupButtons_Likert5);
        HookValueSingleChoice(groupButtons_Phase2);

        ForceGroupNormal(groupButtons_Q1to6_Choice);
        ForceGroupNormal(groupButtons_Q1to6_Confidence);
        ForceGroupNormal(groupButtons_Q7);
        ForceGroupNormal(groupButtons_Q11_Q13);
        ForceGroupNormal(groupButtons_Q14);
        ForceGroupNormal(groupButtons_Likert5);
        ForceGroupNormal(groupButtons_Phase2);

        ValidateLikertSetupOnce();
    }

    private void ValidateLikertSetupOnce()
    {
        if (groupButtons_Likert5 == null || groupButtons_Likert5.Length == 0) return;

        if (groupButtons_Likert5.Length != LikertCount && groupButtons_Likert5.Length != LikertCount * 2)
        {
            WriteDebug("groupButtons_Likert5 should be 5 (only one Likert) or 10 (two Likert sets: Q9/Q10 then Q15). Current size: " + groupButtons_Likert5.Length);
        }
    }

    private void HookLocationTogglesForRealtimeValidation()
    {
        if (locationToggles == null) return;

        for (int i = 0; i < locationToggles.Length; i++)
        {
            Toggle t = locationToggles[i];
            if (t == null) continue;

            t.onValueChanged.RemoveAllListeners();
            t.onValueChanged.AddListener(_ =>
            {
                if (!isInitialized) return;
                if (isApplyingLocationUI) return;
                if (lockAnswersAfterSubmit && hasSubmitted) return;

                if (phase == 1 && currentQuestionIndex >= 0 && currentQuestionIndex <= 5)
                {
                    AnswerWrapperDto answer = GetOrCreateAnswer(GetCurrentQuestionId());
                    answer.locations = GetSelectedLocations();
                    AddOrReplaceResponse(GetCurrentQuestionId(), answer);
                }

                UpdateButtonsOnly();
            });
        }
    }

    private void HookQ8TogglesForRealtimeSave()
    {
        if (q8Toggles == null) return;

        for (int i = 0; i < q8Toggles.Length; i++)
        {
            Toggle t = q8Toggles[i];
            if (t == null) continue;

            t.onValueChanged.RemoveAllListeners();
            t.onValueChanged.AddListener(_ =>
            {
                if (!isInitialized) return;
                if (isApplyingQ8UI) return;
                if (isSubmitting) return;
                if (lockAnswersAfterSubmit && hasSubmitted) return;

                if (phase == 1 && currentQuestionIndex == 7)
                {
                    string qid = GetCurrentQuestionId(); // P1_Q8
                    AnswerWrapperDto a = GetOrCreateAnswer(qid);
                    a.values = GetSelectedQ8Values();
                    AddOrReplaceResponse(qid, a);
                }

                UpdateButtonsOnly();
            });
        }
    }

    private void OnDone()
    {
        WriteDebug("Exiting application...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator InitializeQuestionnaireFlow()
    {
        if (isInitializing) yield break;
        isInitializing = true;

        if (startupDelaySeconds > 0f)
            yield return new WaitForSeconds(startupDelaySeconds);

        yield return StartCoroutine(RefreshUserPhaseAndModel_Once());
        yield return StartCoroutine(FetchSchemaForCurrentPhase_Once());

        currentQuestionIndex = Mathf.Clamp(currentQuestionIndex, 0, Mathf.Max(0, GetQuestionCount() - 1));

        hasSubmitted = false;
        isSubmitting = false;

        RenderHeader();
        RenderCurrentQuestion();
        UpdateButtonsOnly();

        isInitialized = true;
        isInitializing = false;
    }

    private IEnumerator RefreshUserPhaseAndModel_Once()
    {
        baseUrl = $"http://{preGenManager.ipaddress}:8000";

        int oldPhase = phase;

        yield return StartCoroutine(FetchUserPhase());
        yield return StartCoroutine(FetchAnimationSelection());

        if (phase != oldPhase)
        {
            currentQuestionIndex = 0;
            if (responses != null) responses.Clear();
            hasSubmitted = false;
            isSubmitting = false;

            ForceGroupNormal(groupButtons_Q1to6_Choice);
            ForceGroupNormal(groupButtons_Q1to6_Confidence);
            ForceGroupNormal(groupButtons_Q7);
            ForceGroupNormal(groupButtons_Q11_Q13);
            ForceGroupNormal(groupButtons_Q14);
            ForceGroupNormal(groupButtons_Likert5);
            ForceGroupNormal(groupButtons_Phase2);
        }

        ScheduleSaveLocal();
        WriteDebug($"Loaded: participant={participantId} phase={phase} model={model} anim={currentAnimationIndex}");
    }

    private IEnumerator FetchUserPhase()
    {
        using (UnityWebRequest req = UnityWebRequest.Get($"{baseUrl}/userid_phase_selection_status"))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                WriteDebug("FetchUserPhase failed: " + req.error);
                yield break;
            }

            var data = JsonUtility.FromJson<UserPhaseStatusDto>(req.downloadHandler.text);

            if (data != null && !string.IsNullOrEmpty(data.user_id))
                participantId = data.user_id;

            if (data != null && data.phase >= 1)
                phase = data.phase;
        }
    }

    private IEnumerator FetchAnimationSelection()
    {
        using (UnityWebRequest req = UnityWebRequest.Get($"{baseUrl}/get_animation_selection/"))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                WriteDebug("FetchAnimationSelection failed: " + req.error);
                yield break;
            }

            var data = JsonUtility.FromJson<AnimSelectionDto>(req.downloadHandler.text);

            if (data != null)
            {
                if (!string.IsNullOrEmpty(data.model))
                    model = data.model;

                if (data.animation_id >= 1 && data.animation_id <= 5)
                    currentAnimationIndex = data.animation_id;
            }
        }
    }

    private IEnumerator FetchSchemaForCurrentPhase_Once()
    {
        activeSchema = null;

        string url = $"{baseUrl}/questionnaire/schema?phase={phase}&animation_index={currentAnimationIndex}";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                WriteDebug("FetchSchema failed: " + req.error + "\n" + req.downloadHandler.text);
                yield break;
            }

            activeSchema = JsonUtility.FromJson<PhaseSchemaDto>(req.downloadHandler.text);
        }

        if (activeSchema == null || activeSchema.questions == null)
        {
            WriteDebug("Schema loaded but empty.");
            yield break;
        }

        currentQuestionIndex = Mathf.Clamp(currentQuestionIndex, 0, Mathf.Max(0, GetQuestionCount() - 1));
        ScheduleSaveLocal();
    }

    private void RenderHeader()
    {
        if (userIdText != null)
            userIdText.text = $"UserId: {participantId}";

        if (phaseText != null)
            phaseText.text = $"Phase: Phase {phase}";
    }

    private void RenderCurrentQuestion()
    {
        int total = GetQuestionCount();
        if (total <= 0)
        {
            if (progressText != null) progressText.text = "0/0";
            if (questionText != null) questionText.text = "No questions found.";
            DisableAllAnswerAreas();
            TogglePhaseRootPanels();
            return;
        }

        currentQuestionIndex = Mathf.Clamp(currentQuestionIndex, 0, total - 1);

        if (progressText != null)
            progressText.text = $"{currentQuestionIndex + 1}/{total}";

        QuestionSchemaDto q = GetCurrentQuestion();

        TogglePhaseRootPanels();
        SetAnswerAreaForCurrentQuestion();

        if (phase == 1 && currentQuestionIndex >= 0 && currentQuestionIndex <= 5)
            HandlePhase1ALocationQuestion(q);

        if (phase == 1 && currentQuestionIndex == 7)
            HandlePhase1BQ8MultiChoice(q);

        if (questionText != null)
            questionText.text = (q != null && !string.IsNullOrEmpty(q.title)) ? q.title : "";

        RestoreSelectionUIForCurrentQuestion();
        UpdateAnswerInteractivity();
    }

    private void TogglePhaseRootPanels()
    {
        if (phase1Answer != null)
            phase1Answer.SetActive(phase != 2);

        if (phase2Answer != null)
            phase2Answer.SetActive(phase == 2);
    }

    private void DisableAllAnswerAreas()
    {
        if (answerAreaPhase1A != null) answerAreaPhase1A.SetActive(false);
        if (answerAreaPhase1B7 != null) answerAreaPhase1B7.SetActive(false);
        if (answerAreaPhase1B8 != null) answerAreaPhase1B8.SetActive(false);
        if (answerAreaPhase1C910 != null) answerAreaPhase1C910.SetActive(false);

        if (answerButtons3Visible != null) answerButtons3Visible.SetActive(false);
        if (answerButtons3YesNo != null) answerButtons3YesNo.SetActive(false);
        if (answerLikert5 != null) answerLikert5.SetActive(false);
    }

    private void SetAnswerAreaForCurrentQuestion()
    {
        DisableAllAnswerAreas();

        if (phase == 2)
            return;

        if (currentQuestionIndex >= 0 && currentQuestionIndex <= 5)
        {
            if (answerAreaPhase1A != null) answerAreaPhase1A.SetActive(true);
            return;
        }

        if (currentQuestionIndex == 6)
        {
            if (answerAreaPhase1B7 != null) answerAreaPhase1B7.SetActive(true);
            return;
        }

        if (currentQuestionIndex == 7)
        {
            if (answerAreaPhase1B8 != null) answerAreaPhase1B8.SetActive(true);
            return;
        }

        if (currentQuestionIndex == 8 || currentQuestionIndex == 9)
        {
            if (answerAreaPhase1C910 != null) answerAreaPhase1C910.SetActive(true);
            return;
        }

        if (currentQuestionIndex >= 10 && currentQuestionIndex <= 12)
        {
            if (answerButtons3Visible != null) answerButtons3Visible.SetActive(true);
            return;
        }

        if (currentQuestionIndex == 13)
        {
            if (answerButtons3YesNo != null) answerButtons3YesNo.SetActive(true);
            return;
        }

        if (currentQuestionIndex == 14)
        {
            if (answerLikert5 != null) answerLikert5.SetActive(true);
            return;
        }
    }

    private void HandlePhase1ALocationQuestion(QuestionSchemaDto q)
    {
        if (q == null)
        {
            if (answerAreaPhase1A != null) answerAreaPhase1A.SetActive(false);
            return;
        }

        bool isQ1toQ6 = (currentQuestionIndex >= 0 && currentQuestionIndex <= 5);
        if (!isQ1toQ6)
        {
            if (answerAreaPhase1A != null) answerAreaPhase1A.SetActive(false);
            return;
        }

        if (!ValidateLocationUI())
        {
            if (answerAreaPhase1A != null) answerAreaPhase1A.SetActive(false);
            return;
        }

        if (answerAreaPhase1A != null)
            answerAreaPhase1A.SetActive(true);

        isApplyingLocationUI = true;
        PopulateLocationToggles(q.location_options);
        RestoreLocationSelectionIfAny(q.question_id);
        isApplyingLocationUI = false;
    }

    private void HandlePhase1BQ8MultiChoice(QuestionSchemaDto q)
    {
        if (q == null) return;

        if (q8Toggles == null || q8ToggleLabels == null) return;
        if (q8Toggles.Length == 0 || q8ToggleLabels.Length == 0) return;
        if (q8Toggles.Length != q8ToggleLabels.Length) return;

        isApplyingQ8UI = true;

        for (int i = 0; i < q8Toggles.Length; i++)
        {
            if (q8Toggles[i] == null || q8ToggleLabels[i] == null) continue;

            if (q.options != null && i < q.options.Length && !string.IsNullOrEmpty(q.options[i]))
            {
                q8Toggles[i].gameObject.SetActive(true);
                q8ToggleLabels[i].text = q.options[i];
            }
            else
            {
                q8Toggles[i].isOn = false;
                q8Toggles[i].gameObject.SetActive(false);
            }
        }

        RestoreQ8SelectionIfAny(q.question_id);

        isApplyingQ8UI = false;
    }

    private void RestoreQ8SelectionIfAny(string questionId)
    {
        AnswerWrapperDto ans = FindAnswer(questionId);
        HashSet<string> set = new HashSet<string>(ans != null && ans.values != null ? ans.values : Array.Empty<string>());

        for (int i = 0; i < q8Toggles.Length; i++)
        {
            if (q8Toggles[i] == null || q8ToggleLabels[i] == null) continue;
            if (!q8Toggles[i].gameObject.activeSelf) continue;

            q8Toggles[i].isOn = set.Contains(q8ToggleLabels[i].text);
        }
    }

    private bool ValidateLocationUI()
    {
        if (answerAreaPhase1A == null) return false;
        if (locationToggles == null || locationToggleLabels == null) return false;
        if (locationToggles.Length == 0 || locationToggleLabels.Length == 0) return false;
        if (locationToggles.Length != locationToggleLabels.Length) return false;
        return true;
    }

    private void PopulateLocationToggles(string[] options)
    {
        for (int i = 0; i < locationToggles.Length; i++)
        {
            if (locationToggles[i] == null || locationToggleLabels[i] == null)
                continue;

            if (options != null && i < options.Length && !string.IsNullOrEmpty(options[i]))
            {
                locationToggles[i].gameObject.SetActive(true);
                locationToggleLabels[i].text = options[i];
            }
            else
            {
                locationToggles[i].isOn = false;
                locationToggles[i].gameObject.SetActive(false);
            }
        }
    }

    private void RestoreLocationSelectionIfAny(string questionId)
    {
        if (string.IsNullOrEmpty(questionId)) return;

        AnswerWrapperDto ans = FindAnswer(questionId);
        if (ans == null)
        {
            for (int i = 0; i < locationToggles.Length; i++)
            {
                if (locationToggles[i] == null) continue;
                if (!locationToggles[i].gameObject.activeSelf) continue;
                locationToggles[i].isOn = false;
            }
            return;
        }

        HashSet<string> savedSet = new HashSet<string>(ans.locations ?? Array.Empty<string>());

        for (int i = 0; i < locationToggles.Length; i++)
        {
            if (locationToggles[i] == null || locationToggleLabels[i] == null)
                continue;

            if (!locationToggles[i].gameObject.activeSelf)
                continue;

            string label = locationToggleLabels[i].text;
            locationToggles[i].isOn = savedSet.Contains(label);
        }
    }

    private string[] GetSelectedLocations()
    {
        if (locationToggles == null || locationToggleLabels == null)
            return Array.Empty<string>();

        List<string> selected = new List<string>();

        for (int i = 0; i < locationToggles.Length; i++)
        {
            if (locationToggles[i] == null || locationToggleLabels[i] == null)
                continue;

            if (locationToggles[i].gameObject.activeSelf && locationToggles[i].isOn)
                selected.Add(locationToggleLabels[i].text);
        }

        return selected.ToArray();
    }

    private string[] GetSelectedQ8Values()
    {
        if (q8Toggles == null || q8ToggleLabels == null)
            return Array.Empty<string>();

        List<string> selected = new List<string>();

        for (int i = 0; i < q8Toggles.Length; i++)
        {
            if (q8Toggles[i] == null || q8ToggleLabels[i] == null)
                continue;

            if (q8Toggles[i].gameObject.activeSelf && q8Toggles[i].isOn)
                selected.Add(q8ToggleLabels[i].text);
        }

        return selected.ToArray();
    }

    private void UpdateButtonsOnly()
    {
        int total = GetQuestionCount();
        bool hasQuestions = total > 0;

        bool isFirst = currentQuestionIndex <= 0;
        bool isLast = hasQuestions && currentQuestionIndex >= total - 1;

        if (backButton != null)
        {
            bool allowBack = hasQuestions && !isFirst && !isSubmitting;
            if (hasSubmitted && !allowBackAfterSubmit) allowBack = false;
            backButton.interactable = allowBack;
        }

        if (nextButton != null)
        {
            bool nextVisible = hasQuestions && !isLast && !hasSubmitted;
            nextButton.gameObject.SetActive(nextVisible);

            bool nextInter = true;
            if (requireAnswerBeforeNext && nextVisible)
                nextInter = HasAnswer(GetCurrentQuestionId());

            nextButton.interactable = nextInter && !isSubmitting;
        }

        if (submitButton != null)
        {
            bool canShowSubmit = hasQuestions && isLast && !hasSubmitted;
            submitButton.gameObject.SetActive(canShowSubmit);

            submitButton.interactable = canShowSubmit && !isSubmitting;

            if (!submitWithoutValidation && submitButton.gameObject.activeSelf)
                submitButton.interactable = submitButton.interactable && HasAnswer(GetCurrentQuestionId());
        }

        if (doneButton != null)
        {
            doneButton.gameObject.SetActive(hasSubmitted);
            doneButton.interactable = hasSubmitted;
        }
    }

    private void UpdateAnswerInteractivity()
    {
        bool enable = !(lockAnswersAfterSubmit && hasSubmitted);

        SetGroupInteractable(groupButtons_Q1to6_Choice, enable);
        SetGroupInteractable(groupButtons_Q1to6_Confidence, enable);
        SetGroupInteractable(groupButtons_Q7, enable);
        SetGroupInteractable(groupButtons_Q11_Q13, enable);
        SetGroupInteractable(groupButtons_Q14, enable);
        SetGroupInteractable(groupButtons_Likert5, enable);
        SetGroupInteractable(groupButtons_Phase2, enable);

        if (locationToggles != null)
        {
            for (int i = 0; i < locationToggles.Length; i++)
            {
                if (locationToggles[i] == null) continue;
                locationToggles[i].interactable = enable;
            }
        }

        if (q8Toggles != null)
        {
            for (int i = 0; i < q8Toggles.Length; i++)
            {
                if (q8Toggles[i] == null) continue;
                q8Toggles[i].interactable = enable;
            }
        }
    }

    private void SetGroupInteractable(Button[] group, bool enabled)
    {
        if (group == null) return;
        for (int i = 0; i < group.Length; i++)
        {
            if (group[i] == null) continue;
            group[i].interactable = enabled;
        }
    }

    private void SaveLocationsIfCurrentIsQ1toQ6()
    {
        if (phase == 1 && currentQuestionIndex >= 0 && currentQuestionIndex <= 5)
        {
            string qid = GetCurrentQuestionId();
            if (string.IsNullOrEmpty(qid)) return;

            AnswerWrapperDto answer = GetOrCreateAnswer(qid);
            answer.locations = GetSelectedLocations();
            AddOrReplaceResponse(qid, answer);
        }
    }

    private void SaveQ8IfCurrentIsQ8()
    {
        if (phase == 1 && currentQuestionIndex == 7)
        {
            string qid = GetCurrentQuestionId(); // P1_Q8
            if (string.IsNullOrEmpty(qid)) return;

            AnswerWrapperDto a = GetOrCreateAnswer(qid);
            a.values = GetSelectedQ8Values();
            AddOrReplaceResponse(qid, a);
        }
    }

    private void OnNext()
    {
        if (!isInitialized) return;
        if (hasSubmitted || isSubmitting) return;

        int total = GetQuestionCount();
        if (total <= 0) return;

        SaveLocationsIfCurrentIsQ1toQ6();
        SaveQ8IfCurrentIsQ8();

        if (requireAnswerBeforeNext && !HasAnswer(GetCurrentQuestionId()))
        {
            WriteDebug("Please answer the current question before going next.");
            return;
        }

        int next = currentQuestionIndex + 1;
        if (next >= total) return;

        currentQuestionIndex = next;

        ScheduleSaveLocal();

        RenderCurrentQuestion();
        UpdateButtonsOnly();
    }

    private void OnBack()
    {
        if (!isInitialized) return;
        if (isSubmitting) return;

        int total = GetQuestionCount();
        if (total <= 0) return;

        SaveLocationsIfCurrentIsQ1toQ6();
        SaveQ8IfCurrentIsQ8();

        int prev = currentQuestionIndex - 1;
        if (prev < 0) return;

        currentQuestionIndex = prev;

        ScheduleSaveLocal();

        RenderCurrentQuestion();
        UpdateButtonsOnly();
    }

    public string GetCurrentQuestionId()
    {
        QuestionSchemaDto q = GetCurrentQuestion();
        return (q != null) ? q.question_id : "";
    }

    public QuestionSchemaDto GetCurrentQuestion()
    {
        if (activeSchema == null || activeSchema.questions == null) return null;
        if (activeSchema.questions.Count == 0) return null;

        currentQuestionIndex = Mathf.Clamp(currentQuestionIndex, 0, activeSchema.questions.Count - 1);
        return activeSchema.questions[currentQuestionIndex];
    }

    public void AddOrReplaceResponse(string questionId, AnswerWrapperDto answer)
    {
        if (string.IsNullOrEmpty(questionId)) return;

        if (responses == null)
            responses = new List<QuestionnaireResponseItemDto>();

        int idx = responses.FindIndex(r => r != null && r.question_id == questionId);

        if (idx >= 0)
            responses[idx].answer = answer;
        else
            responses.Add(new QuestionnaireResponseItemDto { question_id = questionId, answer = answer });

        ScheduleSaveLocal();
    }

    public bool HasAnswer(string questionId)
    {
        if (responses == null) return false;
        if (string.IsNullOrEmpty(questionId)) return false;

        QuestionSchemaDto q = GetCurrentQuestion();
        AnswerWrapperDto ans = FindAnswer(questionId);
        if (q == null || ans == null) return false;

        if (q.type == "triple_with_location_confidence")
        {
            bool hasChoice = !string.IsNullOrEmpty(ans.choice);
            bool hasConf = ans.confidence >= 1 && ans.confidence <= 5;

            bool mustHaveLocation = true;
            if (requireLocationOnlyWhenYes)
                mustHaveLocation = string.Equals(ans.choice, yesLabel, StringComparison.OrdinalIgnoreCase);

            bool hasLoc = (ans.locations != null && ans.locations.Length > 0);

            if (mustHaveLocation)
                return hasChoice && hasConf && hasLoc;

            return hasChoice && hasConf;
        }

        if (q.type == "multi_choice")
            return ans.values != null && ans.values.Length > 0;

        if (q.type == "rating_1_5")
            return ans.int_value >= 1 && ans.int_value <= 5;

        return !string.IsNullOrEmpty(ans.value);
    }

    private int GetQuestionCount()
    {
        if (activeSchema == null || activeSchema.questions == null) return 0;
        return activeSchema.questions.Count;
    }

    private void ScheduleSaveLocal()
    {
        if (savePending) return;
        savePending = true;
        StartCoroutine(SaveLocalDelayed());
    }

    private IEnumerator SaveLocalDelayed()
    {
        yield return new WaitForSeconds(saveDelaySeconds);
        savePending = false;
        SaveLocal();
    }

    private void SaveLocal()
    {
        if (responses == null)
            responses = new List<QuestionnaireResponseItemDto>();

        var payload = new QuestionnaireSubmitPayloadDto
        {
            participant_id = participantId,
            phase = phase,
            animation_index = currentAnimationIndex,
            model = model,
            responses = responses
        };

        try
        {
            File.WriteAllText(localSavePath, JsonUtility.ToJson(payload, true));
        }
        catch (Exception e)
        {
            WriteDebug("SaveLocal failed: " + e.Message);
        }
    }

    private void LoadLocalIfExists()
    {
        if (!File.Exists(localSavePath))
            return;

        try
        {
            var payload = JsonUtility.FromJson<QuestionnaireSubmitPayloadDto>(File.ReadAllText(localSavePath));
            if (payload == null)
                return;

            participantId = string.IsNullOrEmpty(payload.participant_id) ? participantId : payload.participant_id;
            phase = payload.phase <= 0 ? phase : payload.phase;
            currentAnimationIndex = Mathf.Clamp(payload.animation_index, 1, 5);
            model = string.IsNullOrEmpty(payload.model) ? model : payload.model;
            responses = payload.responses ?? new List<QuestionnaireResponseItemDto>();

            hasSubmitted = false;
            isSubmitting = false;

            WriteDebug("Loaded local questionnaire progress.");
        }
        catch (Exception e)
        {
            WriteDebug("LoadLocal failed: " + e.Message);
        }
    }

    private IEnumerator SubmitToServer()
    {
        if (isSubmitting) yield break;
        isSubmitting = true;

        UpdateButtonsOnly();

        SaveLocationsIfCurrentIsQ1toQ6();
        SaveQ8IfCurrentIsQ8();

        yield return StartCoroutine(RefreshUserPhaseAndModel_Once());

        if (activeSchema == null || GetQuestionCount() == 0)
            yield return StartCoroutine(FetchSchemaForCurrentPhase_Once());

        var payload = new QuestionnaireSubmitPayloadDto
        {
            participant_id = participantId,
            phase = phase,
            animation_index = currentAnimationIndex,
            model = model,
            responses = responses ?? new List<QuestionnaireResponseItemDto>()
        };

        string json = JsonUtility.ToJson(payload, true);

        using (UnityWebRequest req = new UnityWebRequest($"{baseUrl}/questionnaire/submit", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            WriteDebug("Submitting...");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                WriteDebug("Submit failed: " + req.error + "\n" + req.downloadHandler.text);
                isSubmitting = false;
                UpdateButtonsOnly();
                yield break;
            }

            WriteDebug("Submit OK: " + req.downloadHandler.text);

            hasSubmitted = true;
            isSubmitting = false;

            if (File.Exists(localSavePath))
                File.Delete(localSavePath);

            UpdateButtonsOnly();
            UpdateAnswerInteractivity();
        }
    }

    private void WriteDebug(string msg)
    {
        Debug.Log(msg);

        if (msg == lastDebugMsg) return;
        lastDebugMsg = msg;

        if (debugText != null)
            debugText.text = msg;
    }

    private AnswerWrapperDto FindAnswer(string questionId)
    {
        if (responses == null) return null;
        int idx = responses.FindIndex(r => r != null && r.question_id == questionId && r.answer != null);
        if (idx < 0) return null;
        return responses[idx].answer;
    }

    private AnswerWrapperDto GetOrCreateAnswer(string questionId)
    {
        AnswerWrapperDto a = FindAnswer(questionId);
        if (a != null) return a;
        a = new AnswerWrapperDto();
        AddOrReplaceResponse(questionId, a);
        return a;
    }

    private void RestoreSelectionUIForCurrentQuestion()
    {
        string qid = GetCurrentQuestionId();
        if (string.IsNullOrEmpty(qid)) return;

        QuestionSchemaDto q = GetCurrentQuestion();
        if (q == null) return;

        AnswerWrapperDto ans = FindAnswer(qid);

        if (phase == 1 && currentQuestionIndex >= 0 && currentQuestionIndex <= 5)
        {
            RestoreGroupHighlight_ByText(groupButtons_Q1to6_Choice, ans != null ? ans.choice : null);
            RestoreGroupHighlight_ByIndex(groupButtons_Q1to6_Confidence, ans != null ? (ans.confidence - 1) : -1);

            if (ValidateLocationUI())
            {
                isApplyingLocationUI = true;
                RestoreLocationSelectionIfAny(qid);
                isApplyingLocationUI = false;
            }

            return;
        }

        if (phase == 1 && currentQuestionIndex == 7)
        {
            isApplyingQ8UI = true;
            RestoreQ8SelectionIfAny(qid);
            isApplyingQ8UI = false;
            return;
        }

        if (q.type == "rating_1_5")
        {
            int rating = (ans != null) ? ans.int_value : 0;
            RestoreLikertHighlightForCurrentQuestion(rating);
            return;
        }

        if (q.type == "single_choice")
        {
            RestoreGroupHighlight_ByText(GetGroupForCurrentQuestion(), ans != null ? ans.value : null);
            return;
        }

        if (q.type == "multi_choice")
        {
            isApplyingQ8UI = true;
            RestoreQ8SelectionIfAny(qid);
            isApplyingQ8UI = false;
            return;
        }
    }

    private Button[] GetGroupForCurrentQuestion()
    {
        if (phase == 2) return groupButtons_Phase2;

        if (currentQuestionIndex == 6) return groupButtons_Q7;
        if (currentQuestionIndex >= 10 && currentQuestionIndex <= 12) return groupButtons_Q11_Q13;
        if (currentQuestionIndex == 13) return groupButtons_Q14;

        return null;
    }

    private void HookSingleChoice_GroupButtons_Q1to6_Choice(Button[] group)
    {
        if (group == null) return;

        for (int i = 0; i < group.Length; i++)
        {
            int index = i;
            Button btn = group[i];
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (!isInitialized) return;
                if (lockAnswersAfterSubmit && hasSubmitted) return;

                string value = GetButtonLabel(btn);
                if (string.IsNullOrEmpty(value)) return;

                string qid = GetCurrentQuestionId();
                AnswerWrapperDto answer = GetOrCreateAnswer(qid);
                answer.choice = value;

                AddOrReplaceResponse(qid, answer);
                SelectOneInGroup(group, index);

                UpdateButtonsOnly();
            });
        }
    }

    private void HookLikert_GroupButtons_Q1to6_Confidence(Button[] group)
    {
        if (group == null) return;

        for (int i = 0; i < group.Length; i++)
        {
            int index = i;
            Button btn = group[i];
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (!isInitialized) return;
                if (lockAnswersAfterSubmit && hasSubmitted) return;

                string qid = GetCurrentQuestionId();
                AnswerWrapperDto answer = GetOrCreateAnswer(qid);

                int rating = index + 1;
                answer.confidence = rating;

                AddOrReplaceResponse(qid, answer);
                SelectOneInGroup(group, index);

                UpdateButtonsOnly();
            });
        }
    }

    private void HookValueSingleChoice(Button[] group)
    {
        if (group == null) return;

        for (int i = 0; i < group.Length; i++)
        {
            int index = i;
            Button btn = group[i];
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (!isInitialized) return;
                if (lockAnswersAfterSubmit && hasSubmitted) return;

                string value = GetButtonLabel(btn);
                if (string.IsNullOrEmpty(value)) return;

                string qid = GetCurrentQuestionId();
                AnswerWrapperDto a = GetOrCreateAnswer(qid);
                a.value = value;

                AddOrReplaceResponse(qid, a);
                SelectOneInGroup(group, index);

                UpdateButtonsOnly();
            });
        }
    }

    // Updated: This supports 5 buttons OR 10 buttons (two sets).
    private void HookValueLikert_Mapped(Button[] group)
    {
        if (group == null) return;

        for (int i = 0; i < group.Length; i++)
        {
            int rawIndex = i;
            Button btn = group[i];
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (!isInitialized) return;
                if (lockAnswersAfterSubmit && hasSubmitted) return;

                QuestionSchemaDto q = GetCurrentQuestion();
                if (q == null || q.type != "rating_1_5") return;

                int subsetStart = GetLikertSubsetStartForCurrentQuestion();
                if (subsetStart < 0) return;

                if (rawIndex < subsetStart || rawIndex >= subsetStart + LikertCount)
                {
                    // Click from the other Likert set, ignore.
                    return;
                }

                int rating = (rawIndex - subsetStart) + 1;
                rating = Mathf.Clamp(rating, 1, 5);

                string qid = GetCurrentQuestionId();
                AnswerWrapperDto a = GetOrCreateAnswer(qid);
                a.int_value = rating;

                AddOrReplaceResponse(qid, a);

                SelectOneInLikertSubset(group, subsetStart, LikertCount, rawIndex);

                UpdateButtonsOnly();
            });
        }
    }

    // Q9/Q10 (index 8/9) uses first 5 buttons; Q15 (index 14) uses last 5 buttons.
    private int GetLikertSubsetStartForCurrentQuestion()
    {
        if (groupButtons_Likert5 == null || groupButtons_Likert5.Length == 0) return -1;

        // If you only assign 5 buttons, always use them.
        if (groupButtons_Likert5.Length == LikertCount) return 0;

        // If you assign 10 buttons, map by question index.
        if (groupButtons_Likert5.Length >= LikertCount * 2)
        {
            // Q9/Q10 are currentQuestionIndex 8 and 9
            if (phase == 1 && (currentQuestionIndex == 8 || currentQuestionIndex == 9))
                return 0;

            // Q15 is currentQuestionIndex 14
            if (phase == 1 && currentQuestionIndex == 14)
                return LikertCount;

            // If schema uses rating_1_5 elsewhere, default to first set
            return 0;
        }

        // Unexpected size fallback
        return 0;
    }

    private void RestoreLikertHighlightForCurrentQuestion(int rating1to5)
    {
        if (groupButtons_Likert5 == null || groupButtons_Likert5.Length == 0) return;

        int subsetStart = GetLikertSubsetStartForCurrentQuestion();
        if (subsetStart < 0) return;

        int selectedRawIndex = -1;
        if (rating1to5 >= 1 && rating1to5 <= 5)
            selectedRawIndex = subsetStart + (rating1to5 - 1);

        SelectOneInLikertSubset(groupButtons_Likert5, subsetStart, LikertCount, selectedRawIndex);
    }

    private void SelectOneInLikertSubset(Button[] group, int subsetStart, int subsetCount, int selectedRawIndex)
    {
        if (group == null) return;

        // Reset only this subset
        for (int i = subsetStart; i < subsetStart + subsetCount; i++)
        {
            if (i < 0 || i >= group.Length) continue;
            if (group[i] == null) continue;

            ApplyButtonColor(group[i], (i == selectedRawIndex) ? selectedColor : normalColor);
        }
    }

    private string GetButtonLabel(Button btn)
    {
        if (btn == null) return "";

        TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null) return tmp.text.Trim();

        Text t = btn.GetComponentInChildren<Text>();
        if (t != null) return t.text.Trim();

        return "";
    }

    private void ForceGroupNormal(Button[] group)
    {
        if (group == null) return;
        for (int i = 0; i < group.Length; i++)
        {
            if (group[i] == null) continue;
            ApplyButtonColor(group[i], normalColor);
        }
    }

    private void SelectOneInGroup(Button[] group, int selectedIndex)
    {
        if (group == null) return;

        for (int i = 0; i < group.Length; i++)
        {
            Button b = group[i];
            if (b == null) continue;
            ApplyButtonColor(b, (i == selectedIndex) ? selectedColor : normalColor);
        }
    }

    private void RestoreGroupHighlight_ByText(Button[] group, string textValue)
    {
        if (group == null) return;

        int idx = -1;
        if (!string.IsNullOrEmpty(textValue))
        {
            for (int i = 0; i < group.Length; i++)
            {
                if (group[i] == null) continue;
                string label = GetButtonLabel(group[i]);
                if (string.Equals(label, textValue, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
        }

        RestoreGroupHighlight_ByIndex(group, idx);
    }

    private void RestoreGroupHighlight_ByIndex(Button[] group, int index)
    {
        if (group == null) return;

        for (int i = 0; i < group.Length; i++)
        {
            Button b = group[i];
            if (b == null) continue;
            ApplyButtonColor(b, (i == index) ? selectedColor : normalColor);
        }
    }

    private void ApplyButtonColor(Button button, Color color)
    {
        if (button == null) return;

        Image img = button.image;
        if (img != null)
            img.color = color;

        ColorBlock cb = button.colors;
        cb.normalColor = color;
        cb.selectedColor = color;
        cb.highlightedColor = color;
        cb.pressedColor = color;
        button.colors = cb;
    }
}
