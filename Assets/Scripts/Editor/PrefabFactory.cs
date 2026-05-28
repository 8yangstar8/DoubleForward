using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 预制体工厂 - 创建所有游戏预制体
/// 包含：玩家、敌人、Boss、谜题机关、环境物件、管理器、UI
/// 在Unity中通过菜单 DoubleForward/Create All Prefabs 执行
/// </summary>
public static class PrefabFactory
{
    private const string PREFAB_DIR = "Assets/Prefabs";
    private const string SPRITE_DIR = "Assets/Art/Placeholders";
    private const string ANIM_DIR = "Assets/Animations/Controllers";

    [MenuItem("DoubleForward/Create All Prefabs", false, 53)]
    public static void CreateAll()
    {
        if (!PlaceholderSpriteGenerator.HasPlaceholders())
        {
            if (EditorUtility.DisplayDialog("Missing Sprites",
                "Placeholder sprites not found. Generate them first?", "Yes", "Cancel"))
            {
                PlaceholderSpriteGenerator.GenerateAll();
            }
            else return;
        }

        EditorUtility.DisplayProgressBar("Creating Prefabs", "Players...", 0.05f);
        CreatePlayerPrefabs();

        EditorUtility.DisplayProgressBar("Creating Prefabs", "Enemies...", 0.15f);
        CreateEnemyPrefabs();

        EditorUtility.DisplayProgressBar("Creating Prefabs", "Bosses...", 0.25f);
        CreateBossPrefabs();

        EditorUtility.DisplayProgressBar("Creating Prefabs", "Puzzle elements...", 0.40f);
        CreatePuzzlePrefabs();

        EditorUtility.DisplayProgressBar("Creating Prefabs", "Environment...", 0.55f);
        CreateEnvironmentPrefabs();

        EditorUtility.DisplayProgressBar("Creating Prefabs", "Managers...", 0.70f);
        CreateManagerPrefabs();

        EditorUtility.DisplayProgressBar("Creating Prefabs", "VFX...", 0.80f);
        CreateVFXPrefabs();

        EditorUtility.DisplayProgressBar("Creating Prefabs", "UI...", 0.90f);
        CreateUIPrefabs();

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[PrefabFactory] All prefabs created successfully.");
    }

    // ==================== 玩家预制体 ====================

    private static void CreatePlayerPrefabs()
    {
        EnsureDir($"{PREFAB_DIR}/Player");

        CreatePlayerPrefab("Lux", PlayerController.PlayerType.Lux,
            new Color(1f, 0.9f, 0.4f), 0);
        CreatePlayerPrefab("Nox", PlayerController.PlayerType.Nox,
            new Color(0.35f, 0.15f, 0.6f), 1);
    }

    private static void CreatePlayerPrefab(string name, PlayerController.PlayerType type,
        Color color, int index)
    {
        string path = $"{PREFAB_DIR}/Player/{name}.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject(name);

        // -- SpriteRenderer --
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite($"Characters/{name}");
        sr.color = Color.white;
        sr.sortingOrder = 10;

        // -- Physics --
        var rb = obj.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 2.5f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.8f, 1.4f);
        col.offset = new Vector2(0f, 0.1f);

        // -- GroundCheck --
        var groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.SetParent(obj.transform);
        groundCheck.transform.localPosition = new Vector3(0, -0.65f, 0);

        // -- WallCheck --
        var wallCheck = new GameObject("WallCheck");
        wallCheck.transform.SetParent(obj.transform);
        wallCheck.transform.localPosition = new Vector3(0.5f, 0.2f, 0);

        // -- AttackPoint --
        var attackPoint = new GameObject("AttackPoint");
        attackPoint.transform.SetParent(obj.transform);
        attackPoint.transform.localPosition = new Vector3(0.8f, 0.2f, 0);

