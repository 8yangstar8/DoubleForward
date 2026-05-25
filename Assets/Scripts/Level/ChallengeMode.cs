using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 挑战模式系统 - 提供额外重玩性
/// 每日挑战、限时挑战、特殊条件挑战
/// 完成挑战获得额外金币和成就进度
/// </summary>
public class ChallengeMode : MonoBehaviour
{
    public static ChallengeMode Instance { get; private set; }

    [Header("挑战配置")]
    [SerializeField] private ChallengeData[] predefinedChallenges;

    [Header("每日挑战")]
    [SerializeField] private int dailyChallengeCount = 3;
    [SerializeField] private int dailyChallengeBaseReward = 100;

    [Header("限时挑战")]
    [SerializeField] private float defaultTimeLimit = 60f;

    // 运行时
    private List<ChallengeInstance> activeChallenges = new List<ChallengeInstance>();
    private List<ChallengeInstance> dailyChallenges = new List<ChallengeInstance>();
    private ChallengeInstance currentChallenge;
    private float challengeTimer;
    private bool isChallengeActive;

    public bool IsChallengeActive => isChallengeActive;
    public ChallengeInstance CurrentChallenge => currentChallenge;
    public IReadOnlyList<ChallengeInstance> DailyChallenges => dailyChallenges;

    public event System.Action<ChallengeInstance> OnChallengeStarted;
    public event System.Action<ChallengeInstance, bool> OnChallengeCompleted; // challenge, success
    public event System.Action<float> OnChallengeTimerUpdate; // remaining seconds

    // ==================== 数据定义 ====================

    [System.Serializable]
    public class ChallengeData
    {
        public string challengeId;
        public string nameKey;           // 本地化key
        public string descriptionKey;
        public ChallengeType type;
        public Sprite icon;

        [Header("条件")]
        public float timeLimit;          // 限时（秒），0=无限
        public int targetCount;          // 目标数量（击杀/收集）
        public int maxDeaths;            // 最大死亡次数，-1=无限
        public int minCombo;             // 最低连击
        public int requiredChapter;      // 所需章节（0=任意）
        public int requiredLevel;        // 所需关卡（0=任意）

        [Header("奖励")]
        public int coinReward = 100;
        public string achievementId;     // 完成后解锁的成就
    }

    public enum ChallengeType
    {
        SpeedRun,          // 限时通关
        NoDeath,           // 无死亡通关
        HighCombo,         // 达到指定连击
        CollectAll,        // 全收集
        PacifistRun,       // 不击杀敌人通关
        BossRush,          // Boss连战
        DailyChallenge,    // 每日挑战（随机生成）
        CoopSync,          // 双人同步挑战
    }

    [System.Serializable]
    public class ChallengeInstance
    {
        public ChallengeData data;
        public bool isCompleted;
        public bool isFailed;
        public float startTime;
        public float bestTime;

        // 进度追踪
        public int currentCount;
        public int deathCount;
        public int maxComboReached;

        public float Progress
        {
            get
            {
                if (data == null) return 0f;
                return data.type switch
                {
                    ChallengeType.HighCombo => data.minCombo > 0
                        ? (float)maxComboReached / data.minCombo : 0f,
                    ChallengeType.CollectAll => data.targetCount > 0
                        ? (float)currentCount / data.targetCount : 0f,
                    _ => isCompleted ? 1f : 0f
                };
            }
        }
    }

