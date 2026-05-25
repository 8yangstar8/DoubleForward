using UnityEngine;

/// <summary>
/// 玩家动画控制器 - 驱动Animator参数和触发器
/// 监听PlayerController事件，自动同步所有运动状态
/// 支持：跑步、跳跃、下落、冲刺、墙滑、墙跳、攀爬、受伤、死亡、重生
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    private Animator animator;
    private PlayerController controller;
    private PlayerHealth health;

    // ===== Bool参数 =====
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int IsDashing = Animator.StringToHash("IsDashing");
    private static readonly int IsWallSliding = Animator.StringToHash("IsWallSliding");
    private static readonly int IsOnLadder = Animator.StringToHash("IsOnLadder");
    private static readonly int IsHurt = Animator.StringToHash("IsHurt");

    // ===== Float参数 =====
    private static readonly int VelocityY = Animator.StringToHash("VelocityY");
    private static readonly int VelocityX = Animator.StringToHash("VelocityX");
    private static readonly int HealthPercent = Animator.StringToHash("HealthPercent");

    // ===== Trigger参数 =====
    private static readonly int TriggerJump = Animator.StringToHash("Jump");
    private static readonly int TriggerSkill = Animator.StringToHash("Skill");
    private static readonly int TriggerDeath = Animator.StringToHash("Death");
    private static readonly int TriggerRespawn = Animator.StringToHash("Respawn");
    private static readonly int TriggerAttack = Animator.StringToHash("Attack");
    private static readonly int TriggerHurt = Animator.StringToHash("Hurt");
    private static readonly int TriggerLand = Animator.StringToHash("Land");
    private static readonly int TriggerWallJump = Animator.StringToHash("WallJump");
    private static readonly int TriggerHeal = Animator.StringToHash("Heal");

    // ===== Integer参数 =====
    private static readonly int AttackCombo = Animator.StringToHash("AttackCombo");

    // 状态追踪
    private bool wasGrounded;
    private bool wasWallSliding;
    private float hurtTimer;
    private const float HURT_DURATION = 0.3f;

    void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<PlayerController>();
        health = GetComponent<PlayerHealth>();
    }

    void OnEnable()
    {
        controller.OnJumped += PlayJump;
        controller.OnDashed += PlayDash;
        controller.OnLanded += PlayLand;

        if (controller.OnWallJumped != null)
            controller.OnWallJumped += PlayWallJump;

        // 订阅事件
        EventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Subscribe<PlayerHealEvent>(OnPlayerHealed);
    }

    void OnDisable()
    {
        controller.OnJumped -= PlayJump;
        controller.OnDashed -= PlayDash;
        controller.OnLanded -= PlayLand;
        controller.OnWallJumped -= PlayWallJump;

        EventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Unsubscribe<PlayerHealEvent>(OnPlayerHealed);
    }

    void Update()
    {
        // 基础运动参数
        animator.SetBool(IsRunning, Mathf.Abs(controller.Velocity.x) > 0.1f);
        animator.SetBool(IsGrounded, controller.IsGrounded);
        animator.SetFloat(VelocityY, controller.Velocity.y);
        animator.SetFloat(VelocityX, Mathf.Abs(controller.Velocity.x));
        animator.SetBool(IsDashing, controller.IsDashing);

        // 墙滑
        animator.SetBool(IsWallSliding, controller.IsWallSliding);

        // 攀爬
        animator.SetBool(IsOnLadder, controller.IsOnLadder);

        // 生命值
        if (health != null)
            animator.SetFloat(HealthPercent, health.HealthPercent);

        // 受伤状态倒计时
        if (hurtTimer > 0)
        {
            hurtTimer -= Time.deltaTime;
            if (hurtTimer <= 0)
                animator.SetBool(IsHurt, false);
        }

        // 检测落地（备用，防止事件丢失）
        if (!wasGrounded && controller.IsGrounded)
        {
            // OnLanded事件应该已经触发了
        }

        wasGrounded = controller.IsGrounded;
        wasWallSliding = controller.IsWallSliding;
    }

    // ==================== 触发器方法 ====================

    private void PlayJump() => animator.SetTrigger(TriggerJump);
    private void PlayDash() => animator.SetTrigger(TriggerSkill);
    public void PlayDeath() => animator.SetTrigger(TriggerDeath);
    public void PlayRespawn() => animator.SetTrigger(TriggerRespawn);

    private void PlayLand()
    {
        animator.SetTrigger(TriggerLand);
    }

    private void PlayWallJump()
    {
        animator.SetTrigger(TriggerWallJump);
    }

    public void PlayHurt()
    {
        animator.SetTrigger(TriggerHurt);
        animator.SetBool(IsHurt, true);
        hurtTimer = HURT_DURATION;
    }

    public void PlayHeal()
    {
        animator.SetTrigger(TriggerHeal);
    }

    public void PlayAttack(int comboStep = 0)
    {
        animator.SetInteger(AttackCombo, comboStep);
        animator.SetTrigger(TriggerAttack);
    }

    // ==================== 事件处理 ====================

    private void OnPlayerDamaged(PlayerDamagedEvent e)
    {
        if (controller.PlayerIndex == e.playerIndex)
            PlayHurt();
    }

    private void OnPlayerHealed(PlayerHealEvent e)
    {
        if (controller.PlayerIndex == e.playerIndex)
            PlayHeal();
    }

    // ==================== 动画事件回调 ====================
    // 这些方法由AnimationEventRelay或直接由动画事件调用

    /// <summary>脚步声（动画事件调用）</summary>
    public void OnFootstep()
    {
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayFootstep();

        if (VFXManager.Instance != null && controller.IsGrounded)
            VFXManager.Instance.Play(VFXManager.Effects.DustRun, transform.position);
    }

    /// <summary>攻击命中帧（动画事件调用）</summary>
    public void OnAttackHitFrame()
    {
        var combat = GetComponent<PlayerCombat>();
        if (combat != null)
            combat.ExecuteAttackHit();
    }

    /// <summary>技能释放帧（动画事件调用）</summary>
    public void OnSkillCastFrame()
    {
        var ability = GetComponent<PlayerAbilityBase>();
        if (ability != null)
            ability.OnCastFrameTriggered();
    }
}
