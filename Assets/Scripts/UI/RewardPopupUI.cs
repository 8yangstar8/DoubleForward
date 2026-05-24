using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 奖励弹窗UI - 关卡完成奖励、成就奖励、每日奖励等
/// 支持多种奖励展示类型，带动画效果
/// </summary>
public class RewardPopupUI : MonoBehaviour
{
    public static RewardPopupUI Instance { get; private set; }

    [Header("面板")]
    [SerializeField] private CanvasGroup panelCanvas;
    [SerializeField] private RectTransform contentContainer;

    [Header("标题")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Image titleIcon;

    [Header("星星")]
    [SerializeField] private Image[] starImages;                // 3颗星
    [SerializeField] private Sprite starFilled;
    [SerializeField] private Sprite starEmpty;
    [SerializeField] private float starAnimDelay = 0.4f;

    [Header("统计行")]
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI collectiblesText;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("奖励物品")]
    [SerializeField] private Transform rewardItemContainer;
    [SerializeField] private GameObject rewardItemPrefab;        // 奖励物品模板

    [Header("按钮")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button shareButton;
    [SerializeField] private TextMeshProUGUI nextButtonText;

    [Header("动画")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float scaleAnimDuration = 0.25f;
    [SerializeField] private float statCountDuration = 0.8f;

    private System.Action onNextAction;
    private System.Action onReplayAction;
    private System.Action onMenuAction;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (panelCanvas != null) panelCanvas.gameObject.SetActive(false);

        SetupButtons();
    }

    private void SetupButtons()
    {
        if (nextButton != null)
            nextButton.onClick.AddListener(() => { Hide(); onNextAction?.Invoke(); });
        if (replayButton != null)
            replayButton.onClick.AddListener(() => { Hide(); onReplayAction?.Invoke(); });
        if (menuButton != null)
            menuButton.onClick.AddListener(() => { Hide(); onMenuAction?.Invoke(); });
        if (shareButton != null)
            shareButton.onClick.AddListener(OnShareClicked);
    }

    /// <summary>
    /// 显示关卡完成奖励
    /// </summary>
    public void ShowLevelComplete(LevelCompleteData data)
    {
        StartCoroutine(AnimateLevelComplete(data));
    }

    /// <summary>
    /// 显示通用奖励弹窗
    /// </summary>
    public void ShowReward(string title, Sprite icon, List<RewardItem> rewards, System.Action onClose = null)
    {
        StartCoroutine(AnimateRewards(title, icon, rewards, onClose));
    }

    /// <summary>
    /// 设置按钮回调
    /// </summary>
    public void SetCallbacks(System.Action onNext, System.Action onReplay, System.Action onMenu)
    {
        onNextAction = onNext;
        onReplayAction = onReplay;
        onMenuAction = onMenu;
    }

    private IEnumerator AnimateLevelComplete(LevelCompleteData data)
    {
        // 初始化
        if (panelCanvas != null)
        {
            panelCanvas.gameObject.SetActive(true);
            panelCanvas.alpha = 0;
        }

        if (contentContainer != null)
            contentContainer.localScale = Vector3.one * 0.5f;

        // 设置标题
        if (titleText != null)
        {
            string title = "complete_title";
            if (LocalizationSystem.Instance != null)
                title = LocalizationSystem.Instance.Get("complete_title", "Level Complete!");
            titleText.text = title;
        }

        // 重置星星
        if (starImages != null)
        {
            foreach (var star in starImages)
            {
                if (star != null && starEmpty != null)
                    star.sprite = starEmpty;
            }
        }

        // 重置统计
        if (timeText != null) timeText.text = "0.0s";
        if (collectiblesText != null) collectiblesText.text = "0/0";
        if (comboText != null) comboText.text = "0x";
        if (scoreText != null) scoreText.text = "0";

        // 淡入
        yield return FadeIn();

        // 弹出动画
        yield return ScaleIn();

        // 星星动画
        if (starImages != null)
        {
            for (int i = 0; i < Mathf.Min(data.stars, starImages.Length); i++)
            {
                yield return new WaitForSecondsRealtime(starAnimDelay);
                if (starImages[i] != null && starFilled != null)
                {
                    starImages[i].sprite = starFilled;
                    StartCoroutine(PunchScale(starImages[i].rectTransform));

                    if (SoundFeedback.Instance != null)
                        SoundFeedback.Instance.Play("star_earn");
                }
            }
        }

        yield return new WaitForSecondsRealtime(0.3f);

        // 统计数字滚动
        yield return CountUpStats(data);

        // 显示下一关按钮文字
        if (nextButtonText != null)
        {
            string nextText = "complete_next";
            if (LocalizationSystem.Instance != null)
                nextText = LocalizationSystem.Instance.Get("complete_next", "Next Level");
            nextButtonText.text = data.isLastLevel ? "Credits" : nextText;
        }
    }

    private IEnumerator AnimateRewards(string title, Sprite icon, List<RewardItem> rewards, System.Action onClose)
    {
        if (panelCanvas != null)
        {
            panelCanvas.gameObject.SetActive(true);
            panelCanvas.alpha = 0;
        }

        if (contentContainer != null)
            contentContainer.localScale = Vector3.one * 0.5f;

        if (titleText != null) titleText.text = title;
        if (titleIcon != null && icon != null)
        {
            titleIcon.sprite = icon;
            titleIcon.gameObject.SetActive(true);
        }

        // 隐藏星星和统计
        if (starImages != null)
            foreach (var s in starImages) if (s != null) s.gameObject.SetActive(false);
        if (timeText != null) timeText.transform.parent.gameObject.SetActive(false);

        yield return FadeIn();
        yield return ScaleIn();

        // 生成奖励物品
        ClearRewardItems();
        if (rewards != null && rewardItemPrefab != null && rewardItemContainer != null)
        {
            for (int i = 0; i < rewards.Count; i++)
            {
                yield return new WaitForSecondsRealtime(0.2f);

                var item = Instantiate(rewardItemPrefab, rewardItemContainer);
                var icon_ = item.transform.Find("Icon")?.GetComponent<Image>();
                var name_ = item.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                var desc_ = item.transform.Find("Description")?.GetComponent<TextMeshProUGUI>();

                if (icon_ != null && rewards[i].icon != null) icon_.sprite = rewards[i].icon;
                if (name_ != null) name_.text = rewards[i].name;
                if (desc_ != null) desc_.text = rewards[i].description;

                StartCoroutine(PunchScale(item.GetComponent<RectTransform>()));
            }
        }

        // 设置关闭回调
        onNextAction = onClose;
    }

    private IEnumerator FadeIn()
    {
        if (panelCanvas == null) yield break;

        float t = 0;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            panelCanvas.alpha = t / fadeInDuration;
            yield return null;
        }
        panelCanvas.alpha = 1f;
    }

