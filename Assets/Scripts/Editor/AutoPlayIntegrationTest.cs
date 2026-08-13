using UnityEngine;
using UnityEditor;

/// <summary>
/// 自动Play集成测试驱动（Editor）- 批处理模式进入Play模式，
/// 驱动AutoPlayTestRunner执行，读取结果并退出
/// 用法: -executeMethod AutoPlayIntegrationTest.RunFromCommandLine
/// </summary>
public static class AutoPlayIntegrationTest
{
    private static double startTime;
    private static bool runnerSpawned;

    public static void RunFromCommandLine()
    {
        // 禁用域重载，让静态update订阅在进入Play后存活
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions =
            EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity");
        EditorApplication.update += OnUpdate;
        startTime = EditorApplication.timeSinceStartup;
        EditorApplication.EnterPlaymode();
    }

    private static void OnUpdate()
    {
        // 进入Play后生成测试运行器
        if (EditorApplication.isPlaying && !runnerSpawned)
        {
            // 等待场景真正进入Play
            if (Application.isPlaying)
            {
                runnerSpawned = true;
                var go = new GameObject("__AutoPlayTestRunner");
                go.AddComponent<AutoPlayTestRunner>();
                Debug.Log("[AUTOPLAY] Test runner spawned");
            }
            return;
        }

        // 超时保护(走通整关本身最多40秒,留足余量)
        if (EditorApplication.timeSinceStartup - startTime > 180)
        {
            Debug.LogError("[AUTOPLAY] TIMEOUT");
            Finish(false);
            return;
        }

        // 检查测试完成
        if (runnerSpawned && AutoPlayTestRunner.Done)
        {
            foreach (var r in AutoPlayTestRunner.Results)
                Debug.Log($"[AUTOPLAY] {r}");
            Debug.Log($"[AUTOPLAY] {AutoPlayTestRunner.Passed}/{AutoPlayTestRunner.Total} passed");
            Finish(AutoPlayTestRunner.Passed == AutoPlayTestRunner.Total);
        }
    }

    private static void Finish(bool success)
    {
        EditorApplication.update -= OnUpdate;
        if (!success) Debug.LogError("[AUTOPLAY] INTEGRATION TEST FAILED");
        else Debug.Log("[AUTOPLAY] ALL INTEGRATION TESTS PASSED");

        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
    }
}
