using UnityEngine;
using System.Collections;

/// <summary>
/// 进阶敌人变种 - 为不同世界提供差异化的敌人行为
/// ShadowTeleporter: 瞬移型暗影敌人
/// ShadowExploder: 自爆型敌人
/// ShadowMage: 法师型敌人（召唤+范围攻击）
/// ShadowBurrower: 钻地突袭敌人
/// ShadowMimic: 伪装型敌人（伪装成收集品或道具）
/// </summary>

// ============ 暗影传送者 - 瞬移追踪敌人 ============
public class ShadowTeleporter : EnemyBase
{
    [Header("传送特性")]
    [SerializeField] private float teleportCooldown = 3f;
    [SerializeField] private float teleportRange = 6f;
    [SerializeField] private float preTeleportDelay = 0.5f;
    [SerializeField] private float postTeleportStun = 0.3f;
    [SerializeField] private GameObject teleportInEffect;
    [SerializeField] private GameObject teleportOutEffect;

    private float teleportTimer;
    private bool isTeleporting;

    protected override void Update()
    {
        base.Update();

        if (currentState == EnemyState.Chase && !isTeleporting)
        {
            teleportTimer -= Time.deltaTime;
            if (teleportTimer <= 0 && currentTarget != null)
            {
                float dist = Vector2.Distance(transform.position, currentTarget.position);
                if (dist > attackRange * 2f && dist <= teleportRange)
                {
                    StartCoroutine(TeleportSequence());
                    teleportTimer = teleportCooldown;
                }
            }
        }
    }

    private IEnumerator TeleportSequence()
    {
        isTeleporting = true;

        // 预瞬移效果（消失动画）
        if (teleportOutEffect != null)
            Instantiate(teleportOutEffect, transform.position, Quaternion.identity);

        if (spriteRenderer != null)
        {
            float elapsed = 0;
            Color origColor = spriteRenderer.color;
            while (elapsed < preTeleportDelay)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / preTeleportDelay);
                spriteRenderer.color = new Color(origColor.r, origColor.g, origColor.b, alpha);
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(preTeleportDelay);
        }

        // 瞬移到目标附近
        if (currentTarget != null)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * attackRange * 1.5f;
            Vector3 newPos = currentTarget.position + (Vector3)offset;

            // 确保不会瞬移到墙内
            var hit = Physics2D.OverlapCircle(newPos, 0.3f, LayerMask.GetMask("Ground"));
            if (hit == null)
                transform.position = newPos;
        }

        // 出现效果
        if (teleportInEffect != null)
            Instantiate(teleportInEffect, transform.position, Quaternion.identity);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("teleport");

        // 恢复可见
        if (spriteRenderer != null)
        {
            float elapsed = 0;
            Color origColor = spriteRenderer.color;
            while (elapsed < postTeleportStun)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(0f, 1f, elapsed / postTeleportStun);
                spriteRenderer.color = new Color(origColor.r, origColor.g, origColor.b, alpha);
                yield return null;
            }
            spriteRenderer.color = new Color(origColor.r, origColor.g, origColor.b, 1f);
        }
        else
        {
            yield return new WaitForSeconds(postTeleportStun);
        }

        // 面向目标
        if (currentTarget != null)
            FaceDirection(currentTarget.position.x - transform.position.x);

        isTeleporting = false;
    }

    protected override void PerformAttack()
    {
        if (currentTarget == null) return;

        float dist = Vector2.Distance(transform.position, currentTarget.position);
        if (dist <= attackRange * 1.5f)
        {
            var health = currentTarget.GetComponent<PlayerHealth>();
            if (health != null)
            {
                Vector2 knockback = (currentTarget.position - transform.position).normalized;
                health.TakeDamage(damage, knockback);
            }

            // 攻击后后退闪烁
            StartCoroutine(PostAttackBlink());
        }
    }

    private IEnumerator PostAttackBlink()
    {
        if (spriteRenderer == null) yield break;

        for (int i = 0; i < 3; i++)
        {
            spriteRenderer.enabled = false;
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.enabled = true;
            yield return new WaitForSeconds(0.05f);
        }
    }
}

// ============ 暗影自爆者 - 接近自爆 ============
public class ShadowExploder : EnemyBase
{
    [Header("自爆特性")]
    [SerializeField] private float explosionRadius = 2.5f;
    [SerializeField] private float explosionDamage = 2f;
    [SerializeField] private float fuseTime = 1.5f;
    [SerializeField] private float chaseSpeedBoost = 1.5f;
    [SerializeField] private float explosionKnockback = 12f;
    [SerializeField] private GameObject explosionEffect;

