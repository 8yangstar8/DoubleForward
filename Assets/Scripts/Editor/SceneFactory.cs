using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;

/// <summary>
/// 场景工厂 - 创建所有游戏场景（含完整GameObject层次结构）
/// Boot场景、主菜单、关卡（5章×4关）、Boss连战、制作名单
/// </summary>
public static class SceneFactory
{
    private const string SCENE_DIR = "Assets/Scenes";
    private const string PREFAB_DIR = "Assets/Prefabs";

    // 五个世界的主题色
    private static readonly Color[] WorldColors = {
        new Color(1f, 0.9f, 0.5f),     // Ch1: 光影神殿 - 金色
        new Color(0.4f, 0.7f, 1f),     // Ch2: 冰火交界 - 冰蓝
        new Color(0.9f, 0.75f, 0.35f), // Ch3: 沙海迷城 - 沙金
        new Color(0.25f, 0.15f, 0.4f), // Ch4: 暗影深渊 - 深紫
        new Color(0.7f, 0.5f, 0.9f),   // Ch5: 双向归一 - 光暗融合
    };

    private static readonly string[] WorldNames = {
        "光影神殿", "冰火交界", "沙海迷城", "暗影深渊", "双向归一"
    };

    [MenuItem("DoubleForward/Create All Scenes", false, 54)]
    public static void CreateAll()
    {
        EditorUtility.DisplayProgressBar("Creating Scenes", "Boot scene...", 0.05f);
        CreateBootScene();

        EditorUtility.DisplayProgressBar("Creating Scenes", "Main menu...", 0.1f);
        CreateMainMenuScene();

        EditorUtility.DisplayProgressBar("Creating Scenes", "Loading...", 0.15f);
        CreateLoadingScene();

        // 5章 × 4关 = 20关
        for (int ch = 1; ch <= 5; ch++)
        {
            for (int lv = 1; lv <= 4; lv++)
            {
                float progress = 0.15f + (float)((ch - 1) * 4 + lv) / 20f * 0.7f;
                bool isBoss = (lv == 4);
                EditorUtility.DisplayProgressBar("Creating Scenes",
                    $"Ch{ch} Lv{lv}{(isBoss ? " (Boss)" : "")}...", progress);
                CreateLevelScene(ch, lv, isBoss);
            }
        }

        EditorUtility.DisplayProgressBar("Creating Scenes", "Boss Rush...", 0.9f);
        CreateBossRushScene();

        EditorUtility.DisplayProgressBar("Creating Scenes", "Credits...", 0.95f);
        CreateCreditsScene();

        EditorUtility.ClearProgressBar();

        // 注册到Build Settings
        RegisterScenesToBuildSettings();

        AssetDatabase.Refresh();
        Debug.Log("[SceneFactory] All scenes created and registered.");
    }

    // ==================== Boot场景 ====================

    private static void CreateBootScene()
    {
        string path = $"{SCENE_DIR}/Boot.unity";
        if (File.Exists(path)) return;
        EnsureDir(Path.GetDirectoryName(path));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 相机
        var camObj = new GameObject("Main Camera");
        var cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        cam.orthographicSize = 5;
        camObj.AddComponent<AudioListener>();
        camObj.tag = "MainCamera";

        // GameInitializer
        var initObj = new GameObject("GameInitializer");
        initObj.AddComponent<GameInitializer>();

        EditorSceneManager.SaveScene(scene, path);
        Debug.Log("[SceneFactory] Created Boot scene");
    }

    // ==================== 主菜单场景 ====================

