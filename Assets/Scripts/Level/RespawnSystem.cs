using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 复活/重生系统 - 管理玩家死亡后的复活流程
/// 支持检查点复活、倒计时复活、队友复活
/// </summary>
public class RespawnSystem : MonoBehaviour
{
    public static RespawnSystem Instance { get; private set; }

    public enum RespawnMode
    {
        Checkpoint,         // 回到最近检查点
        Partner,            // 在队友身边复活
        TimedAuto,          // 倒计时自动复活
        PartnerRevive       // 队友手动复活（走到身边按键）
    }

    [Header("复活设置")]
    [SerializeField] private RespawnMode respawnMode = RespawnMode.PartnerRevive;
    [SerializeField] private float respawnDelay = 2f;            // 复活延迟
    [SerializeField] private float autoRespawnTime = 10f;        // 自动复活倒计时（仅TimedAuto模式）
    [SerializeField] private float invincibilityDuration = 2f;   // 复活后无敌时间
    [SerializeField] private float reviveRadius = 2f;            // 队友复活距离
    [SerializeField] private float reviveHoldTime = 1.5f;        // 长按复活时间

    [Header("复活惩罚")]
    [SerializeField] private bool loseCollectibles = false;      // 死亡失去收集品
    [SerializeField] private float healthOnRespawn = 0.5f;       // 复活后血量比例 (0-1)
    [SerializeField] private int maxRespawnsPerCheckpoint = -1;  // 每个检查点最大复活次数 (-1无限)

    [Header("双人全灭")]
    [SerializeField] private float bothDeadGameOverDelay = 3f;   // 双人都死亡后的GameOver延迟
    [SerializeField] private bool requireBothAliveForPuzzle = true; // 谜题区域需双人存活

    [Header("特效")]
    [SerializeField] private GameObject deathVFXPrefab;
    [SerializeField] private GameObject respawnVFXPrefab;
    [SerializeField] private GameObject revivePromptPrefab;       // 复活提示UI

    // 运行时
    private Dictionary<int, RespawnData> playerData = new Dictionary<int, RespawnData>();
    private GameObject activeRevivePrompt;
    private Coroutine gameOverCoroutine;

    private class RespawnData
    {
        public PlayerHealth health;
        public PlayerController controller;
        public Vector3 checkpointPosition;
        public int respawnCount;
        public bool isDead;
        public float deathTime;
        public float reviveProgress; // 0-1 复活进度
    }

    public event System.Action<int> OnPlayerRespawned;        // playerIndex
    public event System.Action<int, float> OnReviveProgress;  // playerIndex, progress 0-1
    public event System.Action OnBothPlayersDead;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// 注册玩家到复活系统
    /// </summary>
    public void RegisterPlayer(int playerIndex, PlayerHealth health, PlayerController controller, Vector3 spawnPoint)
    {
        var data = new RespawnData
        {
            health = health,
            controller = controller,
            checkpointPosition = spawnPoint,
            respawnCount = 0,
            isDead = false,
            reviveProgress = 0f
        };

        playerData[playerIndex] = data;

        // 订阅死亡事件
        health.OnDeath += () => HandlePlayerDeath(playerIndex);
    }

    /// <summary>
    /// 更新检查点位置
    /// </summary>
    public void UpdateCheckpoint(int playerIndex, Vector3 position)
    {
        if (playerData.ContainsKey(playerIndex))
        {
            playerData[playerIndex].checkpointPosition = position;
            playerData[playerIndex].respawnCount = 0;

            // 发布事件
            EventBus.Publish(new CheckpointReachedEvent
            {
                position = position,
                checkpointIndex = playerIndex
            });
        }
    }

