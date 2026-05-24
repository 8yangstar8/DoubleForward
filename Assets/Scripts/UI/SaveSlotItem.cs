using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 单个存档槽位UI项 - 显示存档详细信息
/// </summary>
public class SaveSlotItem : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private TextMeshProUGUI slotNameText;
    [SerializeField] private TextMeshProUGUI chapterText;
    [SerializeField] private TextMeshProUGUI playTimeText;
    [SerializeField] private TextMeshProUGUI completionText;
    [SerializeField] private TextMeshProUGUI dateText;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private GameObject dataState;
    [SerializeField] private Button playButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private TextMeshProUGUI playButtonText;
    [SerializeField] private Image slotBackground;
    [SerializeField] private Slider completionBar;

    [Header("颜色")]
    [SerializeField] private Color normalColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);
    [SerializeField] private Color activeColor = new Color(0.2f, 0.2f, 0.35f, 0.95f);

    public event System.Action OnPlayClicked;
    public event System.Action OnDeleteClicked;

    void Start()
    {
        playButton?.onClick.AddListener(() => OnPlayClicked?.Invoke());
        deleteButton?.onClick.AddListener(() => OnDeleteClicked?.Invoke());
    }

    /// <summary>
    /// 设置存档槽位显示内容
    /// </summary>
    public void Setup(SaveSystem.SaveSlotInfo info, bool isActive)
    {
        if (info.isEmpty)
        {
            // 空存档
            if (emptyState != null) emptyState.SetActive(true);
            if (dataState != null) dataState.SetActive(false);
            if (slotNameText != null) slotNameText.text = info.slotName;
            if (deleteButton != null) deleteButton.gameObject.SetActive(false);

            if (playButtonText != null)
            {
                if (LocalizationSystem.Instance != null)
                    playButtonText.text = LocalizationSystem.Instance.Get("save_new", "新建存档");
                else
                    playButtonText.text = "新建存档";
            }
        }
        else
        {
            // 有数据的存档
            if (emptyState != null) emptyState.SetActive(false);
            if (dataState != null) dataState.SetActive(true);
            if (deleteButton != null) deleteButton.gameObject.SetActive(true);

            if (slotNameText != null)
                slotNameText.text = info.slotName;

            if (chapterText != null)
            {
                string chapterKey = $"chapter_{info.chapter}";
                if (LocalizationSystem.Instance != null)
                    chapterText.text = $"{LocalizationSystem.Instance.Get(chapterKey)} - {info.level}";
                else
                    chapterText.text = $"第{info.chapter}章 - 第{info.level}关";
            }

            if (playTimeText != null)
                playTimeText.text = SaveSystem.FormatPlayTime(info.playTime);

            if (completionText != null)
                completionText.text = $"{info.completionPercent:F1}%";

            if (completionBar != null)
                completionBar.value = info.completionPercent / 100f;

            if (dateText != null)
                dateText.text = info.lastSaveDate;

            if (playButtonText != null)
            {
                if (LocalizationSystem.Instance != null)
                    playButtonText.text = LocalizationSystem.Instance.Get("menu_continue", "继续");
                else
                    playButtonText.text = "继续";
            }
        }

        // 高亮当前活跃存档
        if (slotBackground != null)
            slotBackground.color = isActive ? activeColor : normalColor;
    }
}