    [Header("膨胀")]
    [SerializeField] private float swellAmount = 1.3f;
    [SerializeField] private Color fuseColor = new Color(1f, 0.3f, 0f);

    private bool isFusing;
    private float fuseTimer;

    protected override void UpdateChase()
    {
        if (currentTarget == null)
        {
            SetState(EnemyState.Patrol);
            return;
        }

        float dist = Vector2.Distance(transform.position, currentTarget.position);

        // 接近后开始引燃
        if (dist <= attackRange * 2f && !isFusing)
        {
            isFusing = true;
            fuseTimer = fuseTime;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isFusing)
        {
            UpdateFuse();
            return;
        }

        // 冲刺追击（比普通追击更快）
        Vector2 dir = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
        rb.linearVelocity = new Vector2(dir.x * chaseSpeed * chaseSpeedBoost, rb.linearVelocity.y);
        FaceDirection(dir.x);
    }

    private void UpdateFuse()
    {
        fuseTimer -= Time.deltaTime;
        float progress = 1f - (fuseTimer / fuseTime);

        // 膨胀 + 变色
        float scale = Mathf.Lerp(1f, swellAmount, progress);
        transform.localScale = Vector3.one * scale * (isFacingRight ? 1 : -1);

        if (spriteRenderer != null)
        {
            float flash = Mathf.PingPong(Time.time * (5f + progress * 15f), 1f);
            spriteRenderer.color = Color.Lerp(Color.white, fuseColor, flash);
        }

        // 震动
        float shake = Mathf.Sin(Time.time * 40f) * 0.03f * progress;
        transform.position += new Vector3(shake, 0, 0);

        if (fuseTimer <= 0)
        {
            Explode();
        }
    }

    private void Explode()
    {
        // 范围伤害
        var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            // 对玩家造成伤害
            var health = hit.GetComponent<PlayerHealth>();
            if (health != null && health.IsAlive)
            {
                health.TakeDamage(Mathf.CeilToInt(explosionDamage));

                var playerRb = hit.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                {
                    Vector2 knockDir = (hit.transform.position - transform.position).normalized;
                    playerRb.AddForce(knockDir * explosionKnockback, ForceMode2D.Impulse);
                }
            }

            // 对其他敌人也造成伤害
            var enemy = hit.GetComponent<EnemyBase>();
            if (enemy != null && enemy != this && !enemy.IsDead)
            {
                enemy.TakeDamage(explosionDamage * 0.5f);
            }
        }

        // 效果
        if (explosionEffect != null)
            Instantiate(explosionEffect, transform.position, Quaternion.identity);

        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeMedium();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("explosion");

        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Heavy();

