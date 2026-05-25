using UnityEngine;

/// <summary>
/// 单向平台 - 玩家可以从下方跳上、从上方按下跳过
/// 使用PlatformEffector2D实现
/// 支持按下键穿越平台
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(PlatformEffector2D))]
public class OneWayPlatform : MonoBehaviour
{
    [Header("穿越设置")]
    [SerializeField] private float dropThroughDuration = 0.3f;
    [SerializeField] private float dropCooldown = 0.2f;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer platformRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color fadeColor = new Color(1f, 1f, 1f, 0.3f);

    private BoxCollider2D col;
    private PlatformEffector2D effector;
    private float dropTimer;
    private float cooldownTimer;
    private bool isDropping;

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        effector = GetComponent<PlatformEffector2D>();

        // 配置PlatformEffector2D
        col.usedByEffector = true;
        effector.useOneWay = true;
        effector.surfaceArc = 170f; // 允许从两侧略微倾斜通过

        if (platformRenderer != null)
            normalColor = platformRenderer.color;
    }

    void Update()
    {
        if (cooldownTimer > 0)
            cooldownTimer -= Time.deltaTime;

        if (isDropping)
        {
            dropTimer -= Time.deltaTime;
            if (dropTimer <= 0)
            {
                StopDropThrough();
            }
        }

        // 检查玩家按下输入
        CheckDropInput();
    }

    private void CheckDropInput()
    {
        if (isDropping || cooldownTimer > 0) return;
        if (InputManager.Instance == null) return;

        // 检查两个玩家的输入
        for (int i = 0; i < 2; i++)
        {
            Vector2 input = InputManager.Instance.GetMoveInput(i);
            bool jumpPressed = InputManager.Instance.GetJumpPressed(i);

            // 向下 + 跳跃 = 穿越平台
            if (input.y < -0.5f && jumpPressed)
            {
                // 确认玩家在平台上方
                var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                foreach (var player in players)
                {
                    if (player.PlayerIndex != i) continue;
                    if (!player.IsGrounded) continue;

                    // 检查玩家是否站在此平台上
                    float playerBottom = player.transform.position.y - 0.5f;
                    float platformTop = transform.position.y + col.offset.y + col.size.y * 0.5f;

                    if (Mathf.Abs(playerBottom - platformTop) < 0.3f)
                    {
                        StartDropThrough();
                        break;
                    }
                }
            }
        }
    }

    private void StartDropThrough()
    {
        isDropping = true;
        dropTimer = dropThroughDuration;

        // 临时翻转effector方向让玩家穿过
        effector.rotationalOffset = 180f;

        // 视觉淡化
        if (platformRenderer != null)
            platformRenderer.color = fadeColor;
    }

    private void StopDropThrough()
    {
        isDropping = false;
        cooldownTimer = dropCooldown;

        // 恢复正常
        effector.rotationalOffset = 0f;

        if (platformRenderer != null)
            platformRenderer.color = normalColor;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.4f);

        var boxCol = GetComponent<BoxCollider2D>();
        if (boxCol != null)
        {
            Vector3 center = transform.position + (Vector3)boxCol.offset;
            Vector3 size = Vector3.Scale(boxCol.size, transform.lossyScale);
            Gizmos.DrawCube(center, size);

            // 箭头标示方向
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.8f);
            Gizmos.DrawLine(center + Vector3.up * 0.3f, center + Vector3.up * 0.6f);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, "One-Way");
#endif
    }
}