        // -- Core Scripts --
        obj.AddComponent<PlayerController>();
        obj.AddComponent<PlayerHealth>();
        obj.AddComponent<PlayerCombat>();
        obj.AddComponent<PlayerAnimator>();
        obj.AddComponent<PlayerInteraction>();
        obj.AddComponent<PlayerStatusEffect>();
        obj.AddComponent<PlayerBuffSystem>();
        obj.AddComponent<PlayerMovementTuning>();
        obj.AddComponent<PlayerFootsteps>();
        obj.AddComponent<PlayerTrailEffect>();

        // -- Abilities --
        if (type == PlayerController.PlayerType.Lux)
            obj.AddComponent<LuxAbilities>();
        else
            obj.AddComponent<NoxAbilities>();

        // -- Animator --
        var animator = obj.AddComponent<Animator>();
        var ctrlPath = $"{ANIM_DIR}/{name}Controller.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);
        if (ctrl != null) animator.runtimeAnimatorController = ctrl;

        // -- 子物体：光环/阴影 --
        var auraObj = new GameObject("Aura");
        auraObj.transform.SetParent(obj.transform);
        auraObj.transform.localPosition = Vector3.zero;
        var auraSR = auraObj.AddComponent<SpriteRenderer>();
        auraSR.sprite = LoadSprite("VFX/GlowSoft");
        auraSR.color = new Color(color.r, color.g, color.b, 0.3f);
        auraSR.sortingOrder = 9;
        auraObj.transform.localScale = Vector3.one * 2f;

