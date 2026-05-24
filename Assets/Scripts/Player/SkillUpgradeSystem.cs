using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 技能升级系统 - 管理角色技能解锁与增强
/// 基于关卡进度和收集品解锁新能力
/// </summary>
public class SkillUpgradeSystem : MonoBehaviour
{
    public static SkillUpgradeSystem Instance { get; private set; }

    [System.Serializable]
    public class SkillUpgrade
    {
        public string skillId;
        public string skillName;
        public string description;
        public Sprite icon;
        public PlayerController.PlayerType playerType; // Lux or Nox
        public int requiredStars;     // 需要累计多少星
        public int requiredChapter;   // 需要通过第几章
        public bool isUnlocked;

        // 升级效果
        public UpgradeType upgradeType;
        public float upgradeValue;
    }

    public enum UpgradeType
    {
        CooldownReduction,     // 技能冷却缩短
        DurationIncrease,      // 技能持续延长
        DamageIncrease,        // 伤害提升
        RangeIncrease,         // 范围增大
        SpeedBoost,            // 移动速度提升
        HealthIncrease,        // 生命值提升
        NewAbility,            // 解锁新技能
        PassiveEnhance         // 被动增强
    }

    [SerializeField] private List<SkillUpgrade> allUpgrades = new List<SkillUpgrade>();

    private Dictionary<string, SkillUpgrade> upgradeMap = new Dictionary<string, SkillUpgrade>();
    private const string UPGRADE_SAVE_KEY = "skill_upgrades";

    public event System.Action<SkillUpgrade> OnSkillUnlocked;

    public List<SkillUpgrade> AllUpgrades => allUpgrades;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildUpgradeMap();
        LoadUnlockState();
    }

    private void BuildUpgradeMap()
    {
        foreach (var upgrade in allUpgrades)
        {
            upgradeMap[upgrade.skillId] = upgrade;
        }
    }

    /// <summary>
    /// 检查并解锁可用的技能升级
    /// </summary>
    public void CheckUnlocks()
    {
        int totalStars = GetTotalStars();
        int completedChapters = GetCompletedChapters();

        foreach (var upgrade in allUpgrades)
        {
            if (upgrade.isUnlocked) continue;

            bool meetsStars = totalStars >= upgrade.requiredStars;
            bool meetsChapter = completedChapters >= upgrade.requiredChapter;

            if (meetsStars && meetsChapter)
            {
                UnlockSkill(upgrade);
            }
        }
    }

    private void UnlockSkill(SkillUpgrade upgrade)
    {
        upgrade.isUnlocked = true;
        SaveUnlockState();
        OnSkillUnlocked?.Invoke(upgrade);

        // 显示成就通知
        if (AchievementSystem.Instance != null)
        {
            AchievementSystem.Instance.UpdateProgress($"skill_{upgrade.skillId}");
        }

        Debug.Log($"[SkillUpgrade] 技能已解锁: {upgrade.skillName}");
    }

    /// <summary>
    /// 获取指定角色的所有升级
    /// </summary>
    public List<SkillUpgrade> GetUpgradesForPlayer(PlayerController.PlayerType playerType)
    {
        return allUpgrades.FindAll(u => u.playerType == playerType);
    }

    /// <summary>
    /// 获取已解锁的升级
    /// </summary>
    public List<SkillUpgrade> GetUnlockedUpgrades(PlayerController.PlayerType playerType)
    {
        return allUpgrades.FindAll(u => u.playerType == playerType && u.isUnlocked);
    }

    /// <summary>
    /// 检查技能是否已解锁
    /// </summary>
    public bool IsSkillUnlocked(string skillId)
    {
        if (upgradeMap.TryGetValue(skillId, out var upgrade))
            return upgrade.isUnlocked;
        return false;
    }

    /// <summary>
    /// 获取指定类型的升级值（多个同类升级叠加）
    /// </summary>
    public float GetUpgradeBonus(PlayerController.PlayerType playerType, UpgradeType type)
    {
        float total = 0;
        foreach (var upgrade in allUpgrades)
        {
            if (upgrade.playerType == playerType && upgrade.isUnlocked && upgrade.upgradeType == type)
                total += upgrade.upgradeValue;
        }
        return total;
    }

    /// <summary>
    /// 获取解锁进度 (0~1)
    /// </summary>
    public float GetUnlockProgress(string skillId)
    {
        if (!upgradeMap.TryGetValue(skillId, out var upgrade)) return 0;
        if (upgrade.isUnlocked) return 1f;

        int totalStars = GetTotalStars();
        float starProgress = upgrade.requiredStars > 0 ?
            Mathf.Clamp01((float)totalStars / upgrade.requiredStars) : 1f;

        int completedChapters = GetCompletedChapters();
        float chapterProgress = upgrade.requiredChapter > 0 ?
            Mathf.Clamp01((float)completedChapters / upgrade.requiredChapter) : 1f;

        return Mathf.Min(starProgress, chapterProgress);
    }

    private int GetTotalStars()
    {
        if (SaveSystem.Instance == null) return 0;

        int total = 0;
        var data = SaveSystem.Instance.Data;
        if (data.levelStars != null)
        {
            for (int i = 0; i < data.levelStars.Length; i++)
                total += data.levelStars[i];
        }
        return total;
    }

    private int GetCompletedChapters()
    {
        if (SaveSystem.Instance == null) return 0;

        var data = SaveSystem.Instance.Data;
        int[] levelsPerChapter = { 3, 4, 4, 5, 4 };
        int completedChapters = 0;
        int startIdx = 0;

        for (int ch = 0; ch < levelsPerChapter.Length; ch++)
        {
            bool allComplete = true;
            for (int lv = 0; lv < levelsPerChapter[ch]; lv++)
            {
                if (startIdx + lv >= data.levelsCompleted.Length || !data.levelsCompleted[startIdx + lv])
                {
                    allComplete = false;
                    break;
                }
            }
            if (allComplete) completedChapters++;
            startIdx += levelsPerChapter[ch];
        }

        return completedChapters;
    }

    private void SaveUnlockState()
    {
        var saveData = new UpgradeSaveData();
        foreach (var u in allUpgrades)
        {
            if (u.isUnlocked)
                saveData.unlockedIds.Add(u.skillId);
        }
        string json = JsonUtility.ToJson(saveData);
        PlayerPrefs.SetString(UPGRADE_SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    private void LoadUnlockState()
    {
        if (!PlayerPrefs.HasKey(UPGRADE_SAVE_KEY)) return;

        string json = PlayerPrefs.GetString(UPGRADE_SAVE_KEY);
        var saveData = JsonUtility.FromJson<UpgradeSaveData>(json);
        if (saveData == null) return;

        foreach (var id in saveData.unlockedIds)
        {
            if (upgradeMap.TryGetValue(id, out var upgrade))
                upgrade.isUnlocked = true;
        }
    }

    [System.Serializable]
    private class UpgradeSaveData
    {
        public List<string> unlockedIds = new List<string>();
    }
}
