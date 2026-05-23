using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class TutorialSystem : MonoBehaviour
{
    public static TutorialSystem Instance { get; private set; }

    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject arrowIndicator;
    [SerializeField] private Button skipButton;
    [SerializeField] private float autoHideDelay = 5f;
    [SerializeField] private CanvasGroup canvasGroup;

    [System.Serializable]
    public class TutorialStep
    {
        public string id;
        public string title;
        [TextArea] public string description;
        public Sprite icon;
        public Transform highlightTarget;
        public bool pauseGame;
        public bool waitForAction;
        public string requiredAction; // "move", "jump", "skill1", "skill2"
        public float displayTime = 4f;
    }

    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    private int currentStepIndex = -1;
    private bool isShowingTutorial;
    private HashSet<string> completedTutorials = new HashSet<string>();

    void Awake()
    {
        Instance = this;
        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);
        skipButton?.onClick.AddListener(SkipCurrentStep);
    }

    public void StartTutorialSequence()
    {
        currentStepIndex = -1;
        ShowNextStep();
    }

    public void ShowStep(string stepId)
    {
        if (completedTutorials.Contains(stepId)) return;
        var step = steps.Find(s => s.id == stepId);
        if (step != null) ShowTutorialStep(step);
    }

    private void ShowNextStep()
    {
        currentStepIndex++;
        if (currentStepIndex >= steps.Count)
        {
            EndTutorial();
            return;
        }

        var step = steps[currentStepIndex];
        if (completedTutorials.Contains(step.id))
        {
            ShowNextStep();
            return;
        }

        ShowTutorialStep(step);
    }

    private void ShowTutorialStep(TutorialStep step)
    {
        isShowingTutorial = true;

        if (tutorialPanel != null) tutorialPanel.SetActive(true);
        if (titleText != null) titleText.text = step.title;
        if (descriptionText != null) descriptionText.text = step.description;
        if (iconImage != null && step.icon != null)
        {
            iconImage.sprite = step.icon;
            iconImage.gameObject.SetActive(true);
        }

        if (arrowIndicator != null && step.highlightTarget != null)
        {
            arrowIndicator.SetActive(true);
            StartCoroutine(FollowTarget(step.highlightTarget));
        }

        if (step.pauseGame)
            Time.timeScale = 0f;

        if (step.waitForAction)
            StartCoroutine(WaitForPlayerAction(step));
        else
            StartCoroutine(AutoHideStep(step));

        StartCoroutine(FadeIn());
    }

    private IEnumerator AutoHideStep(TutorialStep step)
    {
        yield return new WaitForSecondsRealtime(step.displayTime);
        CompleteCurrentStep();
    }

    private IEnumerator WaitForPlayerAction(TutorialStep step)
    {
        bool actionCompleted = false;

        while (!actionCompleted)
        {
            switch (step.requiredAction)
            {
                case "move":
                    var move = InputManager.Instance?.GetMoveInput(0) ?? Vector2.zero;
                    actionCompleted = move.magnitude > 0.3f;
                    break;
                case "jump":
                    actionCompleted = InputManager.Instance?.GetJumpPressed(0) ?? false;
                    break;
                case "skill1":
                    actionCompleted = InputManager.Instance?.GetSkill1Pressed(0) ?? false;
                    break;
                case "skill2":
                    actionCompleted = InputManager.Instance?.GetSkill2Pressed(0) ?? false;
                    break;
            }
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.5f);
        CompleteCurrentStep();
    }

    private void CompleteCurrentStep()
    {
        if (currentStepIndex >= 0 && currentStepIndex < steps.Count)
            completedTutorials.Add(steps[currentStepIndex].id);

        StartCoroutine(FadeOutThenNext());
    }

    private IEnumerator FadeOutThenNext()
    {
        yield return StartCoroutine(FadeOut());

        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        if (arrowIndicator != null) arrowIndicator.SetActive(false);

        Time.timeScale = 1f;
        isShowingTutorial = false;

        yield return new WaitForSeconds(0.5f);
        ShowNextStep();
    }

    public void SkipCurrentStep()
    {
        StopAllCoroutines();
        CompleteCurrentStep();
    }

    private void EndTutorial()
    {
        isShowingTutorial = false;
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        if (arrowIndicator != null) arrowIndicator.SetActive(false);
        Time.timeScale = 1f;
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null) yield break;
        canvasGroup.alpha = 0;
        float t = 0;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = t / 0.3f;
            yield return null;
        }
        canvasGroup.alpha = 1;
    }

    private IEnumerator FadeOut()
    {
        if (canvasGroup == null) yield break;
        float t = 0;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - (t / 0.3f);
            yield return null;
        }
        canvasGroup.alpha = 0;
    }

    private IEnumerator FollowTarget(Transform target)
    {
        while (isShowingTutorial && target != null && arrowIndicator != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(target.position + Vector3.up * 1.5f);
            arrowIndicator.transform.position = screenPos;
            yield return null;
        }
    }

    public bool HasCompletedTutorial(string stepId)
    {
        return completedTutorials.Contains(stepId);
    }
}
