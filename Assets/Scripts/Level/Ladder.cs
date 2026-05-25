using UnityEngine;

/// <summary>
/// 梯子系统 - 允许玩家攀爬垂直/倾斜梯子
/// 进入梯子区域后禁用重力，支持上下攀爬
/// 顶部/底部自动离开，跳跃键可中途跳离
/// 与PlayerController.EnterLadder/ExitLadder联动
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class Ladder : MonoBehaviour
{
    [Header("攀爬设置")]
    [SerializeField] private float climbSpeed = 4f;
    [SerializeField] private bool disableGravity = true;

    [Header("边界")]
    [SerializeField] private Transform topPoint;
    [SerializeField] private Transform bottomPoint;
    [SerializeField] private float exitBoostForce = 3f;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer ladderRenderer;
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 0.7f, 1f);
    [SerializeField] private float highlightPulseSpeed = 2f;

    [Header("音效")]
    [SerializeField] private string climbSound = "ladder_climb";
    [SerializeField] private float climbSoundInterval = 0.3f;

    public float ClimbSpeed => climbSpeed;
    public bool DisableGravity => disableGravity;

    private Color originalColor;
    private bool hasPlayerOnLadder;
    private PlayerController playerOnLadder;
    private float climbSoundTimer;
    private BoxCollider2D col;

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;

        if (ladderRenderer != null)
            originalColor = ladderRenderer.color;

        // 自动设置顶部和底部
        if (topPoint == null || bottomPoint == null)
            AutoSetBounds();
    }

    void Update()
    {
        if (!hasPlayerOnLadder || playerOnLadder == null) return;

        // 高亮脉冲
        if (ladderRenderer != null)
        {
            float pulse = (Mathf.Sin(Time.time * highlightPulseSpeed) + 1f) * 0.5f;
            ladderRenderer.color = Color.Lerp(originalColor, highlightColor, pulse * 0.3f);
        }

        // 攀爬音效
        if (InputManager.Instance != null)
        {
            float inputY = InputManager.Instance.GetMoveInput(playerOnLadder.PlayerIndex).y;
            if (Mathf.Abs(inputY) > 0.1f)
            {
                climbSoundTimer -= Time.deltaTime;
                if (climbSoundTimer <= 0)
                {
                    if (SoundFeedback.Instance != null)
                        SoundFeedback.Instance.Play(climbSound);
                    climbSoundTimer = climbSoundInterval;
                }
            }
        }

        // 边界检测 — 到达顶部或底部自动离开
        CheckBoundsExit();

        // 跳跃离开
        if (InputManager.Instance != null &&
            InputManager.Instance.GetJumpPressed(playerOnLadder.PlayerIndex))
        {
            ExitLadderWithJump();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasPlayerOnLadder) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 需要有上下方向输入才进入梯子
        if (InputManager.Instance != null)
        {
            float inputY = InputManager.Instance.GetMoveInput(player.PlayerIndex).y;
            if (Mathf.Abs(inputY) > 0.3f)
            {
                EnterLadder(player);
            }
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (hasPlayerOnLadder) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 持续检测上下输入
        if (InputManager.Instance != null)
        {
            float inputY = InputManager.Instance.GetMoveInput(player.PlayerIndex).y;
            if (Mathf.Abs(inputY) > 0.3f)
            {
                EnterLadder(player);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!hasPlayerOnLadder) return;

        var player = other.GetComponent<PlayerController>();
        if (player != null && player == playerOnLadder)
        {
            ExitLadder();
        }
    }

    // ==================== 梯子进出 ====================

    private void EnterLadder(PlayerController player)
    {
        hasPlayerOnLadder = true;
        playerOnLadder = player;

        player.EnterLadder(this);

        // 将玩家对齐到梯子中心（水平）
        Vector3 pos = player.transform.position;
        pos.x = transform.position.x;
        player.transform.position = pos;

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(climbSound);
    }

    private void ExitLadder()
    {
        if (playerOnLadder != null)
        {
            playerOnLadder.ExitLadder();
        }

        hasPlayerOnLadder = false;
        playerOnLadder = null;
        climbSoundTimer = 0;

        // 恢复颜色
        if (ladderRenderer != null)
            ladderRenderer.color = originalColor;
    }

    private void ExitLadderWithJump()
    {
        if (playerOnLadder == null) return;

        var rb = playerOnLadder.GetComponent<Rigidbody2D>();
        playerOnLadder.ExitLadder();

        // 给跳离助力
        if (rb != null)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, exitBoostForce);

        hasPlayerOnLadder = false;
        playerOnLadder = null;
        climbSoundTimer = 0;

        if (ladderRenderer != null)
            ladderRenderer.color = originalColor;
    }

    // ==================== 边界检测 ====================

    private void CheckBoundsExit()
    {
        if (playerOnLadder == null) return;

        float playerY = playerOnLadder.transform.position.y;

        // 到达顶部
        if (topPoint != null && playerY >= topPoint.position.y)
        {
            // 给一个小向上推力以便上到平台
            var rb = playerOnLadder.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, exitBoostForce * 0.5f);

            ExitLadder();
        }
        // 到达底部
        else if (bottomPoint != null && playerY <= bottomPoint.position.y)
        {
            ExitLadder();
        }
    }

    // ==================== 辅助 ====================

    private void AutoSetBounds()
    {
        // 根据碰撞体大小自动计算顶部和底部
        if (col != null)
        {
            float halfHeight = col.size.y * transform.lossyScale.y * 0.5f;
            Vector3 center = transform.position + (Vector3)col.offset;

            if (topPoint == null)
            {
                var topGO = new GameObject("LadderTop");
                topGO.transform.SetParent(transform);
                topGO.transform.position = center + Vector3.up * halfHeight;
                topPoint = topGO.transform;
            }

            if (bottomPoint == null)
            {
                var botGO = new GameObject("LadderBottom");
                botGO.transform.SetParent(transform);
                botGO.transform.position = center + Vector3.down * halfHeight;
                bottomPoint = botGO.transform;
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 0.3f, 0.4f);

        var boxCol = GetComponent<BoxCollider2D>();
        if (boxCol != null)
        {
            Vector3 center = transform.position + (Vector3)boxCol.offset;
            Vector3 size = Vector3.Scale(boxCol.size, transform.lossyScale);
            Gizmos.DrawCube(center, size);
        }

        // 顶部和底部标记
        if (topPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(topPoint.position, 0.15f);
        }
        if (bottomPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(bottomPoint.position, 0.15f);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.right * 0.6f, "Ladder");
#endif
    }
}
