using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 每日奖励UI - 7天日历显示、领取动画、倒计时
/// 进入主菜单时自动弹出（如果有可领取奖励）
/// </summary>
public class DailyRewardUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("日历格子")]
    [SerializeField] private Transform calendarContainer;
    [SerializeField] private GameObject dayCardPrefab;

    [Header("领取按钮")]
    [SerializeField] private Button claimButton;
    [SerializeField] private TextMeshProUGUI claimButtonText;

    [Header("倒计时")]
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private GameObject countdownPanel;

    [Header("连续签到")]
    [SerializeField] private TextMeshProUGUI streakText;
    [SerializeField] private Slider streakProgress;

    [Header("奖励预览")]
    [SerializeField] private TextMeshProUGUI rewardCoinsText;
    [SerializeField] private TextMeshProUGUI rewardGemsText;
    [SerializeField] private Image rewardIcon;

    [Header("动画")]
    [SerializeField] private float cardAnimDelay = 0.1f;
    [SerializeField] private float claimAnimDuration = 0.5f;

    [Header("日卡颜色")]
    [SerializeField] private Color claimedColor = new Color(0.5f, 0.8f, 0.5f);
    [SerializeField] private Color todayColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color lockedColor = new Color(0.4f, 0.4f, 0.4f);
    [SerializeField] private Color normalColor = Color.white;

    private List<GameObject> dayCards = new List<GameObject>();
    private bool isShowingCountdown;

    void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (claimButton != null) claimButton.onClick.AddListener(ClaimReward);
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (isShowingCountdown && DailyRewardSystem.Instance != null)
        {
            UpdateCountdown();
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 显示每日奖励面板
    /// </summary>
    public void Show()
    {
        if (panel != null) panel.SetActive(true);

        RefreshCalendar();
        UpdateClaimButton();
        UpdateStreakDisplay();

        // 弹出动画
        if (panelCanvasGroup != null)
            StartCoroutine(FadeIn());

        // 日卡逐个出现
        StartCoroutine(AnimateDayCards());
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>
    /// 自动检查并弹出（主菜单调用）
    /// </summary>
    public void TryAutoShow()
    {
        if (DailyRewardSystem.Instance != null && DailyRewardSystem.Instance.CanClaimToday)
        {
            Show();
        }
    }

    // ==================== 日历刷新 ====================

    private void RefreshCalendar()
    {
        // 清除旧卡片
        foreach (var card in dayCards)
        {
            if (card != null) Destroy(card);
        }
        dayCards.Clear();

        if (DailyRewardSystem.Instance == null || dayCardPrefab == null || calendarContainer == null)
            return;

        var calendarInfo = DailyRewardSystem.Instance.GetCalendarInfo();

        foreach (var info in calendarInfo)
        {
            var cardObj = Instantiate(dayCardPrefab, calendarContainer);
            dayCards.Add(cardObj);
            SetupDayCard(cardObj, info);
        }
    }

    private void SetupDayCard(GameObject cardObj, DailyRewardInfo info)
    {
        var texts = cardObj.GetComponentsInChildren<TextMeshProUGUI>();
        var images = cardObj.GetComponentsInChildren<Image>();

        // 日期标签
        if (texts.Length > 0)
        {
            string dayLabel = $"Day {info.dayIndex + 1}";
            if (LocalizationSystem.Instance != null)
                dayLabel = LocalizationSystem.Instance.Get($"daily_day_{info.dayIndex + 1}", dayLabel);
            texts[0].text = dayLabel;
        }

        // 奖励内容
        if (texts.Length > 1)
        {
            string rewardText = "";
            if (info.reward.coins > 0)
                rewardText += $"{info.reward.coins}C";
            if (info.reward.gems > 0)
            {
                if (!string.IsNullOrEmpty(rewardText)) rewardText += "\n";
                rewardText += $"{info.reward.gems}G";
            }
            texts[1].text = rewardText;
        }

        // 状态标记
        if (texts.Length > 2)
        {
            if (info.isClaimed)
                texts[2].text = "V"; // 已领取勾号
            else if (info.isToday)
                texts[2].text = "!";
            else
                texts[2].text = "";
        }

        // 背景颜色
        if (images.Length > 0)
        {
            if (info.isClaimed)
                images[0].color = claimedColor;
            else if (info.isToday)
                images[0].color = todayColor;
            else if (info.isLocked)
                images[0].color = lockedColor;
            else
                images[0].color = normalColor;
        }

        // 缩放效果（今天的卡片稍大）
        if (info.isToday)
        {
            cardObj.transform.localScale = Vector3.one * 1.1f;
        }
    }

    // ==================== 领取奖励 ====================

    private void ClaimReward()
    {
        if (DailyRewardSystem.Instance == null || !DailyRewardSystem.Instance.CanClaimToday)
            return;

        var reward = DailyRewardSystem.Instance.TodayReward;

        if (DailyRewardSystem.Instance.ClaimDailyReward())
        {
            // 领取动画
            StartCoroutine(PlayClaimAnimation(reward));

            // 刷新UI
            RefreshCalendar();
            UpdateClaimButton();
            UpdateStreakDisplay();
        }
    }

    private IEnumerator PlayClaimAnimation(DailyRewardSystem.DailyReward reward)
    {
        // 按钮缩放动画
        if (claimButton != null)
        {
            var rt = claimButton.GetComponent<RectTransform>();
            Vector3 originalScale = rt.localScale;

            // 弹出
            float t = 0;
            while (t < claimAnimDuration * 0.5f)
            {
                t += Time.unscaledDeltaTime;
                float progress = t / (claimAnimDuration * 0.5f);
                rt.localScale = originalScale * (1f + Mathf.Sin(progress * Mathf.PI) * 0.3f);
                yield return null;
            }

            rt.localScale = originalScale;
        }

        // 显示获得的奖励
        if (rewardCoinsText != null && reward.coins > 0)
        {
            rewardCoinsText.text = $"+{reward.coins}";
            rewardCoinsText.gameObject.SetActive(true);
            StartCoroutine(FadeOutText(rewardCoinsText, 2f));
        }

        if (rewardGemsText != null && reward.gems > 0)
        {
            rewardGemsText.text = $"+{reward.gems}";
            rewardGemsText.gameObject.SetActive(true);
            StartCoroutine(FadeOutText(rewardGemsText, 2f));
        }
    }

    // ==================== 状态更新 ====================

    private void UpdateClaimButton()
    {
        if (DailyRewardSystem.Instance == null) return;

        bool canClaim = DailyRewardSystem.Instance.CanClaimToday;

        if (claimButton != null)
            claimButton.interactable = canClaim;

        if (claimButtonText != null)
        {
            if (canClaim)
            {
                string text = "Claim";
                if (LocalizationSystem.Instance != null)
                    text = LocalizationSystem.Instance.Get("daily_claim", "Claim");
                claimButtonText.text = text;
            }
            else
            {
                string text = "Claimed";
                if (LocalizationSystem.Instance != null)
                    text = LocalizationSystem.Instance.Get("daily_claimed", "Claimed");
                claimButtonText.text = text;
            }
        }

        // 倒计时显示
        isShowingCountdown = !canClaim;
        if (countdownPanel != null)
            countdownPanel.SetActive(!canClaim);
    }

    private void UpdateCountdown()
    {
        if (countdownText == null || DailyRewardSystem.Instance == null) return;
        countdownText.text = DailyRewardSystem.Instance.GetCountdownString();
    }

    private void UpdateStreakDisplay()
    {
        if (DailyRewardSystem.Instance == null) return;

        int streak = DailyRewardSystem.Instance.CurrentStreak;

        if (streakText != null)
        {
            string label = $"Streak: {streak}";
            if (LocalizationSystem.Instance != null)
                label = LocalizationSystem.Instance.Get("daily_streak", "Streak") + $": {streak}";
            streakText.text = label;
        }

        if (streakProgress != null)
        {
            streakProgress.maxValue = 7;
            streakProgress.value = streak % 7;
        }
    }

    // ==================== 动画 ====================

    private IEnumerator FadeIn()
    {
        if (panelCanvasGroup == null) yield break;

        panelCanvasGroup.alpha = 0;
        float t = 0;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            panelCanvasGroup.alpha = Mathf.Lerp(0, 1, t / 0.3f);
            yield return null;
        }
        panelCanvasGroup.alpha = 1;
    }

    private IEnumerator AnimateDayCards()
    {
        foreach (var card in dayCards)
        {
            if (card == null) continue;

            // 初始隐藏
            var cg = card.GetComponent<CanvasGroup>();
            if (cg == null) cg = card.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            card.transform.localScale = Vector3.one * 0.5f;
        }

        foreach (var card in dayCards)
        {
            if (card == null) continue;

            var cg = card.GetComponent<CanvasGroup>();
            float t = 0;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                float p = t / 0.2f;
                if (cg != null) cg.alpha = p;
                card.transform.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one, p);
                yield return null;
            }

            if (cg != null) cg.alpha = 1;
            card.transform.localScale = Vector3.one;

            yield return new WaitForSecondsRealtime(cardAnimDelay);
        }
    }

    private IEnumerator FadeOutText(TextMeshProUGUI text, float duration)
    {
        float t = 0;
        Color originalColor = text.color;
        Vector3 startPos = text.rectTransform.anchoredPosition;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / duration;

            // 向上飘动
            text.rectTransform.anchoredPosition = startPos + Vector2.up * p * 50f;

            // 渐隐
            Color c = originalColor;
            c.a = 1f - p;
            text.color = c;

            yield return null;
        }

        text.gameObject.SetActive(false);
        text.rectTransform.anchoredPosition = startPos;
        text.color = originalColor;
    }
}
