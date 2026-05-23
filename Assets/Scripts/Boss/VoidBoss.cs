using UnityEngine;
using System.Collections;

/// <summary>
/// 第五章最终 Boss：虚无之影
/// 三阶段战斗：
/// Phase 1 (100%-60%): 影子弹幕 + 地面冲击
/// Phase 2 (60%-30%):  分身攻击 + 光暗区域翻转
/// Phase 3 (30%-0%):   全能力融合 + 需要双人配合击败
/// </summary>
public class VoidBoss : BossBase
{
    [Header("Phase 1: Shadow Barrage")]
    [SerializeField] private GameObject shadowProjectilePrefab;
    [SerializeField] private int barrageCount = 5;
    [SerializeField] private float projectileSpeed = 6f;

    [Header("Phase 2: Clone Attack")]
    [SerializeField] private GameObject clonePrefab;
    [SerializeField] private int cloneCount = 2;
    [SerializeField] private float cloneLifetime = 5f;

    [Header("Phase 3: Void Storm")]
    [SerializeField] private GameObject voidZonePrefab;
    [SerializeField] private float stormRadius = 8f;
    [SerializeField] private float stormDuration = 4f;

    [Header("Movement")]
    [SerializeField] private float hoverHeight = 3f;
    [SerializeField] private float hoverAmplitude = 0.5f;
    [SerializeField] private float hoverSpeed = 2f;

    [Header("Weak Point")]
    [SerializeField] private GameObject weakPointObject;
    [SerializeField] private float weakPointExposeDuration = 3f;

    private Vector3 homePosition;
    private bool weakPointExposed;

    protected override void Awake()
    {
        base.Awake();
        homePosition = transform.position;
    }

    protected override void UpdateMovement()
    {
        var phase = GetCurrentPhase();
        float speed = phase?.moveSpeed ?? 2f;
        var target = FindNearestPlayer();

        if (target == null) return;

        // 悬浮在玩家上方
        float desiredX = Mathf.Lerp(transform.position.x, target.position.x, speed * Time.deltaTime * 0.3f);
        float desiredY = homePosition.y + hoverHeight + Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude;
        transform.position = new Vector3(desiredX, desiredY, 0);

        // 面朝玩家
        float dir = target.position.x - transform.position.x;
        if (Mathf.Abs(dir) > 0.1f)
            transform.localScale = new Vector3(dir > 0 ? -1 : 1, 1, 1);
    }

    protected override IEnumerator ExecuteAttackPattern()
    {
        isAttacking = true;

        switch (CurrentPhaseIndex)
        {
            case 0:
                yield return Phase1Attack();
                break;
            case 1:
                int attackType = Random.Range(0, 2);
                if (attackType == 0)
                    yield return Phase1Attack(); // 保留弹幕
                else
                    yield return Phase2CloneAttack();
                break;
            case 2:
                int p3Attack = Random.Range(0, 3);
                if (p3Attack == 0)
                    yield return Phase1Attack();
                else if (p3Attack == 1)
                    yield return Phase2CloneAttack();
                else
                    yield return Phase3VoidStorm();
                break;
        }

        // 攻击后暴露弱点
        yield return ExposeWeakPoint();

        isAttacking = false;
    }

    /// <summary>
    /// Phase 1: 向玩家发射扇形影子弹幕
    /// </summary>
    private IEnumerator Phase1Attack()
    {
        if (shadowProjectilePrefab == null) yield break;

        var target = FindNearestPlayer();
        if (target == null) yield break;

        // 蓄力
        IsInvincible = true;
        if (spriteRenderer != null)
            spriteRenderer.color = new Color(0.5f, 0, 0.8f);
        yield return new WaitForSeconds(0.5f);

        // 扇形发射
        Vector2 baseDir = ((Vector2)(target.position - transform.position)).normalized;
        float spreadAngle = 30f;
        float startAngle = -spreadAngle / 2f;
        float step = spreadAngle / Mathf.Max(1, barrageCount - 1);

        for (int i = 0; i < barrageCount; i++)
        {
            float angle = startAngle + step * i;
            Vector2 dir = RotateVector(baseDir, angle);

            var proj = Instantiate(shadowProjectilePrefab, transform.position, Quaternion.identity);
            var projRb = proj.GetComponent<Rigidbody2D>();
            if (projRb != null)
                projRb.linearVelocity = dir * projectileSpeed;

            // 添加伤害
            if (proj.GetComponent<Hazard>() == null)
            {
                var hazard = proj.AddComponent<Hazard>();
            }

            Destroy(proj, 5f);
            yield return new WaitForSeconds(0.1f);
        }

        IsInvincible = false;
        if (spriteRenderer != null)
            spriteRenderer.color = GetCurrentPhase()?.phaseColor ?? Color.white;
    }

