using UnityEngine;
using System.Collections;

/// <summary>
/// 检查点 - 玩家经过时激活，保存进度和复活位置
/// 支持双人检测（可选需要两人都到达才激活）
/// 激活时播放动画、粒子和音效，并通知RespawnSystem和AutoSaveSystem
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class Checkpoint : MonoBehaviour
{
    [Header("状态")]
    [SerializeField] private bool isActivated;
    [SerializeField] private bool requireBothPlayers = false;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer flagRenderer;
    [SerializeField] private SpriteRenderer glowRenderer;
    [SerializeField] private Color activeColor = new Color(0.3f, 1f, 0.4f);
    [SerializeField] private Color inactiveColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color glowColor = new Color(0.3f, 1f, 0.4f, 0.4f);

    [Header("动画")]
    [SerializeField] private Animator checkpointAnimator;
    [SerializeField] private float flagRaiseSpeed = 3f;
    [SerializeField] private float glowPulseSpeed = 2f;
    [SerializeField] private float glowPulseMin = 0.2f;
    [SerializeField] private float glowPulseMax = 0.6f;

    [Header("粒子")]
    [SerializeField] private ParticleSystem activationParticles;
    [SerializeField] private ParticleSystem idleParticles;

    [Header("音效")]
    [SerializeField] private string activateSound = "checkpoint_activate";

    [Header("自动保存")]
    [SerializeField] private bool triggerAutoSave = true;

    public bool IsActivated => isActivated;
    public int CheckpointIndex { get; set; } = -1;

    /// <summary>设置检查点顺序（由LevelBuilder调用）</summary>
    public void SetOrder(int order) => CheckpointIndex = order;

    public event System.Action<Checkpoint> OnCheckpointActivated;

    // 双人检测
    private bool player0Arrived;
    private bool player1Arrived;

    void Start()
    {
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        UpdateVisual();

        // 空闲粒子
        if (idleParticles != null)
        {
            if (isActivated)
                idleParticles.Play();
            else
                idleParticles.Stop();
        }
    }

    void Update()
    {
        if (!isActivated) return;

        // 发光脉冲
        if (glowRenderer != null)
        {
            float pulse = Mathf.Lerp(glowPulseMin, glowPulseMax,
                (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f);
            Color c = glowColor;
            c.a = pulse;
            glowRenderer.color = c;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isActivated) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (requireBothPlayers)
        {
            // 标记到达的玩家
            if (player.PlayerIndex == 0) player0Arrived = true;
            if (player.PlayerIndex == 1) player1Arrived = true;

            // 两人都到达才激活
            if (!player0Arrived || !player1Arrived) return;
        }

        Activate(other.gameObject);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (isActivated) return;

        // 双人模式下，离开的玩家取消标记
        if (requireBothPlayers)
        {
            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                if (player.PlayerIndex == 0) player0Arrived = false;
                if (player.PlayerIndex == 1) player1Arrived = false;
            }
        }
    }

    /// <summary>
    /// 激活检查点
    /// </summary>
    private void Activate(GameObject triggerPlayer)
    {
        isActivated = true;

        // 更新所有在场玩家的检查点
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            var health = p.GetComponent<PlayerHealth>();
            if (health != null)
                health.SetCheckpoint(transform.position);

            // 更新RespawnSystem
            if (RespawnSystem.Instance != null)
                RespawnSystem.Instance.UpdateCheckpoint(p.PlayerIndex, transform.position);
        }

        // 视觉反馈
        UpdateVisual();
        StartCoroutine(ActivationSequence());

        // 激活粒子
        if (activationParticles != null)
            activationParticles.Play();
        if (idleParticles != null)
            idleParticles.Play();

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(activateSound);

        // 触觉反馈
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Light();

        // 自动存档
        if (triggerAutoSave && AutoSaveSystem.Instance != null)
            AutoSaveSystem.Instance.TriggerAutoSave("checkpoint");

        // 发布EventBus事件
        EventBus.Publish(new CheckpointReachedEvent
        {
            position = transform.position,
            checkpointIndex = CheckpointIndex
        });

        // 本地事件
        OnCheckpointActivated?.Invoke(this);

        Debug.Log($"[Checkpoint] Activated at {transform.position}");
    }

    /// <summary>
    /// 激活动画序列
    /// </summary>
    private IEnumerator ActivationSequence()
    {
        // 旗帜升起动画
        if (checkpointAnimator != null)
        {
            checkpointAnimator.SetTrigger("Activate");
        }
        else if (flagRenderer != null)
        {
            // 程序化旗帜动画：缩放弹跳
            Vector3 originalScale = flagRenderer.transform.localScale;
            Vector3 targetScale = originalScale * 1.3f;

            float t = 0;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = t / 0.3f;
                float bounce = 1f + 0.3f * Mathf.Sin(progress * Mathf.PI);
                flagRenderer.transform.localScale = originalScale * bounce;
                yield return null;
            }
            flagRenderer.transform.localScale = originalScale;
        }

        // 相机轻微缩放提示
        if (CameraEffects.Instance != null)
            CameraEffects.Instance.HealFlash();

        yield return null;
    }

    private void UpdateVisual()
    {
        if (flagRenderer != null)
            flagRenderer.color = isActivated ? activeColor : inactiveColor;

        if (glowRenderer != null)
        {
            glowRenderer.enabled = isActivated;
            if (isActivated)
                glowRenderer.color = glowColor;
        }
    }

    /// <summary>
    /// 重置检查点状态
    /// </summary>
    public void ResetCheckpoint()
    {
        isActivated = false;
        player0Arrived = false;
        player1Arrived = false;
        UpdateVisual();

        if (idleParticles != null)
            idleParticles.Stop();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isActivated ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // 标记需要双人
        if (requireBothPlayers)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.7f);
        }
    }
}
