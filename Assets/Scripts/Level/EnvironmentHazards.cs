using UnityEngine;
using System.Collections;

/// <summary>
/// 环境危险物合集 - 各种关卡中的陷阱和危险区域
/// 丰富关卡设计的基础危险元素
/// </summary>

// ============ 尖刺陷阱 ============
/// <summary>
/// 定时弹出的尖刺 - 周期性出现/隐藏
/// </summary>
public class SpikeTrap : MonoBehaviour
{
    [Header("时间设置")]
    [SerializeField] private float activeTime = 1.5f;
    [SerializeField] private float inactiveTime = 2f;
    [SerializeField] private float initialDelay = 0f;

    [Header("伤害")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackForce = 8f;

    [Header("视觉")]
    [SerializeField] private Transform spikeTransform;
    [SerializeField] private float raiseSpeed = 10f;
    [SerializeField] private float lowerSpeed = 5f;
    [SerializeField] private float raiseHeight = 0.8f;

    [Header("提示")]
    [SerializeField] private float warningTime = 0.5f;
    [SerializeField] private SpriteRenderer warningIndicator;

    private bool isActive;
    private bool isRaised;
    private float timer;
    private float spikeY;
    private float baseY;
    private Collider2D spikeCollider;

    void Start()
    {
        spikeCollider = GetComponent<Collider2D>();
        if (spikeCollider != null) spikeCollider.enabled = false;

        if (spikeTransform != null)
            baseY = spikeTransform.localPosition.y;

        spikeY = baseY;
        timer = -initialDelay;

        if (warningIndicator != null)
            warningIndicator.enabled = false;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (!isActive)
        {
            // 等待不活跃期
            if (timer >= inactiveTime)
            {
                isActive = true;
                timer = 0f;
            }
            // 预警
            else if (timer >= inactiveTime - warningTime && warningIndicator != null)
            {
                warningIndicator.enabled = true;
                float flash = Mathf.PingPong(Time.time * 10f, 1f);
                warningIndicator.color = new Color(1f, 0.3f, 0f, flash);
            }

            // 收回尖刺
            if (isRaised)
            {
                spikeY = Mathf.MoveTowards(spikeY, baseY, lowerSpeed * Time.deltaTime);
                if (Mathf.Approximately(spikeY, baseY))
                {
                    isRaised = false;
                    if (spikeCollider != null) spikeCollider.enabled = false;
                }
            }
        }
        else
        {
            // 活跃期 - 弹出尖刺
            if (!isRaised)
            {
                spikeY = Mathf.MoveTowards(spikeY, baseY + raiseHeight, raiseSpeed * Time.deltaTime);
                if (Mathf.Approximately(spikeY, baseY + raiseHeight))
                {
                    isRaised = true;
                    if (spikeCollider != null) spikeCollider.enabled = true;
                    if (warningIndicator != null) warningIndicator.enabled = false;
                }
            }

            if (timer >= activeTime)
            {
                isActive = false;
                timer = 0f;
            }
        }

        // 更新位置
        if (spikeTransform != null)
        {
            Vector3 pos = spikeTransform.localPosition;
            pos.y = spikeY;
            spikeTransform.localPosition = pos;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isRaised) return;

        var health = other.GetComponent<PlayerHealth>();
        if (health == null) return;

        Vector2 knockback = (other.transform.position - transform.position).normalized * knockbackForce;
        health.TakeDamage(damage, knockback);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("spike_hit");

        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Medium();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isActive ? Color.red : Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * raiseHeight * 0.5f,
            new Vector3(1f, raiseHeight, 0));
    }
}

