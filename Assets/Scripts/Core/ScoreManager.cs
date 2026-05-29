using UnityEngine;

/// <summary>
/// 分数管理器 - 集中计算和追踪关卡分数
/// 连接ComboSystem、收集品、击杀、速度奖励等多维度计分
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("分数权重")]
    [SerializeField] private int baseKillScore = 100;
    [SerializeField] private int collectibleScore = 200;
    [SerializeField] private int puzzleScore = 500;
    [SerializeField] private int bossKillScore = 2000;
    [SerializeField] private int checkpointScore = 150;
    [SerializeField] private int noDeathBonus = 1000;
    [SerializeField] private int speedRunBonusPerSecond = 10;

    [Header("时间奖励")]
    [SerializeField] private float speedRunThresholdSeconds = 120f;

    public int CurrentScore { get; private set; }
    public int HighScore { get; private set; }
    public int LevelDeaths { get; private set; }
    public int LevelKills { get; private set; }
    public int LevelCollectibles { get; private set; }
    public int LevelPuzzles { get; private set; }

    private float levelStartTime;
    private bool isLevelActive;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Subscribe<CollectiblePickedEvent>(OnCollectiblePicked);
        EventBus.Subscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Subscribe<CheckpointReachedEvent>(OnCheckpointReached);
        EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
        EventBus.Subscribe<ComboChangedEvent>(OnComboChanged);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Unsubscribe<CollectiblePickedEvent>(OnCollectiblePicked);
        EventBus.Unsubscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Unsubscribe<CheckpointReachedEvent>(OnCheckpointReached);
        EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        EventBus.Unsubscribe<ComboChangedEvent>(OnComboChanged);
    }

    // ============ 事件处理 ============

    private void OnLevelStart(LevelStartEvent evt)
    {
        ResetLevelScore();
        levelStartTime = Time.time;
        isLevelActive = true;
    }

    private void OnLevelComplete(LevelCompleteEvent evt)
    {
        if (!isLevelActive) return;
        isLevelActive = false;

        // 无死亡奖励
        if (LevelDeaths == 0)
            AddScore(noDeathBonus, "无死亡通关");

        // 速度奖励
        float elapsed = Time.time - levelStartTime;
        if (elapsed < speedRunThresholdSeconds)
        {
            int timeBonus = Mathf.RoundToInt((speedRunThresholdSeconds - elapsed) * speedRunBonusPerSecond);
            AddScore(timeBonus, "速度奖励");
        }

        // 更新最高分
        string highScoreKey = $"HighScore_{evt.chapter}_{evt.level}";
        int savedHigh = PlayerPrefs.GetInt(highScoreKey, 0);
        if (CurrentScore > savedHigh)
        {
            PlayerPrefs.SetInt(highScoreKey, CurrentScore);
            PlayerPrefs.Save();
            HighScore = CurrentScore;
        }
        else
        {
            HighScore = savedHigh;
        }
    }

    private void OnEnemyDefeated(EnemyDefeatedEvent evt)
    {
        if (!isLevelActive) return;
        LevelKills++;

        // 使用敌人自身的分数值，如果有的话
        int score = evt.scoreValue > 0 ? evt.scoreValue : baseKillScore;

        // 应用当前连击倍率
        float comboMult = GetCurrentComboMultiplier();
        score = Mathf.RoundToInt(score * comboMult);

        AddScore(score, "击杀敌人");
    }

    private void OnCollectiblePicked(CollectiblePickedEvent evt)
    {
        if (!isLevelActive) return;
        LevelCollectibles++;
        AddScore(collectibleScore, "收集品");
    }

    private void OnPuzzleSolved(PuzzleSolvedEvent evt)
    {
        if (!isLevelActive) return;
        LevelPuzzles++;
        AddScore(puzzleScore, "谜题解答");
    }

    private void OnCheckpointReached(CheckpointReachedEvent evt)
    {
        if (!isLevelActive) return;
        AddScore(checkpointScore, "检查点");
    }

    private void OnPlayerDeath(PlayerDeathEvent evt)
    {
        if (!isLevelActive) return;
        LevelDeaths++;
    }

    private void OnBossDefeated(BossDefeatedEvent evt)
    {
        if (!isLevelActive) return;
        AddScore(bossKillScore, "击败Boss");
    }

    private float currentComboMultiplier = 1f;
    private void OnComboChanged(ComboChangedEvent evt)
    {
        currentComboMultiplier = evt.multiplier;
    }

    // ============ 核心方法 ============

    // 临时分数倍率
    private float temporaryMultiplier = 1f;
    private float tempMultiplierTimer;

    void Update()
    {
        // 临时倍率倒计时
        if (tempMultiplierTimer > 0)
        {
            tempMultiplierTimer -= Time.deltaTime;
            if (tempMultiplierTimer <= 0)
                temporaryMultiplier = 1f;
        }
    }

    /// <summary>
    /// 设置临时分数倍率（道具效果）
    /// </summary>
    public void SetTemporaryMultiplier(float multiplier, float duration)
    {
        temporaryMultiplier = multiplier;
        tempMultiplierTimer = duration;
    }

    /// <summary>
    /// 增加分数并发布事件
    /// </summary>
    public void AddScore(int amount, string reason = "")
    {
        if (amount <= 0) return;

        // 应用临时倍率
        amount = Mathf.RoundToInt(amount * temporaryMultiplier);

        CurrentScore += amount;

        EventBus.Publish(new ScoreChangedEvent
        {
            totalScore = CurrentScore,
            delta = amount
        });

        if (!string.IsNullOrEmpty(reason))
            Debug.Log($"[Score] +{amount} ({reason}) → Total: {CurrentScore}");
    }

    /// <summary>
    /// 重置关卡分数
    /// </summary>
    public void ResetLevelScore()
    {
        CurrentScore = 0;
        LevelDeaths = 0;
        LevelKills = 0;
        LevelCollectibles = 0;
        LevelPuzzles = 0;
        currentComboMultiplier = 1f;
        isLevelActive = false;
    }

    /// <summary>
    /// 获取当前连击倍率
    /// </summary>
    private float GetCurrentComboMultiplier()
    {
        if (ComboSystem.Instance != null)
            return ComboSystem.Instance.ComboMultiplier;
        return currentComboMultiplier;
    }

    /// <summary>
    /// 获取指定关卡最高分
    /// </summary>
    public int GetHighScore(int chapter, int level)
    {
        return PlayerPrefs.GetInt($"HighScore_{chapter}_{level}", 0);
    }

    /// <summary>
    /// 根据分数计算星级 (1-3)
    /// </summary>
    public int CalculateStars(int score, int oneStar, int twoStar, int threeStar)
    {
        if (score >= threeStar) return 3;
        if (score >= twoStar) return 2;
        if (score >= oneStar) return 1;
        return 0;
    }

    /// <summary>
    /// 获取关卡结算数据
    /// </summary>
    public LevelScoreSummary GetSummary()
    {
        return new LevelScoreSummary
        {
            totalScore = CurrentScore,
            highScore = HighScore,
            isNewHighScore = CurrentScore >= HighScore && HighScore > 0,
            kills = LevelKills,
            collectibles = LevelCollectibles,
            puzzles = LevelPuzzles,
            deaths = LevelDeaths,
            clearTime = isLevelActive ? Time.time - levelStartTime : 0f
        };
    }

    /// <summary>
    /// 关卡结算数据结构
    /// </summary>
    [System.Serializable]
    public class LevelScoreSummary
    {
        public int totalScore;
        public int highScore;
        public bool isNewHighScore;
        public int kills;
        public int collectibles;
        public int puzzles;
        public int deaths;
        public float clearTime;
    }
}
