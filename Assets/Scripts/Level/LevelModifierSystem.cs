using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 关卡修改器系统 - 为重玩关卡提供随机/可选修改器
/// 增加重玩价值和挑战性
/// 每日随机修改器组合 + 玩家自选修改器
/// 修改器影响得分乘数
/// </summary>
public class LevelModifierSystem : MonoBehaviour
{
    public static LevelModifierSystem Instance { get; private set; }

    [Header("修改器池")]
    [SerializeField] private List<ModifierData> allModifiers = new List<ModifierData>();

    [Header("每日设置")]
    [SerializeField] private int dailyModifierCount = 3;

    // 运行时
    private List<ModifierData> activeModifiers = new List<ModifierData>();
    private List<ModifierData> dailyModifiers = new List<ModifierData>();
    private int lastDailySeed = -1;
    private float currentScoreMultiplier = 1f;

    public IReadOnlyList<ModifierData> ActiveModifiers => activeModifiers;
    public float ScoreMultiplier => currentScoreMultiplier;
    public bool HasActiveModifiers => activeModifiers.Count > 0;

    public event System.Action<ModifierData> OnModifierActivated;
    public event System.Action OnModifiersCleared;
    public event System.Action<float> OnScoreMultiplierChanged;

    [System.Serializable]
    public class ModifierData
    {
        public string modifierId;
        public string nameKey;              // 本地化
        public string descriptionKey;
        public Sprite icon;
        public ModifierCategory category;
        public ModifierDifficulty difficulty;

        [Header("效果参数")]
        public ModifierType type;
        public float value;                 // 倍率或具体值

        [Header("得分影响")]
        public float scoreMultiplier = 1f;  // 对最终分数的乘数

        [Header("解锁条件")]
        public int requiredCompletions;     // 需要通关该关卡多少次
        public bool isSecret;               // 是否隐藏修改器
    }

    public enum ModifierCategory
    {
        Difficulty,      // 难度类
        Movement,        // 移动类
        Combat,          // 战斗类
        Economy,         // 经济类
        Visual,          // 视觉类
        Fun              // 趣味类
    }

    public enum ModifierDifficulty
    {
        Easy,       // 降低难度（分数惩罚）
        Normal,     // 中性
        Hard,       // 增加难度（分数加成）
        Extreme     // 极难（高分数加成）
    }

    public enum ModifierType
    {
        // 难度类
        EnemySpeedMultiplier,        // 敌人移速倍率
        EnemyHealthMultiplier,       // 敌人血量倍率
        EnemyDamageMultiplier,       // 敌人伤害倍率
        PlayerHealthMultiplier,      // 玩家血量倍率
        NoCheckpoints,               // 无存档点
        OneHitKill,                  // 一击必杀（敌我双方）
        TimerPressure,               // 全程限时

        // 移动类
        LowGravity,                  // 低重力
        HighGravity,                 // 高重力
        SpeedBoost,                  // 移速提升
        SlipperyFloors,              // 滑溜地面
        NoDoubleJump,                // 禁止二段跳
        InfiniteJump,                // 无限跳跃

        // 战斗类
        BigHead,                     // 大头模式（命中率提升）
        ExplosiveEnemies,            // 敌人爆炸死亡
        ReflectProjectiles,          // 弹射反弹
        NoCooldowns,                 // 无技能冷却
        DoubleDamage,                // 双倍伤害

        // 经济类
        DoubleCoins,                 // 双倍金币
        CoinMagnet,                  // 金币吸附
        NoCoinDrop,                  // 无金币掉落
        BonusCollectibles,           // 额外收集品

        // 视觉/趣味类
        MirrorMode,                  // 左右镜像
        TinyPlayers,                 // 迷你角色
        GiantPlayers,                // 巨大角色
        RetroFilter,                 // 复古画面滤镜
        Darkness,                    // 全程黑暗（只有角色附近有光）
        Inverted                     // 颜色反转
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        InitializeDefaultModifiers();
    }

    void Start()
    {
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);

        RefreshDailyModifiers();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
        if (Instance == this) Instance = null;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 激活修改器
    /// </summary>
    public bool ActivateModifier(string modifierId)
    {
        var modifier = allModifiers.Find(m => m.modifierId == modifierId);
        if (modifier == null) return false;
        if (activeModifiers.Contains(modifier)) return false;

        // 检查冲突（同类型不可叠加）
        if (activeModifiers.Exists(m => m.type == modifier.type))
            return false;

        activeModifiers.Add(modifier);
        RecalculateScoreMultiplier();

        OnModifierActivated?.Invoke(modifier);

        Debug.Log($"[LevelMod] Activated: {modifierId} (score x{currentScoreMultiplier:F2})");
        return true;
    }

