using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 输入指引UI - 在游戏内显示操作提示
/// 根据当前情景自动显示相关的按键/手势提示
/// 适配触屏和手柄两种输入模式
/// 首次出现带高亮引导动画
/// </summary>
public class InputGuideUI : MonoBehaviour
{
    public static InputGuideUI Instance { get; private set; }

    [Header("提示面板")]
    [SerializeField] private RectTransform guidePanel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI actionText;
    [SerializeField] private Image inputIcon;

    [Header("图标映射")]
    [SerializeField] private InputIconSet touchIcons;
    [SerializeField] private InputIconSet gamepadIcons;

    [Header("动画")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseScale = 1.1f;

    [Header("自动隐藏")]
    [SerializeField] private float defaultDisplayDuration = 4f;
    [SerializeField] private bool hideOnActionPerformed = true;

    [System.Serializable]
    public class InputIconSet
    {
        public Sprite moveIcon;
        public Sprite jumpIcon;
        public Sprite attackIcon;
        public Sprite skill1Icon;
        public Sprite skill2Icon;
        public Sprite interactIcon;
        public Sprite dashIcon;
    }

    public enum ActionType
    {
        Move,
        Jump,
        Attack,
        Skill1,
        Skill2,
        Interact,
        Dash,
        Custom
    }

    private Queue<GuideRequest> pendingGuides = new Queue<GuideRequest>();
    private bool isShowingGuide;
    private Coroutine currentCoroutine;
    private HashSet<string> shownGuides = new HashSet<string>();

    private struct GuideRequest
    {
        public string id;
        public ActionType action;
        public string text;
        public float duration;
        public bool showOnce;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (guidePanel != null)
            guidePanel.gameObject.SetActive(false);
    }

    void Start()
    {
        // 加载已显示过的指引
        LoadShownGuides();
    }

    void Update()
    {
        // 脉冲动画
        if (isShowingGuide && inputIcon != null)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * (pulseScale - 1f);
            inputIcon.transform.localScale = Vector3.one * pulse;
        }

        // 处理队列
        if (!isShowingGuide && pendingGuides.Count > 0)
        {
            var guide = pendingGuides.Dequeue();
            currentCoroutine = StartCoroutine(ShowGuideCoroutine(guide));
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 显示操作指引
    /// </summary>
    public void ShowGuide(string id, ActionType action, string textKey, float duration = 0f, bool showOnce = true)
    {
        if (showOnce && shownGuides.Contains(id)) return;

        string text = LocalizationSystem.Instance != null
            ? LocalizationSystem.Instance.GetText(textKey)
            : textKey;

        var request = new GuideRequest
        {
            id = id,
            action = action,
            text = text,
            duration = duration > 0 ? duration : defaultDisplayDuration,
            showOnce = showOnce
        };

        if (isShowingGuide)
            pendingGuides.Enqueue(request);
        else
            currentCoroutine = StartCoroutine(ShowGuideCoroutine(request));
    }

    /// <summary>
    /// 显示自定义指引
    /// </summary>
    public void ShowCustomGuide(string id, string text, Sprite icon, float duration = 0f)
    {
        if (shownGuides.Contains(id)) return;

        if (isShowingGuide)
        {
            // 排队
            return;
        }

        currentCoroutine = StartCoroutine(ShowCustomCoroutine(id, text, icon, duration > 0 ? duration : defaultDisplayDuration));
    }

    /// <summary>
    /// 隐藏当前指引
    /// </summary>
    public void HideGuide()
    {
        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);
        StartCoroutine(FadeOut());
    }

    /// <summary>
    /// 通知操作已执行（用于自动隐藏）
    /// </summary>
    public void NotifyAction(ActionType action)
    {
        if (hideOnActionPerformed && isShowingGuide)
            HideGuide();
    }

    // ==================== 内部实现 ====================

    private IEnumerator ShowGuideCoroutine(GuideRequest guide)
    {
        isShowingGuide = true;

        if (guide.showOnce)
        {
            shownGuides.Add(guide.id);
            SaveShownGuides();
        }

        // 设置内容
        if (actionText != null) actionText.text = guide.text;

        Sprite icon = GetIcon(guide.action);
        if (inputIcon != null && icon != null)
        {
            inputIcon.sprite = icon;
            inputIcon.gameObject.SetActive(true);
        }

        // 显示
        if (guidePanel != null) guidePanel.gameObject.SetActive(true);
        yield return FadeIn();

        // 持续显示
        yield return new WaitForSecondsRealtime(guide.duration);

        // 隐藏
        yield return FadeOut();
        isShowingGuide = false;
    }

    private IEnumerator ShowCustomCoroutine(string id, string text, Sprite icon, float duration)
    {
        isShowingGuide = true;
        shownGuides.Add(id);

        if (actionText != null) actionText.text = text;
        if (inputIcon != null && icon != null)
        {
            inputIcon.sprite = icon;
            inputIcon.gameObject.SetActive(true);
        }

        if (guidePanel != null) guidePanel.gameObject.SetActive(true);
        yield return FadeIn();

        yield return new WaitForSecondsRealtime(duration);

        yield return FadeOut();
        isShowingGuide = false;
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null) yield break;

        canvasGroup.alpha = 0;
        float elapsed = 0;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = elapsed / fadeInDuration;
            yield return null;
        }
        canvasGroup.alpha = 1;
    }

    private IEnumerator FadeOut()
    {
        if (canvasGroup == null)
        {
            if (guidePanel != null) guidePanel.gameObject.SetActive(false);
            isShowingGuide = false;
            yield break;
        }

        float elapsed = 0;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - (elapsed / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0;

        if (guidePanel != null) guidePanel.gameObject.SetActive(false);
        isShowingGuide = false;
    }

    // ==================== 图标选择 ====================

    private Sprite GetIcon(ActionType action)
    {
        bool isTouch = Application.isMobilePlatform;
        var icons = isTouch ? touchIcons : gamepadIcons;

        if (icons == null) return null;

        return action switch
        {
            ActionType.Move => icons.moveIcon,
            ActionType.Jump => icons.jumpIcon,
            ActionType.Attack => icons.attackIcon,
            ActionType.Skill1 => icons.skill1Icon,
            ActionType.Skill2 => icons.skill2Icon,
            ActionType.Interact => icons.interactIcon,
            ActionType.Dash => icons.dashIcon,
            _ => null
        };
    }

    // ==================== 持久化 ====================

    private void SaveShownGuides()
    {
        string data = string.Join(",", shownGuides);
        PlayerPrefs.SetString("shown_input_guides", data);
    }

    private void LoadShownGuides()
    {
        string data = PlayerPrefs.GetString("shown_input_guides", "");
        if (!string.IsNullOrEmpty(data))
        {
            foreach (string id in data.Split(','))
                shownGuides.Add(id);
        }
    }

    /// <summary>
    /// 重置所有已显示的指引（用于设置）
    /// </summary>
    public void ResetAllGuides()
    {
        shownGuides.Clear();
        PlayerPrefs.DeleteKey("shown_input_guides");
    }
}
