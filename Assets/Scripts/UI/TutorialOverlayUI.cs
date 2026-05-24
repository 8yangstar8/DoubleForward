using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 教程覆盖UI - 聚光灯式引导，高亮目标区域，其余变暗
/// 支持步骤队列、自动跳过已完成教程、手势动画提示
/// </summary>
public class TutorialOverlayUI : MonoBehaviour
{
    public static TutorialOverlayUI Instance { get; private set; }

    [Header("UI元素")]
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private Image darkMask;
    [SerializeField] private Image spotlightHole;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI stepCounter;
    [SerializeField] private Button skipButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI skipButtonText;

    [Header("手势动画")]
    [SerializeField] private RectTransform fingerIcon;
    [SerializeField] private Sprite tapFingerSprite;
    [SerializeField] private Sprite swipeFingerSprite;
    [SerializeField] private Sprite holdFingerSprite;

    [Header("设置")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float spotlightSize = 200f;
    [SerializeField] private Color maskColor = new Color(0, 0, 0, 0.7f);
    [SerializeField] private bool pauseOnShow = true;

    // 教程步骤
    [System.Serializable]
    public class TutorialStep
    {
        public string id;
        [TextArea(2, 4)]
        public string instruction;
        public RectTransform highlightTarget;
        public GestureType gesture = GestureType.Tap;
        public float customSpotlightSize = 0;
        public bool waitForInput = true;
        public float autoAdvanceDelay = 3f;
    }

    public enum GestureType
    {
        None,
        Tap,
        Swipe,
        Hold,
        DragLeft,
        DragRight,
        DragUp
    }

    private Queue<TutorialStep> stepQueue = new Queue<TutorialStep>();
    private TutorialStep currentStep;
    private int totalSteps;
    private int currentStepIndex;
    private bool isShowing;
    private Coroutine gestureAnimCoroutine;
    private Coroutine autoAdvanceCoroutine;

    private const string TUTORIAL_PREFIX = "TutorialDone_";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (overlayRoot != null)
            overlayRoot.SetActive(false);
    }

    void Start()
    {
        if (skipButton != null)
            skipButton.onClick.AddListener(SkipAll);

        if (nextButton != null)
            nextButton.onClick.AddListener(NextStep);
    }

    // ============ 公共API ============

    /// <summary>
    /// 显示教程序列
    /// </summary>
    public void ShowTutorial(TutorialStep[] steps, bool forceShow = false)
    {
        // 过滤已完成的步骤
        stepQueue.Clear();
        foreach (var step in steps)
        {
            if (forceShow || !IsTutorialCompleted(step.id))
                stepQueue.Enqueue(step);
        }

        if (stepQueue.Count == 0) return;

        totalSteps = stepQueue.Count;
        currentStepIndex = 0;
        isShowing = true;

        if (overlayRoot != null)
            overlayRoot.SetActive(true);

        if (pauseOnShow)
            Time.timeScale = 0f;

        StartCoroutine(FadeIn());
        ShowCurrentStep();
    }

    /// <summary>
    /// 显示单步教程
    /// </summary>
    public void ShowSingleTip(string id, string instruction, RectTransform target = null, GestureType gesture = GestureType.None)
    {
        if (IsTutorialCompleted(id)) return;

        var step = new TutorialStep
        {
            id = id,
            instruction = instruction,
            highlightTarget = target,
            gesture = gesture,
            waitForInput = true
        };

        ShowTutorial(new[] { step }, false);
    }

    /// <summary>
    /// 跳到下一步
    /// </summary>
    public void NextStep()
    {
        if (currentStep != null)
            MarkTutorialCompleted(currentStep.id);

        if (autoAdvanceCoroutine != null)
        {
            StopCoroutine(autoAdvanceCoroutine);
            autoAdvanceCoroutine = null;
        }

        if (stepQueue.Count > 0)
        {
            currentStepIndex++;
            ShowCurrentStep();
        }
        else
        {
            HideTutorial();
        }
    }

    /// <summary>
    /// 跳过所有教程
    /// </summary>
    public void SkipAll()
    {
        // 标记所有队列中的步骤为已完成
        while (stepQueue.Count > 0)
        {
            var step = stepQueue.Dequeue();
            MarkTutorialCompleted(step.id);
        }
        if (currentStep != null)
            MarkTutorialCompleted(currentStep.id);

        HideTutorial();
    }

    /// <summary>
    /// 重置某教程（用于调试）
    /// </summary>
    public void ResetTutorial(string id)
    {
        PlayerPrefs.DeleteKey(TUTORIAL_PREFIX + id);
    }

    /// <summary>
    /// 重置所有教程
    /// </summary>
    public void ResetAllTutorials()
    {
        // 清除所有以TUTORIAL_PREFIX开头的key
        // PlayerPrefs没有遍历API，所以需要手动管理
        var knownIds = new string[]
        {
            "move", "jump", "double_jump", "dash",
            "skill1", "skill2", "coop_switch",
            "water", "combat", "puzzle_basic"
        };

        foreach (var id in knownIds)
            PlayerPrefs.DeleteKey(TUTORIAL_PREFIX + id);

        PlayerPrefs.Save();
    }

    // ============ 内部方法 ============