    /// <summary>
    /// 取消修改器
    /// </summary>
    public bool DeactivateModifier(string modifierId)
    {
        int idx = activeModifiers.FindIndex(m => m.modifierId == modifierId);
        if (idx < 0) return false;

        activeModifiers.RemoveAt(idx);
        RecalculateScoreMultiplier();
        return true;
    }

    /// <summary>
    /// 清除所有激活的修改器
    /// </summary>
    public void ClearAllModifiers()
    {
        activeModifiers.Clear();
        currentScoreMultiplier = 1f;
        OnModifiersCleared?.Invoke();
        OnScoreMultiplierChanged?.Invoke(1f);
    }

    /// <summary>
    /// 获取今日每日修改器
    /// </summary>
    public List<ModifierData> GetDailyModifiers()
    {
        RefreshDailyModifiers();
        return new List<ModifierData>(dailyModifiers);
    }

    /// <summary>
    /// 激活今日每日修改器组合
    /// </summary>
    public void ActivateDailySet()
    {
        ClearAllModifiers();
        foreach (var mod in dailyModifiers)
        {
            ActivateModifier(mod.modifierId);
        }
    }

    /// <summary>
    /// 获取某类修改器
    /// </summary>
    public List<ModifierData> GetModifiersByCategory(ModifierCategory category)
    {
        return allModifiers.FindAll(m => m.category == category && !m.isSecret);
    }

    /// <summary>
    /// 获取所有可用修改器（非隐藏）
    /// </summary>
    public List<ModifierData> GetAvailableModifiers()
    {
        return allModifiers.FindAll(m => !m.isSecret);
    }

    /// <summary>
    /// 检查某个修改器是否激活
    /// </summary>
    public bool IsModifierActive(ModifierType type)
    {
        return activeModifiers.Exists(m => m.type == type);
    }

    /// <summary>
    /// 获取修改器的值（如果激活）
    /// </summary>
    public float GetModifierValue(ModifierType type)
    {
        var mod = activeModifiers.Find(m => m.type == type);
        return mod?.value ?? 0f;
    }

    /// <summary>
    /// 获取敌人速度乘数（综合所有激活修改器）
    /// </summary>
    public float GetEnemySpeedMultiplier()
    {
        float mult = 1f;
        foreach (var mod in activeModifiers)
        {
            if (mod.type == ModifierType.EnemySpeedMultiplier)
                mult *= mod.value;
        }
        return mult;
    }

    /// <summary>
    /// 获取敌人血量乘数
    /// </summary>
    public float GetEnemyHealthMultiplier()
    {
        float mult = 1f;
        foreach (var mod in activeModifiers)
        {
            if (mod.type == ModifierType.EnemyHealthMultiplier)
                mult *= mod.value;
        }
        return mult;
    }

    /// <summary>
    /// 获取玩家伤害乘数
    /// </summary>
    public float GetPlayerDamageMultiplier()
    {
        float mult = 1f;
        foreach (var mod in activeModifiers)
        {
            if (mod.type == ModifierType.DoubleDamage)
                mult *= mod.value;
            if (mod.type == ModifierType.OneHitKill)
                mult = 9999f;
        }
        return mult;
    }

    /// <summary>
    /// 获取重力乘数
    /// </summary>
    public float GetGravityMultiplier()
    {
        foreach (var mod in activeModifiers)
        {
            if (mod.type == ModifierType.LowGravity) return mod.value;
            if (mod.type == ModifierType.HighGravity) return mod.value;
        }
        return 1f;
    }

    /// <summary>
    /// 获取金币乘数
    /// </summary>
    public float GetCoinMultiplier()
    {
        float mult = 1f;
        foreach (var mod in activeModifiers)
        {
            if (mod.type == ModifierType.DoubleCoins) mult *= mod.value;
            if (mod.type == ModifierType.NoCoinDrop) mult = 0f;
        }
        return mult;
    }

    /// <summary>
    /// 获取角色缩放
    /// </summary>
    public float GetPlayerScale()
    {
        foreach (var mod in activeModifiers)
        {
            if (mod.type == ModifierType.TinyPlayers) return mod.value;
            if (mod.type == ModifierType.GiantPlayers) return mod.value;
        }
        return 1f;
    }

