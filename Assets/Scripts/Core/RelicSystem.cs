using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 遗物系统 - 管理可收集的遗物/圣物
/// 遗物散布在各关卡中，提供永久被动增益
/// 支持Lux专属、Nox专属和双人共享三种类型
/// 遗物可组合为套装获得额外加成
/// </summary>
public class RelicSystem : MonoBehaviour
{
    public static RelicSystem Instance { get; private set; }

    [Header("遗物数据")]
    [SerializeField] private List<RelicData> allRelics = new List<RelicData>();
    [SerializeField] private List<RelicSetBonus> relicSets = new List<RelicSetBonus>();

    [Header("最大装备数")]
    [SerializeField] private int maxEquippedRelics = 6;

    // 运行时
    private HashSet<string> collectedRelicIds = new HashSet<string>();
    private List<string> equippedRelicIds = new List<string>();
    private Dictionary<string, RelicData> relicMap = new Dictionary<string, RelicData>();
    private const string COLLECTED_KEY = "collected_relics";
    private const string EQUIPPED_KEY = "equipped_relics";

    // 缓存的增益值
    private Dictionary<RelicStat, float> cachedBonuses = new Dictionary<RelicStat, float>();
    private bool bonusesDirty = true;

    public int CollectedCount => collectedRelicIds.Count;
    public int TotalRelicCount => allRelics.Count;
    public int EquippedCount => equippedRelicIds.Count;
    public int MaxEquipped => maxEquippedRelics;

    public event System.Action<RelicData> OnRelicCollected;
    public event System.Action<RelicData> OnRelicEquipped;
    public event System.Action<RelicData> OnRelicUnequipped;
    public event System.Action<RelicSetBonus> OnSetBonusActivated;

    [System.Serializable]
    public class RelicData
    {
        public string relicId;
        public string nameKey;             // 本地化key
        public string descriptionKey;
        public Sprite icon;
        public RelicRarity rarity = RelicRarity.Common;
        public RelicOwner owner = RelicOwner.Shared;
        public string setId;               // 所属套装（空=无套装）

        // 效果
        public List<RelicEffect> effects = new List<RelicEffect>();

        // 获取条件
        public int requiredChapter;        // 在哪章可获取
        public string requiredLevelId;     // 具体关卡ID
    }

    [System.Serializable]
    public class RelicEffect
    {
        public RelicStat stat;
        public float value;
        public bool isPercentage;          // true=百分比加成, false=固定值
    }

    [System.Serializable]
    public class RelicSetBonus
    {
        public string setId;
        public string setNameKey;
        public List<string> relicIds;      // 套装所需遗物
        public int requiredCount;          // 激活所需数量（可以少于总数）
        public List<RelicEffect> bonusEffects;
    }

    public enum RelicRarity
    {
        Common,      // 普通
        Rare,        // 稀有
        Epic,        // 史诗
        Legendary    // 传说
    }

    public enum RelicOwner
    {
        Shared,      // 双人共享
        LuxOnly,     // Lux专属
        NoxOnly      // Nox专属
    }

    public enum RelicStat
    {
        // 通用
        MaxHealth,
        MovementSpeed,
        JumpForce,
        DamageBonus,
        DamageReduction,
        CooldownReduction,

        // 经济
        CoinBonus,
        ExperienceBonus,
        ScoreMultiplier,

        // 合作
        CoopMeterRate,
        ReviveSpeedBonus,
        SyncWindowBonus,

        // Lux专属
        LightRadius,
        LightBridgeDuration,
        LightBeamDamage,

        // Nox专属
        ShadowPhaseDuration,
        ShadowZoneRadius,
        DashDistance,

        // 特殊
        SecretDetectionRange,
        TrapResistance,
        ComboDecayReduction
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildRelicMap();
        LoadData();
        InitializeDefaultRelics();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 收集遗物
    /// </summary>
    public bool CollectRelic(string relicId)
    {
        if (collectedRelicIds.Contains(relicId)) return false;
        if (!relicMap.ContainsKey(relicId)) return false;

        collectedRelicIds.Add(relicId);
        var relic = relicMap[relicId];
        SaveData();

        // 通知
        OnRelicCollected?.Invoke(relic);

        EventBus.Publish(new HintRequestEvent
        {
            textKey = $"relic_{relicId}",
            fallbackText = $"发现遗物！",
            duration = 3f
        });

        // 音效与触觉
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("relic_found");
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Success();

        // 成就
        if (AchievementSystem.Instance != null)
        {
            AchievementSystem.Instance.UpdateProgress("relic_collector", 1);
            if (collectedRelicIds.Count >= 10)
                AchievementSystem.Instance.Unlock("relic_hunter");
            if (collectedRelicIds.Count >= allRelics.Count)
                AchievementSystem.Instance.Unlock("relic_master");
        }

        // 自动装备（如果有空位）
        if (equippedRelicIds.Count < maxEquippedRelics)
            EquipRelic(relicId);

        Debug.Log($"[Relic] Collected: {relicId} ({relic.rarity})");
        return true;
    }

