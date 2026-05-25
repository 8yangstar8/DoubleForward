using UnityEngine;
using System.Collections;

/// <summary>
/// 陷阱系统 - 各种机关陷阱类型
/// SpikeTrap: 周期性升降的尖刺陷阱
/// FallingPlatform: 踩上后延迟坍塌的平台
/// SawBlade: 沿路径移动的锯刃
/// CrumblingPlatform: 踩踏后碎裂的一次性平台
/// ArrowTrap: 感应触发的箭矢陷阱
/// </summary>

// ============ 尖刺陷阱 ============
public class SpikeTrap : MonoBehaviour
{
    [Header("伤害")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackForce = 8f;

    [Header("周期")]
    [SerializeField] private float activeTime = 1.5f;
    [SerializeField] private float retractedTime = 2f;
    [SerializeField] private float extendDuration = 0.15f;
    [SerializeField] private float retractDuration = 0.3f;
    [SerializeField] private float startDelay = 0f;

    [Header("视觉")]
    [SerializeField] private Transform spikeTransform;
    [SerializeField] private Vector3 retractedPosition = Vector3.down * 0.5f;
    [SerializeField] private Vector3 extendedPosition = Vector3.up * 0.3f;
    [SerializeField] private ParticleSystem warningParticles;

    [Header("音效")]
    [SerializeField] private string extendSound = "spike_extend";
    [SerializeField] private string retractSound = "spike_retract";

    private bool isExtended;
    private bool isAnimating;
    private Collider2D damageCollider;

    void Awake()
    {
        damageCollider = GetComponent<Collider2D>();
        if (damageCollider != null)
            damageCollider.enabled = false;

        if (spikeTransform != null)
            spikeTransform.localPosition = retractedPosition;
    }

    void Start()
    {
        StartCoroutine(SpikeCycle());
    }

    private IEnumerator SpikeCycle()
    {
        if (startDelay > 0)
            yield return new WaitForSeconds(startDelay);

        while (true)
        {
            // 预警
            if (warningParticles != null)
                warningParticles.Play();

            yield return new WaitForSeconds(0.3f);

            // 伸出
            yield return AnimateSpike(retractedPosition, extendedPosition, extendDuration);
            isExtended = true;
            if (damageCollider != null) damageCollider.enabled = true;

            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play(extendSound);

            // 屏幕微震
            if (VFXManager.Instance != null)
                VFXManager.Instance.ShakeLight();

            yield return new WaitForSeconds(activeTime);

            // 收回
            isExtended = false;
            if (damageCollider != null) damageCollider.enabled = false;
            yield return AnimateSpike(extendedPosition, retractedPosition, retractDuration);

            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play(retractSound);

            if (warningParticles != null)
                warningParticles.Stop();

            yield return new WaitForSeconds(retractedTime);
        }
    }

    private IEnumerator AnimateSpike(Vector3 from, Vector3 to, float duration)
    {
        if (spikeTransform == null) yield break;

        isAnimating = true;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            spikeTransform.localPosition = Vector3.Lerp(from, to, t);
            yield return null;
        }

        spikeTransform.localPosition = to;
        isAnimating = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isExtended) return;

        var health = other.GetComponent<PlayerHealth>();
        if (health != null && health.IsAlive)
        {
            health.TakeDamage(damage);

            // 击退
            var rb = other.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 knockDir = (other.transform.position - transform.position).normalized;
                knockDir.y = Mathf.Max(knockDir.y, 0.5f);
                rb.AddForce(knockDir * knockbackForce, ForceMode2D.Impulse);
            }

            if (HapticFeedback.Instance != null)
                HapticFeedback.Instance.Light();
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isExtended ? Color.red : new Color(1f, 0.5f, 0f, 0.5f);

        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Vector3 center = transform.position + (Vector3)col.offset;
            Gizmos.DrawWireCube(center, col.size);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}

// ============ 坍塌平台 ============
public class FallingPlatform : MonoBehaviour
{
    [Header("坍塌")]
    [SerializeField] private float shakeDelay = 0.5f;
    [SerializeField] private float fallDelay = 0.3f;
    [SerializeField] private float fallSpeed = 8f;
    [SerializeField] private float respawnTime = 5f;

    [Header("震动")]
    [SerializeField] private float shakeIntensity = 0.05f;
    [SerializeField] private float shakeFrequency = 30f;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer platformRenderer;
    [SerializeField] private ParticleSystem crumbleParticles;
    [SerializeField] private Color warningColor = new Color(1f, 0.7f, 0.3f);

