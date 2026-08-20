using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 无头运行时测试 - 在批处理模式下验证Boot→MainMenu→Level流程
/// 不进入PlayMode，但模拟初始化链并检查所有引用
/// </summary>
public static class HeadlessRuntimeTest
{
    private static int totalChecks;
    private static int passedChecks;
    private static List<string> failures = new List<string>();

    [MenuItem("DoubleForward/Run Headless Test", false, 300)]
    public static void RunFromMenu()
    {
        Run();
        EditorUtility.DisplayDialog(
            failures.Count == 0 ? "ALL TESTS PASSED" : $"{failures.Count} FAILURES",
            $"{passedChecks}/{totalChecks} checks passed\n\n" +
            (failures.Count > 0 ? string.Join("\n", failures.GetRange(0, Mathf.Min(15, failures.Count))) : "No issues found!"),
            "OK");
    }

    /// <summary>CLI入口</summary>
    public static void RunFromCommandLine()
    {
        Run();
        if (failures.Count > 0)
        {
            foreach (var f in failures)
                Debug.LogError($"[TEST FAIL] {f}");
            Debug.LogError($"[TEST] {failures.Count} FAILURES out of {totalChecks} checks");
        }
        else
        {
            Debug.Log($"[TEST] ALL {totalChecks} CHECKS PASSED");
        }
    }

    private static void Run()
    {
        totalChecks = 0;
        passedChecks = 0;
        failures.Clear();

        TestBootScene();
        TestMainMenuScene();
        TestPlayerPrefabs();
        TestManagerPrefabs();
        TestLevelScene_1_1();
        TestPuzzlePlacement();
        TestKeyObjectsAreVisible();
        TestCharacterAnimation();
        TestBuildSettings();

        Debug.Log($"[TEST] Results: {passedChecks}/{totalChecks} passed, {failures.Count} failed");
    }

    // ==================== Boot场景 ====================

    private static void TestBootScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity");

        // GameInitializer存在
        var init = Object.FindAnyObjectByType<GameInitializer>();
        Assert("Boot: GameInitializer exists", init != null);

        if (init != null)
        {
            var so = new SerializedObject(init);

            // 检查关键Manager引用
            string[] critical = {
                "gameManagerPrefab", "audioManagerPrefab", "inputManagerPrefab",
                "saveSystemPrefab", "gameFlowPrefab", "sceneLoaderPrefab",
                "localizationPrefab", "objectPoolPrefab"
            };

            foreach (var prop in critical)
            {
                var p = so.FindProperty(prop);
                Assert($"Boot: GameInitializer.{prop} wired", p != null && p.objectReferenceValue != null);
            }
        }

