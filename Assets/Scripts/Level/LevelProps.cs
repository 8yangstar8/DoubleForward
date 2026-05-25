using UnityEngine;
using System.Collections;

/// <summary>
/// 关卡道具系统 - 各种交互性环境道具
/// SpringPad: 弹射玩家的弹跳板
/// ConveyorBelt: 传送带（水平移动力）
/// TeleportPad: 传送点对
/// DisappearingBlock: 定时出现/消失的方块
/// ToggleBlock: 双人切换方块（Lux激活A组，Nox激活B组）
/// </summary>

// ============ 弹跳板 ============
[RequireComponent(typeof(BoxCollider2D))]
public class SpringPad : MonoBehaviour
{
    [Header("弹射")]
    [SerializeField] private float bounceForce = 18f;
    [SerializeField] private Vector2 bounceDirection = Vector2.up;
    [SerializeField] private bool overrideVelocity = true;

    [Header("视觉")]
    [SerializeField] private Animator springAnimator;
    [SerializeField] private SpriteRenderer padRenderer;
    [SerializeField] private ParticleSystem bounceParticles;

    [Header("音效")]
    [SerializeField] private string bounceSound = "spring_bounce";

    void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var rb = other.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        // 只有玩家或有Rigidbody的可弹射
        var player = other.GetComponent<PlayerController>();
        if (player == null && other.GetComponent<EnemyBase>() == null) return;

        // 弹射
        Vector2 dir = bounceDirection.normalized;
        if (dir == Vector2.zero) dir = Vector2.up;

        if (overrideVelocity)
            rb.linearVelocity = dir * bounceForce;
        else
            rb.AddForce(dir * bounceForce, ForceMode2D.Impulse);

        // 动画
        if (springAnimator != null)
            springAnimator.SetTrigger("Bounce");

        // 挤压效果
        if (padRenderer != null)
            StartCoroutine(SquashStretch());

        // 粒子
        if (bounceParticles != null)
            bounceParticles.Play();

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(bounceSound);

        // 触觉
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Light();
    }

    private IEnumerator SquashStretch()
    {
        Vector3 origScale = padRenderer.transform.localScale;
        float elapsed = 0;
        float dur = 0.2f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float y = Mathf.Lerp(0.5f, 1f, EaseOutBounce(t));
            float x = Mathf.Lerp(1.3f, 1f, EaseOutBounce(t));
            padRenderer.transform.localScale = new Vector3(origScale.x * x, origScale.y * y, origScale.z);
            yield return null;
        }

        padRenderer.transform.localScale = origScale;
    }

    private float EaseOutBounce(float t)
    {
        if (t < 1f / 2.75f)
            return 7.5625f * t * t;
        else if (t < 2f / 2.75f)
        {
            t -= 1.5f / 2.75f;
            return 7.5625f * t * t + 0.75f;
        }
        else
        {
            t -= 2.25f / 2.75f;
            return 7.5625f * t * t + 0.9375f;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.5f);
        Gizmos.DrawRay(transform.position, bounceDirection.normalized * 2f);
        Gizmos.DrawSphere(transform.position, 0.2f);
    }
}

// ============ 传送带 ============
[RequireComponent(typeof(BoxCollider2D))]
public class ConveyorBelt : MonoBehaviour
{
    [Header("传送")]
    [SerializeField] private float beltSpeed = 5f;
    [SerializeField] private Vector2 beltDirection = Vector2.right;
    [SerializeField] private bool affectsEnemies = true;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer beltRenderer;
    [SerializeField] private float scrollSpeed = 2f;

    private Material beltMaterial;

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = false; // 实体碰撞

        if (beltRenderer != null)
        {
            beltMaterial = beltRenderer.material;
        }
    }

    void Update()
    {
        // 纹理滚动
        if (beltMaterial != null)
        {
            Vector2 offset = beltMaterial.mainTextureOffset;
            offset.x += beltDirection.x * scrollSpeed * Time.deltaTime;
            beltMaterial.mainTextureOffset = offset;
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        var rb = collision.collider.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        // 检查是否站在传送带上
        bool isOnTop = false;
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f)
            {
                isOnTop = true;
                break;
            }
        }

        if (!isOnTop) return;

        // 仅对玩家或（可选）敌人生效
        bool isPlayer = collision.collider.GetComponent<PlayerController>() != null;
        bool isEnemy = affectsEnemies && collision.collider.GetComponent<EnemyBase>() != null;

        if (!isPlayer && !isEnemy) return;

        // 施加传送带力
        Vector2 force = beltDirection.normalized * beltSpeed;
        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x + force.x * Time.deltaTime * 10f,
            rb.linearVelocity.y
        );
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.4f);
        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Vector3 center = transform.position + (Vector3)col.offset;
            Gizmos.DrawCube(center, col.size);
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, (Vector3)(beltDirection.normalized * 1.5f));
    }
}

