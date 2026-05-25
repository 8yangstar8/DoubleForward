using UnityEngine;
using System.Collections;

/// <summary>
/// 第一章Boss：古树守卫 (Forest Guardian)
/// 森林主题 - 利用光照弱点和暗影穿越根须
/// Phase 1: 根须扫击 + 落叶弹幕
/// Phase 2: 召唤藤蔓陷阱 + 疯狂摆动
/// Phase 3: 核心暴露，需Lux照明 + Nox攻击
/// </summary>
public class ForestGuardianBoss : BossBase
{
    [Header("根须攻击")]
    [SerializeField] private GameObject rootSwipePrefab;
    [SerializeField] private GameObject leafProjectilePrefab;
    [SerializeField] private int leafCount = 6;
    [SerializeField] private float leafSpeed = 5f;

    [Header("藤蔓")]
    [SerializeField] private GameObject vineTrapPrefab;
    [SerializeField] private int vineCount = 3;
    [SerializeField] private float vineDuration = 4f;

    [Header("核心")]
    [SerializeField] private GameObject coreObject;
    [SerializeField] private float coreExposeDuration = 4f;

    [Header("移动")]
    [SerializeField] private float swayAmplitude = 1f;
    [SerializeField] private float swaySpeed = 1.5f;

    private Vector3 homePos;
    private bool coreExposed;

    protected override void Awake()
    {
        base.Awake();
        homePos = transform.position;
        if (coreObject != null) coreObject.SetActive(false);
    }

    protected override void UpdateMovement()
    {
        // 古树缓慢摆动
        float phase = GetCurrentPhase()?.moveSpeed ?? 1f;
        float x = homePos.x + Mathf.Sin(Time.time * swaySpeed * phase) * swayAmplitude;
        transform.position = new Vector3(x, homePos.y, 0);
    }

    protected override IEnumerator ExecuteAttackPattern()
    {
        isAttacking = true;

        switch (CurrentPhaseIndex)
        {
            case 0:
                yield return RootSwipeAttack();
                break;
            case 1:
                int roll = Random.Range(0, 3);
                if (roll == 0) yield return RootSwipeAttack();
                else if (roll == 1) yield return LeafBarrage();
                else yield return VineTrapAttack();
                break;
            case 2:
                int roll2 = Random.Range(0, 2);
                if (roll2 == 0) yield return LeafBarrage();
                else yield return VineTrapAttack();
                yield return ExposeCore();
                break;
        }

        isAttacking = false;
    }