    private static void CreateMainMenuScene()
    {
        string path = $"{SCENE_DIR}/MainMenu.unity";
        if (File.Exists(path)) return;
        EnsureDir(Path.GetDirectoryName(path));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 背景
        var bgObj = new GameObject("Background");
        var bgSR = bgObj.AddComponent<SpriteRenderer>();
        bgSR.color = new Color(0.05f, 0.03f, 0.1f);
        bgSR.sortingOrder = -10;
        bgObj.transform.localScale = new Vector3(30, 20, 1);

        // 标题光效
        var titleGlow = new GameObject("TitleGlow");
        var glowSR = titleGlow.AddComponent<SpriteRenderer>();
        glowSR.color = new Color(0.5f, 0.3f, 0.8f, 0.3f);
        glowSR.sortingOrder = -9;
        titleGlow.transform.position = new Vector3(0, 2, 0);
        titleGlow.transform.localScale = new Vector3(10, 6, 1);

        // 主Canvas
        var canvas = CreateUICanvas("MainCanvas", 1080, 1920);

        // 标题
        var titleObj = CreateUIText(canvas.transform, "TitleText", "双向前行",
            new Vector2(0, 350), 72, Color.white, TextAlignmentOptions.Center);
        var subtitleObj = CreateUIText(canvas.transform, "SubtitleText", "DOUBLE FORWARD",
            new Vector2(0, 280), 28, new Color(0.7f, 0.5f, 0.9f), TextAlignmentOptions.Center);

        // 按钮面板
        var buttonPanel = CreateUIPanel(canvas.transform, "ButtonPanel",
            new Vector2(0, -50), new Vector2(400, 500));

        CreateUIButton(buttonPanel.transform, "ContinueButton", "继续游戏",
            new Vector2(0, 180), new Vector2(350, 55));
        CreateUIButton(buttonPanel.transform, "NewGameButton", "新游戏",
            new Vector2(0, 110), new Vector2(350, 55));
        CreateUIButton(buttonPanel.transform, "LocalPlayButton", "本地双人",
            new Vector2(0, 40), new Vector2(350, 55));
        CreateUIButton(buttonPanel.transform, "OnlinePlayButton", "在线匹配",
            new Vector2(0, -30), new Vector2(350, 55));
        CreateUIButton(buttonPanel.transform, "SettingsButton", "设置",
            new Vector2(0, -100), new Vector2(350, 55));

        // 后期解锁按钮
        var postGamePanel = CreateUIPanel(canvas.transform, "PostGamePanel",
            new Vector2(0, -350), new Vector2(400, 200));

        CreateUIButton(postGamePanel.transform, "NGPlusButton", "新游戏+",
            new Vector2(0, 50), new Vector2(170, 50));
        CreateUIButton(postGamePanel.transform, "BossRushButton", "Boss连战",
            new Vector2(0, -10), new Vector2(170, 50));
        CreateUIButton(postGamePanel.transform, "StoryRecapButton", "剧情回顾",
            new Vector2(0, -70), new Vector2(170, 50));

        // MainMenuUI组件
        canvas.AddComponent<MainMenuUI>();

        // EventSystem
        CreateEventSystem();

        EditorSceneManager.SaveScene(scene, path);
        Debug.Log("[SceneFactory] Created MainMenu scene");
    }

    // ==================== 加载场景 ====================

    private static void CreateLoadingScene()
    {
        string path = $"{SCENE_DIR}/Loading.unity";
        if (File.Exists(path)) return;
        EnsureDir(Path.GetDirectoryName(path));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var canvas = CreateUICanvas("LoadingCanvas", 1080, 1920);

        // 加载条背景
        var loadBarBG = CreateUIImage(canvas.transform, "LoadBarBG",
            new Vector2(0, -300), new Vector2(600, 20),
            new Color(0.15f, 0.15f, 0.2f));

        // 加载条填充
        var loadBarFill = CreateUIImage(loadBarBG.transform, "LoadBarFill",
            Vector2.zero, new Vector2(600, 20),
            new Color(0.3f, 0.7f, 1f));

        // 提示文字
        CreateUIText(canvas.transform, "TipText", "Loading...",
            new Vector2(0, -350), 20, Color.white, TextAlignmentOptions.Center);

        // 百分比
        CreateUIText(canvas.transform, "PercentText", "0%",
            new Vector2(0, -270), 24, Color.white, TextAlignmentOptions.Center);

        canvas.AddComponent<LoadingScreenUI>();

        CreateEventSystem();
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log("[SceneFactory] Created Loading scene");
    }

    // ==================== 关卡场景 ====================

