using UnityEngine;

/// <summary>
/// 各种敌人类型实现
/// </summary>

// ============ 暗影史莱姆 - 基础近战敌人 ============
public class ShadowSlime : EnemyBase
{
    [Header("史莱姆特性")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float jumpInterval = 2f;
    private float jumpTimer;

    protected override void Update()
    {
        base.Update();

        // 跳跃移动
        if (currentState == EnemyState.Chase || currentState == EnemyState.Patrol)
        {
            jumpTimer += Time.deltaTime;
            if (jumpTimer >= jumpInterval)
            {
                jumpTimer = 0;
                rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            }
        }
    }

    protected override void PerformAttack()
    {
        if (currentTarget == null) return;

        float dist = Vector2.Distance(transform.position, currentTarget.position);
        if (dist <= attackRange)
        {
            var health = currentTarget.GetComponent<PlayerHealth>();
            if (health != null)
            {
                Vector2 knockback = (currentTarget.position - transform.position).normalized;
                health.TakeDamage(damage, knockback);
            }
        }
    }
}

// ============ 暗影射手 - 远程投射敌人 ============
public class ShadowArcher : EnemyBase
{
    [Header("射手特性")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 10f;

    protected override void PerformAttack()
    {
        if (currentTarget == null || projectilePrefab == null || firePoint == null) return;

        Vector2 dir = ((Vector2)currentTarget.position - (Vector2)firePoint.position).normalized;

        var proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        var rb2d = proj.GetComponent<Rigidbody2D>();
        if (rb2d != null)
            rb2d.linearVelocity = dir * projectileSpeed;

        // 旋转投射物朝向
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        proj.transform.rotation = Quaternion.Euler(0, 0, angle);

        Destroy(proj, 5f);
    }
}

// ============ 暗影守卫 - 盾牌防御敌人 ============
public class ShadowGuard : EnemyBase
{
    [Header("守卫特性")]
    [SerializeField] private float blockChance = 0.4f;
    [SerializeField] private float shieldBashRange = 2f;
    [SerializeField] private float shieldBashForce = 10f;
    [SerializeField] private GameObject shieldEffect;

    private bool isBlocking;

    public override void TakeDamage(float amount, Vector2 knockbackDir = default)
    {
        // 有概率格挡
        if (Random.value < blockChance && currentState != EnemyState.Hurt)
        {
            isBlocking = true;
            if (shieldEffect != null)
                shieldEffect.SetActive(true);

            // 格挡只受一半伤害
            base.TakeDamage(amount * 0.5f, knockbackDir * 0.3f);

            Invoke(nameof(EndBlock), 0.5f);
            return;
        }

        base.TakeDamage(amount, knockbackDir);
    }

    private void EndBlock()
    {
        isBlocking = false;
        if (shieldEffect != null)
            shieldEffect.SetActive(false);
    }

    protected override void PerformAttack()
    {
        if (currentTarget == null) return;

        float dist = Vector2.Distance(transform.position, currentTarget.position);
        if (dist <= shieldBashRange)
        {
            var health = currentTarget.GetComponent<PlayerHealth>();
            if (health != null)
            {
                Vector2 knockback = (currentTarget.position - transform.position).normalized * shieldBashForce;
                health.TakeDamage(damage * 1.5f, knockback);
            }

            if (VFXManager.Instance != null)
                VFXManager.Instance.ShakeMedium();
        }
    }
}

// ============ 暗影飞虫 - 飞行追踪敌人 ============
public class ShadowFlyer : EnemyBase
{
    [Header("飞虫特性")]
    [SerializeField] private float hoverHeight = 3f;
    [SerializeField] private float hoverAmplitude = 0.5f;
    [SerializeField] private float hoverFrequency = 2f;
    [SerializeField] private float diveBombSpeed = 12f;
    [SerializeField] private float diveBombCooldown = 4f;

    private float hoverTimer;
    private float diveBombTimer;
    private bool isDiveBombing;
    private Vector2 diveBombTarget;

    protected override void Awake()
    {
        base.Awake();
        rb.gravityScale = 0; // 飞行敌人无重力
    }

    protected override void UpdatePatrol()
    {
        // 悬浮运动
        hoverTimer += Time.deltaTime;
        float hoverOffset = Mathf.Sin(hoverTimer * hoverFrequency) * hoverAmplitude;

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            Transform target = patrolPoints[currentPatrolIndex];
            Vector2 targetPos = (Vector2)target.position + Vector2.up * (hoverHeight + hoverOffset);
            float dist = Vector2.Distance(transform.position, targetPos);

            if (dist < 0.5f)
            {
                patrolWaitTimer += Time.deltaTime;
                if (patrolWaitTimer >= patrolWaitTime)
                {
                    patrolWaitTimer = 0;
                    currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                }
            }
            else
            {
                Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
                rb.linearVelocity = dir * patrolSpeed;
                FaceDirection(dir.x);
            }
        }
        else
        {
            // 原地悬浮
            Vector2 hoverPos = spawnPosition + Vector2.up * (hoverHeight + hoverOffset);
            Vector2 dir = (hoverPos - (Vector2)transform.position).normalized;
            rb.linearVelocity = dir * patrolSpeed * 0.5f;
        }
    }

    protected override void UpdateChase()
    {
        if (currentTarget == null)
        {
            SetState(EnemyState.Patrol);
            return;
        }

        diveBombTimer += Time.deltaTime;

        if (!isDiveBombing)
        {
            // 盘旋在目标上方
            Vector2 hoverPos = (Vector2)currentTarget.position + Vector2.up * hoverHeight;
            float hoverOffset = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
            hoverPos.y += hoverOffset;

            Vector2 dir = (hoverPos - (Vector2)transform.position).normalized;
            rb.linearVelocity = dir * chaseSpeed;
            FaceDirection(dir.x);

            // 俯冲攻击
            if (diveBombTimer >= diveBombCooldown)
            {
                diveBombTimer = 0;
                isDiveBombing = true;
                diveBombTarget = currentTarget.position;
            }
        }
        else
        {
            // 俯冲中
            Vector2 dir = (diveBombTarget - (Vector2)transform.position).normalized;
            rb.linearVelocity = dir * diveBombSpeed;

            if (Vector2.Distance(transform.position, diveBombTarget) < 0.5f)
            {
                isDiveBombing = false;
                PerformAttack();
            }
        }
    }

    protected override void PerformAttack()
    {
        if (currentTarget == null) return;

        float dist = Vector2.Distance(transform.position, currentTarget.position);
        if (dist <= attackRange * 2f)
        {
            var health = currentTarget.GetComponent<PlayerHealth>();
            if (health != null)
            {
                Vector2 knockback = (currentTarget.position - transform.position).normalized;
                health.TakeDamage(damage, knockback);
            }
        }

        isDiveBombing = false;
    }
}

// ============ 投射物伤害组件 ============
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float damage = 15f;
    [SerializeField] private GameObject hitEffect;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            var health = other.GetComponent<PlayerHealth>();
            if (health != null)
            {
                Vector2 knockback = (other.transform.position - transform.position).normalized;
                health.TakeDamage(damage, knockback);
            }

            if (hitEffect != null)
                Instantiate(hitEffect, transform.position, Quaternion.identity);

            Destroy(gameObject);
        }

        // 碰到墙壁也销毁
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            if (hitEffect != null)
                Instantiate(hitEffect, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