    /// <summary>
    /// 处理玩家死亡
    /// </summary>
    private void HandlePlayerDeath(int playerIndex)
    {
        if (!playerData.ContainsKey(playerIndex)) return;

        var data = playerData[playerIndex];
        data.isDead = true;
        data.deathTime = Time.time;
        data.reviveProgress = 0f;

        // 死亡特效
        if (deathVFXPrefab != null && data.controller != null)
        {
            var vfx = Instantiate(deathVFXPrefab, data.controller.transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("player_death");

        // 相机特效
        if (CameraEffects.Instance != null)
            CameraEffects.Instance.DeathEffect();

        // 发布死亡事件
        EventBus.Publish(new PlayerDeathEvent
        {
            playerIndex = playerIndex,
            deathPosition = data.controller != null ? data.controller.transform.position : Vector3.zero
        });

        // 记录死亡统计
        if (DifficultyManager.Instance != null)
            DifficultyManager.Instance.RecordPlayerDeath();

        if (GameStats.Instance != null)
            GameStats.Instance.RecordDeath();

        // 检查是否双人都死亡
        if (AreBothPlayersDead())
        {
            OnBothPlayersDead?.Invoke();
            HandleBothDead();
            return;
        }

        // 根据模式处理复活
        switch (respawnMode)
        {
            case RespawnMode.Checkpoint:
                StartCoroutine(RespawnAtCheckpoint(playerIndex));
                break;

            case RespawnMode.Partner:
                StartCoroutine(RespawnNearPartner(playerIndex));
                break;

            case RespawnMode.TimedAuto:
                StartCoroutine(TimedAutoRespawn(playerIndex));
                break;

            case RespawnMode.PartnerRevive:
                ShowRevivePrompt(playerIndex);
                StartCoroutine(WaitForReviveOrTimeout(playerIndex));
                break;
        }
    }

    /// <summary>
    /// 检查点复活
    /// </summary>
    private IEnumerator RespawnAtCheckpoint(int playerIndex)
    {
        yield return new WaitForSeconds(respawnDelay);

        if (!playerData.ContainsKey(playerIndex)) yield break;
        var data = playerData[playerIndex];

        if (maxRespawnsPerCheckpoint >= 0 && data.respawnCount >= maxRespawnsPerCheckpoint)
        {
            // 超过复活次数限制 → GameOver
            GameFlowManager.Instance?.TriggerGameOver();
            yield break;
        }

        ExecuteRespawn(playerIndex, data.checkpointPosition);
    }

    /// <summary>
    /// 在队友身边复活
    /// </summary>
    private IEnumerator RespawnNearPartner(int playerIndex)
    {
        yield return new WaitForSeconds(respawnDelay);

        int partnerIndex = playerIndex == 0 ? 1 : 0;

        if (playerData.ContainsKey(partnerIndex) && !playerData[partnerIndex].isDead)
        {
            Vector3 partnerPos = playerData[partnerIndex].controller.transform.position;
            Vector3 offset = Vector3.right * (playerIndex == 0 ? -1.5f : 1.5f);
            ExecuteRespawn(playerIndex, partnerPos + offset);
        }
        else
        {
            // 队友也死了，回检查点
            ExecuteRespawn(playerIndex, playerData[playerIndex].checkpointPosition);
        }
    }

    /// <summary>
    /// 倒计时自动复活
    /// </summary>
    private IEnumerator TimedAutoRespawn(int playerIndex)
    {
        float timer = autoRespawnTime;

        while (timer > 0)
        {
            timer -= Time.deltaTime;
            float progress = 1f - (timer / autoRespawnTime);
            OnReviveProgress?.Invoke(playerIndex, progress);
            yield return null;
        }

        // 在队友旁或检查点复活
        int partnerIndex = playerIndex == 0 ? 1 : 0;
        if (playerData.ContainsKey(partnerIndex) && !playerData[partnerIndex].isDead)
        {
            Vector3 partnerPos = playerData[partnerIndex].controller.transform.position;
            Vector3 offset = Vector3.right * 1.5f;
            ExecuteRespawn(playerIndex, partnerPos + offset);
        }
        else
        {
            ExecuteRespawn(playerIndex, playerData[playerIndex].checkpointPosition);
        }
    }

    /// <summary>
    /// 等待队友复活或超时
    /// </summary>
    private IEnumerator WaitForReviveOrTimeout(int playerIndex)
    {
        float timer = autoRespawnTime;
        int partnerIndex = playerIndex == 0 ? 1 : 0;
        var data = playerData[playerIndex];

        while (timer > 0 && data.isDead)
        {
            timer -= Time.deltaTime;

            // 检查队友是否在复活范围内并按住复活键
            if (playerData.ContainsKey(partnerIndex) && !playerData[partnerIndex].isDead)
            {
                var partnerCtrl = playerData[partnerIndex].controller;
                if (partnerCtrl != null && data.controller != null)
                {
                    float dist = Vector2.Distance(
                        partnerCtrl.transform.position,
                        data.controller.transform.position
                    );

                    if (dist <= reviveRadius && IsReviveButtonHeld(partnerIndex))
                    {
                        // 增加复活进度
                        data.reviveProgress += Time.deltaTime / reviveHoldTime;
                        OnReviveProgress?.Invoke(playerIndex, data.reviveProgress);

                        if (data.reviveProgress >= 1f)
                        {
                            // 复活成功
                            Vector3 respawnPos = data.controller.transform.position;
                            HideRevivePrompt();
                            ExecuteRespawn(playerIndex, respawnPos);
                            yield break;
                        }
                    }
                    else
                    {
                        // 未在范围内或未按住键，缓慢减少进度
                        data.reviveProgress = Mathf.Max(0, data.reviveProgress - Time.deltaTime * 0.5f);
                        OnReviveProgress?.Invoke(playerIndex, data.reviveProgress);
                    }
                }
            }

            yield return null;
        }

        // 超时 → 回检查点
        if (data.isDead)
        {
            HideRevivePrompt();
            ExecuteRespawn(playerIndex, data.checkpointPosition);
        }
    }

    /// <summary>
    /// 执行复活
    /// </summary>
    private void ExecuteRespawn(int playerIndex, Vector3 position)
    {
        if (!playerData.ContainsKey(playerIndex)) return;

        var data = playerData[playerIndex];

        // 复活特效
        if (respawnVFXPrefab != null)
        {
            var vfx = Instantiate(respawnVFXPrefab, position, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        // 重置玩家
        if (data.controller != null)
        {
            data.controller.transform.position = position;
            data.controller.gameObject.SetActive(true);

            var rb = data.controller.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }

        if (data.health != null)
        {
            float maxHP = data.health.MaxHealth;
            data.health.Respawn(maxHP * healthOnRespawn, invincibilityDuration);
        }

        data.isDead = false;
        data.respawnCount++;
        data.reviveProgress = 0f;

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("player_respawn");

        // 相机特效
        if (CameraEffects.Instance != null)
            CameraEffects.Instance.HealFlash();

        // 发布复活事件
        EventBus.Publish(new PlayerRespawnEvent
        {
            playerIndex = playerIndex,
            spawnPosition = position
        });

        OnPlayerRespawned?.Invoke(playerIndex);
    }

    /// <summary>
    /// 处理双人全灭
    /// </summary>
    private void HandleBothDead()
    {
        if (gameOverCoroutine != null) StopCoroutine(gameOverCoroutine);
        gameOverCoroutine = StartCoroutine(BothDeadSequence());
    }

    private IEnumerator BothDeadSequence()
    {
        yield return new WaitForSeconds(bothDeadGameOverDelay);

        // 检查是否仍然双人死亡（可能有人在此期间复活）
        if (AreBothPlayersDead())
        {
            GameFlowManager.Instance?.TriggerGameOver();
        }
    }

    /// <summary>
    /// 检查是否两个玩家都死亡
    /// </summary>
    public bool AreBothPlayersDead()
    {
        bool p0Dead = !playerData.ContainsKey(0) || playerData[0].isDead;
        bool p1Dead = !playerData.ContainsKey(1) || playerData[1].isDead;
        return p0Dead && p1Dead;
    }

    /// <summary>
    /// 检查某玩家是否死亡
    /// </summary>
    public bool IsPlayerDead(int playerIndex)
    {
        return playerData.ContainsKey(playerIndex) && playerData[playerIndex].isDead;
    }

    /// <summary>
    /// 获取复活进度 (0-1)
    /// </summary>
    public float GetReviveProgress(int playerIndex)
    {
        if (playerData.ContainsKey(playerIndex))
            return playerData[playerIndex].reviveProgress;
        return 0f;
    }

    private bool IsReviveButtonHeld(int partnerIndex)
    {
        // 使用交互键或技能键作为复活键
        if (InputManager.Instance != null)
            return InputManager.Instance.GetInteractHeld(partnerIndex);

        return Input.GetKey(partnerIndex == 0 ? KeyCode.E : KeyCode.RightShift);
    }

    private void ShowRevivePrompt(int deadPlayerIndex)
    {
        if (revivePromptPrefab == null || !playerData.ContainsKey(deadPlayerIndex)) return;

        var data = playerData[deadPlayerIndex];
        if (data.controller == null) return;

        activeRevivePrompt = Instantiate(revivePromptPrefab,
            data.controller.transform.position + Vector3.up * 1.5f,
            Quaternion.identity);
    }

    private void HideRevivePrompt()
    {
        if (activeRevivePrompt != null)
        {
            Destroy(activeRevivePrompt);
            activeRevivePrompt = null;
        }
    }

    /// <summary>
    /// 强制复活所有死亡玩家（用于检查点激活等）
    /// </summary>
    public void ReviveAllPlayers()
    {
        foreach (var kvp in playerData)
        {
            if (kvp.Value.isDead)
            {
                ExecuteRespawn(kvp.Key, kvp.Value.checkpointPosition);
            }
        }
    }

    /// <summary>
    /// 重置复活计数（新关卡/新检查点时调用）
    /// </summary>
    public void ResetRespawnCounts()
    {
        foreach (var kvp in playerData)
        {
            kvp.Value.respawnCount = 0;
        }
    }

    void OnDestroy()
    {
        HideRevivePrompt();
    }
}
