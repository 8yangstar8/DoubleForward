using UnityEngine;
using System.Collections;

/// <summary>
/// 敌人基类 - 提供通用AI行为：巡逻、追击、攻击、受伤、死亡
/// 所有敌人类型继承此类并扩展特定行为
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    public enum EnemyState
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Hurt,
        Dead
    }

    [Header("基础属性")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float moveSpeed = 3f;
    [SerializeField] protected float damage = 20f;
    [SerializeField] protected float attackRange = 1.5f;
    [SerializeField] protected float detectionRange = 8f;
    [SerializeField] protected float attackCooldown = 1.5f;

    [Header("巡逻")]
    [SerializeField] protected Transform[] patrolPoints;
    [SerializeField] protected float patrolWaitTime = 1f;
    [SerializeField] protected float patrolSpeed = 2f;

    [Header("追击")]
    [SerializeField] protected float chaseSpeed = 4f;
    [SerializeField] protected float maxChaseDistance = 15f;
    [SerializeField] protected float loseTargetTime = 3f;

    [Header("视觉反馈")]
    [SerializeField] protected SpriteRenderer spriteRenderer;
    [SerializeField] protected Color hurtFlashColor = Color.red;
    [SerializeField] protected float hurtFlashDuration = 0.1f;

    [Header("掉落")]
    [SerializeField] protected GameObject[] dropItems;
    [SerializeField] [Range(0f, 1f)] protected float dropChance = 0.3f;

    [Header("得分")]
    [SerializeField] protected int scoreValue = 100;
    [SerializeField] protected string enemyTypeName = "";

    // 运行时状态
    protected EnemyState currentState = EnemyState.Idle;
    protected float currentHealth;
    protected Rigidbody2D rb;
    protected Animator animator;
    protected Transform currentTarget;
    protected float attackTimer;
    protected int currentPatrolIndex;
    protected float patrolWaitTimer;
    protected float loseTargetTimer;
    protected bool isFacingRight = true;
    protected bool isInvincible;
    protected Vector2 spawnPosition;

    // 属性
    public float CurrentHealth => currentHealth;
    public float HealthPercent => currentHealth / maxHealth;
    public EnemyState State => currentState;
    public bool IsDead => currentState == EnemyState.Dead;

    // IDamageable implementation
    bool IDamageable.IsAlive => !IsDead;
    void IDamageable.TakeDamage(int damage) => TakeDamage(damage);

    public event System.Action<float> OnDamaged;      // 受到的伤害
    public event System.Action OnDeath;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        currentHealth = maxHealth;
        spawnPosition = transform.position;
    }

    protected virtual void Update()
    {
        if (currentState == EnemyState.Dead) return;

        attackTimer -= Time.deltaTime;

        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdle();
                break;
            case EnemyState.Patrol:
                UpdatePatrol();
                break;
            case EnemyState.Chase:
                UpdateChase();
                break;
            case EnemyState.Attack:
                UpdateAttack();
                break;
        }

        // 持续检测玩家
        if (currentState != EnemyState.Hurt && currentState != EnemyState.Attack)
            DetectPlayer();

        UpdateAnimator();
    }

    // ============ 状态更新 ============

    protected virtual void UpdateIdle()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
            SetState(EnemyState.Patrol);
    }

    protected virtual void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        Transform target = patrolPoints[currentPatrolIndex];
        if (target == null) return;

        float dist = Vector2.Distance(transform.position, target.position);

        if (dist < 0.3f)
        {
            // 到达巡逻点
            patrolWaitTimer += Time.deltaTime;
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

            if (patrolWaitTimer >= patrolWaitTime)
            {
                patrolWaitTimer = 0;
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            }
        }
        else
        {
            // 移向巡逻点
            Vector2 dir = ((Vector2)target.position - (Vector2)transform.position).normalized;
            rb.linearVelocity = new Vector2(dir.x * patrolSpeed, rb.linearVelocity.y);
            FaceDirection(dir.x);
        }
    }

    protected virtual void UpdateChase()
    {
        if (currentTarget == null)
        {
            SetState(EnemyState.Patrol);
            return;
        }

        float dist = Vector2.Distance(transform.position, currentTarget.position);

        // 超出最大追击距离
        if (dist > maxChaseDistance)
        {
            loseTargetTimer += Time.deltaTime;
            if (loseTargetTimer >= loseTargetTime)
            {
                currentTarget = null;
                SetState(EnemyState.Patrol);
                return;
            }
        }
        else
        {
            loseTargetTimer = 0;
        }

        // 到达攻击范围
        if (dist <= attackRange)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            if (attackTimer <= 0)
                SetState(EnemyState.Attack);
        }
        else
        {
            // 追击
            Vector2 dir = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
            rb.linearVelocity = new Vector2(dir.x * chaseSpeed, rb.linearVelocity.y);
            FaceDirection(dir.x);
        }
    }

    protected virtual void UpdateAttack()
    {
        if (attackTimer > 0) return;

        PerformAttack();
        attackTimer = attackCooldown;
        SetState(EnemyState.Chase);
    }

    /// <summary>
    /// 子类重写以实现具体攻击逻辑
    /// </summary>
    protected abstract void PerformAttack();

    // ============ 检测 ============

    protected virtual void DetectPlayer()
    {
        // 查找范围内最近的玩家
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange, LayerMask.GetMask("Player"));

        Transform nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = hit.transform;
            }
        }

        if (nearest != null && currentState != EnemyState.Chase && currentState != EnemyState.Attack)
        {
            currentTarget = nearest;
            loseTargetTimer = 0;
            SetState(EnemyState.Chase);
        }
    }

    // ============ 伤害 ============

    public virtual void TakeDamage(float amount, Vector2 knockbackDir = default)
    {
        if (isInvincible || currentState == EnemyState.Dead) return;

        currentHealth -= amount;
        OnDamaged?.Invoke(amount);

        // 击退
        if (knockbackDir != default && rb != null)
            rb.AddForce(knockbackDir.normalized * 5f, ForceMode2D.Impulse);

        // 连击计数
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.AddComboHit("enemy_hit", 150);

        // 特效
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.EnemyHit, transform.position);

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayHurt();

        // 发布受击事件（用于FloatingDamageText等UI）
        EventBus.Publish(new EnemyHitEvent
        {
            playerIndex = -1,
            damage = Mathf.CeilToInt(amount),
            position = transform.position
        });

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(HurtFlash());
        }
    }

    protected virtual void Die()
    {
        SetState(EnemyState.Dead);
        OnDeath?.Invoke();

        // 掉落物品
        SpawnDrops();

        // 特效
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.EnemyDeath, transform.position);

        // 屏幕震动
        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeLight();

        // 发布击败事件
        string typeName = !string.IsNullOrEmpty(enemyTypeName) ? enemyTypeName : GetType().Name;
        EventBus.Publish(new EnemyDefeatedEvent
        {
            enemyType = typeName,
            position = transform.position,
            scoreValue = scoreValue
        });

        // 加分
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(scoreValue, "enemy_defeat");

        // 触觉反馈
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Medium();

        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;

        // 播放死亡动画后销毁
        if (animator != null)
            animator.SetTrigger("Death");

        Destroy(gameObject, 2f);
    }

    protected IEnumerator HurtFlash()
    {
        SetState(EnemyState.Hurt);
        isInvincible = true;

        if (spriteRenderer != null)
        {
            Color original = spriteRenderer.color;
            spriteRenderer.color = hurtFlashColor;
            yield return new WaitForSeconds(hurtFlashDuration);
            spriteRenderer.color = original;
        }
        else
        {
            yield return new WaitForSeconds(hurtFlashDuration);
        }

        isInvincible = false;

        if (currentTarget != null)
            SetState(EnemyState.Chase);
        else
            SetState(EnemyState.Patrol);
    }

    protected void SpawnDrops()
    {
        if (dropItems == null || dropItems.Length == 0) return;
        if (Random.value > dropChance) return;

        int index = Random.Range(0, dropItems.Length);
        if (dropItems[index] != null)
        {
            Instantiate(dropItems[index], transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }
    }

    // ============ 工具方法 ============

    protected void SetState(EnemyState newState)
    {
        currentState = newState;
    }

    protected void FaceDirection(float xDir)
    {
        if ((xDir > 0 && !isFacingRight) || (xDir < 0 && isFacingRight))
        {
            isFacingRight = !isFacingRight;
            Vector3 scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
        }
    }

    protected virtual void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
        animator.SetBool("IsChasing", currentState == EnemyState.Chase);
        animator.SetBool("IsAttacking", currentState == EnemyState.Attack);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
