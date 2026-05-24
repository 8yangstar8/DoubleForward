using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 技能展示与升级界面 - 显示角色技能树和解锁进度
/// </summary>
public class SkillTreeUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject skillTreePanel;
    [SerializeField] private Button closeButton;

    [Header("角色切换")]
    [SerializeField] private Button luxTab;
    [SerializeField] private Button noxTab;
    [SerializeField] private Image luxTabBg;
    [SerializeField] private Image noxTabBg;
    [SerializeField] private Color activeTabColor = new Color(0.3f, 0.3f, 0.6f);
    [SerializeField] private Color inactiveTabColor = new Color(0.15f, 0.15f, 0.2f);

    [Header("角色信息")]
    [SerializeField] private Image characterPortrait;
    [SerializeField] private TextMeshProUGUI characterName;
    [SerializeField] private TextMeshProUGUI characterDesc;
    [SerializeField] private Sprite luxPortrait;
    [SerializeField] private Sprite noxPortrait;

    [Header("技能列表")]
    [SerializeField] private Transform skillListContent;
    [SerializeField] private GameObject skillItemPrefab;

    [Header("技能详情")]
    [SerializeField] private GameObject skillDetailPanel;
    [SerializeField] private Image detailIcon;
    [SerializeField] private TextMeshProUGUI detailName;
    [SerializeField] private TextMeshProUGUI detailDesc;
    [SerializeField] private Slider detailProgress;
    [SerializeField] private TextMeshProUGUI detailRequirement;
    [SerializeField] private TextMeshProUGUI detailEffect;
    [SerializeField] private Image detailLockedOverlay;

    private PlayerController.PlayerType currentViewType = PlayerController.PlayerType.Lux;
    private List<GameObject> spawnedItems = new List<GameObject>();

    void Start()
    {
        closeButton?.onClick.AddListener(Hide);
        luxTab?.onClick.AddListener(() => ShowCharacter(PlayerController.PlayerType.Lux));
        noxTab?.onClick.AddListener(() => ShowCharacter(PlayerController.PlayerType.Nox));

        if (skillTreePanel != null)
            skillTreePanel.SetActive(false);
        if (skillDetailPanel != null)
            skillDetailPanel.SetActive(false);
    }

    public void Show()
    {
        if (skillTreePanel != null)
            skillTreePanel.SetActive(true);

        ShowCharacter(currentViewType);
    }

    public void Hide()
    {
        if (skillTreePanel != null)
            skillTreePanel.SetActive(false);
    }

    private void ShowCharacter(PlayerController.PlayerType type)
    {
        currentViewType = type;

        // 更新标签页
        if (luxTabBg != null) luxTabBg.color = type == PlayerController.PlayerType.Lux ? activeTabColor : inactiveTabColor;
        if (noxTabBg != null) noxTabBg.color = type == PlayerController.PlayerType.Nox ? activeTabColor : inactiveTabColor;

        // 更新角色信息
        if (characterPortrait != null)
            characterPortrait.sprite = type == PlayerController.PlayerType.Lux ? luxPortrait : noxPortrait;

        if (characterName != null)
            characterName.text = type == PlayerController.PlayerType.Lux ? "Lux" : "Nox";

        if (characterDesc != null)
        {
            if (type == PlayerController.PlayerType.Lux)
                characterDesc.text = GetLocalized("lux_desc", "光之守护者 - 照亮前路，创造桥梁");
            else
                characterDesc.text = GetLocalized("nox_desc", "影之行者 - 穿越障碍，掌控暗影");
        }

        RefreshSkillList();
    }

    private void RefreshSkillList()
    {
        // 清除旧项
        foreach (var item in spawnedItems)
        {
            if (item != null) Destroy(item);
        }
        spawnedItems.Clear();

        if (SkillUpgradeSystem.Instance == null || skillListContent == null || skillItemPrefab == null) return;

        var upgrades = SkillUpgradeSystem.Instance.GetUpgradesForPlayer(currentViewType);

        foreach (var upgrade in upgrades)
        {
            var itemObj = Instantiate(skillItemPrefab, skillListContent);
            SetupSkillItem(itemObj, upgrade);
            spawnedItems.Add(itemObj);
        }
    }

    private void SetupSkillItem(GameObject itemObj, SkillUpgradeSystem.SkillUpgrade upgrade)
    {
        // 图标
        var iconImg = itemObj.transform.Find("Icon")?.GetComponent<Image>();
        if (iconImg != null && upgrade.icon != null)
        {
            iconImg.sprite = upgrade.icon;
            iconImg.color = upgrade.isUnlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f);
        }

        // 名称
        var nameText = itemObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
            nameText.text = upgrade.skillName;

        // 锁定覆盖
        var lockOverlay = itemObj.transform.Find("LockOverlay")?.gameObject;
        if (lockOverlay != null)
            lockOverlay.SetActive(!upgrade.isUnlocked);

        // 进度条
        var progressBar = itemObj.transform.Find("ProgressBar")?.GetComponent<Slider>();
        if (progressBar != null)
        {
            float prog = SkillUpgradeSystem.Instance.GetUnlockProgress(upgrade.skillId);
            progressBar.value = prog;
            progressBar.gameObject.SetActive(!upgrade.isUnlocked);
        }

        // 点击查看详情
        var btn = itemObj.GetComponent<Button>();
        if (btn == null) btn = itemObj.AddComponent<Button>();
        var capturedUpgrade = upgrade;
        btn.onClick.AddListener(() => ShowSkillDetail(capturedUpgrade));
    }

    private void ShowSkillDetail(SkillUpgradeSystem.SkillUpgrade upgrade)
    {
        if (skillDetailPanel == null) return;

        skillDetailPanel.SetActive(true);

        if (detailIcon != null && upgrade.icon != null)
            detailIcon.sprite = upgrade.icon;

        if (detailName != null)
            detailName.text = upgrade.skillName;

        if (detailDesc != null)
            detailDesc.text = upgrade.description;

        if (detailProgress != null)
        {
            float prog = SkillUpgradeSystem.Instance != null ?
                SkillUpgradeSystem.Instance.GetUnlockProgress(upgrade.skillId) : 0;
            detailProgress.value = prog;
        }

        if (detailRequirement != null)
        {
            if (upgrade.isUnlocked)
            {
                detailRequirement.text = GetLocalized("skill_unlocked", "已解锁");
                detailRequirement.color = Color.green;
            }
            else
            {
                string req = "";
                if (upgrade.requiredStars > 0)
                    req += $"{GetLocalized("skill_req_stars", "需要")} {upgrade.requiredStars} {GetLocalized("skill_stars", "星")}";
                if (upgrade.requiredChapter > 0)
                {
                    if (!string.IsNullOrEmpty(req)) req += " | ";
                    req += $"{GetLocalized("skill_req_chapter", "通过第")} {upgrade.requiredChapter} {GetLocalized("skill_chapter", "章")}";
                }
                detailRequirement.text = req;
                detailRequirement.color = Color.yellow;
            }
        }

        if (detailEffect != null)
        {
            string effectDesc = GetUpgradeEffectText(upgrade);
            detailEffect.text = effectDesc;
        }

        if (detailLockedOverlay != null)
            detailLockedOverlay.gameObject.SetActive(!upgrade.isUnlocked);

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayUIClick();
    }

    private string GetUpgradeEffectText(SkillUpgradeSystem.SkillUpgrade upgrade)
    {
        switch (upgrade.upgradeType)
        {
            case SkillUpgradeSystem.UpgradeType.CooldownReduction:
                return $"{GetLocalized("effect_cooldown", "冷却缩短")} {upgrade.upgradeValue * 100:F0}%";
            case SkillUpgradeSystem.UpgradeType.DurationIncrease:
                return $"{GetLocalized("effect_duration", "持续延长")} {upgrade.upgradeValue:F1}s";
            case SkillUpgradeSystem.UpgradeType.DamageIncrease:
                return $"{GetLocalized("effect_damage", "伤害提升")} {upgrade.upgradeValue * 100:F0}%";
            case SkillUpgradeSystem.UpgradeType.RangeIncrease:
                return $"{GetLocalized("effect_range", "范围增大")} {upgrade.upgradeValue * 100:F0}%";
            case SkillUpgradeSystem.UpgradeType.SpeedBoost:
                return $"{GetLocalized("effect_speed", "速度提升")} {upgrade.upgradeValue * 100:F0}%";
            case SkillUpgradeSystem.UpgradeType.HealthIncrease:
                return $"{GetLocalized("effect_health", "生命提升")} +{upgrade.upgradeValue:F0}";
            case SkillUpgradeSystem.UpgradeType.NewAbility:
                return GetLocalized("effect_new_ability", "解锁新技能");
            case SkillUpgradeSystem.UpgradeType.PassiveEnhance:
                return $"{GetLocalized("effect_passive", "被动强化")} {upgrade.upgradeValue * 100:F0}%";
            default:
                return "";
        }
    }

    private string GetLocalized(string key, string fallback)
    {
        if (LocalizationSystem.Instance != null)
            return LocalizationSystem.Instance.Get(key, fallback);
        return fallback;
    }
}
