using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// 世界地图节点 - 表示地图上的一个关卡
/// 支持锁定/解锁、星级显示、路径连线
/// 与WorldSelectUI配合使用
/// </summary>
public class WorldMapNode : MonoBehaviour, IPointerClickHandler
{
    [Header("关卡数据")]
    [SerializeField] private int chapter;
    [SerializeField] private int level;
    [SerializeField] private string levelSceneName;

    [Header("UI元素")]
    [SerializeField] private Image nodeIcon;
    [SerializeField] private Image lockIcon;
    [SerializeField] private Image[] starImages;
    [SerializeField] private TextMeshProUGUI levelNumberText;
    [SerializeField] private GameObject selectedIndicator;
    [SerializeField] private GameObject currentLevelIndicator;

    [Header("状态图标")]
    [SerializeField] private Sprite lockedSprite;
    [SerializeField] private Sprite unlockedSprite;
    [SerializeField] private Sprite completedSprite;
    [SerializeField] private Sprite starFilled;
    [SerializeField] private Sprite starEmpty;

    [Header("连接")]
    [SerializeField] private WorldMapNode[] nextNodes;
    [SerializeField] private LineRenderer pathRenderer;

    [Header("颜色")]
    [SerializeField] private Color lockedColor = new Color(0.4f, 0.4f, 0.4f);
    [SerializeField] private Color unlockedColor = Color.white;
    [SerializeField] private Color completedColor = new Color(0.3f, 1f, 0.5f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.8f, 0f);

    public int Chapter => chapter;
    public int Level => level;
    public string SceneName => levelSceneName;

    public enum NodeState { Locked, Unlocked, Completed }
    public NodeState State { get; private set; } = NodeState.Locked;
    public int Stars { get; private set; }

    public event System.Action<WorldMapNode> OnNodeClicked;

    void Start()
    {
        RefreshState();
        UpdateVisual();
        DrawPaths();
    }

    /// <summary>
    /// 从存档读取状态
    /// </summary>
    public void RefreshState()
    {
        if (SaveSystem.Instance == null)
        {
            State = NodeState.Locked;
            Stars = 0;
            return;
        }

        if (SaveSystem.Instance.IsLevelCompleted(chapter, level))
        {
            State = NodeState.Completed;
            Stars = SaveSystem.Instance.GetLevelStars(chapter, level);
        }
        else if (IsUnlocked())
        {
            State = NodeState.Unlocked;
            Stars = 0;
        }
        else
        {
            State = NodeState.Locked;
            Stars = 0;
        }

        UpdateVisual();
    }

    /// <summary>
    /// 检查是否已解锁（前一关完成或第一关）
    /// </summary>
    private bool IsUnlocked()
    {
        // 第一章第一关总是解锁的
        if (chapter == 1 && level == 1) return true;

        if (SaveSystem.Instance == null) return false;

        // 前一关完成即解锁
        if (level > 1)
            return SaveSystem.Instance.IsLevelCompleted(chapter, level - 1);

        // 本章第一关：前一章最后一关完成
        // 简化：前一章任意关卡完成即可
        int prevChapter = chapter - 1;
        if (prevChapter >= 1)
        {
            // 检查前一章最后一关
            int[] levelsPerChapter = { 3, 4, 4, 5, 4 };
            if (prevChapter - 1 < levelsPerChapter.Length)
            {
                int lastLevel = levelsPerChapter[prevChapter - 1];
                return SaveSystem.Instance.IsLevelCompleted(prevChapter, lastLevel);
            }
        }

        return false;
    }

    /// <summary>
    /// 更新节点视觉
    /// </summary>
    public void UpdateVisual()
    {
        // 节点图标
        if (nodeIcon != null)
        {
            switch (State)
            {
                case NodeState.Locked:
                    if (lockedSprite != null) nodeIcon.sprite = lockedSprite;
                    nodeIcon.color = lockedColor;
                    break;
                case NodeState.Unlocked:
                    if (unlockedSprite != null) nodeIcon.sprite = unlockedSprite;
                    nodeIcon.color = unlockedColor;
                    break;
                case NodeState.Completed:
                    if (completedSprite != null) nodeIcon.sprite = completedSprite;
                    nodeIcon.color = completedColor;
                    break;
            }
        }

        // 锁图标
        if (lockIcon != null)
            lockIcon.gameObject.SetActive(State == NodeState.Locked);

        // 关卡号
        if (levelNumberText != null)
            levelNumberText.text = $"{chapter}-{level}";

        // 星级
        if (starImages != null)
        {
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] != null)
                {
                    if (State == NodeState.Locked)
                    {
                        starImages[i].gameObject.SetActive(false);
                    }
                    else
                    {
                        starImages[i].gameObject.SetActive(true);
                        starImages[i].sprite = i < Stars ? starFilled : starEmpty;
                        starImages[i].color = i < Stars ? Color.yellow : new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    }
                }
            }
        }

        // 当前关卡指示器
        if (currentLevelIndicator != null)
        {
            bool isCurrent = false;
            if (SaveSystem.Instance != null)
            {
                isCurrent = SaveSystem.Instance.Data.lastChapter == chapter &&
                           SaveSystem.Instance.Data.lastLevel == level;
            }
            currentLevelIndicator.SetActive(isCurrent);
        }
    }

    /// <summary>
    /// 设置选中状态
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (selectedIndicator != null)
            selectedIndicator.SetActive(selected);

        if (selected && nodeIcon != null)
            nodeIcon.color = selectedColor;
        else
            UpdateVisual();
    }

    /// <summary>
    /// 画到下一个节点的路径连线
    /// </summary>
    private void DrawPaths()
    {
        if (nextNodes == null || pathRenderer == null) return;

        // 简单实现：只连到第一个下一节点
        if (nextNodes.Length > 0 && nextNodes[0] != null)
        {
            pathRenderer.positionCount = 2;
            pathRenderer.SetPosition(0, transform.position);
            pathRenderer.SetPosition(1, nextNodes[0].transform.position);

            // 已完成的路径变亮
            pathRenderer.startColor = State == NodeState.Completed ? completedColor : lockedColor;
            pathRenderer.endColor = pathRenderer.startColor;
        }
    }

    // ============ 交互 ============

    public void OnPointerClick(PointerEventData eventData)
    {
        if (State == NodeState.Locked) return;

        OnNodeClicked?.Invoke(this);
    }
}
