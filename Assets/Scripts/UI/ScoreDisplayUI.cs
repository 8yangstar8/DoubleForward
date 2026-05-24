using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 分数显示UI - HUD中的分数显示、分数弹出动画、结算面板
/// </summary>
public class ScoreDisplayUI : MonoBehaviour
{
    [Header("HUD 分数")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI highScoreText;
    [SerializeField] private RectTransform scoreContainer;

    [Header("分数弹出")]
    [SerializeField] private GameObject scorePopupPrefab;
    [SerializeField] private RectTransform popupParent;
    [SerializeField] private float popupDuration = 1f;
    [SerializeField] private float popupRiseDistance = 80f;

    [Header("结算面板")]
    [SerializeField] private GameObject summaryPanel;
    [SerializeField] private TextMeshProUGUI summaryTotalText;
    [SerializeField] private TextMeshProUGUI summaryKillsText;
    [SerializeField] private TextMeshProUGUI summaryCollectiblesText;
    [SerializeField] private TextMeshProUGUI summaryPuzzlesText;
    [SerializeField] private TextMeshProUGUI summaryTimeText;
    [SerializeField] private TextMeshProUGUI summaryHighScoreText;
    [SerializeField] private GameObject newHighScoreBadge;

    [Header("动画")]
    [SerializeField] private float countUpDuration = 1.5f;
    [SerializeField] private float punchScale = 1.3f;
    [SerializeField] private float punchDuration = 0.15f;

    private int displayedScore;
    private Coroutine countUpCoroutine;

    void OnEnable()
    {
        EventBus.Subscribe<ScoreChangedEvent>(OnScoreChanged);
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<ScoreChangedEvent>(OnScoreChanged);
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
    }

    void Start()
    {
        if (summaryPanel != null)
            summaryPanel.SetActive(false);

        UpdateScoreDisplay(0);
    }

    // ============ 事件处理 ============

    private void OnLevelStart(LevelStartEvent evt)
    {
        displayedScore = 0;
        UpdateScoreDisplay(0);

        if (summaryPanel != null)
            summaryPanel.SetActive(false);

        // 显示最高分
        if (highScoreText != null && ScoreManager.Instance != null)
        {
            int high = ScoreManager.Instance.GetHighScore(evt.chapter, evt.level);
            highScoreText.text = high > 0 ? $"HI: {FormatScore(high)}" : "";
        }
    }

    private void OnScoreChanged(ScoreChangedEvent evt)
    {
        // 弹出加分动画
        ShowScorePopup(evt.delta);

        // 分数递增动画
        if (countUpCoroutine != null)
            StopCoroutine(countUpCoroutine);
        countUpCoroutine = StartCoroutine(AnimateScoreChange(displayedScore, evt.totalScore));

        // 弹跳动画
        if (scoreContainer != null)
            StartCoroutine(PunchScale(scoreContainer));
    }

    private void OnLevelComplete(LevelCompleteEvent evt)
    {
        if (ScoreManager.Instance == null) return;

        StartCoroutine(ShowSummaryDelayed(1f));
    }

    // ============ 显示方法 ============

    private void UpdateScoreDisplay(int score)
    {
        displayedScore = score;
        if (scoreText != null)
            scoreText.text = FormatScore(score);
    }

    private void ShowScorePopup(int amount)
    {
        if (scorePopupPrefab == null || popupParent == null) return;

        var popup = Instantiate(scorePopupPrefab, popupParent);
        var text = popup.GetComponent<TextMeshProUGUI>();
        if (text == null) text = popup.GetComponentInChildren<TextMeshProUGUI>();

        if (text != null)
        {
            text.text = $"+{FormatScore(amount)}";

            // 根据分数大小调整颜色
            if (amount >= 1000)
                text.color = new Color(1f, 0.85f, 0f); // 金色
            else if (amount >= 500)
                text.color = new Color(0.4f, 0.8f, 1f); // 蓝色
            else
                text.color = Color.white;
        }

        StartCoroutine(AnimatePopup(popup));
    }

    private IEnumerator AnimatePopup(GameObject popup)
    {
        if (popup == null) yield break;

        var rect = popup.GetComponent<RectTransform>();
        var canvasGroup = popup.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popup.AddComponent<CanvasGroup>();

        Vector2 startPos = rect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < popupDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popupDuration;

            // 上浮
            rect.anchoredPosition = startPos + Vector2.up * (popupRiseDistance * t);

            // 淡出（后半段）
            if (t > 0.5f)
                canvasGroup.alpha = 1f - (t - 0.5f) * 2f;

            // 前半段放大
            if (t < 0.2f)
            {
                float scale = 1f + (t / 0.2f) * 0.3f;
                rect.localScale = Vector3.one * scale;
            }
            else
            {
                rect.localScale = Vector3.one;
            }

            yield return null;
        }

        Destroy(popup);
    }