    [Header("音效")]
    [SerializeField] private string crumbleSound = "platform_crumble";
    [SerializeField] private string fallSound = "platform_fall";

    private Vector3 originalPosition;
    private Color originalColor;
    private Collider2D col;
    private bool isTriggered;
    private bool isFalling;

    void Awake()
    {
        originalPosition = transform.position;
        col = GetComponent<Collider2D>();

        if (platformRenderer != null)
            originalColor = platformRenderer.color;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isTriggered) return;

        // 只有从上方踩上才触发
        var player = collision.collider.GetComponent<PlayerController>();
        if (player == null) return;

        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f) // 玩家在上方
            {
                isTriggered = true;
                StartCoroutine(FallSequence());
                break;
            }
        }
    }

    private IEnumerator FallSequence()
    {
        // 阶段1: 警告震动
        if (platformRenderer != null)
            platformRenderer.color = warningColor;

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(crumbleSound);

        float shakeElapsed = 0;
        while (shakeElapsed < shakeDelay)
        {
            shakeElapsed += Time.deltaTime;
            float offsetX = Mathf.Sin(shakeElapsed * shakeFrequency) * shakeIntensity;
            float offsetY = Mathf.Cos(shakeElapsed * shakeFrequency * 1.3f) * shakeIntensity * 0.5f;
            transform.position = originalPosition + new Vector3(offsetX, offsetY, 0);
            yield return null;
        }

        // 阶段2: 短暂停顿
        transform.position = originalPosition;
        yield return new WaitForSeconds(fallDelay);

        // 阶段3: 坠落
        isFalling = true;

        if (crumbleParticles != null)
        {
            crumbleParticles.transform.SetParent(null);
            crumbleParticles.transform.position = originalPosition;
            crumbleParticles.Play();
        }

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(fallSound);

        // 禁用碰撞
        if (col != null) col.enabled = false;

        // 坠落 + 淡出
        float fallElapsed = 0;
        float fadeTime = 1f;
        while (fallElapsed < fadeTime)
        {
            fallElapsed += Time.deltaTime;
            transform.position += Vector3.down * fallSpeed * Time.deltaTime;

            if (platformRenderer != null)
            {
                Color c = platformRenderer.color;
                c.a = Mathf.Lerp(1f, 0f, fallElapsed / fadeTime);
                platformRenderer.color = c;
            }

            yield return null;
        }

        // 隐藏
        gameObject.SetActive(false);

        // 重生
        yield return new WaitForSecondsRealtime(respawnTime);
        RespawnPlatform();
    }

    private void RespawnPlatform()
    {
        transform.position = originalPosition;
        isTriggered = false;
        isFalling = false;

        if (col != null) col.enabled = true;
        if (platformRenderer != null)
            platformRenderer.color = originalColor;

        gameObject.SetActive(true);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.4f);
        Gizmos.DrawCube(transform.position, new Vector3(2f, 0.3f, 0));

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, "Falling");
#endif
    }
}

// ============ 锯刃 ============
public class SawBlade : MonoBehaviour
{
    [Header("移动")]
    [SerializeField] private Transform[] pathPoints;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float waitAtPoint = 0.5f;
    [SerializeField] private bool pingPong = true;

    [Header("旋转")]
    [SerializeField] private float rotateSpeed = 360f;
    [SerializeField] private Transform bladeVisual;

    [Header("伤害")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float damageCooldown = 0.5f;

    [Header("音效")]
    [SerializeField] private string sawSound = "saw_blade";

    private int currentPoint;
    private int direction = 1;
    private float waitTimer;
    private float lastDamageTime;

    void Start()
    {
        if (pathPoints == null || pathPoints.Length < 2) return;
        transform.position = pathPoints[0].position;

        // 循环音效
        if (!string.IsNullOrEmpty(sawSound) && AudioManager.Instance != null)
            AudioManager.Instance.PlayAmbient(sawSound);
    }

    void Update()
    {
        if (pathPoints == null || pathPoints.Length < 2) return;

        // 旋转视觉
        if (bladeVisual != null)
            bladeVisual.Rotate(0, 0, rotateSpeed * Time.deltaTime);

        // 移动
        MoveAlongPath();
    }

    private void MoveAlongPath()
    {
        if (waitTimer > 0)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        int targetIndex = currentPoint + direction;

        // 边界检查
        if (targetIndex >= pathPoints.Length || targetIndex < 0)
        {
            if (pingPong)
            {
                direction *= -1;
                targetIndex = currentPoint + direction;
            }
            else
            {
                targetIndex = 0;
                currentPoint = -1;
                direction = 1;
                targetIndex = 0;
            }
        }

        if (targetIndex < 0 || targetIndex >= pathPoints.Length) return;

        Vector3 target = pathPoints[targetIndex].position;
        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.05f)
        {
            currentPoint = targetIndex;
            waitTimer = waitAtPoint;
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time - lastDamageTime < damageCooldown) return;

        var health = other.GetComponent<PlayerHealth>();
        if (health != null && health.IsAlive)
        {
            health.TakeDamage(damage);
            lastDamageTime = Time.time;

            // 击退（远离锯刃）
            var rb = other.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 knockDir = (other.transform.position - transform.position).normalized;
                rb.AddForce(knockDir * knockbackForce, ForceMode2D.Impulse);
            }

            if (HapticFeedback.Instance != null)
                HapticFeedback.Instance.Medium();
        }

