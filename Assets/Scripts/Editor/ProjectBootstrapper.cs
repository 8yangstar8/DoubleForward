using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 项目总引导向导 - 一键创建完整可运行项目
/// 整合所有工厂：占位精灵→动画→预制体→ScriptableObject→场景→Build Settings
/// 菜单：DoubleForward / Project Setup Wizard
/// </summary>
public class ProjectBootstrapper : EditorWindow
{
    private Vector2 scroll;
    private bool step1Done, step2Done, step3Done, step4Done, step5Done;
    private string statusMessage = "";
    private MessageType statusType = MessageType.None;

    [MenuItem("DoubleForward/Project Setup Wizard", false, 0)]
    public static void ShowWindow()
    {
        var window = GetWindow<ProjectBootstrapper>("Project Setup Wizard");
        window.minSize = new Vector2(520, 680);
        window.CheckStatus();
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        // 标题
        EditorGUILayout.Space(10);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        GUILayout.Label("双向前行 — 项目构建向导", titleStyle);
        EditorGUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "此向导将自动创建项目运行所需的全部资产：\n" +
            "占位精灵 → 动画控制器 → 预制体 → 数据资产 → 场景\n\n" +
            "按顺序执行每个步骤，或点击底部\"一键全部构建\"。\n" +
            "绿色 = 已完成，可安全重复执行（已有文件不会覆盖）。",
            MessageType.Info);

        EditorGUILayout.Space(15);

        // ====== Step 1: 占位精灵 ======
        DrawStep(1, "生成占位精灵", "创建角色、敌人、Boss、谜题、UI等全部占位精灵图（PNG）",
            step1Done, "Assets/Art/Placeholders/Characters/Lux.png",
            () => {
                PlaceholderSpriteGenerator.GenerateAll();
                step1Done = true;
            });

        // ====== Step 2: 动画控制器 ======
        DrawStep(2, "创建动画控制器", "为Lux/Nox、4种敌人、5个Boss创建Animator Controller + 动画片段",
            step2Done, "Assets/Animations/Controllers/LuxController.controller",
            () => {
                AnimatorFactory.CreateAll();
                step2Done = true;
            });

        // ====== Step 3: 预制体 ======
        DrawStep(3, "创建全部预制体", "玩家(2) + 敌人(5) + Boss(8) + 谜题(9) + 环境(8) + 管理器(45+) + VFX(7) + UI(7)",
            step3Done, "Assets/Prefabs/Player/Lux.prefab",
            () => {
                PrefabFactory.CreateAll();
                step3Done = true;
            });

        // ====== Step 4: ScriptableObject ======
        DrawStep(4, "创建数据资产", "LevelData(20) + LevelConfig(20) + AbilityData(8) + ChapterStory(5) + Catalog(1)",
            step4Done, "Assets/ScriptableObjects/LevelData",
            () => {
                ScriptableObjectFactory.CreateAll();
                step4Done = true;
            });

        // ====== Step 5: 场景 ======
        DrawStep(5, "创建全部场景", "Boot + 主菜单 + 加载 + 20关卡(含Boss) + Boss连战 + 制作名单 = 25个场景",
            step5Done, "Assets/Scenes/Boot.unity",
            () => {
                SceneFactory.CreateAll();
                step5Done = true;
            });

        EditorGUILayout.Space(20);

        // ====== 一键全部 ======
        DrawSeparator();
        EditorGUILayout.Space(10);

