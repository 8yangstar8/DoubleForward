using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 成就展示面板 - 显示所有成就列表、进度和统计
/// </summary>
public class AchievementPanelUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject achievementPanel;
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject achievementItemPrefab;
    [SerializeField] private Button closeButton;

    [Header("统计")]
    [SerializeField] private TextMeshProUGUI totalText;
    [SerializeField] private Slider totalProgressBar;

    [Header("分类筛选")]
    [SerializeField] private Button allButton;
    [SerializeField] private Button storyButton;
    [SerializeField] private Button challengeButton;
    [SerializeField] private Button secretButton;

    private List<GameObject> spawnedItems = new List<GameObject>();
    private string currentFilter = "";

    void Start()
    {
        closeButton?.onClick.AddListener(Hide);
        allButton?.onClick.AddListener(() => FilterByCategory(""));
        storyButton?.onClick.AddListener(() => FilterByCategory("story"));
        challengeButton?.onClick.AddListener(() => FilterByCategory("challenge"));
        secretButton?.onClick.AddListener(() => FilterByCategory("secret"));
    }

    /// <summary>
    /// 显示成就面板
    /// </summary>
    public void Show()
    {
        if (achievementPanel != null)
            achievementPanel.SetActive(true);

        FilterByCategory(currentFilter);
        UpdateStats();
    }

    /// <summary>
    /// 隐藏成就面板
    /// </summary>
    public void Hide()
    {
        if (achievementPanel != null)
            achievementPanel.SetActive(false);
    }

    /// <summary>
    /// 按分类筛选成就
    /// </summary>
    public void FilterByCategory(string category)
    {
        currentFilter = category;

        // 清除旧列表
        foreach (var item in spawnedItems)
        {
            if (item != null) Destroy(item);
        }
        spawnedItems.Clear();

        if (AchievementSystem.Instance == null || contentParent == null || achievementItemPrefab == null) return;

        List<AchievementSystem.Achievement> achievements;
        if (string.IsNullOrEmpty(category))
            achievements = AchievementSystem.Instance.GetAllAchievements();
        else
            achievements = AchievementSystem.Instance.GetByCategory(category);

        // 先显示已解锁的，再显示未解锁的
        achievements.Sort((a, b) =>
        {
            if (a.isUnlocked != b.isUnlocked)
                return a.isUnlocked ? -1 : 1;
            return string.Compare(a.id, b.id);
        });

        foreach (var achievement in achievements)
        {
            var itemObj = Instantiate(achievementItemPrefab, contentParent);
            SetupAchievementItem(itemObj, achievement);
            spawnedItems.Add(itemObj);
        }
    }

    private void SetupAchievementItem(GameObject itemObj, AchievementSystem.Achievement achievement)
    {
        // 标题
        var titleText = itemObj.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
        if (titleText != null)
        {
            if (achievement.isHidden && !achievement.isUnlocked)
                titleText.text = "???";
            else
                titleText.text = achievement.title;
        }

        // 描述
        var descText = itemObj.transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
        if (descText != null)
        {
            if (achievement.isHidden && !achievement.isUnlocked)
                descText.text = LocalizationSystem.Instance != null ?
                    LocalizationSystem.Instance.Get("achievement_hidden", "隐藏成就") : "隐藏成就";
            else
                descText.text = achievement.description;
        }

        // 图标
        var iconImage = itemObj.transform.Find("Icon")?.GetComponent<Image>();
        if (iconImage != null && achievement.icon != null)
        {
            iconImage.sprite = achievement.icon;
            iconImage.color = achievement.isUnlocked ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.8f);
        }

        // 进度条
        var progressBar = itemObj.transform.Find("ProgressBar")?.GetComponent<Slider>();
        if (progressBar != null)
        {
            if (achievement.isUnlocked)
            {
                progressBar.value = 1f;
            }
            else
            {
                progressBar.value = achievement.progress / achievement.targetProgress;
            }
        }

        // 进度文本
        var progressText = itemObj.transform.Find("ProgressText")?.GetComponent<TextMeshProUGUI>();
        if (progressText != null)
        {
            if (achievement.isUnlocked)
                progressText.text = "✓";
            else
                progressText.text = $"{Mathf.RoundToInt(achievement.progress)}/{Mathf.RoundToInt(achievement.targetProgress)}";
        }

        // 已解锁状态视觉
        var bg = itemObj.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = achievement.isUnlocked ?
                new Color(0.15f, 0.25f, 0.15f, 0.9f) :
                new Color(0.15f, 0.15f, 0.15f, 0.7f);
        }
    }

    private void UpdateStats()
    {
        if (AchievementSystem.Instance == null) return;

        int unlocked = AchievementSystem.Instance.GetUnlockedCount();
        int total = AchievementSystem.Instance.GetTotalCount();

        if (totalText != null)
            totalText.text = $"{unlocked} / {total}";

        if (totalProgressBar != null)
            totalProgressBar.value = total > 0 ? (float)unlocked / total : 0;
    }
}
