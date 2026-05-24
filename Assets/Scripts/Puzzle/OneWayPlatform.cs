using UnityEngine;

/// <summary>
/// 单向平台 - 玩家可从下方跳上来，站在上面
/// 按下+跳跃可以穿过平台向下落
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(PlatformEffector2D))]
public class OneWayPlatform : MonoBehaviour
{
    [Header("设置")]
    [SerializeField] private float dropThroughDuration = 0.3f;
    [SerializeField] private float dropCooldown = 0.1f;

    private PlatformEffector2D effector;
    private BoxCollider2D platformCollider;
    private float dropTimer;
    private bool isDropping;

    void Awake()
    {
        effector = GetComponent<PlatformEffector2D>();
        platformCollider = GetComponent<BoxCollider2D>();

        // 配置PlatformEffector2D
        effector.useOneWay = true;
        effector.surfaceArc = 170f;
        effector.useOneWayGrouping = true;

        platformCollider.usedByEffector = true;
    }

    void Update()
    {
        if (isDropping)
        {
            dropTimer -= Time.deltaTime;
            if (dropTimer <= 0)
            {
                isDropping = false;
                effector.rotationalOffset = 0f;
            }
        }
    }

    /// <summary>
    /// 允许玩家穿过平台向下落
    /// 由PlayerController在检测到下+跳跃时调用
    /// </summary>
    public void AllowDropThrough()
    {
        if (isDropping) return;

        isDropping = true;
        dropTimer = dropThroughDuration;
        effector.rotationalOffset = 180f; // 反转，允许穿过
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        // 检查玩家是否正在按下方向
        var player = collision.gameObject.GetComponent<PlayerController>();
        if (player == null) return;

        if (InputManager.Instance != null)
        {
            Vector2 input = InputManager.Instance.GetMoveInput(player.PlayerIndex);
            // 按下+跳跃 = 穿过
            if (input.y < -0.5f && InputManager.Instance.GetJumpPressed(player.PlayerIndex))
            {
                AllowDropThrough();
            }
        }
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.3f);
        Vector3 center = transform.position + (Vector3)col.offset;
        Vector3 size = Vector3.Scale(col.size, transform.lossyScale);
        Gizmos.DrawCube(center, size);

        // 画向上的箭头表示单向
        Gizmos.color = Color.green;
        Vector3 arrowStart = center + Vector3.down * 0.5f;
        Vector3 arrowEnd = center + Vector3.up * 0.5f;
        Gizmos.DrawLine(arrowStart, arrowEnd);
        Gizmos.DrawLine(arrowEnd, arrowEnd + new Vector3(-0.2f, -0.2f, 0));
        Gizmos.DrawLine(arrowEnd, arrowEnd + new Vector3(0.2f, -0.2f, 0));
    }
}
