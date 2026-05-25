using UnityEngine;

/// <summary>
/// 玩家移动微调 - 提供手感优化的辅助机制
/// 包含：土狼时间(Coyote Time)、输入缓冲(Jump Buffer)、
/// 跳跃高度控制(Variable Jump)、着地挤压(Land Squash)
/// 挂载在玩家物体上，自动与PlayerController协作
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerMovementTuning : MonoBehaviour
{
    [Header("土狼时间 (Coyote Time)")]
    [Tooltip("离开平台后仍可跳跃的宽限时间")]
    [SerializeField] private float coyoteTime = 0.12f;

    [Header("跳跃缓冲 (Jump Buffer)")]
    [Tooltip("落地前提前按跳跃键的缓冲时间")]
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("可变跳跃高度 (Variable Jump)")]
    [Tooltip("松开跳跃键时的重力倍率，实现短按低跳")]
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 2f;
    [SerializeField] private float maxFallSpeed = 20f;

    [Header("着地反馈 (Land Effects)")]
    [SerializeField] private float squashAmount = 0.15f;
    [SerializeField] private float squashDuration = 0.1f;
    [SerializeField] private float stretchAmount = 0.1f;

    [Header("加速曲线")]
    [SerializeField] private float accelerationTime = 0.08f;
    [SerializeField] private float decelerationTime = 0.05f;

    // 运行时
    private PlayerController controller;
    private Rigidbody2D rb;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool wasGrounded;
    private bool jumpHeld;
    private Vector3 originalScale;
    private float squashTimer;

    // 公共属性供PlayerController查询
    public bool CanCoyoteJump => coyoteTimer > 0;
    public bool HasBufferedJump => jumpBufferTimer > 0;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;
    }

    void Update()
    {
        UpdateCoyoteTime();
        UpdateJumpBuffer();
        UpdateVariableJump();
        UpdateLandSquash();
    }

    // ==================== 土狼时间 ====================

    private void UpdateCoyoteTime()
    {
        if (controller.IsGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }

        // 检测离开地面瞬间（非跳跃离开）
        if (wasGrounded && !controller.IsGrounded)
        {
            // 如果不是因为跳跃离开地面，给予土狼时间
            if (rb.linearVelocity.y <= 0.1f)
                coyoteTimer = coyoteTime;
        }

        wasGrounded = controller.IsGrounded;
    }

    /// <summary>
    /// 消耗土狼时间（跳跃成功后调用）
    /// </summary>
    public void ConsumeCoyoteTime()
    {
        coyoteTimer = 0;
    }

    // ==================== 跳跃缓冲 ====================

    private void UpdateJumpBuffer()
    {
        // 检测跳跃按键
        if (InputManager.Instance != null &&
            InputManager.Instance.GetJumpPressed(controller.PlayerIndex))
        {
            jumpBufferTimer = jumpBufferTime;
        }

        if (jumpBufferTimer > 0)
            jumpBufferTimer -= Time.deltaTime;

        // 如果有缓冲跳跃且刚着地，自动执行跳跃
        if (HasBufferedJump && controller.IsGrounded)
        {
            jumpBufferTimer = 0;
            controller.TryJump();
        }
    }

    /// <summary>
    /// 消耗跳跃缓冲
    /// </summary>
    public void ConsumeJumpBuffer()
    {
        jumpBufferTimer = 0;
    }

    // ==================== 可变跳跃高度 ====================

    private void UpdateVariableJump()
    {
        if (rb == null) return;

        // 检测跳跃键是否持续按住
        if (InputManager.Instance != null)
        {
            jumpHeld = InputManager.Instance.GetSkill1Held(controller.PlayerIndex) ||
                       Input.GetKey(controller.PlayerIndex == 0 ? KeyCode.Space : KeyCode.UpArrow);
        }

        // 下落时增加重力
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        }
        // 上升时松开跳跃键 → 快速降落（短按=低跳）
        else if (rb.linearVelocity.y > 0 && !jumpHeld)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
        }

        // 限制最大下落速度
        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
        }
    }

    // ==================== 着地挤压 ====================

    private void UpdateLandSquash()
    {
        if (squashTimer > 0)
        {
            squashTimer -= Time.deltaTime;
            float t = 1f - (squashTimer / squashDuration);

            // 弹性恢复
            float scaleY = Mathf.Lerp(1f - squashAmount, 1f, EaseOutBounce(t));
            float scaleX = Mathf.Lerp(1f + squashAmount * 0.5f, 1f, EaseOutBounce(t));

            float facingSign = controller.IsFacingRight ? 1f : -1f;
            transform.localScale = new Vector3(
                Mathf.Abs(originalScale.x) * scaleX * facingSign,
                originalScale.y * scaleY,
                originalScale.z);
        }

        // 检测着地瞬间
        if (!wasGrounded && controller.IsGrounded)
        {
            TriggerLandSquash();
        }
    }

    /// <summary>
    /// 触发着地挤压动画
    /// </summary>
    public void TriggerLandSquash()
    {
        squashTimer = squashDuration;

        // 着地粒子
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.DustLand, transform.position);
    }

    /// <summary>
    /// 触发跳跃拉伸
    /// </summary>
    public void TriggerJumpStretch()
    {
        float facingSign = controller.IsFacingRight ? 1f : -1f;
        transform.localScale = new Vector3(
            Mathf.Abs(originalScale.x) * (1f - stretchAmount * 0.3f) * facingSign,
            originalScale.y * (1f + stretchAmount),
            originalScale.z);

        // 快速恢复
        squashTimer = squashDuration * 0.5f;
    }

    // ==================== 缓动函数 ====================

    private static float EaseOutBounce(float t)
    {
        if (t < 1f / 2.75f)
            return 7.5625f * t * t;
        else if (t < 2f / 2.75f)
        {
            t -= 1.5f / 2.75f;
            return 7.5625f * t * t + 0.75f;
        }
        else if (t < 2.5f / 2.75f)
        {
            t -= 2.25f / 2.75f;
            return 7.5625f * t * t + 0.9375f;
        }
        else
        {
            t -= 2.625f / 2.75f;
            return 7.5625f * t * t + 0.984375f;
        }
    }
}
