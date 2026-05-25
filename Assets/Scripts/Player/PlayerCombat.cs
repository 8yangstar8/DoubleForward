using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 玩家战斗系统 - 近战攻击、远程攻击、伤害计算、连招
/// Lux偏向远程光弹攻击，Nox偏向近战暗影斩击
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("基础设置")]
    [SerializeField] private int baseDamage = 1;
    [SerializeField] private float attackCooldown = 0.3f;
    [SerializeField] private float comboResetTime = 0.8f;

    [Header("近战")]
    [SerializeField] private Transform meleeAttackPoint;
    [SerializeField] private float meleeRange = 1.2f;
    [SerializeField] private float meleeArcAngle = 120f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("远程")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float projectileCooldown = 0.5f;

    [Header("击退")]
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float knockbackDuration = 0.15f;

    [Header("蓄力攻击")]
    [SerializeField] private float chargeTimeRequired = 1.0f;
    [SerializeField] private float chargedDamageMultiplier = 3f;
    [SerializeField] private float chargedRangeMultiplier = 1.5f;

    [Header("空中攻击")]
    [SerializeField] private float airAttackDownForce = 8f;
    [SerializeField] private float airAttackRadius = 1.5f;

    // 内部状态
    private PlayerController controller;
    private PlayerAnimator animator;
    private PlayerHealth health;
    private float attackTimer;
    private float projectileTimer;
    private int comboStep;
    private float comboTimer;
    private bool isCharging;
    private float chargeTimer;
    private bool isAttacking;

    // 连招定义
    private static readonly int MAX_COMBO = 3;
    private static readonly float[] comboDamageMultipliers = { 1f, 1.2f, 1.8f };
    private static readonly float[] comboRangeMultipliers = { 1f, 1.1f, 1.3f };

    // 事件
    public event System.Action<int> OnComboStep;          // comboStep
    public event System.Action<float> OnChargeProgress;   // 0~1
    public event System.Action OnAttackPerformed;
    public event System.Action OnChargedAttackReleased;

    // 已命中追踪（防止同一次攻击多次伤害）
    private HashSet<int> hitThisSwing = new HashSet<int>();

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        animator = GetComponent<PlayerAnimator>();
        health = GetComponent<PlayerHealth>();
    }

    void Update()
    {
        UpdateTimers();
        UpdateComboReset();
        UpdateCharge();
    }

    private void UpdateTimers()
    {
        if (attackTimer > 0) attackTimer -= Time.deltaTime;
        if (projectileTimer > 0) projectileTimer -= Time.deltaTime;
    }

    private void UpdateComboReset()
    {
        if (comboStep > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0)
            {
                comboStep = 0;
            }
        }
    }

    private void UpdateCharge()
    {
        if (isCharging)
        {
            chargeTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(chargeTimer / chargeTimeRequired);
            OnChargeProgress?.Invoke(progress);
        }
    }

    // ==================== 公共攻击接口 ====================

    /// <summary>
    /// 执行近战攻击（连招系统）
    /// </summary>
    public void MeleeAttack()
    {
        if (attackTimer > 0 || isAttacking || !health.IsAlive) return;

        isAttacking = true;
        attackTimer = attackCooldown;
        hitThisSwing.Clear();

        // 连招步骤
        comboStep = Mathf.Min(comboStep + 1, MAX_COMBO);
        comboTimer = comboResetTime;

        float damageMultiplier = comboDamageMultipliers[comboStep - 1];
        float rangeMultiplier = comboRangeMultipliers[comboStep - 1];

        // 播放连招动画
        if (animator != null)
            animator.PlayAttack(comboStep);

        // 检测命中
        PerformMeleeHitDetection(damageMultiplier, rangeMultiplier);

        OnComboStep?.Invoke(comboStep);
        OnAttackPerformed?.Invoke();

        // 通知ComboSystem
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.OnHit();

        // VFX
        PlayMeleeVFX(comboStep);

        StartCoroutine(EndAttackAfterDelay(0.2f));
    }

    /// <summary>
    /// 执行远程攻击（发射光弹/暗影弹）
    /// </summary>
    public void RangedAttack()
    {
        if (projectileTimer > 0 || !health.IsAlive) return;
        if (projectilePrefab == null || firePoint == null) return;

        projectileTimer = projectileCooldown;

        // 创建子弹
        GameObject projObj;
        if (ObjectPool.Instance != null)
            projObj = ObjectPool.Instance.Get(projectilePrefab, firePoint.position, firePoint.rotation);
        else
            projObj = Object.Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        var proj = projObj.GetComponent<Projectile>();
        if (proj != null)
        {
            float dir = controller != null && controller.IsFacingRight ? 1f : -1f;
            proj.Initialize(dir, projectileSpeed, baseDamage, controller.PlayerIndex);
        }

        if (animator != null)
            animator.PlayAttack(0); // 远程攻击动画

        OnAttackPerformed?.Invoke();

        // SFX
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("player_ranged");
    }

    /// <summary>
    /// 开始蓄力攻击
    /// </summary>
    public void StartCharge()
    {
        if (!health.IsAlive) return;
        isCharging = true;
        chargeTimer = 0f;
    }

    /// <summary>
    /// 释放蓄力攻击
    /// </summary>
    public void ReleaseCharge()
    {
        if (!isCharging) return;
        isCharging = false;

        float chargeRatio = Mathf.Clamp01(chargeTimer / chargeTimeRequired);

        if (chargeRatio >= 0.9f)
        {
            // 满蓄力攻击
            PerformChargedAttack();
        }
        else if (chargeRatio >= 0.3f)
        {
            // 半蓄力 - 增强普通攻击
            float bonus = Mathf.Lerp(1f, chargedDamageMultiplier * 0.6f, chargeRatio);
            PerformMeleeHitDetection(bonus, Mathf.Lerp(1f, chargedRangeMultiplier * 0.7f, chargeRatio));
        }
        else
        {
            // 蓄力时间太短 - 普通攻击
            MeleeAttack();
        }

        OnChargeProgress?.Invoke(0f);
    }

    /// <summary>
    /// 空中下砸攻击
    /// </summary>
    public void AirDownAttack()
    {
        if (controller == null || controller.IsGrounded || !health.IsAlive) return;

        // 向下施加力
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.3f, -airAttackDownForce);
        }

        StartCoroutine(AirAttackLandDetection());
    }

    // ==================== 内部逻辑 ====================

    private void PerformMeleeHitDetection(float damageMultiplier, float rangeMultiplier)
    {
        if (meleeAttackPoint == null) return;

        float effectiveRange = meleeRange * rangeMultiplier;
        var hits = Physics2D.OverlapCircleAll(meleeAttackPoint.position, effectiveRange, enemyLayer);

        foreach (var hit in hits)
        {
            int hitId = hit.GetInstanceID();
            if (hitThisSwing.Contains(hitId)) continue;
            hitThisSwing.Add(hitId);

            // 角度检查（弧形攻击）
            Vector2 toTarget = hit.transform.position - meleeAttackPoint.position;
            float facingDir = controller != null && controller.IsFacingRight ? 0f : 180f;
            float angle = Vector2.Angle(Quaternion.Euler(0, 0, facingDir) * Vector2.right, toTarget);

            if (angle > meleeArcAngle * 0.5f) continue;

            // 计算伤害
            int finalDamage = Mathf.CeilToInt(baseDamage * damageMultiplier);

            // 难度修正
            if (DifficultyManager.Instance != null)
                finalDamage = Mathf.CeilToInt(finalDamage * DifficultyManager.Instance.GetPlayerDamageMultiplier());

            // 施加伤害
            ApplyDamageToTarget(hit.gameObject, finalDamage);

            // 击退
            ApplyKnockback(hit.gameObject);

            // EventBus
            EventBus.Publish(new EnemyHitEvent
            {
                playerIndex = controller != null ? controller.PlayerIndex : 0,
                damage = finalDamage,
                position = hit.transform.position
            });
        }
    }

    private void PerformChargedAttack()
    {
        hitThisSwing.Clear();

        float effectiveRange = meleeRange * chargedRangeMultiplier;
        int chargeDamage = Mathf.CeilToInt(baseDamage * chargedDamageMultiplier);

        // 蓄力攻击是全方向的
        var hits = Physics2D.OverlapCircleAll(
            meleeAttackPoint != null ? meleeAttackPoint.position : transform.position,
            effectiveRange,
            enemyLayer);

        foreach (var hit in hits)
        {
            ApplyDamageToTarget(hit.gameObject, chargeDamage);
            ApplyKnockback(hit.gameObject, 1.5f);
        }

        // 屏幕震动
        if (CameraEffects.Instance != null)
            CameraEffects.Instance.Shake(0.3f, 0.15f);

        // 蓄力攻击VFX
        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayEffect("charged_attack",
                meleeAttackPoint != null ? meleeAttackPoint.position : transform.position);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("charged_attack");

        OnChargedAttackReleased?.Invoke();

        if (ComboSystem.Instance != null)
        {
            ComboSystem.Instance.OnHit();
            ComboSystem.Instance.OnHit(); // 蓄力攻击额外连击
        }
    }

    private void ApplyDamageToTarget(GameObject target, int damage)
    {
        // 尝试EnemyBase
        var enemy = target.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            return;
        }

        // 通用IDamageable接口（包括可破坏物等）
        var damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }
    }

    private void ApplyKnockback(GameObject target, float multiplier = 1f)
    {
        var targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;

        Vector2 knockDir = (target.transform.position - transform.position).normalized;
        knockDir.y = Mathf.Max(knockDir.y, 0.3f); // 保证有一点向上的力

        targetRb.AddForce(knockDir * knockbackForce * multiplier, ForceMode2D.Impulse);
    }

    private IEnumerator AirAttackLandDetection()
    {
        isAttacking = true;

        // 等待着地
        while (controller != null && !controller.IsGrounded)
            yield return null;

        // 着地冲击
        var hits = Physics2D.OverlapCircleAll(transform.position, airAttackRadius, enemyLayer);
        int airDamage = Mathf.CeilToInt(baseDamage * 2f);

        foreach (var hit in hits)
        {
            ApplyDamageToTarget(hit.gameObject, airDamage);
            ApplyKnockback(hit.gameObject, 1.2f);
        }

        if (CameraEffects.Instance != null)
            CameraEffects.Instance.Shake(0.2f, 0.1f);

        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayEffect("air_slam", transform.position);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("air_slam");

        isAttacking = false;
    }

    private IEnumerator EndAttackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        isAttacking = false;
    }

    private void PlayMeleeVFX(int step)
    {
        if (VFXManager.Instance == null) return;

        string effectName = step switch
        {
            1 => "melee_slash_1",
            2 => "melee_slash_2",
            3 => "melee_slash_3",
            _ => "melee_slash_1"
        };

        Vector3 pos = meleeAttackPoint != null ? meleeAttackPoint.position : transform.position;
        VFXManager.Instance.PlayEffect(effectName, pos);

        if (SoundFeedback.Instance != null)
        {
            string sfx = step >= 3 ? "melee_heavy" : "melee_light";
            SoundFeedback.Instance.Play(sfx);
        }
    }

    /// <summary>
    /// 动画事件调用 — 在攻击命中帧执行伤害检测
    /// 用于精确同步攻击动画与伤害判定
    /// </summary>
    public void ExecuteAttackHit()
    {
        hitThisSwing.Clear();
        float damageMultiplier = comboStep > 0 ? comboDamageMultipliers[comboStep - 1] : 1f;
        float rangeMultiplier = comboStep > 0 ? comboRangeMultipliers[comboStep - 1] : 1f;
        PerformMeleeHitDetection(damageMultiplier, rangeMultiplier);
    }

    // ==================== Gizmos ====================

    void OnDrawGizmosSelected()
    {
        if (meleeAttackPoint != null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
            Gizmos.DrawWireSphere(meleeAttackPoint.position, meleeRange);
        }

        if (firePoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(firePoint.position, 0.1f);
            Gizmos.DrawLine(firePoint.position,
                firePoint.position + firePoint.right * 2f);
        }
    }
}

