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
        bool wasGrounded = IsGrounded;
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (IsGrounded && !wasGrounded)
        {
            hasDoubleJumped = false;
            OnLanded?.Invoke();
        }

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
    }

    private void HandleInput()
    {
        if (InputManager.Instance == null) return;

        Vector2 moveInput = InputManager.Instance.GetMoveInput(playerIndex);
        SetMoveInput(moveInput);

        if (InputManager.Instance.GetJumpPressed(playerIndex))
            TryJump();

        if (InputManager.Instance.GetSkill1Pressed(playerIndex))
            TrySkill1();

        if (InputManager.Instance.GetSkill2Pressed(playerIndex))
            TrySkill2();
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

        if (IsGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            hasDoubleJumped = false;
            OnJumped?.Invoke();
        }
        else if (canDoubleJump && !hasDoubleJumped)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, doubleJumpForce);
            hasDoubleJumped = true;
            OnJumped?.Invoke();
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

    // ============ 重生 ============

    public void Respawn(Vector3 position)
    {
        transform.position = position;
        rb.linearVelocity = Vector2.zero;
        IsDashing = false;
        hasDoubleJumped = false;
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
