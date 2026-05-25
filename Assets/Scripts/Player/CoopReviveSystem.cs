using UnityEngine;
using System.Collections;

/// <summary>
/// 双人互助复活系统 - 当一方倒下时，另一方可以在限定时间内前往复活
/// 核心合作机制：鼓励玩家相互保护和配合
/// 若两人同时倒下则Game Over
/// </summary>
public class CoopReviveSystem : MonoBehaviour
{
    public static CoopReviveSystem Instance { get; private set; }

    [Header("复活设置")]
    [SerializeField] private float reviveTime = 3f;          // 长按复活所需时间
    [SerializeField] private float bleedoutTime = 15f;       // 倒地等待时间（超时则直接重生）
    [SerializeField] private float reviveRadius = 2f;        // 复活触发范围
    [SerializeField] private float reviveHealthPercent = 0.5f; // 复活后生命百分比

    [Header("倒地状态")]
    [SerializeField] private float downedMoveSpeed = 1f;     // 倒地时微量移动
    [SerializeField] private bool allowDownedMovement = true;

    [Header("视觉")]
    [SerializeField] private GameObject reviveIndicatorPrefab;
    [SerializeField] private GameObject reviveCompletedVFX;
    [SerializeField] private Color reviveProgressColor = new Color(0.2f, 1f, 0.4f);

    // 状态
    private DownedPlayerInfo[] downedPlayers = new DownedPlayerInfo[2];
    private bool isReviving;
    private float reviveProgress;
    private int revivingPlayerIndex = -1;     // 正在执行复活的玩家
    private int downedPlayerIndex = -1;       // 被复活的玩家

    private PlayerController[] players;
    private PlayerHealth[] healths;

    public float ReviveProgress => reviveProgress;
    public bool IsAnyoneDowned => downedPlayers[0].isDowned || downedPlayers[1].isDowned;

    public event System.Action<int> OnPlayerDowned;           // playerIndex
    public event System.Action<int, int> OnPlayerRevived;     // revivedIndex, reviverIndex
    public event System.Action OnBothPlayersDowned;           // Game Over

    private class DownedPlayerInfo
    {
        public bool isDowned;
        public float bleedoutTimer;
        public GameObject indicator;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        downedPlayers[0] = new DownedPlayerInfo();
        downedPlayers[1] = new DownedPlayerInfo();
    }

    void Start()
    {
        EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        FindPlayers();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (players == null || players.Length < 2) FindPlayers();
        if (players == null || players.Length < 2) return;

        // 更新倒地计时
        for (int i = 0; i < 2; i++)
        {
            if (!downedPlayers[i].isDowned) continue;

            downedPlayers[i].bleedoutTimer -= Time.deltaTime;

            // 更新指示器位置
            if (downedPlayers[i].indicator != null && players[i] != null)
            {
                downedPlayers[i].indicator.transform.position =
                    players[i].transform.position + Vector3.up * 1.5f;
            }

            // 超时 — 自动重生在检查点
            if (downedPlayers[i].bleedoutTimer <= 0)
            {
                ForceRespawn(i);
            }
        }

        // 检测复活
        CheckReviveInput();
    }

    // ==================== 事件处理 ====================

    private void OnPlayerDeath(PlayerDeathEvent e)
    {
        int idx = e.playerIndex;
        if (idx < 0 || idx >= 2) return;

        // 标记倒地
        downedPlayers[idx].isDowned = true;
        downedPlayers[idx].bleedoutTimer = bleedoutTime;

        // 创建复活指示器
        if (reviveIndicatorPrefab != null && players[idx] != null)
        {
            downedPlayers[idx].indicator = Instantiate(reviveIndicatorPrefab,
                players[idx].transform.position + Vector3.up * 1.5f,
                Quaternion.identity);
        }

        OnPlayerDowned?.Invoke(idx);

        // 检查是否两人都倒下
        if (downedPlayers[0].isDowned && downedPlayers[1].isDowned)
        {
            OnBothPlayersDowned?.Invoke();

            // 触发Game Over
            if (GameFlowManager.Instance != null)
                GameFlowManager.Instance.TriggerGameOver();
        }
    }

    // ==================== 复活逻辑 ====================