    /// <summary>
    /// Phase 2: 生成分身攻击两个玩家
    /// </summary>
    private IEnumerator Phase2CloneAttack()
    {
        if (clonePrefab == null) yield break;

        IsInvincible = true;

        // 闪烁消失
        for (int i = 0; i < 4; i++)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(0.1f);
        }
        if (spriteRenderer != null) spriteRenderer.enabled = true;

        // 在两个玩家附近生成分身
        var lux = GetLuxPlayer();
        var nox = GetNoxPlayer();

        if (lux != null)
        {
            var clone = Instantiate(clonePrefab, lux.position + Vector3.right * 3, Quaternion.identity);
            Destroy(clone, cloneLifetime);
        }
        if (nox != null)
        {
            var clone = Instantiate(clonePrefab, nox.position + Vector3.left * 3, Quaternion.identity);
            Destroy(clone, cloneLifetime);
        }

        yield return new WaitForSeconds(1f);
        IsInvincible = false;
    }

    /// <summary>
    /// Phase 3: 虚无风暴 - 需要一人用光照亮安全区，另一人攻击弱点
    /// </summary>
    private IEnumerator Phase3VoidStorm()
    {
        if (voidZonePrefab == null) yield break;

        IsInvincible = true;

        // 飞到场地中央
        float t = 0;
        Vector3 centerPos = new Vector3(homePosition.x, homePosition.y + hoverHeight + 2, 0);
        Vector3 startPos = transform.position;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            transform.position = Vector3.Lerp(startPos, centerPos, t);
            yield return null;
        }

        // 释放虚无风暴（圆形伤害区域，但有安全缝隙）
        int zoneCount = 8;
        for (int i = 0; i < zoneCount; i++)
        {
            float angle = (360f / zoneCount) * i;
            // 留出两个安全缝隙（需要 Lux 用光桥、Nox 用影子区域）
            if (i == 2 || i == 6) continue;

            Vector2 dir = RotateVector(Vector2.right, angle);
            Vector3 zonePos = transform.position + (Vector3)(dir * stormRadius * 0.5f);

            var zone = Instantiate(voidZonePrefab, zonePos, Quaternion.identity);
            zone.transform.localScale = Vector3.one * 2f;
            Destroy(zone, stormDuration);
        }

        yield return new WaitForSeconds(stormDuration);
        IsInvincible = false;
    }

    /// <summary>
    /// 攻击后暴露弱点，玩家需在此时攻击
    /// </summary>
    private IEnumerator ExposeWeakPoint()
    {
        if (weakPointObject == null) yield break;

        weakPointExposed = true;
        weakPointObject.SetActive(true);

        yield return new WaitForSeconds(weakPointExposeDuration);

        weakPointObject.SetActive(false);
        weakPointExposed = false;
    }

    protected override void OnPhaseEnter(int phaseIndex)
    {
        base.OnPhaseEnter(phaseIndex);

        switch (phaseIndex)
        {
            case 1:
                barrageCount = 8;
                projectileSpeed = 8f;
                break;
            case 2:
                barrageCount = 12;
                projectileSpeed = 10f;
                hoverSpeed = 3f;
                break;
        }
    }

    protected override IEnumerator DefeatSequence()
    {
        IsBattleActive = false;
        IsInvincible = true;

        // Boss 被击败：光影融合动画
        float duration = 3f;
        float elapsed = 0;
        Vector3 startScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.localScale = startScale * (1f + t * 0.5f);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(
                    new Color(0.3f, 0, 0.5f),
                    new Color(1f, 1f, 0.9f),
                    t
                );
                spriteRenderer.color = new Color(
                    spriteRenderer.color.r,
                    spriteRenderer.color.g,
                    spriteRenderer.color.b,
                    1f - t
                );
            }

            yield return null;
        }

        OnBossDefeated?.Invoke();
        LevelManager.Instance?.CompleteLevel();
        Destroy(gameObject);
    }

    private Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}