        // 保存
        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
        Debug.Log($"[PrefabFactory] Created player prefab: {name}");
    }

    // ==================== 敌人预制体 ====================

    private static void CreateEnemyPrefabs()
    {
        EnsureDir($"{PREFAB_DIR}/Enemies");

        CreateEnemyPrefab("ShadowSlime", "Enemies/ShadowSlime",
            100, 3f, 15f, 1.2f, 6f, new Vector2(0.9f, 0.7f));
        CreateEnemyPrefab("ShadowArcher", "Enemies/ShadowArcher",
            80, 2.5f, 12f, 8f, 10f, new Vector2(0.8f, 1.4f));
        CreateEnemyPrefab("ShadowGuard", "Enemies/ShadowGuard",
            150, 2f, 25f, 1.5f, 7f, new Vector2(1f, 1.4f));
        CreateEnemyPrefab("ShadowFlyer", "Enemies/ShadowFlyer",
            60, 4f, 10f, 1.5f, 12f, new Vector2(1.2f, 0.8f));

        // 射弹
        CreateProjectilePrefab("EnemyProjectile", "Enemies/Projectile",
            new Color(0.8f, 0.2f, 0.8f), 10f);
    }

    private static void CreateEnemyPrefab(string name, string spritePath,
        float hp, float speed, float dmg, float atkRange, float detectRange, Vector2 colSize)
    {
        string path = $"{PREFAB_DIR}/Enemies/{name}.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject(name);

        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite(spritePath);
        sr.sortingOrder = 8;

        var rb = obj.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 2.5f;

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = colSize;

        // 巡逻点（子物体）
        var patrol1 = new GameObject("PatrolPoint_0");
        patrol1.transform.SetParent(obj.transform);
        patrol1.transform.localPosition = new Vector3(-3, 0, 0);

        var patrol2 = new GameObject("PatrolPoint_1");
        patrol2.transform.SetParent(obj.transform);
        patrol2.transform.localPosition = new Vector3(3, 0, 0);

        // 添加对应敌人脚本
        switch (name)
        {
            case "ShadowSlime":
                obj.AddComponent<ShadowSlime>();
                break;
            case "ShadowArcher":
                var archer = obj.AddComponent<ShadowArcher>();
                var firePoint = new GameObject("FirePoint");
                firePoint.transform.SetParent(obj.transform);
                firePoint.transform.localPosition = new Vector3(0.5f, 0.3f, 0);
                break;
            case "ShadowGuard":
                obj.AddComponent<ShadowGuard>();
                break;
            case "ShadowFlyer":
                obj.AddComponent<ShadowFlyer>();
                rb.gravityScale = 0; // 飞行
                break;
        }

        // Animator
        var animator = obj.AddComponent<Animator>();
        var ctrlPath = $"{ANIM_DIR}/{name}Controller.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);
        if (ctrl != null) animator.runtimeAnimatorController = ctrl;

        // 血条锚点
        var hpAnchor = new GameObject("HPBarAnchor");
        hpAnchor.transform.SetParent(obj.transform);
        hpAnchor.transform.localPosition = new Vector3(0, colSize.y / 2 + 0.3f, 0);

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
        Debug.Log($"[PrefabFactory] Created enemy prefab: {name}");
    }

    private static void CreateProjectilePrefab(string name, string spritePath,
        Color color, float speed)
    {
        string path = $"{PREFAB_DIR}/Enemies/{name}.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject(name);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite(spritePath);
        sr.color = color;
        sr.sortingOrder = 12;

        var rb = obj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.freezeRotation = true;

        var col = obj.AddComponent<CircleCollider2D>();
        col.radius = 0.2f;
        col.isTrigger = true;

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    // ==================== Boss预制体 ====================

    private static void CreateBossPrefabs()
    {
        EnsureDir($"{PREFAB_DIR}/Bosses");

        CreateBossPrefab("ForestGuardian", "Bosses/ForestGuardian",
            typeof(ForestGuardianBoss), 20, new Vector2(3f, 4.5f));
        CreateBossPrefab("GearTyrant", "Bosses/IceFlameTitan",
            typeof(GearTyrantBoss), 25, new Vector2(4f, 4f));
        CreateBossPrefab("AbyssSerpent", "Bosses/AbyssalSerpent",
            typeof(AbyssSerpentBoss), 22, new Vector2(4.5f, 2.5f));
        CreateBossPrefab("RuinSentinel", "Bosses/SandstormDjinn",
            typeof(RuinSentinelBoss), 28, new Vector2(3f, 4f));
        CreateBossPrefab("VoidEntity", "Bosses/VoidEntity",
            typeof(VoidBoss), 30, new Vector2(4f, 4f));

        // Boss战场
        CreateBossArenaPrefab();

        // Boss投射物
        CreateProjectilePrefab("BossRootSwipe", "Bosses/RootSwipe",
            new Color(0.3f, 0.5f, 0.2f), 0f);
        CreateProjectilePrefab("BossLeafProjectile", "Bosses/LeafProjectile",
            new Color(0.4f, 0.7f, 0.2f), 5f);
        CreateProjectilePrefab("BossVineTrap", "Bosses/VineTrap",
            new Color(0.2f, 0.6f, 0.15f), 0f);
    }

    private static void CreateBossPrefab(string name, string spritePath,
        System.Type bossType, int hp, Vector2 colSize)
    {
        string path = $"{PREFAB_DIR}/Bosses/{name}.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject(name);

        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite(spritePath);
        sr.sortingOrder = 8;

        var rb = obj.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Kinematic;

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = colSize;

        obj.AddComponent(bossType);

        // Boss中心
        var center = new GameObject("BossCenter");
        center.transform.SetParent(obj.transform);
        center.transform.localPosition = Vector3.zero;

        // 核心（弱点）
        var core = new GameObject("Core");
        core.transform.SetParent(obj.transform);
        core.transform.localPosition = new Vector3(0, -0.5f, 0);
        var coreSR = core.AddComponent<SpriteRenderer>();
        coreSR.sprite = LoadSprite("Bosses/BossCore");
        coreSR.sortingOrder = 9;
        core.SetActive(false);

        // Animator
        var animator = obj.AddComponent<Animator>();
        var ctrlPath = $"{ANIM_DIR}/{name}Controller.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);
        if (ctrl != null) animator.runtimeAnimatorController = ctrl;

        // 血条锚点
        var hpAnchor = new GameObject("HPBarAnchor");
        hpAnchor.transform.SetParent(obj.transform);
        hpAnchor.transform.localPosition = new Vector3(0, colSize.y / 2 + 0.5f, 0);

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
        Debug.Log($"[PrefabFactory] Created boss prefab: {name}");
    }

    private static void CreateBossArenaPrefab()
    {
        string path = $"{PREFAB_DIR}/Bosses/BossArena.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject("BossArena");
        obj.AddComponent<BossArena>();

        // 边界墙
        var leftWall = CreateWall("LeftWall", new Vector3(-12, 4, 0), new Vector2(1, 12));
        leftWall.transform.SetParent(obj.transform);

        var rightWall = CreateWall("RightWall", new Vector3(12, 4, 0), new Vector2(1, 12));
        rightWall.transform.SetParent(obj.transform);

        // 地面
        var floor = CreateWall("Floor", new Vector3(0, -1, 0), new Vector2(24, 2));
        floor.transform.SetParent(obj.transform);

        // Boss出生点
        var bossSpawn = new GameObject("BossSpawnPoint");
        bossSpawn.transform.SetParent(obj.transform);
        bossSpawn.transform.localPosition = new Vector3(0, 3, 0);

        // 进入触发器
        var trigger = new GameObject("EntryTrigger");
        trigger.transform.SetParent(obj.transform);
        trigger.transform.localPosition = new Vector3(-10, 2, 0);
        var trigCol = trigger.AddComponent<BoxCollider2D>();
        trigCol.size = new Vector2(2, 6);
        trigCol.isTrigger = true;

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    private static GameObject CreateWall(string name, Vector3 pos, Vector2 size)
    {
        var wall = new GameObject(name);
        wall.transform.position = pos;
        var col = wall.AddComponent<BoxCollider2D>();
        col.size = size;
        wall.layer = LayerMask.NameToLayer("Default");
        return wall;
    }

    // ==================== 谜题预制体 ====================

    private static void CreatePuzzlePrefabs()
    {
        EnsureDir($"{PREFAB_DIR}/Puzzles");

        // 压力板
        CreateSimplePrefab("Puzzles/PressurePlate", "Puzzles/PressurePlate",
            typeof(PressurePlate), new Vector2(2f, 0.3f), true, false);

        // 光感应器
        CreateSimplePrefab("Puzzles/LightSensor", "Puzzles/LightSensor",
            typeof(LightSensor), new Vector2(1.2f, 1.2f), true, false);

        // 影墙
        CreateSimplePrefab("Puzzles/ShadowWall", "Puzzles/ShadowWall",
            typeof(ShadowWall), new Vector2(1f, 3f), false, false);

        // 传送门
        CreateSimplePrefab("Puzzles/Portal", "Puzzles/Portal",
            typeof(Portal), new Vector2(1.5f, 2f), true, false);

        // 齿轮
        CreateSimplePrefab("Puzzles/GearMechanism", "Puzzles/GearMechanism",
            typeof(GearMechanism), new Vector2(2f, 2f), true, false);

        // 收集品
        CreateSimplePrefab("Puzzles/Collectible", "Puzzles/Collectible",
            typeof(Collectible), new Vector2(0.8f, 0.8f), true, true);

        // 检查点
        CreateCheckpointPrefab();

        // 关卡终点
        CreateGoalPrefab();

        // 合作机关
        CreateSimplePrefab("Puzzles/CoopMechanism", "Puzzles/PressurePlate",
            typeof(CoopMechanism), new Vector2(3f, 0.4f), true, false);
    }

    private static void CreateSimplePrefab(string prefabPath, string spritePath,
        System.Type componentType, Vector2 colSize, bool isTrigger, bool isCollectible)
    {
        string fullPath = $"{PREFAB_DIR}/{prefabPath}.prefab";
        if (File.Exists(fullPath)) return;

        string name = Path.GetFileNameWithoutExtension(prefabPath);
        var obj = new GameObject(name);

        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite(spritePath);
        sr.sortingOrder = isCollectible ? 6 : 3;

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = colSize;
        col.isTrigger = isTrigger;

        if (componentType != null)
            obj.AddComponent(componentType);

        PrefabUtility.SaveAsPrefabAsset(obj, fullPath);
        Object.DestroyImmediate(obj);
    }

    private static void CreateCheckpointPrefab()
    {
        string path = $"{PREFAB_DIR}/Puzzles/Checkpoint.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject("Checkpoint");
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite("Puzzles/Checkpoint");
        sr.sortingOrder = 3;

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 2f);
        col.isTrigger = true;

        obj.AddComponent<Checkpoint>();

        // 重生点
        var respawn = new GameObject("RespawnPoint");
        respawn.transform.SetParent(obj.transform);
        respawn.transform.localPosition = new Vector3(0, 0.5f, 0);

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    private static void CreateGoalPrefab()
    {
        string path = $"{PREFAB_DIR}/Puzzles/LevelGoal.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject("LevelGoal");
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite("Puzzles/GoalFlag");
        sr.sortingOrder = 5;

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(2f, 3f);
        col.isTrigger = true;

        obj.AddComponent<LevelGoalTrigger>();

        // 发光效果
        var glow = new GameObject("Glow");
        glow.transform.SetParent(obj.transform);
        glow.transform.localPosition = Vector3.zero;
        var glowSR = glow.AddComponent<SpriteRenderer>();
        glowSR.sprite = LoadSprite("VFX/GlowSoft");
        glowSR.color = new Color(0.3f, 1f, 0.5f, 0.3f);
        glowSR.sortingOrder = 4;
        glow.transform.localScale = Vector3.one * 3f;

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    // ==================== 环境预制体 ====================

    private static void CreateEnvironmentPrefabs()
    {
        EnsureDir($"{PREFAB_DIR}/Environment");

        // 移动平台
        CreateMovingPlatformPrefab();

        // 单向平台
        CreateSimplePrefab("Environment/OneWayPlatform", "Environment/OneWayPlatform",
            typeof(OneWayPlatform), new Vector2(4f, 0.3f), false, false);

        // 弹跳板
        CreateSimplePrefab("Environment/BouncePad", "Environment/BouncePad",
            typeof(BouncePad), new Vector2(2f, 0.5f), true, false);

        // 梯子
        CreateSimplePrefab("Environment/Ladder", "Environment/Ladder",
            typeof(Ladder), new Vector2(1f, 4f), true, false);

        // 摆绳
        CreateSimplePrefab("Environment/SwingRope", "Environment/SwingRope",
            typeof(SwingRope), new Vector2(0.3f, 4f), true, false);

        // 伤害区
        CreateHazardPrefab("Spike", "Environment/Spike", new Vector2(1f, 0.5f));

        // 强化拾取
        CreateSimplePrefab("Environment/PowerUp", "Environment/PowerUp",
            typeof(PowerUpPickup), new Vector2(0.8f, 0.8f), true, true);

        // 遗物拾取
        CreateSimplePrefab("Environment/RelicPickup", "Environment/RelicPickup",
            typeof(RelicPickup), new Vector2(1f, 1f), true, true);

        // 死亡区域
        CreateDeathZonePrefab();

        // 伤害区域
        CreateSimplePrefab("Environment/DamageZone", "Environment/Spike",
            typeof(DamageZone), new Vector2(3f, 1f), true, false);
    }

    private static void CreateMovingPlatformPrefab()
    {
        string path = $"{PREFAB_DIR}/Environment/MovingPlatform.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject("MovingPlatform");
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite("Environment/MovingPlatform");
        sr.sortingOrder = 2;

        var rb = obj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(4f, 0.5f);

        obj.AddComponent<MovingPlatform>();

        // 路径点
        var wp0 = new GameObject("Waypoint_0");
        wp0.transform.SetParent(obj.transform);
        wp0.transform.localPosition = new Vector3(-3, 0, 0);

        var wp1 = new GameObject("Waypoint_1");
        wp1.transform.SetParent(obj.transform);
        wp1.transform.localPosition = new Vector3(3, 0, 0);

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    private static void CreateHazardPrefab(string name, string spritePath, Vector2 colSize)
    {
        string path = $"{PREFAB_DIR}/Environment/{name}.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject(name);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite(spritePath);
        sr.sortingOrder = 2;

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = colSize;
        col.isTrigger = true;

        obj.AddComponent<Hazard>();

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    private static void CreateDeathZonePrefab()
    {
        string path = $"{PREFAB_DIR}/Environment/DeathZone.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject("DeathZone");
        var col = obj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(100f, 2f);
        col.isTrigger = true;

        obj.AddComponent<DeathZone>();

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    // ==================== Manager预制体 ====================

    public static void CreateManagerPrefabs()
    {
        EnsureDir($"{PREFAB_DIR}/Managers");

        // 第1层：基础核心
        CreateManagerPrefab("ObjectPool", typeof(ObjectPool));
        CreateManagerPrefab("AudioManager", typeof(AudioManager), obj =>
        {
            obj.AddComponent<AudioSource>().playOnAwake = false; // BGM
            obj.AddComponent<AudioSource>().playOnAwake = false; // SFX
            obj.AddComponent<AudioSource>().playOnAwake = false; // Ambient
        });
        CreateManagerPrefab("InputManager", typeof(InputManager));

        // 第2层：数据系统
        CreateManagerPrefab("SaveSystem", typeof(SaveSystem));
        CreateManagerPrefab("LocalizationSystem", typeof(LocalizationSystem));
        CreateManagerPrefab("SettingsPersistence", typeof(SettingsPersistence));
        CreateManagerPrefab("GameStats", typeof(GameStats));

        // 第3层：游戏逻辑
        CreateManagerPrefab("GameManager", typeof(GameManager));
        CreateManagerPrefab("DifficultyManager", typeof(DifficultyManager));
        CreateManagerPrefab("AchievementSystem", typeof(AchievementSystem));
        CreateManagerPrefab("ComboSystem", typeof(ComboSystem));
        CreateManagerPrefab("CurrencyManager", typeof(CurrencyManager));
        CreateManagerPrefab("ScoreManager", typeof(ScoreManager));
        CreateManagerPrefab("LeaderboardManager", typeof(LeaderboardManager));

        // 第4层：平台与性能
        CreateManagerPrefab("PerformanceManager", typeof(PerformanceManager));
        CreateManagerPrefab("ScreenAdapter", typeof(ScreenAdapter));
        CreateManagerPrefab("AccessibilityManager", typeof(AccessibilityManager));
        CreateManagerPrefab("GamepadAdapter", typeof(GamepadAdapter));
        CreateManagerPrefab("AndroidPermissionManager", typeof(AndroidPermissionManager));
        CreateManagerPrefab("HapticFeedback", typeof(HapticFeedback));
        CreateManagerPrefab("NotificationScheduler", typeof(NotificationScheduler));

        // 第5层：辅助服务
        CreateManagerPrefab("VFXManager", typeof(VFXManager));
        CreateManagerPrefab("SoundFeedback", typeof(SoundFeedback));
        CreateManagerPrefab("AnalyticsTracker", typeof(AnalyticsTracker));
        CreateManagerPrefab("MobileServices", typeof(MobileServices));
        CreateManagerPrefab("DailyRewardSystem", typeof(DailyRewardSystem));
        CreateManagerPrefab("SkinManager", typeof(SkinManager));
        CreateManagerPrefab("CloudSaveManager", typeof(CloudSaveManager));
        CreateManagerPrefab("TimeManager", typeof(TimeManager));
        CreateManagerPrefab("AutoSaveSystem", typeof(AutoSaveSystem));
        CreateManagerPrefab("AchievementTracker", typeof(AchievementTracker));

        // 第5.5层：游戏玩法
        CreateManagerPrefab("MusicLayerSystem", typeof(MusicLayerSystem));
        CreateManagerPrefab("GameplayTipSystem", typeof(GameplayTipSystem));
        CreateManagerPrefab("ChallengeMode", typeof(ChallengeMode));
        CreateManagerPrefab("PlayerCoopSync", typeof(PlayerCoopSync));
        CreateManagerPrefab("WorldProgressionManager", typeof(WorldProgressionManager));
        CreateManagerPrefab("EnemyDirector", typeof(EnemyDirector));
        CreateManagerPrefab("ScreenEffectsController", typeof(ScreenEffectsController));
        CreateManagerPrefab("EnvironmentEffectManager", typeof(EnvironmentEffectManager));
        CreateManagerPrefab("SecretAreaSystem", typeof(SecretAreaSystem));
        CreateManagerPrefab("GameEndingManager", typeof(GameEndingManager));
        CreateManagerPrefab("RelicSystem", typeof(RelicSystem));
        CreateManagerPrefab("AbilityComboSystem", typeof(AbilityComboSystem));
        CreateManagerPrefab("LevelModifierSystem", typeof(LevelModifierSystem));
        CreateManagerPrefab("PlayerProgressionSystem", typeof(PlayerProgressionSystem));
        CreateManagerPrefab("BossRushMode", typeof(BossRushMode));
        CreateManagerPrefab("NewGamePlusManager", typeof(NewGamePlusManager));
        CreateManagerPrefab("PlayerBondSystem", typeof(PlayerBondSystem));
        CreateManagerPrefab("StoryRecapSystem", typeof(StoryRecapSystem));

        // 场景/过渡
        CreateManagerPrefab("SceneLoader", typeof(SceneLoader));
        CreateManagerPrefab("SceneTransition", typeof(SceneTransition));
        CreateManagerPrefab("ScreenTransitionUI", typeof(ScreenTransitionUI));
        CreateManagerPrefab("LoadingScreenUI", typeof(LoadingScreenUI));

        // 扩展系统
        CreateManagerPrefab("AudioMixerSetup", typeof(AudioMixerSetup));
        CreateManagerPrefab("WorldThemeManager", typeof(WorldThemeManager));
        CreateManagerPrefab("SkillUpgradeSystem", typeof(SkillUpgradeSystem));
        CreateManagerPrefab("InputRemapper", typeof(InputRemapper));
        CreateManagerPrefab("InputRemapSystem", typeof(InputRemapSystem));

        // 第6层：流程控制
        CreateManagerPrefab("GameFlowManager", typeof(GameFlowManager));

        // 辅助
        CreateManagerPrefab("DebugOverlay", typeof(DebugOverlay));
    }

    private static void CreateManagerPrefab(string name, System.Type type,
        System.Action<GameObject> extraSetup = null)
    {
        string path = $"{PREFAB_DIR}/Managers/{name}.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject(name);
        obj.AddComponent(type);
        extraSetup?.Invoke(obj);

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    // ==================== VFX预制体 ====================

    private static void CreateVFXPrefabs()
    {
        EnsureDir($"{PREFAB_DIR}/VFX");

        CreateVFXPrefab("HitEffect", "VFX/Particle", Color.white, 0.5f);
        CreateVFXPrefab("DeathEffect", "VFX/Ring", Color.red, 1f);
        CreateVFXPrefab("HealEffect", "VFX/GlowSoft", Color.green, 0.8f);
        CreateVFXPrefab("CollectEffect", "VFX/Particle", new Color(1f, 0.85f, 0.2f), 0.5f);
        CreateVFXPrefab("LevelUpEffect", "VFX/Ring", new Color(1f, 0.85f, 0.2f), 1.5f);
        CreateVFXPrefab("DashTrail", "VFX/GlowSoft", new Color(0.5f, 0.8f, 1f, 0.5f), 0.3f);
        CreateVFXPrefab("RespawnEffect", "VFX/GlowSoft", new Color(0.3f, 1f, 0.5f), 1f);
    }

    private static void CreateVFXPrefab(string name, string spritePath, Color color, float lifetime)
    {
        string path = $"{PREFAB_DIR}/VFX/{name}.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject(name);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite(spritePath);
        sr.color = color;
        sr.sortingOrder = 20;

        // 简易自毁
        var autoDestroy = obj.AddComponent<AutoDestroyAfterTime>();

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    // ==================== UI预制体 ====================

    private static void CreateUIPrefabs()
    {
        EnsureDir($"{PREFAB_DIR}/UI");

        // 浮动伤害数字
        CreateFloatingTextPrefab("FloatingDamageText", Color.red);
        CreateFloatingTextPrefab("FloatingHealText", Color.green);
        CreateFloatingTextPrefab("FloatingExpText", new Color(0.3f, 0.8f, 1f));

        // 敌人血条
        CreateEnemyHPBarPrefab();

        // Boss血条
        CreateBossHPBarPrefab();

        // 成就通知
        CreateAchievementNotifPrefab();

        // 交互提示
        CreateInteractionPromptPrefab();
    }

    private static void CreateFloatingTextPrefab(string name, Color color)
    {
        string path = $"{PREFAB_DIR}/UI/{name}.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject(name);

        var canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        var rectTransform = obj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(2f, 0.5f);

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform);
        var text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        text.text = "0";
        text.color = color;
        text.fontSize = 4;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.fontStyle = TMPro.FontStyles.Bold;
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    private static void CreateEnemyHPBarPrefab()
    {
        string path = $"{PREFAB_DIR}/UI/EnemyHPBar.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject("EnemyHPBar");

        var canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        var rectTransform = obj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(1.5f, 0.15f);

        // 背景
        var bgObj = new GameObject("Background");
        bgObj.transform.SetParent(obj.transform);
        var bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        var bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // 填充
        var fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(obj.transform);
        var fillImage = fillObj.AddComponent<UnityEngine.UI.Image>();
        fillImage.color = new Color(0.9f, 0.2f, 0.2f);
        var fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(1, 1);
        fillRect.offsetMax = new Vector2(-1, -1);

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    private static void CreateBossHPBarPrefab()
    {
        string path = $"{PREFAB_DIR}/UI/BossHPBar.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject("BossHPBar");

        // 名字
        var nameObj = new GameObject("BossName");
        nameObj.transform.SetParent(obj.transform);
        var nameText = nameObj.AddComponent<TMPro.TextMeshProUGUI>();
        nameText.text = "BOSS";
        nameText.fontSize = 24;
        nameText.alignment = TMPro.TextAlignmentOptions.Center;
        nameText.color = Color.white;

        obj.AddComponent<BossHealthBar>();

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    private static void CreateAchievementNotifPrefab()
    {
        string path = $"{PREFAB_DIR}/UI/AchievementNotification.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject("AchievementNotification");

        var cg = obj.AddComponent<CanvasGroup>();

        var rectTransform = obj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(300, 60);

        // 背景
        var bg = obj.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

        // 标题
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(obj.transform);
        var title = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
        title.text = "Achievement Unlocked!";
        title.fontSize = 14;
        title.color = new Color(1f, 0.85f, 0.2f);
        title.alignment = TMPro.TextAlignmentOptions.TopLeft;

        // 描述
        var descObj = new GameObject("Description");
        descObj.transform.SetParent(obj.transform);
        var desc = descObj.AddComponent<TMPro.TextMeshProUGUI>();
        desc.text = "";
        desc.fontSize = 12;
        desc.color = Color.white;
        desc.alignment = TMPro.TextAlignmentOptions.BottomLeft;

        obj.AddComponent<AchievementNotificationUI>();

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    private static void CreateInteractionPromptPrefab()
    {
        string path = $"{PREFAB_DIR}/UI/InteractionPrompt.prefab";
        if (File.Exists(path)) return;

        var obj = new GameObject("InteractionPrompt");

        var canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 90;

        var rectTransform = obj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(2f, 0.5f);

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform);
        var text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        text.text = "Press E";
        text.fontSize = 3;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.color = Color.white;
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
    }

    // ==================== 辅助 ====================

    private static Sprite LoadSprite(string relativePath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITE_DIR}/{relativePath}.png");
    }

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}

