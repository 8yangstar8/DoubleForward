using UnityEngine;

/// <summary>
/// 关卡奖励处理器 - 关卡完成时计算并发放所有奖励
/// 整合ComboSystem评分、CurrencyManager金币、SaveSystem存档
/// 在LevelCompleteEvent时触发，统一处理所有结算逻辑
/// </summary>
public class LevelRewardProcessor : MonoBehaviour
{
    public static LevelRewardProcessor Instance { get; private set; }

    [Header("基础奖励")]
    [SerializeField] private int baseCompletionCoins = 50;
    [SerializeField] private int coinsPerStar = 25;
    [SerializeField] private int coinsPerCollectible = 5;
    [SerializeField] private int firstClearBonus = 100;

    [Header("评分奖励")]
    [SerializeField] private int perfectClearBonus = 200;       // 全收集+零死亡
    [SerializeField] private int speedRunBonus = 75;             // 在par time内完成
    [SerializeField] private int noDeathBonus = 50;             // 零死亡

    [Header("连击奖励")]
    [SerializeField] private int highComboThreshold = 10;       // 高连击阈值
    [SerializeField] private int highComboBonus = 30;

    [Header("合作奖励")]
    [SerializeField] private int coopReviveBonus = 20;          // 复活队友
    [SerializeField] private int coopAbilityBonus = 15;         // 使用合体技

    // 运行时统计
    private int coopReviveCount;
    private int coopAbilityCount;
    private LevelRewardResult lastResult;

    public LevelRewardResult LastResult => lastResult;