// ============ 传送垫对 ============
public class TeleportPad : MonoBehaviour, IInteractable
{
    [Header("传送")]
    [SerializeField] private TeleportPad linkedPad;
    [SerializeField] private float teleportCooldown = 1f;
    [SerializeField] private bool autoTeleport = false;
    [SerializeField] private float autoTeleportDelay = 0.5f;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer padRenderer;
    [SerializeField] private ParticleSystem idleParticles;
    [SerializeField] private ParticleSystem teleportParticles;
    [SerializeField] private Color padColor = new Color(0.4f, 0.3f, 1f);
    [SerializeField] private float pulseSpeed = 2f;

    [Header("音效")]
    [SerializeField] private string teleportSound = "teleport";

    private float cooldownTimer;
    private bool isOnCooldown;

    void Start()
    {
        GetComponent<Collider2D>().isTrigger = true;

        if (padRenderer != null)
            padRenderer.color = padColor;

        if (idleParticles != null)
            idleParticles.Play();
    }

    void Update()
    {
        if (isOnCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0)
                isOnCooldown = false;
        }

        // 脉冲
        if (padRenderer != null && !isOnCooldown)
        {
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            Color c = padColor;
            c.a = Mathf.Lerp(0.5f, 1f, pulse);
            padRenderer.color = c;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!autoTeleport || isOnCooldown) return;

        var player = other.GetComponent<PlayerController>();
        if (player != null)
            StartCoroutine(AutoTeleportSequence(player));
    }

    private IEnumerator AutoTeleportSequence(PlayerController player)
    {
        yield return new WaitForSeconds(autoTeleportDelay);

        if (linkedPad != null && !isOnCooldown)
            TeleportPlayer(player.gameObject);
    }

    // IInteractable
    public bool CanInteract(GameObject player)
    {
        return linkedPad != null && !isOnCooldown;
    }

    public void OnInteract(GameObject player)
    {
        TeleportPlayer(player);
    }

    public string GetInteractPrompt()
    {
        return "interact_teleport";
    }

    private void TeleportPlayer(GameObject playerObj)
    {
        if (linkedPad == null || isOnCooldown) return;

        // 设置双向冷却
        isOnCooldown = true;
        cooldownTimer = teleportCooldown;
        linkedPad.isOnCooldown = true;
        linkedPad.cooldownTimer = teleportCooldown;

        // 传送
        playerObj.transform.position = linkedPad.transform.position + Vector3.up * 0.5f;

        var rb = playerObj.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        // 效果
        if (teleportParticles != null)
            teleportParticles.Play();
        if (linkedPad.teleportParticles != null)
            linkedPad.teleportParticles.Play();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(teleportSound);

        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Light();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = padColor;
        Gizmos.DrawWireSphere(transform.position, 0.4f);

        if (linkedPad != null)
        {
            Gizmos.color = new Color(padColor.r, padColor.g, padColor.b, 0.3f);
            Gizmos.DrawLine(transform.position, linkedPad.transform.position);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, "Teleport");
#endif
    }
}

// ============ 定时出现/消失方块 ============
public class DisappearingBlock : MonoBehaviour
{
    [Header("周期")]
    [SerializeField] private float visibleDuration = 2f;
    [SerializeField] private float hiddenDuration = 2f;
    [SerializeField] private float transitionDuration = 0.3f;
    [SerializeField] private float startOffset = 0f;
    [SerializeField] private bool startVisible = true;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer blockRenderer;
    [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 0.5f);

    private Collider2D col;
    private Color originalColor;
    private bool isVisible;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (blockRenderer != null)
            originalColor = blockRenderer.color;