    private IEnumerator RootSwipeAttack()
    {
        if (rootSwipePrefab == null) yield break;

        // 蓄力
        if (spriteRenderer != null)
            spriteRenderer.color = new Color(0.3f, 0.6f, 0.2f);
        yield return new WaitForSeconds(0.8f);

        // 从地面升起根须，横扫玩家
        var target = FindNearestPlayer();
        if (target != null)
        {
            Vector3 spawnPos = new Vector3(target.position.x, target.position.y - 1f, 0);
            var root = Instantiate(rootSwipePrefab, spawnPos, Quaternion.identity);
            Destroy(root, 2f);

            if (VFXManager.Instance != null)
                VFXManager.Instance.ShakeLight();
        }

        if (spriteRenderer != null)
            spriteRenderer.color = GetCurrentPhase()?.phaseColor ?? Color.white;

        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator LeafBarrage()
    {
        if (leafProjectilePrefab == null) yield break;

        // 向四周发射树叶
        for (int i = 0; i < leafCount; i++)
        {
            float angle = Random.Range(0f, 360f);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            var leaf = Instantiate(leafProjectilePrefab,
                transform.position + Vector3.up * 2f, Quaternion.identity);
            var rb = leaf.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = dir * leafSpeed;

            Destroy(leaf, 4f);
            yield return new WaitForSeconds(0.15f);
        }
    }

    private IEnumerator VineTrapAttack()
    {
        if (vineTrapPrefab == null) yield break;

        // 在玩家脚下生成藤蔓陷阱
        var lux = GetLuxPlayer();
        var nox = GetNoxPlayer();

        Transform[] targets = { lux, nox };
        foreach (var t in targets)
        {
            if (t == null) continue;
            var vine = Instantiate(vineTrapPrefab, t.position, Quaternion.identity);
            Destroy(vine, vineDuration);
        }

        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator ExposeCore()
    {
        if (coreObject == null) yield break;

        coreExposed = true;
        coreObject.SetActive(true);
        IsInvincible = false;

        yield return new WaitForSeconds(coreExposeDuration);

        coreObject.SetActive(false);
        coreExposed = false;
    }

    protected override void OnPhaseEnter(int phaseIndex)
    {
        base.OnPhaseEnter(phaseIndex);
        if (phaseIndex >= 1)
        {
            swaySpeed *= 1.3f;
            leafCount += 3;
        }

        // 发布Boss阶段变更事件
        EventBus.Publish(new BossPhaseChangedEvent
        {
            newPhase = phaseIndex,
            bossHealthPercent = (float)CurrentHealth / maxHealth
        });
    }

    protected override IEnumerator DefeatSequence()
    {
        IsBattleActive = false;
        IsInvincible = true;

        // 古树倒塌动画
        float duration = 2.5f;
        float elapsed = 0;
        Quaternion startRot = transform.rotation;
        Quaternion endRot = Quaternion.Euler(0, 0, -90f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.rotation = Quaternion.Lerp(startRot, endRot, t * t);

            if (spriteRenderer != null)
                spriteRenderer.color = new Color(1, 1, 1, 1f - t);

            yield return null;
        }

        EventBus.Publish(new BossDefeatedEvent
        {
            bossName = bossName,
            chapter = 1
        });

        OnBossDefeated?.Invoke();
        Destroy(gameObject);
    }
}

/// <summary>
/// 第二章Boss：齿轮暴君 (Gear Tyrant)
/// 工厂主题 - 利用机械传送带和齿轮机制
/// Phase 1: 齿轮飞弹 + 冲压攻击
/// Phase 2: 传送带操纵 + 激光扫射
/// Phase 3: 自爆倒计时，需合作破坏能量核心
/// </summary>
public class GearTyrantBoss : BossBase
{
    [Header("齿轮攻击")]
    [SerializeField] private GameObject gearProjectilePrefab;
    [SerializeField] private int gearCount = 4;
    [SerializeField] private float gearSpeed = 7f;

    [Header("冲压")]
    [SerializeField] private float stompHeight = 8f;
    [SerializeField] private float stompSpeed = 15f;
    [SerializeField] private float stompShakeIntensity = 0.3f;

    [Header("激光")]
    [SerializeField] private LineRenderer laserBeam;
    [SerializeField] private float laserSweepSpeed = 30f;
    [SerializeField] private float laserDuration = 3f;
    [SerializeField] private float laserDamageRadius = 0.5f;

    [Header("传送带")]
    [SerializeField] private ConveyorBelt[] arenaConveyors;
    [SerializeField] private float conveyorReverseInterval = 5f;

    [Header("移动")]
    [SerializeField] private Transform[] moveWaypoints;
    [SerializeField] private float moveSpeed = 3f;

    private int currentWaypoint;
    private float conveyorTimer;

    protected override void Awake()
    {
        base.Awake();
        if (laserBeam != null) laserBeam.enabled = false;
    }

    protected override void UpdateMovement()
    {
        if (moveWaypoints == null || moveWaypoints.Length == 0) return;

        var target = moveWaypoints[currentWaypoint];
        if (target == null) return;

        float speed = GetCurrentPhase()?.moveSpeed ?? moveSpeed;
        transform.position = Vector3.MoveTowards(
            transform.position, target.position, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.position) < 0.3f)
            currentWaypoint = (currentWaypoint + 1) % moveWaypoints.Length;

        // Phase 2+: 定期反转传送带
        if (CurrentPhaseIndex >= 1 && arenaConveyors != null)
        {
            conveyorTimer += Time.deltaTime;
            if (conveyorTimer >= conveyorReverseInterval)
            {
                conveyorTimer = 0;
                foreach (var belt in arenaConveyors)
                {
                    if (belt != null) belt.ReverseDirection();
                }
            }
        }
    }

    protected override IEnumerator ExecuteAttackPattern()
    {
        isAttacking = true;

        switch (CurrentPhaseIndex)
        {
            case 0:
                if (Random.value > 0.5f) yield return GearBarrage();
                else yield return StompAttack();
                break;
            case 1:
                int roll = Random.Range(0, 3);
                if (roll == 0) yield return GearBarrage();
                else if (roll == 1) yield return StompAttack();
                else yield return LaserSweep();
                break;
            case 2:
                yield return LaserSweep();
                yield return GearBarrage();
                break;
        }

        isAttacking = false;
    }

    private IEnumerator GearBarrage()
    {
        if (gearProjectilePrefab == null) yield break;

        var target = FindNearestPlayer();
        if (target == null) yield break;

        for (int i = 0; i < gearCount; i++)
        {
            Vector2 dir = ((Vector2)(target.position - transform.position)).normalized;
            dir += new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.2f));
            dir.Normalize();

            var gear = Instantiate(gearProjectilePrefab, transform.position, Quaternion.identity);
            var rb = gear.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = dir * gearSpeed;
                rb.angularVelocity = 360f; // 旋转
            }
            Destroy(gear, 5f);