    private static void CreateLevelScene(int chapter, int level, bool isBoss)
    {
        string dir = $"{SCENE_DIR}/Chapter{chapter}";
        EnsureDir(dir);
        string path = $"{dir}/Level_{chapter}_{level}.unity";
        if (File.Exists(path)) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Color worldColor = WorldColors[chapter - 1];

        // ====== 相机 ======
        var camObj = new GameObject("Main Camera");
        var cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.Lerp(worldColor * 0.15f, Color.black, 0.5f);
        cam.orthographic = true;
        cam.orthographicSize = 7;
        camObj.AddComponent<AudioListener>();
        camObj.AddComponent<CameraController>();
        camObj.AddComponent<DualPlayerCamera>();
        camObj.AddComponent<CameraShake>();
        camObj.tag = "MainCamera";

        // ====== 世界 ======
        var worldRoot = new GameObject("--- WORLD ---");

        // 背景
        CreateParallaxLayers(worldRoot.transform, worldColor, chapter);

        // 地面
        var ground = new GameObject("Ground");
        ground.transform.SetParent(worldRoot.transform);
        var groundSR = ground.AddComponent<SpriteRenderer>();
        groundSR.color = Color.Lerp(worldColor, new Color(0.3f, 0.25f, 0.2f), 0.6f);
        groundSR.sortingLayerName = "Default";
        groundSR.sortingOrder = 0;
        ground.transform.position = new Vector3(20, -2, 0);
        ground.transform.localScale = new Vector3(60, 2, 1);
        var groundCol = ground.AddComponent<BoxCollider2D>();
        groundCol.size = new Vector2(1, 1);

        // 平台（随机几个）
        CreateSamplePlatforms(worldRoot.transform, worldColor, chapter, level);

        // ====== 出生点 ======
        var spawnRoot = new GameObject("--- SPAWNS ---");
        var luxSpawn = new GameObject("LuxSpawnPoint");
        luxSpawn.transform.SetParent(spawnRoot.transform);
        luxSpawn.transform.position = new Vector3(-3, 0, 0);
        var noxSpawn = new GameObject("NoxSpawnPoint");
        noxSpawn.transform.SetParent(spawnRoot.transform);
        noxSpawn.transform.position = new Vector3(-1, 0, 0);

        // ====== LevelBootstrap ======
        var bootstrapObj = new GameObject("LevelBootstrap");
        var bootstrap = bootstrapObj.AddComponent<LevelBootstrap>();

        // 尝试关联玩家预制体
        var luxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/Player/Lux.prefab");
        var noxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/Player/Nox.prefab");

        var bootstrapSO = new SerializedObject(bootstrap);
        bootstrapSO.FindProperty("chapter").intValue = chapter;
        bootstrapSO.FindProperty("level").intValue = level;
        bootstrapSO.FindProperty("isFirstLevelInChapter").boolValue = (level == 1);
        bootstrapSO.FindProperty("isBossLevel").boolValue = isBoss;

        var luxSpawnProp = bootstrapSO.FindProperty("luxSpawnPoint");
        if (luxSpawnProp != null) luxSpawnProp.objectReferenceValue = luxSpawn.transform;
        var noxSpawnProp = bootstrapSO.FindProperty("noxSpawnPoint");
        if (noxSpawnProp != null) noxSpawnProp.objectReferenceValue = noxSpawn.transform;
        var luxPrefabProp = bootstrapSO.FindProperty("luxPrefab");
        if (luxPrefabProp != null) luxPrefabProp.objectReferenceValue = luxPrefab;
        var noxPrefabProp = bootstrapSO.FindProperty("noxPrefab");
        if (noxPrefabProp != null) noxPrefabProp.objectReferenceValue = noxPrefab;

        bootstrapSO.ApplyModifiedPropertiesWithoutUndo();

        // ====== 谜题区 ======
        var puzzleRoot = new GameObject("--- PUZZLES ---");
        CreateSamplePuzzles(puzzleRoot.transform, chapter, level);

        // ====== 敌人 ======
        var enemyRoot = new GameObject("--- ENEMIES ---");
        if (!isBoss)
            CreateSampleEnemies(enemyRoot.transform, chapter, level);

        // ====== Boss（Boss关） ======
        if (isBoss)
        {
            var bossRoot = new GameObject("--- BOSS ---");
            CreateBossSetup(bossRoot.transform, chapter);
        }

        // ====== 收集品 ======
        var collectRoot = new GameObject("--- COLLECTIBLES ---");
        CreateSampleCollectibles(collectRoot.transform, level);

        // ====== 检查点 ======
        var checkpointRoot = new GameObject("--- CHECKPOINTS ---");
        CreateSampleCheckpoints(checkpointRoot.transform, level);

        // ====== 关卡终点 ======
        var goalObj = new GameObject("LevelGoal");
        goalObj.AddComponent<LevelGoalTrigger>();
        var goalCol = goalObj.AddComponent<BoxCollider2D>();
        goalCol.size = new Vector2(2, 3);
        goalCol.isTrigger = true;
        goalObj.transform.position = new Vector3(40 + level * 5, 0.5f, 0);

        // ====== 死亡区域 ======
        var deathZone = new GameObject("DeathZone");
        var dzCol = deathZone.AddComponent<BoxCollider2D>();
        dzCol.size = new Vector2(200, 2);
        dzCol.isTrigger = true;
        deathZone.AddComponent<DeathZone>();
        deathZone.transform.position = new Vector3(20, -15, 0);

        // ====== HUD Canvas ======
        var hudCanvas = CreateUICanvas("HUDCanvas", 1080, 1920);
        SetupHUDCanvas(hudCanvas);

        // ====== Pause Canvas ======
        var pauseCanvas = CreateUICanvas("PauseCanvas", 1080, 1920);
        pauseCanvas.GetComponent<Canvas>().sortingOrder = 100;
        SetupPauseCanvas(pauseCanvas);
        pauseCanvas.SetActive(false);

        CreateEventSystem();

        // ====== 关卡管理 ======
        var levelMgrObj = new GameObject("LevelManager");
        levelMgrObj.AddComponent<LevelManager>();
        levelMgrObj.AddComponent<LevelTimer>();
        levelMgrObj.AddComponent<LevelCompletionChecker>();

        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[SceneFactory] Created Level_{chapter}_{level}{(isBoss ? " (Boss)" : "")}");
    }

