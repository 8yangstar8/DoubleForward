using UnityEngine;

/// <summary>
/// 弹跳板 - 玩家踏上后被弹射到高处
/// 支持角度弹射、力度配置、视觉反馈
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BouncePad : MonoBehaviour
{
    [Header("弹跳设置")]
    [SerializeField] private float bounceForce = 18f;
    [SerializeField] private float bounceAngle = 0f; // 0=正上方, 正值=右偏, 负值=左偏
    [SerializeField] private bool overrideVelocity = true;
    [SerializeField] private bool preserveHorizontalSpeed = false;

    [Header("连击弹跳")]
    [SerializeField] private bool enableChainBounce = false;
    [SerializeField] private float chainBounceMultiplier = 1.2f;
    [SerializeField] private float chainWindowSeconds = 0.5f;

    [Header("视觉")]
    [SerializeField] private Animator padAnimator;
    [SerializeField] private string bounceAnimTrigger = "Bounce";
    [SerializeField] private GameObject bounceEffectPrefab;
    [SerializeField] private Color padColor = new Color(1f, 0.5f, 0f);

    [Header("音效")]
    [SerializeField] private string bounceSoundKey = "bounce_pad";

    private float lastBounceTime;
    private int chainCount;
    private static readonly int AnimBounce = Animator.StringToHash("Bounce");

    void Awake()
    {
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = false; // 物理碰撞，不是trigger

        if (padAnimator == null)
            padAnimator = GetComponent<Animator>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 只在从上方踩踏时触发
        if (!collision.gameObject.CompareTag("Player")) return;

        // 检查接触方向 - 只有从上方踩踏时触发
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f) // 玩家在上方
            {
                Bounce(collision.rigidbody);
                return;
            }
        }
    }

    private void Bounce(Rigidbody2D rb)
    {
        if (rb == null) return;

        // 连击弹跳
        float multiplier = 1f;
        if (enableChainBounce)
        {
            if (Time.time - lastBounceTime < chainWindowSeconds)
            {
                chainCount++;
                multiplier = Mathf.Pow(chainBounceMultiplier, Mathf.Min(chainCount, 5));
            }
            else
            {
                chainCount = 0;
            }
            lastBounceTime = Time.time;
        }

        // 计算弹射方向
        float angleRad = bounceAngle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad)).normalized;

        float force = bounceForce * multiplier;

        if (overrideVelocity)
        {
            float hSpeed = preserveHorizontalSpeed ? rb.linearVelocity.x : direction.x * force;
            rb.linearVelocity = new Vector2(hSpeed, direction.y * force);
        }
        else
        {
            rb.AddForce(direction * force, ForceMode2D.Impulse);
        }

        // 动画
        if (padAnimator != null)
            padAnimator.SetTrigger(AnimBounce);

        // 特效
        if (bounceEffectPrefab != null)
        {
            var effect = Instantiate(bounceEffectPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            Destroy(effect, 2f);
        }

        // 音效
        AudioManager.Instance?.PlaySFX(bounceSoundKey);

        // 震动
        CameraShake.Instance?.ShakeLight();

        // 触感反馈
        HapticFeedback.Instance?.PlayPreset(HapticFeedback.HapticPreset.Light);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = padColor;

        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Vector3 center = transform.position + (Vector3)col.offset;
            Vector3 size = Vector3.Scale(col.size, transform.lossyScale);
            Gizmos.DrawCube(center, size);
        }

        // 画弹射方向
        Gizmos.color = Color.yellow;
        float angleRad = bounceAngle * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(angleRad), Mathf.Cos(angleRad), 0);
        Vector3 start = transform.position + Vector3.up * 0.3f;
        float arrowLength = bounceForce * 0.15f;
        Gizmos.DrawRay(start, dir * arrowLength);

        // 箭头头部
        Vector3 arrowEnd = start + dir * arrowLength;
        Vector3 perpendicular = new Vector3(-dir.y, dir.x, 0) * 0.15f;
        Gizmos.DrawLine(arrowEnd, arrowEnd - dir * 0.3f + perpendicular);
        Gizmos.DrawLine(arrowEnd, arrowEnd - dir * 0.3f - perpendicular);
    }
}