    private void ShowCurrentStep()
    {
        if (stepQueue.Count == 0) return;

        currentStep = stepQueue.Dequeue();

        // 更新文本
        if (instructionText != null)
            instructionText.text = currentStep.instruction;

        if (stepCounter != null)
            stepCounter.text = $"{currentStepIndex + 1}/{totalSteps}";

        // 高亮目标
        if (currentStep.highlightTarget != null)
        {
            PositionSpotlight(currentStep.highlightTarget);
        }
        else
        {
            // 无高亮目标，隐藏聚光灯
            if (spotlightHole != null)
                spotlightHole.gameObject.SetActive(false);
        }

        // 手势动画
        ShowGesture(currentStep.gesture);

        // 自动推进
        if (!currentStep.waitForInput)
        {
            autoAdvanceCoroutine = StartCoroutine(AutoAdvance(currentStep.autoAdvanceDelay));
        }

        // 显示/隐藏下一步按钮
        if (nextButton != null)
            nextButton.gameObject.SetActive(currentStep.waitForInput);
    }

    private void PositionSpotlight(RectTransform target)
    {
        if (spotlightHole == null) return;

        spotlightHole.gameObject.SetActive(true);

        // 将目标位置转换到覆盖层空间
        Vector3 worldPos = target.position;
        spotlightHole.rectTransform.position = worldPos;

        float size = currentStep.customSpotlightSize > 0 ? currentStep.customSpotlightSize : spotlightSize;
        spotlightHole.rectTransform.sizeDelta = new Vector2(size, size);
    }

    private void ShowGesture(GestureType gesture)
    {
        if (gestureAnimCoroutine != null)
            StopCoroutine(gestureAnimCoroutine);

        if (fingerIcon == null || gesture == GestureType.None)
        {
            if (fingerIcon != null)
                fingerIcon.gameObject.SetActive(false);
            return;
        }

        fingerIcon.gameObject.SetActive(true);

        // 设置手指图标
        var image = fingerIcon.GetComponent<Image>();
        if (image != null)
        {
            switch (gesture)
            {
                case GestureType.Tap:
                    if (tapFingerSprite != null) image.sprite = tapFingerSprite;
                    break;
                case GestureType.Swipe:
                case GestureType.DragLeft:
                case GestureType.DragRight:
                case GestureType.DragUp:
                    if (swipeFingerSprite != null) image.sprite = swipeFingerSprite;
                    break;
                case GestureType.Hold:
                    if (holdFingerSprite != null) image.sprite = holdFingerSprite;
                    break;
            }
        }

        gestureAnimCoroutine = StartCoroutine(AnimateGesture(gesture));
    }

    private IEnumerator AnimateGesture(GestureType gesture)
    {
        if (fingerIcon == null) yield break;

        Vector2 startPos = fingerIcon.anchoredPosition;

        while (true)
        {
            switch (gesture)
            {
                case GestureType.Tap:
                    // 按下弹起动画
                    yield return AnimateScale(fingerIcon, 1f, 0.85f, 0.15f);
                    yield return AnimateScale(fingerIcon, 0.85f, 1f, 0.15f);
                    yield return new WaitForSecondsRealtime(0.5f);
                    break;

                case GestureType.Hold:
                    // 按住不放
                    yield return AnimateScale(fingerIcon, 1f, 0.85f, 0.2f);
                    yield return new WaitForSecondsRealtime(1f);
                    yield return AnimateScale(fingerIcon, 0.85f, 1f, 0.2f);
                    yield return new WaitForSecondsRealtime(0.3f);
                    break;

                case GestureType.Swipe:
                case GestureType.DragRight:
                    // 向右滑动
                    yield return AnimatePosition(fingerIcon, startPos, startPos + Vector2.right * 100f, 0.5f);
                    fingerIcon.anchoredPosition = startPos;
                    yield return new WaitForSecondsRealtime(0.3f);
                    break;

                case GestureType.DragLeft:
                    yield return AnimatePosition(fingerIcon, startPos, startPos + Vector2.left * 100f, 0.5f);
                    fingerIcon.anchoredPosition = startPos;
                    yield return new WaitForSecondsRealtime(0.3f);
                    break;

                case GestureType.DragUp:
                    yield return AnimatePosition(fingerIcon, startPos, startPos + Vector2.up * 100f, 0.5f);
                    fingerIcon.anchoredPosition = startPos;
                    yield return new WaitForSecondsRealtime(0.3f);
                    break;

                default:
                    yield return new WaitForSecondsRealtime(1f);
                    break;
            }
        }
    }

    private IEnumerator AnimateScale(RectTransform target, float from, float to, float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float scale = Mathf.Lerp(from, to, t);
            target.localScale = Vector3.one * scale;
            yield return null;
        }
        target.localScale = Vector3.one * to;
    }

    private IEnumerator AnimatePosition(RectTransform target, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // smoothstep
            target.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }
        target.anchoredPosition = to;
    }

    private IEnumerator AutoAdvance(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        NextStep();
    }

    private IEnumerator FadeIn()
    {
        if (darkMask == null) yield break;

        Color start = new Color(maskColor.r, maskColor.g, maskColor.b, 0);
        float elapsed = 0;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / fadeInDuration;
            darkMask.color = Color.Lerp(start, maskColor, t);
            yield return null;
        }
        darkMask.color = maskColor;
    }

    private void HideTutorial()
    {
        isShowing = false;

        if (gestureAnimCoroutine != null)
            StopCoroutine(gestureAnimCoroutine);

        if (autoAdvanceCoroutine != null)
            StopCoroutine(autoAdvanceCoroutine);

        if (overlayRoot != null)
            overlayRoot.SetActive(false);

        if (pauseOnShow)
            Time.timeScale = 1f;

        currentStep = null;
    }

    private bool IsTutorialCompleted(string id)
    {
        return PlayerPrefs.GetInt(TUTORIAL_PREFIX + id, 0) == 1;
    }

    private void MarkTutorialCompleted(string id)
    {
        PlayerPrefs.SetInt(TUTORIAL_PREFIX + id, 1);
        PlayerPrefs.Save();
    }
}
