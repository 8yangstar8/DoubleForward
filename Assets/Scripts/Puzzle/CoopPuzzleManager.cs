using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 合作谜题管理器 - 管理需要两名玩家同时操作的谜题
/// 追踪谜题状态、提供提示、奖励同步操作
/// </summary>
public class CoopPuzzleManager : MonoBehaviour
{
    public static CoopPuzzleManager Instance { get; private set; }

    [Header("合作谜题追踪")]
    [SerializeField] private float simultaneousWindow = 0.5f;    // 同时操作判定窗口
    [SerializeField] private float hintDelay = 20f;              // 卡关多久显示提示

    [Header("奖励")]
    [SerializeField] private int puzzleSolveScore = 200;
    [SerializeField] private int simultaneousBonus = 100;
    [SerializeField] private float coopMeterOnSolve = 15f;

    // 运行时
    private List<CoopPuzzleState> activePuzzles = new List<CoopPuzzleState>();
    private int solvedCount;
    private int simultaneousSolves;

    public int SolvedCount => solvedCount;
    public int SimultaneousSolves => simultaneousSolves;

    public event System.Action<string, bool> OnPuzzleSolved;     // puzzleId, wasSimultaneous
    public event System.Action<string> OnPuzzleHintNeeded;       // puzzleId

    private class CoopPuzzleState
    {
        public string puzzleId;
        public CoopMechanismBase mechanism;
        public float activationTime;
        public bool isSolved;
        public float timeStuck;
        public bool hintGiven;
        public float player0ActivateTime = -999f;
        public float player1ActivateTime = -999f;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // 检查卡关状态
        UpdateStuckDetection();
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 注册合作谜题
    /// </summary>
    public void RegisterPuzzle(string puzzleId, CoopMechanismBase mechanism)
    {
        // 避免重复注册
        foreach (var existing in activePuzzles)
        {
            if (existing.puzzleId == puzzleId) return;
        }

        activePuzzles.Add(new CoopPuzzleState
        {
            puzzleId = puzzleId,
            mechanism = mechanism,
            activationTime = Time.time
        });
    }

    /// <summary>
    /// 记录玩家操作（判定同时操作）
    /// </summary>
    public void RecordPlayerAction(string puzzleId, int playerIndex)
    {
        var state = GetState(puzzleId);
        if (state == null || state.isSolved) return;

        float now = Time.time;
        if (playerIndex == 0) state.player0ActivateTime = now;
        else state.player1ActivateTime = now;

        // 检查是否同时操作
        float timeDiff = Mathf.Abs(state.player0ActivateTime - state.player1ActivateTime);
        if (timeDiff <= simultaneousWindow)
        {
            CompletePuzzle(puzzleId, true);
        }
    }

    /// <summary>
    /// 标记谜题完成
    /// </summary>
    public void CompletePuzzle(string puzzleId, bool wasSimultaneous = false)
    {
        var state = GetState(puzzleId);
        if (state == null || state.isSolved) return;

        state.isSolved = true;
        solvedCount++;
        if (wasSimultaneous) simultaneousSolves++;

        // 评分
        int score = puzzleSolveScore;
        if (wasSimultaneous) score += simultaneousBonus;

        if (ComboSystem.Instance != null)
        {
            string action = wasSimultaneous ? "coop_puzzle_sync" : "coop_puzzle";
            ComboSystem.Instance.PerfectAction(action, score);
        }

        // 合作能量
        if (CoopAbilitySystem.Instance != null)
            CoopAbilitySystem.Instance.AddMeter(coopMeterOnSolve);

        // 发布事件
        EventBus.Publish(new PuzzleSolvedEvent
        {
            puzzleId = puzzleId,
            puzzleType = "coop"
        });

        // 音效/特效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(wasSimultaneous ? "puzzle_perfect" : "puzzle_solve");

        if (wasSimultaneous && VFXManager.Instance != null)
        {
            Vector3 pos = state.mechanism != null
                ? state.mechanism.transform.position
                : Vector3.zero;
            VFXManager.Instance.Play("coop_puzzle_solve", pos);
        }

        // 触觉
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Success();

        OnPuzzleSolved?.Invoke(puzzleId, wasSimultaneous);
    }

    /// <summary>
    /// 重置所有谜题状态（关卡重启时）
    /// </summary>
    public void ResetAll()
    {
        activePuzzles.Clear();
        solvedCount = 0;
        simultaneousSolves = 0;
    }

    /// <summary>
    /// 获取未解谜题数量
    /// </summary>
    public int GetUnsolvedCount()
    {
        int count = 0;
        foreach (var state in activePuzzles)
        {
            if (!state.isSolved) count++;
        }
        return count;
    }

    // ==================== 内部方法 ====================

    private void UpdateStuckDetection()
    {
        foreach (var state in activePuzzles)
        {
            if (state.isSolved || state.hintGiven) continue;

            state.timeStuck += Time.deltaTime;

            if (state.timeStuck >= hintDelay)
            {
                state.hintGiven = true;
                OnPuzzleHintNeeded?.Invoke(state.puzzleId);

                // 通过EventBus通知提示系统（避免跨程序集依赖）
                EventBus.Publish(new HintRequestEvent
                {
                    textKey = "tip_coop_mechanism",
                    fallbackText = "这个机关需要两人同时操作！",
                    duration = 5f
                });
            }
        }
    }

    private CoopPuzzleState GetState(string puzzleId)
    {
        foreach (var state in activePuzzles)
        {
            if (state.puzzleId == puzzleId) return state;
        }
        return null;
    }

    private void OnLevelStart(LevelStartEvent e)
    {
        ResetAll();
    }
}

/// <summary>
/// 合作机关基类 - 需要两名玩家交互的机关继承此类
/// </summary>
public abstract class CoopMechanismBase : MonoBehaviour
{
    [SerializeField] protected string puzzleId;
    [SerializeField] protected bool requireSimultaneous = true;

    protected bool player0Active;
    protected bool player1Active;
    protected bool isCompleted;

    protected virtual void Start()
    {
        if (CoopPuzzleManager.Instance != null)
            CoopPuzzleManager.Instance.RegisterPuzzle(puzzleId, this);
    }

    /// <summary>
    /// 玩家激活此机关
    /// </summary>
    public virtual void Activate(int playerIndex)
    {
        if (isCompleted) return;

        if (playerIndex == 0) player0Active = true;
        else player1Active = true;

        if (CoopPuzzleManager.Instance != null)
            CoopPuzzleManager.Instance.RecordPlayerAction(puzzleId, playerIndex);

        OnActivated(playerIndex);

        // 检查完成条件
        if (!requireSimultaneous && (player0Active || player1Active))
        {
            // 非同时操作型谜题，单人也可完成部分
        }
    }

    /// <summary>
    /// 玩家取消激活
    /// </summary>
    public virtual void Deactivate(int playerIndex)
    {
        if (playerIndex == 0) player0Active = false;
        else player1Active = false;

        OnDeactivated(playerIndex);
    }

    protected abstract void OnActivated(int playerIndex);
    protected abstract void OnDeactivated(int playerIndex);
}
