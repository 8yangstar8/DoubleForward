using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 关卡完成条件检查器 - 集中管理关卡的通关条件
/// 支持多种条件组合：到达终点、消灭所有敌人、收集所有物品、
/// 解开所有谜题、Boss击败、双人到达等
/// </summary>
public class LevelCompletionChecker : MonoBehaviour
{
    public static LevelCompletionChecker Instance { get; private set; }

    [Header("完成条件")]
    [SerializeField] private CompletionCondition[] conditions;
    [SerializeField] private bool requireAllConditions = true;  // true=全部满足, false=任一满足

    [Header("通关触发")]
    [SerializeField] private float completionDelay = 1f;     // 条件满足后延迟触发
    [SerializeField] private bool autoComplete = true;       // 自动检测完成

    // 状态
    private Dictionary<string, bool> conditionStates = new Dictionary<string, bool>();
    private bool isCompleted;
    private float completionTimer;
    private bool conditionsMetPending;

    // 运行时计数
    private int enemiesDefeated;
    private int totalEnemies;
    private int collectiblesGathered;
    private int totalCollectibles;
    private int puzzlesSolved;
    private int totalPuzzles;

    public bool IsCompleted => isCompleted;
    public float CompletionPercent => CalculateProgress();

    public event System.Action OnAllConditionsMet;
    public event System.Action OnLevelCompleted;

    [System.Serializable]
    public class CompletionCondition
    {
        public string id;
        public ConditionType type;
        public bool isRequired = true;
        [Tooltip("仅ReachGoal类型: 需要两位玩家都到达")]
        public bool requireBothPlayers = false;
        [Tooltip("仅Custom类型: 手动调用MarkCondition完成")]
        public string customDescription;
    }

    public enum ConditionType
    {
        ReachGoal,           // 到达终点区域
        DefeatAllEnemies,    // 消灭所有敌人
        CollectAllItems,     // 收集所有收集品
        SolveAllPuzzles,     // 完成所有谜题
        DefeatBoss,          // 击败Boss
        SurviveTime,         // 存活一定时间
        Custom               // 自定义（脚本控制）
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 初始化条件状态
        if (conditions != null)
        {
            foreach (var cond in conditions)
                conditionStates[cond.id] = false;
        }
    }

    void Start()
    {
        // 订阅事件
        EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Subscribe<CollectiblePickedEvent>(OnCollectiblePicked);
        EventBus.Subscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);

        // 统计关卡内的目标总数
        CountLevelObjectives();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Unsubscribe<CollectiblePickedEvent>(OnCollectiblePicked);
        EventBus.Unsubscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (isCompleted) return;

        // 延迟完成
        if (conditionsMetPending)
        {
            completionTimer -= Time.deltaTime;
            if (completionTimer <= 0)
            {
                CompleteLevel();
            }
        }

        // 自动检测
        if (autoComplete)
            CheckConditions();
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 手动标记某个条件为完成
    /// </summary>
    public void MarkCondition(string conditionId, bool completed = true)
    {
        if (conditionStates.ContainsKey(conditionId))
        {
            conditionStates[conditionId] = completed;
            CheckConditions();
        }
    }

    /// <summary>
    /// 玩家到达终点区域时调用
    /// </summary>
    public void OnPlayerReachedGoal(int playerIndex)
    {
        foreach (var cond in conditions)
        {
            if (cond.type != ConditionType.ReachGoal) continue;

            if (cond.requireBothPlayers)
            {
                // 需要检查两个玩家是否都到达
                // 标记当前玩家，由Goal触发器分别调用
                string key = $"{cond.id}_p{playerIndex}";
                PlayerPrefs.SetInt(key, 1);

                bool p0 = PlayerPrefs.GetInt($"{cond.id}_p0", 0) == 1;
                bool p1 = PlayerPrefs.GetInt($"{cond.id}_p1", 0) == 1;

                if (p0 && p1)
                    conditionStates[cond.id] = true;
            }
            else
            {
                conditionStates[cond.id] = true;
            }
        }

        CheckConditions();
    }

    /// <summary>
    /// 获取条件完成状态
    /// </summary>
    public bool IsConditionMet(string conditionId)
    {
        return conditionStates.ContainsKey(conditionId) && conditionStates[conditionId];
    }

    /// <summary>
    /// 强制完成关卡（调试用）
    /// </summary>
    public void ForceComplete()
    {
        foreach (var key in new List<string>(conditionStates.Keys))
            conditionStates[key] = true;
        CompleteLevel();
    }

