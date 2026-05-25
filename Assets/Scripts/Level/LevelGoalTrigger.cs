using UnityEngine;

/// <summary>
/// 关卡终点触发器 - 玩家到达终点时通知完成系统
/// 支持单人/双人到达要求
/// 同时通知LevelCompletionChecker和LevelManager
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LevelGoalTrigger : MonoBehaviour
{
    [Header("到达条件")]
    [SerializeField] private bool requireBothPlayers = true;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer goalRenderer;
    [SerializeField] private ParticleSystem goalParticles;
    [SerializeField] private Color activeColor = new Color(1f, 0.9f, 0.3f);
    [SerializeField] private float pulseSpeed = 2f;

    [Header("音效")]
    [SerializeField] private string arriveSound = "goal_arrive";
    [SerializeField] private string completeSound = "level_complete";

    private bool player1Arrived;
    private bool player2Arrived;
    private bool isCompleted;

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;

        if (goalParticles != null)
            goalParticles.Play();
    }

    void Update()
    {
        if (isCompleted) return;

        // 目标点发光脉冲
        if (goalRenderer != null)
        {
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            Color c = activeColor;
            c.a = Mathf.Lerp(0.5f, 1f, pulse);
            goalRenderer.color = c;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isCompleted) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (player.PlayerIndex == 0)
            player1Arrived = true;
        else
            player2Arrived = true;

        // 音效反馈
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(arriveSound);

        // 通知LevelCompletionChecker
        if (LevelCompletionChecker.Instance != null)
            LevelCompletionChecker.Instance.OnPlayerReachedGoal(player.PlayerIndex);

        // 直接完成判定
        if (!requireBothPlayers || (player1Arrived && player2Arrived))
        {
            CompleteGoal();
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (isCompleted) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (player.PlayerIndex == 0)
            player1Arrived = false;
        else
            player2Arrived = false;
    }

    private void CompleteGoal()
    {
        if (isCompleted) return;
        isCompleted = true;

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(completeSound);

        // 触觉
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Success();

        // VFX
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.LevelComplete, transform.position);

        // 粒子增强
        if (goalParticles != null)
        {
            var emission = goalParticles.emission;
            emission.rateOverTime = emission.rateOverTime.constant * 5f;
        }

        // 通知LevelManager
        LevelManager.Instance?.CompleteLevel();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.5f);
        Gizmos.DrawSphere(transform.position, 0.5f);

        if (requireBothPlayers)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.8f);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f,
            requireBothPlayers ? "GOAL (2P)" : "GOAL");
#endif
    }
}