        // 自杀
        currentHealth = 0;
        Die();
    }

    protected override void PerformAttack()
    {
        // 自爆者不普通攻击，直接引燃
        if (!isFusing)
        {
            isFusing = true;
            fuseTimer = fuseTime;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}

// ============ 暗影法师 - 远程法术攻击 ============
public class ShadowMage : EnemyBase
{
    [Header("法师特性")]
    [SerializeField] private float spellCooldown = 3f;
    [SerializeField] private float spellRange = 8f;
    [SerializeField] private float spellRadius = 1.5f;
    [SerializeField] private float spellDelay = 1f;
    [SerializeField] private int spellDamage = 2;
    [SerializeField] private GameObject spellWarningPrefab;
    [SerializeField] private GameObject spellImpactPrefab;

    [Header("传送逃脱")]
    [SerializeField] private float fleeDistance = 3f;
    [SerializeField] private float fleeTeleportRange = 4f;

    private float spellTimer;

    protected override void Update()
    {
        base.Update();
        spellTimer -= Time.deltaTime;
    }

    protected override void UpdateChase()
    {
        if (currentTarget == null)
        {
            SetState(EnemyState.Patrol);
            return;
        }

        float dist = Vector2.Distance(transform.position, currentTarget.position);

        // 保持距离 — 太近则后退
        if (dist < fleeDistance)
        {
            Vector2 fleeDir = ((Vector2)transform.position - (Vector2)currentTarget.position).normalized;
            rb.linearVelocity = new Vector2(fleeDir.x * chaseSpeed, rb.linearVelocity.y);
            FaceDirection(-fleeDir.x); // 面向敌人
        }
        // 在攻击范围内 — 停下施法
        else if (dist <= spellRange)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            FaceDirection(currentTarget.position.x - transform.position.x);

            if (spellTimer <= 0)
            {
                StartCoroutine(CastSpell());
                spellTimer = spellCooldown;
            }
        }
        else
        {
            // 靠近到施法距离
            Vector2 dir = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
            rb.linearVelocity = new Vector2(dir.x * chaseSpeed, rb.linearVelocity.y);
            FaceDirection(dir.x);
        }
    }

    private IEnumerator CastSpell()
    {
        if (currentTarget == null) yield break;

        Vector3 targetPos = currentTarget.position;

        // 预警标记
        if (spellWarningPrefab != null)
        {
            var warning = Instantiate(spellWarningPrefab, targetPos, Quaternion.identity);
            Destroy(warning, spellDelay + 0.5f);
        }

        // 施法动画
        if (animator != null)
            animator.SetTrigger("Cast");

        yield return new WaitForSeconds(spellDelay);

        // 法术爆炸
        if (spellImpactPrefab != null)
            Instantiate(spellImpactPrefab, targetPos, Quaternion.identity);

        // 范围伤害
        var hits = Physics2D.OverlapCircleAll(targetPos, spellRadius, LayerMask.GetMask("Player"));
        foreach (var hit in hits)
        {
            var health = hit.GetComponent<PlayerHealth>();
            if (health != null && health.IsAlive)
            {
                health.TakeDamage(spellDamage);

                var playerRb = hit.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                {
                    Vector2 knockDir = (hit.transform.position - targetPos).normalized;
                    playerRb.AddForce(knockDir * 6f, ForceMode2D.Impulse);
                }
            }
        }

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("spell_impact");
    }

    protected override void PerformAttack()
    {
        // 法师不使用普通攻击，靠施法
        if (spellTimer <= 0)
        {
            StartCoroutine(CastSpell());
            spellTimer = spellCooldown;
        }
    }

    public override void TakeDamage(float amount, Vector2 knockbackDir = default)
    {
        base.TakeDamage(amount, knockbackDir);

        // 受击后有概率闪现逃脱
        if (!IsDead && Random.value < 0.3f)
        {
            Vector2 fleeDir = -knockbackDir.normalized;
            Vector3 newPos = transform.position + (Vector3)(fleeDir * fleeTeleportRange);

            var hit = Physics2D.OverlapCircle(newPos, 0.3f, LayerMask.GetMask("Ground"));
            if (hit == null)
            {
                transform.position = newPos;
                if (VFXManager.Instance != null)
                    VFXManager.Instance.Play(VFXManager.Effects.EnemyDeath, transform.position);
            }
        }
    }
}

// ============ 暗影钻地者 - 地底突袭 ============
public class ShadowBurrower : EnemyBase
{
    [Header("钻地特性")]
    [SerializeField] private float burrowSpeed = 6f;
    [SerializeField] private float burrowCooldown = 5f;
    [SerializeField] private float burrowDuration = 2f;
    [SerializeField] private float surfaceAttackRadius = 1.5f;
    [SerializeField] private float surfaceDamage = 2f;
    [SerializeField] private GameObject burrowDustPrefab;
    [SerializeField] private GameObject surfaceImpactPrefab;

    private float burrowTimer;
    private bool isBurrowed;
    private Vector3 burrowTargetPos;

    protected override void Awake()
    {
        base.Awake();
        burrowTimer = burrowCooldown;
    }

    protected override void Update()
    {
        base.Update();

        if (isBurrowed)
        {
            UpdateBurrow();
            return;
        }

        if (currentState == EnemyState.Chase)
        {
            burrowTimer -= Time.deltaTime;
            if (burrowTimer <= 0 && currentTarget != null)
            {
                StartCoroutine(BurrowSequence());
                burrowTimer = burrowCooldown;
            }
        }
    }

    private void UpdateBurrow()
    {
        // 地下移动到目标位置
        transform.position = Vector3.MoveTowards(transform.position, burrowTargetPos,
            burrowSpeed * Time.deltaTime);

        // 地面灰尘效果（仅显示在地表）
        if (burrowDustPrefab != null && Time.frameCount % 10 == 0)
        {
            Vector3 dustPos = new Vector3(transform.position.x,
                burrowTargetPos.y, transform.position.z);
            Instantiate(burrowDustPrefab, dustPos, Quaternion.identity);
        }
    }

