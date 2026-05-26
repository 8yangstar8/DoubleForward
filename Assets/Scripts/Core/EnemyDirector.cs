using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 敌人导演系统 - 动态控制敌人强度和行为密度
/// 根据玩家表现（连击数、血量、死亡次数）实时调整
/// 确保战斗节奏适合玩家水平
/// </summary>
public class EnemyDirector : MonoBehaviour
{
    public static EnemyDirector Instance { get; private set; }

    [Header("敌人预算系统")]
    [SerializeField] private float baseBudgetPerMinute = 5f;      // 基础每分钟敌人预算
    [SerializeField] private float maxBudgetMultiplier = 2f;       // 最大预算倍率
    [SerializeField] private float minBudgetMultiplier = 0.5f;     // 最小预算倍率

    [Header("难度响应")]
    [SerializeField] private float skillRatingDecay = 0.01f;       // 技能评分自然衰减
    [SerializeField] private float comboSkillBonus = 0.05f;        // 连击数增加技能评分
    [SerializeField] private float deathSkillPenalty = 0.15f;      // 死亡降低技能评分
    [SerializeField] private float damageSkillPenalty = 0.02f;     // 受伤降低技能评分

    [Header("敌人行为修正")]
    [SerializeField] private float aggressionRange = 0.5f;         // 攻击性调整范围（±）
    [SerializeField] private float reactionTimeAdjust = 0.3f;      // 反应时间调整（秒）

    [Header("安全阀")]
    [SerializeField] private float lowHealthThreshold = 0.25f;     // 低血量阈值
    [SerializeField] private float lowHealthAggressionCut = 0.5f;  // 低血量时敌人攻击减半
    [SerializeField] private int maxActiveEnemies = 8;             // 同时活跃敌人上限
    [SerializeField] private float respiteAfterDeath = 5f;         // 死亡后暂缓敌人时间

    // 运行时
    private float skillRating = 0.5f;       // 0=新手 1=高手
    private float currentBudget;
    private float budgetTimer;
    private float respiteTimer;
    private float aggressionModifier;
    private float reactionModifier;

    // 统计追踪
    private int enemiesDefeatedThisLevel;
    private int deathsThisLevel;
    private float totalDamageTaken;
    private int highestCombo;
    private float levelStartTime;

    // 活跃敌人追踪
    private List<EnemyBase> activeEnemies = new List<EnemyBase>();

    public float SkillRating => skillRating;
    public float AggressionModifier => aggressionModifier;
    public float ReactionModifier => reactionModifier;
    public int ActiveEnemyCount => activeEnemies.Count;
    public float BudgetMultiplier => Mathf.Lerp(minBudgetMultiplier, maxBudgetMultiplier, skillRating);