// ============ 激光栅栏 ============
/// <summary>
/// 激光障碍 - 周期性开关的激光线
/// 可由开关控制
/// </summary>
public class LaserBarrier : MonoBehaviour
{
    [Header("激光设置")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private float onDuration = 2f;
    [SerializeField] private float offDuration = 2f;
    [SerializeField] private float initialDelay = 0f;

    [Header("伤害")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float damageCooldown = 0.5f;

    [Header("视觉")]
    [SerializeField] private LineRenderer laserLine;
    [SerializeField] private Color laserColor = Color.red;
    [SerializeField] private float laserWidth = 0.1f;
    [SerializeField] private ParticleSystem startParticle;
    [SerializeField] private ParticleSystem endParticle;

    private bool isOn = true;
    private float timer;
    private float damageCooldownTimer;
    private bool controlledBySwitch;

    void Start()
    {
        timer = -initialDelay;

        if (laserLine != null)
        {
            laserLine.startWidth = laserWidth;
            laserLine.endWidth = laserWidth;
            laserLine.startColor = laserColor;
            laserLine.endColor = laserColor;
        }

        UpdateLaserVisual();
    }

    void Update()
    {
        if (!controlledBySwitch)
        {
            timer += Time.deltaTime;

            if (isOn && timer >= onDuration)
            {
                isOn = false;
                timer = 0f;
                UpdateLaserVisual();
            }
            else if (!isOn && timer >= offDuration)
            {
                isOn = true;
                timer = 0f;
                UpdateLaserVisual();
            }
        }

        if (damageCooldownTimer > 0)
            damageCooldownTimer -= Time.deltaTime;

        // 更新激光线位置
        if (isOn && laserLine != null && startPoint != null && endPoint != null)
        {
            laserLine.SetPosition(0, startPoint.position);
            laserLine.SetPosition(1, endPoint.position);

            // 检测激光线上的碰撞
            CheckLaserHit();
        }
    }

    private void CheckLaserHit()
    {
        if (startPoint == null || endPoint == null) return;
        if (damageCooldownTimer > 0) return;

        Vector2 dir = (endPoint.position - startPoint.position);
        float dist = dir.magnitude;
        dir.Normalize();

        var hit = Physics2D.Raycast(startPoint.position, dir, dist, LayerMask.GetMask("Player"));
        if (hit.collider != null)
        {
            var health = hit.collider.GetComponent<PlayerHealth>();
            if (health != null)
            {
                Vector2 knockback = hit.normal * -5f;
                health.TakeDamage(damage, knockback);
                damageCooldownTimer = damageCooldown;
            }
        }
    }

    /// <summary>
    /// 由开关控制开/关
    /// </summary>
    public void SetControlledState(bool on)
    {
        controlledBySwitch = true;
        isOn = on;
        UpdateLaserVisual();
    }

    private void UpdateLaserVisual()
    {
        if (laserLine != null)
            laserLine.enabled = isOn;

        if (startParticle != null)
        {
            if (isOn) startParticle.Play();
            else startParticle.Stop();
        }

        if (endParticle != null)
        {
            if (isOn) endParticle.Play();
            else endParticle.Stop();
        }
    }

    void OnDrawGizmos()
    {
        if (startPoint == null || endPoint == null) return;

        Gizmos.color = isOn ? Color.red : new Color(1, 0, 0, 0.2f);
        Gizmos.DrawLine(startPoint.position, endPoint.position);
        Gizmos.DrawSphere(startPoint.position, 0.1f);
        Gizmos.DrawSphere(endPoint.position, 0.1f);
    }
}

// ============ 落石/天花板掉落 ============
/// <summary>
/// 触发式落石陷阱 - 玩家经过时触发石头掉落
/// </summary>
public class FallingRock : MonoBehaviour
{
    [Header("设置")]
    [SerializeField] private float triggerDelay = 0.5f;    // 触发后延迟掉落
    [SerializeField] private float fallSpeed = 15f;
    [SerializeField] private int damage = 1;
    [SerializeField] private bool destroyOnImpact = true;
    [SerializeField] private bool respawnable = true;
    [SerializeField] private float respawnTime = 5f;

    [Header("检测")]
    [SerializeField] private float triggerWidth = 2f;      // 触发区域宽度
    [SerializeField] private LayerMask playerLayer;

    [Header("视觉")]
    [SerializeField] private float shakeBeforeFall = 0.3f;
    [SerializeField] private GameObject impactVFX;
    [SerializeField] private SpriteRenderer warningSprite;

    private Vector3 originalPosition;
    private bool hasTriggered;
    private bool isFalling;
    private Rigidbody2D rb;
    private Collider2D col;

    void Start()
    {
        originalPosition = transform.position;
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0;
        }

        if (warningSprite != null)
            warningSprite.enabled = false;
    }

    void Update()
    {
        if (hasTriggered || isFalling) return;

        // 检测下方玩家
        var hit = Physics2D.OverlapBox(
            transform.position + Vector3.down * 5f,
            new Vector2(triggerWidth, 10f),
            0f, playerLayer);

        if (hit != null)
        {
            hasTriggered = true;
            StartCoroutine(TriggerFall());
        }
    }

    private IEnumerator TriggerFall()
    {
        // 显示预警
        if (warningSprite != null)
            warningSprite.enabled = true;

        // 震动预警
        float elapsed = 0f;
        Vector3 basePos = transform.position;
        while (elapsed < shakeBeforeFall)
        {
            elapsed += Time.deltaTime;
            float shakeX = Random.Range(-0.05f, 0.05f);
            transform.position = basePos + new Vector3(shakeX, 0, 0);
            yield return null;
        }

        // 等待延迟
        yield return new WaitForSeconds(Mathf.Max(0, triggerDelay - shakeBeforeFall));

        if (warningSprite != null)
            warningSprite.enabled = false;

        // 开始掉落
        isFalling = true;
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = fallSpeed / 9.81f;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isFalling) return;

        // 伤害玩家
        var health = collision.collider.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.TakeDamage(damage, Vector2.down * 3f);
        }

