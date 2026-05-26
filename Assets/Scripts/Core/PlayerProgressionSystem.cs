using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 玩家成长系统 - 管理经验值、等级和天赋点
/// 经验通过击杀敌人、解谜、收集品获得
/// 每次升级获得天赋点，用于解锁SkillUpgradeSystem中的技能
/// 双角色共享等级但各自独立天赋树
/// </summary>
public class PlayerProgressionSystem : MonoBehaviour
{
    public static PlayerProgressionSystem Instance { get; private set; }

    [Header("等级设置")]
    [SerializeField] private int maxLevel = 30;
    [SerializeField] private float baseExpForLevel = 100f;
    [SerializeField] private float expScalePerLevel = 1.3f;      // 每级经验需求倍率
    [SerializeField] private int talentPointsPerLevel = 1;
    [SerializeField] private int bonusTalentAtMilestone = 2;     // 里程碑等级额外点数
    [SerializeField] private int[] milestoneLevels = { 5, 10, 15, 20, 25, 30 };

    [Header("经验来源")]
    [SerializeField] private float expPerEnemyKill = 15f;
    [SerializeField] private float expPerBossKill = 200f;
    [SerializeField] private float expPerPuzzleSolve = 25f;
    [SerializeField] private float expPerCollectible = 5f;
    [SerializeField] private float expPerSecretFound = 50f;
    [SerializeField] private float expPerLevelComplete = 100f;
    [SerializeField] private float expComboMultiplierRate = 0.01f; // 连击乘数（每连击+1%）

    // 运行时
    private int currentLevel = 1;
    private float currentExp;
    private int luxTalentPoints;
    private int noxTalentPoints;
    private float expMultiplier = 1f;

    // 持久化
    private const string SAVE_KEY = "player_progression";

    public int CurrentLevel => currentLevel;
    public float CurrentExp => currentExp;
    public float ExpForNextLevel => GetExpRequirement(currentLevel);
    public float ExpProgress => ExpForNextLevel > 0 ? currentExp / ExpForNextLevel : 1f;
    public int LuxTalentPoints => luxTalentPoints;
    public int NoxTalentPoints => noxTalentPoints;
    public bool IsMaxLevel => currentLevel >= maxLevel;

