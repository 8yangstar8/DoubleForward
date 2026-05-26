using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 游戏进度总览UI - 显示整体通关进度、世界进度、统计数据
/// 主菜单和暂停菜单中显示
/// </summary>
public class GameProgressUI : MonoBehaviour
{
    [Header("总进度")]
    [SerializeField] private Slider overallProgressBar;
    [SerializeField] private TextMeshProUGUI overallPercentText;
    [SerializeField] private TextMeshProUGUI totalStarsText;
    [SerializeField] private TextMeshProUGUI totalTimeText;

    [Header("世界进度")]
    [SerializeField] private WorldProgressEntry[] worldEntries;

    [Header("统计面板")]
    [SerializeField] private TextMeshProUGUI levelsCompletedText;
    [SerializeField] private TextMeshProUGUI totalDeathsText;
    [SerializeField] private TextMeshProUGUI totalCollectiblesText;
    [SerializeField] private TextMeshProUGUI bestComboText;
    [SerializeField] private TextMeshProUGUI syncActionsText;

    [Header("面板控制")]
    [SerializeField] private GameObject progressPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeSpeed = 5f;

    [System.Serializable]
    public class WorldProgressEntry
    {
        public int chapter;
        public TextMeshProUGUI nameText;
        public Slider progressBar;
        public TextMeshProUGUI starText;
        public Image statusIcon;           // 锁定/进行中/完成
        public Image[] starIcons;           // 3个星星图标
        public Color unlockedColor = Color.white;
        public Color lockedColor = new Color(0.5f, 0.5f, 0.5f);
        public Color completedColor = Color.yellow;
    }

    private bool isVisible;

    void Start()
    {
        closeButton?.onClick.AddListener(Hide);
        if (progressPanel != null)
            progressPanel.SetActive(false);
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 显示进度面板
    /// </summary>
    public void Show()
    {
        if (progressPanel == null) return;

        progressPanel.SetActive(true);
        isVisible = true;
        RefreshAll();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            StartCoroutine(FadeIn());
        }

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayUIClick();
    }

    /// <summary>
    /// 隐藏进度面板
    /// </summary>
    public void Hide()
    {
        isVisible = false;
        if (canvasGroup != null)
            StartCoroutine(FadeOut());
        else if (progressPanel != null)
            progressPanel.SetActive(false);
    }

    /// <summary>
    /// 刷新所有数据
    /// </summary>
    public void RefreshAll()
    {
        RefreshOverallProgress();
        RefreshWorldEntries();
        RefreshStatistics();
    }

    // ==================== 刷新 ====================

    private void RefreshOverallProgress()
    {
        float completion = 0;
        int totalStars = 0;
        float totalTime = 0;

        if (WorldProgressionManager.Instance != null)
        {
            completion = WorldProgressionManager.Instance.GetOverallCompletion();
            totalStars = WorldProgressionManager.Instance.GetTotalStarsEarned();
        }

        if (SaveSystem.Instance != null)
            totalTime = SaveSystem.Instance.Data.totalPlayTime;

        if (overallProgressBar != null)
            overallProgressBar.value = completion / 100f;

        if (overallPercentText != null)
            overallPercentText.text = $"{completion:F1}%";

        if (totalStarsText != null)
            totalStarsText.text = $"{totalStars} / 60";

        if (totalTimeText != null)
            totalTimeText.text = SaveSystem.FormatPlayTime(totalTime);
    }

    private void RefreshWorldEntries()
    {
        if (worldEntries == null || WorldProgressionManager.Instance == null) return;

        foreach (var entry in worldEntries)
        {
            var progress = WorldProgressionManager.Instance.GetChapterProgress(entry.chapter);

            // 名称
            if (entry.nameText != null)
            {
                string name = GetChapterName(entry.chapter);
                entry.nameText.text = name;
                entry.nameText.color = progress.isUnlocked ? entry.unlockedColor : entry.lockedColor;
            }

            // 进度条
            if (entry.progressBar != null)
            {
                float fillAmount = progress.maxStars > 0
                    ? (float)progress.totalStars / progress.maxStars
                    : 0f;
                entry.progressBar.value = fillAmount;
                entry.progressBar.interactable = progress.isUnlocked;
            }

            // 星数
            if (entry.starText != null)
            {
                if (progress.isUnlocked)
                    entry.starText.text = $"{progress.totalStars}/{progress.maxStars}";
                else
                    entry.starText.text = "???";
            }

            // 星星图标
            if (entry.starIcons != null)
            {
                int starsPerLevel = progress.maxStars > 0
                    ? Mathf.CeilToInt(progress.totalStars / (float)(progress.maxStars / 3))
                    : 0;
                for (int i = 0; i < entry.starIcons.Length; i++)
                {
                    if (entry.starIcons[i] != null)
                    {
                        entry.starIcons[i].color = i < starsPerLevel
                            ? Color.yellow
                            : new Color(0.3f, 0.3f, 0.3f);
                    }
                }
            }

            // 状态图标颜色
            if (entry.statusIcon != null)
            {
                if (!progress.isUnlocked)
                    entry.statusIcon.color = entry.lockedColor;
                else if (progress.allStarsCollected)
                    entry.statusIcon.color = entry.completedColor;
                else
                    entry.statusIcon.color = entry.unlockedColor;
            }
        }
    }

    private void RefreshStatistics()
    {
        if (SaveSystem.Instance == null) return;

        var data = SaveSystem.Instance.Data;

        if (levelsCompletedText != null)
            levelsCompletedText.text = data.levelsCompletedCount.ToString();

        if (totalDeathsText != null)
            totalDeathsText.text = data.totalDeaths.ToString();

        if (totalCollectiblesText != null)
            totalCollectiblesText.text = data.totalCollectibles.ToString();

        // 最高连击
        if (bestComboText != null && GameStats.Instance != null)
            bestComboText.text = GameStats.Instance.BestCombo.ToString();

        // 同步操作数
        if (syncActionsText != null && PlayerCoopSync.Instance != null)
            syncActionsText.text = PlayerCoopSync.Instance.TotalSyncActions.ToString();
    }

    // ==================== 辅助 ====================

    private string GetChapterName(int chapter)
    {
        string[] defaultNames = { "光明森林", "水晶洞窟", "深渊", "天空城", "黄昏境界" };
        string key = $"chapter_{chapter}_name";

        if (LocalizationSystem.Instance != null)
        {
            string localized = LocalizationSystem.Instance.GetText(key);
            if (localized != key) return localized;
        }

        return (chapter >= 1 && chapter <= defaultNames.Length)
            ? defaultNames[chapter - 1]
            : $"Chapter {chapter}";
    }

    private IEnumerator FadeIn()
    {
        while (canvasGroup != null && canvasGroup.alpha < 1f)
        {
            canvasGroup.alpha += Time.unscaledDeltaTime * fadeSpeed;
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOut()
    {
        while (canvasGroup != null && canvasGroup.alpha > 0f)
        {
            canvasGroup.alpha -= Time.unscaledDeltaTime * fadeSpeed;
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (progressPanel != null) progressPanel.SetActive(false);
    }
}