        // 对敌人也造成伤害
        var enemy = other.GetComponent<EnemyBase>();
        if (enemy != null && !enemy.IsDead)
        {
            enemy.TakeDamage(damage * 2);
            lastDamageTime = Time.time;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        if (pathPoints != null && pathPoints.Length > 1)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            for (int i = 0; i < pathPoints.Length - 1; i++)
            {
                if (pathPoints[i] != null && pathPoints[i + 1] != null)
                    Gizmos.DrawLine(pathPoints[i].position, pathPoints[i + 1].position);
            }

            foreach (var p in pathPoints)
            {
                if (p != null)
                    Gizmos.DrawSphere(p.position, 0.15f);
            }
        }
    }
}

// ============ 碎裂平台 ============
public class CrumblingPlatform : MonoBehaviour
{
    [Header("碎裂")]
    [SerializeField] private float crumbleDelay = 0.8f;
    [SerializeField] private int segments = 4;
    [SerializeField] private float segmentForce = 3f;

    [Header("重生")]
    [SerializeField] private bool canRespawn = true;
    [SerializeField] private float respawnTime = 8f;
    [SerializeField] private float reformDuration = 0.5f;

    [Header("视觉")]
    [SerializeField] private Sprite[] segmentSprites;
    [SerializeField] private ParticleSystem dustParticles;
    [SerializeField] private Color crackColor = new Color(0.8f, 0.6f, 0.4f);

    [Header("音效")]
    [SerializeField] private string crackSound = "platform_crack";
    [SerializeField] private string crumbleSound = "platform_crumble";

    private SpriteRenderer mainRenderer;
    private Collider2D col;
    private bool isTriggered;
    private bool isCrumbled;

    void Awake()
    {
        mainRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isTriggered || isCrumbled) return;

        var player = collision.collider.GetComponent<PlayerController>();
        if (player == null) return;

        // 只有从上方踩上才触发
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f)
            {
                isTriggered = true;
                StartCoroutine(CrumbleSequence());
                break;
            }
        }
    }

    private IEnumerator CrumbleSequence()
    {
        // 龟裂效果
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(crackSound);

        if (mainRenderer != null)
        {
            float crackElapsed = 0;
            Color origColor = mainRenderer.color;
            while (crackElapsed < crumbleDelay)
            {
                crackElapsed += Time.deltaTime;
                float t = crackElapsed / crumbleDelay;
                mainRenderer.color = Color.Lerp(origColor, crackColor, t);

                // 微震
                float shake = Mathf.Sin(crackElapsed * 40f) * 0.02f * t;
                transform.position += new Vector3(shake, 0, 0);

                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(crumbleDelay);
        }

        // 碎裂
        isCrumbled = true;
        if (col != null) col.enabled = false;
        if (mainRenderer != null) mainRenderer.enabled = false;

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(crumbleSound);

        if (dustParticles != null)
            dustParticles.Play();

        // 生成碎片（纯视觉）
        SpawnSegments();

        // 重生
        if (canRespawn)
        {
            yield return new WaitForSeconds(respawnTime);
            yield return ReformPlatform();
        }
    }

    private void SpawnSegments()
    {
        for (int i = 0; i < segments; i++)
        {
            var segGO = new GameObject($"Segment_{i}");
            segGO.transform.position = transform.position +
                new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.1f, 0.1f), 0);

            var segSR = segGO.AddComponent<SpriteRenderer>();
            if (segmentSprites != null && i < segmentSprites.Length && segmentSprites[i] != null)
                segSR.sprite = segmentSprites[i];
            else if (mainRenderer != null)
            {
                segSR.sprite = mainRenderer.sprite;
                segGO.transform.localScale = Vector3.one * (1f / segments);
            }

            var segRB = segGO.AddComponent<Rigidbody2D>();
            segRB.gravityScale = 2f;
            segRB.AddForce(new Vector2(
                Random.Range(-1f, 1f), Random.Range(0.5f, 1.5f)
            ) * segmentForce, ForceMode2D.Impulse);
            segRB.AddTorque(Random.Range(-10f, 10f));

            Destroy(segGO, 3f);
        }
    }

    private IEnumerator ReformPlatform()
    {
        isTriggered = false;
        isCrumbled = false;

        if (mainRenderer != null)
        {
            mainRenderer.enabled = true;
            Color c = mainRenderer.color;
            c.a = 0f;
            mainRenderer.color = c;

            float elapsed = 0;
            while (elapsed < reformDuration)
            {
                elapsed += Time.deltaTime;
                c.a = elapsed / reformDuration;
                mainRenderer.color = c;
                yield return null;
            }

            c.a = 1f;
            mainRenderer.color = c;
        }

        if (col != null) col.enabled = true;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.8f, 0.5f, 0.2f, 0.4f);

        var boxCol = GetComponent<BoxCollider2D>();
        if (boxCol != null)
        {
            Vector3 center = transform.position + (Vector3)boxCol.offset;
            Gizmos.DrawCube(center, boxCol.size);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, "Crumble");
