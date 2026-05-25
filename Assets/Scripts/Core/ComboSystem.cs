using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 连击/评分系统 - 追踪玩家表现并计算关卡评分
/// 连击倍率、完美操作加分、时间奖励
/// </summary>
public class ComboSystem : MonoBehaviour
{
    public static ComboSystem Instance { get; private set; }

    [Header("连击设置")]
    [SerializeField] private float comboTimeout = 3f;       // 连击超时时间
    [SerializeField] private int maxComboMultiplier = 10;    // 最大连击倍率
    [SerializeField] private float comboDecayRate = 0.5f;    // 连击衰减速率

    [Header("评分权重")]
    [SerializeField] private float timeWeight = 0.3f;        // 时间占比
    [SerializeField] private float collectibleWeight = 0.3f;  // 收集品占比
    [SerializeField] private float comboWeight = 0.2f;       // 连击占比
    [SerializeField] private float deathPenalty = 0.2f;      // 死亡扣分占比

    [Header("星级阈值")]
    [SerializeField] private float threeStarThreshold = 0.85f;
    [SerializeField] private float twoStarThreshold = 0.6f;
    [SerializeField] private float oneStarThreshold = 0.3f;

    // 运行时数据
    private int currentCombo;
    private int maxComboReached;
    private float comboTimer;
    private int totalScore;
    private int deathCount;
    private int perfectActionsCount;
    private float levelStartTime;
    private bool isTracking;

    // 连击相关统计
    private int totalComboHits;
    private List<string> performedActions = new List<string>();

    public int CurrentCombo => currentCombo;
    public int MaxCombo => maxComboReached;
    public int TotalScore => totalScore;
    public int DeathCount => deathCount;
    public float ComboMultiplier => Mathf.Min(1f + currentCombo * 0.1f, maxComboMultiplier);
    public float ComboTimerNormalized => comboTimer / comboTimeout;
    public bool IsComboActive => currentCombo > 0 && comboTimer > 0;

    public event System.Action<int> OnComboChanged;      // 当前连击数
    public event System.Action OnComboBreak;              // 连击中断
    public event System.Action<int> OnScoreChanged;       // 分数变化
    public event System.Action<string, int> OnActionScore; // 动作名, 获得分数

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (!isTracking) return;

        // 连击倒计时
        if (currentCombo > 0 && comboTimer > 0)
        {
            comboTimer -= Time.deltaTime * comboDecayRate;
            if (comboTimer <= 0)
                BreakCombo();
        }
    }

    /// <summary>
    /// 开始追踪关卡表现
    /// </summary>
    public void StartTracking()
    {
        isTracking = true;
        currentCombo = 0;
        maxComboReached = 0;
        totalScore = 0;
        deathCount = 0;
        perfectActionsCount = 0;
        totalComboHits = 0;
        comboTimer = 0;
        levelStartTime = Time.time;
        performedActions.Clear();
    }

    /// <summary>
    /// 停止追踪
    /// </summary>
    public void StopTracking()
    {
        isTracking = false;
    }

    /// <summary>
    /// 增加连击（简化别名）
    /// </summary>
    public void AddCombo() => AddComboHit();

    /// <summary>
    /// 命中时增加连击（PlayerCombat/Projectile调用）
    /// </summary>
    public void OnHit() => AddComboHit("hit", 100);

    /// <summary>
    /// 增加连击（击杀敌人、解开谜题、精确跳跃等）
    /// </summary>
    public void AddComboHit(string actionName = "hit", int baseScore = 100)
    {
        if (!isTracking) return;

        currentCombo++;
        totalComboHits++;
        comboTimer = comboTimeout;

        if (currentCombo > maxComboReached)
            maxComboReached = currentCombo;

        int scoredPoints = Mathf.RoundToInt(baseScore * ComboMultiplier);
        totalScore += scoredPoints;

        performedActions.Add(actionName);

        OnComboChanged?.Invoke(currentCombo);
        OnScoreChanged?.Invoke(totalScore);
        OnActionScore?.Invoke(actionName, scoredPoints);

        // 通过EventBus发布（供AchievementTracker等跨程序集系统使用）
        EventBus.Publish(new ComboChangedEvent
        {
            comboCount = currentCombo,
            multiplier = ComboMultiplier
        });
    }

    /// <summary>
    /// 完美操作加分（如完美着陆、精准配合）
    /// </summary>
    public void PerfectAction(string actionName, int bonusScore = 200)
    {
        if (!isTracking) return;

        perfectActionsCount++;
        int scoredPoints = Mathf.RoundToInt(bonusScore * ComboMultiplier);
        totalScore += scoredPoints;

        OnActionScore?.Invoke($"Perfect {actionName}", scoredPoints);
        OnScoreChanged?.Invoke(totalScore);
    }

    /// <summary>
    /// 连击中断
    /// </summary>
    public void BreakCombo()
    {
        if (currentCombo > 0)
        {
            currentCombo = 0;
            comboTimer = 0;
            OnComboBreak?.Invoke();
            OnComboChanged?.Invoke(0);
        }
    }

    /// <summary>
    /// 记录死亡
    /// </summary>
    public void RecordDeath()
    {
        if (!isTracking) return;
        deathCount++;
        BreakCombo();
    }

    /// <summary>
    /// 计算关卡最终评分 (0~1)
    /// </summary>
    public LevelResult CalculateLevelResult(float parTime, int totalCollectibles, int collectedCount)
    {
        float elapsedTime = Time.time - levelStartTime;

        // 时间评分 (完成越快分越高)
        float timeScore = Mathf.Clamp01(parTime / Mathf.Max(elapsedTime, 1f));

        // 收集品评分
        float collectibleScore = totalCollectibles > 0 ?
            (float)collectedCount / totalCollectibles : 1f;

        // 连击评分 (基于最大连击)
        float comboScore = Mathf.Clamp01(maxComboReached / 20f);

        // 死亡惩罚
        float deathScore = Mathf.Clamp01(1f - deathCount * 0.15f);

        // 加权最终分数
        float finalScore = timeScore * timeWeight +
                          collectibleScore * collectibleWeight +
                          comboScore * comboWeight +
                          deathScore * deathPenalty;

        finalScore = Mathf.Clamp01(finalScore);

        // 计算星级
        int stars = 0;
        if (finalScore >= threeStarThreshold) stars = 3;
        else if (finalScore >= twoStarThreshold) stars = 2;
        else if (finalScore >= oneStarThreshold) stars = 1;

        return new LevelResult
        {
            finalScore = finalScore,
            stars = stars,
            elapsedTime = elapsedTime,
            collectiblesFound = collectedCount,
            totalCollectibles = totalCollectibles,
            maxCombo = maxComboReached,
            totalScore = totalScore,
            deathCount = deathCount,
            perfectActions = perfectActionsCount,
            timeScore = timeScore,
            collectibleScore = collectibleScore,
            comboScore = comboScore
        };
    }

    [System.Serializable]
    public class LevelResult
    {
        public float finalScore;
        public int stars;
        public float elapsedTime;
        public int collectiblesFound;
        public int totalCollectibles;
        public int maxCombo;
        public int totalScore;
        public int deathCount;
        public int perfectActions;
        public float timeScore;
        public float collectibleScore;
        public float comboScore;
    }
}
