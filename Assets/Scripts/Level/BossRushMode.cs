using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Boss连战模式 - 通关后解锁的挑战模式
/// 连续挑战所有章节Boss，中间有短暂回复时间
/// 追踪总用时、无伤击败、最少死亡等成就
/// </summary>
public class BossRushMode : MonoBehaviour
{
    public static BossRushMode Instance { get; private set; }

    [Header("Boss列表")]
    [SerializeField] private List<BossRushEntry> bossEntries = new List<BossRushEntry>();

    [Header("休息设置")]
    [SerializeField] private float restDurationBetweenBosses = 10f;
    [SerializeField] private float healthRestorePercent = 0.5f;    // 每次休息回复50%血量

    [Header("难度递增")]
    [SerializeField] private float healthScalePerBoss = 0.15f;     // 每个Boss血量+15%
    [SerializeField] private float damageScalePerBoss = 0.1f;      // 每个Boss伤害+10%

    // 运行时
    private BossRushState currentState = BossRushState.Idle;
    private int currentBossIndex;
    private float totalElapsedTime;
    private int totalDeaths;
    private int noHitBossCount;
    private bool currentBossNoHit;
    private float currentBossStartTime;
    private List<BossResult> results = new List<BossResult>();

    // 最佳记录
    private const string BEST_TIME_KEY = "boss_rush_best_time";
    private const string BEST_DEATHS_KEY = "boss_rush_best_deaths";

    public BossRushState CurrentState => currentState;
    public int CurrentBossIndex => currentBossIndex;
    public int TotalBosses => bossEntries.Count;
    public float TotalTime => totalElapsedTime;
    public int TotalDeaths => totalDeaths;
    public BossRushEntry CurrentBossEntry =>
        currentBossIndex < bossEntries.Count ? bossEntries[currentBossIndex] : null;
    public bool IsActive => currentState != BossRushState.Idle && currentState != BossRushState.Complete;

    public event System.Action OnBossRushStarted;
    public event System.Action<int> OnBossEncounterStarted;      // bossIndex
    public event System.Action<int, BossResult> OnBossDefeated;  // bossIndex, result
    public event System.Action<float> OnRestPhase;               // restTimeRemaining
    public event System.Action<BossRushSummary> OnBossRushComplete;

    [System.Serializable]
    public class BossRushEntry
    {
        public string bossId;
        public string bossNameKey;
        public int chapter;
        public string bossSceneName;        // 场景名或Boss预制体ID
        public GameObject bossPrefab;
        public AudioClip bossMusic;
        public Sprite bossPortrait;
        public float baseHealth = 500f;
        public float baseDamage = 20f;
    }

    public enum BossRushState
    {
        Idle,
        Starting,
        Fighting,
        Rest,
        Complete
    }

    public class BossResult
    {
        public string bossId;
        public float timeToDefeat;
        public int deathsDuringBoss;
        public bool noHit;
        public int comboHighScore;
    }

    public class BossRushSummary
    {
        public float totalTime;
        public int totalDeaths;
        public int noHitBossCount;
        public int totalBossesDefeated;
        public List<BossResult> bossResults;
        public bool isNewBestTime;
        public bool isNewBestDeaths;
        public string rank;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        InitializeDefaultBosses();
    }

    void Start()
    {
        EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
    }