    private IEnumerator BurrowSequence()
    {
        if (currentTarget == null) yield break;

        isBurrowed = true;

        // 钻入地下
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("burrow_in");

        if (burrowDustPrefab != null)
            Instantiate(burrowDustPrefab, transform.position, Quaternion.identity);

        // 隐藏
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        rb.simulated = false;
        isInvincible = true;

        // 在地下追踪
        burrowTargetPos = currentTarget.position;
        yield return new WaitForSeconds(burrowDuration * 0.5f);

        // 更新目标位置（预判）
        if (currentTarget != null)
            burrowTargetPos = currentTarget.position;

        yield return new WaitForSeconds(burrowDuration * 0.5f);

        // 从地下冒出
        if (currentTarget != null)
            transform.position = currentTarget.position;

        isBurrowed = false;
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        rb.simulated = true;
        isInvincible = false;

        // 冒出攻击
        SurfaceAttack();
    }

    private void SurfaceAttack()
    {
        if (surfaceImpactPrefab != null)
            Instantiate(surfaceImpactPrefab, transform.position, Quaternion.identity);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("burrow_surface");

        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeMedium();

        // 范围伤害
        var hits = Physics2D.OverlapCircleAll(transform.position, surfaceAttackRadius,
            LayerMask.GetMask("Player"));

        foreach (var hit in hits)
        {
            var health = hit.GetComponent<PlayerHealth>();
            if (health != null && health.IsAlive)
            {
                health.TakeDamage(Mathf.CeilToInt(surfaceDamage));

                var playerRb = hit.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                    playerRb.AddForce(Vector2.up * 10f, ForceMode2D.Impulse);
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.3f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, surfaceAttackRadius);
    }
}

// ============ 暗影拟态 - 伪装型敌人 ============
public class ShadowMimic : EnemyBase
{
    [Header("伪装特性")]
    [SerializeField] private Sprite disguiseSprite;
    [SerializeField] private float revealDistance = 2f;
    [SerializeField] private float revealDelay = 0.5f;
    [SerializeField] private float ambushDamageMultiplier = 2f;
    [SerializeField] private GameObject revealEffect;

    private Sprite originalSprite;
    private bool isDisguised = true;
    private bool isRevealing;
    private Collider2D attackCollider;

    protected override void Awake()
    {
        base.Awake();

        if (spriteRenderer != null)
        {
            originalSprite = spriteRenderer.sprite;
            if (disguiseSprite != null)
                spriteRenderer.sprite = disguiseSprite;
        }

        // 伪装时禁用AI
        attackCollider = GetComponent<Collider2D>();
        rb.bodyType = RigidbodyType2D.Static;
    }

    protected override void Update()
    {
        if (isDisguised)
        {
            CheckReveal();
            return;
        }

        base.Update();
    }

    private void CheckReveal()
    {
        if (isRevealing) return;

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            float dist = Vector2.Distance(transform.position, player.transform.position);
            if (dist <= revealDistance)
            {
                StartCoroutine(RevealSequence(player));
                break;
            }
        }
    }

    private IEnumerator RevealSequence(PlayerController nearestPlayer)
    {
        isRevealing = true;

        // 震动预警
        float elapsed = 0;
        while (elapsed < revealDelay)
        {
            elapsed += Time.deltaTime;
            float shake = Mathf.Sin(elapsed * 50f) * 0.02f;
            transform.position += new Vector3(shake, 0, 0);
            yield return null;
        }

        // 变形
        isDisguised = false;
        if (spriteRenderer != null && originalSprite != null)
            spriteRenderer.sprite = originalSprite;

        if (revealEffect != null)
            Instantiate(revealEffect, transform.position, Quaternion.identity);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("mimic_reveal");

        // 恢复物理
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 2.5f;

        // 突袭攻击 — 对最近玩家造成强化伤害
        float dist = Vector2.Distance(transform.position, nearestPlayer.transform.position);
        if (dist <= attackRange * 2f)
        {
            var health = nearestPlayer.GetComponent<PlayerHealth>();
            if (health != null)
            {
                Vector2 knockback = (nearestPlayer.transform.position - transform.position).normalized;
                health.TakeDamage(damage * ambushDamageMultiplier, knockback * 8f);
            }

            if (VFXManager.Instance != null)
                VFXManager.Instance.ShakeMedium();
        }

        // 设定目标并开始追击
        currentTarget = nearestPlayer.transform;
        SetState(EnemyState.Chase);
        isRevealing = false;
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

    void OnDrawGizmos()
    {
        Gizmos.color = isDisguised ? new Color(0f, 1f, 0f, 0.3f) : new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, revealDistance);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f,
            isDisguised ? "Mimic (隐藏)" : "Mimic (暴露)");
#endif
    }
}