    // ==================== Boss连战场景 ====================

    private static void CreateBossRushScene()
    {
        string path = $"{SCENE_DIR}/BossRush.unity";
        if (File.Exists(path)) return;
        EnsureDir(Path.GetDirectoryName(path));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 背景
        Camera.main.backgroundColor = new Color(0.05f, 0.02f, 0.08f);

        // 竞技场
        var arena = new GameObject("BossArena");
        arena.AddComponent<BossArena>();

        // 地面
        var ground = new GameObject("Floor");
        ground.transform.position = new Vector3(0, -1, 0);
        ground.transform.localScale = new Vector3(30, 2, 1);
        var gc = ground.AddComponent<BoxCollider2D>();
        gc.size = Vector2.one;
        var gsr = ground.AddComponent<SpriteRenderer>();
        gsr.color = new Color(0.15f, 0.1f, 0.2f);

        // UI
        var canvas = CreateUICanvas("BossRushCanvas", 1080, 1920);
        canvas.AddComponent<BossRushUI>();

        // 出生点
        var luxSpawn = new GameObject("LuxSpawn");
        luxSpawn.transform.position = new Vector3(-4, 0, 0);
        var noxSpawn = new GameObject("NoxSpawn");
        noxSpawn.transform.position = new Vector3(4, 0, 0);

        CreateEventSystem();
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log("[SceneFactory] Created BossRush scene");
    }

    // ==================== 制作名单 ====================

    private static void CreateCreditsScene()
    {
        string path = $"{SCENE_DIR}/Credits.unity";
        if (File.Exists(path)) return;
        EnsureDir(Path.GetDirectoryName(path));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Camera.main.backgroundColor = Color.black;

        var canvas = CreateUICanvas("CreditsCanvas", 1080, 1920);
        canvas.AddComponent<CreditsUI>();

        CreateUIText(canvas.transform, "CreditsTitle", "DOUBLE FORWARD",
            new Vector2(0, 400), 48, Color.white, TextAlignmentOptions.Center);
        CreateUIText(canvas.transform, "CreditsContent", "A Game by Your Studio\n\nDesign & Programming\nYour Name",
            new Vector2(0, 0), 24, Color.white, TextAlignmentOptions.Center);

        CreateUIButton(canvas.transform, "BackButton", "返回",
            new Vector2(0, -400), new Vector2(200, 50));

        CreateEventSystem();
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log("[SceneFactory] Created Credits scene");
    }

    // ==================== 关卡内容生成器 ====================

    private static void CreateParallaxLayers(Transform parent, Color worldColor, int chapter)
    {
        for (int i = 0; i < 3; i++)
        {
            var layer = new GameObject($"ParallaxLayer_{i}");
            layer.transform.SetParent(parent);
            var sr = layer.AddComponent<SpriteRenderer>();
            float darkness = 0.15f + i * 0.05f;
            sr.color = new Color(worldColor.r * darkness, worldColor.g * darkness, worldColor.b * darkness);
            sr.sortingOrder = -10 + i;
            layer.transform.position = new Vector3(20, 3 - i * 2, 10 - i * 3);
            layer.transform.localScale = new Vector3(80, 15 - i * 3, 1);
        }
    }

