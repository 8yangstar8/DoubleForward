using UnityEngine;

/// <summary>
/// 荡绳 - 玩家抓住后可以荡过深渊
/// 物理模拟的钟摆运动，按跳跃释放并获得弹射速度
/// </summary>
public class SwingRope : MonoBehaviour
{
    [Header("绳索设置")]
    [SerializeField] private Transform anchorPoint;
    [SerializeField] private float ropeLength = 5f;
    [SerializeField] private float swingForce = 8f;
    [SerializeField] private float gravity = 15f;
    [SerializeField] private float damping = 0.98f;
    [SerializeField] private float launchBoost = 1.3f;

    [Header("抓取设置")]
    [SerializeField] private float grabRadius = 1.5f;
    [SerializeField] private bool autoGrab = true;
    [SerializeField] private KeyCode grabKey = KeyCode.E;

    [Header("视觉")]
    [SerializeField] private LineRenderer ropeRenderer;
    [SerializeField] private int ropeSegments = 10;

    // 运行时状态
    private PlayerController attachedPlayer;
    private Rigidbody2D playerRb;
    private float currentAngle; // 弧度
    private float angularVelocity;
    private bool isActive;
    private float originalGravity;

    void Start()
    {
        if (anchorPoint == null)
            anchorPoint = transform;

        if (ropeRenderer != null)
        {
            ropeRenderer.positionCount = ropeSegments;
            UpdateRopeVisual();
        }
    }

    void Update()
    {
        if (isActive && attachedPlayer != null)
        {
            HandleSwing();
            UpdatePlayerPosition();

            // 按跳跃释放
            if (InputManager.Instance != null &&
                InputManager.Instance.GetJumpPressed(attachedPlayer.PlayerIndex))
            {
                Release();
            }
        }
        else if (autoGrab)
        {
            CheckForPlayer();
        }

        UpdateRopeVisual();
    }

    private void HandleSwing()
    {
        // 输入加速
        if (InputManager.Instance != null)
        {
            Vector2 input = InputManager.Instance.GetMoveInput(attachedPlayer.PlayerIndex);
            angularVelocity += input.x * swingForce * Time.deltaTime;
        }

        // 重力影响钟摆
        angularVelocity += -Mathf.Sin(currentAngle) * gravity / ropeLength * Time.deltaTime;

        // 阻尼
        angularVelocity *= damping;

        // 更新角度
        currentAngle += angularVelocity * Time.deltaTime;

        // 限制摆动角度
        currentAngle = Mathf.Clamp(currentAngle, -Mathf.PI * 0.45f, Mathf.PI * 0.45f);
    }

    private void UpdatePlayerPosition()
    {
        if (attachedPlayer == null) return;

        Vector3 ropeEnd = GetRopeEndPosition();
        attachedPlayer.transform.position = ropeEnd;

        // 保持速度为0（由绳索控制移动）
        if (playerRb != null)
            playerRb.linearVelocity = Vector2.zero;
    }

    private Vector3 GetRopeEndPosition()
    {
        float x = anchorPoint.position.x + Mathf.Sin(currentAngle) * ropeLength;
        float y = anchorPoint.position.y - Mathf.Cos(currentAngle) * ropeLength;
        return new Vector3(x, y, 0);
    }

    private void CheckForPlayer()
    {
        if (isActive) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(GetRopeEndPosition(), grabRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                var player = hit.GetComponent<PlayerController>();
                if (player != null)
                {
                    Grab(player);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 玩家抓住绳索
    /// </summary>
    public void Grab(PlayerController player)
    {
        if (isActive) return;

        attachedPlayer = player;
        playerRb = player.GetComponent<Rigidbody2D>();
        isActive = true;

        // 保存原始重力
        if (playerRb != null)
        {
            originalGravity = playerRb.gravityScale;
            playerRb.gravityScale = 0;
            playerRb.linearVelocity = Vector2.zero;
        }

        // 根据玩家位置计算初始角度
        Vector3 toPlayer = player.transform.position - anchorPoint.position;
        currentAngle = Mathf.Atan2(toPlayer.x, -toPlayer.y);
        angularVelocity = 0;

        AudioManager.Instance?.PlaySFX("rope_grab");
    }

    /// <summary>
    /// 释放绳索，弹射玩家
    /// </summary>
    public void Release()
    {
        if (!isActive || attachedPlayer == null) return;

        // 计算弹射速度
        Vector2 tangent = new Vector2(
            Mathf.Cos(currentAngle),
            Mathf.Sin(currentAngle)
        );
        Vector2 launchVelocity = tangent * angularVelocity * ropeLength * launchBoost;

        // 向上修正
        launchVelocity.y = Mathf.Max(launchVelocity.y, 3f);

        // 恢复物理
        if (playerRb != null)
        {
            playerRb.gravityScale = originalGravity;
            playerRb.linearVelocity = launchVelocity;
        }

        isActive = false;
        attachedPlayer = null;
        playerRb = null;

        AudioManager.Instance?.PlaySFX("rope_release");
    }

    private void UpdateRopeVisual()
    {
        if (ropeRenderer == null) return;

        Vector3 start = anchorPoint.position;
        Vector3 end = isActive ? GetRopeEndPosition() : start + Vector3.down * ropeLength;

        for (int i = 0; i < ropeSegments; i++)
        {
            float t = (float)i / (ropeSegments - 1);

            // 稍微下垂的曲线
            Vector3 point = Vector3.Lerp(start, end, t);
            if (!isActive)
            {
                float sag = Mathf.Sin(t * Mathf.PI) * 0.3f;
                point.y -= sag;
            }

            ropeRenderer.SetPosition(i, point);
        }
    }

    void OnDrawGizmos()
    {
        Transform anchor = anchorPoint != null ? anchorPoint : transform;

        // 锚点
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(anchor.position, 0.3f);

        // 绳索
        Gizmos.color = new Color(0.6f, 0.4f, 0.2f);
        Vector3 bottom = anchor.position + Vector3.down * ropeLength;
        Gizmos.DrawLine(anchor.position, bottom);

        // 抓取范围
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(bottom, grabRadius);

        // 摆动弧线
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        int arcSegments = 20;
        for (int i = 0; i < arcSegments; i++)
        {
            float angle1 = Mathf.Lerp(-Mathf.PI * 0.45f, Mathf.PI * 0.45f, (float)i / arcSegments);
            float angle2 = Mathf.Lerp(-Mathf.PI * 0.45f, Mathf.PI * 0.45f, (float)(i + 1) / arcSegments);

            Vector3 p1 = new Vector3(
                anchor.position.x + Mathf.Sin(angle1) * ropeLength,
                anchor.position.y - Mathf.Cos(angle1) * ropeLength,
                0
            );
            Vector3 p2 = new Vector3(
                anchor.position.x + Mathf.Sin(angle2) * ropeLength,
                anchor.position.y - Mathf.Cos(angle2) * ropeLength,
                0
            );
            Gizmos.DrawLine(p1, p2);
        }
    }
}
