using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 世界地图UI - 展示5个世界的章节选择界面
/// 带解锁进度、世界预览、星级统计
/// 与LevelSelectUI配合：WorldMapUI选世界 → LevelSelectUI选关卡
/// </summary>
public class WorldMapUI : MonoBehaviour
{
    [Header("世界节点")]
    [SerializeField] private WorldNode[] worldNodes;

    [Header("详情面板")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private TextMeshProUGUI worldNameText;
    [SerializeField] private TextMeshProUGUI worldDescText;
    [SerializeField] private TextMeshProUGUI starCountText;
    [SerializeField] private Image worldPreviewImage;
    [SerializeField] private Button enterButton;
    [SerializeField] private TextMeshProUGUI enterButtonText;

    [Header("导航")]
    [SerializeField] private Button backButton;

    [Header("路径线")]
    [SerializeField] private LineRenderer pathLine;
    [SerializeField] private Color unlockedPathColor = Color.white;
    [SerializeField] private Color lockedPathColor = new Color(0.3f, 0.3f, 0.3f);

    [Header("动画")]
    [SerializeField] private float nodeScaleSpeed = 3f;
    [SerializeField] private float selectedScale = 1.2f;

    [System.Serializable]
    public class WorldNode
    {
        public int chapter;
        public string worldNameKey;
        public string descriptionKey;
        public Button nodeButton;
        public Image nodeImage;
        public Image lockIcon;
        public TextMeshProUGUI starText;
        public Sprite previewSprite;
        public Color worldColor = Color.white;

        [Header("世界信息")]
        public int totalLevels = 4;
        public string[] levelSceneNames;
    }

    private int selectedWorld = -1;
    private bool isAnimating;

    void Start()
    {
        // 绑定世界节点按钮
        for (int i = 0; i < worldNodes.Length; i++)
        {
            int chapter = worldNodes[i].chapter;
            worldNodes[i].nodeButton?.onClick.AddListener(() => SelectWorld(chapter));
        }

        enterButton?.onClick.AddListener(EnterSelectedWorld);
        backButton?.onClick.AddListener(GoBack);

        if (detailPanel != null)
            detailPanel.SetActive(false);

        RefreshAllNodes();
    }

    // ==================== 节点刷新 ====================

    private void RefreshAllNodes()
    {
        foreach (var node in worldNodes)
        {
            bool unlocked = IsWorldUnlocked(node.chapter);
            int stars = GetWorldStars(node.chapter, node.totalLevels);
            int maxStars = node.totalLevels * 3;

            // 锁状态
            if (node.lockIcon != null)
                node.lockIcon.gameObject.SetActive(!unlocked);

            if (node.nodeButton != null)
                node.nodeButton.interactable = unlocked;

            // 颜色
            if (node.nodeImage != null)
            {
                if (unlocked)
                    node.nodeImage.color = node.worldColor;
                else
                    node.nodeImage.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
            }

            // 星级
            if (node.starText != null)
            {
                if (unlocked)
                    node.starText.text = $"{stars}/{maxStars}";
                else
                    node.starText.text = "";
            }
        }

        UpdatePathLine();
    }

    // ==================== 世界选择 ====================

    private void SelectWorld(int chapter)
    {
        if (isAnimating) return;

        selectedWorld = chapter;
        var node = GetNode(chapter);
        if (node == null) return;

        // 显示详情
        if (detailPanel != null)
            detailPanel.SetActive(true);

        string worldName = LocalizationSystem.Instance != null
            ? LocalizationSystem.Instance.GetText(node.worldNameKey)
            : node.worldNameKey;

        string worldDesc = LocalizationSystem.Instance != null
            ? LocalizationSystem.Instance.GetText(node.descriptionKey)
            : node.descriptionKey;

        if (worldNameText != null) worldNameText.text = worldName;
        if (worldDescText != null) worldDescText.text = worldDesc;

        if (worldPreviewImage != null && node.previewSprite != null)
        {
            worldPreviewImage.sprite = node.previewSprite;
            worldPreviewImage.color = Color.white;
        }

        int stars = GetWorldStars(chapter, node.totalLevels);
        int maxStars = node.totalLevels * 3;
        if (starCountText != null)
            starCountText.text = $"{stars} / {maxStars}";

        if (enterButton != null)
            enterButton.interactable = IsWorldUnlocked(chapter);

        // 缩放动画
        StartCoroutine(AnimateSelection(chapter));

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayClick();
    }

    private void EnterSelectedWorld()
    {
        if (selectedWorld <= 0) return;

        // 切换到关卡选择
        if (GameFlowManager.Instance != null)
        {
            // 通过LevelSelectUI加载该章节的关卡列表
        }

        // 直接加载关卡选择场景或切换UI
        EventBus.Publish(new WorldSelectedEvent
        {
            chapter = selectedWorld
        });

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();
    }

    private void GoBack()
    {
        GameManager.Instance?.ReturnToMainMenu();
    }

    // ==================== 动画 ====================

    private IEnumerator AnimateSelection(int chapter)
    {
        isAnimating = true;

        // 缩回所有节点
        foreach (var node in worldNodes)
        {
            if (node.nodeButton == null) continue;
            StartCoroutine(ScaleNode(node.nodeButton.transform,
                node.chapter == chapter ? selectedScale : 1f));
        }

        yield return new WaitForSeconds(0.3f);
        isAnimating = false;
    }

    private IEnumerator ScaleNode(Transform nodeTransform, float targetScale)
    {
        Vector3 target = Vector3.one * targetScale;
        float elapsed = 0;
        float duration = 0.2f;
        Vector3 start = nodeTransform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            nodeTransform.localScale = Vector3.Lerp(start, target, EaseOutBack(t));
            yield return null;
        }

        nodeTransform.localScale = target;
    }