    // ==================== 内部逻辑 ====================

    private void CheckConditions()
    {
        if (isCompleted || conditionsMetPending) return;

        bool allMet = true;
        bool anyMet = false;

        foreach (var cond in conditions)
        {
            if (!cond.isRequired) continue;

            bool met = conditionStates.ContainsKey(cond.id) && conditionStates[cond.id];

            if (met) anyMet = true;
            else allMet = false;
        }

        bool shouldComplete = requireAllConditions ? allMet : anyMet;

        if (shouldComplete && !conditionsMetPending)
        {
            conditionsMetPending = true;
            completionTimer = completionDelay;
            OnAllConditionsMet?.Invoke();
        }
    }

    private void CompleteLevel()
    {
        if (isCompleted) return;
        isCompleted = true;

        float levelTime = LevelManager.Instance != null
            ? LevelManager.Instance.GetLevelTime()
            : 0f;

        // 通知GameFlowManager
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.CompleteLevelFlow(
                levelTime, collectiblesGathered, totalCollectibles);
        }

        // 发布事件
        int chapter = GameFlowManager.Instance?.CurrentChapter ?? 1;
        int level = GameFlowManager.Instance?.CurrentLevel ?? 1;

        EventBus.Publish(new LevelCompleteEvent
        {
            chapter = chapter,
            level = level,
            stars = CalculateStars(),
            time = levelTime,
            collectibles = collectiblesGathered
        });

        OnLevelCompleted?.Invoke();

        Debug.Log($"[LevelComplete] Chapter {chapter} Level {level} | " +
            $"Time: {levelTime:F1}s | Collectibles: {collectiblesGathered}/{totalCollectibles}");
    }

    // ==================== 事件处理 ====================

    private void OnEnemyDefeated(EnemyDefeatedEvent e)
    {
        enemiesDefeated++;

        foreach (var cond in conditions)
        {
            if (cond.type == ConditionType.DefeatAllEnemies)
            {
                if (enemiesDefeated >= totalEnemies)
                    conditionStates[cond.id] = true;
            }
        }
    }

    private void OnCollectiblePicked(CollectiblePickedEvent e)
    {
        collectiblesGathered = e.collected;

        foreach (var cond in conditions)
        {
            if (cond.type == ConditionType.CollectAllItems)
            {
                if (collectiblesGathered >= totalCollectibles)
                    conditionStates[cond.id] = true;
            }
        }
    }

    private void OnPuzzleSolved(PuzzleSolvedEvent e)
    {
        puzzlesSolved++;

        foreach (var cond in conditions)
        {
            if (cond.type == ConditionType.SolveAllPuzzles)
            {
                if (puzzlesSolved >= totalPuzzles)
                    conditionStates[cond.id] = true;
            }
        }
    }

    private void OnBossDefeated(BossDefeatedEvent e)
    {
        foreach (var cond in conditions)
        {
            if (cond.type == ConditionType.DefeatBoss)
                conditionStates[cond.id] = true;
        }
    }

    // ==================== 辅助方法 ====================

    private void CountLevelObjectives()
    {
        totalEnemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None).Length;
        totalCollectibles = LevelManager.Instance != null
            ? LevelManager.Instance.GetTotalCollectibles()
            : FindObjectsByType<Collectible>(FindObjectsSortMode.None).Length;

        // 谜题数量（含压力板、合作机关等）
        totalPuzzles = FindObjectsByType<PressurePlate>(FindObjectsSortMode.None).Length
            + FindObjectsByType<CoopMechanism>(FindObjectsSortMode.None).Length;
    }

    private float CalculateProgress()
    {
        if (conditions == null || conditions.Length == 0) return 0f;

        int met = 0;
        int total = 0;

        foreach (var cond in conditions)
        {
            if (!cond.isRequired) continue;
            total++;
            if (conditionStates.ContainsKey(cond.id) && conditionStates[cond.id])
                met++;
        }

        return total > 0 ? (float)met / total : 0f;
    }

    private int CalculateStars()
    {
        int stars = 1; // 完成 = 1星

        // 2星: 在时间内完成
        float levelTime = LevelManager.Instance?.GetLevelTime() ?? 999f;
        if (levelTime < 180f) stars = 2;

        // 3星: 全收集 + 时间
        if (collectiblesGathered >= totalCollectibles && levelTime < 150f)
            stars = 3;

        return stars;
    }
}