    private static void CreateSamplePlatforms(Transform parent, Color color, int chapter, int level)
    {
        int platformCount = 3 + level;
        Color platformColor = Color.Lerp(color, new Color(0.4f, 0.35f, 0.3f), 0.5f);

        for (int i = 0; i < platformCount; i++)
        {
            var plat = new GameObject($"Platform_{i}");
            plat.transform.SetParent(parent);

            float x = 5 + i * 8;
            float y = -0.5f + (i % 3) * 2;
            plat.transform.position = new Vector3(x, y, 0);
            plat.transform.localScale = new Vector3(4 + (i % 2) * 2, 0.5f, 1);

            var sr = plat.AddComponent<SpriteRenderer>();
            sr.color = platformColor;
            sr.sortingOrder = 0;

            var col = plat.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
        }
    }

    private static void CreateSamplePuzzles(Transform parent, int chapter, int level)
    {
        // 每关放2-4个谜题元素
        float baseX = 10;

        // 压力板
        var plate = new GameObject("PressurePlate_1");
        plate.transform.SetParent(parent);
        plate.transform.position = new Vector3(baseX, -0.8f, 0);
        plate.AddComponent<PressurePlate>();
        var plateCol = plate.AddComponent<BoxCollider2D>();
        plateCol.size = new Vector2(2, 0.3f);
        plateCol.isTrigger = true;

        // 根据章节不同添加特色谜题
        if (chapter >= 1)
        {
            var sensor = new GameObject("LightSensor_1");
            sensor.transform.SetParent(parent);
            sensor.transform.position = new Vector3(baseX + 10, 1, 0);
            sensor.AddComponent<LightSensor>();
            var sCol = sensor.AddComponent<BoxCollider2D>();
            sCol.size = new Vector2(1.2f, 1.2f);
            sCol.isTrigger = true;
        }

        if (chapter >= 2 && level >= 2)
        {
            var gear = new GameObject("GearMechanism_1");
            gear.transform.SetParent(parent);
            gear.transform.position = new Vector3(baseX + 20, 0, 0);
            gear.AddComponent<GearMechanism>();
            var gCol = gear.AddComponent<BoxCollider2D>();
            gCol.size = new Vector2(2, 2);
            gCol.isTrigger = true;
        }

        if (chapter >= 3)
        {
            var portal = new GameObject("Portal_1");
            portal.transform.SetParent(parent);
            portal.transform.position = new Vector3(baseX + 15, 0, 0);
            portal.AddComponent<Portal>();
            var pCol = portal.AddComponent<BoxCollider2D>();
            pCol.size = new Vector2(1.5f, 2f);
            pCol.isTrigger = true;
        }
    }

    private static void CreateSampleEnemies(Transform parent, int chapter, int level)
    {
        int enemyCount = 2 + level;

        for (int i = 0; i < enemyCount; i++)
        {
            var enemyObj = new GameObject($"Enemy_{i}");
            enemyObj.transform.SetParent(parent);
            float x = 8 + i * 7;
            enemyObj.transform.position = new Vector3(x, 0, 0);

            // 根据章节选择敌人类型
            var rb = enemyObj.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;

            var sr = enemyObj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 8;

            var col = enemyObj.AddComponent<BoxCollider2D>();

            if (i % 3 == 0)
            {
                enemyObj.AddComponent<ShadowSlime>();
                col.size = new Vector2(0.9f, 0.7f);
                sr.color = new Color(0.3f, 0.1f, 0.4f);
            }
            else if (i % 3 == 1)
            {
                enemyObj.AddComponent<ShadowArcher>();
                col.size = new Vector2(0.8f, 1.4f);
                sr.color = new Color(0.4f, 0.15f, 0.3f);
                var fp = new GameObject("FirePoint");
                fp.transform.SetParent(enemyObj.transform);
                fp.transform.localPosition = new Vector3(0.5f, 0.3f, 0);
            }
            else
            {
                enemyObj.AddComponent<ShadowGuard>();
                col.size = new Vector2(1f, 1.4f);
                sr.color = new Color(0.25f, 0.25f, 0.35f);
            }

            // 巡逻点
            var p0 = new GameObject("Patrol_0");
            p0.transform.SetParent(enemyObj.transform);
            p0.transform.localPosition = new Vector3(-3, 0, 0);
            var p1 = new GameObject("Patrol_1");
            p1.transform.SetParent(enemyObj.transform);
            p1.transform.localPosition = new Vector3(3, 0, 0);
        }
    }

