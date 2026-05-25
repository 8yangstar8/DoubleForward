using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 关卡计时器UI - 显示当前关卡用时
/// 支持速通计时、最佳记录对比、3星时间提示
/// 时间紧张时变色警告
/// </summary>
public class LevelTimerUI : MonoBehaviour
{
    [Header("显示")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI bestTimeText;
    [SerializeField] private TextMeshProUGUI starTimeHintText;

    [Header("图标")]
    [SerializeField] private Image timerIcon;
    [SerializeField] private Image[] starIcons;

    [Header("星级时间阈值")]
    [SerializeField] private float twoStarTime = 180f;
    [SerializeField] private float threeStarTime = 150f;

    [Header("颜色")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color goodColor = new Color(0.3f, 1f, 0.4f);
    [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color dangerColor = new Color(1f, 0.3f, 0.2f);

    [Header("动画")]
    [SerializeField] private float tickPulseScale = 1.05f;
    [SerializeField] private float dangerPulseSpeed = 3f;
    [SerializeField] private bool showMilliseconds = false;

    // 运行时
    private float previousSecond;
    private float bestTime = -1f;
    private bool isPaused;
    private RectTransform timerRect;
    private Vector3 originalScale;

    void Start()
    {
        if (timerText != null)
        {
            timerRect = timerText.GetComponent<RectTransform>();
            originalScale = timerRect != null ? timerRect.localScale : Vector3.one;
        }

        // 加载最佳记录
        LoadBestTime();

        // 显示星级时间提示
        UpdateStarHint();

        // 订阅暂停事件
        EventBus.Subscribe<GamePausedEvent>(OnGamePaused);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<GamePausedEvent>(OnGamePaused);
    }

    void Update()
    {
        if (isPaused) return;

        float currentTime = GetCurrentTime();
        UpdateTimerDisplay(currentTime);
        UpdateTimerColor(currentTime);
        UpdateStarIcons(currentTime);
        CheckSecondTick(currentTime);
    }

    // ==================== 显示更新 ====================

    private void UpdateTimerDisplay(float time)
    {
        if (timerText == null) return;

        int minutes = (int)(time / 60f);
        int seconds = (int)(time % 60f);

        if (showMilliseconds)
        {
            int ms = (int)((time * 100f) % 100f);
            timerText.text = $"{minutes:00}:{seconds:00}.{ms:00}";
        }
        else
        {
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void UpdateTimerColor(float time)
    {
        if (timerText == null) return;

        if (time < threeStarTime * 0.7f)
        {
            timerText.color = goodColor;
        }
        else if (time < threeStarTime)
        {
            // 接近3星线，渐变到警告色
            float t = (time - threeStarTime * 0.7f) / (threeStarTime * 0.3f);
            timerText.color = Color.Lerp(goodColor, warningColor, t);
        }
        else if (time < twoStarTime)
        {
            timerText.color = warningColor;
        }
        else
        {
            // 超过2星时间，危险色脉冲
            float pulse = (Mathf.Sin(Time.time * dangerPulseSpeed) + 1f) * 0.5f;
            timerText.color = Color.Lerp(dangerColor, warningColor, pulse);
        }
    }

    private void UpdateStarIcons(float time)
    {
        if (starIcons == null) return;

        // 3颗星图标：根据当前时间显示可获得的星数
        int possibleStars = 1;
        if (time < twoStarTime) possibleStars = 2;
        if (time < threeStarTime) possibleStars = 3;

        for (int i = 0; i < starIcons.Length && i < 3; i++)
        {
            if (starIcons[i] == null) continue;
            starIcons[i].color = i < possibleStars
                ? new Color(1f, 0.85f, 0.2f, 1f)  // 金色
                : new Color(0.4f, 0.4f, 0.4f, 0.5f); // 灰色
        }
    }

    /// <summary>
    /// 每秒脉冲效果
    /// </summary>
    private void CheckSecondTick(float time)
    {
        float currentSecond = Mathf.Floor(time);
        if (currentSecond != previousSecond)
        {
            previousSecond = currentSecond;

            // 接近3星时间时每秒脉冲
            if (time > threeStarTime * 0.8f && time < threeStarTime)
            {
                if (timerRect != null)
                {
                    StopAllCoroutines();
                    StartCoroutine(PulseAnimation());
                }
            }
        }
    }

    private System.Collections.IEnumerator PulseAnimation()
    {
        if (timerRect == null) yield break;

        timerRect.localScale = originalScale * tickPulseScale;
        float t = 0;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            timerRect.localScale = Vector3.Lerp(
                originalScale * tickPulseScale,
                originalScale,
                t / 0.15f);
            yield return null;
        }
        timerRect.localScale = originalScale;
    }

    // ==================== 最佳记录 ====================

    private void LoadBestTime()
    {
        if (GameFlowManager.Instance == null) return;

        int ch = GameFlowManager.Instance.CurrentChapter;
        int lv = GameFlowManager.Instance.CurrentLevel;
        string key = $"best_time_{ch}_{lv}";
        bestTime = PlayerPrefs.GetFloat(key, -1f);

        if (bestTimeText != null)
        {
            if (bestTime > 0f)
            {
                int bm = (int)(bestTime / 60f);
                int bs = (int)(bestTime % 60f);
                bestTimeText.text = $"BEST {bm:00}:{bs:00}";
                bestTimeText.color = goodColor;
            }
            else
            {
                bestTimeText.text = "";
            }
        }
    }

    /// <summary>
    /// 关卡完成时保存最佳记录
    /// </summary>
    public void SaveBestTime(float completionTime)
    {
        if (GameFlowManager.Instance == null) return;

        int ch = GameFlowManager.Instance.CurrentChapter;
        int lv = GameFlowManager.Instance.CurrentLevel;
        string key = $"best_time_{ch}_{lv}";

        float previous = PlayerPrefs.GetFloat(key, -1f);
        if (previous < 0 || completionTime < previous)
        {
            PlayerPrefs.SetFloat(key, completionTime);
            PlayerPrefs.Save();
            bestTime = completionTime;
        }
    }

    private void UpdateStarHint()
    {
        if (starTimeHintText == null) return;

        int m3 = (int)(threeStarTime / 60f);
        int s3 = (int)(threeStarTime % 60f);
        starTimeHintText.text = $"★★★ < {m3:00}:{s3:00}";
        starTimeHintText.color = new Color(1f, 0.85f, 0.2f, 0.6f);
    }

    // ==================== 辅助 ====================

    private float GetCurrentTime()
    {
        if (LevelManager.Instance != null)
            return LevelManager.Instance.GetLevelTime();
        return 0f;
    }

    private void OnGamePaused(GamePausedEvent e)
    {
        isPaused = e.isPaused;
    }

    /// <summary>
    /// 设置星级时间阈值（由LevelData配置调用）
    /// </summary>
    public void SetStarThresholds(float twoStar, float threeStar)
    {
        twoStarTime = twoStar;
        threeStarTime = threeStar;
        UpdateStarHint();
    }
}