            yield return new WaitForSeconds(0.2f);
        }
    }

    private IEnumerator StompAttack()
    {
        // 飞到高处
        Vector3 startPos = transform.position;
        Vector3 highPos = new Vector3(startPos.x, startPos.y + stompHeight, 0);

        float t = 0;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, highPos, t / 0.5f);
            yield return null;
        }

        // 锁定目标
        var target = FindNearestPlayer();
        if (target != null)
            highPos.x = target.position.x;

        yield return new WaitForSeconds(0.3f);

        // 快速下砸
        Vector3 groundPos = new Vector3(highPos.x, startPos.y, 0);
        t = 0;
        float dur = Vector3.Distance(highPos, groundPos) / stompSpeed;
        while (t < dur)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(highPos, groundPos, t / dur);
            yield return null;
        }

        // 着地震波
        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeHeavy();

        // 范围伤害
        var hits = Physics2D.OverlapCircleAll(transform.position, 3f);
        foreach (var hit in hits)
        {
            var health = hit.GetComponent<PlayerHealth>();
            if (health != null && health.IsAlive)
                health.TakeDamage(2);
        }

        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator LaserSweep()
    {
        if (laserBeam == null) yield break;

        laserBeam.enabled = true;
        IsInvincible = true;

        float angle = 0;
        float elapsed = 0;

        while (elapsed < laserDuration)
        {
            elapsed += Time.deltaTime;
            angle += laserSweepSpeed * Time.deltaTime;

            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector3 endPos = transform.position + (Vector3)(dir * 15f);

            laserBeam.SetPosition(0, transform.position);
            laserBeam.SetPosition(1, endPos);

            // 激光伤害检测
            var hits = Physics2D.CircleCastAll(transform.position, laserDamageRadius, dir, 15f);
            foreach (var hit in hits)
            {
                var health = hit.collider.GetComponent<PlayerHealth>();
                if (health != null && health.IsAlive && !health.IsInvincible)
                    health.TakeDamage(1);
            }

            yield return null;
        }

        laserBeam.enabled = false;
        IsInvincible = false;
    }

    protected override void OnPhaseEnter(int phaseIndex)
    {
        base.OnPhaseEnter(phaseIndex);
        gearCount += phaseIndex * 2;

        EventBus.Publish(new BossPhaseChangedEvent
        {
            newPhase = phaseIndex,
            bossHealthPercent = (float)CurrentHealth / maxHealth
        });
    }

    protected override IEnumerator DefeatSequence()
    {
        IsBattleActive = false;
        IsInvincible = true;

        // 机械爆炸动画
        for (int i = 0; i < 6; i++)
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-1.5f, 1.5f), Random.Range(-1f, 2f), 0);

            if (VFXManager.Instance != null)
                VFXManager.Instance.Play(VFXManager.Effects.Explosion,
                    transform.position + randomOffset);

            if (VFXManager.Instance != null)
                VFXManager.Instance.ShakeMedium();

            yield return new WaitForSeconds(0.3f);
        }

        EventBus.Publish(new BossDefeatedEvent { bossName = bossName, chapter = 2 });
        OnBossDefeated?.Invoke();
        Destroy(gameObject);
    }
}