    public event System.Action<LevelRewardResult> OnRewardCalculated;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Subscribe<CoopReviveEvent>(OnCoopRevive);
        EventBus.Subscribe<CoopAbilityUsedEvent>(OnCoopAbility);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Unsubscribe<CoopReviveEvent>(OnCoopRevive);
        EventBus.Unsubscribe<CoopAbilityUsedEvent>(OnCoopAbility);
        if (Instance == this) Instance = null;
    }

    // ==================== 事件处理 ====================

    private void OnLevelStart(LevelStartEvent e)
    {
        coopReviveCount = 0;
        coopAbilityCount = 0;
        lastResult = null;
    }

    private void OnCoopRevive(CoopReviveEvent e)
    {
        coopReviveCount++;
    }

    private void OnCoopAbility(CoopAbilityUsedEvent e)
    {
        coopAbilityCount++;
    }

    private void OnLevelComplete(LevelCompleteEvent e)
    {
        lastResult = CalculateRewards(e);
        DistributeRewards(lastResult);
        OnRewardCalculated?.Invoke(lastResult);

        Debug.Log($"[Reward] Total: {lastResult.totalCoins} coins | " +
            $"Score: {lastResult.totalScore} | Stars: {lastResult.stars}");
    }

    // ==================== 奖励计算 ====================

    /// <summary>
    /// 计算关卡完成奖励
    /// </summary>
    public LevelRewardResult CalculateRewards(LevelCompleteEvent levelEvent)
    {
        var result = new LevelRewardResult();
        result.chapter = levelEvent.chapter;
        result.level = levelEvent.level;
        result.stars = levelEvent.stars;
        result.completionTime = levelEvent.time;
        result.collectiblesFound = levelEvent.collectibles;

        // 1. 基础完成奖励
        result.baseReward = baseCompletionCoins;

        // 2. 星级奖励
        result.starReward = levelEvent.stars * coinsPerStar;

        // 3. 收集品奖励
        result.collectibleReward = levelEvent.collectibles * coinsPerCollectible;

        // 4. 首次通关奖励
        bool isFirstClear = false;
        if (SaveSystem.Instance != null)
        {
            isFirstClear = !SaveSystem.Instance.IsLevelCompleted(
                levelEvent.chapter, levelEvent.level);
        }
        result.firstClearReward = isFirstClear ? firstClearBonus : 0;
        result.isFirstClear = isFirstClear;

        // 5. ComboSystem评分
        if (ComboSystem.Instance != null)
        {
            int totalCollectibles = LevelManager.Instance != null
                ? LevelManager.Instance.GetTotalCollectibles() : 0;

            var comboResult = ComboSystem.Instance.CalculateLevelResult(
                GetParTime(levelEvent.chapter, levelEvent.level),
                totalCollectibles,
                levelEvent.collectibles
            );

            result.comboScore = comboResult.totalScore;
            result.maxCombo = comboResult.maxCombo;
            result.deathCount = comboResult.deathCount;
            result.perfectActions = comboResult.perfectActions;

            // 连击奖励
            if (comboResult.maxCombo >= highComboThreshold)
                result.comboBonus = highComboBonus;
        }

        // 6. 速通奖励
        float parTime = GetParTime(levelEvent.chapter, levelEvent.level);
        if (levelEvent.time <= parTime && parTime > 0)
        {
            result.speedRunReward = speedRunBonus;
        }

        // 7. 无死亡奖励
        if (result.deathCount == 0)
        {
            result.noDeathReward = noDeathBonus;
        }

        // 8. 完美通关奖励
        int totalCollectiblesAll = LevelManager.Instance != null
            ? LevelManager.Instance.GetTotalCollectibles() : 0;
        if (result.deathCount == 0 && levelEvent.collectibles >= totalCollectiblesAll && totalCollectiblesAll > 0)
        {
            result.perfectClearReward = perfectClearBonus;
        }

        // 9. 合作奖励
        result.coopReviveReward = coopReviveCount * coopReviveBonus;
        result.coopAbilityReward = coopAbilityCount * coopAbilityBonus;

        // 10. 总计
        result.totalCoins = result.baseReward + result.starReward + result.collectibleReward
            + result.firstClearReward + result.comboBonus + result.speedRunReward
            + result.noDeathReward + result.perfectClearReward
            + result.coopReviveReward + result.coopAbilityReward;

        result.totalScore = result.comboScore + result.totalCoins * 10;

        return result;
    }

    // ==================== 奖励发放 ====================

    private void DistributeRewards(LevelRewardResult result)
    {
        // 发放金币
        if (CurrencyManager.Instance != null && result.totalCoins > 0)
        {
            CurrencyManager.Instance.AddCoins(result.totalCoins, "level_complete");
        }

        // 更新存档
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.MarkLevelComplete(
                result.chapter, result.level, result.stars,
                result.completionTime, result.collectiblesFound);
        }

        // 更新分数
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddScore(result.totalScore, "level_complete");
        }

        // 提交排行榜
        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.SubmitScore(
                result.chapter, result.level, result.totalScore,
                result.completionTime, result.stars, result.deathCount);
        }

        // 记录统计
        if (GameStats.Instance != null)
        {
            string levelId = $"{result.chapter}_{result.level}";
            GameStats.Instance.RecordLevelComplete(
                levelId, result.completionTime, result.totalScore,
                result.stars, result.maxCombo);
        }

        // 难度系统记录
        if (DifficultyManager.Instance != null)
        {
            DifficultyManager.Instance.RecordLevelComplete();
        }
    }

    // ==================== 辅助 ====================

    [Header("关卡数据")]
    [SerializeField] private LevelDataCatalog levelCatalog;

    private float GetParTime(int chapter, int level)
    {
        // 从LevelDataCatalog获取
        if (levelCatalog != null)
        {
            var entry = levelCatalog.GetLevel(chapter, level);
            if (entry != null) return entry.parTime;
        }

        // 从LevelManager获取
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
            return LevelManager.Instance.CurrentLevel.parTime;

        // 默认par time
        return 120f + (chapter - 1) * 30f;
    }

    // ==================== 结果数据 ====================

    [System.Serializable]
    public class LevelRewardResult
    {
        // 关卡信息
        public int chapter;
        public int level;
        public int stars;
        public float completionTime;
        public int collectiblesFound;
        public bool isFirstClear;

        // 金币明细
        public int baseReward;
        public int starReward;
        public int collectibleReward;
        public int firstClearReward;
        public int comboBonus;
        public int speedRunReward;
        public int noDeathReward;
        public int perfectClearReward;
        public int coopReviveReward;
        public int coopAbilityReward;
        public int totalCoins;

        // 评分数据
        public int comboScore;
        public int totalScore;
        public int maxCombo;
        public int deathCount;
        public int perfectActions;
    }
}
