using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Player Identity")]
    [SerializeField] private int playerIndex = 0;
    [SerializeField] private PlayerType playerType = PlayerType.Lux;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float doubleJumpForce = 8f;
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.2f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Jump Feel")]
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Passive Buffs")]
    [SerializeField] private float passiveSpeedBuff = 0.2f;

    public enum PlayerType { Lux, Nox }

    public int PlayerIndex => playerIndex;
    public PlayerType Type => playerType;
    public bool IsGrounded { get; private set; }
    public bool IsFacingRight { get; private set; } = true;
    public bool IsDashing { get; private set; }
    public Vector2 Velocity => rb.linearVelocity;

    private Rigidbody2D rb;
    private bool canDoubleJump;
    private bool hasDoubleJumped;
    private float dashTimer;
    private float coyoteTimer;
    private float jumpBufferTimer;

    public event System.Action OnJumped;
    public event System.Action OnLanded;
    public event System.Action OnDashed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 2.5f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        canDoubleJump = playerType == PlayerType.Lux;
    }

    void Update()
    {
        if (isFrozen) return;

        bool wasGrounded = IsGrounded;
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (IsGrounded && !wasGrounded)
        {
            hasDoubleJumped = false;
            coyoteTimer = coyoteTime;
            OnLanded?.Invoke();

            // 成就：重置墙跳连击
            if (AchievementTracker.Instance != null)
                AchievementTracker.Instance.ResetWallJumpChain();
        }

        // Coyote time: 离开地面后仍允许短暂跳跃
        if (wasGrounded && !IsGrounded && rb.linearVelocity.y <= 0)
            coyoteTimer = coyoteTime;
        else if (!IsGrounded)
            coyoteTimer -= Time.deltaTime;

        // 跳跃缓冲计时
        if (jumpBufferTimer > 0)
            jumpBufferTimer -= Time.deltaTime;

        if (IsDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
                IsDashing = false;
        }

        // 梯子移动
        if (IsOnLadder)
        {
            HandleLadderMovement();
            return;
        }

        UpdateWallSlide();
        HandleInput();

        // 跳跃缓冲：如果之前按了跳跃且现在着地了
        if (jumpBufferTimer > 0 && IsGrounded)
        {
            jumpBufferTimer = 0;
            TryJump();
        }
    }

    private void HandleInput()
    {
        if (InputManager.Instance == null) return;

        Vector2 moveInput = InputManager.Instance.GetMoveInput(playerIndex);
        SetMoveInput(moveInput);

        if (InputManager.Instance.GetJumpPressed(playerIndex))
            TryJump();

        if (InputManager.Instance.GetAttackPressed(playerIndex))
            TryAttack();

        if (InputManager.Instance.GetDashPressed(playerIndex))
            TryDash();

        if (InputManager.Instance.GetSkill1Pressed(playerIndex))
            TrySkill1();

        if (InputManager.Instance.GetSkill2Pressed(playerIndex))
            TrySkill2();
    }

    private void TryAttack()
    {
        var combat = GetComponent<PlayerCombat>();
        if (combat == null) return;

        if (IsGrounded)
        {
            // 地面攻击：Lux远程、Nox近战
            if (playerType == PlayerType.Lux)
                combat.RangedAttack();
            else
                combat.MeleeAttack();
        }
        else
        {
            // 空中攻击：下砸
            combat.AirDownAttack();
        }
    }

    public void SetMoveInput(Vector2 input)
    {
        if (IsDashing) return;

        // 状态效果检查（定身）
        var statusEffect = GetComponent<PlayerStatusEffect>();
        if (statusEffect != null && statusEffect.IsRooted)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }

        float speed = moveSpeed;
        if (playerType == PlayerType.Lux && IsInLightZone())
            speed *= (1f + passiveSpeedBuff);
        else if (playerType == PlayerType.Nox && IsInShadowZone())
            speed *= (1f + passiveSpeedBuff);

        // 应用状态效果速度修正
        if (statusEffect != null)
            speed *= statusEffect.SpeedMultiplier;

        rb.linearVelocity = new Vector2(input.x * speed, rb.linearVelocity.y);

        if (input.x > 0.01f)
        {
            IsFacingRight = true;
            transform.localScale = new Vector3(1, 1, 1);
        }
        else if (input.x < -0.01f)
        {
            IsFacingRight = false;
            transform.localScale = new Vector3(-1, 1, 1);
        }
    }

    public void TryJump()
    {
        // 墙跳优先
        if (IsWallSliding)
        {
            TryWallJump();
            return;
        }

        bool canCoyoteJump = coyoteTimer > 0;

        if (IsGrounded || canCoyoteJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            hasDoubleJumped = false;
            coyoteTimer = 0; // 消耗coyote time
            OnJumped?.Invoke();
        }
        else if (canDoubleJump && !hasDoubleJumped)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, doubleJumpForce);
            hasDoubleJumped = true;
            OnJumped?.Invoke();
        }
        else
        {
            // 不能跳：缓冲跳跃请求
            jumpBufferTimer = jumpBufferTime;
        }
    }

    public void TryDash()
    {
        if (IsDashing) return;
        if (playerType != PlayerType.Nox) return;

        IsDashing = true;
        dashTimer = dashDuration;
        float dir = IsFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * dashSpeed, 0f);
        OnDashed?.Invoke();
    }

    private void TrySkill1()
    {
        var ability = GetComponent<PlayerAbilityBase>();
        if (ability != null)
            ability.TryActivate();
    }

    private void TrySkill2()
    {
        if (playerType == PlayerType.Nox)
            TryDash();
    }

    private bool IsInLightZone()
    {
        var colliders = Physics2D.OverlapCircleAll(transform.position, 0.5f);
        foreach (var col in colliders)
            if (col.CompareTag("LightZone")) return true;
        return false;
    }

    private bool IsInShadowZone()
    {
        var colliders = Physics2D.OverlapCircleAll(transform.position, 0.5f);
        foreach (var col in colliders)
            if (col.CompareTag("ShadowZone")) return true;
        return false;
    }

    // ============ 墙壁滑行与跳跃 ============

    [Header("Wall Slide")]
    [SerializeField] private Transform wallCheckPoint;
    [SerializeField] private float wallCheckDistance = 0.3f;
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] private float wallJumpForceX = 8f;
    [SerializeField] private float wallJumpForceY = 10f;
    [SerializeField] private float wallJumpLockTime = 0.15f;

    public bool IsWallSliding { get; private set; }
    public event System.Action OnWallJumped;

    private bool isTouchingWall;
    private float wallJumpLockTimer;
    private int wallJumpDir;

    private void UpdateWallSlide()
    {
        if (wallCheckPoint == null) return;

        float dir = IsFacingRight ? 1f : -1f;
        isTouchingWall = Physics2D.Raycast(wallCheckPoint.position,
            Vector2.right * dir, wallCheckDistance, groundLayer);

        // 墙壁滑行：非地面 + 贴墙 + 有水平输入
        bool wantsToSlide = isTouchingWall && !IsGrounded && rb.linearVelocity.y < 0;

        if (wantsToSlide && InputManager.Instance != null)
        {
            float inputX = InputManager.Instance.GetMoveInput(playerIndex).x;
            bool pushingWall = (IsFacingRight && inputX > 0.1f) || (!IsFacingRight && inputX < -0.1f);
            IsWallSliding = pushingWall;
        }
        else
        {
            IsWallSliding = false;
        }

        if (IsWallSliding)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x,
                Mathf.Max(rb.linearVelocity.y, -wallSlideSpeed));
        }

        // 墙跳锁定计时
        if (wallJumpLockTimer > 0)
        {
            wallJumpLockTimer -= Time.deltaTime;
            rb.linearVelocity = new Vector2(wallJumpDir * wallJumpForceX, rb.linearVelocity.y);
        }
    }

    public void TryWallJump()
    {
        if (!IsWallSliding) return;

        wallJumpDir = IsFacingRight ? -1 : 1;
        rb.linearVelocity = new Vector2(wallJumpDir * wallJumpForceX, wallJumpForceY);
        wallJumpLockTimer = wallJumpLockTime;

        // 翻转面向
        IsFacingRight = !IsFacingRight;
        transform.localScale = new Vector3(IsFacingRight ? 1 : -1, 1, 1);

        IsWallSliding = false;
        OnWallJumped?.Invoke();

        // 成就：墙跳连击追踪
        if (AchievementTracker.Instance != null)
            AchievementTracker.Instance.NotifyWallJump();
    }

    // ============ 梯子系统 ============

    private Ladder currentLadder;
    public bool IsOnLadder { get; private set; }

    public void EnterLadder(Ladder ladder)
    {
        currentLadder = ladder;
        IsOnLadder = true;
        rb.gravityScale = ladder.DisableGravity ? 0 : rb.gravityScale;
        rb.linearVelocity = Vector2.zero;
    }

    public void ExitLadder()
    {
        if (currentLadder != null && currentLadder.DisableGravity)
            rb.gravityScale = 2.5f;

        currentLadder = null;
        IsOnLadder = false;
    }

    private void HandleLadderMovement()
    {
        if (!IsOnLadder || currentLadder == null) return;

        if (InputManager.Instance == null) return;
        Vector2 input = InputManager.Instance.GetMoveInput(playerIndex);

        rb.linearVelocity = new Vector2(
            input.x * moveSpeed * 0.5f,
            input.y * currentLadder.ClimbSpeed
        );
    }

    // ============ 状态控制 ============

    private bool isFrozen;
    public bool IsFrozen => isFrozen;

    /// <summary>
    /// 冻结/解冻玩家（减益效果使用）
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
        if (frozen)
        {
            rb.linearVelocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        else
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    // ============ 重生 ============

    public void Respawn(Vector3 position)
    {
        transform.position = position;
        rb.linearVelocity = Vector2.zero;
        IsDashing = false;
        hasDoubleJumped = false;
        isFrozen = false;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        if (IsOnLadder) ExitLadder();
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