    private IEnumerator AnimateScoreChange(int from, int to)
    {
        float elapsed = 0f;
        float duration = Mathf.Min(countUpDuration, Mathf.Abs(to - from) * 0.01f);
        duration = Mathf.Max(duration, 0.3f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t; // ease in

            int current = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
            UpdateScoreDisplay(current);
            yield return null;
        }

        UpdateScoreDisplay(to);
        countUpCoroutine = null;
    }

    private IEnumerator PunchScale(RectTransform target)
    {
        float elapsed = 0f;

        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / punchDuration;

            float scale;
            if (t < 0.5f)
                scale = Mathf.Lerp(1f, punchScale, t * 2f);
            else
                scale = Mathf.Lerp(punchScale, 1f, (t - 0.5f) * 2f);

            target.localScale = Vector3.one * scale;
            yield return null;
        }

        target.localScale = Vector3.one;
    }

    // ============ 结算面板 ============

    private IEnumerator ShowSummaryDelayed(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        ShowSummary();
    }

    private void ShowSummary()
    {
        if (summaryPanel == null || ScoreManager.Instance == null) return;

        var summary = ScoreManager.Instance.GetSummary();
        summaryPanel.SetActive(true);

        // 逐项递增显示
        StartCoroutine(AnimateSummary(summary));
    }

    private IEnumerator AnimateSummary(ScoreManager.LevelScoreSummary summary)
    {
        // 击杀
        yield return AnimateSummaryLine(summaryKillsText, 0, summary.kills, 0.3f);
        yield return new WaitForSecondsRealtime(0.1f);

        // 收集品
        yield return AnimateSummaryLine(summaryCollectiblesText, 0, summary.collectibles, 0.3f);
        yield return new WaitForSecondsRealtime(0.1f);

        // 谜题
        yield return AnimateSummaryLine(summaryPuzzlesText, 0, summary.puzzles, 0.3f);
        yield return new WaitForSecondsRealtime(0.1f);

        // 时间
        if (summaryTimeText != null)
        {
            int minutes = Mathf.FloorToInt(summary.clearTime / 60f);
            int seconds = Mathf.FloorToInt(summary.clearTime % 60f);
            summaryTimeText.text = $"{minutes:00}:{seconds:00}";
        }

        yield return new WaitForSecondsRealtime(0.2f);

        // 总分（大数递增动画）
        yield return AnimateSummaryLine(summaryTotalText, 0, summary.totalScore, 1f);

        // 最高分标记
        if (summary.isNewHighScore && newHighScoreBadge != null)
        {
            newHighScoreBadge.SetActive(true);
            // 弹跳出现
            var rect = newHighScoreBadge.GetComponent<RectTransform>();
            if (rect != null)
                StartCoroutine(PunchScale(rect));
        }

        if (summaryHighScoreText != null)
            summaryHighScoreText.text = FormatScore(summary.highScore);
    }

    private IEnumerator AnimateSummaryLine(TextMeshProUGUI text, int from, int to, float duration)
    {
        if (text == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            int val = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
            text.text = FormatScore(val);
            yield return null;
        }
        text.text = FormatScore(to);
    }

    // ============ 工具方法 ============

    private string FormatScore(int score)
    {
        if (score >= 1000000)
            return $"{score / 1000000f:F1}M";
        if (score >= 10000)
            return $"{score / 1000f:F1}K";
        return score.ToString("N0");
    }
}