    // ==================== 事件处理 ====================

    private void OnLevelStart(LevelStartEvent e)
    {
        // 应用活动修改器到游戏系统
        ApplyActiveModifiers();
    }

    private void OnLevelComplete(LevelCompleteEvent e)
    {
        // 关卡结束后清除修改器效果
        // 修改器本身保留以供查看最终分数
    }

    // ==================== 内部方法 ====================

    private void ApplyActiveModifiers()
    {
        foreach (var mod in activeModifiers)
        {
            switch (mod.type)
            {
                case ModifierType.SpeedBoost:
                    // 通过PlayerController查询此系统应用
                    break;
                case ModifierType.Darkness:
                    if (EnvironmentEffectManager.Instance != null)
                        EnvironmentEffectManager.Instance.TransitionToDarkness(0.5f);
                    break;
                case ModifierType.LowGravity:
                case ModifierType.HighGravity:
                    Physics2D.gravity = new Vector2(0, -9.81f * mod.value);
                    break;
            }
        }
    }

    private void RecalculateScoreMultiplier()
    {
        currentScoreMultiplier = 1f;
        foreach (var mod in activeModifiers)
        {
            currentScoreMultiplier *= mod.scoreMultiplier;
        }
        OnScoreMultiplierChanged?.Invoke(currentScoreMultiplier);
    }

    private void RefreshDailyModifiers()
    {
        // 每天一个seed
        int todaySeed = System.DateTime.Now.Year * 10000 +
                        System.DateTime.Now.Month * 100 +
                        System.DateTime.Now.Day;

        if (todaySeed == lastDailySeed) return;
        lastDailySeed = todaySeed;

        dailyModifiers.Clear();
        var available = allModifiers.FindAll(m => !m.isSecret);
        if (available.Count == 0) return;

        var rng = new System.Random(todaySeed);
        var shuffled = new List<ModifierData>(available);

        // Fisher-Yates shuffle
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        // 选取不冲突的修改器
        var usedTypes = new HashSet<ModifierType>();
        foreach (var mod in shuffled)
        {
            if (dailyModifiers.Count >= dailyModifierCount) break;
            if (usedTypes.Contains(mod.type)) continue;

            dailyModifiers.Add(mod);
            usedTypes.Add(mod.type);
        }
    }

    // ==================== 默认修改器 ====================

