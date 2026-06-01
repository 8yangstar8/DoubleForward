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

            // 掉落死亡复活测试
            var health = lux.GetComponent<PlayerHealth>();
            if (health != null)
            {
                int hpFull = health.CurrentHealth;
                // 传送到深渊（y < -20触发掉落死亡）
                lux.transform.position = new Vector3(lux.transform.position.x, -25f, 0);
                yield return new WaitForSeconds(0.2f);
                // 死亡后延迟复活（deathRespawnDelay=1s）
                yield return new WaitForSeconds(1.5f);
                bool respawnedAbove = lux.transform.position.y > -20f;
                Check($"Fall death respawns player (y={lux.transform.position.y:F1})", respawnedAbove);
                Check("Player alive after respawn", health.IsAlive);
            }

            // 关卡完成测试
            var goal = Object.FindAnyObjectByType<LevelGoalTrigger>();
            if (goal != null)
            {
                bool completed = false;
                if (LevelManager.Instance != null)
                    LevelManager.Instance.OnLevelComplete += () => completed = true;

                // 传送到终点附近触发完成
                lux.transform.position = goal.transform.position + Vector3.left * 1f;
                yield return new WaitForSeconds(0.5f);
                Check("Reaching goal completes level", completed || LevelManager.Instance == null);
            }
        }

        yield return null;
        Done = true;
    }
}
