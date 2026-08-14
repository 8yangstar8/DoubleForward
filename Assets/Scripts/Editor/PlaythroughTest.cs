using UnityEngine;
using UnityEditor;

/// <summary>
/// 可玩性测试入口 - 只跑"按真人操作走通整关"的测试。
///
/// 为什么单独一个入口: 这类测试必须按真实时间走完整关(每关几十秒,还要切场景),
/// 混在 AutoPlayIntegrationTest 的快速回归套件里会把总时长拖过十分钟并超时,
/// 导致整套结果都拿不到。拆开后快速套件继续保持可迭代的时长。
///
/// 用法: -executeMethod PlaythroughTest.RunFromCommandLine
/// </summary>
public static class PlaythroughTest
{
    private static double startTime;
    private static bool runnerSpawned;

    public static void RunFromCommandLine()
    {
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions =
            EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

        AutoPlayTestRunner.WalkthroughOnly = true;

        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity");
        EditorApplication.update += OnUpdate;
        startTime = EditorApplication.timeSinceStartup;
        EditorApplication.EnterPlaymode();
    }

    private static void OnUpdate()
    {
        if (EditorApplication.isPlaying && !runnerSpawned)
        {
            if (Application.isPlaying)
            {
                runnerSpawned = true;
                var go = new GameObject("__PlaythroughRunner");
                go.AddComponent<AutoPlayTestRunner>();
                Debug.Log("[PLAYTHROUGH] runner spawned");
            }
            return;
        }

        // 走完两关约需2分钟,留足余量
        if (EditorApplication.timeSinceStartup - startTime > 300)
        {
            Debug.LogError("[PLAYTHROUGH] TIMEOUT");
            Finish(false);
            return;
        }

        if (runnerSpawned && AutoPlayTestRunner.Done)
        {
            foreach (var r in AutoPlayTestRunner.Results)
                Debug.Log($"[PLAYTHROUGH] {r}");
            Debug.Log($"[PLAYTHROUGH] {AutoPlayTestRunner.Passed}/{AutoPlayTestRunner.Total} passed");
            Finish(AutoPlayTestRunner.Passed == AutoPlayTestRunner.Total);
        }
    }

    private static void Finish(bool success)
    {
        EditorApplication.update -= OnUpdate;
        AutoPlayTestRunner.WalkthroughOnly = false;
        if (!success) Debug.LogError("[PLAYTHROUGH] PLAYABILITY TEST FAILED");
        else Debug.Log("[PLAYTHROUGH] LEVELS ARE WALKABLE");

        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
    }
}