    /// <summary>
    /// 装备遗物
    /// </summary>
    public bool EquipRelic(string relicId)
    {
        if (!collectedRelicIds.Contains(relicId)) return false;
        if (equippedRelicIds.Contains(relicId)) return false;
        if (equippedRelicIds.Count >= maxEquippedRelics) return false;

        equippedRelicIds.Add(relicId);
        bonusesDirty = true;
        SaveData();

        var relic = relicMap[relicId];
        OnRelicEquipped?.Invoke(relic);

        // 检查套装激活
        CheckSetBonuses();

        return true;
    }

    /// <summary>
    /// 卸下遗物
    /// </summary>
    public bool UnequipRelic(string relicId)
    {
        if (!equippedRelicIds.Contains(relicId)) return false;

        equippedRelicIds.Remove(relicId);
        bonusesDirty = true;
        SaveData();

        if (relicMap.TryGetValue(relicId, out var relic))
            OnRelicUnequipped?.Invoke(relic);

        return true;
    }

    /// <summary>
    /// 检查遗物是否已收集
    /// </summary>
    public bool IsCollected(string relicId) => collectedRelicIds.Contains(relicId);

    /// <summary>
    /// 检查遗物是否已装备
    /// </summary>
    public bool IsEquipped(string relicId) => equippedRelicIds.Contains(relicId);

    /// <summary>
    /// 获取某个属性的总加成值
    /// </summary>
    public float GetStatBonus(RelicStat stat)
    {
        if (bonusesDirty)
            RecalculateBonuses();

        return cachedBonuses.TryGetValue(stat, out float val) ? val : 0f;
    }

    /// <summary>
    /// 获取某个属性的乘数 (1 + 百分比加成总和)
    /// </summary>
    public float GetStatMultiplier(RelicStat stat)
    {
        return 1f + GetStatBonus(stat);
    }

    /// <summary>
    /// 获取所有已收集遗物
    /// </summary>
    public List<RelicData> GetCollectedRelics()
    {
        var result = new List<RelicData>();
        foreach (var id in collectedRelicIds)
        {
            if (relicMap.TryGetValue(id, out var relic))
                result.Add(relic);
        }
        return result;
    }

    /// <summary>
    /// 获取所有已装备遗物
    /// </summary>
    public List<RelicData> GetEquippedRelics()
    {
        var result = new List<RelicData>();
        foreach (var id in equippedRelicIds)
        {
            if (relicMap.TryGetValue(id, out var relic))
                result.Add(relic);
        }
        return result;
    }

    /// <summary>
    /// 获取指定章节可获取的遗物
    /// </summary>
    public List<RelicData> GetRelicsForChapter(int chapter)
    {
        return allRelics.FindAll(r => r.requiredChapter == chapter);
    }

    /// <summary>
    /// 获取激活的套装奖励
    /// </summary>
    public List<RelicSetBonus> GetActiveSetBonuses()
    {
        var active = new List<RelicSetBonus>();
        foreach (var set in relicSets)
        {
            int count = 0;
            foreach (var relicId in set.relicIds)
            {
                if (equippedRelicIds.Contains(relicId))
                    count++;
            }
            if (count >= set.requiredCount)
                active.Add(set);
        }
        return active;
    }

    /// <summary>
    /// 获取遗物数据
    /// </summary>
    public RelicData GetRelicData(string relicId)
    {
        return relicMap.TryGetValue(relicId, out var data) ? data : null;
    }

    // ==================== 内部方法 ====================

    private void RecalculateBonuses()
    {
        cachedBonuses.Clear();

        // 装备遗物效果
        foreach (var relicId in equippedRelicIds)
        {
            if (!relicMap.TryGetValue(relicId, out var relic)) continue;

            foreach (var effect in relic.effects)
            {
                if (!cachedBonuses.ContainsKey(effect.stat))
                    cachedBonuses[effect.stat] = 0f;

                cachedBonuses[effect.stat] += effect.value;
            }
        }

        // 套装加成
        foreach (var set in relicSets)
        {
            int count = 0;
            foreach (var relicId in set.relicIds)
            {
                if (equippedRelicIds.Contains(relicId))
                    count++;
            }

            if (count >= set.requiredCount && set.bonusEffects != null)
            {
                foreach (var effect in set.bonusEffects)
                {
                    if (!cachedBonuses.ContainsKey(effect.stat))
                        cachedBonuses[effect.stat] = 0f;

                    cachedBonuses[effect.stat] += effect.value;
                }
            }
        }

        bonusesDirty = false;
    }

