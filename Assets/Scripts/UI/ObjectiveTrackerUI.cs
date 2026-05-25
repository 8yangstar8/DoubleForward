using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 关卡目标追踪UI - 显示当前关卡的活跃目标和进度
/// 动态更新目标状态、支持淡入淡出动画
/// 与LevelCompletionChecker配合，读取条件状态
/// </summary>
public class ObjectiveTrackerUI : MonoBehaviour
{
    [Header("容器")]
    [SerializeField] private RectTransform objectiveContainer;
    [SerializeField] private GameObject objectiveItemPrefab;

    [Header("样式")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color completedColor = new Color(0.3f, 1f, 0.5f);
    [SerializeField] private Color failedColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private Sprite checkmarkSprite;
    [SerializeField] private Sprite circleSprite;

    [Header("动画")]
    [SerializeField] private float showDuration = 0.3f;
    [SerializeField] private float completionPulseScale = 1.3f;
    [SerializeField] private float autoHideDelay = 5f;
    [SerializeField] private bool autoHideWhenInactive = true;

    [Header("位置")]
    [SerializeField] private Vector2 showPosition = new Vector2(-20f, -80f);
    [SerializeField] private Vector2 hidePosition = new Vector2(300f, -80f);

    // 运行时
    private Dictionary<string, ObjectiveEntry> entries = new Dictionary<string, ObjectiveEntry>();
    private CanvasGroup canvasGroup;
    private float hideTimer;
    private bool isShowing;

    private class ObjectiveEntry
    {
        public string id;
        public string text;
        public bool completed;
        public GameObject uiObject;
        public TextMeshProUGUI textComponent;
        public Image statusIcon;
        public Image progressBar;
    }

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void Start()
    {
        // 订阅事件
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Subscribe<CollectiblePickedEvent>(OnCollectiblePicked);
        EventBus.Subscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);

        // 初始隐藏
        if (objectiveContainer != null)
            objectiveContainer.anchoredPosition = hidePosition;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Unsubscribe<CollectiblePickedEvent>(OnCollectiblePicked);
        EventBus.Unsubscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
    }

    void Update()
    {
        // 自动隐藏
        if (autoHideWhenInactive && isShowing)
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0)
                Hide();
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 添加目标条目
    /// </summary>
    public void AddObjective(string id, string text, bool showImmediately = true)
    {
        if (entries.ContainsKey(id)) return;
        if (objectiveItemPrefab == null || objectiveContainer == null) return;

        var go = Instantiate(objectiveItemPrefab, objectiveContainer);
        var textComp = go.GetComponentInChildren<TextMeshProUGUI>();
        var icons = go.GetComponentsInChildren<Image>();

        Image statusIcon = null;
        Image progressBar = null;

        if (icons.Length > 1) statusIcon = icons[1]; // 第二个Image作为状态图标
        if (icons.Length > 2) progressBar = icons[2]; // 第三个Image作为进度条

        if (textComp != null)
        {
            textComp.text = text;
            textComp.color = activeColor;
        }

        if (statusIcon != null && circleSprite != null)
            statusIcon.sprite = circleSprite;

        if (progressBar != null)
            progressBar.fillAmount = 0f;

        var entry = new ObjectiveEntry
        {
            id = id,
            text = text,
            completed = false,
            uiObject = go,
            textComponent = textComp,
            statusIcon = statusIcon,
            progressBar = progressBar
        };

        entries[id] = entry;

        if (showImmediately) Show();
    }

    /// <summary>
    /// 更新目标进度
    /// </summary>
    public void UpdateProgress(string id, float progress, string updatedText = null)
    {
        if (!entries.ContainsKey(id)) return;

        var entry = entries[id];

        if (entry.progressBar != null)
            entry.progressBar.fillAmount = Mathf.Clamp01(progress);

        if (updatedText != null && entry.textComponent != null)
            entry.textComponent.text = updatedText;

        if (progress >= 1f)
            CompleteObjective(id);

        Show();
    }

    /// <summary>
    /// 标记目标完成
    /// </summary>
    public void CompleteObjective(string id)
    {
        if (!entries.ContainsKey(id)) return;

        var entry = entries[id];
        if (entry.completed) return;

        entry.completed = true;

        if (entry.textComponent != null)
        {
            entry.textComponent.color = completedColor;
            entry.textComponent.fontStyle = FontStyles.Strikethrough;
        }

        if (entry.statusIcon != null && checkmarkSprite != null)
        {
            entry.statusIcon.sprite = checkmarkSprite;
            entry.statusIcon.color = completedColor;
        }

        if (entry.progressBar != null)
            entry.progressBar.fillAmount = 1f;

        // 完成脉冲动画
        StartCoroutine(CompletionPulse(entry.uiObject.transform));

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();

        Show();
    }