/// <summary>
/// 第三章Boss：深渊海蛇 (Abyss Serpent)
/// 深渊主题 - 水下环境，利用水流和光暗区域
/// Phase 1: 盘旋冲撞 + 毒液喷射
/// Phase 2: 缠绕攻击 + 潮汐翻涌
/// Phase 3: 分体重组，需同时攻击头尾
/// </summary>
public class AbyssSerpentBoss : BossBase
{
    [Header("蛇体")]
    [SerializeField] private Transform[] bodySegments;           // 蛇身体节
    [SerializeField] private float segmentFollowSpeed = 8f;
    [SerializeField] private float bodySpacing = 1.2f;

    [Header("冲撞")]
    [SerializeField] private float chargeSpeed = 12f;
    [SerializeField] private float chargeWindup = 1f;

    [Header("毒液")]
    [SerializeField] private GameObject venomPrefab;
    [SerializeField] private int venomCount = 5;
    [SerializeField] private float venomSpeed = 6f;

    [Header("潮汐")]
    [SerializeField] private GameObject tidalWavePrefab;
    [SerializeField] private float waveDuration = 3f;

    [Header("蛇尾")]
    [SerializeField] private BossBase tailBoss;                  // Phase3的尾巴弱点

    [Header("路径")]
    [SerializeField] private Transform[] patrolPath;
    [SerializeField] private float patrolSpeed = 4f;

    private int pathIndex;
    private bool isCharging;
    private Vector2 chargeDirection;

    protected override void UpdateMovement()
    {
        if (isCharging) return;

        // 沿路径巡游
        if (patrolPath != null && patrolPath.Length > 0)
        {
            var target = patrolPath[pathIndex];
            if (target != null)
            {
                float speed = GetCurrentPhase()?.moveSpeed ?? patrolSpeed;
                transform.position = Vector3.MoveTowards(
                    transform.position, target.position, speed * Time.deltaTime);

                // 面朝移动方向
                Vector3 dir = target.position - transform.position;
                if (dir.sqrMagnitude > 0.01f)
                {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Lerp(transform.rotation,
                        Quaternion.Euler(0, 0, angle), Time.deltaTime * 5f);
                }

                if (Vector3.Distance(transform.position, target.position) < 0.5f)
                    pathIndex = (pathIndex + 1) % patrolPath.Length;
            }
        }

        // 蛇身跟随
        UpdateBodySegments();
    }

    private void UpdateBodySegments()
    {
        if (bodySegments == null) return;

        Transform leader = transform;
        foreach (var segment in bodySegments)
        {
            if (segment == null) continue;

            Vector3 targetPos = leader.position - leader.right * bodySpacing;
            segment.position = Vector3.Lerp(segment.position, targetPos,
                segmentFollowSpeed * Time.deltaTime);

            Vector3 dir = leader.position - segment.position;
            if (dir.sqrMagnitude > 0.01f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                segment.rotation = Quaternion.Lerp(segment.rotation,
                    Quaternion.Euler(0, 0, angle), Time.deltaTime * 8f);
            }

            leader = segment;
        }
    }

    protected override IEnumerator ExecuteAttackPattern()
    {
        isAttacking = true;

        switch (CurrentPhaseIndex)
        {
            case 0:
                if (Random.value > 0.5f) yield return ChargeAttack();
                else yield return VenomSpray();
                break;
            case 1:
                int roll = Random.Range(0, 3);
                if (roll == 0) yield return ChargeAttack();
                else if (roll == 1) yield return VenomSpray();
                else yield return TidalWave();
                break;
            case 2:
                yield return ChargeAttack();
                yield return TidalWave();
                break;
        }

        isAttacking = false;
    }