    private void CheckSetBonuses()
    {
        foreach (var set in relicSets)
        {
            int count = 0;
            foreach (var relicId in set.relicIds)
            {
                if (equippedRelicIds.Contains(relicId))
                    count++;
            }

            if (count >= set.requiredCount)
            {
                OnSetBonusActivated?.Invoke(set);

                if (SoundFeedback.Instance != null)
                    SoundFeedback.Instance.Play("set_bonus_activate");
            }
        }
    }

    private void BuildRelicMap()
    {
        relicMap.Clear();
        foreach (var relic in allRelics)
        {
            if (!string.IsNullOrEmpty(relic.relicId))
                relicMap[relic.relicId] = relic;
        }
    }

    // ==================== 持久化 ====================

    private void SaveData()
    {
        var saveData = new RelicSaveData
        {
            collected = new List<string>(collectedRelicIds),
            equipped = new List<string>(equippedRelicIds)
        };
        string json = JsonUtility.ToJson(saveData);
        PlayerPrefs.SetString(COLLECTED_KEY, json);
        PlayerPrefs.Save();
    }

    private void LoadData()
    {
        if (!PlayerPrefs.HasKey(COLLECTED_KEY)) return;

        string json = PlayerPrefs.GetString(COLLECTED_KEY);
        var saveData = JsonUtility.FromJson<RelicSaveData>(json);
        if (saveData == null) return;

        if (saveData.collected != null)
        {
            foreach (var id in saveData.collected)
                collectedRelicIds.Add(id);
        }

        if (saveData.equipped != null)
        {
            foreach (var id in saveData.equipped)
            {
                if (collectedRelicIds.Contains(id))
                    equippedRelicIds.Add(id);
            }
        }

        bonusesDirty = true;
    }

    [System.Serializable]
    private class RelicSaveData
    {
        public List<string> collected;
        public List<string> equipped;
    }

    // ==================== 默认数据 ====================

    private void InitializeDefaultRelics()
    {
        if (allRelics.Count > 0) return;

        // ===== 第一章：光明森林 =====
        allRelics.Add(new RelicData
        {
            relicId = "dawn_crystal", nameKey = "relic_dawn_crystal",
            descriptionKey = "relic_dawn_crystal_desc",
            rarity = RelicRarity.Common, owner = RelicOwner.LuxOnly,
            setId = "light_set", requiredChapter = 1,
            effects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.LightRadius, value = 0.15f, isPercentage = true }
            }
        });

