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

        float speed = moveSpeed;
        if (playerType == PlayerType.Lux && IsInLightZone())
            speed *= (1f + passiveSpeedBuff);
        else if (playerType == PlayerType.Nox && IsInShadowZone())
            speed *= (1f + passiveSpeedBuff);

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

    public void Respawn(Vector3 position)
    {
        transform.position = position;
        rb.linearVelocity = Vector2.zero;
        IsDashing = false;
        hasDoubleJumped = false;
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
