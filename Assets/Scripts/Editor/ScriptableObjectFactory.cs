using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject工厂 - 创建所有数据资产
/// 包含：20关LevelData、20关LevelConfig、技能数据、章节剧情数据
/// </summary>
public static class ScriptableObjectFactory
{
    private const string SO_DIR = "Assets/ScriptableObjects";

    // 关卡元数据
    private static readonly string[,] LevelNames = {
        // Ch1: 光影神殿
        { "觉醒之光", "并肩同行", "光影交织", "古树守卫" },
        // Ch2: 冰火交界
        { "冰封隘口", "熔岩之心", "齿轮圣殿", "冰焰巨像" },
        // Ch3: 沙海迷城
        { "流沙之门", "暗流涌动", "荧光洞窟", "沙暴魔灵" },
        // Ch4: 暗影深渊
        { "深渊边缘", "倒悬之塔", "重力漩涡", "深渊巨蛇" },
        // Ch5: 双向归一
        { "虚空门槛", "破碎世界", "记忆回廊", "虚空本体" },
    };

    private static readonly string[,] LevelNamesEN = {
        { "Awakening Light", "Walking Together", "Intertwined", "Forest Guardian" },
        { "Frozen Pass", "Heart of Lava", "Gear Sanctuary", "Ice-Flame Titan" },
        { "Quicksand Gate", "Dark Currents", "Luminescent Cave", "Sandstorm Djinn" },
        { "Edge of Abyss", "Inverted Tower", "Gravity Vortex", "Abyssal Serpent" },
        { "Void Threshold", "Fractured World", "Memory Corridor", "Void Entity" },
    };

    // 每关标准时间(秒)
    private static readonly float[,] ParTimes = {
        { 90, 120, 150, 300 },
        { 120, 150, 180, 360 },
        { 150, 180, 200, 360 },
        { 180, 200, 240, 420 },
        { 200, 240, 270, 480 },
    };

    // 每关收集品数量
    private static readonly int[,] CollectibleCounts = {
        { 3, 4, 5, 3 },
        { 4, 5, 5, 3 },
        { 5, 5, 6, 4 },
        { 5, 6, 6, 4 },
        { 6, 6, 7, 5 },
    };

