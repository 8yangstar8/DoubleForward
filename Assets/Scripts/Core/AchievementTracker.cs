using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 成就自动追踪器 - 监听EventBus事件，自动更新AchievementSystem进度
/// 独立于AchievementSystem，避免核心类过于膨胀
/// </summary>
public class AchievementTracker : MonoBehaviour
{
    public static AchievementTracker Instance { get; private set; }

    // 运行时统计
    private int totalEnemiesDefeated;
    private int currentCombo;
    private int consecutiveWallJumps;
    private HashSet<int> completedChapters = new HashSet<int>();
    private Dictionary<int, bool> chapterNoDeath = new Dictionary<int, bool>(); // 章节无死亡追踪
    private bool bossHitTaken; // Boss战中是否受伤

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        // 关卡事件
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);

        // 战斗事件
        EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
        EventBus.Subscribe<ComboChangedEvent>(OnComboChanged);

        // 玩家事件
        EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);

        // 收集事件
        EventBus.Subscribe<CollectiblePickedEvent>(OnCollectiblePicked);

        // 谜题/探索
        EventBus.Subscribe<PuzzleSolvedEvent>(OnPuzzleSolved);

        // 合作事件
        EventBus.Subscribe<CoopReviveEvent>(OnCoopRevive);
        EventBus.Subscribe<CoopAbilityUsedEvent>(OnCoopAbilityUsed);

        // 技能事件
        EventBus.Subscribe<AbilityUsedEvent>(OnAbilityUsed);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        EventBus.Unsubscribe<ComboChangedEvent>(OnComboChanged);
        EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Unsubscribe<CollectiblePickedEvent>(OnCollectiblePicked);
        EventBus.Unsubscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Unsubscribe<CoopReviveEvent>(OnCoopRevive);
        EventBus.Unsubscribe<CoopAbilityUsedEvent>(OnCoopAbilityUsed);
        EventBus.Unsubscribe<AbilityUsedEvent>(OnAbilityUsed);
    }

    // ==================== 关卡完成 ====================

    private void OnLevelStart(LevelStartEvent e)
    {
        bossHitTaken = false;

        // 初始化章节无死亡追踪
        if (!chapterNoDeath.ContainsKey(e.chapter))
            chapterNoDeath[e.chapter] = true;
    }

    private void OnLevelComplete(LevelCompleteEvent e)
    {
        if (AchievementSystem.Instance == null) return;

        // 章节完成
        string chapterAchievement = $"ch{e.chapter}_complete";
        AchievementSystem.Instance.UpdateProgress(chapterAchievement);

        // 检查全章节完成
        completedChapters.Add(e.chapter);
        if (completedChapters.Count >= 5)
            AchievementSystem.Instance.UpdateProgress("all_chapters");

        // 3星追踪
        if (e.stars >= 3)
            AchievementSystem.Instance.UpdateProgress("all_stars");

        // 速通成就
        if (e.time <= 60f)
            AchievementSystem.Instance.UpdateProgress("speed_demon");

        // 章节无死亡
        if (chapterNoDeath.ContainsKey(e.chapter) && chapterNoDeath[e.chapter])
        {
            // 检查是否是章节最后一关（每章4关）
            if (e.level == 4)
                AchievementSystem.Instance.UpdateProgress("no_death_chapter");
        }
    }

    // ==================== 战斗 ====================

    private void OnEnemyDefeated(EnemyDefeatedEvent e)
    {
        if (AchievementSystem.Instance == null) return;

        totalEnemiesDefeated++;
        AchievementSystem.Instance.SetProgress("defeat_100", totalEnemiesDefeated);
        AchievementSystem.Instance.SetProgress("defeat_500", totalEnemiesDefeated);
    }

    private void OnBossDefeated(BossDefeatedEvent e)
    {
        if (AchievementSystem.Instance == null) return;

        string bossAchievement = $"boss_ch{e.chapter}";
        AchievementSystem.Instance.UpdateProgress(bossAchievement);

        // 无伤Boss
        if (!bossHitTaken)
            AchievementSystem.Instance.UpdateProgress("no_hit_boss");
    }

    private void OnComboChanged(ComboChangedEvent e)
    {
        if (AchievementSystem.Instance == null) return;

        currentCombo = e.comboCount;

        if (currentCombo >= 50)
            AchievementSystem.Instance.UpdateProgress("combo_master");
        if (currentCombo >= 100)
            AchievementSystem.Instance.UpdateProgress("combo_legend");
    }

    // ==================== 玩家 ====================

    private void OnPlayerDeath(PlayerDeathEvent e)
    {
        // 标记当前章节有死亡
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
        {
            int chapter = LevelManager.Instance.CurrentLevel.chapter;
            chapterNoDeath[chapter] = false;
        }
    }

    private void OnPlayerDamaged(PlayerDamagedEvent e)
    {
        // Boss战中受伤标记
        bossHitTaken = true;
    }

    // ==================== 收集 ====================

    private void OnCollectiblePicked(CollectiblePickedEvent e)
    {
        if (AchievementSystem.Instance == null) return;

        AchievementSystem.Instance.SetProgress("collect_all", e.collected);
    }

    // ==================== 谜题/探索 ====================

    private void OnPuzzleSolved(PuzzleSolvedEvent e)
    {
        if (AchievementSystem.Instance == null) return;

        // 隐藏区域发现
        if (e.puzzleType == "secret_area")
        {
            AchievementSystem.Instance.UpdateProgress("first_secret");
            AchievementSystem.Instance.UpdateProgress("all_secrets");
        }
    }

    // ==================== 合作 ====================

    private void OnCoopRevive(CoopReviveEvent e)
    {
        if (AchievementSystem.Instance == null) return;

        AchievementSystem.Instance.UpdateProgress("revive_partner_10");
    }

    private void OnCoopAbilityUsed(CoopAbilityUsedEvent e)
    {
        if (AchievementSystem.Instance == null) return;

        AchievementSystem.Instance.UpdateProgress("coop_ability_first");
    }

    // ==================== 技能 ====================

    private HashSet<string> usedAbilities = new HashSet<string>();

    private void OnAbilityUsed(AbilityUsedEvent e)
    {
        if (AchievementSystem.Instance == null) return;

        usedAbilities.Add(e.abilityName);

        // 6种技能：light_beam, shadow_phase, light_bridge, shadow_zone, (合作技) light_burst, shadow_storm
        if (usedAbilities.Count >= 6)
            AchievementSystem.Instance.UpdateProgress("use_all_abilities");
    }

    // ==================== 外部调用 ====================

    /// <summary>
    /// 通知连续墙跳（由PlayerController调用）
    /// </summary>
    public void NotifyWallJump()
    {
        consecutiveWallJumps++;
        if (consecutiveWallJumps >= 5 && AchievementSystem.Instance != null)
            AchievementSystem.Instance.UpdateProgress("wall_jump_chain");
    }

    /// <summary>
    /// 重置墙跳计数（落地时调用）
    /// </summary>
    public void ResetWallJumpChain()
    {
        consecutiveWallJumps = 0;
    }

    /// <summary>
    /// 通知拍照模式使用
    /// </summary>
    public void NotifyPhotoModeTaken()
    {
        AchievementSystem.Instance?.UpdateProgress("photo_mode_10");
    }

    /// <summary>
    /// 通知分享
    /// </summary>
    public void NotifyShare()
    {
        AchievementSystem.Instance?.UpdateProgress("share_first");
    }
}
