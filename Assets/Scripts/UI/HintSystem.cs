using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 游戏内提示与引导系统 - 当玩家卡关时提供渐进式提示
/// 支持自动检测卡关、手动请求提示、视觉引导箭头
/// </summary>
public class HintSystem : MonoBehaviour
{
    public static HintSystem Instance { get; private set; }

    [System.Serializable]
    public class HintData
    {
        public string hintId;
        [TextArea(2, 4)] public string[] progressiveHints; // 渐进式提示（越来越详细）
        public Transform targetPosition;                    // 目标位置（用于箭头指向）
        public float stuckTimeThreshold = 60f;             // 多少秒未进展算卡关
    }

    [Header("UI组件")]
    [SerializeField] private GameObject hintPanel;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private CanvasGroup hintCanvasGroup;
    [SerializeField] private Button hintButton;            // 主动请求提示按钮
    [SerializeField] private Button closeHintButton;
    [SerializeField] private Image hintButtonGlow;          // 提示可用时的发光效果

    [Header("箭头引导")]
    [SerializeField] private RectTransform guideArrow;
    [SerializeField] private float arrowBobSpeed = 2f;
    [SerializeField] private float arrowBobAmount = 10f;

    [Header("设置")]
    [SerializeField] private float autoHintDelay = 90f;      // 自动提示延迟（秒）
    [SerializeField] private float hintDisplayDuration = 8f;
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private bool autoHintEnabled = true;

    [Header("当前关卡提示")]
    [SerializeField] private List<HintData> currentLevelHints = new List<HintData>();