    private IEnumerator ChargeAttack()
    {
        var target = FindNearestPlayer();
        if (target == null) yield break;

        // 蓄力：红色闪烁
        for (int i = 0; i < 3; i++)
        {
            if (spriteRenderer != null)
                spriteRenderer.color = i % 2 == 0 ? Color.red : Color.white;
            yield return new WaitForSeconds(chargeWindup / 3f);
        }

        // 冲向玩家
        isCharging = true;
        chargeDirection = ((Vector2)(target.position - transform.position)).normalized;
        float angle = Mathf.Atan2(chargeDirection.y, chargeDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        float elapsed = 0;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            transform.position += (Vector3)(chargeDirection * chargeSpeed * Time.deltaTime);
            UpdateBodySegments();

            // 碰撞检测
            var hits = Physics2D.OverlapCircleAll(transform.position, 1f);
            foreach (var hit in hits)
            {
                var health = hit.GetComponent<PlayerHealth>();
                if (health != null && health.IsAlive && !health.IsInvincible)
                {
                    health.TakeDamage(3);
                    var hitRb = hit.GetComponent<Rigidbody2D>();
                    if (hitRb != null)
                        hitRb.AddForce(chargeDirection * 8f, ForceMode2D.Impulse);
                }
            }

            yield return null;
        }

        isCharging = false;
        if (spriteRenderer != null)
            spriteRenderer.color = GetCurrentPhase()?.phaseColor ?? Color.white;
    }

    private IEnumerator VenomSpray()
    {
        if (venomPrefab == null) yield break;

        var target = FindNearestPlayer();
        if (target == null) yield break;

        for (int i = 0; i < venomCount; i++)
        {
            Vector2 dir = ((Vector2)(target.position - transform.position)).normalized;
            dir += new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.2f));

            var venom = Instantiate(venomPrefab, transform.position + transform.right * 1.5f,
                Quaternion.identity);
            var rb = venom.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = dir.normalized * venomSpeed;
            Destroy(venom, 4f);

            yield return new WaitForSeconds(0.12f);
        }
    }

    private IEnumerator TidalWave()
    {
        if (tidalWavePrefab == null) yield break;

        // 从两侧生成潮汐
        var leftWave = Instantiate(tidalWavePrefab,
            transform.position + Vector3.left * 10f, Quaternion.identity);
        var rightWave = Instantiate(tidalWavePrefab,
            transform.position + Vector3.right * 10f,
            Quaternion.Euler(0, 180, 0));

        Destroy(leftWave, waveDuration);
        Destroy(rightWave, waveDuration);

        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeMedium();

        yield return new WaitForSeconds(waveDuration);
    }

    protected override void OnPhaseEnter(int phaseIndex)
    {
        base.OnPhaseEnter(phaseIndex);
        EventBus.Publish(new BossPhaseChangedEvent
        {
            newPhase = phaseIndex,
            bossHealthPercent = (float)CurrentHealth / maxHealth
        });
    }

    protected override IEnumerator DefeatSequence()
    {
        IsBattleActive = false;
        IsInvincible = true;

        // 蛇体解体动画
        if (bodySegments != null)
        {
            foreach (var seg in bodySegments)
            {
                if (seg == null) continue;
                var segRb = seg.GetComponent<Rigidbody2D>();
                if (segRb != null)
                {
                    segRb.bodyType = RigidbodyType2D.Dynamic;
                    segRb.AddForce(Random.insideUnitCircle * 5f, ForceMode2D.Impulse);
                }
                yield return new WaitForSeconds(0.15f);
            }
        }

        yield return new WaitForSeconds(1.5f);

        EventBus.Publish(new BossDefeatedEvent { bossName = bossName, chapter = 3 });
        OnBossDefeated?.Invoke();
        Destroy(gameObject);
    }
}

/// <summary>
/// 第四章Boss：遗迹守护者 (Ruin Sentinel)
/// 遗迹主题 - 石像机关Boss，利用光影切换和谜题机制
/// Phase 1: 石拳砸地 + 符文弹
/// Phase 2: 光暗盾切换（需对应角色攻击）+ 石柱召唤
/// Phase 3: 符文矩阵谜题 + 全屏攻击
/// </summary>
public class RuinSentinelBoss : BossBase
{
    [Header("石拳")]
    [SerializeField] private Transform leftFist;
    [SerializeField] private Transform rightFist;
    [SerializeField] private float fistSlamSpeed = 12f;
    [SerializeField] private float fistReturnSpeed = 3f;

