using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 排行榜界面 - 展示关卡排名、排序切换、玩家高亮
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;

    [Header("关卡选择")]
    [SerializeField] private TextMeshProUGUI levelTitle;
    [SerializeField] private Button prevLevelButton;
    [SerializeField] private Button nextLevelButton;

    [Header("排序标签")]
    [SerializeField] private Button sortScoreButton;
    [SerializeField] private Button sortTimeButton;
    [SerializeField] private Button sortStarsButton;
    [SerializeField] private Button sortDeathsButton;
    [SerializeField] private Color activeSortColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color inactiveSortColor = Color.white;

    [Header("列表")]
    [SerializeField] private Transform entryContainer;
    [SerializeField] private GameObject entryPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Header("玩家信息")]
    [SerializeField] private TextMeshProUGUI playerRankText;
    [SerializeField] private TextMeshProUGUI playerScoreText;
    [SerializeField] private Color playerHighlightColor = new Color(0.3f, 0.7f, 1f, 0.3f);

    [Header("排名图标颜色")]
    [SerializeField] private Color goldColor = new Color(1f, 0.84f, 0f);
    [SerializeField] private Color silverColor = new Color(0.75f, 0.75f, 0.75f);
    [SerializeField] private Color bronzeColor = new Color(0.8f, 0.5f, 0.2f);

    private int currentChapter = 1;
    private int currentLevel = 1;
    private LeaderboardManager.BoardType currentSortType = LeaderboardManager.BoardType.HighScore;
    private List<GameObject> spawnedEntries = new List<GameObject>();
    private static readonly int[] levelsPerChapter = { 4, 4, 4, 4, 4 };

    void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (prevLevelButton != null) prevLevelButton.onClick.AddListener(PrevLevel);
        if (nextLevelButton != null) nextLevelButton.onClick.AddListener(NextLevel);

        if (sortScoreButton != null) sortScoreButton.onClick.AddListener(
            () => SetSortType(LeaderboardManager.BoardType.HighScore));
        if (sortTimeButton != null) sortTimeButton.onClick.AddListener(
            () => SetSortType(LeaderboardManager.BoardType.BestTime));
        if (sortStarsButton != null) sortStarsButton.onClick.AddListener(
            () => SetSortType(LeaderboardManager.BoardType.MostStars));
        if (sortDeathsButton != null) sortDeathsButton.onClick.AddListener(
            () => SetSortType(LeaderboardManager.BoardType.LeastDeaths));

        if (panel != null) panel.SetActive(false);
    }

    public void Show(int chapter = 1, int level = 1)
    {
        currentChapter = chapter;
        currentLevel = level;

        if (panel != null) panel.SetActive(true);
        RefreshDisplay();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void SetSortType(LeaderboardManager.BoardType type)
    {
        currentSortType = type;
        UpdateSortButtons();
        RefreshEntryList();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    private void PrevLevel()
    {
        currentLevel--;
        if (currentLevel < 1)
        {
            currentChapter--;
            if (currentChapter < 1) currentChapter = 5;
            currentLevel = levelsPerChapter[currentChapter - 1];
        }
        RefreshDisplay();
    }

    private void NextLevel()
    {
        currentLevel++;
        if (currentLevel > levelsPerChapter[currentChapter - 1])
        {
            currentChapter++;
            if (currentChapter > 5) currentChapter = 1;
            currentLevel = 1;
        }
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        UpdateLevelTitle();
        UpdateSortButtons();
        RefreshEntryList();
        UpdatePlayerInfo();
    }

    private void UpdateLevelTitle()
    {
        if (levelTitle == null) return;

        string chapterName = $"Chapter {currentChapter}";
        if (LocalizationSystem.Instance != null)
            chapterName = LocalizationSystem.Instance.Get($"chapter_{currentChapter}_title", chapterName);

        levelTitle.text = $"{chapterName} - {currentLevel}";
    }

    private void UpdateSortButtons()
    {
        SetButtonColor(sortScoreButton, currentSortType == LeaderboardManager.BoardType.HighScore);
        SetButtonColor(sortTimeButton, currentSortType == LeaderboardManager.BoardType.BestTime);
        SetButtonColor(sortStarsButton, currentSortType == LeaderboardManager.BoardType.MostStars);
        SetButtonColor(sortDeathsButton, currentSortType == LeaderboardManager.BoardType.LeastDeaths);
    }

    private void SetButtonColor(Button button, bool active)
    {
        if (button == null) return;
        var text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.color = active ? activeSortColor : inactiveSortColor;
    }

    private void RefreshEntryList()
    {
        // 清除旧条目
        foreach (var obj in spawnedEntries)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedEntries.Clear();

        if (LeaderboardManager.Instance == null || entryPrefab == null || entryContainer == null) return;

        var entries = LeaderboardManager.Instance.GetLeaderboard(
            currentChapter, currentLevel, currentSortType, 50);

        string myPlayerId = SystemInfo.deviceUniqueIdentifier;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var entryObj = Instantiate(entryPrefab, entryContainer);
            spawnedEntries.Add(entryObj);

            var texts = entryObj.GetComponentsInChildren<TextMeshProUGUI>();
            var images = entryObj.GetComponentsInChildren<Image>();

            // 排名
            if (texts.Length > 0)
            {
                texts[0].text = $"#{i + 1}";

                // 前三名颜色
                if (i == 0) texts[0].color = goldColor;
                else if (i == 1) texts[0].color = silverColor;
                else if (i == 2) texts[0].color = bronzeColor;
            }

            // 玩家名
            if (texts.Length > 1)
                texts[1].text = entry.playerName;

            // 分数/时间（根据排序类型）
            if (texts.Length > 2)
            {
                switch (currentSortType)
                {
                    case LeaderboardManager.BoardType.BestTime:
                        texts[2].text = LevelTimer.FormatTime(entry.time);
                        break;
                    case LeaderboardManager.BoardType.LeastDeaths:
                        texts[2].text = entry.deaths.ToString();
                        break;
                    case LeaderboardManager.BoardType.MostStars:
                        texts[2].text = $"{entry.stars}/3";
                        break;
                    default:
                        texts[2].text = entry.score.ToString();
                        break;
                }
            }

            // 高亮当前玩家
            if (entry.playerId == myPlayerId && images.Length > 0)
            {
                images[0].color = playerHighlightColor;
            }
        }

        // 滚动到顶部
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private void UpdatePlayerInfo()
    {
        if (LeaderboardManager.Instance == null) return;

        int rank = LeaderboardManager.Instance.GetPlayerRank(
            currentChapter, currentLevel, currentSortType);

        if (playerRankText != null)
        {
            if (rank > 0)
            {
                string rankLabel = "Rank";
                if (LocalizationSystem.Instance != null)
                    rankLabel = LocalizationSystem.Instance.Get("leaderboard_rank", "Rank");
                playerRankText.text = $"{rankLabel}: #{rank}";
            }
            else
            {
                string noRank = "Not ranked";
                if (LocalizationSystem.Instance != null)
                    noRank = LocalizationSystem.Instance.Get("leaderboard_no_rank", "Not ranked");
                playerRankText.text = noRank;
            }
        }

        if (playerScoreText != null)
        {
            var best = LeaderboardManager.Instance.GetPlayerBest(currentChapter, currentLevel);
            if (best != null)
                playerScoreText.text = $"Best: {best.score}";
            else
                playerScoreText.text = "";
        }
    }
}
