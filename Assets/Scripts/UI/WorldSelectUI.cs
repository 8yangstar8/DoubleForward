using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 世界/章节选择UI - 横向滑动选择5个章节
/// 支持触摸滑动、锁定展示、进度显示
/// </summary>
public class WorldSelectUI : MonoBehaviour
{
    [Header("章节卡片")]
    [SerializeField] private RectTransform cardContainer;
    [SerializeField] private WorldCard[] worldCards;               // 5个章节卡片

    [Header("滑动")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private float snapSpeed = 10f;
    [SerializeField] private float swipeThreshold = 50f;

    [Header("信息面板")]
    [SerializeField] private TextMeshProUGUI chapterTitleText;
    [SerializeField] private TextMeshProUGUI chapterDescText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Image[] starIndicators;               // 总星星数
    [SerializeField] private Button enterButton;
    [SerializeField] private TextMeshProUGUI enterButtonText;
    [SerializeField] private GameObject lockOverlay;

    [Header("按钮")]
    [SerializeField] private Button leftArrow;
    [SerializeField] private Button rightArrow;
    [SerializeField] private Button backButton;

    [Header("导航点")]
    [SerializeField] private Image[] pageDots;
    [SerializeField] private Color dotActiveColor = Color.white;
    [SerializeField] private Color dotInactiveColor = new Color(1, 1, 1, 0.3f);

    private int currentWorldIndex = 0;
    private float targetScrollPos;
    private bool isSnapping;

    [System.Serializable]
    public class WorldCard
    {
        public RectTransform cardTransform;
        public Image worldArt;
        public TextMeshProUGUI titleText;
        public Image lockIcon;
        public TextMeshProUGUI starCountText;
        public int requiredStarsToUnlock;          // 解锁所需总星数
    }

    void Start()
    {
        SetupButtons();
        RefreshWorldCards();
        SelectWorld(GetFirstAvailableWorld());
    }

    private void SetupButtons()
    {
        if (leftArrow != null)
            leftArrow.onClick.AddListener(() => NavigateWorld(-1));
        if (rightArrow != null)
            rightArrow.onClick.AddListener(() => NavigateWorld(1));
        if (enterButton != null)
            enterButton.onClick.AddListener(EnterSelectedWorld);
        if (backButton != null)
            backButton.onClick.AddListener(GoBack);
    }

    /// <summary>
    /// 刷新所有章节卡片状态
    /// </summary>
    public void RefreshWorldCards()
    {
        if (worldCards == null) return;

        int totalStars = GetTotalStars();

        for (int i = 0; i < worldCards.Length; i++)
        {
            var card = worldCards[i];
            if (card.cardTransform == null) continue;

            bool unlocked = IsWorldUnlocked(i, totalStars);
            int worldStars = GetWorldStars(i);
            int maxStars = GetWorldMaxStars(i);

            // 锁定图标
            if (card.lockIcon != null)
                card.lockIcon.gameObject.SetActive(!unlocked);

            // 星星数
            if (card.starCountText != null)
            {
                card.starCountText.text = unlocked ? $"★ {worldStars}/{maxStars}" : "";
            }

            // 章节标题
            if (card.titleText != null)
            {
                string key = $"chapter_{i + 1}";
                string title = key;
                if (LocalizationSystem.Instance != null)
                    title = LocalizationSystem.Instance.Get(key, $"Chapter {i + 1}");
                card.titleText.text = title;
            }

            // 灰度锁定
            if (card.worldArt != null)
                card.worldArt.color = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
        }
    }

    /// <summary>
    /// 选择指定章节
    /// </summary>
    public void SelectWorld(int index)
    {
        index = Mathf.Clamp(index, 0, worldCards.Length - 1);
        currentWorldIndex = index;

        // 更新信息面板
        UpdateInfoPanel(index);

        // 更新导航点
        UpdatePageDots(index);

        // 更新箭头
        if (leftArrow != null) leftArrow.gameObject.SetActive(index > 0);
        if (rightArrow != null) rightArrow.gameObject.SetActive(index < worldCards.Length - 1);

        // 滑动动画
        SnapToWorld(index);
    }

    private void NavigateWorld(int direction)
    {
        int newIndex = Mathf.Clamp(currentWorldIndex + direction, 0, worldCards.Length - 1);
        if (newIndex != currentWorldIndex)
        {
            SelectWorld(newIndex);

            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play("ui_swipe");
        }
    }

    private void UpdateInfoPanel(int index)
    {
        bool unlocked = IsWorldUnlocked(index, GetTotalStars());

        // 章节标题
        if (chapterTitleText != null)
        {
            string key = $"chapter_{index + 1}";
            if (LocalizationSystem.Instance != null)
                chapterTitleText.text = LocalizationSystem.Instance.Get(key, $"Chapter {index + 1}");
            else
                chapterTitleText.text = $"Chapter {index + 1}";
        }

        // 章节描述
        if (chapterDescText != null)
        {
            string key = $"chapter_{index + 1}_desc";
            if (LocalizationSystem.Instance != null)
                chapterDescText.text = LocalizationSystem.Instance.Get(key, "");
            else
                chapterDescText.text = "";
        }

        // 进度
        if (progressText != null)
        {
            if (unlocked)
            {
                int worldStars = GetWorldStars(index);
                int maxStars = GetWorldMaxStars(index);
                progressText.text = $"★ {worldStars}/{maxStars}";
            }
            else
            {
                int needed = worldCards[index].requiredStarsToUnlock;
                int total = GetTotalStars();
                progressText.text = $"★ {total}/{needed}";
            }
        }

        // 锁定遮罩
        if (lockOverlay != null)
            lockOverlay.SetActive(!unlocked);

        // 进入按钮
        if (enterButton != null)
            enterButton.interactable = unlocked;

        if (enterButtonText != null)
        {
            if (unlocked)
            {
                string key = "level_play";
                if (LocalizationSystem.Instance != null)
                    enterButtonText.text = LocalizationSystem.Instance.Get(key, "Play");
                else
                    enterButtonText.text = "Play";
            }
            else
            {
                string key = "level_locked";
                if (LocalizationSystem.Instance != null)
                    enterButtonText.text = LocalizationSystem.Instance.Get(key, "Locked");
                else
                    enterButtonText.text = "Locked";
            }
        }
    }

    private void UpdatePageDots(int activeIndex)
    {
        if (pageDots == null) return;
        for (int i = 0; i < pageDots.Length; i++)
        {
            if (pageDots[i] != null)
                pageDots[i].color = i == activeIndex ? dotActiveColor : dotInactiveColor;
        }
    }

    private void SnapToWorld(int index)
    {
        if (worldCards.Length <= 1) return;

        targetScrollPos = (float)index / (worldCards.Length - 1);
        isSnapping = true;
    }

    void Update()
    {
        if (isSnapping && scrollRect != null)
        {
            float current = scrollRect.horizontalNormalizedPosition;
            float newPos = Mathf.Lerp(current, targetScrollPos, Time.unscaledDeltaTime * snapSpeed);
            scrollRect.horizontalNormalizedPosition = newPos;

            if (Mathf.Abs(newPos - targetScrollPos) < 0.001f)
            {
                scrollRect.horizontalNormalizedPosition = targetScrollPos;
                isSnapping = false;
            }
        }

        // 缩放当前选中卡片
        UpdateCardScales();
    }

    private void UpdateCardScales()
    {
        for (int i = 0; i < worldCards.Length; i++)
        {
            if (worldCards[i].cardTransform == null) continue;

            float targetScale = i == currentWorldIndex ? 1f : 0.85f;
            float current = worldCards[i].cardTransform.localScale.x;
            float newScale = Mathf.Lerp(current, targetScale, Time.unscaledDeltaTime * 8f);
            worldCards[i].cardTransform.localScale = Vector3.one * newScale;
        }
    }

    private void EnterSelectedWorld()
    {
        if (!IsWorldUnlocked(currentWorldIndex, GetTotalStars())) return;

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_confirm");

        // 进入关卡选择
        GameFlowManager.Instance?.LoadLevel(currentWorldIndex + 1, 1);
    }

    private void GoBack()
    {
        GameFlowManager.Instance?.GoToMainMenu();
    }

    // ============ 数据查询 ============

    private bool IsWorldUnlocked(int worldIndex, int totalStars)
    {
        if (worldIndex == 0) return true; // 第一章始终解锁

        if (worldIndex < worldCards.Length)
            return totalStars >= worldCards[worldIndex].requiredStarsToUnlock;

        return false;
    }

    private int GetTotalStars()
    {
        if (SaveSystem.Instance == null) return 0;

        int total = 0;
        var data = SaveSystem.Instance.Data;
        if (data.levelStars != null)
        {
            foreach (var s in data.levelStars)
                total += s;
        }
        return total;
    }

    private int GetWorldStars(int worldIndex)
    {
        if (SaveSystem.Instance == null) return 0;

        int[] levelsPerChapter = { 3, 4, 4, 5, 4 };
        int startLevel = 0;
        for (int i = 0; i < worldIndex; i++)
            startLevel += levelsPerChapter[i];

        int stars = 0;
        var data = SaveSystem.Instance.Data;
        if (data.levelStars != null)
        {
            int count = levelsPerChapter[worldIndex];
            for (int i = startLevel; i < startLevel + count && i < data.levelStars.Length; i++)
                stars += data.levelStars[i];
        }
        return stars;
    }

    private int GetWorldMaxStars(int worldIndex)
    {
        int[] levelsPerChapter = { 3, 4, 4, 5, 4 };
        if (worldIndex < levelsPerChapter.Length)
            return levelsPerChapter[worldIndex] * 3;
        return 0;
    }

    private int GetFirstAvailableWorld()
    {
        int totalStars = GetTotalStars();
        int lastUnlocked = 0;

        for (int i = 0; i < worldCards.Length; i++)
        {
            if (IsWorldUnlocked(i, totalStars))
                lastUnlocked = i;
        }
        return lastUnlocked;
    }
}
