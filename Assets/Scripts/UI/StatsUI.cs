using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 统计数据与排行榜显示界面
/// </summary>
public class StatsUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private Button closeButton;

    [Header("标签页")]
    [SerializeField] private Button overviewTab;
    [SerializeField] private Button leaderboardTab;
    [SerializeField] private Button characterTab;
    [SerializeField] private GameObject overviewPage;
    [SerializeField] private GameObject leaderboardPage;
    [SerializeField] private GameObject characterPage;

    [Header("概览统计")]
    [SerializeField] private TextMeshProUGUI playTimeText;
    [SerializeField] private TextMeshProUGUI levelsCompletedText;
    [SerializeField] private TextMeshProUGUI bossesDefeatedText;
    [SerializeField] private TextMeshProUGUI enemiesKilledText;
    [SerializeField] private TextMeshProUGUI collectiblesText;
    [SerializeField] private TextMeshProUGUI highestComboText;
    [SerializeField] private TextMeshProUGUI deathsText;
    [SerializeField] private TextMeshProUGUI distanceText;

    [Header("排行榜")]
    [SerializeField] private Transform leaderboardContent;
    [SerializeField] private GameObject leaderboardItemPrefab;

    [Header("角色统计")]
    [SerializeField] private TextMeshProUGUI luxJumpsText;
    [SerializeField] private TextMeshProUGUI luxBeamText;
    [SerializeField] private TextMeshProUGUI luxBridgeText;
    [SerializeField] private TextMeshProUGUI noxDashText;
    [SerializeField] private TextMeshProUGUI noxPhaseText;
    [SerializeField] private TextMeshProUGUI noxZoneText;

    private List<GameObject> spawnedItems = new List<GameObject>();

    void Start()
    {
        closeButton?.onClick.AddListener(Hide);
        overviewTab?.onClick.AddListener(() => ShowPage(0));
        leaderboardTab?.onClick.AddListener(() => ShowPage(1));
        characterTab?.onClick.AddListener(() => ShowPage(2));
    }

    public void Show()
    {
        if (statsPanel != null)
            statsPanel.SetActive(true);

        ShowPage(0);
        RefreshData();
    }

    public void Hide()
    {
        if (statsPanel != null)
            statsPanel.SetActive(false);
    }

    private void ShowPage(int index)
    {
        if (overviewPage != null) overviewPage.SetActive(index == 0);
        if (leaderboardPage != null) leaderboardPage.SetActive(index == 1);
        if (characterPage != null) characterPage.SetActive(index == 2);

        if (index == 1) RefreshLeaderboard();
    }

    private void RefreshData()
    {
        if (GameStats.Instance == null) return;
        var stats = GameStats.Instance.Stats;

        float totalTime = stats.totalPlayTime + Time.time;

        SetText(playTimeText, SaveSystem.FormatPlayTime(totalTime));
        SetText(levelsCompletedText, stats.totalLevelsCompleted.ToString());
        SetText(bossesDefeatedText, stats.totalBossesDefeated.ToString());
        SetText(enemiesKilledText, stats.totalEnemiesDefeated.ToString());
        SetText(collectiblesText, stats.totalCollectiblesFound.ToString());
        SetText(highestComboText, stats.highestCombo.ToString());
        SetText(deathsText, stats.totalDeaths.ToString());
        SetText(distanceText, $"{stats.totalDistanceTraveled:F0}m");

        // 角色统计
        SetText(luxJumpsText, stats.luxDoubleJumps.ToString());
        SetText(luxBeamText, stats.luxLightBeamUses.ToString());
        SetText(luxBridgeText, stats.luxLightBridgeUses.ToString());
        SetText(noxDashText, stats.noxDashCount.ToString());
        SetText(noxPhaseText, stats.noxShadowPhaseUses.ToString());
        SetText(noxZoneText, stats.noxShadowZoneUses.ToString());
    }

    private void RefreshLeaderboard()
    {
        // 清除旧项
        foreach (var item in spawnedItems)
        {
            if (item != null) Destroy(item);
        }
        spawnedItems.Clear();

        if (GameStats.Instance == null || leaderboardContent == null || leaderboardItemPrefab == null) return;

        var records = GameStats.Instance.GetLeaderboard(10);
        int rank = 1;

        foreach (var record in records)
        {
            var itemObj = Instantiate(leaderboardItemPrefab, leaderboardContent);

            var rankText = itemObj.transform.Find("RankText")?.GetComponent<TextMeshProUGUI>();
            var levelText = itemObj.transform.Find("LevelText")?.GetComponent<TextMeshProUGUI>();
            var scoreText = itemObj.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
            var timeText = itemObj.transform.Find("TimeText")?.GetComponent<TextMeshProUGUI>();
            var starsText = itemObj.transform.Find("StarsText")?.GetComponent<TextMeshProUGUI>();

            if (rankText != null) rankText.text = $"#{rank}";
            if (levelText != null) levelText.text = record.levelId;
            if (scoreText != null) scoreText.text = record.bestScore.ToString("N0");
            if (timeText != null)
            {
                int minutes = Mathf.FloorToInt(record.bestTime / 60);
                int seconds = Mathf.FloorToInt(record.bestTime % 60);
                timeText.text = $"{minutes}:{seconds:D2}";
            }
            if (starsText != null)
            {
                string stars = new string('★', record.bestStars) +
                              new string('☆', 3 - record.bestStars);
                starsText.text = stars;
            }

            // 排名颜色
            var bg = itemObj.GetComponent<Image>();
            if (bg != null)
            {
                if (rank == 1) bg.color = new Color(1f, 0.84f, 0f, 0.3f);      // 金
                else if (rank == 2) bg.color = new Color(0.75f, 0.75f, 0.75f, 0.3f); // 银
                else if (rank == 3) bg.color = new Color(0.8f, 0.5f, 0.2f, 0.3f);    // 铜
            }

            spawnedItems.Add(itemObj);
            rank++;
        }
    }

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null) text.text = value;
    }
}