    private void CheckReviveInput()
    {
        for (int i = 0; i < 2; i++)
        {
            if (downedPlayers[i].isDowned) continue; // 跳过已倒地的玩家
            if (players[i] == null) continue;

            // 查找倒地的队友
            int otherIdx = 1 - i;
            if (!downedPlayers[otherIdx].isDowned) continue;
            if (players[otherIdx] == null) continue;

            // 检查距离
            float dist = Vector2.Distance(
                players[i].transform.position,
                players[otherIdx].transform.position);

            if (dist > reviveRadius) continue;

            // 检查交互键是否按住
            if (InputManager.Instance != null &&
                InputManager.Instance.GetInteractPressed(i))
            {
                StartRevive(i, otherIdx);
            }
        }

        // 如果正在复活，持续更新进度
        if (isReviving)
        {
            UpdateReviveProgress();
        }
    }

    private void StartRevive(int reviverIdx, int targetIdx)
    {
        if (isReviving) return;

        isReviving = true;
        reviveProgress = 0f;
        revivingPlayerIndex = reviverIdx;
        downedPlayerIndex = targetIdx;
    }

    private void UpdateReviveProgress()
    {
        if (revivingPlayerIndex < 0 || downedPlayerIndex < 0) return;

        // 检查复活者是否还在范围内
        if (players[revivingPlayerIndex] == null || players[downedPlayerIndex] == null)
        {
            CancelRevive();
            return;
        }

        float dist = Vector2.Distance(
            players[revivingPlayerIndex].transform.position,
            players[downedPlayerIndex].transform.position);

        if (dist > reviveRadius * 1.2f) // 稍微宽松一点避免抖动
        {
            CancelRevive();
            return;
        }

        // 增加进度
        reviveProgress += Time.deltaTime / reviveTime;

        if (reviveProgress >= 1f)
        {
            CompleteRevive();
        }
    }

    private void CompleteRevive()
    {
        int revivedIdx = downedPlayerIndex;
        int reviverIdx = revivingPlayerIndex;

        // 复活
        downedPlayers[revivedIdx].isDowned = false;

        // 清理指示器
        if (downedPlayers[revivedIdx].indicator != null)
            Destroy(downedPlayers[revivedIdx].indicator);

        // 恢复生命值
        if (healths[revivedIdx] != null)
        {
            int healAmount = Mathf.CeilToInt(healths[revivedIdx].MaxHealth * reviveHealthPercent);
            healths[revivedIdx].ResetHealth();
            // 设置为部分生命
            int targetHp = Mathf.Max(1, healAmount);
            while (healths[revivedIdx].CurrentHealth > targetHp)
                healths[revivedIdx].TakeDamage(1);
        }

        // 特效
        if (reviveCompletedVFX != null && players[revivedIdx] != null)
            Instantiate(reviveCompletedVFX, players[revivedIdx].transform.position, Quaternion.identity);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("revive_complete");

        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Success();

        // 重置状态
        isReviving = false;
        reviveProgress = 0f;
        revivingPlayerIndex = -1;
        downedPlayerIndex = -1;

        OnPlayerRevived?.Invoke(revivedIdx, reviverIdx);

        // 发布事件
        EventBus.Publish(new PlayerRespawnEvent
        {
            playerIndex = revivedIdx,
            spawnPosition = players[revivedIdx].transform.position
        });

        // 合作复活成就事件
        EventBus.Publish(new CoopReviveEvent
        {
            reviverIndex = reviverIdx,
            revivedIndex = revivedIdx
        });
    }

    private void CancelRevive()
    {
        isReviving = false;
        reviveProgress = 0f;
        revivingPlayerIndex = -1;
        downedPlayerIndex = -1;
    }

    private void ForceRespawn(int playerIndex)
    {
        downedPlayers[playerIndex].isDowned = false;

        if (downedPlayers[playerIndex].indicator != null)
            Destroy(downedPlayers[playerIndex].indicator);

        // 在最后检查点重生
        if (healths[playerIndex] != null)
            healths[playerIndex].ResetHealth();
    }

    private void FindPlayers()
    {
        var found = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        if (found.Length < 2) return;

        players = new PlayerController[2];
        healths = new PlayerHealth[2];

        foreach (var p in found)
        {
            int idx = p.PlayerIndex;
            if (idx >= 0 && idx < 2)
            {
                players[idx] = p;
                healths[idx] = p.GetComponent<PlayerHealth>();
            }
        }
    }

    /// <summary>
    /// 获取倒地玩家的剩余等待时间
    /// </summary>
    public float GetBleedoutTimeRemaining(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= 2) return 0f;
        return downedPlayers[playerIndex].isDowned ? downedPlayers[playerIndex].bleedoutTimer : 0f;
    }

    /// <summary>
    /// 检查某位玩家是否倒地
    /// </summary>
    public bool IsPlayerDowned(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= 2) return false;
        return downedPlayers[playerIndex].isDowned;
    }
}
