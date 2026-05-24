using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 关卡计时器 - 速通模式、最佳时间追踪、时间评分
/// 支持暂停时自动停止计时，显示分段时间
/// </summary>
public class LevelTimer : MonoBehaviour
{
    public static LevelTimer Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI bestTimeText;
    [SerializeField] private TextMeshProUGUI splitTimeText;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color goodPaceColor = new Color(0.3f, 1f, 0.3f);
    [SerializeField] private Color badPaceColor = new Color(1f, 0.5f, 0.3f);

    [Header("评分时间阈值（秒）")]
    [SerializeField] private float threeStarTime = 60f;
    [SerializeField] private float twoStarTime = 120f;
    [SerializeField] private float oneStarTime = 300f;

    [Header("设置")]
    [SerializeField] private bool showMilliseconds = true;
    [SerializeField] private bool showSplitTimes = true;

    // 状态
    private float currentTime;
    private float bestTime;
    private bool isRunning;
    private int currentChapter;
    private int currentLevel;

    // 分段计时
    private float lastSplitTime;
    private int splitCount;
    private float splitDisplayTimer;
    private const float SPLIT_DISPLAY_DURATION = 3f;

    // 公共属性
    public float CurrentTime => currentTime;
    public float BestTime => bestTime;
    public bool IsRunning => isRunning;

    public event System.Action<float> OnTimerStopped;     // finalTime
    public event System.Action<float, float> OnSplitTime; // splitTime, totalTime

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (!isRunning) return;

        // 暂停时不计时
        if (Time.timeScale <= 0.01f) return;

        currentTime += Time.deltaTime;
        UpdateTimerDisplay();
        UpdateSplitDisplay();
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 开始计时（关卡开始时调用）
    /// </summary>
    public void StartTimer(int chapter, int level)
    {
        currentChapter = chapter;
        currentLevel = level;
        currentTime = 0f;
        lastSplitTime = 0f;
        splitCount = 0;
        isRunning = true;

        // 加载最佳时间
        bestTime = 0f;
        if (SaveSystem.Instance != null)
            bestTime = SaveSystem.Instance.GetLevelBestTime(chapter, level);

        UpdateBestTimeDisplay();
        UpdateTimerDisplay();
    }

    /// <summary>
    /// 停止计时（关卡完成或失败时调用）
    /// </summary>
    public float StopTimer()
    {
        if (!isRunning) return currentTime;

        isRunning = false;
        OnTimerStopped?.Invoke(currentTime);

        // 检查是否刷新最佳时间
        if (bestTime <= 0 || currentTime < bestTime)
        {
            bestTime = currentTime;
            UpdateBestTimeDisplay();
        }

        return currentTime;
    }

    /// <summary>
    /// 暂停计时
    /// </summary>
    public void PauseTimer()
    {
        isRunning = false;
    }

    /// <summary>
    /// 恢复计时
    /// </summary>
    public void ResumeTimer()
    {
        isRunning = true;
    }

    /// <summary>
    /// 记录分段时间（经过检查点时调用）
    /// </summary>
    public void RecordSplit(string splitName = "")
    {
        if (!isRunning) return;

        splitCount++;
        float splitTime = currentTime - lastSplitTime;
        lastSplitTime = currentTime;
        splitDisplayTimer = SPLIT_DISPLAY_DURATION;

        OnSplitTime?.Invoke(splitTime, currentTime);

        // 显示分段时间
        if (showSplitTimes && splitTimeText != null)
        {
            string label = string.IsNullOrEmpty(splitName) ? $"Split {splitCount}" : splitName;
            splitTimeText.text = $"{label}: {FormatTime(splitTime)}";
            splitTimeText.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 根据完成时间计算星级
    /// </summary>
    public int CalculateTimeStars()
    {
        return CalculateTimeStars(currentTime);
    }

    public int CalculateTimeStars(float time)
    {
        if (time <= threeStarTime) return 3;
        if (time <= twoStarTime) return 2;
        if (time <= oneStarTime) return 1;
        return 1; // 至少1星（完成了就有）
    }

    /// <summary>
    /// 设置星级时间阈值（从LevelConfig读取）
    /// </summary>
    public void SetTimeThresholds(float star3, float star2, float star1)
    {
        threeStarTime = star3;
        twoStarTime = star2;
        oneStarTime = star1;
    }

    // ==================== 显示更新 ====================

    private void UpdateTimerDisplay()
    {
        if (timerText == null) return;

        timerText.text = FormatTime(currentTime);

        // 颜色提示
        if (bestTime > 0)
        {
            if (currentTime < bestTime * 0.9f)
                timerText.color = goodPaceColor;
            else if (currentTime > bestTime)
                timerText.color = badPaceColor;
            else
                timerText.color = normalColor;
        }
    }

    private void UpdateBestTimeDisplay()
    {
        if (bestTimeText == null) return;

        if (bestTime > 0)
        {
            string label = "BEST";
            if (LocalizationSystem.Instance != null)
                label = LocalizationSystem.Instance.Get("timer_best", "BEST");
            bestTimeText.text = $"{label}: {FormatTime(bestTime)}";
            bestTimeText.gameObject.SetActive(true);
        }
        else
        {
            bestTimeText.gameObject.SetActive(false);
        }
    }

    private void UpdateSplitDisplay()
    {
        if (splitTimeText == null) return;

        if (splitDisplayTimer > 0)
        {
            splitDisplayTimer -= Time.deltaTime;
            if (splitDisplayTimer <= 0)
                splitTimeText.gameObject.SetActive(false);
        }
    }

    // ==================== 工具方法 ====================

    /// <summary>
    /// 格式化时间为 MM:SS.ms
    /// </summary>
    public static string FormatTime(float seconds)
    {
        if (seconds < 0) return "00:00";

        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        int ms = Mathf.FloorToInt((seconds * 100f) % 100f);

        return $"{minutes:D2}:{secs:D2}.{ms:D2}";
    }

    /// <summary>
    /// 格式化时间为简短文本（用于UI）
    /// </summary>
    public static string FormatTimeShort(float seconds)
    {
        if (seconds < 60)
            return $"{seconds:F1}s";

        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{minutes}:{secs:D2}";
    }

    /// <summary>
    /// 获取时间与最佳时间的差距文本
    /// </summary>
    public string GetDeltaText()
    {
        if (bestTime <= 0) return "";

        float delta = currentTime - bestTime;
        string sign = delta >= 0 ? "+" : "-";
        return $"{sign}{FormatTime(Mathf.Abs(delta))}";
    }
}