    public event System.Action<float> OnSkillRatingChanged;     // newRating
    public event System.Action<float> OnAggressionChanged;      // newAggression

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        LoadSkillRating();
    }

    void Start()
    {
        EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Subscribe<ComboChangedEvent>(OnComboChanged);
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Unsubscribe<ComboChangedEvent>(OnComboChanged);
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);

        SaveSkillRating();
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // 自然衰减（缓慢回归中间值）
        float decayDir = skillRating > 0.5f ? -1f : 1f;
        skillRating += decayDir * skillRatingDecay * Time.deltaTime;
        skillRating = Mathf.Clamp01(skillRating);

        // 暂缓计时
        if (respiteTimer > 0)
            respiteTimer -= Time.deltaTime;

        // 更新敌人行为修正值
        UpdateModifiers();

        // 清理已销毁的敌人引用
        activeEnemies.RemoveAll(e => e == null || !e.IsAlive);
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 注册活跃敌人
    /// </summary>
    public void RegisterEnemy(EnemyBase enemy)
    {
        if (enemy != null && !activeEnemies.Contains(enemy))
            activeEnemies.Add(enemy);
    }

    /// <summary>
    /// 注销敌人
    /// </summary>
    public void UnregisterEnemy(EnemyBase enemy)
    {
        activeEnemies.Remove(enemy);
    }

    /// <summary>
    /// 是否可以生成更多敌人
    /// </summary>
    public bool CanSpawnMoreEnemies()
    {
        if (respiteTimer > 0) return false;
        return activeEnemies.Count < maxActiveEnemies;
    }

    /// <summary>
    /// 获取敌人建议数量（基于当前预算和技能评分）
    /// </summary>
    public int GetSuggestedSpawnCount(float enemyCost = 1f)
    {
        float budget = baseBudgetPerMinute * BudgetMultiplier;
        int maxFromBudget = Mathf.FloorToInt(budget / Mathf.Max(enemyCost, 0.1f));
        int maxFromCap = maxActiveEnemies - activeEnemies.Count;
        return Mathf.Max(0, Mathf.Min(maxFromBudget, maxFromCap));
    }

    /// <summary>
    /// 获取敌人伤害修正值
    /// </summary>
    public float GetEnemyDamageModifier()
    {
        // 高技能 → 敌人伤害增加，低技能 → 敌人伤害降低
        float base_ = Mathf.Lerp(0.7f, 1.3f, skillRating);

        // 低血量安全阀
        if (IsAnyPlayerLowHealth())
            base_ *= lowHealthAggressionCut;

        // DifficultyManager进一步修正
        if (DifficultyManager.Instance != null)
            base_ *= DifficultyManager.Instance.GetCurrentModifier();

        return base_;
    }

    /// <summary>
    /// 获取敌人血量修正值
    /// </summary>
    public float GetEnemyHealthModifier()
    {
        return Mathf.Lerp(0.8f, 1.4f, skillRating);
    }

    /// <summary>
    /// 获取敌人攻击频率修正（值越大越慢）
    /// </summary>
    public float GetEnemyAttackCooldownModifier()
    {
        // 高技能 → 敌人攻击更快（cooldown更短）
        return Mathf.Lerp(1.3f, 0.7f, skillRating);
    }

    /// <summary>
    /// 获取敌人移动速度修正
    /// </summary>
    public float GetEnemySpeedModifier()
    {
        return Mathf.Lerp(0.85f, 1.15f, skillRating);
    }

    /// <summary>
    /// 手动设置技能评分（教程结束时用）
    /// </summary>
    public void SetSkillRating(float rating)
    {
        skillRating = Mathf.Clamp01(rating);
        OnSkillRatingChanged?.Invoke(skillRating);
    }

    // ==================== 事件处理 ====================

    private void OnEnemyDefeated(EnemyDefeatedEvent e)
    {
        enemiesDefeatedThisLevel++;

        // 击杀提升技能评分
        float bonus = 0.01f;
        if (activeEnemies.Count > 3) bonus *= 1.5f; // 面对多敌加成
        AdjustSkillRating(bonus);
    }

    private void OnPlayerDeath(PlayerDeathEvent e)
    {
        deathsThisLevel++;

        // 死亡大幅降低技能评分
        AdjustSkillRating(-deathSkillPenalty);

        // 给予暂缓
        respiteTimer = respiteAfterDeath;
    }

    private void OnPlayerDamaged(PlayerDamagedEvent e)
    {
        totalDamageTaken += e.damage;

        // 受伤略微降低技能评分
        AdjustSkillRating(-damageSkillPenalty);
    }

    private void OnComboChanged(ComboChangedEvent e)
    {
        if (e.comboCount > highestCombo)
        {
            highestCombo = e.comboCount;
            // 高连击提升技能评分
            if (e.comboCount >= 10)
                AdjustSkillRating(comboSkillBonus * 2f);
            else if (e.comboCount >= 5)
                AdjustSkillRating(comboSkillBonus);
        }
    }

    private void OnLevelStart(LevelStartEvent e)
    {
        enemiesDefeatedThisLevel = 0;
        deathsThisLevel = 0;
        totalDamageTaken = 0;
        highestCombo = 0;
        levelStartTime = Time.time;
        activeEnemies.Clear();
    }

    private void OnLevelComplete(LevelCompleteEvent e)
    {
        // 关卡完成统计分析
        float levelDuration = Time.time - levelStartTime;
        float killRate = levelDuration > 0 ? enemiesDefeatedThisLevel / (levelDuration / 60f) : 0;

        // 无伤通关 → 技能评分大幅提升
        if (deathsThisLevel == 0 && totalDamageTaken < 5)
            AdjustSkillRating(0.1f);

        // 高星通关 → 技能评分提升
        if (e.stars >= 3)
            AdjustSkillRating(0.05f);

        SaveSkillRating();
    }

    // ==================== 内部方法 ====================

    private void AdjustSkillRating(float delta)
    {
        float old = skillRating;
        skillRating = Mathf.Clamp01(skillRating + delta);

        if (Mathf.Abs(skillRating - old) > 0.001f)
            OnSkillRatingChanged?.Invoke(skillRating);
    }

    private void UpdateModifiers()
    {
        // 攻击性：基于技能评分
        float targetAggression = Mathf.Lerp(-aggressionRange, aggressionRange, skillRating);

        // 低血量减少攻击性
        if (IsAnyPlayerLowHealth())
            targetAggression *= lowHealthAggressionCut;

        aggressionModifier = Mathf.MoveTowards(aggressionModifier, targetAggression, Time.deltaTime * 0.5f);

        // 反应时间：高技能 → 敌人反应更快
        reactionModifier = Mathf.Lerp(reactionTimeAdjust, -reactionTimeAdjust, skillRating);
    }

    private bool IsAnyPlayerLowHealth()
    {
        var players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (var ph in players)
        {
            if (ph.IsAlive && ph.HealthPercent <= lowHealthThreshold)
                return true;
        }
        return false;
    }

    private void SaveSkillRating()
    {
        PlayerPrefs.SetFloat("enemy_director_skill", skillRating);
    }

    private void LoadSkillRating()
    {
        skillRating = PlayerPrefs.GetFloat("enemy_director_skill", 0.5f);
    }
}