    private static void CreateBossSetup(Transform parent, int chapter)
    {
        string[] bossTypes = {
            "ForestGuardianBoss", "IceFlameTitanBoss",
            "SandstormDjinnBoss", "AbyssalSerpentBoss", "VoidBoss"
        };

        // 竞技场
        var arena = new GameObject("BossArena");
        arena.transform.SetParent(parent);
        arena.AddComponent<BossArena>();

        // Boss出生点
        var bossSpawn = new GameObject("BossSpawnPoint");
        bossSpawn.transform.SetParent(parent);
        bossSpawn.transform.position = new Vector3(35, 2, 0);

        // Boss占位（实际由BossArena在运行时生成）
        var bossObj = new GameObject($"Boss_Ch{chapter}");
        bossObj.transform.SetParent(parent);
        bossObj.transform.position = new Vector3(35, 2, 0);

        var sr = bossObj.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 8;
        sr.color = WorldColors[chapter - 1];

        var rb = bossObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;

        var col = bossObj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(3, 4);

        // 添加对应Boss脚本
        switch (chapter)
        {
            case 1: bossObj.AddComponent<ForestGuardianBoss>(); break;
            case 2: bossObj.AddComponent<GearTyrantBoss>(); break;
            case 3: bossObj.AddComponent<AbyssSerpentBoss>(); break;
            case 4: bossObj.AddComponent<RuinSentinelBoss>(); break;
            case 5: bossObj.AddComponent<VoidBoss>(); break;
        }

        // 封锁墙（进入Boss区后封闭）
        var sealWall = new GameObject("SealWall");
        sealWall.transform.SetParent(parent);
        sealWall.transform.position = new Vector3(28, 3, 0);
        var swCol = sealWall.AddComponent<BoxCollider2D>();
        swCol.size = new Vector2(1, 10);
        sealWall.SetActive(false); // 默认关闭
    }