    /// <summary>
    /// 显示目标面板
    /// </summary>
    public void Show()
    {
        isShowing = true;
        hideTimer = autoHideDelay;
        StopCoroutine(nameof(AnimateShow));
        StartCoroutine(AnimateShow(true));
    }

    /// <summary>
    /// 隐藏目标面板
    /// </summary>
    public void Hide()
    {
        isShowing = false;
        StopCoroutine(nameof(AnimateShow));
        StartCoroutine(AnimateShow(false));
    }

    /// <summary>
    /// 清除所有目标
    /// </summary>
    public void ClearAll()
    {
        foreach (var entry in entries.Values)
        {
            if (entry.uiObject != null)
                Destroy(entry.uiObject);
        }
        entries.Clear();
    }

    // ==================== 事件响应 ====================

    private void OnLevelStart(LevelStartEvent e)
    {
        ClearAll();
        SetupObjectivesFromChecker();
    }

    private void OnEnemyDefeated(EnemyDefeatedEvent e)
    {
        if (!entries.ContainsKey("defeat_enemies")) return;

        int total = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None).Length + 1;
        int current = total; // 已击败的由当前剩余反推
        var remaining = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None).Length;
        int defeated = total - remaining;

        UpdateProgress("defeat_enemies", (float)defeated / total,
            $"消灭敌人 {defeated}/{total}");
    }

    private void OnCollectiblePicked(CollectiblePickedEvent e)
    {
        if (!entries.ContainsKey("collect_items")) return;

        int total = e.total > 0 ? e.total : 1;
        UpdateProgress("collect_items", (float)e.collected / total,
            $"收集品 {e.collected}/{total}");
    }

    private void OnPuzzleSolved(PuzzleSolvedEvent e)
    {
        if (entries.ContainsKey("solve_puzzles"))
        {
            Show(); // 刷新显示
        }
    }

    private void OnBossDefeated(BossDefeatedEvent e)
    {
        if (entries.ContainsKey("defeat_boss"))
            CompleteObjective("defeat_boss");
    }

    private void OnLevelComplete(LevelCompleteEvent e)
    {
        // 所有目标标记完成
        foreach (var entry in entries.Values)
        {
            if (!entry.completed)
                CompleteObjective(entry.id);
        }
    }

    // ==================== 内部 ====================

    private void SetupObjectivesFromChecker()
    {
        var checker = LevelCompletionChecker.Instance;
        if (checker == null) return;

        // 根据LevelCompletionChecker的条件创建UI条目
        // 默认条件
        AddObjective("reach_goal", "到达终点");

        int totalCollectibles = LevelManager.Instance != null
            ? LevelManager.Instance.GetTotalCollectibles() : 0;
        if (totalCollectibles > 0)
            AddObjective("collect_items", $"收集品 0/{totalCollectibles}");

        int totalEnemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None).Length;
        if (totalEnemies > 0)
            AddObjective("defeat_enemies", $"消灭敌人 0/{totalEnemies}");
    }

    private IEnumerator AnimateShow(bool show)
    {
        float elapsed = 0;
        Vector2 startPos = objectiveContainer != null
            ? objectiveContainer.anchoredPosition : Vector2.zero;
        Vector2 targetPos = show ? showPosition : hidePosition;
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 0f;
        float targetAlpha = show ? 1f : 0f;

        while (elapsed < showDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / showDuration;
            t = t * t * (3f - 2f * t); // Smoothstep

            if (objectiveContainer != null)
                objectiveContainer.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            yield return null;
        }

        if (objectiveContainer != null)
            objectiveContainer.anchoredPosition = targetPos;
        if (canvasGroup != null)
            canvasGroup.alpha = targetAlpha;
    }

    private IEnumerator CompletionPulse(Transform target)
    {
        float elapsed = 0;
        float duration = 0.3f;
        Vector3 original = target.localScale;
        Vector3 big = original * completionPulseScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float scale = t < 0.5f
                ? Mathf.Lerp(1f, completionPulseScale, t * 2f)
                : Mathf.Lerp(completionPulseScale, 1f, (t - 0.5f) * 2f);

            target.localScale = original * scale;
            yield return null;
        }

        target.localScale = original;
    }
}