    [Header("符文弹")]
    [SerializeField] private GameObject runeProjectilePrefab;
    [SerializeField] private int runeCount = 6;
    [SerializeField] private float runeSpeed = 5f;

    [Header("光暗盾")]
    [SerializeField] private SpriteRenderer shieldRenderer;
    [SerializeField] private Color lightShieldColor = new Color(1f, 0.9f, 0.5f);
    [SerializeField] private Color shadowShieldColor = new Color(0.3f, 0.1f, 0.5f);
    [SerializeField] private float shieldSwitchInterval = 4f;

    [Header("石柱")]
    [SerializeField] private GameObject pillarPrefab;
    [SerializeField] private float pillarDuration = 5f;

    private bool isLightShield = true;  // true=光盾(需Nox攻击)，false=暗盾(需Lux攻击)
    private float shieldTimer;
    private Vector3 leftFistHome;
    private Vector3 rightFistHome;

    protected override void Awake()
    {
        base.Awake();
        if (leftFist != null) leftFistHome = leftFist.localPosition;
        if (rightFist != null) rightFistHome = rightFist.localPosition;
        UpdateShieldVisual();
    }

    protected override void UpdateMovement()
    {
        // 石像基本不移动，只轻微震动
        float shake = Mathf.Sin(Time.time * 3f) * 0.05f;
        transform.position = new Vector3(transform.position.x, transform.position.y + shake, 0);

        // Phase 2+：定期切换光暗盾
        if (CurrentPhaseIndex >= 1)
        {
            shieldTimer += Time.deltaTime;
            if (shieldTimer >= shieldSwitchInterval)
            {
                shieldTimer = 0;
                isLightShield = !isLightShield;
                UpdateShieldVisual();
            }
        }
    }

    /// <summary>
    /// 重写受伤：Phase 2+需要对应角色攻击
    /// 光盾状态需要Nox(暗影)攻击，暗盾状态需要Lux(光明)攻击
    /// </summary>
    public override void TakeDamage(int damage)
    {
        if (CurrentPhaseIndex >= 1 && shieldRenderer != null && shieldRenderer.gameObject.activeSelf)
        {
            // 通过检测攻击来源判断是否匹配
            // 使用最近攻击的玩家类型（由EventBus中EnemyHitEvent传递）
            var lux = GetLuxPlayer();
            var nox = GetNoxPlayer();

            // 简化判定：检查最近的玩家 — 距离更近的视为攻击者
            bool attackerIsLux = false;
            if (lux != null && nox != null)
            {
                float dLux = Vector2.Distance(transform.position, lux.position);
                float dNox = Vector2.Distance(transform.position, nox.position);
                attackerIsLux = dLux < dNox;
            }
            else if (lux != null)
            {
                attackerIsLux = true;
            }

            // 光盾 → 需要暗影攻击(Nox) | 暗盾 → 需要光明攻击(Lux)
            bool shieldBlocks = (isLightShield && attackerIsLux) || (!isLightShield && !attackerIsLux);

            if (shieldBlocks)
            {
                // 盾挡住了 — 反弹特效
                if (VFXManager.Instance != null)
                    VFXManager.Instance.Play(VFXManager.Effects.ShieldBlock, transform.position);
                if (SoundFeedback.Instance != null)
                    SoundFeedback.Instance.Play("shield_block");
                return;
            }
        }

        base.TakeDamage(damage);
    }

    protected override IEnumerator ExecuteAttackPattern()
    {
        isAttacking = true;

        switch (CurrentPhaseIndex)
        {
            case 0:
                if (Random.value > 0.5f) yield return FistSlam();
                else yield return RuneBarrage();
                break;
            case 1:
                int roll = Random.Range(0, 3);
                if (roll == 0) yield return FistSlam();
                else if (roll == 1) yield return RuneBarrage();
                else yield return PillarSummon();
                break;
            case 2:
                yield return RuneBarrage();
                yield return FistSlam();
                yield return PillarSummon();
                break;
        }

        isAttacking = false;
    }