    // ==================== 路径线 ====================

    private void UpdatePathLine()
    {
        if (pathLine == null || worldNodes.Length < 2) return;

        pathLine.positionCount = worldNodes.Length;
        for (int i = 0; i < worldNodes.Length; i++)
        {
            Vector3 pos = worldNodes[i].nodeButton != null
                ? worldNodes[i].nodeButton.transform.position
                : Vector3.zero;
            pathLine.SetPosition(i, pos);
        }

        // 根据解锁进度设置颜色
        int unlockedCount = 0;
        foreach (var node in worldNodes)
        {
            if (IsWorldUnlocked(node.chapter))
                unlockedCount++;
        }

        Gradient gradient = new Gradient();
        float unlockRatio = (float)unlockedCount / worldNodes.Length;
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(unlockedPathColor, 0f),
                new GradientColorKey(unlockedPathColor, unlockRatio),
                new GradientColorKey(lockedPathColor, unlockRatio + 0.01f),
                new GradientColorKey(lockedPathColor, 1f)
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );

        pathLine.colorGradient = gradient;
    }

    // ==================== 辅助方法 ====================

    private bool IsWorldUnlocked(int chapter)
    {
        if (chapter == 1) return true;

        // 前一章的最后一关需要通关
        int[] levelsPerChapter = { 4, 4, 4, 4, 4 };
        int prevChapter = chapter - 1;
        if (prevChapter < 1 || prevChapter > levelsPerChapter.Length) return false;

        int lastLevel = levelsPerChapter[prevChapter - 1];
        return SaveSystem.Instance?.IsLevelCompleted(prevChapter, lastLevel) ?? false;
    }

    private int GetWorldStars(int chapter, int totalLevels)
    {
        int stars = 0;
        if (ScoreManager.Instance == null) return stars;

        for (int level = 1; level <= totalLevels; level++)
        {
            int highScore = ScoreManager.Instance.GetHighScore(chapter, level);
            // 简化星级计算
            if (highScore > 0) stars++;
            if (highScore >= 1000) stars++;
            if (highScore >= 3000) stars++;
        }

        return stars;
    }

    private WorldNode GetNode(int chapter)
    {
        foreach (var node in worldNodes)
        {
            if (node.chapter == chapter)
                return node;
        }
        return null;
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);
    }
}

// WorldSelectedEvent 已迁移到 EventBus.cs（Core程序集，支持跨程序集使用）
