using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LevelSelectUI : MonoBehaviour
{
    [Header("Chapter Tabs")]
    [SerializeField] private List<Button> chapterButtons;
    [SerializeField] private Color activeTabColor = new Color(0.3f, 0.7f, 1f);
    [SerializeField] private Color inactiveTabColor = Color.gray;
    [SerializeField] private Color lockedTabColor = new Color(0.3f, 0.3f, 0.3f);

    [Header("Level Grid")]
    [SerializeField] private Transform levelGridContainer;
    [SerializeField] private GameObject levelCardPrefab;

    [Header("Info Panel")]
    [SerializeField] private TextMeshProUGUI levelNameText;
    [SerializeField] private TextMeshProUGUI levelDescText;
    [SerializeField] private TextMeshProUGUI bestTimeText;
    [SerializeField] private Image[] starImages;
    [SerializeField] private Button playButton;

    [Header("Navigation")]
    [SerializeField] private Button backButton;

    [Header("Data")]
    [SerializeField] private LevelProgressTracker progressTracker;

    private int selectedChapter = 1;
    private LevelData selectedLevel;

    void Start()
    {
        for (int i = 0; i < chapterButtons.Count; i++)
        {
            int chapter = i + 1;
            chapterButtons[i]?.onClick.AddListener(() => SelectChapter(chapter));
        }

        playButton?.onClick.AddListener(PlaySelectedLevel);
        backButton?.onClick.AddListener(() => GameManager.Instance?.ReturnToMainMenu());

        SelectChapter(1);
    }

    private void SelectChapter(int chapter)
    {
        selectedChapter = chapter;
        UpdateChapterTabs();
        PopulateLevelGrid();
    }

    private void UpdateChapterTabs()
    {
        for (int i = 0; i < chapterButtons.Count; i++)
        {
            int ch = i + 1;
            var img = chapterButtons[i]?.GetComponent<Image>();
            if (img == null) continue;

            bool isUnlocked = IsChapterUnlocked(ch);
            bool isSelected = ch == selectedChapter;

            if (isSelected)
                img.color = activeTabColor;
            else if (isUnlocked)
                img.color = inactiveTabColor;
            else
                img.color = lockedTabColor;

            chapterButtons[i].interactable = isUnlocked;
        }
    }

    private void PopulateLevelGrid()
    {
        // 清除旧卡片
        if (levelGridContainer != null)
        {
            foreach (Transform child in levelGridContainer)
                Destroy(child.gameObject);
        }

        if (progressTracker == null) return;

        var levels = progressTracker.GetChapterLevels(selectedChapter);

        foreach (var levelData in levels)
        {
            if (levelCardPrefab == null || levelGridContainer == null) continue;

            var card = Instantiate(levelCardPrefab, levelGridContainer);
            SetupLevelCard(card, levelData);
        }
    }

    private void SetupLevelCard(GameObject card, LevelData data)
    {
        bool unlocked = progressTracker.IsLevelUnlocked(data.chapter, data.levelIndex);
        bool completed = SaveSystem.Instance?.IsLevelCompleted(data.chapter, data.levelIndex) ?? false;

        // 关卡编号
        var numberText = card.GetComponentInChildren<TextMeshProUGUI>();
        if (numberText != null)
            numberText.text = data.levelIndex.ToString();

        // 颜色/状态
        var cardImage = card.GetComponent<Image>();
        if (cardImage != null)
        {
            if (completed)
                cardImage.color = new Color(0.4f, 0.9f, 0.4f);
            else if (unlocked)
                cardImage.color = Color.white;
            else
                cardImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        // 按钮交互
        var button = card.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = unlocked;
            LevelData capturedData = data;
            button.onClick.AddListener(() => SelectLevel(capturedData));
        }
    }

    private void SelectLevel(LevelData data)
    {
        selectedLevel = data;

        if (levelNameText != null)
            levelNameText.text = data.DisplayName;
        if (levelDescText != null)
            levelDescText.text = data.description ?? "";

        if (playButton != null)
            playButton.interactable = true;
    }

    private void PlaySelectedLevel()
    {
        if (selectedLevel == null) return;
        GameManager.Instance?.LoadLevel(selectedLevel.chapter, selectedLevel.levelIndex);
    }

    private bool IsChapterUnlocked(int chapter)
    {
        if (chapter == 1) return true;
        if (progressTracker == null) return false;

        // 前一章最后一关通过才解锁
        int[] lastLevels = { 3, 4, 4, 5, 4 };
        int prevChapter = chapter - 1;
        int prevLastLevel = prevChapter <= lastLevels.Length ? lastLevels[prevChapter - 1] : 1;
        return SaveSystem.Instance?.IsLevelCompleted(prevChapter, prevLastLevel) ?? false;
    }
}