    public event System.Action<int> OnLevelUp;                // newLevel
    public event System.Action<float, string> OnExpGained;    // amount, source
    public event System.Action<int, int> OnTalentPointsChanged; // lux, nox

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadProgress();
    }

    void Start()
    {
        // 订阅EventBus事件获取经验
        EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Subscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Subscribe<CollectiblePickedUpEvent>(OnCollectiblePickedUp);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Unsubscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Unsubscribe<CollectiblePickedUpEvent>(OnCollectiblePickedUp);
        if (Instance == this) Instance = null;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 添加经验值
    /// </summary>
    public void AddExp(float amount, string source = "")
    {
        if (IsMaxLevel) return;

        // 应用遗物加成
        if (RelicSystem.Instance != null)
            amount *= RelicSystem.Instance.GetStatMultiplier(RelicSystem.RelicStat.ExperienceBonus);

        // 应用全局倍率
        amount *= expMultiplier;

        // 连击加成
        if (ComboSystem.Instance != null)
        {
            int combo = ComboSystem.Instance.CurrentCombo;
            amount *= (1f + combo * expComboMultiplierRate);
        }

        currentExp += amount;
        OnExpGained?.Invoke(amount, source);

        // 检查升级
        while (currentExp >= ExpForNextLevel && !IsMaxLevel)
        {
            LevelUpInternal();
        }

        SaveProgress();
    }

    /// <summary>
    /// 消耗天赋点（用于技能解锁）
    /// </summary>
    public bool SpendTalentPoint(PlayerController.PlayerType playerType)
    {
        if (playerType == PlayerController.PlayerType.Lux)
        {
            if (luxTalentPoints <= 0) return false;
            luxTalentPoints--;
        }
        else
        {
            if (noxTalentPoints <= 0) return false;
            noxTalentPoints--;
        }

        OnTalentPointsChanged?.Invoke(luxTalentPoints, noxTalentPoints);
        SaveProgress();
        return true;
    }

    /// <summary>
    /// 获取指定等级所需经验
    /// </summary>
    public float GetExpRequirement(int level)
    {
        if (level >= maxLevel) return 0;
        return baseExpForLevel * Mathf.Pow(expScalePerLevel, level - 1);
    }

    /// <summary>
    /// 获取等级统计信息
    /// </summary>
    public string GetProgressSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Lv.{currentLevel}");
        if (!IsMaxLevel)
            sb.AppendLine($"EXP: {currentExp:F0}/{ExpForNextLevel:F0}");
        else
            sb.AppendLine("MAX LEVEL");
        sb.AppendLine($"Lux Points: {luxTalentPoints}");
        sb.AppendLine($"Nox Points: {noxTalentPoints}");
        return sb.ToString();
    }

    /// <summary>
    /// 设置经验倍率（活动/促销期间）
    /// </summary>
    public void SetExpMultiplier(float multiplier)
    {
        expMultiplier = Mathf.Max(0.1f, multiplier);
    }

    // ==================== 事件处理 ====================

    private void OnEnemyDefeated(EnemyDefeatedEvent e)
    {
        float exp = e.isBoss ? expPerBossKill : expPerEnemyKill;
        AddExp(exp, e.isBoss ? "boss_kill" : "enemy_kill");
    }

    private void OnPuzzleSolved(PuzzleSolvedEvent e)
    {
        AddExp(expPerPuzzleSolve, "puzzle");
    }

    private void OnLevelComplete(LevelCompleteEvent e)
    {
        float exp = expPerLevelComplete;

        // 星级加成
        exp += e.stars * 20f;

        AddExp(exp, "level_complete");
    }

    private void OnCollectiblePickedUp(CollectiblePickedUpEvent e)
    {
        AddExp(expPerCollectible, "collectible");
    }

    // ==================== 升级逻辑 ====================

    private void LevelUpInternal()
    {
        currentExp -= ExpForNextLevel;
        currentLevel++;

        // 天赋点奖励
        int points = talentPointsPerLevel;
        bool isMilestone = System.Array.IndexOf(milestoneLevels, currentLevel) >= 0;
        if (isMilestone)
            points += bonusTalentAtMilestone;

        luxTalentPoints += points;
        noxTalentPoints += points;

        OnLevelUp?.Invoke(currentLevel);
        OnTalentPointsChanged?.Invoke(luxTalentPoints, noxTalentPoints);

        // 通知UI
        EventBus.Publish(new HintRequestEvent
        {
            textKey = "level_up",
            fallbackText = $"Level Up! Lv.{currentLevel}",
            duration = 3f
        });

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("level_up");
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Success();

        // 成就
        if (AchievementSystem.Instance != null)
        {
            if (currentLevel >= 10)
                AchievementSystem.Instance.Unlock("level_10");
            if (currentLevel >= 20)
                AchievementSystem.Instance.Unlock("level_20");
            if (currentLevel >= maxLevel)
                AchievementSystem.Instance.Unlock("max_level");
        }

        // 触发技能检查
        if (SkillUpgradeSystem.Instance != null)
            SkillUpgradeSystem.Instance.CheckUnlocks();

        Debug.Log($"[Progression] Level up! Lv.{currentLevel} (+{points} talent points)");
    }

    // ==================== 持久化 ====================

    private void SaveProgress()
    {
        var data = new ProgressionSaveData
        {
            level = currentLevel,
            exp = currentExp,
            luxPoints = luxTalentPoints,
            noxPoints = noxTalentPoints
        };
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    private void LoadProgress()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY)) return;

        string json = PlayerPrefs.GetString(SAVE_KEY);
        var data = JsonUtility.FromJson<ProgressionSaveData>(json);
        if (data == null) return;

        currentLevel = Mathf.Max(1, data.level);
        currentExp = Mathf.Max(0, data.exp);
        luxTalentPoints = Mathf.Max(0, data.luxPoints);
        noxTalentPoints = Mathf.Max(0, data.noxPoints);
    }

    [System.Serializable]
    private class ProgressionSaveData
    {
        public int level;
        public float exp;
        public int luxPoints;
        public int noxPoints;
    }
}

/// <summary>
/// 收集品拾取事件（在Core中定义供跨程序集使用）
/// </summary>
public struct CollectiblePickedUpEvent : IGameEvent
{
    public string collectibleType;   // coin, gem, hidden, star
    public int value;
    public UnityEngine.Vector3 position;
}