    private void InitializeDefaultModifiers()
    {
        if (allModifiers.Count > 0) return;

        // === 难度类 ===
        allModifiers.Add(new ModifierData
        {
            modifierId = "fast_enemies", nameKey = "mod_fast_enemies",
            descriptionKey = "mod_fast_enemies_desc",
            category = ModifierCategory.Difficulty,
            difficulty = ModifierDifficulty.Hard,
            type = ModifierType.EnemySpeedMultiplier, value = 1.5f,
            scoreMultiplier = 1.3f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "tanky_enemies", nameKey = "mod_tanky_enemies",
            descriptionKey = "mod_tanky_enemies_desc",
            category = ModifierCategory.Difficulty,
            difficulty = ModifierDifficulty.Hard,
            type = ModifierType.EnemyHealthMultiplier, value = 2f,
            scoreMultiplier = 1.4f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "one_hit", nameKey = "mod_one_hit",
            descriptionKey = "mod_one_hit_desc",
            category = ModifierCategory.Difficulty,
            difficulty = ModifierDifficulty.Extreme,
            type = ModifierType.OneHitKill, value = 1f,
            scoreMultiplier = 2.0f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "no_checkpoints", nameKey = "mod_no_checkpoints",
            descriptionKey = "mod_no_checkpoints_desc",
            category = ModifierCategory.Difficulty,
            difficulty = ModifierDifficulty.Extreme,
            type = ModifierType.NoCheckpoints, value = 1f,
            scoreMultiplier = 1.8f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "glass_cannon", nameKey = "mod_glass_cannon",
            descriptionKey = "mod_glass_cannon_desc",
            category = ModifierCategory.Difficulty,
            difficulty = ModifierDifficulty.Hard,
            type = ModifierType.PlayerHealthMultiplier, value = 0.5f,
            scoreMultiplier = 1.5f
        });

        // === 移动类 ===
        allModifiers.Add(new ModifierData
        {
            modifierId = "low_gravity", nameKey = "mod_low_gravity",
            descriptionKey = "mod_low_gravity_desc",
            category = ModifierCategory.Movement,
            difficulty = ModifierDifficulty.Normal,
            type = ModifierType.LowGravity, value = 0.5f,
            scoreMultiplier = 1.0f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "high_gravity", nameKey = "mod_high_gravity",
            descriptionKey = "mod_high_gravity_desc",
            category = ModifierCategory.Movement,
            difficulty = ModifierDifficulty.Hard,
            type = ModifierType.HighGravity, value = 1.8f,
            scoreMultiplier = 1.3f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "speed_boost", nameKey = "mod_speed_boost",
            descriptionKey = "mod_speed_boost_desc",
            category = ModifierCategory.Movement,
            difficulty = ModifierDifficulty.Easy,
            type = ModifierType.SpeedBoost, value = 1.4f,
            scoreMultiplier = 0.9f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "slippery", nameKey = "mod_slippery",
            descriptionKey = "mod_slippery_desc",
            category = ModifierCategory.Movement,
            difficulty = ModifierDifficulty.Hard,
            type = ModifierType.SlipperyFloors, value = 0.1f,
            scoreMultiplier = 1.25f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "infinite_jump", nameKey = "mod_infinite_jump",
            descriptionKey = "mod_infinite_jump_desc",
            category = ModifierCategory.Movement,
            difficulty = ModifierDifficulty.Easy,
            type = ModifierType.InfiniteJump, value = 1f,
            scoreMultiplier = 0.7f
        });

        // === 战斗类 ===
        allModifiers.Add(new ModifierData
        {
            modifierId = "double_damage", nameKey = "mod_double_damage",
            descriptionKey = "mod_double_damage_desc",
            category = ModifierCategory.Combat,
            difficulty = ModifierDifficulty.Easy,
            type = ModifierType.DoubleDamage, value = 2f,
            scoreMultiplier = 0.8f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "explosive_enemies", nameKey = "mod_explosive",
            descriptionKey = "mod_explosive_desc",
            category = ModifierCategory.Combat,
            difficulty = ModifierDifficulty.Hard,
            type = ModifierType.ExplosiveEnemies, value = 1f,
            scoreMultiplier = 1.2f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "no_cooldowns", nameKey = "mod_no_cooldowns",
            descriptionKey = "mod_no_cooldowns_desc",
            category = ModifierCategory.Combat,
            difficulty = ModifierDifficulty.Easy,
            type = ModifierType.NoCooldowns, value = 1f,
            scoreMultiplier = 0.75f
        });

        // === 经济类 ===
        allModifiers.Add(new ModifierData
        {
            modifierId = "double_coins", nameKey = "mod_double_coins",
            descriptionKey = "mod_double_coins_desc",
            category = ModifierCategory.Economy,
            difficulty = ModifierDifficulty.Normal,
            type = ModifierType.DoubleCoins, value = 2f,
            scoreMultiplier = 1.0f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "coin_magnet", nameKey = "mod_coin_magnet",
            descriptionKey = "mod_coin_magnet_desc",
            category = ModifierCategory.Economy,
            difficulty = ModifierDifficulty.Normal,
            type = ModifierType.CoinMagnet, value = 5f,
            scoreMultiplier = 1.0f
        });

        // === 趣味类 ===
        allModifiers.Add(new ModifierData
        {
            modifierId = "mirror_mode", nameKey = "mod_mirror",
            descriptionKey = "mod_mirror_desc",
            category = ModifierCategory.Fun,
            difficulty = ModifierDifficulty.Hard,
            type = ModifierType.MirrorMode, value = 1f,
            scoreMultiplier = 1.5f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "tiny_players", nameKey = "mod_tiny",
            descriptionKey = "mod_tiny_desc",
            category = ModifierCategory.Fun,
            difficulty = ModifierDifficulty.Normal,
            type = ModifierType.TinyPlayers, value = 0.5f,
            scoreMultiplier = 1.1f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "giant_players", nameKey = "mod_giant",
            descriptionKey = "mod_giant_desc",
            category = ModifierCategory.Fun,
            difficulty = ModifierDifficulty.Normal,
            type = ModifierType.GiantPlayers, value = 2f,
            scoreMultiplier = 0.9f
        });

        allModifiers.Add(new ModifierData
        {
            modifierId = "darkness_mode", nameKey = "mod_darkness",
            descriptionKey = "mod_darkness_desc",
            category = ModifierCategory.Visual,
            difficulty = ModifierDifficulty.Extreme,
            type = ModifierType.Darkness, value = 1f,
            scoreMultiplier = 1.6f
        });
    }
}
