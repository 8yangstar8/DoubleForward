using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 运行时系统验证器 - 在Play前检查所有系统是否正确配置
/// 自动作为PlayMode回调执行，也可手动运行
/// </summary>
[InitializeOnLoad]
public static class RuntimeSystemValidator
{
    static RuntimeSystemValidator()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // 仅在Boot场景启动时检查
            string currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;
            if (currentScene == "Boot")
            {
                var errors = RunQuickValidation();
                if (errors.Count > 0)
                {
                    string msg = "发现以下问题：\n\n";
                    foreach (var err in errors.Take(10))
                        msg += $"• {err}\n";

                    if (errors.Count > 10)
                        msg += $"\n... 还有 {errors.Count - 10} 个问题";

                    Debug.LogWarning($"[Validator] {errors.Count} issues found before Play:\n{msg}");
                }
            }
        }
    }

    [MenuItem("DoubleForward/Validate Project", false, 2)]
    public static void ValidateFromMenu()
    {
        var errors = RunFullValidation();

        if (errors.Count == 0)
        {
            EditorUtility.DisplayDialog("验证通过 ✅",
                "所有系统检查通过！项目可以正常运行。", "OK");
            Debug.Log("[Validator] All checks passed!");
        }
        else
        {
            string msg = "";
            foreach (var err in errors)
                msg += $"• {err}\n";

            EditorUtility.DisplayDialog($"发现 {errors.Count} 个问题 ⚠️", msg, "OK");
            Debug.LogWarning($"[Validator] {errors.Count} issues found:\n{msg}");
        }
    }

    // ==================== 快速验证（Play前） ====================

    private static List<string> RunQuickValidation()
    {
        var errors = new List<string>();

        // 检查Boot场景中有GameInitializer
        var initializer = Object.FindAnyObjectByType<GameInitializer>();
        if (initializer == null)
        {
            errors.Add("Boot场景缺少GameInitializer组件");
            return errors;
        }

        // 检查关键Manager预制体是否已关联
        var so = new SerializedObject(initializer);
        CheckPrefabRef(so, "gameManagerPrefab", "GameManager", errors);
        CheckPrefabRef(so, "audioManagerPrefab", "AudioManager", errors);
        CheckPrefabRef(so, "inputManagerPrefab", "InputManager", errors);
        CheckPrefabRef(so, "saveSystemPrefab", "SaveSystem", errors);
        CheckPrefabRef(so, "gameFlowPrefab", "GameFlowManager", errors);

        return errors;
    }

    // ==================== 完整验证（手动） ====================

    private static List<string> RunFullValidation()
    {
        var errors = new List<string>();

        EditorUtility.DisplayProgressBar("Validating...", "Checking scenes...", 0.1f);
        ValidateScenes(errors);

        EditorUtility.DisplayProgressBar("Validating...", "Checking prefabs...", 0.3f);
        ValidatePrefabs(errors);

        EditorUtility.DisplayProgressBar("Validating...", "Checking ScriptableObjects...", 0.5f);
        ValidateScriptableObjects(errors);

        EditorUtility.DisplayProgressBar("Validating...", "Checking layers...", 0.7f);
        ValidateLayers(errors);

        EditorUtility.DisplayProgressBar("Validating...", "Checking Build Settings...", 0.9f);
        ValidateBuildSettings(errors);

        EditorUtility.ClearProgressBar();
        return errors;
    }

    // ==================== 各项检查 ====================

    private static void ValidateScenes(List<string> errors)
    {
        string[] requiredScenes = {
            "Assets/Scenes/Boot.unity",
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Loading.unity"
        };

        foreach (var scene in requiredScenes)
        {
            if (!System.IO.File.Exists(scene))
                errors.Add($"缺少必要场景: {scene}");
        }

        // 检查至少有一个关卡场景
        bool hasLevel = false;
        for (int ch = 1; ch <= 5; ch++)
        {
            if (System.IO.File.Exists($"Assets/Scenes/Chapter{ch}/Level_{ch}_1.unity"))
            {
                hasLevel = true;
                break;
            }
        }
        if (!hasLevel)
            errors.Add("未找到任何关卡场景");
    }

    private static void ValidatePrefabs(List<string> errors)
    {
        // 玩家预制体
        CheckPrefabExists("Assets/Prefabs/Player/Lux.prefab", "Lux玩家预制体", errors);
        CheckPrefabExists("Assets/Prefabs/Player/Nox.prefab", "Nox玩家预制体", errors);

        // 玩家预制体组件检查
        CheckPrefabComponent<PlayerController>("Assets/Prefabs/Player/Lux.prefab", errors);
        CheckPrefabComponent<PlayerHealth>("Assets/Prefabs/Player/Lux.prefab", errors);
        CheckPrefabComponent<PlayerController>("Assets/Prefabs/Player/Nox.prefab", errors);
        CheckPrefabComponent<PlayerHealth>("Assets/Prefabs/Player/Nox.prefab", errors);

        // Manager预制体
        string[] criticalManagers = {
            "GameManager", "AudioManager", "InputManager", "SaveSystem",
            "GameFlowManager", "LocalizationSystem", "PerformanceManager"
        };

        foreach (var mgr in criticalManagers)
        {
            CheckPrefabExists($"Assets/Prefabs/Managers/{mgr}.prefab", $"{mgr}预制体", errors);
        }
    }

    private static void ValidateScriptableObjects(List<string> errors)
    {
        string dir = "Assets/ScriptableObjects/LevelData";
        if (!System.IO.Directory.Exists(dir))
        {
            errors.Add("缺少LevelData目录");
            return;
        }

        int count = System.IO.Directory.GetFiles(dir, "*.asset").Length;
        if (count < 20)
            errors.Add($"LevelData不完整: 找到{count}/20个");
    }

    private static void ValidateLayers(List<string> errors)
    {
        string[] requiredLayers = { "Ground", "Player", "Enemy" };
        foreach (var layer in requiredLayers)
        {
            if (LayerMask.NameToLayer(layer) == -1)
                errors.Add($"缺少Layer: {layer}（执行 Setup Layers & Tags）");
        }
    }

    private static void ValidateBuildSettings(List<string> errors)
    {
        var scenes = EditorBuildSettings.scenes;
        if (scenes.Length == 0)
        {
            errors.Add("Build Settings中没有注册场景");
            return;
        }

        bool hasBoot = scenes.Any(s => s.path.Contains("Boot.unity") && s.enabled);
        if (!hasBoot)
            errors.Add("Boot场景未在Build Settings中注册为首场景");

        // 检查Boot是否为index 0
        if (scenes.Length > 0 && !scenes[0].path.Contains("Boot.unity"))
            errors.Add("Boot场景应该是Build Settings中的第一个场景");
    }

    // ==================== 辅助 ====================

    private static void CheckPrefabRef(SerializedObject so, string propName, string displayName,
        List<string> errors)
    {
        var prop = so.FindProperty(propName);
        if (prop == null || prop.objectReferenceValue == null)
            errors.Add($"GameInitializer缺少{displayName}预制体引用");
    }

    private static void CheckPrefabExists(string path, string displayName, List<string> errors)
    {
        if (!System.IO.File.Exists(path))
            errors.Add($"缺少{displayName}: {path}");
    }

    private static void CheckPrefabComponent<T>(string prefabPath, List<string> errors) where T : Component
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        if (prefab.GetComponent<T>() == null)
            errors.Add($"{prefab.name} 缺少 {typeof(T).Name} 组件");
    }
}