        GUI.backgroundColor = AllDone() ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.3f, 0.7f, 1f);
        GUIStyle bigButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };

        if (GUILayout.Button(AllDone() ? "✅ 全部完成!" : "⚡ 一键全部构建", bigButtonStyle, GUILayout.Height(55)))
        {
            if (!AllDone())
                RunFullSetup();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);

        // 状态消息
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        EditorGUILayout.Space(10);

        // ====== 统计 ======
        DrawSeparator();
        EditorGUILayout.Space(5);
        GUILayout.Label("项目统计", EditorStyles.boldLabel);
        DrawStatRow("C# 脚本", CountFiles("Assets/Scripts", "*.cs").ToString());
        DrawStatRow("预制体", CountFiles("Assets/Prefabs", "*.prefab").ToString());
        DrawStatRow("场景", CountFiles("Assets/Scenes", "*.unity").ToString());
        DrawStatRow("SO 资产", CountFiles("Assets/ScriptableObjects", "*.asset").ToString());
        DrawStatRow("动画片段", CountFiles("Assets/Animations/Clips", "*.anim").ToString());
        DrawStatRow("占位精灵", CountFiles("Assets/Art/Placeholders", "*.png").ToString());

        EditorGUILayout.Space(10);

        // ====== 快捷操作 ======
        DrawSeparator();
        EditorGUILayout.Space(5);
        GUILayout.Label("快捷操作", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("打开Boot场景", GUILayout.Height(30)))
            OpenScene("Assets/Scenes/Boot.unity");
        if (GUILayout.Button("打开主菜单场景", GUILayout.Height(30)))
            OpenScene("Assets/Scenes/MainMenu.unity");
        if (GUILayout.Button("打开1-1关卡", GUILayout.Height(30)))
            OpenScene("Assets/Scenes/Chapter1/Level_1_1.unity");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("配置GameInitializer引用", GUILayout.Height(30)))
            AutoWireGameInitializer();
        if (GUILayout.Button("刷新状态", GUILayout.Height(30)))
            CheckStatus();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    // ==================== 核心流程 ====================

    private void RunFullSetup()
    {
        var startTime = System.DateTime.Now;

        try
        {
            if (!step1Done)
            {
                PlaceholderSpriteGenerator.GenerateAll();
                step1Done = true;
            }

            if (!step2Done)
            {
                AnimatorFactory.CreateAll();
                step2Done = true;
            }

            if (!step3Done)
            {
                PrefabFactory.CreateAll();
                step3Done = true;
            }

            if (!step4Done)
            {
                ScriptableObjectFactory.CreateAll();
                step4Done = true;
            }

            if (!step5Done)
            {
                SceneFactory.CreateAll();
                step5Done = true;
            }

            // 自动关联GameInitializer
            AutoWireGameInitializer();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var elapsed = (System.DateTime.Now - startTime).TotalSeconds;
            statusMessage = $"✅ 全部构建完成！耗时 {elapsed:F1} 秒\n\n" +
                "下一步：\n" +
                "1. 打开 Boot 场景\n" +
                "2. 在 GameInitializer 上关联Manager预制体\n" +
                "3. 按 Play 测试";
            statusType = MessageType.Info;

            EditorUtility.DisplayDialog("构建完成",
                $"所有资产已创建完毕！\n\n" +
                $"• 占位精灵\n• 动画控制器\n• {CountFiles("Assets/Prefabs", "*.prefab")} 个预制体\n" +
                $"• {CountFiles("Assets/ScriptableObjects", "*.asset")} 个数据资产\n" +
                $"• {CountFiles("Assets/Scenes", "*.unity")} 个场景\n\n" +
                "打开 Assets/Scenes/Boot.unity 开始测试！",
                "OK");
        }
        catch (System.Exception e)
        {
            statusMessage = $"❌ 构建出错: {e.Message}";
            statusType = MessageType.Error;
            Debug.LogError($"[Setup] Error: {e}");
            EditorUtility.ClearProgressBar();
        }
    }

    // ==================== GameInitializer自动关联 ====================

    private void AutoWireGameInitializer()
    {
        // 查找Boot场景中的GameInitializer
        string bootPath = "Assets/Scenes/Boot.unity";
        if (!File.Exists(bootPath))
        {
            statusMessage = "Boot场景不存在，请先创建场景";
            statusType = MessageType.Warning;
            return;
        }

        // 打开Boot场景
        var currentScene = EditorSceneManager.GetActiveScene().path;
        EditorSceneManager.OpenScene(bootPath);

        var initializer = Object.FindAnyObjectByType<GameInitializer>();
        if (initializer == null)
        {
            Debug.LogWarning("[Setup] GameInitializer not found in Boot scene");
            if (!string.IsNullOrEmpty(currentScene))
                EditorSceneManager.OpenScene(currentScene);
            return;
        }

        var so = new SerializedObject(initializer);
        int wiredCount = 0;

        // 自动关联所有Manager预制体
        string managerDir = "Assets/Prefabs/Managers";
        if (Directory.Exists(managerDir))
        {
            var prefabFiles = Directory.GetFiles(managerDir, "*.prefab");
            foreach (var file in prefabFiles)
            {
                string assetPath = file.Replace('\\', '/');
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                string prefabName = Path.GetFileNameWithoutExtension(assetPath);

                // 构建SerializedProperty名称映射
                string propName = GetPrefabPropertyName(prefabName);
                if (string.IsNullOrEmpty(propName)) continue;

                var prop = so.FindProperty(propName);
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = prefab;
                    wiredCount++;
                }
            }
        }

        so.ApplyModifiedProperties();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        statusMessage = $"已关联 {wiredCount} 个Manager预制体到GameInitializer";
        statusType = MessageType.Info;
        Debug.Log($"[Setup] Wired {wiredCount} manager prefabs to GameInitializer");
    }

    private string GetPrefabPropertyName(string prefabName)
    {
        // Manager名 → SerializedProperty名 映射
        var map = new System.Collections.Generic.Dictionary<string, string>
        {
            // 第1层
            { "ObjectPool", "objectPoolPrefab" },
            { "AudioManager", "audioManagerPrefab" },
            { "InputManager", "inputManagerPrefab" },
            // 第2层
            { "SaveSystem", "saveSystemPrefab" },
            { "LocalizationSystem", "localizationPrefab" },
            { "SettingsPersistence", "settingsPersistencePrefab" },
            { "GameStats", "gameStatsPrefab" },
            // 第3层
            { "GameManager", "gameManagerPrefab" },
            { "DifficultyManager", "difficultyPrefab" },
            { "AchievementSystem", "achievementSystemPrefab" },
            { "ComboSystem", "comboSystemPrefab" },
            { "CurrencyManager", "currencyManagerPrefab" },
            { "ScoreManager", "scoreManagerPrefab" },
            { "LeaderboardManager", "leaderboardManagerPrefab" },
            // 第4层
            { "PerformanceManager", "performancePrefab" },
            { "ScreenAdapter", "screenAdapterPrefab" },
            { "AccessibilityManager", "accessibilityPrefab" },
            { "GamepadAdapter", "gamepadAdapterPrefab" },
            { "AndroidPermissionManager", "permissionManagerPrefab" },
            { "HapticFeedback", "hapticFeedbackPrefab" },
            { "NotificationScheduler", "notificationSchedulerPrefab" },
            // 第5层
            { "VFXManager", "vfxManagerPrefab" },
            { "SoundFeedback", "soundFeedbackPrefab" },
            { "AnalyticsTracker", "analyticsTrackerPrefab" },
            { "MobileServices", "mobileServicesPrefab" },
            { "DailyRewardSystem", "dailyRewardSystemPrefab" },
            { "SkinManager", "skinManagerPrefab" },
            { "CloudSaveManager", "cloudSaveManagerPrefab" },
            { "TimeManager", "timeManagerPrefab" },
            { "AutoSaveSystem", "autoSaveSystemPrefab" },
            { "AchievementTracker", "achievementTrackerPrefab" },
            // 第5.5层
            { "MusicLayerSystem", "musicLayerSystemPrefab" },
            { "GameplayTipSystem", "gameplayTipSystemPrefab" },
            { "ChallengeMode", "challengeModePrefab" },
            { "PlayerCoopSync", "playerCoopSyncPrefab" },
            { "WorldProgressionManager", "worldProgressionPrefab" },
            { "EnemyDirector", "enemyDirectorPrefab" },
            { "ScreenEffectsController", "screenEffectsControllerPrefab" },
            { "EnvironmentEffectManager", "environmentEffectManagerPrefab" },
            { "SecretAreaSystem", "secretAreaSystemPrefab" },
            { "GameEndingManager", "gameEndingManagerPrefab" },
            { "RelicSystem", "relicSystemPrefab" },
            { "AbilityComboSystem", "abilityComboSystemPrefab" },
            { "LevelModifierSystem", "levelModifierSystemPrefab" },
            { "PlayerProgressionSystem", "playerProgressionSystemPrefab" },
            { "BossRushMode", "bossRushModePrefab" },
            { "NewGamePlusManager", "newGamePlusManagerPrefab" },
            { "PlayerBondSystem", "playerBondSystemPrefab" },
            { "StoryRecapSystem", "storyRecapSystemPrefab" },
            // 第6层
            { "GameFlowManager", "gameFlowPrefab" },
            // 调试
            { "DebugOverlay", "debugOverlayPrefab" },
        };

        return map.TryGetValue(prefabName, out string propName) ? propName : null;
    }

    // ==================== UI辅助 ====================

    private void DrawStep(int num, string title, string description, bool done, string checkPath,
        System.Action action)
    {
        EditorGUILayout.Space(5);
        GUI.backgroundColor = done ? new Color(0.3f, 0.85f, 0.3f) : Color.white;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();

        GUIStyle stepStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
        string icon = done ? "✅" : $"  {num}";
        GUILayout.Label($"{icon}  {title}", stepStyle);

        GUILayout.FlexibleSpace();

        GUI.enabled = !done || true; // 允许重新执行
        if (GUILayout.Button(done ? "已完成 ✓" : "执行", GUILayout.Width(100), GUILayout.Height(25)))
        {
            action?.Invoke();
            Repaint();
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();

        GUI.backgroundColor = Color.white;
    }

    private void DrawStatRow(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(100));
        GUILayout.Label(value, EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSeparator()
    {
        EditorGUILayout.Space(5);
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
    }

    // ==================== 状态检查 ====================

    private void CheckStatus()
    {
        step1Done = File.Exists("Assets/Art/Placeholders/Characters/Lux.png");
        step2Done = File.Exists("Assets/Animations/Controllers/LuxController.controller");
        step3Done = File.Exists("Assets/Prefabs/Player/Lux.prefab");
        step4Done = Directory.Exists("Assets/ScriptableObjects/LevelData") &&
                    Directory.GetFiles("Assets/ScriptableObjects/LevelData", "*.asset").Length >= 20;
        step5Done = File.Exists("Assets/Scenes/Boot.unity") &&
                    File.Exists("Assets/Scenes/MainMenu.unity");
        Repaint();
    }

    private bool AllDone() => step1Done && step2Done && step3Done && step4Done && step5Done;

    private void OpenScene(string path)
    {
        if (File.Exists(path))
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);
        }
        else
        {
            EditorUtility.DisplayDialog("场景不存在", $"请先执行步骤5创建场景\n{path}", "OK");
        }
    }

    private int CountFiles(string dir, string pattern)
    {
        if (!Directory.Exists(dir)) return 0;
        return Directory.GetFiles(dir, pattern, SearchOption.AllDirectories).Length;
    }
}
