using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.IO;

/// <summary>
/// 场景UI关联工厂 - 将SceneFactory创建的UI对象关联到对应脚本的SerializeField
/// 修复MainMenu/Level/Pause等场景中UI组件引用为空的问题
/// </summary>
public static class SceneUIWiringFactory
{
    [MenuItem("DoubleForward/Wire All Scene UI References", false, 60)]
    public static void WireAll()
    {
        int totalWired = 0;

        EditorUtility.DisplayProgressBar("Wiring", "Player prefabs...", 0.05f);
        totalWired += WirePlayerPrefabs();

        EditorUtility.DisplayProgressBar("Wiring", "Enemy prefabs...", 0.08f);
        totalWired += WireEnemyPrefabs();

        EditorUtility.DisplayProgressBar("Wiring", "MainMenu...", 0.1f);
        totalWired += WireMainMenuScene();

        EditorUtility.DisplayProgressBar("Wiring", "Level scenes...", 0.3f);
        for (int ch = 1; ch <= 5; ch++)
        {
            for (int lv = 1; lv <= 4; lv++)
            {
                float p = 0.3f + (float)((ch - 1) * 4 + lv) / 20f * 0.6f;
                EditorUtility.DisplayProgressBar("Wiring", $"Level_{ch}_{lv}...", p);
                totalWired += WireLevelScene(ch, lv);
            }
        }

        EditorUtility.ClearProgressBar();
        Debug.Log($"[UIWiring] Done! Wired {totalWired} references across all scenes and prefabs.");
    }

    /// <summary>
    /// CLI入口
    /// </summary>
    public static void WireAllFromCommandLine()
    {
        WireAll();
    }

    // ==================== Player Prefabs ====================

    private static int WirePlayerPrefabs()
    {
        int wired = 0;
        string[] players = { "Lux", "Nox" };

        foreach (var name in players)
        {
            string path = $"Assets/Prefabs/Player/{name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            // 打开prefab编辑模式
            var root = PrefabUtility.LoadPrefabContents(path);
            var controller = root.GetComponent<PlayerController>();
            if (controller == null) { PrefabUtility.UnloadPrefabContents(root); continue; }

            var so = new SerializedObject(controller);

            // groundCheck
            var groundCheckT = root.transform.Find("GroundCheck");
            if (groundCheckT != null)
            {
                var prop = so.FindProperty("groundCheck");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = groundCheckT;
                    wired++;
                }
            }

            // wallCheckPoint
            var wallCheckT = root.transform.Find("WallCheck");
            if (wallCheckT != null)
            {
                var prop = so.FindProperty("wallCheckPoint");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = wallCheckT;
                    wired++;
                }
            }

            // groundLayer → "Ground" layer
            var layerProp = so.FindProperty("groundLayer");
            if (layerProp != null)
            {
                int groundLayerIdx = LayerMask.NameToLayer("Ground");
                if (groundLayerIdx >= 0)
                {
                    int mask = 1 << groundLayerIdx;
                    if (layerProp.intValue == 0)
                    {
                        layerProp.intValue = mask;
                        wired++;
                    }
                }
            }

            // playerIndex
            var indexProp = so.FindProperty("playerIndex");
            if (indexProp != null)
            {
                int expected = name == "Lux" ? 0 : 1;
                if (indexProp.intValue != expected)
                {
                    indexProp.intValue = expected;
                    wired++;
                }
            }

            // playerType
            var typeProp = so.FindProperty("playerType");
            if (typeProp != null)
            {
                int expected = name == "Lux" ? 0 : 1; // Lux=0, Nox=1
                if (typeProp.enumValueIndex != expected)
                {
                    typeProp.enumValueIndex = expected;
                    wired++;
                }
            }

