using UnityEngine;

/// <summary>
/// 自动存档系统 - 周期性和事件驱动的自动保存
/// 在检查点、关卡完成、Boss击败等关键时刻自动存档
/// 可通过SettingsPersistence开关
/// </summary>
public class AutoSaveSystem : MonoBehaviour
{
    public static AutoSaveSystem Instance { get; private set; }

    [Header("定时存档")]
    [SerializeField] private float autoSaveInterval = 120f;  // 每2分钟
    [SerializeField] private bool enablePeriodicSave = true;

    [Header("事件存档")]
    [SerializeField] private bool saveOnCheckpoint = true;
    [SerializeField] private bool saveOnLevelComplete = true;
    [SerializeField] private bool saveOnBossDefeat = true;
    [SerializeField] private bool saveOnCollectMajorItem = true;

    [Header("提示")]
    [SerializeField] private float saveIndicatorDuration = 1.5f;

    private float saveTimer;
    private float lastSaveTime;
    private bool isSaving;

    public event System.Action OnAutoSaveStarted;
    public event System.Action OnAutoSaveCompleted;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // 订阅事件
        if (saveOnCheckpoint)
            EventBus.Subscribe<CheckpointReachedEvent>(OnCheckpoint);
        if (saveOnLevelComplete)
            EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
        if (saveOnBossDefeat)
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<CheckpointReachedEvent>(OnCheckpoint);
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!IsAutoSaveEnabled()) return;
        if (!enablePeriodicSave) return;

        // 仅在游戏中定期存档
        if (GameFlowManager.Instance != null &&
            GameFlowManager.Instance.CurrentState != GameFlowManager.FlowState.Playing)
            return;

        saveTimer += Time.deltaTime;
        if (saveTimer >= autoSaveInterval)
        {
            saveTimer = 0f;
            TriggerAutoSave("periodic");
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 手动触发自动存档
    /// </summary>
    public void TriggerAutoSave(string reason = "manual")
    {
        if (isSaving) return;
        if (!IsAutoSaveEnabled() && reason != "force") return;

        isSaving = true;
        lastSaveTime = Time.time;
        OnAutoSaveStarted?.Invoke();

        // 执行存档
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.Save();
        }

        // 统计
        if (GameStats.Instance != null)
            GameStats.Instance.IncrementStat("auto_saves");

        Debug.Log($"[AutoSave] Saved ({reason})");

        isSaving = false;
        OnAutoSaveCompleted?.Invoke();
    }

    /// <summary>
    /// 获取上次存档距今时间
    /// </summary>
    public float TimeSinceLastSave => Time.time - lastSaveTime;

    // ==================== 事件处理 ====================

    private void OnCheckpoint(CheckpointReachedEvent e)
    {
        TriggerAutoSave("checkpoint");
    }

    private void OnLevelComplete(LevelCompleteEvent e)
    {
        TriggerAutoSave("level_complete");
    }

    private void OnBossDefeated(BossDefeatedEvent e)
    {
        TriggerAutoSave("boss_defeated");
    }

    private bool IsAutoSaveEnabled()
    {
        if (SettingsPersistence.Instance != null)
            return SettingsPersistence.Instance.AutoSave;
        return true;
    }
}
