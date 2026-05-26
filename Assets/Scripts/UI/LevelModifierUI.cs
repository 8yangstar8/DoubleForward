using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 关卡修改器选择UI - 在关卡选择界面显示
/// 玩家可以选择修改器组合来增加挑战和得分乘数
/// </summary>
public class LevelModifierUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject modifierPanel;
    [SerializeField] private Transform modifierListParent;
    [SerializeField] private GameObject modifierItemPrefab;

    [Header("分类标签")]
    [SerializeField] private Button tabDifficulty;
    [SerializeField] private Button tabMovement;
    [SerializeField] private Button tabCombat;
    [SerializeField] private Button tabEconomy;
    [SerializeField] private Button tabFun;
    [SerializeField] private Button tabAll;

    [Header("得分显示")]
    [SerializeField] private TextMeshProUGUI scoreMultiplierText;
    [SerializeField] private Image multiplierBarFill;

    [Header("每日挑战")]
    [SerializeField] private Button dailyChallengeButton;
    [SerializeField] private TextMeshProUGUI dailyInfoText;

    [Header("激活的修改器")]
    [SerializeField] private Transform activeSlotsParent;
    [SerializeField] private TextMeshProUGUI activeCountText;

    [Header("按钮")]
    [SerializeField] private Button clearAllButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button closeButton;

    // 运行时
    private LevelModifierSystem.ModifierCategory? currentCategory;
    private List<ModifierItemUI> itemInstances = new List<ModifierItemUI>();

    void Start()
    {
        // 分类标签
        if (tabDifficulty != null) tabDifficulty.onClick.AddListener(() => ShowCategory(LevelModifierSystem.ModifierCategory.Difficulty));
        if (tabMovement != null) tabMovement.onClick.AddListener(() => ShowCategory(LevelModifierSystem.ModifierCategory.Movement));
        if (tabCombat != null) tabCombat.onClick.AddListener(() => ShowCategory(LevelModifierSystem.ModifierCategory.Combat));
        if (tabEconomy != null) tabEconomy.onClick.AddListener(() => ShowCategory(LevelModifierSystem.ModifierCategory.Economy));
        if (tabFun != null) tabFun.onClick.AddListener(() => ShowCategory(LevelModifierSystem.ModifierCategory.Fun));
        if (tabAll != null) tabAll.onClick.AddListener(() => ShowCategory(null));

        // 按钮
        if (clearAllButton != null) clearAllButton.onClick.AddListener(OnClearAll);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (dailyChallengeButton != null) dailyChallengeButton.onClick.AddListener(OnDailyChallenge);

        if (modifierPanel != null) modifierPanel.SetActive(false);
    }

    // ==================== 公共接口 ====================

    public void Show()
    {
        if (modifierPanel != null) modifierPanel.SetActive(true);
        ShowCategory(null);
        RefreshScoreMultiplier();
        RefreshDailyInfo();
        RefreshActiveCount();
    }

    public void Hide()
    {
        if (modifierPanel != null) modifierPanel.SetActive(false);
    }

    // ==================== 刷新 ====================

    private void ShowCategory(LevelModifierSystem.ModifierCategory? category)
    {
        currentCategory = category;

        // 清理
        foreach (var item in itemInstances)
        {
            if (item != null && item.gameObject != null)
                Destroy(item.gameObject);
        }
        itemInstances.Clear();

        if (LevelModifierSystem.Instance == null) return;

        List<LevelModifierSystem.ModifierData> modifiers;

        if (category.HasValue)
            modifiers = LevelModifierSystem.Instance.GetModifiersByCategory(category.Value);
        else
            modifiers = LevelModifierSystem.Instance.GetAvailableModifiers();

        foreach (var mod in modifiers)
        {
            if (modifierItemPrefab == null || modifierListParent == null) continue;

            var obj = Instantiate(modifierItemPrefab, modifierListParent);
            var item = obj.GetComponent<ModifierItemUI>();
            if (item == null) item = obj.AddComponent<ModifierItemUI>();

            bool active = LevelModifierSystem.Instance.IsModifierActive(mod.type);
            item.Setup(mod, active, OnModifierToggled);
            itemInstances.Add(item);
        }

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayUIClick();
    }

    private void RefreshScoreMultiplier()
    {
        if (LevelModifierSystem.Instance == null) return;

        float mult = LevelModifierSystem.Instance.ScoreMultiplier;

        if (scoreMultiplierText != null)
        {
            string color = mult > 1f ? "#FFD700" : mult < 1f ? "#FF6666" : "#FFFFFF";
            scoreMultiplierText.text = $"<color={color}>x{mult:F2}</color>";
        }

        if (multiplierBarFill != null)
        {
            // 归一化到 0.5~2.5 范围
            float norm = Mathf.InverseLerp(0.5f, 2.5f, mult);
            multiplierBarFill.fillAmount = norm;

            // 颜色梯度
            multiplierBarFill.color = mult > 1.5f ? new Color(1f, 0.8f, 0.2f) :
                                     mult > 1f ? new Color(0.4f, 1f, 0.4f) :
                                     mult < 1f ? new Color(1f, 0.4f, 0.4f) :
                                     Color.white;
        }
    }

    private void RefreshDailyInfo()
    {
        if (LevelModifierSystem.Instance == null || dailyInfoText == null) return;

        var daily = LevelModifierSystem.Instance.GetDailyModifiers();
        if (daily.Count == 0)
        {
            dailyInfoText.text = "";
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append(GetLocalizedText("daily_modifiers", "Daily: "));

        for (int i = 0; i < daily.Count; i++)
        {
            string name = GetLocalizedText(daily[i].nameKey, daily[i].modifierId);
            sb.Append(name);
            if (i < daily.Count - 1) sb.Append(", ");
        }

        dailyInfoText.text = sb.ToString();
    }

    private void RefreshActiveCount()
    {
        if (LevelModifierSystem.Instance == null) return;

        int count = LevelModifierSystem.Instance.ActiveModifiers.Count;
        if (activeCountText != null)
            activeCountText.text = count.ToString();
    }

    // ==================== 事件 ====================

    private void OnModifierToggled(LevelModifierSystem.ModifierData mod, bool activate)
    {
        if (LevelModifierSystem.Instance == null) return;

        if (activate)
        {
            bool success = LevelModifierSystem.Instance.ActivateModifier(mod.modifierId);
            if (!success)
            {
                // 同类型冲突
                EventBus.Publish(new HintRequestEvent
                {
                    textKey = "modifier_conflict",
                    fallbackText = "Cannot activate: conflicting modifier active",
                    duration = 2f
                });
            }
        }
        else
        {
            LevelModifierSystem.Instance.DeactivateModifier(mod.modifierId);
        }

        // 刷新所有
        ShowCategory(currentCategory);
        RefreshScoreMultiplier();
        RefreshActiveCount();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(activate ? "modifier_on" : "modifier_off");
    }

    private void OnClearAll()
    {
        if (LevelModifierSystem.Instance != null)
            LevelModifierSystem.Instance.ClearAllModifiers();

        ShowCategory(currentCategory);
        RefreshScoreMultiplier();
        RefreshActiveCount();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayUIClick();
    }

    private void OnDailyChallenge()
    {
        if (LevelModifierSystem.Instance != null)
            LevelModifierSystem.Instance.ActivateDailySet();

        ShowCategory(currentCategory);
        RefreshScoreMultiplier();
        RefreshActiveCount();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();
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

/// <summary>
/// 修改器列表项UI
/// </summary>
public class ModifierItemUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private TextMeshProUGUI multiplierText;
    [SerializeField] private Image difficultyBadge;
    [SerializeField] private Toggle activeToggle;

    private LevelModifierSystem.ModifierData modData;
    private System.Action<LevelModifierSystem.ModifierData, bool> toggleCallback;

    public void Setup(LevelModifierSystem.ModifierData data, bool isActive,
        System.Action<LevelModifierSystem.ModifierData, bool> onToggle)
    {
        modData = data;
        toggleCallback = onToggle;

        // 图标
        if (iconImage != null && data.icon != null)
            iconImage.sprite = data.icon;

        // 名称
        if (nameText != null)
        {
            string name = data.nameKey;
            if (LocalizationSystem.Instance != null)
            {
                string localized = LocalizationSystem.Instance.GetText(data.nameKey);
                if (localized != data.nameKey) name = localized;
            }
            nameText.text = name;
        }

        // 描述
        if (descText != null)
        {
            string desc = data.descriptionKey;
            if (LocalizationSystem.Instance != null)
            {
                string localized = LocalizationSystem.Instance.GetText(data.descriptionKey);
                if (localized != data.descriptionKey) desc = localized;
            }
            descText.text = desc;
        }

        // 分数乘数
        if (multiplierText != null)
        {
            string color = data.scoreMultiplier > 1f ? "#FFD700" : data.scoreMultiplier < 1f ? "#FF6666" : "#CCCCCC";
            multiplierText.text = $"<color={color}>x{data.scoreMultiplier:F2}</color>";
        }

        // 难度颜色
        if (difficultyBadge != null)
        {
            difficultyBadge.color = data.difficulty switch
            {
                LevelModifierSystem.ModifierDifficulty.Easy => new Color(0.4f, 0.8f, 0.4f),
                LevelModifierSystem.ModifierDifficulty.Normal => new Color(0.8f, 0.8f, 0.8f),
                LevelModifierSystem.ModifierDifficulty.Hard => new Color(1f, 0.6f, 0.2f),
                LevelModifierSystem.ModifierDifficulty.Extreme => new Color(1f, 0.2f, 0.2f),
                _ => Color.white
            };
        }

        // 开关
        if (activeToggle != null)
        {
            activeToggle.isOn = isActive;
            activeToggle.onValueChanged.AddListener(OnToggleChanged);
        }
    }

    private void OnToggleChanged(bool value)
    {
        toggleCallback?.Invoke(modData, value);
    }

    void OnDestroy()
    {
        if (activeToggle != null)
            activeToggle.onValueChanged.RemoveListener(OnToggleChanged);
    }
}
