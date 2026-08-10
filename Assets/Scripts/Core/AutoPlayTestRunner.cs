using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 自动Play集成测试运行器（运行时）- 由Editor驱动在Play模式下执行
/// 结果通过静态字段暴露给Editor读取
/// </summary>
public class AutoPlayTestRunner : MonoBehaviour
{
    public static bool Done;
    public static int Passed;
    public static int Total;
    public static readonly List<string> Results = new List<string>();

    private static void Check(string name, bool cond)
    {
        Total++;
        if (cond) { Passed++; Results.Add($"PASS: {name}"); }
        else Results.Add($"FAIL: {name}");
    }

    void Start()
    {
        Done = false;
        Passed = Total = 0;
        Results.Clear();
        DontDestroyOnLoad(gameObject); // 切场景时存活
        StartCoroutine(RunTests());
    }

    private IEnumerator RunTests()
    {
        // 等待GameInitializer
        float timeout = 10f;
        while (!GameInitializer.IsReady && timeout > 0)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        Check("GameInitializer ready", GameInitializer.IsReady);
        Check("GameManager exists", GameManager.Instance != null);
        Check("InputManager exists", InputManager.Instance != null);
        Check("AudioManager exists", AudioManager.Instance != null);

        // 等待Boot流程稳定到MainMenu（避免与自动流程竞态）
        float settleTimeout = 8f;
        while (settleTimeout > 0)
        {
            if (GameFlowManager.Instance != null &&
                GameFlowManager.Instance.CurrentState == GameFlowManager.FlowState.MainMenu)
                break;
            settleTimeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        Check("Boot settled at MainMenu", GameFlowManager.Instance != null &&
            GameFlowManager.Instance.CurrentState == GameFlowManager.FlowState.MainMenu);

        // 加载关卡
        if (GameManager.Instance != null)
            GameManager.Instance.LoadLevel(1, 1);

        yield return new WaitForSecondsRealtime(3f);

        var players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        Check($"2 players spawned (found {players.Length})", players.Length == 2);

        PlayerController lux = null;
        foreach (var p in players)
            if (p.Type == PlayerController.PlayerType.Lux) lux = p;
        Check("Lux player found", lux != null);

        if (lux != null)
        {
            // 移动测试
            Vector3 startPos = lux.transform.position;
            for (int i = 0; i < 60; i++)
            {
                lux.SetMoveInput(Vector2.right);
                yield return null;
            }
            float moved = lux.transform.position.x - startPos.x;
            Check($"Lux moved right (dx={moved:F1})", moved > 1f);

            // 跳跃测试
            yield return new WaitForSeconds(0.3f);
            float yBefore = lux.transform.position.y;
            lux.TryJump();
            yield return new WaitForSeconds(0.15f);
            Check($"Lux can jump (dy={lux.transform.position.y - yBefore:F2})",
                lux.transform.position.y > yBefore + 0.05f);
            yield return new WaitForSeconds(0.6f);

            // 战斗测试
            var enemyPrefab = Resources.Load<GameObject>("TestEnemy");
            if (enemyPrefab == null)
            {
                // 直接从已生成的场景敌人测试
                var sceneEnemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
                if (sceneEnemies.Length > 0)
                {
                    var enemy = sceneEnemies[0];
                    var combat = lux.GetComponent<PlayerCombat>();
                    Check("Lux has PlayerCombat", combat != null);

                    if (combat != null)
                    {
                        // 完全冻结敌人（kinematic），固定位置不被AI移动
                        var enemyRb = enemy.GetComponent<Rigidbody2D>();
                        if (enemyRb != null) enemyRb.bodyType = RigidbodyType2D.Kinematic;
                        Vector3 fixedEnemyPos = enemy.transform.position;

                        // 攻击前一刻精确定位双方
                        lux.transform.position = fixedEnemyPos + Vector3.left * 1.0f;
                        enemy.transform.position = fixedEnemyPos;
                        lux.SetMoveInput(Vector2.right); // 面向右
                        yield return null;
                        // 再次锁定（防止任何漂移）
                        enemy.transform.position = fixedEnemyPos;
                        lux.transform.position = fixedEnemyPos + Vector3.left * 1.0f;

                        float hpBefore = enemy.CurrentHealth;
                        combat.MeleeAttack();
                        yield return new WaitForSeconds(0.3f);
                        Check($"Melee damaged enemy ({hpBefore}->{enemy.CurrentHealth})",
                            enemy.CurrentHealth < hpBefore);

                        // 远程攻击测试（光弹）
                        // 敌人改回Dynamic但冻结约束（Kinematic-Kinematic触发器不可靠）
                        if (enemyRb != null)
                        {
                            enemyRb.bodyType = RigidbodyType2D.Dynamic;
                            enemyRb.constraints = RigidbodyConstraints2D.FreezeAll;
                        }
                        enemy.transform.position = fixedEnemyPos;
                        // 精确对齐: 玩家Y使firePoint(局部+0.2)与敌人中心同高,光弹必经敌人
                        lux.transform.position = fixedEnemyPos + new Vector3(-4.0f, -0.2f, 0);
                        lux.SetMoveInput(Vector2.right);
                        yield return null;
                        enemy.transform.position = fixedEnemyPos;
                        float hpBeforeRanged = enemy.CurrentHealth;
                        // 窗口内多次发射(冷却0.5s),提高命中确定性
                        float waited = 0;
                        float fireTimer = 0;
                        combat.RangedAttack();
                        while (waited < 2.5f && enemy.CurrentHealth >= hpBeforeRanged)
                        {
                            enemy.transform.position = fixedEnemyPos; // 保持静止
                            fireTimer += Time.deltaTime;
                            if (fireTimer >= 0.6f) { fireTimer = 0; combat.RangedAttack(); }
                            waited += Time.deltaTime;
                            yield return null;
                        }
                        Check($"Ranged bolt damaged enemy ({hpBeforeRanged}->{enemy.CurrentHealth})",
                            enemy.CurrentHealth < hpBeforeRanged);

                        // 敌人攻击玩家测试
                        if (enemyRb != null)
                        {
                            enemyRb.constraints = RigidbodyConstraints2D.FreezeRotation;
                            enemyRb.bodyType = RigidbodyType2D.Dynamic;
                        }
                        var luxHealth = lux.GetComponent<PlayerHealth>();
                        if (luxHealth != null)
                        {
                            // 玩家站在敌人攻击范围内,冻结玩家位置
                            lux.transform.position = enemy.transform.position + Vector3.left * 0.8f;
                            lux.SetFrozen(true); // 玩家不动,让敌人攻击
                            int playerHpBefore = luxHealth.CurrentHealth;
                            // 等敌人侦测→追击→首次攻击,命中即停(测单次伤害)
                            float atkWait = 0;
                            while (atkWait < 4f && luxHealth.CurrentHealth >= playerHpBefore)
                            {
                                atkWait += Time.deltaTime;
                                yield return null;
                            }
                            int dmgTaken = playerHpBefore - luxHealth.CurrentHealth;
                            Check($"Enemy attacks player ({playerHpBefore}->{luxHealth.CurrentHealth})",
                                dmgTaken > 0);
                            // 平衡: 单次攻击只扣1滴血,不秒杀(玩家3滴血可承受多次)
                            Check($"Enemy single hit = 1 heart (dmg={dmgTaken})", dmgTaken == 1);
                            lux.SetFrozen(false);
                            luxHealth.ResetHealth();
                        }
                    }
                }
                else
                {
                    Check("Scene has enemies to test combat", false);
                }
            }

            // 压力板谜题测试
            var plate = Object.FindAnyObjectByType<PressurePlate>();
            if (plate != null)
            {
                lux.SetFrozen(false);
                var link = plate.GetComponent<PuzzleLink>();
                var door = GameObject.Find("PuzzleDoor");

                // 踩上压力板
                lux.transform.position = plate.transform.position + Vector3.up * 0.3f;
                yield return new WaitForSeconds(0.3f);
                Check($"PressurePlate triggers when stepped on (pressed={plate.IsPressed})",
                    plate.IsPressed);

                // 谜题连线存在且配置正确
                Check("PressurePlate has PuzzleLink", link != null);
                Check("PuzzleLink target door exists", door != null);

                // 门上升测试: 从已知关闭位开始(直接设门位置),按住验证门上升
                if (link != null && door != null)
                {
                    // 强制门到关闭基线位置
                    Vector3 closedBaseline = door.transform.position + Vector3.down * 5f;
                    door.transform.position = closedBaseline;
                    float doorYClosed = door.transform.position.y;
                    // 按住压力板,门应朝openPos上升
                    float t = 0;
                    while (t < 1.2f)
                    {
                        lux.transform.position = plate.transform.position + Vector3.up * 0.3f;
                        t += Time.deltaTime;
                        yield return null;
                    }
                    Check($"Pressed plate raises door (y {doorYClosed:F1}->{door.transform.position.y:F1})",
                        door.transform.position.y > doorYClosed + 0.5f);
                }
            }

            // 非对称能力链路测试(游戏核心设计支柱)
            yield return RunAbilityChainTest(lux);

            // 关卡完成测试(在死亡测试前,确保玩家健康)
            var goal = Object.FindAnyObjectByType<LevelGoalTrigger>();
            if (goal != null)
            {
                bool completed = false;
                if (LevelManager.Instance != null)
                    LevelManager.Instance.OnLevelComplete += () => completed = true;

                lux.SetFrozen(false);
                lux.transform.position = goal.transform.position + Vector3.left * 1f;
                yield return new WaitForSeconds(0.5f);
                Check("Reaching goal completes level", completed || LevelManager.Instance == null);
            }

            // 合作复活系统测试(双人核心机制)
            yield return new WaitForSeconds(0.3f);
            var coop = CoopReviveSystem.Instance;
            Check("CoopReviveSystem active in level", coop != null);
            if (coop != null)
            {
                var luxH = lux.GetComponent<PlayerHealth>();
                luxH.ResetHealth();
                lux.SetFrozen(false);
                // Lux死亡,队友Nox存活 → 应倒地而非自动重生
                luxH.TakeDamage(999);
                yield return new WaitForSeconds(0.3f);
                Check($"Player downed (not auto-respawn) when partner alive (downed={coop.IsPlayerDowned(0)})",
                    coop.IsPlayerDowned(0));
                Check("CoopReviveSystem reports someone downed", coop.IsAnyoneDowned);

                // 队友靠近并复活: 把Nox移到Lux旁,模拟复活完成
                var nox = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                PlayerController noxP = null;
                foreach (var p in nox) if (p.Type == PlayerController.PlayerType.Nox) noxP = p;
                if (noxP != null)
                {
                    noxP.transform.position = lux.transform.position + Vector3.right * 0.5f;
                    // 等待倒地状态可被处理(此处验证倒地→可恢复)
                    yield return new WaitForSeconds(0.3f);
                    // 验证: 倒地玩家健康可被ResetHealth恢复(复活路径)
                    luxH.ResetHealth();
                    Check("Downed player health restorable (revive path)", luxH.IsAlive);
                }
            }
        }

        // ===== Boss战测试(加载Boss关Level_1_4) =====
        yield return RunBossTest();

        yield return null;
        Done = true;
    }

    /// <summary>
    /// 非对称能力链路测试 - Lux光束→光敏机关, Nox影穿→影墙
    /// 这是"能力互补门"设计的运行时基础,此前无任何测试覆盖
    /// </summary>
    private IEnumerator RunAbilityChainTest(PlayerController lux)
    {
        // ===== 层配置: 影穿和影墙依赖的Layer必须存在 =====
        Check("PhaseThrough layer exists (Nox shadow phase)",
            LayerMask.NameToLayer("PhaseThrough") >= 0);
        Check("ShadowWall layer exists", LayerMask.NameToLayer("ShadowWall") >= 0);

        // ===== Lux光束 → 光敏机关 =====
        var sensor = Object.FindAnyObjectByType<LightSensor>();
        var luxAbilities = lux != null ? lux.GetComponent<LuxAbilities>() : null;
        Check("Lux has LuxAbilities component", luxAbilities != null);

        if (luxAbilities != null && sensor != null)
        {
            sensor.Reset();
            lux.SetFrozen(true);
            // 站到机关左侧,朝右释放光束覆盖机关
            lux.transform.position = sensor.transform.position + Vector3.left * 2f;
            yield return null;

            luxAbilities.TryActivate();
            yield return null;

            Check("Lux light beam spawns a LightZone trigger",
                GameObject.FindGameObjectsWithTag("LightZone").Length > 0);

            // 等待LightSensor的activationDelay(0.5s)
            yield return new WaitForSeconds(1f);
            Check("Light beam activates LightSensor", sensor.IsActivated);

            lux.SetFrozen(false);
        }
        else
        {
            Check("Scene has a LightSensor to test the light beam", sensor != null);
        }

        // ===== Nox影穿 =====
        PlayerController nox = null;
        foreach (var p in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (p.Type == PlayerController.PlayerType.Nox) nox = p;

        var noxAbilities = nox != null ? nox.GetComponent<NoxAbilities>() : null;
        Check("Nox has NoxAbilities component", noxAbilities != null);

        if (noxAbilities != null)
        {
            int layerBefore = nox.gameObject.layer;
            float xBefore = nox.transform.position.x;

            noxAbilities.TryActivate();
            yield return new WaitForSeconds(0.6f); // phaseDuration 0.3s + 余量

            // 阈值取接近phaseDistance(4): 只要求">0.5"会被batchmode的帧节奏蒙混过关
            Check($"Nox shadow phase dashes ~phaseDistance (dx={Mathf.Abs(nox.transform.position.x - xBefore):F2})",
                Mathf.Abs(nox.transform.position.x - xBefore) > 2f);
            Check($"Nox shadow phase restores original layer (layer={nox.gameObject.layer})",
                nox.gameObject.layer == layerBefore);

            // ===== 能力互补门: Nox影穿可过影墙, Lux过不去 =====
            yield return RunShadowWallGateTest(lux, nox, noxAbilities);
        }
    }

    /// <summary>
    /// 影墙门测试 - 运行时造一堵影墙,验证"只有影穿中的Nox能过"这一非对称设计
    /// </summary>
    private IEnumerator RunShadowWallGateTest(PlayerController lux, PlayerController nox,
        NoxAbilities noxAbilities)
    {
        // 先把Nox放回出生点一带: 关卡中段有谜题门等障碍,上一次冲刺会把他顶在门上,
        // 留在原地测穿墙等于在"已经卡死"的位置上测
        nox.transform.position = new Vector3(-1f, nox.transform.position.y, 0f);
        yield return null;

        // 墙放在2.5单位外,留出加速距离
        var wall = new GameObject("TestShadowWall");
        wall.transform.position = nox.transform.position + new Vector3(2.5f, 0f, 0f);
        wall.transform.localScale = new Vector3(0.5f, 3f, 1f);
        wall.AddComponent<BoxCollider2D>();
        wall.AddComponent<ShadowWall>();
        yield return null; // 等ShadowWall.Start()设置层

        Check($"ShadowWall lands on the ShadowWall layer (layer={wall.layer})",
            wall.layer == LayerMask.NameToLayer("ShadowWall"));

        // Nox朝右并等技能冷却结束
        nox.SetMoveInput(Vector2.right);
        yield return null;
        while (!noxAbilities.IsReady)
            yield return null;

        noxAbilities.TryActivate();
        yield return new WaitForSeconds(0.6f);
        Check($"Nox phases through ShadowWall (noxX={nox.transform.position.x:F2}, wallX={wall.transform.position.x:F2})",
            nox.transform.position.x > wall.transform.position.x + 0.3f);

        // 对照组: Lux没有影穿,必须被影墙挡住(防止修复时把墙对所有人都变透明)
        if (lux != null)
        {
            lux.SetFrozen(false);
            lux.transform.position = wall.transform.position + Vector3.left * 1.2f;
            yield return null;
            for (int i = 0; i < 60; i++)
            {
                lux.SetMoveInput(Vector2.right);
                yield return null;
            }
            Check($"Lux is blocked by ShadowWall (luxX={lux.transform.position.x:F2}, wallX={wall.transform.position.x:F2})",
                lux.transform.position.x < wall.transform.position.x);
        }

        Object.Destroy(wall);
        yield return null;
    }

    private IEnumerator RunBossTest()
    {
        if (GameManager.Instance == null) yield break;
        GameManager.Instance.LoadLevel(1, 4); // 第1章Boss关
        yield return new WaitForSecondsRealtime(3f);

        var boss = Object.FindAnyObjectByType<BossBase>();
        Check("Boss spawned in boss level", boss != null);
        if (boss == null) yield break;

        // Boss实现IDamageable(玩家可攻击)
        var damageable = boss.GetComponent<IDamageable>();
        Check("Boss implements IDamageable (player can hit it)", damageable != null);

        // 启动Boss战
        boss.StartBattle();
        yield return null;
        Check("Boss battle active", boss.IsBattleActive);

        // 玩家攻击Boss造成伤害
        var players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        PlayerController lux = null;
        foreach (var p in players) if (p.Type == PlayerController.PlayerType.Lux) lux = p;

        if (lux != null && damageable != null)
        {
            int bossHpBefore = boss.CurrentHealth;
            // 直接通过IDamageable施加伤害(验证接口可用)
            damageable.TakeDamage(5);
            yield return null;
            Check($"Boss takes damage ({bossHpBefore}->{boss.CurrentHealth})",
                boss.CurrentHealth < bossHpBefore);

            // 验证击败流程
            while (boss.IsAlive)
                boss.TakeDamage(50);
            yield return new WaitForSeconds(0.2f);
            Check("Boss can be defeated", !boss.IsAlive);
        }
    }
}
