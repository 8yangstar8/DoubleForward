using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 新游戏+管理器 - 通关后开启的二周目模式
/// 保留遗物、等级、技能，但增强敌人难度
/// 解锁新的隐藏内容和Boss变体
/// 支持最多NG+3
/// </summary>
public class NewGamePlusManager : MonoBehaviour
{
    public static NewGamePlusManager Instance { get; private set; }

    [Header("NG+设置")]
    [SerializeField] private int maxNewGamePlusLevel = 3;

    [Header("难度缩放")]
    [SerializeField] private float enemyHealthScalePerNG = 0.5f;      // +50%/周目
    [SerializeField] private float enemyDamageScalePerNG = 0.3f;      // +30%/周目
    [SerializeField] private float enemySpeedScalePerNG = 0.1f;       // +10%/周目

    [Header("奖励缩放")]
    [SerializeField] private float expBonusPerNG = 0.25f;             // +25%/周目
    [SerializeField] private float coinBonusPerNG = 0.3f;             // +30%/周目
    [SerializeField] private float scoreBonusPerNG = 0.5f;            // +50%/周目

    [Header("新内容")]
    [SerializeField] private List<NGPlusContent> additionalContent = new List<NGPlusContent>();

    // 运行时
    private int currentNGLevel;                      // 0=一周目, 1=NG+1, 2=NG+2, 3=NG+3
    private const string NG_LEVEL_KEY = "ng_plus_level";
    private const string NG_UNLOCK_KEY = "ng_plus_unlocked";

    public int CurrentNGLevel => currentNGLevel;
    public bool IsNewGamePlus => currentNGLevel > 0;
    public bool CanStartNewGamePlus => currentNGLevel < maxNewGamePlusLevel && HasCompletedGame();
    public string NGDisplayName => currentNGLevel == 0 ? "" : $"NG+{currentNGLevel}";

    // 难度乘数
    public float EnemyHealthMultiplier => 1f + currentNGLevel * enemyHealthScalePerNG;
    public float EnemyDamageMultiplier => 1f + currentNGLevel * enemyDamageScalePerNG;
    public float EnemySpeedMultiplier => 1f + currentNGLevel * enemySpeedScalePerNG;
    public float ExpMultiplier => 1f + currentNGLevel * expBonusPerNG;
    public float CoinMultiplier => 1f + currentNGLevel * coinBonusPerNG;
    public float ScoreMultiplier => 1f + currentNGLevel * scoreBonusPerNG;

    public event System.Action<int> OnNewGamePlusStarted;   // ngLevel
    public event System.Action<int> OnNGContentUnlocked;    // contentIndex

    [System.Serializable]
    public class NGPlusContent
    {
        public string contentId;
        public string descriptionKey;
        public int requiredNGLevel;     // 需要NG+几才能解锁
        public ContentType type;

        public enum ContentType
        {
            BossVariant,         // Boss变体（新攻击模式）
            HiddenLevel,         // 隐藏关卡
            AlternateEnding,     // 替代结局
            CosmeticReward,      // 外观奖励
            NewRelic,            // 新遗物
            ExtraDifficulty      // 额外难度选项
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadNGLevel();
        InitializeDefaultContent();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 开始新游戏+
    /// </summary>
    public bool StartNewGamePlus()
    {
        if (!CanStartNewGamePlus) return false;

        currentNGLevel++;
        PlayerPrefs.SetInt(NG_LEVEL_KEY, currentNGLevel);
        PlayerPrefs.SetInt(NG_UNLOCK_KEY, 1);

        // 保留数据：遗物、等级、技能
        // 重置数据：关卡进度、隐藏区域发现、Boss击杀记录
        ResetProgressForNG();

        // 应用NG+难度
        ApplyNGSettings();

        OnNewGamePlusStarted?.Invoke(currentNGLevel);

        // 解锁NG+内容
        CheckContentUnlocks();

        // 成就
        if (AchievementSystem.Instance != null)
        {
            AchievementSystem.Instance.Unlock($"ng_plus_{currentNGLevel}");
            if (currentNGLevel >= maxNewGamePlusLevel)
                AchievementSystem.Instance.Unlock("ng_plus_max");
        }

        // 通知
        EventBus.Publish(new HintRequestEvent
        {
            textKey = "ng_plus_started",
            fallbackText = $"New Game +{currentNGLevel} Started!",
            duration = 5f
        });

        PlayerPrefs.Save();
        Debug.Log($"[NG+] Started NG+{currentNGLevel}");
        return true;
    }

    /// <summary>
    /// 检查是否已通关
    /// </summary>
    public bool HasCompletedGame()
    {
        return PlayerPrefs.GetInt("best_ending", 0) > 0;
    }

    /// <summary>
    /// 获取当前NG+等级的解锁内容
    /// </summary>
    public List<NGPlusContent> GetUnlockedContent()
    {
        return additionalContent.FindAll(c => c.requiredNGLevel <= currentNGLevel);
    }

