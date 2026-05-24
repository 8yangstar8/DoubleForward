using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// 网络状态同步增强 - 处理关键游戏状态的网络同步
/// 延迟补偿、状态快照、断线恢复时的状态重建
/// </summary>
public class NetworkStateSync : NetworkBehaviour
{
    [Header("同步设置")]
    [SerializeField] private float syncInterval = 0.1f;       // 100ms同步间隔
    [SerializeField] private float positionThreshold = 0.01f;  // 位置变化阈值
    [SerializeField] private float interpolationSpeed = 12f;   // 插值速度

    [Header("延迟补偿")]
    [SerializeField] private int snapshotBufferSize = 30;
    [SerializeField] private float maxExtrapolationTime = 0.2f;

    // 位置同步
    private NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> netFacingRight = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> netVelocityX = new NetworkVariable<float>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // 状态同步
    private NetworkVariable<int> netHealth = new NetworkVariable<int>(
        100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> netIsDashing = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // 快照缓冲
    private struct StateSnapshot
    {
        public float timestamp;
        public Vector3 position;
        public float velocityX;
        public bool facingRight;
    }

    private Queue<StateSnapshot> snapshotBuffer = new Queue<StateSnapshot>();
    private float syncTimer;
    private Vector3 lastSyncedPosition;
    private PlayerController playerController;
    private PlayerHealth playerHealth;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        playerController = GetComponent<PlayerController>();
        playerHealth = GetComponent<PlayerHealth>();

        if (!IsOwner)
        {
            // 非本地玩家监听位置变化
            netPosition.OnValueChanged += OnPositionChanged;
            netHealth.OnValueChanged += OnHealthChanged;
        }
    }

    void Update()
    {
        if (!IsSpawned) return;

        if (IsOwner)
        {
            // 本地玩家：上传状态
            syncTimer += Time.deltaTime;
            if (syncTimer >= syncInterval)
            {
                syncTimer = 0;
                SyncLocalState();
            }
        }
        else
        {
            // 远程玩家：插值到目标位置
            InterpolateRemotePlayer();
        }
    }

    /// <summary>
    /// 同步本地状态到网络
    /// </summary>
    private void SyncLocalState()
    {
        Vector3 currentPos = transform.position;

        // 仅在位置变化超过阈值时同步
        if (Vector3.Distance(currentPos, lastSyncedPosition) > positionThreshold)
        {
            netPosition.Value = currentPos;
            lastSyncedPosition = currentPos;
        }

        if (playerController != null)
        {
            netFacingRight.Value = playerController.IsFacingRight;
            netVelocityX.Value = playerController.Velocity.x;
            netIsGrounded.Value = playerController.IsGrounded;
            netIsDashing.Value = playerController.IsDashing;
        }

        if (playerHealth != null)
        {
            netHealth.Value = Mathf.RoundToInt(playerHealth.CurrentHealth);
        }
    }

    /// <summary>
    /// 插值远程玩家位置
    /// </summary>
    private void InterpolateRemotePlayer()
    {
        Vector3 targetPos = netPosition.Value;

        // 简单外推：基于速度预测未来位置
        float timeSinceLastUpdate = Time.time - GetLatestSnapshotTime();
        if (timeSinceLastUpdate < maxExtrapolationTime)
        {
            targetPos.x += netVelocityX.Value * timeSinceLastUpdate * 0.5f;
        }

        // 平滑插值
        transform.position = Vector3.Lerp(transform.position, targetPos,
            interpolationSpeed * Time.deltaTime);

        // 朝向同步
        Vector3 scale = transform.localScale;
        float targetScaleX = netFacingRight.Value ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
        scale.x = targetScaleX;
        transform.localScale = scale;
    }

    private void OnPositionChanged(Vector3 prev, Vector3 current)
    {
        // 记录快照
        snapshotBuffer.Enqueue(new StateSnapshot
        {
            timestamp = Time.time,
            position = current,
            velocityX = netVelocityX.Value,
            facingRight = netFacingRight.Value
        });

        // 限制缓冲大小
        while (snapshotBuffer.Count > snapshotBufferSize)
            snapshotBuffer.Dequeue();
    }

    private void OnHealthChanged(int prev, int current)
    {
        // 远程玩家血量变化处理
        if (playerHealth != null && !IsOwner)
        {
            // 触发受伤特效
            if (current < prev && VFXManager.Instance != null)
            {
                VFXManager.Instance.Play(VFXManager.Effects.PlayerHit, transform.position);
            }
        }
    }

    private float GetLatestSnapshotTime()
    {
        if (snapshotBuffer.Count == 0) return Time.time;

        StateSnapshot latest = default;
        foreach (var s in snapshotBuffer)
            latest = s;
        return latest.timestamp;
    }

    // ============ RPC：关键事件同步 ============

    /// <summary>
    /// 同步技能使用
    /// </summary>
    [Rpc(SendTo.NotOwner)]
    public void SyncAbilityUseRpc(string abilityName, Vector3 position)
    {
        // 远程玩家播放技能特效/音效
        switch (abilityName)
        {
            case "light_beam":
                if (VFXManager.Instance != null)
                    VFXManager.Instance.Play(VFXManager.Effects.LightBeam, position);
                if (SoundFeedback.Instance != null)
                    SoundFeedback.Instance.PlayLightBeam();
                break;
            case "shadow_phase":
                if (VFXManager.Instance != null)
                    VFXManager.Instance.Play(VFXManager.Effects.ShadowPhase, position);
                if (SoundFeedback.Instance != null)
                    SoundFeedback.Instance.PlayShadowPhase();
                break;
        }
    }

    /// <summary>
    /// 同步检查点激活
    /// </summary>
    [Rpc(SendTo.Everyone)]
    public void SyncCheckpointRpc(Vector3 checkpointPos)
    {
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.CheckpointActivate, checkpointPos);
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayCheckpoint();
    }

    /// <summary>
    /// 同步玩家死亡
    /// </summary>
    [Rpc(SendTo.NotOwner)]
    public void SyncPlayerDeathRpc(Vector3 deathPos)
    {
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.PlayerDeath, deathPos);
        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeMedium();
    }

    /// <summary>
    /// 同步玩家重生
    /// </summary>
    [Rpc(SendTo.NotOwner)]
    public void SyncPlayerRespawnRpc(Vector3 respawnPos)
    {
        transform.position = respawnPos;
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.PlayerRespawn, respawnPos);
    }

    /// <summary>
    /// 请求全局状态快照（断线重连时）
    /// </summary>
    [Rpc(SendTo.Server)]
    public void RequestFullStateSyncRpc()
    {
        if (!IsServer) return;
        // Server发送完整状态给请求的客户端
        SendFullStateRpc(
            transform.position,
            netHealth.Value,
            netFacingRight.Value,
            netIsGrounded.Value
        );
    }

    [Rpc(SendTo.Owner)]
    private void SendFullStateRpc(Vector3 pos, int health, bool facingRight, bool grounded)
    {
        transform.position = pos;
        // 重建本地状态...
    }
}