    // ==================== 生命周期 ====================

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Subscribe<CollectiblePickedEvent>(OnCollectiblePicked);
        EventBus.Subscribe<ComboChangedEvent>(OnComboChanged);
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);

        GenerateDailyChallenges();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Unsubscribe<CollectiblePickedEvent>(OnCollectiblePicked);
        EventBus.Unsubscribe<ComboChangedEvent>(OnComboChanged);
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!isChallengeActive || currentChallenge == null) return;

        // 限时挑战倒计时
        if (currentChallenge.data.timeLimit > 0)
        {
            challengeTimer -= Time.deltaTime;
            OnChallengeTimerUpdate?.Invoke(challengeTimer);

            if (challengeTimer <= 0)
            {
                FailChallenge("time_up");
            }
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 开始一个挑战
    /// </summary>
    public void StartChallenge(ChallengeData data)
    {
        if (isChallengeActive) return;
        if (data == null) return;

        currentChallenge = new ChallengeInstance
        {
            data = data,
            startTime = Time.time,
            isCompleted = false,
            isFailed = false
        };

        challengeTimer = data.timeLimit > 0 ? data.timeLimit : float.MaxValue;
        isChallengeActive = true;

        OnChallengeStarted?.Invoke(currentChallenge);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("challenge_start");

        Debug.Log($"[Challenge] Started: {data.challengeId} ({data.type})");
    }

    /// <summary>
    /// 开始每日挑战
    /// </summary>
    public void StartDailyChallenge(int index)
    {
        if (index < 0 || index >= dailyChallenges.Count) return;

        var daily = dailyChallenges[index];
        if (daily.isCompleted) return;

        StartChallenge(daily.data);
    }

    /// <summary>
    /// 放弃当前挑战
    /// </summary>
    public void AbandonChallenge()
    {
        if (!isChallengeActive) return;
        FailChallenge("abandoned");
    }

    /// <summary>
    /// 获取挑战是否已完成（用于检查每日挑战状态）
    /// </summary>
    public bool IsChallengeCompleted(string challengeId)
    {
        string key = $"challenge_{challengeId}_{GetDaySeed()}";
        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    // ==================== 事件处理 ====================

    private void OnPlayerDeath(PlayerDeathEvent e)
    {
        if (!isChallengeActive || currentChallenge == null) return;

        currentChallenge.deathCount++;

        // 无死亡挑战
        if (currentChallenge.data.type == ChallengeType.NoDeath)
        {
            FailChallenge("death");
        }

        // 死亡次数超限
        if (currentChallenge.data.maxDeaths >= 0 &&
            currentChallenge.deathCount > currentChallenge.data.maxDeaths)
        {
            FailChallenge("max_deaths");
        }
    }

    private void OnEnemyDefeated(EnemyDefeatedEvent e)
    {
        if (!isChallengeActive || currentChallenge == null) return;

        // 和平主义挑战：击杀敌人则失败
        if (currentChallenge.data.type == ChallengeType.PacifistRun)
        {
            FailChallenge("enemy_killed");
        }

        currentChallenge.currentCount++;
    }

    private void OnCollectiblePicked(CollectiblePickedEvent e)
    {
        if (!isChallengeActive || currentChallenge == null) return;

        if (currentChallenge.data.type == ChallengeType.CollectAll)
        {
            currentChallenge.currentCount = e.collected;

            if (e.collected >= e.total && e.total > 0)
            {
                CompleteChallenge();
            }
        }
    }

    private void OnComboChanged(ComboChangedEvent e)
    {
        if (!isChallengeActive || currentChallenge == null) return;

        if (e.comboCount > currentChallenge.maxComboReached)
            currentChallenge.maxComboReached = e.comboCount;

        // 高连击挑战
        if (currentChallenge.data.type == ChallengeType.HighCombo)
        {
            if (currentChallenge.maxComboReached >= currentChallenge.data.minCombo)
            {
                CompleteChallenge();
            }
        }
    }

    private void OnLevelComplete(LevelCompleteEvent e)
    {
        if (!isChallengeActive || currentChallenge == null) return;

        // 速通、无死亡、和平主义在通关时检查
        switch (currentChallenge.data.type)
        {
            case ChallengeType.SpeedRun:
            case ChallengeType.NoDeath:
            case ChallengeType.PacifistRun:
            case ChallengeType.CoopSync:
                CompleteChallenge();
                break;
        }
    }

    private void OnBossDefeated(BossDefeatedEvent e)
    {
        if (!isChallengeActive || currentChallenge == null) return;

        if (currentChallenge.data.type == ChallengeType.BossRush)
        {
            currentChallenge.currentCount++;
            if (currentChallenge.currentCount >= currentChallenge.data.targetCount)
                CompleteChallenge();
        }
    }

    // ==================== 内部逻辑 ====================

    private void CompleteChallenge()
    {
        if (currentChallenge == null || currentChallenge.isCompleted) return;

        currentChallenge.isCompleted = true;
        currentChallenge.bestTime = Time.time - currentChallenge.startTime;
        isChallengeActive = false;

        // 发放奖励
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.AddCoins(currentChallenge.data.coinReward, "challenge");

        // 解锁成就
        if (!string.IsNullOrEmpty(currentChallenge.data.achievementId))
        {
            AchievementSystem.Instance?.Unlock(currentChallenge.data.achievementId);
        }

        // 标记每日挑战已完成
        string key = $"challenge_{currentChallenge.data.challengeId}_{GetDaySeed()}";
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();

        OnChallengeCompleted?.Invoke(currentChallenge, true);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();

        Debug.Log($"[Challenge] Completed: {currentChallenge.data.challengeId}");
    }

    private void FailChallenge(string reason)
    {
        if (currentChallenge == null) return;

        currentChallenge.isFailed = true;
        isChallengeActive = false;

        OnChallengeCompleted?.Invoke(currentChallenge, false);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("challenge_fail");

        Debug.Log($"[Challenge] Failed: {currentChallenge.data.challengeId} ({reason})");
    }

    // ==================== 每日挑战生成 ====================

    private void GenerateDailyChallenges()
    {
        dailyChallenges.Clear();
        int seed = GetDaySeed();
        var rng = new System.Random(seed);

        // 挑战类型池
        ChallengeType[] types =
        {
            ChallengeType.SpeedRun,
            ChallengeType.NoDeath,
            ChallengeType.HighCombo,
            ChallengeType.CollectAll,
            ChallengeType.PacifistRun
        };

        for (int i = 0; i < dailyChallengeCount; i++)
        {
            var type = types[rng.Next(types.Length)];

            var data = new ChallengeData
            {
                challengeId = $"daily_{i}_{seed}",
                nameKey = $"challenge_{type.ToString().ToLower()}_name",
                descriptionKey = $"challenge_{type.ToString().ToLower()}_desc",
                type = type,
                coinReward = dailyChallengeBaseReward + i * 25,
            };

            // 根据类型设置条件
            switch (type)
            {
                case ChallengeType.SpeedRun:
                    data.timeLimit = 90f + rng.Next(0, 60);
                    break;
                case ChallengeType.NoDeath:
                    data.maxDeaths = 0;
                    break;
                case ChallengeType.HighCombo:
                    data.minCombo = 5 + rng.Next(0, 10);
                    break;
                case ChallengeType.CollectAll:
                    data.targetCount = -1; // 动态获取
                    break;
                case ChallengeType.PacifistRun:
                    data.maxDeaths = 3;
                    break;
            }

            // 随机关卡（已解锁的）
            data.requiredChapter = rng.Next(1, 6);
            data.requiredLevel = rng.Next(1, 5);

            var instance = new ChallengeInstance
            {
                data = data,
                isCompleted = IsChallengeCompleted(data.challengeId)
            };

            dailyChallenges.Add(instance);
        }
    }

    private int GetDaySeed()
    {
        var now = System.DateTime.UtcNow;
        return now.Year * 10000 + now.Month * 100 + now.Day;
    }
}
