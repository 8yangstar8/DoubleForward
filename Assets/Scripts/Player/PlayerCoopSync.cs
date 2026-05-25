using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 双人同步检测系统 - 检测两名玩家的同步行为
/// 同步跳跃、同步攻击、同步到达等合作表演
/// 同步动作触发特殊增益和评分加成
/// </summary>
public class PlayerCoopSync : MonoBehaviour
{
    public static PlayerCoopSync Instance { get; private set; }

    [Header("同步窗口")]
    [SerializeField] private float syncWindow = 0.3f;        // 同步判定窗口（秒）
    [SerializeField] private float perfectSyncWindow = 0.1f; // 完美同步窗口

    [Header("同步奖励")]
    [SerializeField] private int syncScoreBonus = 50;
    [SerializeField] private int perfectSyncBonus = 150;
    [SerializeField] private float syncSpeedBoostDuration = 3f;
    [SerializeField] private float syncSpeedBoostMagnitude = 1.3f;

    [Header("同步特效")]
    [SerializeField] private GameObject syncVFXPrefab;
    [SerializeField] private GameObject perfectSyncVFXPrefab;
    [SerializeField] private Color syncFlashColor = new Color(1f, 0.8f, 0.2f);

    // 动作记录
    private Dictionary<ActionType, float[]> actionTimestamps;
    private PlayerController[] players = new PlayerController[2];

    // 统计
    private int totalSyncActions;
    private int perfectSyncActions;
    private int currentSyncStreak;
    private int maxSyncStreak;

    public int TotalSyncActions => totalSyncActions;
    public int PerfectSyncActions => perfectSyncActions;
    public int CurrentSyncStreak => currentSyncStreak;
    public int MaxSyncStreak => maxSyncStreak;

    public event System.Action<ActionType, bool> OnSyncDetected; // actionType, isPerfect
    public event System.Action<int> OnSyncStreakChanged;

    public enum ActionType
    {
        Jump,
        Attack,
        Ability,
        Dash,
        Interact,
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        actionTimestamps = new Dictionary<ActionType, float[]>();
        foreach (ActionType type in System.Enum.GetValues(typeof(ActionType)))
        {
            actionTimestamps[type] = new float[] { -999f, -999f };
        }
    }

    void Start()
    {
        FindPlayers();
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 记录玩家动作（由PlayerController/PlayerCombat调用）
    /// </summary>
    public void RecordAction(int playerIndex, ActionType action)
    {
        if (playerIndex < 0 || playerIndex > 1) return;

        float now = Time.time;
        actionTimestamps[action][playerIndex] = now;

        // 检查另一个玩家的同一动作
        int otherIndex = 1 - playerIndex;
        float otherTime = actionTimestamps[action][otherIndex];
        float timeDiff = Mathf.Abs(now - otherTime);

        if (timeDiff <= perfectSyncWindow)
        {
            // 完美同步!
            TriggerSync(action, true);
        }
        else if (timeDiff <= syncWindow)
        {
            // 普通同步
            TriggerSync(action, false);
        }
        else
        {
            // 没有同步 — 中断连续计数
            if (currentSyncStreak > 0)
            {
                currentSyncStreak = 0;
                OnSyncStreakChanged?.Invoke(currentSyncStreak);
            }
        }
    }

    /// <summary>
    /// 重置统计（关卡开始时）
    /// </summary>
    public void ResetStats()
    {
        totalSyncActions = 0;
        perfectSyncActions = 0;
        currentSyncStreak = 0;
        maxSyncStreak = 0;

        foreach (var key in actionTimestamps.Keys)
        {
            actionTimestamps[key][0] = -999f;
            actionTimestamps[key][1] = -999f;
        }
    }

    // ==================== 内部逻辑 ====================

    private void TriggerSync(ActionType action, bool isPerfect)
    {
        totalSyncActions++;
        currentSyncStreak++;

        if (isPerfect) perfectSyncActions++;

        if (currentSyncStreak > maxSyncStreak)
            maxSyncStreak = currentSyncStreak;

        // 评分加成
        if (ComboSystem.Instance != null)
        {
            string actionName = isPerfect ? $"perfect_sync_{action}" : $"sync_{action}";
            int score = isPerfect ? perfectSyncBonus : syncScoreBonus;
            ComboSystem.Instance.PerfectAction(actionName, score);
        }

        // 合作能量增加
        float meterAmount = isPerfect ? 10f : 5f;
        CoopAbilitySystem.Instance?.AddMeter(meterAmount);

        // 同步速度增益
        foreach (var player in players)
        {
            if (player == null) continue;
            var buffSystem = player.GetComponent<PlayerBuffSystem>();
            if (buffSystem != null)
            {
                buffSystem.AddSpeedBoost(syncSpeedBoostDuration, syncSpeedBoostMagnitude);
            }
        }

        // 视觉效果
        SpawnSyncVFX(isPerfect);

        // 音效
        if (SoundFeedback.Instance != null)
        {
            string sfx = isPerfect ? "perfect_sync" : "sync_action";
            SoundFeedback.Instance.Play(sfx);
        }

        // 触觉
        if (HapticFeedback.Instance != null)
        {
            if (isPerfect)
                HapticFeedback.Instance.Success();
            else
                HapticFeedback.Instance.Light();
        }

        OnSyncDetected?.Invoke(action, isPerfect);
        OnSyncStreakChanged?.Invoke(currentSyncStreak);

        // 成就追踪
        if (AchievementTracker.Instance != null)
        {
            if (isPerfect)
                AchievementSystem.Instance?.UpdateProgress("sync_master", 1);
            if (currentSyncStreak >= 5)
                AchievementSystem.Instance?.Unlock("sync_streak_5");
            if (currentSyncStreak >= 10)
                AchievementSystem.Instance?.Unlock("sync_streak_10");
        }

        // 重置时间戳防止双触发
        foreach (var key in actionTimestamps.Keys)
        {
            if (key == action)
            {
                actionTimestamps[key][0] = -999f;
                actionTimestamps[key][1] = -999f;
            }
        }
    }

    private void SpawnSyncVFX(bool isPerfect)
    {
        if (players[0] == null || players[1] == null) return;

        Vector3 midPoint = (players[0].transform.position + players[1].transform.position) / 2f;

        // 使用VFXManager
        if (VFXManager.Instance != null)
        {
            string effect = isPerfect ? "perfect_sync" : "sync_flash";
            VFXManager.Instance.Play(effect, midPoint);
        }

        // 备用：直接实例化
        var prefab = isPerfect ? perfectSyncVFXPrefab : syncVFXPrefab;
        if (prefab != null)
        {
            var vfx = Instantiate(prefab, midPoint, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        // 屏幕闪光
        if (CameraEffects.Instance != null && isPerfect)
        {
            CameraEffects.Instance.ChromaticPulse(0.5f, 0.2f);
        }
    }

    private void FindPlayers()
    {
        var found = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in found)
        {
            int idx = p.PlayerIndex;
            if (idx >= 0 && idx < 2)
                players[idx] = p;
        }
    }
}