/// <summary>
/// 子弹/投射物组件
/// </summary>
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private GameObject hitEffectPrefab;

    private float direction;
    private float speed;
    private int damage;
    private int ownerPlayerIndex;
    private float timer;

    public void Initialize(float dir, float spd, int dmg, int playerIdx)
    {
        direction = dir;
        speed = spd;
        damage = dmg;
        ownerPlayerIndex = playerIdx;
        timer = lifetime;

        // 翻转Sprite
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.flipX = dir < 0;
    }

    void Update()
    {
        transform.Translate(Vector3.right * direction * speed * Time.deltaTime);

        timer -= Time.deltaTime;
        if (timer <= 0)
            DestroyProjectile();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 检查是否命中敌人
        var enemy = other.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);

            EventBus.Publish(new EnemyHitEvent
            {
                playerIndex = ownerPlayerIndex,
                damage = damage,
                position = other.transform.position
            });

            if (ComboSystem.Instance != null)
                ComboSystem.Instance.OnHit();

            SpawnHitEffect();
            DestroyProjectile();
            return;
        }

        // 可破坏物
        var breakable = other.GetComponent<Breakable>();
        if (breakable != null)
        {
            breakable.TakeDamage(damage, "");
            SpawnHitEffect();
            DestroyProjectile();
            return;
        }

        // 撞墙
        if (((1 << other.gameObject.layer) & hitLayers) != 0)
        {
            SpawnHitEffect();
            DestroyProjectile();
        }
    }

    private void SpawnHitEffect()
    {
        if (hitEffectPrefab == null) return;

        if (ObjectPool.Instance != null)
            ObjectPool.Instance.Get(hitEffectPrefab, transform.position, Quaternion.identity);
        else
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
    }

    private void DestroyProjectile()
    {
        if (ObjectPool.Instance != null)
            ObjectPool.Instance.Return(gameObject);
        else
            Destroy(gameObject);
    }
}

// IDamageable和EnemyHitEvent定义在Core/EventBus.cs中
