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
        // 这一击落空时把原因记下来。近战有三个静默出口(没目标/够不着/目标身上
        // 没有 PlayerHealth),从外面只看得到"血没掉",分不清是哪一个。
        if (currentTarget == null) { LastAttackResult = "no target"; return; }

        float dist = Vector2.Distance(transform.position, currentTarget.position);
        if (dist > attackRange)
        {
            LastAttackResult = $"out of range (dist={dist:F2} > {attackRange:F2})";
            return;
        }

        var health = currentTarget.GetComponent<PlayerHealth>();
        if (health == null)
        {
            LastAttackResult = $"'{currentTarget.name}' has no PlayerHealth";
            return;
        }

        Vector2 knockback = (currentTarget.position - transform.position).normalized;
        int before = health.CurrentHealth;
        health.TakeDamage(damage, knockback);
        LastAttackResult = health.CurrentHealth < before
            ? $"hit for {before - health.CurrentHealth}"
            : $"absorbed (dmg={damage}, alive={health.IsAlive}, invincible={health.IsInvincible})";
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
        // 三个静默出口。实测场景里的 ShadowArcher 两个引用都是空的 ——
        // 它进 Attack 状态、出手计数照加,却永远射不出任何东西,也不报错。
        if (currentTarget == null) { LastAttackResult = "no target"; return; }
        if (projectilePrefab == null) { LastAttackResult = "no projectilePrefab wired"; return; }
        if (firePoint == null) { LastAttackResult = "no firePoint wired"; return; }

        // 发射点要随朝向镜像。它是个固定在 X 正方向的子物件,朝向翻转只翻 sprite,
        // 不动子物件 —— 目标在左边时,弹丸会先从射手身后生成再倒穿过它自己,
        // 贴脸距离下第一个物理步就可能直接越过目标。
        Vector3 offset = firePoint.localPosition;
        if (currentTarget.position.x < transform.position.x) offset.x = -offset.x;
        Vector3 spawnPos = transform.position + offset;

        Vector2 dir = ((Vector2)currentTarget.position - (Vector2)spawnPos).normalized;

        var proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        var rb2d = proj.GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            rb2d.velocity = dir * projectileSpeed;
            // 必须开连续检测。离散检测下一步走 0.2 格,而弹丸半径只有 0.2、
            // 目标也就半格宽 —— 批处理里帧长时会直接跨过玩家,弹丸照飞、
            // OnTriggerEnter2D 一次都不触发。玩家那边的光弹早就为同一个问题
            // 加过扫掠检测,敌人这边一直没加。
            rb2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // 旋转投射物朝向
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        proj.transform.rotation = Quaternion.Euler(0, 0, angle);

        Destroy(proj, 5f);
        LastAttackResult = "fired";
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
                rb.velocity = dir * patrolSpeed;
                FaceDirection(dir.x);
            }
        }
        else
        {
            // 原地悬浮
            Vector2 hoverPos = spawnPosition + Vector2.up * (hoverHeight + hoverOffset);
            Vector2 dir = (hoverPos - (Vector2)transform.position).normalized;
            rb.velocity = dir * patrolSpeed * 0.5f;
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
            rb.velocity = dir * chaseSpeed;
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
            rb.velocity = dir * diveBombSpeed;

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

    private Vector2 lastPosition;
    private bool spent;

    void Awake() => lastPosition = transform.position;

    /// <summary>
    /// 沿本帧位移扫一遍。只靠 OnTriggerEnter2D 是不够的: 批处理里一帧可长达
    /// 0.2-0.3 秒,弹丸一帧能飞两三格,而它半径只有 0.2、玩家也就半格宽 ——
    /// 直接跨过去,弹丸照飞,触发器一次都不响。玩家那边的光弹早就为同一个问题
    /// 加过扫掠,敌人这边一直没有,表现就是"射手开火了但永远打不中"。
    ///
    /// 只对身上找得到 PlayerHealth 的碰撞体结算,所以不会像当初玩家光弹那样
    /// 被自己人或地形吃掉(那次是 RaycastAll 命中一切、绕过了层碰撞矩阵)。
    /// </summary>
    void Update()
    {
        if (spent) return;

        Vector2 now = transform.position;
        Vector2 delta = now - lastPosition;
        float dist = delta.magnitude;
        lastPosition = now;
        if (dist <= 0.0001f) return;

        foreach (var hit in Physics2D.RaycastAll(lastPosition - delta, delta / dist, dist))
        {
            if (hit.collider == null) continue;
            var health = hit.collider.GetComponentInParent<PlayerHealth>();
            if (health == null) continue;

            ApplyHit(health, hit.point);
            return;
        }
    }

    private void ApplyHit(PlayerHealth health, Vector2 point)
    {
        if (spent) return;
        spent = true;

        Vector2 knockback = ((Vector2)health.transform.position - point).normalized;
        health.TakeDamage(damage, knockback);
        if (hitEffect != null) Instantiate(hitEffect, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 按组件找玩家,不靠标签,也不假设碰撞体就挂在玩家根物件上。
        // 原来是 CompareTag("Player") + other.GetComponent<PlayerHealth>():
        // 标签没配好、或者碰撞体在子物件上(脚底检测、受击盒),都会让这一发
        // 静默穿过去 —— 不掉血、不报错、弹丸继续飞。
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            ApplyHit(health, transform.position);
            return;
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