        // 撞击效果
        if (impactVFX != null)
            Instantiate(impactVFX, transform.position, Quaternion.identity);

        if (CameraShake.Instance != null)
            CameraShake.Instance.ShakeLight();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("rock_impact");

        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Medium();

        if (destroyOnImpact)
        {
            if (respawnable)
                StartCoroutine(RespawnAfterDelay());
            else
                Destroy(gameObject);
        }
        else
        {
            isFalling = false;
            if (rb != null)
                rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private IEnumerator RespawnAfterDelay()
    {
        // 隐藏
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
        if (col != null) col.enabled = false;
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        yield return new WaitForSeconds(respawnTime);

        // 重置
        transform.position = originalPosition;
        if (sr != null) sr.enabled = true;
        if (col != null) col.enabled = true;
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        hasTriggered = false;
        isFalling = false;
    }

    void OnDrawGizmosSelected()
    {
        // 触发区域
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawCube(transform.position + Vector3.down * 5f,
            new Vector3(triggerWidth, 10f, 0));

        // 掉落路径
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 10f);
    }
}

// ============ 旋转锯刃 ============
/// <summary>
/// 沿轨道移动的旋转锯刃
/// </summary>
public class SawBlade : MonoBehaviour
{
    [Header("移动")]
    [SerializeField] private Transform[] pathPoints;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private bool pingPong = true;

    [Header("旋转")]
    [SerializeField] private float rotateSpeed = 360f;

    [Header("伤害")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float damageCooldown = 0.8f;

    private int currentPointIndex;
    private int direction = 1;
    private float damageCooldownTimer;

    void Update()
    {
        // 旋转
        transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);

        // 沿路径移动
        if (pathPoints == null || pathPoints.Length < 2) return;

        Transform target = pathPoints[currentPointIndex];
        if (target == null) return;

        transform.position = Vector3.MoveTowards(
            transform.position, target.position, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.position) < 0.1f)
        {
            if (pingPong)
            {
                currentPointIndex += direction;
                if (currentPointIndex >= pathPoints.Length || currentPointIndex < 0)
                {
                    direction = -direction;
                    currentPointIndex += direction * 2;
                }
            }
            else
            {
                currentPointIndex = (currentPointIndex + 1) % pathPoints.Length;
            }
        }

        if (damageCooldownTimer > 0)
            damageCooldownTimer -= Time.deltaTime;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (damageCooldownTimer > 0) return;

        var health = other.GetComponent<PlayerHealth>();
        if (health == null) return;

        Vector2 knockback = (other.transform.position - transform.position).normalized * knockbackForce;
        health.TakeDamage(damage, knockback);
        damageCooldownTimer = damageCooldown;

        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Heavy();
    }

    void OnDrawGizmos()
    {
        if (pathPoints == null || pathPoints.Length < 2) return;

        Gizmos.color = Color.red;
        for (int i = 0; i < pathPoints.Length - 1; i++)
        {
            if (pathPoints[i] != null && pathPoints[i + 1] != null)
                Gizmos.DrawLine(pathPoints[i].position, pathPoints[i + 1].position);
        }

        if (!pingPong && pathPoints[0] != null && pathPoints[pathPoints.Length - 1] != null)
            Gizmos.DrawLine(pathPoints[pathPoints.Length - 1].position, pathPoints[0].position);

        foreach (var point in pathPoints)
        {
            if (point != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(point.position, 0.15f);
            }
        }
    }
}
