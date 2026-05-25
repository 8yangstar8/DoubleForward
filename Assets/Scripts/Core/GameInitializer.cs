using UnityEngine;
using System.Collections;

/// <summary>
/// 游戏初始化器 - 确保所有核心Manager按正确顺序初始化
/// 放在Boot场景中，是所有系统的起点
/// </summary>
public class GameInitializer : MonoBehaviour
{
    [Header("Manager预制体")]
    [SerializeField] private GameObject gameManagerPrefab;
    [SerializeField] private GameObject audioManagerPrefab;
    [SerializeField] private GameObject inputManagerPrefab;
    [SerializeField] private GameObject saveSystemPrefab;
    [SerializeField] private GameObject gameFlowPrefab;
    [SerializeField] private GameObject localizationPrefab;
    [SerializeField] private GameObject performancePrefab;
    [SerializeField] private GameObject difficultyPrefab;
    [SerializeField] private GameObject accessibilityPrefab;
    [SerializeField] private GameObject analyticsTrackerPrefab;
    [SerializeField] private GameObject mobileServicesPrefab;
    [SerializeField] private GameObject vfxManagerPrefab;
    [SerializeField] private GameObject objectPoolPrefab;
    [SerializeField] private GameObject achievementSystemPrefab;
    [SerializeField] private GameObject gameStatsPrefab;
    [SerializeField] private GameObject comboSystemPrefab;
    [SerializeField] private GameObject soundFeedbackPrefab;
    [SerializeField] private GameObject screenAdapterPrefab;
    [SerializeField] private GameObject gamepadAdapterPrefab;
    [SerializeField] private GameObject permissionManagerPrefab;

    [Header("新增Manager预制体")]
    [SerializeField] private GameObject currencyManagerPrefab;
    [SerializeField] private GameObject dailyRewardSystemPrefab;
    [SerializeField] private GameObject hapticFeedbackPrefab;
    [SerializeField] private GameObject leaderboardManagerPrefab;
    [SerializeField] private GameObject notificationSchedulerPrefab;
    [SerializeField] private GameObject cloudSaveManagerPrefab;
    [SerializeField] private GameObject skinManagerPrefab;
    [SerializeField] private GameObject scoreManagerPrefab;
    [SerializeField] private GameObject settingsPersistencePrefab;
    [SerializeField] private GameObject autoSaveSystemPrefab;
    [SerializeField] private GameObject timeManagerPrefab;

    [Header("UI预制体")]
    [SerializeField] private GameObject debugOverlayPrefab;

    [Header("设置")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private int targetFrameRate = 60;

    private bool isInitialized = false;

    public static bool IsReady { get; private set; }

    public event System.Action OnInitializationComplete;

    void Awake()
    {
        // 防止重复初始化（场景重新加载时）
        if (IsReady)
        {
            Destroy(gameObject);
            return;
        }

        Application.targetFrameRate = targetFrameRate;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        if (autoStart)
            StartCoroutine(InitializeAll());
    }

    /// <summary>
    /// 按顺序初始化所有核心系统
    /// </summary>
    public IEnumerator InitializeAll()
    {
        if (isInitialized) yield break;
        isInitialized = true;

        Debug.Log("[GameInit] Starting initialization...");

        // ====== 第1层：基础核心（无依赖） ======
        SpawnIfNeeded<ObjectPool>(objectPoolPrefab);
        SpawnIfNeeded<AudioManager>(audioManagerPrefab);
        SpawnIfNeeded<InputManager>(inputManagerPrefab);
        yield return null; // 等一帧让Awake执行

        // ====== 第2层：数据系统 ======
        SpawnIfNeeded<SaveSystem>(saveSystemPrefab);
        SpawnIfNeeded<LocalizationSystem>(localizationPrefab);
        SpawnIfNeeded<SettingsPersistence>(settingsPersistencePrefab);
        SpawnIfNeeded<GameStats>(gameStatsPrefab);
        yield return null;

        // ====== 第3层：游戏逻辑系统 ======
        SpawnIfNeeded<GameManager>(gameManagerPrefab);
        SpawnIfNeeded<DifficultyManager>(difficultyPrefab);
        SpawnIfNeeded<AchievementSystem>(achievementSystemPrefab);
        SpawnIfNeeded<ComboSystem>(comboSystemPrefab);
        SpawnIfNeeded<CurrencyManager>(currencyManagerPrefab);
        SpawnIfNeeded<ScoreManager>(scoreManagerPrefab);
        SpawnIfNeeded<LeaderboardManager>(leaderboardManagerPrefab);
        yield return null;

        // ====== 第4层：平台与性能 ======
        SpawnIfNeeded<PerformanceManager>(performancePrefab);
        SpawnIfNeeded<ScreenAdapter>(screenAdapterPrefab);
        SpawnIfNeeded<AccessibilityManager>(accessibilityPrefab);
        SpawnIfNeeded<GamepadAdapter>(gamepadAdapterPrefab);
        SpawnIfNeeded<AndroidPermissionManager>(permissionManagerPrefab);
        SpawnIfNeeded<HapticFeedback>(hapticFeedbackPrefab);
        SpawnIfNeeded<NotificationScheduler>(notificationSchedulerPrefab);
        yield return null;

        // ====== 第5层：辅助服务 ======
        SpawnIfNeeded<VFXManager>(vfxManagerPrefab);
        SpawnIfNeeded<SoundFeedback>(soundFeedbackPrefab);
        SpawnIfNeeded<AnalyticsTracker>(analyticsTrackerPrefab);
        SpawnIfNeeded<MobileServices>(mobileServicesPrefab);
        SpawnIfNeeded<DailyRewardSystem>(dailyRewardSystemPrefab);
        SpawnIfNeeded<SkinManager>(skinManagerPrefab);
        SpawnIfNeeded<CloudSaveManager>(cloudSaveManagerPrefab);
        SpawnIfNeeded<TimeManager>(timeManagerPrefab);
        SpawnIfNeeded<AutoSaveSystem>(autoSaveSystemPrefab);
        yield return null;

        // ====== 第6层：流程控制（依赖上面所有系统） ======
        SpawnIfNeeded<GameFlowManager>(gameFlowPrefab);
        yield return null;

        // ====== 可选：调试工具 ======
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugOverlayPrefab != null)
            Instantiate(debugOverlayPrefab);
#endif

        // 连接事件系统
        ConnectEventListeners();

        IsReady = true;
        Debug.Log("[GameInit] All systems initialized!");

        OnInitializationComplete?.Invoke();
    }