    private float stuckTimer;
    private int currentHintIndex;           // 当前激活的提示组
    private int currentHintLevel;           // 渐进提示等级
    private bool isShowingHint;
    private bool hintAvailable;
    private Vector3 lastPlayerPosition;
    private float positionCheckTimer;
    private Coroutine hideCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (hintPanel != null) hintPanel.SetActive(false);
        if (guideArrow != null) guideArrow.gameObject.SetActive(false);
    }

    void Start()
    {
        hintButton?.onClick.AddListener(RequestHint);
        closeHintButton?.onClick.AddListener(HideHint);

        SetHintButtonGlow(false);
    }

    void Update()
    {
        if (!autoHintEnabled) return;

        UpdateStuckDetection();
        UpdateGuideArrow();
    }

    /// <summary>
    /// 设置当前关卡的提示数据
    /// </summary>
    public void SetLevelHints(List<HintData> hints)
    {
        currentLevelHints = hints;
        currentHintIndex = 0;
        currentHintLevel = 0;
        stuckTimer = 0;
    }

    /// <summary>
    /// 标记进度推进（玩家解决了一个谜题/到达新区域）
    /// </summary>
    public void MarkProgress(string hintId = null)
    {
        stuckTimer = 0;
        currentHintLevel = 0;
        SetHintButtonGlow(false);

        if (hintId != null)
        {
            // 跳到下一个提示组
            for (int i = 0; i < currentLevelHints.Count; i++)
            {
                if (currentLevelHints[i].hintId == hintId)
                {
                    currentHintIndex = i + 1;
                    break;
                }
            }
        }
        else
        {
            currentHintIndex++;
        }

        HideHint();
    }

    /// <summary>
    /// 玩家主动请求提示
    /// </summary>
    public void RequestHint()
    {
        if (currentHintIndex >= currentLevelHints.Count) return;

        var hintData = currentLevelHints[currentHintIndex];
        if (hintData.progressiveHints == null || hintData.progressiveHints.Length == 0) return;

        // 显示当前等级提示
        int level = Mathf.Min(currentHintLevel, hintData.progressiveHints.Length - 1);
        ShowHint(hintData.progressiveHints[level]);

        // 显示箭头指向目标
        if (hintData.targetPosition != null && guideArrow != null)
        {
            guideArrow.gameObject.SetActive(true);
        }

        // 下次请求显示更详细的提示
        if (currentHintLevel < hintData.progressiveHints.Length - 1)
            currentHintLevel++;

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayUIClick();
    }

    /// <summary>
    /// 显示提示文本
    /// </summary>
    public void ShowHint(string text, float duration)
    {
        ShowHint(text);
        // 覆盖自动隐藏延迟
        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideAfterDelay(duration));
    }

    private System.Collections.IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideHint();
    }

    public void ShowHint(string text)
    {
        if (hintPanel == null || hintText == null) return;

        // 本地化
        if (LocalizationSystem.Instance != null && LocalizationSystem.Instance.HasKey(text))
            text = LocalizationSystem.Instance.Get(text);

        hintText.text = text;
        hintPanel.SetActive(true);
        isShowingHint = true;

        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        StartCoroutine(FadeIn());
        hideCoroutine = StartCoroutine(AutoHideAfterDelay());
    }

    /// <summary>
    /// 隐藏提示
    /// </summary>
    public void HideHint()
    {
        if (!isShowingHint) return;

        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        StartCoroutine(FadeOutAndHide());

        if (guideArrow != null)
            guideArrow.gameObject.SetActive(false);
    }

    /// <summary>
    /// 直接显示自定义提示（不受渐进系统控制）
    /// </summary>
    public void ShowCustomHint(string text, float duration = -1f)
    {
        ShowHint(text);
        if (duration > 0)
        {
            if (hideCoroutine != null) StopCoroutine(hideCoroutine);
            hideCoroutine = StartCoroutine(AutoHideAfterDelay(duration));
        }
    }

    // ============ 卡关检测 ============

    private void UpdateStuckDetection()
    {
        if (currentHintIndex >= currentLevelHints.Count) return;
        if (isShowingHint) return;

        positionCheckTimer += Time.deltaTime;

        // 每2秒检查一次玩家位置
        if (positionCheckTimer >= 2f)
        {
            positionCheckTimer = 0;

            // 查找玩家
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                float moved = Vector3.Distance(player.transform.position, lastPlayerPosition);
                lastPlayerPosition = player.transform.position;

                // 如果玩家几乎没有移动，增加卡关计时
                if (moved < 1f)
                    stuckTimer += 2f;
                else
                    stuckTimer = Mathf.Max(0, stuckTimer - 1f);
            }
        }

        // 卡关时间达到阈值，显示提示按钮发光
        var hintData = currentLevelHints[currentHintIndex];
        if (stuckTimer >= hintData.stuckTimeThreshold)
        {
            if (!hintAvailable)
            {
                hintAvailable = true;
                SetHintButtonGlow(true);
            }

            // 超过自动提示延迟，主动弹出
            if (stuckTimer >= autoHintDelay)
            {
                stuckTimer = 0;
                RequestHint();
            }
        }
    }

    private void SetHintButtonGlow(bool active)
    {
        hintAvailable = active;
        if (hintButtonGlow != null)
            hintButtonGlow.gameObject.SetActive(active);
    }

    // ============ 箭头引导 ============

    private void UpdateGuideArrow()
    {
        if (guideArrow == null || !guideArrow.gameObject.activeSelf) return;
        if (currentHintIndex >= currentLevelHints.Count) return;

        var hintData = currentLevelHints[currentHintIndex];
        if (hintData.targetPosition == null) return;

        // 将世界坐标转为屏幕坐标
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(hintData.targetPosition.position);

        // 箭头上下浮动
        float bob = Mathf.Sin(Time.time * arrowBobSpeed) * arrowBobAmount;
        guideArrow.position = screenPos + Vector3.up * (30f + bob);

        // 如果目标在屏幕外，指向边缘
        bool onScreen = screenPos.x > 0 && screenPos.x < Screen.width &&
                        screenPos.y > 0 && screenPos.y < Screen.height;

        if (!onScreen)
        {
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = ((Vector2)screenPos - center).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            guideArrow.rotation = Quaternion.Euler(0, 0, angle - 90);

            // 钳制到屏幕边缘
            float margin = 50f;
            float x = Mathf.Clamp(screenPos.x, margin, Screen.width - margin);
            float y = Mathf.Clamp(screenPos.y, margin, Screen.height - margin);
            guideArrow.position = new Vector3(x, y + bob, 0);
        }
        else
        {
            guideArrow.rotation = Quaternion.identity;
        }
    }

    // ============ 动画 ============

    private IEnumerator FadeIn()
    {
        if (hintCanvasGroup == null) yield break;
        hintCanvasGroup.alpha = 0;
        float t = 0;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            hintCanvasGroup.alpha = t / fadeInDuration;
            yield return null;
        }
        hintCanvasGroup.alpha = 1;
    }

    private IEnumerator FadeOutAndHide()
    {
        if (hintCanvasGroup == null) yield break;
        float t = 0;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            hintCanvasGroup.alpha = 1f - (t / fadeOutDuration);
            yield return null;
        }
        hintCanvasGroup.alpha = 0;
        if (hintPanel != null) hintPanel.SetActive(false);
        isShowingHint = false;
    }

    private IEnumerator AutoHideAfterDelay(float delay = -1f)
    {
        yield return new WaitForSecondsRealtime(delay > 0 ? delay : hintDisplayDuration);
        if (isShowingHint)
        {
            StartCoroutine(FadeOutAndHide());
            if (guideArrow != null) guideArrow.gameObject.SetActive(false);
        }
    }
}