    [MenuItem("DoubleForward/Create All ScriptableObjects", false, 55)]
    public static void CreateAll()
    {
        EditorUtility.DisplayProgressBar("Creating ScriptableObjects", "LevelData...", 0.1f);
        CreateAllLevelData();

        EditorUtility.DisplayProgressBar("Creating ScriptableObjects", "LevelConfig...", 0.3f);
        CreateAllLevelConfig();

        EditorUtility.DisplayProgressBar("Creating ScriptableObjects", "AbilityData...", 0.5f);
        CreateAbilityData();

        EditorUtility.DisplayProgressBar("Creating ScriptableObjects", "ChapterStoryData...", 0.7f);
        CreateChapterStoryData();

        EditorUtility.DisplayProgressBar("Creating ScriptableObjects", "LevelDataCatalog...", 0.9f);
        CreateLevelDataCatalog();

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SOFactory] All ScriptableObjects created.");
    }

    // ==================== LevelData ====================

    private static void CreateAllLevelData()
    {
        string dir = $"{SO_DIR}/LevelData";
        EnsureDir(dir);

        for (int ch = 1; ch <= 5; ch++)
        {
            for (int lv = 1; lv <= 4; lv++)
            {
                string assetName = $"Ch{ch}_Lv{lv}_{LevelNamesEN[ch - 1, lv - 1].Replace(" ", "")}";
                string path = $"{dir}/{assetName}.asset";
                if (File.Exists(path)) continue;

                var data = ScriptableObject.CreateInstance<LevelData>();
                data.chapter = ch;
                data.levelIndex = lv;
                data.levelName = LevelNames[ch - 1, lv - 1];
                data.description = $"第{ch}章 第{lv}关 - {LevelNames[ch - 1, lv - 1]}";
                data.sceneName = $"Level_{ch}_{lv}";

                // 出生点（随关卡复杂度调整）
                data.luxSpawnPoint = new Vector2(-3, 0);
                data.noxSpawnPoint = new Vector2(-1, 0);

                data.parTime = ParTimes[ch - 1, lv - 1];
                data.hasTimerChallenge = (lv >= 3);
                data.collectibleCount = CollectibleCounts[ch - 1, lv - 1];

                AssetDatabase.CreateAsset(data, path);
            }
        }
        Debug.Log("[SOFactory] Created 20 LevelData assets");
    }

    // ==================== LevelConfig ====================

    private static void CreateAllLevelConfig()
    {
        string dir = $"{SO_DIR}/LevelConfig";
        EnsureDir(dir);

        for (int ch = 1; ch <= 5; ch++)
        {
            for (int lv = 1; lv <= 4; lv++)
            {
                string assetName = $"Config_Ch{ch}_Lv{lv}";
                string path = $"{dir}/{assetName}.asset";
                if (File.Exists(path)) continue;

                var config = ScriptableObject.CreateInstance<LevelConfig>();
                config.chapter = ch;
                config.level = lv;
                config.levelName = LevelNames[ch - 1, lv - 1];
                config.levelNameKey = $"level_name_{ch}_{lv}";

                // 地图范围（随章节递增）
                float mapWidth = 40 + ch * 5 + lv * 3;
                config.mapMin = new Vector2(-5, -10);
                config.mapMax = new Vector2(mapWidth, 20);
                config.cameraBoundsMin = new Vector2(-3, -5);
                config.cameraBoundsMax = new Vector2(mapWidth - 2, 15);

                config.luxSpawnPoint = new Vector2(-3, 0);
                config.noxSpawnPoint = new Vector2(-1, 0);

                config.parTime = ParTimes[ch - 1, lv - 1];
                config.totalCollectibles = CollectibleCounts[ch - 1, lv - 1];
                config.requireBothPlayersAtGoal = true;

                // 目标位置
                config.goalPosition = new Vector2(mapWidth - 5, 0);

                // 谜题元素
                config.puzzles = GeneratePuzzlePlacements(ch, lv);

                // 敌人配置
                bool isBoss = (lv == 4);
                config.enemies = isBoss
                    ? new List<LevelConfig.EnemyPlacement>()
                    : GenerateEnemyPlacements(ch, lv);

                // 收集品位置
                config.collectiblePositions = GenerateCollectiblePositions(ch, lv);

                // 检查点
                config.checkpoints = GenerateCheckpoints(ch, lv);

                // 移动平台
                config.movingPlatforms = GeneratePlatforms(ch, lv);

                // 传送门（ch3+）
                config.portalPairs = (ch >= 3) ? GeneratePortals(ch, lv) : new List<LevelConfig.PortalPair>();

                // 环境区域
                config.zones = GenerateZones(ch, lv);

                AssetDatabase.CreateAsset(config, path);
            }
        }
        Debug.Log("[SOFactory] Created 20 LevelConfig assets");
    }

    // ==================== AbilityData ====================

    private static void CreateAbilityData()
    {
        string dir = $"{SO_DIR}/Abilities";
        EnsureDir(dir);

        // Lux技能
        CreateAbility(dir, "Lux_LightBeam", "光之射线", "发射一道光束照亮前方并造成伤害",
            "Lux", 0, 3f, 0.5f, 15f, 8f);
        CreateAbility(dir, "Lux_LightShield", "光之护盾", "展开光盾短暂抵挡伤害",
            "Lux", 1, 8f, 2f, 0f, 0f);
        CreateAbility(dir, "Lux_LightDash", "闪光突进", "化为光线快速突进",
            "Lux", 2, 5f, 0.3f, 10f, 12f);

        // Nox技能
        CreateAbility(dir, "Nox_ShadowStrike", "暗影突击", "从阴影中发动突袭造成高额伤害",
            "Nox", 0, 4f, 0.4f, 25f, 6f);
        CreateAbility(dir, "Nox_ShadowPhase", "暗影穿越", "化为阴影穿过障碍物",
            "Nox", 1, 6f, 1.5f, 0f, 0f);
        CreateAbility(dir, "Nox_ShadowTrap", "暗影陷阱", "在地面放置减速陷阱",
            "Nox", 2, 10f, 5f, 8f, 4f);

        // 合作技能
        CreateAbility(dir, "Coop_LightBridge", "光桥", "Lux创建光之桥梁供两人通行",
            "Coop", 0, 12f, 4f, 0f, 0f);
        CreateAbility(dir, "Coop_DualBlast", "光暗齐发", "两人同时释放光暗能量造成范围伤害",
            "Coop", 1, 15f, 1f, 40f, 5f);

        Debug.Log("[SOFactory] Created 8 AbilityData assets");
    }

    private static void CreateAbility(string dir, string fileName, string abilityName, string desc,
        string owner, int index, float cooldown, float duration, float damage, float range)
    {
        string path = $"{dir}/{fileName}.asset";
        if (File.Exists(path)) return;

        var data = ScriptableObject.CreateInstance<AbilityData>();

        // 使用SerializedObject设置字段
        AssetDatabase.CreateAsset(data, path);
        var so = new SerializedObject(data);

        SetPropertyString(so, "abilityName", abilityName);
        SetPropertyString(so, "description", desc);
        SetPropertyFloat(so, "cooldownTime", cooldown);
        SetPropertyFloat(so, "duration", duration);
        SetPropertyFloat(so, "damage", damage);
        SetPropertyFloat(so, "range", range);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    // ==================== ChapterStoryData ====================

    private static void CreateChapterStoryData()
    {
        string dir = $"{SO_DIR}/Story";
        EnsureDir(dir);

        string[] chapterTitles = { "光影神殿", "冰火交界", "沙海迷城", "暗影深渊", "双向归一" };

        string[] openingKeys = {
            "ch1_opening", "ch2_opening", "ch3_opening", "ch4_opening", "ch5_opening"
        };

        for (int ch = 1; ch <= 5; ch++)
        {
            string path = $"{dir}/Chapter{ch}_StoryData.asset";
            if (File.Exists(path)) continue;

            var data = ScriptableObject.CreateInstance<ChapterStoryData>();
            AssetDatabase.CreateAsset(data, path);

            var so = new SerializedObject(data);
            SetPropertyInt(so, "chapter", ch);
            SetPropertyString(so, "chapterTitle", chapterTitles[ch - 1]);
            SetPropertyString(so, "chapterTitleKey", $"chapter_{ch}_title");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }

        Debug.Log("[SOFactory] Created 5 ChapterStoryData assets");
    }

    // ==================== LevelDataCatalog ====================

    private static void CreateLevelDataCatalog()
    {
        string path = $"{SO_DIR}/LevelDataCatalog.asset";
        if (File.Exists(path)) return;

        var catalog = ScriptableObject.CreateInstance<LevelDataCatalog>();
        AssetDatabase.CreateAsset(catalog, path);

        // LevelDataCatalog使用chapters列表，内含ChapterInfo→LevelEntry
        // 由于InitializeDefaults()会在运行时自动创建默认数据，
        // 这里我们通过SerializedObject尝试关联已有的LevelData资产
        var so = new SerializedObject(catalog);
        var chaptersProperty = so.FindProperty("chapters");
        if (chaptersProperty != null)
        {
            chaptersProperty.ClearArray();

            string[] worldNames = { "光影神殿", "冰火交界", "沙海迷城", "暗影深渊", "双向归一" };
            string[] worldThemes = { "LightShadow", "IceFire", "SandSea", "DarkAbyss", "Convergence" };

            for (int ch = 1; ch <= 5; ch++)
            {
                chaptersProperty.InsertArrayElementAtIndex(ch - 1);
                var chapterElement = chaptersProperty.GetArrayElementAtIndex(ch - 1);

                var chIndexProp = chapterElement.FindPropertyRelative("chapterIndex");
                if (chIndexProp != null) chIndexProp.intValue = ch;

                var chNameProp = chapterElement.FindPropertyRelative("chapterName");
                if (chNameProp != null) chNameProp.stringValue = worldNames[ch - 1];

                var chNameKeyProp = chapterElement.FindPropertyRelative("chapterNameKey");
                if (chNameKeyProp != null) chNameKeyProp.stringValue = $"chapter_{ch}_name";

                var themeProp = chapterElement.FindPropertyRelative("worldTheme");
                if (themeProp != null) themeProp.stringValue = worldThemes[ch - 1];

                // 关联LevelEntry
                var levelsProp = chapterElement.FindPropertyRelative("levels");
                if (levelsProp != null)
                {
                    levelsProp.ClearArray();
                    for (int lv = 1; lv <= 4; lv++)
                    {
                        levelsProp.InsertArrayElementAtIndex(lv - 1);
                        var levelEntry = levelsProp.GetArrayElementAtIndex(lv - 1);

                        // LevelData引用
                        string assetName = $"Ch{ch}_Lv{lv}_{LevelNamesEN[ch - 1, lv - 1].Replace(" ", "")}";
                        string ldPath = $"{SO_DIR}/LevelData/{assetName}.asset";
                        var levelData = AssetDatabase.LoadAssetAtPath<LevelData>(ldPath);

                        var ldProp = levelEntry.FindPropertyRelative("levelData");
                        if (ldProp != null && levelData != null)
                            ldProp.objectReferenceValue = levelData;

                        // LevelConfig引用
                        string configPath = $"{SO_DIR}/LevelConfig/Config_Ch{ch}_Lv{lv}.asset";
                        var configData = AssetDatabase.LoadAssetAtPath<LevelConfig>(configPath);

                        var lcProp = levelEntry.FindPropertyRelative("levelConfig");
                        if (lcProp != null && configData != null)
                            lcProp.objectReferenceValue = configData;

                        var sceneProp = levelEntry.FindPropertyRelative("sceneName");
                        if (sceneProp != null) sceneProp.stringValue = $"Level_{ch}_{lv}";

                        var bossProp = levelEntry.FindPropertyRelative("isBossLevel");
                        if (bossProp != null) bossProp.boolValue = (lv == 4);
                    }
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(catalog);
        Debug.Log("[SOFactory] Created LevelDataCatalog with 5 chapters × 4 levels");
    }

    // ==================== 关卡内容生成器 ====================

    private static List<LevelConfig.PuzzlePlacement> GeneratePuzzlePlacements(int ch, int lv)
    {
        var list = new List<LevelConfig.PuzzlePlacement>();
        int count = 2 + lv;
        float spacing = 10f;

        for (int i = 0; i < count; i++)
        {
            var puzzle = new LevelConfig.PuzzlePlacement();
            puzzle.position = new Vector2(8 + i * spacing, 0);
            puzzle.size = Vector2.one;
            puzzle.requireBothPlayers = (i % 2 == 1);

            // 根据章节选择谜题类型
            int typeIndex = (i + ch) % 5;
            puzzle.type = (LevelConfig.PuzzleType)typeIndex;

            list.Add(puzzle);
        }
        return list;
    }

    private static List<LevelConfig.EnemyPlacement> GenerateEnemyPlacements(int ch, int lv)
    {
        var list = new List<LevelConfig.EnemyPlacement>();
        int count = 2 + lv + (ch - 1);
        float spacing = 8f;

        for (int i = 0; i < count; i++)
        {
            var enemy = new LevelConfig.EnemyPlacement();
            enemy.position = new Vector2(10 + i * spacing, 0);
            enemy.detectionRange = 6f + ch;
            enemy.respawns = false;

            int typeIndex = (i + ch) % 4;
            enemy.type = (LevelConfig.EnemyType)typeIndex;

            enemy.patrolPath = new Vector2[] {
                enemy.position + new Vector2(-3, 0),
                enemy.position + new Vector2(3, 0)
            };

            list.Add(enemy);
        }
        return list;
    }

    private static List<Vector2> GenerateCollectiblePositions(int ch, int lv)
    {
        var list = new List<Vector2>();
        int count = CollectibleCounts[ch - 1, lv - 1];
        float spacing = (35f + ch * 3) / count;

        for (int i = 0; i < count; i++)
        {
            float x = 5 + i * spacing;
            float y = 1.5f + (i % 3) * 1.5f;
            list.Add(new Vector2(x, y));
        }
        return list;
    }

    private static List<LevelConfig.CheckpointPlacement> GenerateCheckpoints(int ch, int lv)
    {
        var list = new List<LevelConfig.CheckpointPlacement>();
        int count = 1 + (lv > 2 ? 1 : 0);
        float mapWidth = 40 + ch * 5 + lv * 3;

        for (int i = 0; i < count; i++)
        {
            list.Add(new LevelConfig.CheckpointPlacement
            {
                position = new Vector2(mapWidth * (i + 1) / (count + 1), 0),
                order = i
            });
        }
        return list;
    }

    private static List<LevelConfig.PlatformPlacement> GeneratePlatforms(int ch, int lv)
    {
        var list = new List<LevelConfig.PlatformPlacement>();
        int count = 1 + lv / 2;

        for (int i = 0; i < count; i++)
        {
            list.Add(new LevelConfig.PlatformPlacement
            {
                startPosition = new Vector2(12 + i * 15, 2 + i),
                waypoints = new Vector2[] {
                    new Vector2(12 + i * 15, 2 + i),
                    new Vector2(12 + i * 15 + 5, 2 + i + 3)
                },
                speed = 1.5f + ch * 0.3f,
                loop = true
            });
        }
        return list;
    }

    private static List<LevelConfig.PortalPair> GeneratePortals(int ch, int lv)
    {
        var list = new List<LevelConfig.PortalPair>();
        if (lv >= 2)
        {
            list.Add(new LevelConfig.PortalPair
            {
                portalA = new Vector2(15, 0),
                portalB = new Vector2(30, 5),
                portalColor = Color.cyan
            });
        }
        return list;
    }

    private static List<LevelConfig.ZonePlacement> GenerateZones(int ch, int lv)
    {
        var list = new List<LevelConfig.ZonePlacement>();

        // 每章特色区域
        switch (ch)
        {
            case 1: // 光影
                list.Add(new LevelConfig.ZonePlacement {
                    type = LevelConfig.ZoneType.LightZone,
                    position = new Vector2(20, 2), size = new Vector2(6, 6), intensity = 1f
                });
                list.Add(new LevelConfig.ZonePlacement {
                    type = LevelConfig.ZoneType.ShadowZone,
                    position = new Vector2(30, 2), size = new Vector2(6, 6), intensity = 0.8f
                });
                break;
            case 2: // 冰火
                list.Add(new LevelConfig.ZonePlacement {
                    type = LevelConfig.ZoneType.Hazard,
                    position = new Vector2(25, -1), size = new Vector2(8, 2), intensity = 1f
                });
                break;
            case 3: // 沙海
                list.Add(new LevelConfig.ZonePlacement {
                    type = LevelConfig.ZoneType.WaterCurrent,
                    position = new Vector2(18, 0), size = new Vector2(10, 4), intensity = 0.6f
                });
                list.Add(new LevelConfig.ZonePlacement {
                    type = LevelConfig.ZoneType.GravityZone,
                    position = new Vector2(32, 3), size = new Vector2(5, 5), intensity = 0.5f
                });
                break;
            case 4: // 深渊
                list.Add(new LevelConfig.ZonePlacement {
                    type = LevelConfig.ZoneType.ShadowZone,
                    position = new Vector2(15, 2), size = new Vector2(10, 8), intensity = 1f
                });
                list.Add(new LevelConfig.ZonePlacement {
                    type = LevelConfig.ZoneType.GravityZone,
                    position = new Vector2(28, 5), size = new Vector2(6, 6), intensity = -0.5f
                });
                break;
            case 5: // 双向
                list.Add(new LevelConfig.ZonePlacement {
                    type = LevelConfig.ZoneType.LightZone,
                    position = new Vector2(15, 3), size = new Vector2(5, 5), intensity = 1f
                });
                list.Add(new LevelConfig.ZonePlacement {
                    type = LevelConfig.ZoneType.ShadowZone,
                    position = new Vector2(25, 3), size = new Vector2(5, 5), intensity = 1f
                });
                list.Add(new LevelConfig.ZonePlacement {
                    type = LevelConfig.ZoneType.GravityZone,
                    position = new Vector2(35, 3), size = new Vector2(8, 8), intensity = 0.3f
                });
                break;
        }

        return list;
    }

    // ==================== 辅助 ====================

    private static void SetPropertyString(SerializedObject so, string name, string value)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.stringValue = value;
    }

    private static void SetPropertyFloat(SerializedObject so, string name, float value)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.floatValue = value;
    }

    private static void SetPropertyInt(SerializedObject so, string name, int value)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.intValue = value;
    }

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