        // Camera存在
        Assert("Boot: Camera exists", Camera.main != null);
    }

    // ==================== MainMenu场景 ====================

    private static void TestMainMenuScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");

        var menuUI = Object.FindAnyObjectByType<MainMenuUI>();
        Assert("MainMenu: MainMenuUI exists", menuUI != null);

        if (menuUI != null)
        {
            var so = new SerializedObject(menuUI);
            Assert("MainMenu: continueButton wired",
                so.FindProperty("continueButton")?.objectReferenceValue != null);
            Assert("MainMenu: newGameButton wired",
                so.FindProperty("newGameButton")?.objectReferenceValue != null);
            Assert("MainMenu: localPlayButton wired",
                so.FindProperty("localPlayButton")?.objectReferenceValue != null);
        }

        // EventSystem
        var eventSystem = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        Assert("MainMenu: EventSystem exists", eventSystem != null);
    }

    // ==================== 玩家预制体 ====================

    private static void TestPlayerPrefabs()
    {
        TestPlayerPrefab("Lux", 0);
        TestPlayerPrefab("Nox", 1);
    }

    private static void TestPlayerPrefab(string name, int expectedIndex)
    {
        string path = $"Assets/Prefabs/Player/{name}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert($"Player/{name}: prefab exists", prefab != null);
        if (prefab == null) return;

        // Components
        Assert($"Player/{name}: has PlayerController", prefab.GetComponent<PlayerController>() != null);
        Assert($"Player/{name}: has PlayerHealth", prefab.GetComponent<PlayerHealth>() != null);
        Assert($"Player/{name}: has PlayerCombat", prefab.GetComponent<PlayerCombat>() != null);
        Assert($"Player/{name}: has Rigidbody2D", prefab.GetComponent<Rigidbody2D>() != null);
        Assert($"Player/{name}: has BoxCollider2D", prefab.GetComponent<BoxCollider2D>() != null);
        Assert($"Player/{name}: has Animator", prefab.GetComponent<Animator>() != null);

        // Critical references
        var controller = prefab.GetComponent<PlayerController>();
        if (controller != null)
        {
            var so = new SerializedObject(controller);
            Assert($"Player/{name}: groundCheck wired",
                so.FindProperty("groundCheck")?.objectReferenceValue != null);
            Assert($"Player/{name}: groundLayer set",
                so.FindProperty("groundLayer")?.intValue != 0);
            Assert($"Player/{name}: playerIndex={expectedIndex}",
                so.FindProperty("playerIndex")?.intValue == expectedIndex);
        }

        // Child objects
        Assert($"Player/{name}: GroundCheck child exists", prefab.transform.Find("GroundCheck") != null);
        Assert($"Player/{name}: WallCheck child exists", prefab.transform.Find("WallCheck") != null);

        // Layer
        int playerLayer = LayerMask.NameToLayer("Player");
        Assert($"Player/{name}: layer=Player", playerLayer >= 0 && prefab.layer == playerLayer);
    }

    // ==================== Manager预制体 ====================

    private static void TestManagerPrefabs()
    {
        string[] managers = {
            "GameManager", "AudioManager", "InputManager", "SaveSystem",
            "GameFlowManager", "SceneLoader", "LocalizationSystem"
        };

        foreach (var mgr in managers)
        {
            string path = $"Assets/Prefabs/Managers/{mgr}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert($"Manager/{mgr}: prefab exists", prefab != null);

            if (prefab != null)
            {
                // 检查没有missing scripts
                var components = prefab.GetComponentsInChildren<Component>(true);
                bool hasMissing = false;
                foreach (var c in components)
                {
                    if (c == null) { hasMissing = true; break; }
                }
                Assert($"Manager/{mgr}: no missing scripts", !hasMissing);
            }
        }

        // InputManager特殊检查：触控UI
        var inputPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Managers/InputManager.prefab");
        if (inputPrefab != null)
        {
            var im = inputPrefab.GetComponent<InputManager>();
            if (im != null)
            {
                var so = new SerializedObject(im);
                Assert("InputManager: joystickP1 wired",
                    so.FindProperty("joystickP1")?.objectReferenceValue != null);
                Assert("InputManager: jumpButtonP1 wired",
                    so.FindProperty("jumpButtonP1")?.objectReferenceValue != null);
                Assert("InputManager: attackButtonP1 wired",
                    so.FindProperty("attackButtonP1")?.objectReferenceValue != null);
            }
        }
    }

    // ==================== Level 1-1 ====================

    private static void TestLevelScene_1_1()
    {
        string path = "Assets/Scenes/Chapter1/Level_1_1.unity";
        if (!File.Exists(path)) { Assert("Level_1_1: scene exists", false); return; }

        EditorSceneManager.OpenScene(path);

        // LevelBootstrap
        var bootstrap = Object.FindAnyObjectByType<LevelBootstrap>();
        Assert("Level_1_1: LevelBootstrap exists", bootstrap != null);

        if (bootstrap != null)
        {
            var so = new SerializedObject(bootstrap);
            Assert("Level_1_1: luxPrefab wired",
                so.FindProperty("luxPrefab")?.objectReferenceValue != null);
            Assert("Level_1_1: noxPrefab wired",
                so.FindProperty("noxPrefab")?.objectReferenceValue != null);
            Assert("Level_1_1: luxSpawnPoint wired",
                so.FindProperty("luxSpawnPoint")?.objectReferenceValue != null);
            Assert("Level_1_1: chapter=1",
                so.FindProperty("chapter")?.intValue == 1);
            Assert("Level_1_1: level=1",
                so.FindProperty("level")?.intValue == 1);
        }

        // Camera
        Assert("Level_1_1: Camera exists", Camera.main != null);
        Assert("Level_1_1: CameraController exists",
            Object.FindAnyObjectByType<CameraController>() != null);

        // LevelManager
        Assert("Level_1_1: LevelManager exists",
            Object.FindAnyObjectByType<LevelManager>() != null);

        // LevelGoalTrigger
        Assert("Level_1_1: LevelGoalTrigger exists",
            Object.FindAnyObjectByType<LevelGoalTrigger>() != null);

        // Ground objects on correct layer
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
        {
            var ground = GameObject.Find("Ground");
            Assert("Level_1_1: Ground on Ground layer",
                ground != null && ground.layer == groundLayer);
        }

        // EventSystem
        Assert("Level_1_1: EventSystem exists",
            Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null);

        // HUDManager
        Assert("Level_1_1: HUDManager exists",
            Object.FindAnyObjectByType<HUDManager>() != null);
    }

    // ==================== Build Settings ====================

    private static void TestBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes;
        Assert("BuildSettings: has scenes", scenes.Length > 0);

        if (scenes.Length > 0)
        {
            Assert("BuildSettings: Boot is first scene",
                scenes[0].path.Contains("Boot.unity") && scenes[0].enabled);
        }

        // 所有注册场景文件存在
        int missingCount = 0;
        foreach (var s in scenes)
        {
            if (s.enabled && !File.Exists(s.path))
                missingCount++;
        }
        Assert("BuildSettings: all enabled scenes exist", missingCount == 0);
    }

    // ==================== 角色动画 ====================

    /// <summary>
    /// 角色剪辑必须含有真正的多帧精灵曲线。原本这些 .anim 是 AnimatorFactory
    /// 生成的占位(只做缩放脉动),没有任何精灵关键帧。
    /// Play 测试里因为没有输入设备进不了Run状态,只能靠这里保证帧数。
    /// </summary>
    private static void TestCharacterAnimation()
    {
        var expected = new (string clip, int minFrames)[]
        {
            ("Lux_Idle", 4), ("Lux_Run", 4), ("Lux_Jump", 1), ("Lux_Fall", 1),
            ("Nox_Idle", 4), ("Nox_Run", 4), ("Nox_Jump", 1), ("Nox_Fall", 1),
        };

        foreach (var (clipName, minFrames) in expected)
        {
            string path = $"Assets/Animations/Clips/{clipName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { Assert($"Anim: {clipName} exists", false); continue; }

            int frames = 0;
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") continue;
                frames = AnimationUtility.GetObjectReferenceCurve(clip, binding).Length;
            }
            Assert($"Anim: {clipName} has >= {minFrames} sprite keyframes (got {frames})",
                frames >= minFrames);
        }
    }

    // ==================== 机关摆放 ====================

    /// <summary>
    /// 全关卡扫一遍: 压力板不能停在世界原点。
    /// PressurePlate 曾定义 public void Reset() 撞上Unity的魔法回调,编辑器
    /// AddComponent时自动调用,把每个板都瞬移到了(0,0) —— 那里正好是出生点,
    /// 于是开局就被踩下,联动的门永远敞开,谜题形同虚设。
    /// </summary>
    private static void TestPuzzlePlacement()
    {
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            string sceneName = Path.GetFileNameWithoutExtension(entry.path);

            foreach (var plate in Object.FindObjectsByType<PressurePlate>(FindObjectsSortMode.None))
            {
                var p = plate.transform.position;
                Assert($"{sceneName}: '{plate.name}' not stranded at world origin ({p.x:F1},{p.y:F1})",
                    new Vector2(p.x, p.y).magnitude > 1f);
            }
        }
    }

    /// <summary>
    /// 关键物件必须真的画得出来。
    ///
    /// 第一章的最终 Boss 的 SpriteRenderer 一直是 m_Sprite: 0 —— 它有血量、
    /// 会追击、能被打死,画面上却什么都没有,而且不报任何错。整套测试没有一条
    /// 能发现它,因为大家查的都是"组件在不在""血量掉没掉",没人查"看不看得见"。
    /// </summary>
    private static void TestKeyObjectsAreVisible()
    {
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            string sceneName = Path.GetFileNameWithoutExtension(entry.path);

            foreach (var boss in Object.FindObjectsByType<BossBase>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var sr = boss.GetComponent<SpriteRenderer>();
                Assert($"{sceneName}: boss '{boss.name}' is actually visible (has a sprite)",
                    sr != null && sr.sprite != null);
            }

            var goal = GameObject.Find("LevelGoal");
            if (goal != null)
            {
                // 终点本体的贴图被关掉了,视觉交给场景里的终点旗
                Assert($"{sceneName}: the goal is marked by a visible flag",
                    GameObject.Find("GoalFlag") != null);
            }
        }
    }

    // ==================== Assert ====================

    private static void Assert(string name, bool condition)
    {
        totalChecks++;
        if (condition)
        {
            passedChecks++;
            Debug.Log($"[TEST] PASS: {name}");
        }
        else
        {
            failures.Add(name);
            Debug.LogWarning($"[TEST] FAIL: {name}");
        }
    }
}