    /// <summary>
    /// 检查特定内容是否解锁
    /// </summary>
    public bool IsContentUnlocked(string contentId)
    {
        var content = additionalContent.Find(c => c.contentId == contentId);
        return content != null && content.requiredNGLevel <= currentNGLevel;
    }

    /// <summary>
    /// 获取NG+难度信息文本
    /// </summary>
    public string GetDifficultyInfo()
    {
        if (!IsNewGamePlus) return "";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"NG+{currentNGLevel}");
        sb.AppendLine($"Enemy HP: x{EnemyHealthMultiplier:F1}");
        sb.AppendLine($"Enemy DMG: x{EnemyDamageMultiplier:F1}");
        sb.AppendLine($"EXP Bonus: +{(currentNGLevel * expBonusPerNG * 100):F0}%");
        sb.AppendLine($"Score Bonus: +{(currentNGLevel * scoreBonusPerNG * 100):F0}%");
        return sb.ToString();
    }

    // ==================== 内部方法 ====================

    private void ResetProgressForNG()
    {
        // 重置关卡进度（保留存档系统中的等级/技能/遗物）
        if (SaveSystem.Instance != null)
        {
            var data = SaveSystem.Instance.Data;
            // 重置关卡完成状态
            if (data.levelsCompleted != null)
            {
                for (int i = 0; i < data.levelsCompleted.Length; i++)
                    data.levelsCompleted[i] = false;
            }
            // 重置星级
            if (data.levelStars != null)
            {
                for (int i = 0; i < data.levelStars.Length; i++)
                    data.levelStars[i] = 0;
            }
            data.lastChapter = 1;
            data.lastLevel = 1;
            data.totalStars = 0;
            SaveSystem.Instance.Save();
        }

        // 保留SecretAreaSystem数据（NG+可以重新发现获取奖励）
        PlayerPrefs.DeleteKey("discovered_secrets");
    }

    private void ApplyNGSettings()
    {
        // 设置经验倍率
        if (PlayerProgressionSystem.Instance != null)
            PlayerProgressionSystem.Instance.SetExpMultiplier(ExpMultiplier);

        // EnemyDirector会通过查询此系统获取难度乘数
    }

    private void CheckContentUnlocks()
    {
        for (int i = 0; i < additionalContent.Count; i++)
        {
            if (additionalContent[i].requiredNGLevel == currentNGLevel)
            {
                OnNGContentUnlocked?.Invoke(i);

                EventBus.Publish(new HintRequestEvent
                {
                    textKey = additionalContent[i].descriptionKey,
                    fallbackText = $"New content unlocked!",
                    duration = 3f
                });
            }
        }
    }

    private void LoadNGLevel()
    {
        currentNGLevel = PlayerPrefs.GetInt(NG_LEVEL_KEY, 0);
    }

    // ==================== 默认内容 ====================

    private void InitializeDefaultContent()
    {
        if (additionalContent.Count > 0) return;

        additionalContent.Add(new NGPlusContent
        {
            contentId = "boss_variant_1",
            descriptionKey = "ng_boss_variant_forest",
            requiredNGLevel = 1,
            type = NGPlusContent.ContentType.BossVariant
        });

        additionalContent.Add(new NGPlusContent
        {
            contentId = "hidden_level_abyss",
            descriptionKey = "ng_hidden_abyss",
            requiredNGLevel = 1,
            type = NGPlusContent.ContentType.HiddenLevel
        });

        additionalContent.Add(new NGPlusContent
        {
            contentId = "relic_ng_eclipse",
            descriptionKey = "ng_relic_eclipse",
            requiredNGLevel = 1,
            type = NGPlusContent.ContentType.NewRelic
        });

        additionalContent.Add(new NGPlusContent
        {
            contentId = "boss_variant_all",
            descriptionKey = "ng_all_boss_variants",
            requiredNGLevel = 2,
            type = NGPlusContent.ContentType.BossVariant
        });

        additionalContent.Add(new NGPlusContent
        {
            contentId = "true_ending",
            descriptionKey = "ng_true_ending",
            requiredNGLevel = 2,
            type = NGPlusContent.ContentType.AlternateEnding
        });

        additionalContent.Add(new NGPlusContent
        {
            contentId = "skin_golden",
            descriptionKey = "ng_golden_skins",
            requiredNGLevel = 2,
            type = NGPlusContent.ContentType.CosmeticReward
        });

        additionalContent.Add(new NGPlusContent
        {
            contentId = "chaos_mode",
            descriptionKey = "ng_chaos_mode",
            requiredNGLevel = 3,
            type = NGPlusContent.ContentType.ExtraDifficulty
        });

        additionalContent.Add(new NGPlusContent
        {
            contentId = "skin_cosmic",
            descriptionKey = "ng_cosmic_skins",
            requiredNGLevel = 3,
            type = NGPlusContent.ContentType.CosmeticReward
        });
    }
}
