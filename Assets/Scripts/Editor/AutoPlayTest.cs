using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 自动Play测试 - 打开Boot场景并进入Play模式
/// 在Console中捕获运行时错误
/// </summary>
public static class AutoPlayTest
{
    [MenuItem("DoubleForward/▶ Play Boot Scene", false, 1)]
    public static void PlayBootScene()
    {
        // 保存当前场景
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        // 打开Boot场景
        string bootPath = "Assets/Scenes/Boot.unity";
        if (!System.IO.File.Exists(bootPath))
        {
            Debug.LogError("[AutoPlay] Boot.unity not found!");
            return;
        }

        EditorSceneManager.OpenScene(bootPath);
        Debug.Log("[AutoPlay] Opened Boot scene. Entering Play mode...");

        // 注册Play模式回调以监听运行时错误
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        // 进入Play模式
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Debug.Log("[AutoPlay] ▶ Play mode started! Watch Console for errors.");
            Debug.Log("[AutoPlay] Expected flow: GameInitializer → spawn managers → GameFlowManager → load MainMenu");
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            Debug.Log("[AutoPlay] ■ Play mode ended.");
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }
    }
}