#endif
    }
}

// ============ 箭矢陷阱 ============
public class ArrowTrap : MonoBehaviour
{
    [Header("箭矢")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Vector2 fireDirection = Vector2.left;
    [SerializeField] private float arrowSpeed = 15f;
    [SerializeField] private int arrowDamage = 1;

    [Header("触发")]
    [SerializeField] private TriggerMode triggerMode = TriggerMode.Periodic;
    [SerializeField] private float fireInterval = 2f;
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private LayerMask detectionMask;
    [SerializeField] private float startDelay = 0f;

    [Header("视觉")]
    [SerializeField] private Animator trapAnimator;
    [SerializeField] private ParticleSystem fireParticles;

    [Header("音效")]
    [SerializeField] private string fireSound = "arrow_fire";

    public enum TriggerMode
    {
        Periodic,      // 周期发射
        PlayerDetect,  // 检测到玩家发射
        Manual         // 手动触发（通过开关等）
    }

    private float fireTimer;

    void Start()
    {
        fireTimer = startDelay;
    }

    void Update()
    {
        switch (triggerMode)
        {
            case TriggerMode.Periodic:
                fireTimer -= Time.deltaTime;
                if (fireTimer <= 0)
                {
                    Fire();
                    fireTimer = fireInterval;
                }
                break;

            case TriggerMode.PlayerDetect:
                fireTimer -= Time.deltaTime;
                if (fireTimer <= 0 && DetectPlayer())
                {
                    Fire();
                    fireTimer = fireInterval;
                }
                break;
        }
    }

    private bool DetectPlayer()
    {
        if (firePoint == null) return false;

        var hit = Physics2D.Raycast(firePoint.position, fireDirection.normalized,
            detectionRange, detectionMask);

        return hit.collider != null && hit.collider.GetComponent<PlayerController>() != null;
    }

    /// <summary>
    /// 手动触发发射（开关、压力板等调用）
    /// </summary>
    public void Fire()
    {
        if (arrowPrefab == null || firePoint == null) return;

        GameObject arrow;
        if (ObjectPool.Instance != null)
            arrow = ObjectPool.Instance.Get(arrowPrefab, firePoint.position, Quaternion.identity);
        else
            arrow = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);

        // 设置方向
        Vector2 dir = fireDirection.normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        arrow.transform.rotation = Quaternion.Euler(0, 0, angle);

        var rb = arrow.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0;
            rb.linearVelocity = dir * arrowSpeed;
        }

        // 设置伤害
        var proj = arrow.GetComponent<EnemyProjectile>();
        // EnemyProjectile已有碰撞伤害逻辑

        // 生命周期
        Destroy(arrow, 5f);

        // 效果
        if (trapAnimator != null)
            trapAnimator.SetTrigger("Fire");

        if (fireParticles != null)
            fireParticles.Play();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(fireSound);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Vector3 start = firePoint != null ? firePoint.position : transform.position;
        Gizmos.DrawRay(start, (Vector3)(fireDirection.normalized * 3f));
        Gizmos.DrawSphere(start, 0.1f);

        if (triggerMode == TriggerMode.PlayerDetect)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.2f);
            Gizmos.DrawRay(start, (Vector3)(fireDirection.normalized * detectionRange));
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, "Arrow Trap");
#endif
    }
}
