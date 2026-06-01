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
                    // 传送玩家到敌人左侧（敌人需在攻击点前方，留出攻击点偏移0.8）
                    lux.transform.position = enemy.transform.position + Vector3.left * 1.4f;
                    lux.SetMoveInput(Vector2.right); // 面向右
                    yield return null;
                    yield return null;

                    float hpBefore = enemy.CurrentHealth;
                    var combat = lux.GetComponent<PlayerCombat>();
                    Check("Lux has PlayerCombat", combat != null);
                    if (combat != null)
                    {
                        combat.MeleeAttack();
                        yield return new WaitForSeconds(0.3f);
                        Check($"Melee damaged enemy ({hpBefore}->{enemy.CurrentHealth})",
                            enemy.CurrentHealth < hpBefore);
                    }
                }
                else
                {
                    Check("Scene has enemies to test combat", false);
                }
            }
        }

        yield return null;
        Done = true;
    }
}
