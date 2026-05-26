using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 遗物背包UI - 展示收集的遗物、管理装备、显示套装加成
/// </summary>
public class RelicInventoryUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private Transform relicGridParent;
    [SerializeField] private GameObject relicSlotPrefab;

    [Header("详情面板")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private Image detailIcon;
    [SerializeField] private TextMeshProUGUI detailNameText;
    [SerializeField] private TextMeshProUGUI detailDescText;
    [SerializeField] private TextMeshProUGUI detailEffectsText;
    [SerializeField] private TextMeshProUGUI detailRarityText;
    [SerializeField] private Button equipButton;
    [SerializeField] private TextMeshProUGUI equipButtonText;

    [Header("装备栏")]
    [SerializeField] private Transform equippedSlotsParent;
    [SerializeField] private TextMeshProUGUI equippedCountText;

    [Header("套装信息")]
    [SerializeField] private Transform setBonusParent;
    [SerializeField] private GameObject setBonusPrefab;

    [Header("统计")]
    [SerializeField] private TextMeshProUGUI collectionProgressText;
    [SerializeField] private Slider collectionSlider;

    [Header("筛选")]
    [SerializeField] private Button filterAllButton;
    [SerializeField] private Button filterLuxButton;
    [SerializeField] private Button filterNoxButton;
    [SerializeField] private Button filterSharedButton;

    [Header("音效")]
    [SerializeField] private string equipSound = "relic_equip";
    [SerializeField] private string unequipSound = "relic_unequip";
    [SerializeField] private string selectSound = "ui_click";

    // 运行时
    private List<RelicSlotUI> slotInstances = new List<RelicSlotUI>();
    private RelicSystem.RelicData selectedRelic;
    private RelicSystem.RelicOwner? currentFilter;

    void Start()
    {
        if (equipButton != null)
            equipButton.onClick.AddListener(OnEquipButtonClicked);

        // 筛选按钮
        if (filterAllButton != null) filterAllButton.onClick.AddListener(() => SetFilter(null));
        if (filterLuxButton != null) filterLuxButton.onClick.AddListener(() => SetFilter(RelicSystem.RelicOwner.LuxOnly));
        if (filterNoxButton != null) filterNoxButton.onClick.AddListener(() => SetFilter(RelicSystem.RelicOwner.NoxOnly));
        if (filterSharedButton != null) filterSharedButton.onClick.AddListener(() => SetFilter(RelicSystem.RelicOwner.Shared));

        // 监听遗物收集
        if (RelicSystem.Instance != null)
        {
            RelicSystem.Instance.OnRelicCollected += OnRelicCollected;
            RelicSystem.Instance.OnRelicEquipped += OnRelicChanged;
            RelicSystem.Instance.OnRelicUnequipped += OnRelicChanged;
        }

        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (detailPanel != null) detailPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (RelicSystem.Instance != null)
        {
            RelicSystem.Instance.OnRelicCollected -= OnRelicCollected;
            RelicSystem.Instance.OnRelicEquipped -= OnRelicChanged;
            RelicSystem.Instance.OnRelicUnequipped -= OnRelicChanged;
        }
    }

    // ==================== 公共接口 ====================

    public void Show()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(true);

        RefreshInventory();
        RefreshStats();
        RefreshSetBonuses();
    }

    public void Hide()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
    }

    public void Toggle()
    {
        if (inventoryPanel != null && inventoryPanel.activeSelf)
            Hide();
        else
            Show();
    }

    // ==================== 刷新 ====================

    private void RefreshInventory()
    {
        if (RelicSystem.Instance == null) return;

        // 清理旧槽位
        foreach (var slot in slotInstances)
        {
            if (slot != null && slot.gameObject != null)
                Destroy(slot.gameObject);
        }
        slotInstances.Clear();

        // 获取已收集遗物
        var relics = RelicSystem.Instance.GetCollectedRelics();

        // 应用筛选
        if (currentFilter.HasValue)
            relics = relics.FindAll(r => r.owner == currentFilter.Value);

        // 按稀有度排序
        relics.Sort((a, b) => ((int)b.rarity).CompareTo((int)a.rarity));

        // 创建UI槽位
        foreach (var relic in relics)
        {
            if (relicSlotPrefab == null || relicGridParent == null) continue;

            var obj = Instantiate(relicSlotPrefab, relicGridParent);
            var slot = obj.GetComponent<RelicSlotUI>();
            if (slot == null) slot = obj.AddComponent<RelicSlotUI>();

            bool equipped = RelicSystem.Instance.IsEquipped(relic.relicId);
            slot.Setup(relic, equipped, OnRelicSlotClicked);
            slotInstances.Add(slot);
        }
    }

    private void RefreshStats()
    {
        if (RelicSystem.Instance == null) return;

        int collected = RelicSystem.Instance.CollectedCount;
        int total = RelicSystem.Instance.TotalRelicCount;
        int equipped = RelicSystem.Instance.EquippedCount;
        int maxEquip = RelicSystem.Instance.MaxEquipped;

        if (collectionProgressText != null)
            collectionProgressText.text = $"{collected}/{total}";

        if (collectionSlider != null)
            collectionSlider.value = total > 0 ? (float)collected / total : 0;

        if (equippedCountText != null)
            equippedCountText.text = $"{equipped}/{maxEquip}";
    }

    private void RefreshSetBonuses()
    {
        if (RelicSystem.Instance == null || setBonusParent == null) return;

        // 清理
        foreach (Transform child in setBonusParent)
            Destroy(child.gameObject);

        var activeSets = RelicSystem.Instance.GetActiveSetBonuses();
        foreach (var set in activeSets)
        {
            if (setBonusPrefab == null) continue;

            var obj = Instantiate(setBonusPrefab, setBonusParent);
            var label = obj.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                string name = set.setNameKey;
                if (LocalizationSystem.Instance != null)
                {
                    string localized = LocalizationSystem.Instance.GetText(set.setNameKey);
                    if (localized != set.setNameKey) name = localized;
                }
                label.text = $"[{name}] Active";
            }
        }
    }

    private void RefreshDetailPanel()
    {
        if (selectedRelic == null || detailPanel == null) return;

        detailPanel.SetActive(true);

        if (detailIcon != null && selectedRelic.icon != null)
            detailIcon.sprite = selectedRelic.icon;

        // 名称
        if (detailNameText != null)
        {
            string name = selectedRelic.nameKey;
            if (LocalizationSystem.Instance != null)
            {
                string localized = LocalizationSystem.Instance.GetText(selectedRelic.nameKey);
                if (localized != selectedRelic.nameKey) name = localized;
            }
            detailNameText.text = name;
        }

        // 描述
        if (detailDescText != null)
        {
            string desc = selectedRelic.descriptionKey;
            if (LocalizationSystem.Instance != null)
            {
                string localized = LocalizationSystem.Instance.GetText(selectedRelic.descriptionKey);
                if (localized != selectedRelic.descriptionKey) desc = localized;
            }
            detailDescText.text = desc;
        }

        // 效果列表
        if (detailEffectsText != null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var effect in selectedRelic.effects)
            {
                string sign = effect.value >= 0 ? "+" : "";
                string format = effect.isPercentage ? $"{sign}{effect.value * 100:F0}%" : $"{sign}{effect.value:F0}";
                sb.AppendLine($"• {effect.stat}: {format}");
            }
            detailEffectsText.text = sb.ToString();
        }

        // 稀有度
        if (detailRarityText != null)
        {
            detailRarityText.text = selectedRelic.rarity.ToString();
            detailRarityText.color = GetRarityColor(selectedRelic.rarity);
        }

        // 装备按钮
        if (equipButton != null && equipButtonText != null && RelicSystem.Instance != null)
        {
            bool equipped = RelicSystem.Instance.IsEquipped(selectedRelic.relicId);
            bool canEquip = !equipped && RelicSystem.Instance.EquippedCount < RelicSystem.Instance.MaxEquipped;

            equipButtonText.text = equipped ? GetLocalizedText("relic_unequip", "Unequip") : GetLocalizedText("relic_equip", "Equip");
            equipButton.interactable = equipped || canEquip;
        }
    }

    // ==================== 事件 ====================

    private void OnRelicSlotClicked(RelicSystem.RelicData relic)
    {
        selectedRelic = relic;
        RefreshDetailPanel();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(selectSound);
    }

    private void OnEquipButtonClicked()
    {
        if (selectedRelic == null || RelicSystem.Instance == null) return;

        bool equipped = RelicSystem.Instance.IsEquipped(selectedRelic.relicId);

        if (equipped)
        {
            RelicSystem.Instance.UnequipRelic(selectedRelic.relicId);
            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play(unequipSound);
        }
        else
        {
            RelicSystem.Instance.EquipRelic(selectedRelic.relicId);
            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play(equipSound);
        }

        RefreshInventory();
        RefreshDetailPanel();
        RefreshStats();
        RefreshSetBonuses();
    }

    private void OnRelicCollected(RelicSystem.RelicData relic)
    {
        if (inventoryPanel != null && inventoryPanel.activeSelf)
        {
            RefreshInventory();
            RefreshStats();
        }
    }

    private void OnRelicChanged(RelicSystem.RelicData relic)
    {
        if (inventoryPanel != null && inventoryPanel.activeSelf)
        {
            RefreshInventory();
            RefreshStats();
            RefreshSetBonuses();
        }
    }

    private void SetFilter(RelicSystem.RelicOwner? filter)
    {
        currentFilter = filter;
        RefreshInventory();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayUIClick();
    }

    // ==================== 辅助 ====================

    private Color GetRarityColor(RelicSystem.RelicRarity rarity)
    {
        return rarity switch
        {
            RelicSystem.RelicRarity.Common => Color.gray,
            RelicSystem.RelicRarity.Rare => new Color(0.3f, 0.6f, 1f),
            RelicSystem.RelicRarity.Epic => new Color(0.7f, 0.3f, 1f),
            RelicSystem.RelicRarity.Legendary => new Color(1f, 0.8f, 0.2f),
            _ => Color.white
        };
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
/// 遗物格子UI组件
/// </summary>
public class RelicSlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image borderImage;
    [SerializeField] private Image equippedIndicator;
    [SerializeField] private Button button;

    private RelicSystem.RelicData relicData;
    private System.Action<RelicSystem.RelicData> clickCallback;

    public void Setup(RelicSystem.RelicData relic, bool equipped, System.Action<RelicSystem.RelicData> onClick)
    {
        relicData = relic;
        clickCallback = onClick;

        // 图标
        if (iconImage != null && relic.icon != null)
            iconImage.sprite = relic.icon;

        // 边框颜色（稀有度）
        if (borderImage != null)
        {
            borderImage.color = relic.rarity switch
            {
                RelicSystem.RelicRarity.Common => new Color(0.6f, 0.6f, 0.6f),
                RelicSystem.RelicRarity.Rare => new Color(0.3f, 0.6f, 1f),
                RelicSystem.RelicRarity.Epic => new Color(0.7f, 0.3f, 1f),
                RelicSystem.RelicRarity.Legendary => new Color(1f, 0.8f, 0.2f),
                _ => Color.white
            };
        }

        // 装备指示
        if (equippedIndicator != null)
            equippedIndicator.gameObject.SetActive(equipped);

        // 按钮
        if (button == null) button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(() => clickCallback?.Invoke(relicData));
    }
}
