using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 故事回顾UI - 故事画廊界面
/// 按章节分类展示已解锁的故事片段，支持重新播放
/// 未解锁的显示为锁定状态并带有解锁提示
/// </summary>
public class StoryRecapUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject storyRecapPanel;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("标题")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI completionText;

    [Header("章节选项卡")]
    [SerializeField] private Transform chapterTabParent;
    [SerializeField] private GameObject chapterTabPrefab;
    [SerializeField] private Color tabActiveColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color tabInactiveColor = new Color(0.5f, 0.5f, 0.5f);

    [Header("故事网格")]
    [SerializeField] private Transform storyGridParent;
    [SerializeField] private GameObject storyCardPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Header("详情预览")]
    [SerializeField] private GameObject previewPanel;
    [SerializeField] private Image previewThumbnail;
    [SerializeField] private TextMeshProUGUI previewTitleText;
    [SerializeField] private TextMeshProUGUI previewCategoryText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button closePreviewButton;

    [Header("按钮")]
    [SerializeField] private Button backButton;

    [Header("锁定显示")]
    [SerializeField] private Sprite lockedThumbnail;
    [SerializeField] private Color lockedCardColor = new Color(0.3f, 0.3f, 0.3f);
    [SerializeField] private Color unlockedCardColor = Color.white;

    [Header("设置")]
    [SerializeField] private float cardRevealInterval = 0.05f;

    // 缓存
    private int currentChapter = 0;     // 0=全部
    private List<GameObject> spawnedTabs = new List<GameObject>();
    private List<GameObject> spawnedCards = new List<GameObject>();
    private string selectedStoryId;

    void Start()
    {
        if (backButton != null) backButton.onClick.AddListener(Hide);
        if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
        if (closePreviewButton != null) closePreviewButton.onClick.AddListener(ClosePreview);

        if (storyRecapPanel != null) storyRecapPanel.SetActive(false);
        if (previewPanel != null) previewPanel.SetActive(false);
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 显示故事回顾画廊
    /// </summary>
    public void Show()
    {
        if (storyRecapPanel != null) storyRecapPanel.SetActive(true);

        // 标题
        if (titleText != null)
            titleText.text = GetLocalizedText("story_gallery", "Story Gallery");

        // 完成度
        UpdateCompletionText();

        // 创建章节选项卡
        CreateChapterTabs();

        // 显示所有故事
        ShowChapter(0);

        StartCoroutine(FadeIn());
    }

    /// <summary>
    /// 隐藏画廊
    /// </summary>
    public void Hide()
    {
        StartCoroutine(FadeOutAndHide());
    }

    // ==================== 章节选项卡 ====================

    private void CreateChapterTabs()
    {
        ClearList(spawnedTabs);

        if (chapterTabPrefab == null || chapterTabParent == null) return;

        // "全部"选项卡
        SpawnTab(GetLocalizedText("story_all", "All"), 0);

        // 各章节
        string[] chapterNames = {
            "Forest", "Crystal", "Abyss", "Sky", "Twilight"
        };

        for (int i = 0; i < chapterNames.Length; i++)
        {
            string name = GetLocalizedText($"chapter_{i + 1}_short", chapterNames[i]);
            SpawnTab(name, i + 1);
        }

        // 特殊分类
        SpawnTab(GetLocalizedText("story_endings", "Endings"), -1);
        SpawnTab(GetLocalizedText("story_lore", "Lore"), -2);
    }

    private void SpawnTab(string label, int chapter)
    {
        var tabObj = Instantiate(chapterTabPrefab, chapterTabParent);
        spawnedTabs.Add(tabObj);

        var text = tabObj.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null) text.text = label;

        var button = tabObj.GetComponent<Button>();
        int capturedChapter = chapter;
        if (button != null)
        {
            button.onClick.AddListener(() => ShowChapter(capturedChapter));
        }

        // 初始颜色
        var image = tabObj.GetComponent<Image>();
        if (image != null)
            image.color = chapter == currentChapter ? tabActiveColor : tabInactiveColor;
    }

    private void ShowChapter(int chapter)
    {
        currentChapter = chapter;

        // 更新选项卡高亮
        UpdateTabHighlights();

        // 获取条目
        var entries = GetFilteredEntries(chapter);

        // 创建卡片
        StartCoroutine(PopulateCards(entries));
    }

    private List<(StoryRecapSystem.StoryEntry entry, bool unlocked)> GetFilteredEntries(int chapter)
    {
        if (StoryRecapSystem.Instance == null)
            return new List<(StoryRecapSystem.StoryEntry, bool)>();

        var all = StoryRecapSystem.Instance.GetAllEntriesWithStatus();

        if (chapter == 0) return all; // 全部

        if (chapter == -1) // 结局
            return all.FindAll(e => e.entry.category == StoryRecapSystem.StoryEntry.StoryCategory.Ending);

        if (chapter == -2) // 传说
            return all.FindAll(e => e.entry.category == StoryRecapSystem.StoryEntry.StoryCategory.SecretLore);

        return all.FindAll(e => e.entry.chapter == chapter);
    }

    // ==================== 卡片 ====================

    private IEnumerator PopulateCards(List<(StoryRecapSystem.StoryEntry entry, bool unlocked)> entries)
    {
        ClearList(spawnedCards);

        if (storyCardPrefab == null || storyGridParent == null) yield break;

        foreach (var (entry, unlocked) in entries)
        {
            var cardObj = Instantiate(storyCardPrefab, storyGridParent);
            spawnedCards.Add(cardObj);

            SetupCard(cardObj, entry, unlocked);

            // 逐个显示动画
            cardObj.transform.localScale = Vector3.one * 0.8f;
            StartCoroutine(ScaleCardIn(cardObj.transform));

            yield return new WaitForSecondsRealtime(cardRevealInterval);
        }

        // 滚动到顶部
        if (scrollRect != null)
            scrollRect.normalizedPosition = new Vector2(0, 1);
    }

    private void SetupCard(GameObject card, StoryRecapSystem.StoryEntry entry, bool unlocked)
    {
        // 缩略图
        var thumbnail = card.GetComponentInChildren<Image>();
        if (thumbnail != null)
        {
            if (unlocked && entry.thumbnail != null)
                thumbnail.sprite = entry.thumbnail;
            else if (lockedThumbnail != null)
                thumbnail.sprite = lockedThumbnail;

            thumbnail.color = unlocked ? unlockedCardColor : lockedCardColor;
        }

        // 标题
        var texts = card.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length > 0)
        {
            if (unlocked)
                texts[0].text = GetLocalizedText(entry.titleKey, entry.titleFallback);
            else
                texts[0].text = "???";
        }

        // 分类标签
        if (texts.Length > 1)
        {
            texts[1].text = GetCategoryName(entry.category);
            texts[1].color = unlocked ? GetCategoryColor(entry.category) : lockedCardColor;
        }

        // 点击
        var button = card.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = unlocked;
            string capturedId = entry.storyId;
            StoryRecapSystem.StoryEntry capturedEntry = entry;
            button.onClick.AddListener(() => OnCardClicked(capturedId, capturedEntry));
        }
    }

    private void OnCardClicked(string storyId, StoryRecapSystem.StoryEntry entry)
    {
        selectedStoryId = storyId;

        if (previewPanel != null) previewPanel.SetActive(true);

        if (previewTitleText != null)
            previewTitleText.text = GetLocalizedText(entry.titleKey, entry.titleFallback);

        if (previewCategoryText != null)
        {
            previewCategoryText.text = GetCategoryName(entry.category);
            previewCategoryText.color = GetCategoryColor(entry.category);
        }

        if (previewThumbnail != null && entry.thumbnail != null)
            previewThumbnail.sprite = entry.thumbnail;

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    private void OnPlayClicked()
    {
        if (string.IsNullOrEmpty(selectedStoryId)) return;

        if (StoryRecapSystem.Instance != null)
        {
            StoryRecapSystem.Instance.PlayStory(selectedStoryId);
        }

        ClosePreview();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();
    }

    private void ClosePreview()
    {
        if (previewPanel != null) previewPanel.SetActive(false);
        selectedStoryId = null;
    }

    // ==================== 辅助方法 ====================

    private void UpdateCompletionText()
    {
        if (completionText == null || StoryRecapSystem.Instance == null) return;

        int unlocked = StoryRecapSystem.Instance.UnlockedCount;
        int total = StoryRecapSystem.Instance.TotalEntries;
        float percent = StoryRecapSystem.Instance.CompletionPercent;

        completionText.text = $"{unlocked}/{total} ({percent:F0}%)";
    }

    private void UpdateTabHighlights()
    {
        // 简化：通过索引对应（0=All, 1-5=Chapters, 6=Endings, 7=Lore）
        int[] chapters = { 0, 1, 2, 3, 4, 5, -1, -2 };

        for (int i = 0; i < spawnedTabs.Count && i < chapters.Length; i++)
        {
            var img = spawnedTabs[i].GetComponent<Image>();
            if (img != null)
                img.color = chapters[i] == currentChapter ? tabActiveColor : tabInactiveColor;
        }
    }

    private string GetCategoryName(StoryRecapSystem.StoryEntry.StoryCategory category)
    {
        return category switch
        {
            StoryRecapSystem.StoryEntry.StoryCategory.ChapterIntro => GetLocalizedText("cat_intro", "Intro"),
            StoryRecapSystem.StoryEntry.StoryCategory.ChapterOutro => GetLocalizedText("cat_outro", "Outro"),
            StoryRecapSystem.StoryEntry.StoryCategory.BossIntro => GetLocalizedText("cat_boss", "Boss"),
            StoryRecapSystem.StoryEntry.StoryCategory.BossDefeat => GetLocalizedText("cat_boss_defeat", "Victory"),
            StoryRecapSystem.StoryEntry.StoryCategory.BondDialogue => GetLocalizedText("cat_bond", "Bond"),
            StoryRecapSystem.StoryEntry.StoryCategory.SecretLore => GetLocalizedText("cat_lore", "Lore"),
            StoryRecapSystem.StoryEntry.StoryCategory.Ending => GetLocalizedText("cat_ending", "Ending"),
            StoryRecapSystem.StoryEntry.StoryCategory.SpecialEvent => GetLocalizedText("cat_event", "Event"),
            _ => "Unknown"
        };
    }

    private Color GetCategoryColor(StoryRecapSystem.StoryEntry.StoryCategory category)
    {
        return category switch
        {
            StoryRecapSystem.StoryEntry.StoryCategory.ChapterIntro => new Color(0.4f, 0.8f, 0.4f),
            StoryRecapSystem.StoryEntry.StoryCategory.ChapterOutro => new Color(0.4f, 0.6f, 1f),
            StoryRecapSystem.StoryEntry.StoryCategory.BossIntro => new Color(1f, 0.4f, 0.3f),
            StoryRecapSystem.StoryEntry.StoryCategory.BossDefeat => new Color(1f, 0.7f, 0.2f),
            StoryRecapSystem.StoryEntry.StoryCategory.BondDialogue => new Color(1f, 0.5f, 0.7f),
            StoryRecapSystem.StoryEntry.StoryCategory.SecretLore => new Color(0.7f, 0.5f, 1f),
            StoryRecapSystem.StoryEntry.StoryCategory.Ending => new Color(1f, 0.85f, 0.2f),
            StoryRecapSystem.StoryEntry.StoryCategory.SpecialEvent => Color.white,
            _ => Color.gray
        };
    }

    private IEnumerator ScaleCardIn(Transform card)
    {
        float t = 0;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            float scale = Mathf.Lerp(0.8f, 1f, t / 0.2f);
            card.localScale = Vector3.one * scale;
            yield return null;
        }
        card.localScale = Vector3.one;
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null) yield break;
        canvasGroup.alpha = 0;
        float t = 0;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = t / 0.3f;
            yield return null;
        }
        canvasGroup.alpha = 1;
    }

    private IEnumerator FadeOutAndHide()
    {
        if (canvasGroup != null)
        {
            float t = 0;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - t / 0.3f;
                yield return null;
            }
        }

        ClearList(spawnedTabs);
        ClearList(spawnedCards);
        if (storyRecapPanel != null) storyRecapPanel.SetActive(false);
    }

    private void ClearList(List<GameObject> list)
    {
        foreach (var obj in list)
        {
            if (obj != null) Destroy(obj);
        }
        list.Clear();
    }

    private string GetLocalizedText(string key, string fallback)
    {
        if (LocalizationSystem.Instance != null)
        {
            string localized = LocalizationSystem.Instance.GetText(key);
            if (localized != key) return localized;
        }
        return fallback;
    }
}