    private IEnumerator ScaleIn()
    {
        if (contentContainer == null) yield break;

        float t = 0;
        while (t < scaleAnimDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / scaleAnimDuration;
            // Elastic ease out
            float scale = 1f + Mathf.Sin(p * Mathf.PI * 2f) * 0.1f * (1f - p);
            contentContainer.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, p) * (1f + (scale - 1f));
            yield return null;
        }
        contentContainer.localScale = Vector3.one;
    }

    private IEnumerator PunchScale(RectTransform rt)
    {
        if (rt == null) yield break;

        Vector3 original = rt.localScale;
        rt.localScale = original * 1.3f;

        float t = 0;
        float dur = 0.2f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(original * 1.3f, original, t / dur);
            yield return null;
        }
        rt.localScale = original;
    }

    private IEnumerator CountUpStats(LevelCompleteData data)
    {
        float elapsed = 0;
        while (elapsed < statCountDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = elapsed / statCountDuration;

            if (timeText != null)
                timeText.text = $"{Mathf.Lerp(0, data.time, p):F1}s";

            if (collectiblesText != null)
                collectiblesText.text = $"{Mathf.RoundToInt(Mathf.Lerp(0, data.collectibles, p))}/{data.totalCollectibles}";

            if (comboText != null)
                comboText.text = $"{Mathf.RoundToInt(Mathf.Lerp(0, data.maxCombo, p))}x";

            if (scoreText != null)
                scoreText.text = $"{Mathf.RoundToInt(Mathf.Lerp(0, data.score, p))}";

            yield return null;
        }

        // 最终精确值
        if (timeText != null) timeText.text = $"{data.time:F1}s";
        if (collectiblesText != null) collectiblesText.text = $"{data.collectibles}/{data.totalCollectibles}";
        if (comboText != null) comboText.text = $"{data.maxCombo}x";
        if (scoreText != null) scoreText.text = $"{data.score}";
    }

    private void OnShareClicked()
    {
        if (MobileServices.Instance != null)
        {
            int chapter = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentChapter : 1;
            int level = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentLevel : 1;
            MobileServices.Instance.ShareWithScreenshot(
                $"Level {chapter}-{level} Complete!"
            );
        }
    }

    private void ClearRewardItems()
    {
        if (rewardItemContainer == null) return;
        for (int i = rewardItemContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(rewardItemContainer.GetChild(i).gameObject);
        }
    }

    public void Hide()
    {
        if (panelCanvas != null)
            panelCanvas.gameObject.SetActive(false);
    }

    // ============ 数据结构 ============

    [System.Serializable]
    public class LevelCompleteData
    {
        public int stars;
        public float time;
        public int collectibles;
        public int totalCollectibles;
        public int maxCombo;
        public int score;
        public bool isLastLevel;
    }

    [System.Serializable]
    public class RewardItem
    {
        public string name;
        public string description;
        public Sprite icon;
    }
}