    private IEnumerator FistSlam()
    {
        var target = FindNearestPlayer();
        if (target == null) yield break;

        // 选择最近的拳
        Transform fist = Random.value > 0.5f ? leftFist : rightFist;
        if (fist == null) yield break;

        Vector3 home = fist == leftFist ? leftFistHome : rightFistHome;

        // 抬拳
        Vector3 raisePos = fist.localPosition + Vector3.up * 3f;
        yield return MoveFist(fist, raisePos, 0.4f);

        yield return new WaitForSeconds(0.2f);

        // 砸向玩家X位置
        Vector3 slamTarget = fist.parent.InverseTransformPoint(
            new Vector3(target.position.x, fist.parent.position.y - 1f, 0));

        yield return MoveFist(fist, slamTarget, 0.15f);

        // 震波
        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeHeavy();

        // 范围伤害
        Vector3 worldPos = fist.position;
        var hits = Physics2D.OverlapCircleAll(worldPos, 2f);
        foreach (var hit in hits)
        {
            var health = hit.GetComponent<PlayerHealth>();
            if (health != null && health.IsAlive)
                health.TakeDamage(2);
        }

        yield return new WaitForSeconds(1f);

        // 收拳
        yield return MoveFist(fist, home, 0.8f);
    }

    private IEnumerator MoveFist(Transform fist, Vector3 targetLocal, float duration)
    {
        Vector3 start = fist.localPosition;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            fist.localPosition = Vector3.Lerp(start, targetLocal, t / duration);
            yield return null;
        }
        fist.localPosition = targetLocal;
    }

    private IEnumerator RuneBarrage()
    {
        if (runeProjectilePrefab == null) yield break;

        // 圆形散射符文弹
        float angleStep = 360f / runeCount;
        for (int i = 0; i < runeCount; i++)
        {
            float angle = angleStep * i;
            Vector2 dir = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));

            var rune = Instantiate(runeProjectilePrefab,
                transform.position + Vector3.up, Quaternion.identity);
            var rb = rune.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = dir * runeSpeed;
            Destroy(rune, 5f);
        }

        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator PillarSummon()
    {
        if (pillarPrefab == null) yield break;

        // 在两个玩家位置召唤石柱
        var lux = GetLuxPlayer();
        var nox = GetNoxPlayer();

        // 预警闪烁
        yield return new WaitForSeconds(0.8f);

        if (lux != null)
        {
            var p = Instantiate(pillarPrefab, lux.position, Quaternion.identity);
            Destroy(p, pillarDuration);
        }
        if (nox != null)
        {
            var p = Instantiate(pillarPrefab, nox.position, Quaternion.identity);
            Destroy(p, pillarDuration);
        }

        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeMedium();

        yield return new WaitForSeconds(0.5f);
    }

    private void UpdateShieldVisual()
    {
        if (shieldRenderer != null)
            shieldRenderer.color = isLightShield ? lightShieldColor : shadowShieldColor;
    }

    protected override void OnPhaseEnter(int phaseIndex)
    {
        base.OnPhaseEnter(phaseIndex);
        EventBus.Publish(new BossPhaseChangedEvent
        {
            newPhase = phaseIndex,
            bossHealthPercent = (float)CurrentHealth / maxHealth
        });
    }

    protected override IEnumerator DefeatSequence()
    {
        IsBattleActive = false;
        IsInvincible = true;

        // 石像碎裂动画
        for (int i = 0; i < 8; i++)
        {
            if (VFXManager.Instance != null)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-2f, 2f), Random.Range(-1f, 3f), 0);
                VFXManager.Instance.Play(VFXManager.Effects.Explosion,
                    transform.position + offset);
            }
            yield return new WaitForSeconds(0.2f);
        }

        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeHeavy();

        yield return new WaitForSeconds(0.5f);

        EventBus.Publish(new BossDefeatedEvent { bossName = bossName, chapter = 4 });
        OnBossDefeated?.Invoke();
        Destroy(gameObject);
    }
}