    void Update()
    {
        if (currentState == BossRushState.Fighting)
        {
            totalElapsedTime += Time.deltaTime;
        }
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        if (Instance == this) Instance = null;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 检查是否解锁Boss连战模式（需通关游戏）
    /// </summary>
    public bool IsUnlocked()
    {
        if (WorldProgressionManager.Instance != null)
            return WorldProgressionManager.Instance.IsBossDefeated(5);

        return PlayerPrefs.GetInt("best_ending", 0) > 0;
    }

    /// <summary>
    /// 开始Boss连战
    /// </summary>
    public void StartBossRush()
    {
        if (!IsUnlocked()) return;
        if (IsActive) return;

        currentBossIndex = 0;
        totalElapsedTime = 0;
        totalDeaths = 0;
        noHitBossCount = 0;
        results.Clear();

        currentState = BossRushState.Starting;
        OnBossRushStarted?.Invoke();

        StartCoroutine(BossRushSequence());
    }

    /// <summary>
    /// 通知当前Boss已被击败
    /// </summary>
    public void NotifyBossDefeated()
    {
        if (currentState != BossRushState.Fighting) return;

        float bossTime = Time.time - currentBossStartTime;
        var result = new BossResult
        {
            bossId = bossEntries[currentBossIndex].bossId,
            timeToDefeat = bossTime,
            deathsDuringBoss = 0,  // 填充后续
            noHit = currentBossNoHit,
            comboHighScore = ComboSystem.Instance?.CurrentCombo ?? 0
        };

        results.Add(result);
        if (currentBossNoHit) noHitBossCount++;

        OnBossDefeated?.Invoke(currentBossIndex, result);
    }

    /// <summary>
    /// 放弃Boss连战
    /// </summary>
    public void AbandonBossRush()
    {
        if (!IsActive) return;

        StopAllCoroutines();
        currentState = BossRushState.Idle;

        if (AudioManager.Instance != null)
            AudioManager.Instance.StopBGM();
    }

    /// <summary>
    /// 获取最佳记录
    /// </summary>
    public float GetBestTime()
    {
        return PlayerPrefs.GetFloat(BEST_TIME_KEY, float.MaxValue);
    }

    /// <summary>
    /// 获取最少死亡记录
    /// </summary>
    public int GetBestDeaths()
    {
        return PlayerPrefs.GetInt(BEST_DEATHS_KEY, int.MaxValue);
    }

    /// <summary>
    /// 获取当前Boss的缩放难度
    /// </summary>
    public float GetCurrentHealthScale()
    {
        return 1f + currentBossIndex * healthScalePerBoss;
    }

    public float GetCurrentDamageScale()
    {
        return 1f + currentBossIndex * damageScalePerBoss;
    }

    // ==================== 事件处理 ====================

    private void OnPlayerDeath(PlayerDeathEvent e)
    {
        if (currentState == BossRushState.Fighting)
            totalDeaths++;
    }

    private void OnPlayerDamaged(PlayerDamagedEvent e)
    {
        if (currentState == BossRushState.Fighting)
            currentBossNoHit = false;
    }

    // ==================== Boss连战流程 ====================

    private IEnumerator BossRushSequence()
    {
        yield return new WaitForSeconds(2f); // 起始延迟

        while (currentBossIndex < bossEntries.Count)
        {
            var entry = bossEntries[currentBossIndex];

            // === 战斗前准备 ===
            currentState = BossRushState.Starting;
            currentBossNoHit = true;
            currentBossStartTime = Time.time;

            // 播放Boss音乐
            if (entry.bossMusic != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayBGM(entry.bossMusic);

            // 通知UI显示Boss信息
            OnBossEncounterStarted?.Invoke(currentBossIndex);

            EventBus.Publish(new HintRequestEvent
            {
                textKey = entry.bossNameKey,
                fallbackText = $"Boss {currentBossIndex + 1}/{bossEntries.Count}",
                duration = 3f
            });

            yield return new WaitForSeconds(3f);

            // === 战斗阶段 ===
            currentState = BossRushState.Fighting;

            // 等待Boss被击败（由外部调用NotifyBossDefeated）
            while (currentState == BossRushState.Fighting)
                yield return null;

            // Boss被击败后
            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.PlayConfirm();
            if (VFXManager.Instance != null)
                VFXManager.Instance.ShakeHeavy();

            currentBossIndex++;

            // === 休息阶段（非最后一个Boss） ===
            if (currentBossIndex < bossEntries.Count)
            {
                currentState = BossRushState.Rest;

                // 回复部分生命
                EventBus.Publish(new HealAllPlayersEvent
                {
                    healAmount = Mathf.CeilToInt(100 * healthRestorePercent),
                    source = "boss_rush_rest"
                });

                float restTimer = restDurationBetweenBosses;
                while (restTimer > 0)
                {
                    restTimer -= Time.deltaTime;
                    OnRestPhase?.Invoke(restTimer);
                    yield return null;
                }
            }
        }

        // === 全部击败 ===
        currentState = BossRushState.Complete;
        yield return CompleteBossRush();
    }

    private IEnumerator CompleteBossRush()
    {
        // 计算总结
        bool isNewBestTime = totalElapsedTime < GetBestTime();
        bool isNewBestDeaths = totalDeaths < GetBestDeaths();

        if (isNewBestTime)
        {
            PlayerPrefs.SetFloat(BEST_TIME_KEY, totalElapsedTime);
        }
        if (isNewBestDeaths)
        {
            PlayerPrefs.SetInt(BEST_DEATHS_KEY, totalDeaths);
        }
        PlayerPrefs.Save();

        // 评级
        string rank = CalculateRank();

        var summary = new BossRushSummary
        {
            totalTime = totalElapsedTime,
            totalDeaths = totalDeaths,
            noHitBossCount = noHitBossCount,
            totalBossesDefeated = bossEntries.Count,
            bossResults = new List<BossResult>(results),
            isNewBestTime = isNewBestTime,
            isNewBestDeaths = isNewBestDeaths,
            rank = rank
        };

        OnBossRushComplete?.Invoke(summary);

        // 成就
        if (AchievementSystem.Instance != null)
        {
            AchievementSystem.Instance.Unlock("boss_rush_complete");

            if (totalDeaths == 0)
                AchievementSystem.Instance.Unlock("boss_rush_no_death");

            if (noHitBossCount == bossEntries.Count)
                AchievementSystem.Instance.Unlock("boss_rush_no_hit");

            if (rank == "S")
                AchievementSystem.Instance.Unlock("boss_rush_s_rank");
        }

        // 奖励
        if (CurrencyManager.Instance != null)
        {
            int coinReward = 500 + (noHitBossCount * 100);
            CurrencyManager.Instance.AddCoins(coinReward, "boss_rush");
        }

        yield return new WaitForSeconds(1f);

        Debug.Log($"[BossRush] Complete! Time: {totalElapsedTime:F1}s, Deaths: {totalDeaths}, Rank: {rank}");
    }

    private string CalculateRank()
    {
        int score = 0;

        // 时间评分（每个Boss平均60秒内S级）
        float avgTime = totalElapsedTime / Mathf.Max(1, bossEntries.Count);
        if (avgTime < 45f) score += 40;
        else if (avgTime < 60f) score += 30;
        else if (avgTime < 90f) score += 20;
        else if (avgTime < 120f) score += 10;

        // 死亡评分
        if (totalDeaths == 0) score += 30;
        else if (totalDeaths <= 2) score += 20;
        else if (totalDeaths <= 5) score += 10;

        // 无伤评分
        score += noHitBossCount * 6;

        if (score >= 85) return "S";
        if (score >= 70) return "A";
        if (score >= 50) return "B";
        if (score >= 30) return "C";
        return "D";
    }

    // ==================== 默认Boss列表 ====================

    private void InitializeDefaultBosses()
    {
        if (bossEntries.Count > 0) return;

        bossEntries.Add(new BossRushEntry
        {
            bossId = "forest_guardian",
            bossNameKey = "boss_1_name",
            chapter = 1,
            bossSceneName = "Boss_Forest",
            baseHealth = 300f,
            baseDamage = 15f
        });

        bossEntries.Add(new BossRushEntry
        {
            bossId = "crystal_golem",
            bossNameKey = "boss_2_name",
            chapter = 2,
            bossSceneName = "Boss_Crystal",
            baseHealth = 450f,
            baseDamage = 20f
        });

        bossEntries.Add(new BossRushEntry
        {
            bossId = "void_serpent",
            bossNameKey = "boss_3_name",
            chapter = 3,
            bossSceneName = "Boss_Abyss",
            baseHealth = 600f,
            baseDamage = 25f
        });

        bossEntries.Add(new BossRushEntry
        {
            bossId = "sky_warden",
            bossNameKey = "boss_4_name",
            chapter = 4,
            bossSceneName = "Boss_Sky",
            baseHealth = 750f,
            baseDamage = 30f
        });

        bossEntries.Add(new BossRushEntry
        {
            bossId = "twilight_king",
            bossNameKey = "boss_5_name",
            chapter = 5,
            bossSceneName = "Boss_Twilight",
            baseHealth = 1000f,
            baseDamage = 35f
        });
    }
}