    private static void CreateSampleCollectibles(Transform parent, int level)
    {
        int count = 3 + level;
        for (int i = 0; i < count; i++)
        {
            var item = new GameObject($"Collectible_{i}");
            item.transform.SetParent(parent);
            item.transform.position = new Vector3(5 + i * 6, 1.5f + (i % 2), 0);

            var sr = item.AddComponent<SpriteRenderer>();
            sr.color = new Color(1f, 0.85f, 0.2f);
            sr.sortingOrder = 6;

            var col = item.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.8f);
            col.isTrigger = true;

            item.AddComponent<Collectible>();
        }
    }

    private static void CreateSampleCheckpoints(Transform parent, int level)
    {
        int count = 1 + level / 2;
        for (int i = 0; i < count; i++)
        {
            var cp = new GameObject($"Checkpoint_{i}");
            cp.transform.SetParent(parent);
            cp.transform.position = new Vector3(10 + i * 15, 0, 0);

            var col = cp.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1, 2);
            col.isTrigger = true;

            cp.AddComponent<Checkpoint>();

            var sr = cp.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.2f, 0.8f, 0.3f);
            sr.sortingOrder = 3;
        }
    }

    // ==================== HUD设置 ====================

    private static void SetupHUDCanvas(GameObject canvas)
    {
        canvas.AddComponent<HUDManager>();

        // 左上：Lux血量
        var luxHP = CreateUIText(canvas.transform, "LuxHP", "Lux: ♥♥♥",
            new Vector2(-380, 440), 22, new Color(1f, 0.9f, 0.4f), TextAlignmentOptions.Left);

        // 右上：Nox血量
        var noxHP = CreateUIText(canvas.transform, "NoxHP", "Nox: ♥♥♥",
            new Vector2(380, 440), 22, new Color(0.5f, 0.3f, 0.8f), TextAlignmentOptions.Right);

        // 上中：关卡名
        CreateUIText(canvas.transform, "LevelName", "Level 1-1",
            new Vector2(0, 440), 20, Color.white, TextAlignmentOptions.Center);

        // 右上：计时器
        CreateUIText(canvas.transform, "Timer", "00:00",
            new Vector2(380, 410), 18, Color.white, TextAlignmentOptions.Right);

        // 左下：收集品
        CreateUIText(canvas.transform, "Collectibles", "★ 0/5",
            new Vector2(-380, -420), 18, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Left);

        // 右下：连击
        CreateUIText(canvas.transform, "ComboText", "",
            new Vector2(380, -350), 28, new Color(1f, 0.5f, 0.2f), TextAlignmentOptions.Right);

        // 中上：合作能量条
        var coopBarBG = CreateUIImage(canvas.transform, "CoopBarBG",
            new Vector2(0, 400), new Vector2(200, 10),
            new Color(0.2f, 0.2f, 0.25f));
        CreateUIImage(coopBarBG.transform, "CoopBarFill",
            Vector2.zero, new Vector2(200, 10),
            new Color(0.3f, 0.8f, 1f));

        // 暂停按钮
        CreateUIButton(canvas.transform, "PauseButton", "⏸",
            new Vector2(460, 440), new Vector2(50, 50));
    }

    private static void SetupPauseCanvas(GameObject canvas)
    {
        canvas.AddComponent<PauseMenuUI>();

        var panel = CreateUIPanel(canvas.transform, "PausePanel",
            Vector2.zero, new Vector2(500, 600));

        CreateUIText(panel.transform, "PauseTitle", "暂停",
            new Vector2(0, 220), 40, Color.white, TextAlignmentOptions.Center);

        CreateUIButton(panel.transform, "ResumeButton", "继续游戏",
            new Vector2(0, 100), new Vector2(300, 55));
        CreateUIButton(panel.transform, "RestartButton", "重新开始",
            new Vector2(0, 30), new Vector2(300, 55));
        CreateUIButton(panel.transform, "SettingsButton", "设置",
            new Vector2(0, -40), new Vector2(300, 55));
        CreateUIButton(panel.transform, "QuitButton", "返回菜单",
            new Vector2(0, -110), new Vector2(300, 55));
    }

    // ==================== UI辅助 ====================

    private static GameObject CreateUICanvas(string name, int refWidth, int refHeight)
    {
        var obj = new GameObject(name);
        var canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = obj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(refWidth, refHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        obj.AddComponent<GraphicRaycaster>();
        return obj;
    }

    private static GameObject CreateUIText(Transform parent, string name, string text,
        Vector2 anchoredPos, int fontSize, Color color, TextAlignmentOptions alignment)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;

        var rect = obj.GetComponent<RectTransform>();
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(400, 50);

        return obj;
    }

    private static GameObject CreateUIButton(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 size)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var image = obj.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.28f, 0.9f);

        var btn = obj.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.4f);
        colors.pressedColor = new Color(0.15f, 0.15f, 0.2f);
        btn.colors = colors;

        // 文字
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return obj;
    }

    private static GameObject CreateUIImage(Transform parent, string name,
        Vector2 anchoredPos, Vector2 size, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var img = obj.AddComponent<Image>();
        img.color = color;

        return obj;
    }

    private static GameObject CreateUIPanel(Transform parent, string name,
        Vector2 anchoredPos, Vector2 size)
    {
        var obj = CreateUIImage(parent, name, anchoredPos, size,
            new Color(0.1f, 0.1f, 0.15f, 0.85f));
        obj.AddComponent<CanvasGroup>();
        return obj;
    }

    private static void CreateEventSystem()
    {
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            return;

        var obj = new GameObject("EventSystem");
        obj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        obj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    // ==================== Build Settings 注册 ====================

    private static void RegisterScenesToBuildSettings()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();

        // Boot -> MainMenu -> Loading -> Chapters -> BossRush -> Credits
        AddSceneToBuild(scenes, $"{SCENE_DIR}/Boot.unity");
        AddSceneToBuild(scenes, $"{SCENE_DIR}/MainMenu.unity");
        AddSceneToBuild(scenes, $"{SCENE_DIR}/Loading.unity");

        for (int ch = 1; ch <= 5; ch++)
            for (int lv = 1; lv <= 4; lv++)
                AddSceneToBuild(scenes, $"{SCENE_DIR}/Chapter{ch}/Level_{ch}_{lv}.unity");

        AddSceneToBuild(scenes, $"{SCENE_DIR}/BossRush.unity");
        AddSceneToBuild(scenes, $"{SCENE_DIR}/Credits.unity");

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[SceneFactory] Registered {scenes.Count} scenes to Build Settings");
    }

    private static void AddSceneToBuild(System.Collections.Generic.List<EditorBuildSettingsScene> list,
        string path)
    {
        if (File.Exists(path))
            list.Add(new EditorBuildSettingsScene(path, true));
    }

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