        allRelics.Add(new RelicData
        {
            relicId = "shadow_shard", nameKey = "relic_shadow_shard",
            descriptionKey = "relic_shadow_shard_desc",
            rarity = RelicRarity.Common, owner = RelicOwner.NoxOnly,
            setId = "shadow_set", requiredChapter = 1,
            effects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.ShadowPhaseDuration, value = 0.1f, isPercentage = true }
            }
        });

        allRelics.Add(new RelicData
        {
            relicId = "forest_amulet", nameKey = "relic_forest_amulet",
            descriptionKey = "relic_forest_amulet_desc",
            rarity = RelicRarity.Common, owner = RelicOwner.Shared,
            requiredChapter = 1,
            effects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.MaxHealth, value = 10f, isPercentage = false }
            }
        });

        // ===== 第二章：水晶洞窟 =====
        allRelics.Add(new RelicData
        {
            relicId = "crystal_lens", nameKey = "relic_crystal_lens",
            descriptionKey = "relic_crystal_lens_desc",
            rarity = RelicRarity.Rare, owner = RelicOwner.LuxOnly,
            setId = "light_set", requiredChapter = 2,
            effects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.LightBeamDamage, value = 0.2f, isPercentage = true },
                new RelicEffect { stat = RelicStat.LightBridgeDuration, value = 0.15f, isPercentage = true }
            }
        });

        allRelics.Add(new RelicData
        {
            relicId = "echo_stone", nameKey = "relic_echo_stone",
            descriptionKey = "relic_echo_stone_desc",
            rarity = RelicRarity.Rare, owner = RelicOwner.NoxOnly,
            setId = "shadow_set", requiredChapter = 2,
            effects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.DashDistance, value = 0.2f, isPercentage = true }
            }
        });

        allRelics.Add(new RelicData
        {
            relicId = "resonance_gem", nameKey = "relic_resonance_gem",
            descriptionKey = "relic_resonance_gem_desc",
            rarity = RelicRarity.Rare, owner = RelicOwner.Shared,
            setId = "harmony_set", requiredChapter = 2,
            effects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.CoopMeterRate, value = 0.15f, isPercentage = true },
                new RelicEffect { stat = RelicStat.SyncWindowBonus, value = 0.1f, isPercentage = true }
            }
        });

        // ===== 第三章：深渊 =====
        allRelics.Add(new RelicData
        {
            relicId = "abyssal_eye", nameKey = "relic_abyssal_eye",
            descriptionKey = "relic_abyssal_eye_desc",
            rarity = RelicRarity.Epic, owner = RelicOwner.Shared,
            requiredChapter = 3,
            effects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.SecretDetectionRange, value = 3f, isPercentage = false },
                new RelicEffect { stat = RelicStat.DamageReduction, value = 0.05f, isPercentage = true }
            }
        });

        allRelics.Add(new RelicData
        {
            relicId = "void_cloak", nameKey = "relic_void_cloak",
            descriptionKey = "relic_void_cloak_desc",
            rarity = RelicRarity.Epic, owner = RelicOwner.NoxOnly,
            setId = "shadow_set", requiredChapter = 3,
            effects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.ShadowZoneRadius, value = 0.25f, isPercentage = true },
                new RelicEffect { stat = RelicStat.TrapResistance, value = 0.15f, isPercentage = true }
            }
        });

        // ===== 第四章：天空城 =====
        allRelics.Add(new RelicData
        {
            relicId = "sky_feather", nameKey = "relic_sky_feather",
            descriptionKey = "relic_sky_feather_desc",
            rarity = RelicRarity.Epic, owner = RelicOwner.Shared,
            requiredChapter = 4,
            effects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.JumpForce, value = 0.12f, isPercentage = true },
                new RelicEffect { stat = RelicStat.MovementSpeed, value = 0.08f, isPercentage = true }
            }
        });

        allRelics.Add(new RelicData
        {
            relicId = "prism_crown", nameKey = "relic_prism_crown",
            descriptionKey = "relic_prism_crown_desc",
            rarity = RelicRarity.Epic, owner = RelicOwner.LuxOnly,
            setId = "light_set", requiredChapter = 4,
            effects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.LightRadius, value = 0.3f, isPercentage = true },
                new RelicEffect { stat = RelicStat.DamageBonus, value = 0.1f, isPercentage = true }
            }
        });

        // ===== 第五章：黄昏境界 =====
        allRelics.Add(new RelicData
        {
            relicId = "twilight_heart", nameKey = "relic_twilight_heart",
            descriptionKey = "relic_twilight_heart_desc",
            rarity = RelicRarity.Legendary, owner = RelicOwner.Shared,
            setId = "harmony_set", requiredChapter = 5,
            effects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.ReviveSpeedBonus, value = 0.3f, isPercentage = true },
                new RelicEffect { stat = RelicStat.CoopMeterRate, value = 0.2f, isPercentage = true },
                new RelicEffect { stat = RelicStat.ComboDecayReduction, value = 0.15f, isPercentage = true }
            }
        });

        allRelics.Add(new RelicData
        {
            relicId = "eclipse_blade", nameKey = "relic_eclipse_blade",
            descriptionKey = "relic_eclipse_blade_desc",
            rarity = RelicRarity.Legendary, owner = RelicOwner.Shared,
            requiredChapter = 5,
            effects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.DamageBonus, value = 0.2f, isPercentage = true },
                new RelicEffect { stat = RelicStat.CooldownReduction, value = 0.15f, isPercentage = true }
            }
        });

        // ===== 套装 =====
        relicSets.Add(new RelicSetBonus
        {
            setId = "light_set", setNameKey = "set_light",
            relicIds = new List<string> { "dawn_crystal", "crystal_lens", "prism_crown" },
            requiredCount = 2,
            bonusEffects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.LightBeamDamage, value = 0.15f, isPercentage = true },
                new RelicEffect { stat = RelicStat.LightRadius, value = 0.1f, isPercentage = true }
            }
        });

        relicSets.Add(new RelicSetBonus
        {
            setId = "shadow_set", setNameKey = "set_shadow",
            relicIds = new List<string> { "shadow_shard", "echo_stone", "void_cloak" },
            requiredCount = 2,
            bonusEffects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.ShadowPhaseDuration, value = 0.2f, isPercentage = true },
                new RelicEffect { stat = RelicStat.DashDistance, value = 0.1f, isPercentage = true }
            }
        });

        relicSets.Add(new RelicSetBonus
        {
            setId = "harmony_set", setNameKey = "set_harmony",
            relicIds = new List<string> { "resonance_gem", "twilight_heart" },
            requiredCount = 2,
            bonusEffects = new List<RelicEffect>
            {
                new RelicEffect { stat = RelicStat.CoopMeterRate, value = 0.25f, isPercentage = true },
                new RelicEffect { stat = RelicStat.ScoreMultiplier, value = 0.1f, isPercentage = true }
            }
        });

        BuildRelicMap();
    }
}
