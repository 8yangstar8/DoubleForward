using UnityEngine;
using UnityEditor;

/// <summary>
/// 全量走通扫描 - 把每章的 1/2/3 关都真的从出生点走一遍。
///
/// 单独一个入口而不是并进 PlaythroughTest: 15关按真实时间走要好几分钟,
/// 塞进默认套件会把它拖过超时上限,结果一条都拿不到。这个入口按需手动跑,
/// 比如改完关卡模板、改完地形之后。
///
/// x_4 是 Boss 关,通关要靠"Lux光束打弱点 + 输出"的配合流程,
/// 不是走过去就行,不在这套逻辑覆盖范围内。
///
/// 用法: -executeMethod AllLevelsWalkthroughTest.RunFromCommandLine
/// </summary>
public static class AllLevelsWalkthroughTest
{
    private const double TimeoutSeconds = 900;   // 15关 x 每关最多55秒,留足余量

    private static double startTime;
    private static bool runnerSpawned;

    public static void RunFromCommandLine()
    {
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions =
            EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

        AutoPlayTestRunner.WalkthroughOnly = true;
        AutoPlayTestRunner.AllLevels = true;

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
                var go = new GameObject("__AllLevelsRunner");
                go.AddComponent<AutoPlayTestRunner>();
                Debug.Log("[ALLWALK] runner spawned");
            }
            return;
        }

        if (EditorApplication.timeSinceStartup - startTime > TimeoutSeconds)
        {
            // 超时也要把已经跑出来的结果打出来 —— 光报一句 TIMEOUT
            // 等于把前面十几关的信息全扔了
            foreach (var r in AutoPlayTestRunner.Results) Debug.Log($"[ALLWALK] {r}");
            Debug.LogError("[ALLWALK] TIMEOUT");
            Finish(false);
            return;
        }

        if (runnerSpawned && AutoPlayTestRunner.Done)
        {
            foreach (var r in AutoPlayTestRunner.Results) Debug.Log($"[ALLWALK] {r}");
            Debug.Log($"[ALLWALK] {AutoPlayTestRunner.Passed}/{AutoPlayTestRunner.Total} passed");
            Finish(AutoPlayTestRunner.Passed == AutoPlayTestRunner.Total);
        }
    }

    private static void Finish(bool success)
    {
        EditorApplication.update -= OnUpdate;
        AutoPlayTestRunner.WalkthroughOnly = false;
        AutoPlayTestRunner.AllLevels = false;
        if (!success) Debug.LogError("[ALLWALK] SOME LEVELS ARE NOT WALKABLE");
        else Debug.Log("[ALLWALK] ALL SCANNED LEVELS ARE WALKABLE");

        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
    }
}