        isVisible = startVisible;
        SetBlockState(startVisible);
    }

    void Start()
    {
        StartCoroutine(BlockCycle());
    }

    private IEnumerator BlockCycle()
    {
        if (startOffset > 0)
            yield return new WaitForSeconds(startOffset);

        while (true)
        {
            if (isVisible)
            {
                // 预警闪烁
                yield return FlashWarning(0.5f);

                // 消失
                yield return TransitionBlock(false);
                yield return new WaitForSeconds(hiddenDuration);
            }
            else
            {
                // 出现
                yield return TransitionBlock(true);
                yield return new WaitForSeconds(visibleDuration);
            }
        }
    }

    private IEnumerator FlashWarning(float duration)
    {
        if (blockRenderer == null) yield break;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float flash = Mathf.PingPong(elapsed * 8f, 1f);
            blockRenderer.color = Color.Lerp(originalColor, flashColor, flash);
            yield return null;
        }
    }

    private IEnumerator TransitionBlock(bool visible)
    {
        float elapsed = 0;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;

            if (blockRenderer != null)
            {
                Color c = originalColor;
                c.a = visible ? Mathf.Lerp(0f, originalColor.a, t) : Mathf.Lerp(originalColor.a, 0f, t);
                blockRenderer.color = c;
            }

            yield return null;
        }

        isVisible = visible;
        SetBlockState(visible);
    }

    private void SetBlockState(bool visible)
    {
        if (col != null) col.enabled = visible;
        if (blockRenderer != null)
        {
            Color c = originalColor;
            c.a = visible ? originalColor.a : 0f;
            blockRenderer.color = c;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isVisible ?
            new Color(0f, 0.8f, 1f, 0.4f) :
            new Color(0.5f, 0.5f, 0.5f, 0.2f);

        var boxCol = GetComponent<BoxCollider2D>();
        if (boxCol != null)
        {
            Vector3 center = transform.position + (Vector3)boxCol.offset;
            Gizmos.DrawCube(center, boxCol.size);
        }
    }
}

// ============ 双人切换方块 ============
public class ToggleBlock : MonoBehaviour
{
    [Header("阵营")]
    [SerializeField] private BlockGroup group = BlockGroup.GroupA;
    [SerializeField] private bool defaultActive = true;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer blockRenderer;
    [SerializeField] private Color groupAColor = new Color(1f, 0.9f, 0.5f);
    [SerializeField] private Color groupBColor = new Color(0.5f, 0.3f, 0.9f);
    [SerializeField] private float transitionSpeed = 5f;

    public enum BlockGroup { GroupA, GroupB }

    private Collider2D col;
    private bool isActive;
    private float targetAlpha;

    // 全局切换（静态）
    private static bool groupAActive = true;
    public static event System.Action<bool> OnGroupToggled; // true = GroupA active

    void Awake()
    {
        col = GetComponent<Collider2D>();
        isActive = defaultActive;

        if (blockRenderer != null)
            blockRenderer.color = group == BlockGroup.GroupA ? groupAColor : groupBColor;
    }

    void OnEnable()
    {
        OnGroupToggled += HandleToggle;
    }

    void OnDisable()
    {
        OnGroupToggled -= HandleToggle;
    }

    void Start()
    {
        // 初始化状态
        bool shouldBeActive = (group == BlockGroup.GroupA) == groupAActive;
        SetActive(shouldBeActive, true);
    }

    void Update()
    {
        // 平滑过渡
        if (blockRenderer != null)
        {
            Color c = blockRenderer.color;
            c.a = Mathf.MoveTowards(c.a, targetAlpha, transitionSpeed * Time.deltaTime);
            blockRenderer.color = c;
        }
    }

    private void HandleToggle(bool groupAIsActive)
    {
        bool shouldBeActive = (group == BlockGroup.GroupA) == groupAIsActive;
        SetActive(shouldBeActive, false);
    }

    private void SetActive(bool active, bool instant)
    {
        isActive = active;
        if (col != null) col.enabled = active;
        targetAlpha = active ? 1f : 0.15f;

        if (instant && blockRenderer != null)
        {
            Color c = blockRenderer.color;
            c.a = targetAlpha;
            blockRenderer.color = c;
        }
    }

    /// <summary>
    /// 全局切换方块组（由开关、压力板等调用）
    /// </summary>
    public static void Toggle()
    {
        groupAActive = !groupAActive;
        OnGroupToggled?.Invoke(groupAActive);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("toggle_switch");
    }

    /// <summary>
    /// 设置为特定状态
    /// </summary>
    public static void SetGroupState(bool groupAIsActive)
    {
        groupAActive = groupAIsActive;
        OnGroupToggled?.Invoke(groupAActive);
    }

    void OnDrawGizmos()
    {
        Color c = group == BlockGroup.GroupA ? groupAColor : groupBColor;
        c.a = isActive ? 0.6f : 0.15f;
        Gizmos.color = c;

        var boxCol = GetComponent<BoxCollider2D>();
        if (boxCol != null)
        {
            Vector3 center = transform.position + (Vector3)boxCol.offset;
            Gizmos.DrawCube(center, boxCol.size);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
            $"Toggle {group}");
#endif
    }
}