    /// <summary>
    /// 如果单例不存在，则从预制体生成
    /// </summary>
    private T SpawnIfNeeded<T>(GameObject prefab) where T : MonoBehaviour
    {
        // 检查是否已存在（如场景中已放置）
        var existing = FindAnyObjectByType<T>();
        if (existing != null) return existing;

        if (prefab == null)
        {
            Debug.LogWarning($"[GameInit] Prefab for {typeof(T).Name} is not assigned");
            return null;
        }

        var obj = Instantiate(prefab);
        obj.name = typeof(T).Name;

        var component = obj.GetComponent<T>();
        if (component == null)
        {
            Debug.LogError($"[GameInit] Prefab does not contain {typeof(T).Name} component");
            Destroy(obj);
            return null;
        }

        return component;
    }

    /// <summary>
    /// 连接各系统间的事件监听
    /// </summary>
    private void ConnectEventListeners()
    {
        // 难度变化 → 发布事件
        if (DifficultyManager.Instance != null)
        {
            DifficultyManager.Instance.OnDifficultyChanged += (modifier) =>
            {
                EventBus.Publish(new DifficultyChangedEvent { modifier = modifier });
            };
        }

        // 游戏流程变化 → 追踪分析
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.OnStateChanged += (oldState, newState) =>
            {
                if (newState == GameFlowManager.FlowState.Playing)
                {
                    AnalyticsTracker.Instance?.TrackLevelStart(
                        GameFlowManager.Instance.CurrentChapter,
                        GameFlowManager.Instance.CurrentLevel
                    );
                }
            };
        }

        // 成就解锁 → 追踪分析
        if (AchievementSystem.Instance != null)
        {
            // AchievementSystem已有自己的事件，通过EventBus补充
        }

        // 订阅EventBus全局事件
        EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Subscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);

        Debug.Log("[GameInit] Event listeners connected");
    }

    // ============ 全局事件处理 ============

    private void OnPlayerDeath(PlayerDeathEvent evt)
    {
        AnalyticsTracker.Instance?.TrackPlayerDeath(
            GameFlowManager.Instance?.CurrentChapter ?? 0,
            GameFlowManager.Instance?.CurrentLevel ?? 0,
            evt.deathPosition,
            "unknown"
        );
    }

    private void OnLevelComplete(LevelCompleteEvent evt)
    {
        AnalyticsTracker.Instance?.TrackLevelComplete(
            evt.chapter, evt.level, evt.stars, evt.time, 0
        );

        // 检查评分提示
        if (MobileServices.Instance != null && SaveSystem.Instance != null)
        {
            int totalLevels = 0;
            foreach (bool completed in SaveSystem.Instance.Data.levelsCompleted)
                if (completed) totalLevels++;
            if (MobileServices.Instance.ShouldShowRatePrompt(totalLevels))
            {
                MobileServices.Instance.ShowRatePrompt(totalLevels);
            }
        }
    }

    private void OnPuzzleSolved(PuzzleSolvedEvent evt)
    {
        // 合作能量增加
        CoopAbilitySystem.Instance?.AddMeterForPuzzle();
    }

    private void OnEnemyDefeated(EnemyDefeatedEvent evt)
    {
        // 合作能量增加
        CoopAbilitySystem.Instance?.AddMeterForHit();
    }

    void OnDestroy()
    {
        // 退订EventBus
        EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Unsubscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
    }
}