            // Player layer
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0 && root.layer != playerLayer)
            {
                root.layer = playerLayer;
                wired++;
            }

            so.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[UIWiring] Player prefabs: wired {wired} references");
        return wired;
    }

    // ==================== Enemy Prefabs ====================

    private static int WireEnemyPrefabs()
    {
        int wired = 0;
        string dir = "Assets/Prefabs/Enemies";
        if (!Directory.Exists(dir)) return 0;

        var files = Directory.GetFiles(dir, "*.prefab");
        foreach (var file in files)
        {
            string path = file.Replace('\\', '/');
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var enemy = prefab.GetComponent<EnemyBase>();
            if (enemy == null) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            var enemyComp = root.GetComponent<EnemyBase>();
            if (enemyComp == null) { PrefabUtility.UnloadPrefabContents(root); continue; }

            var so = new SerializedObject(enemyComp);

            // groundLayer
            var layerProp = so.FindProperty("groundLayer");
            if (layerProp != null && layerProp.intValue == 0)
            {
                int groundLayerIdx = LayerMask.NameToLayer("Ground");
                if (groundLayerIdx >= 0)
                {
                    layerProp.intValue = 1 << groundLayerIdx;
                    wired++;
                }
            }

            // patrol points
            var patrolProp = so.FindProperty("patrolPoints");
            if (patrolProp != null && patrolProp.arraySize == 0)
            {
                var p0 = root.transform.Find("Patrol_0");
                var p1 = root.transform.Find("Patrol_1");
                if (p0 != null && p1 != null)
                {
                    patrolProp.arraySize = 2;
                    patrolProp.GetArrayElementAtIndex(0).objectReferenceValue = p0;
                    patrolProp.GetArrayElementAtIndex(1).objectReferenceValue = p1;
                    wired += 2;
                }
            }

            so.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[UIWiring] Enemy prefabs: wired {wired} references");
        return wired;
    }

    // ==================== MainMenu ====================

    private static int WireMainMenuScene()
    {
        string path = "Assets/Scenes/MainMenu.unity";
        if (!File.Exists(path)) return 0;

        EditorSceneManager.OpenScene(path);
        int wired = 0;

        var menuUI = Object.FindAnyObjectByType<MainMenuUI>();
        if (menuUI == null)
        {
            Debug.LogWarning("[UIWiring] MainMenuUI not found in MainMenu scene");
            return 0;
        }

        var so = new SerializedObject(menuUI);

        // 按钮关联
        wired += WireButton(so, "continueButton", "ContinueButton");
        wired += WireButton(so, "newGameButton", "NewGameButton");
        wired += WireButton(so, "localPlayButton", "LocalPlayButton");
        wired += WireButton(so, "onlinePlayButton", "OnlinePlayButton");
        wired += WireButton(so, "settingsButton", "SettingsButton");

        // 通关后按钮
        wired += WireButton(so, "newGamePlusButton", "NGPlusButton");
        wired += WireButton(so, "bossRushButton", "BossRushButton");
        wired += WireButton(so, "storyRecapButton", "StoryRecapButton");

        // 面板
        wired += WireGameObject(so, "mainPanel", "ButtonPanel");
        wired += WireGameObject(so, "postGameGroup", "PostGamePanel");

        // 文本
        wired += WireTMP(so, "versionText", "SubtitleText"); // 副标题可复用为版本号

        // CanvasGroup
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            var cg = canvas.GetComponent<CanvasGroup>();
            if (cg == null) cg = canvas.gameObject.AddComponent<CanvasGroup>();
            var cgProp = so.FindProperty("mainCanvasGroup");
            if (cgProp != null && cgProp.objectReferenceValue == null)
            {
                cgProp.objectReferenceValue = cg;
                wired++;
            }
        }

        so.ApplyModifiedProperties();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log($"[UIWiring] MainMenu: wired {wired} references");
        return wired;
    }

    // ==================== Level Scenes ====================

    private static int WireLevelScene(int chapter, int level)
    {
        string path = $"Assets/Scenes/Chapter{chapter}/Level_{chapter}_{level}.unity";
        if (!File.Exists(path)) return 0;

        EditorSceneManager.OpenScene(path);
        int wired = 0;

        // 设置Ground/Platform层
        wired += AssignGroundLayers();

        // 设置Player层
        wired += AssignPlayerLayers();

        // 设置Enemy层
        wired += AssignEnemyLayers();

        // HUDManager
        var hud = Object.FindAnyObjectByType<HUDManager>();
        if (hud != null)
        {
            var so = new SerializedObject(hud);
            wired += WireTMP(so, "levelNameText", "LevelName");
            wired += WireTMP(so, "timerText", "Timer");
            wired += WireTMP(so, "collectibleText", "Collectibles");
            so.ApplyModifiedProperties();
        }

        // PauseMenuUI
        var pause = Object.FindAnyObjectByType<PauseMenuUI>();
        if (pause != null)
        {
            var so = new SerializedObject(pause);
            wired += WireButton(so, "resumeButton", "ResumeButton");
            wired += WireButton(so, "settingsButton", "SettingsButton");
            wired += WireButton(so, "restartButton", "RestartButton");
            wired += WireButton(so, "mainMenuButton", "QuitButton");
            wired += WireGameObject(so, "pausePanel", "PausePanel");
            so.ApplyModifiedProperties();
        }

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        return wired;
    }

    // ==================== Layer 分配 ====================

    private static int AssignGroundLayers()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0) return 0;

        int count = 0;
        var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            string n = t.name.ToLower();
            bool isGround = n.Contains("ground") || n.Contains("platform") || n.Contains("floor");
            // 只处理有Collider2D的对象
            if (isGround && t.GetComponent<Collider2D>() != null && t.gameObject.layer != groundLayer)
            {
                t.gameObject.layer = groundLayer;
                count++;
            }
        }
        return count;
    }

    private static int AssignPlayerLayers()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer < 0) return 0;

        int count = 0;
        var controllers = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var pc in controllers)
        {
            if (pc.gameObject.layer != playerLayer)
            {
                pc.gameObject.layer = playerLayer;
                count++;
            }
        }
        return count;
    }

    private static int AssignEnemyLayers()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) return 0;

        int count = 0;
        var enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            if (e.gameObject.layer != enemyLayer)
            {
                e.gameObject.layer = enemyLayer;
                count++;
            }
        }
        return count;
    }

    // ==================== 辅助方法 ====================

    private static int WireButton(SerializedObject so, string propName, string goName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null || prop.objectReferenceValue != null) return 0;

        var go = FindInScene(goName);
        if (go == null) return 0;

        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();

        prop.objectReferenceValue = btn;
        return 1;
    }

    private static int WireGameObject(SerializedObject so, string propName, string goName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null || prop.objectReferenceValue != null) return 0;

        var go = FindInScene(goName);
        if (go == null) return 0;

        prop.objectReferenceValue = go;
        return 1;
    }

    private static int WireTMP(SerializedObject so, string propName, string goName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null || prop.objectReferenceValue != null) return 0;

        var go = FindInScene(goName);
        if (go == null) return 0;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) return 0;

        prop.objectReferenceValue = tmp;
        return 1;
    }

    private static GameObject FindInScene(string name)
    {
        // 全场景搜索同名对象
        var allObjects = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in allObjects)
        {
            if (t.name == name)
                return t.gameObject;
        }
        return null;
    }
}
