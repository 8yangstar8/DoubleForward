using UnityEngine;
using UnityEditor;
using System.Reflection;

/// <summary>
/// 项目层/标签/排序层配置 - 自动设置Unity项目所需的全部Layer、Tag、Sorting Layer
/// 菜单：DoubleForward / Setup Layers & Tags
/// </summary>
public static class ProjectLayerSetup
{
    // 自定义Layer（Unity预留0-7，我们从8开始）
    private static readonly (int index, string name)[] CustomLayers = {
        (8,  "Ground"),
        (9,  "Player"),
        (10, "Enemy"),
        (11, "Projectile"),
        (12, "Puzzle"),
        (13, "Interactable"),
        (14, "Trigger"),
        (15, "Water"),
        (16, "OneWayPlatform"),
        (17, "PlayerBullet"),
        (18, "EnemyBullet"),
        (19, "Boss"),
        (20, "Collectible"),
        (21, "Hazard"),
        (22, "InvisibleWall"),
    };

    // 自定义Sorting Layer（从后到前渲染顺序）
    private static readonly string[] SortingLayers = {
        "Background_Far",    // 最远背景
        "Background_Mid",    // 中景
        "Background_Near",   // 近景
        "Tilemap",          // 地图瓦片
        "Environment",       // 环境物体（平台、梯子等）
        "Puzzles",          // 谜题机关
        "Items",            // 收集品、拾取物
        "Enemies",          // 敌人
        "Players",          // 玩家角色
        "Projectiles",      // 弹射物
        "Effects",          // 特效
        "Foreground",       // 前景装饰
        "UI_World",         // 世界空间UI（血条、提示）
    };

    // 自定义Tag
    private static readonly string[] CustomTags = {
        "Lux",
        "Nox",
        "Enemy",
        "Boss",
        "Checkpoint",
        "LevelGoal",
        "Collectible",
        "Puzzle",
        "Hazard",
        "Interactable",
        "Respawn",
        "SecretArea",
        "DialogueTrigger",
        "Water",
        "Ladder",
        "MovingPlatform",
        "OneWayPlatform",
        "DeathZone",
        "BossArena",
        "Projectile",
    };

    [MenuItem("DoubleForward/Setup Layers && Tags", false, 1)]
    public static void SetupAll()
    {
        SetupLayers();
        SetupSortingLayers();
        SetupTags();
        SetupPhysicsMatrix();

        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectLayerSetup] All layers, sorting layers, tags, and physics matrix configured.");
        EditorUtility.DisplayDialog("配置完成",
            $"已配置：\n• {CustomLayers.Length} 个自定义Layer\n" +
            $"• {SortingLayers.Length} 个Sorting Layer\n" +
            $"• {CustomTags.Length} 个Tag\n" +
            "• 物理碰撞矩阵",
            "OK");
    }

    // ==================== Layer ====================

    private static void SetupLayers()
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        int addedCount = 0;
        foreach (var (index, name) in CustomLayers)
        {
            if (index >= layers.arraySize) continue;
            var element = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(element.stringValue))
            {
                element.stringValue = name;
                addedCount++;
            }
            else if (element.stringValue != name)
            {
                Debug.LogWarning($"[LayerSetup] Layer {index} already set to '{element.stringValue}', expected '{name}'");
            }
        }

        tagManager.ApplyModifiedProperties();
        Debug.Log($"[LayerSetup] Configured {addedCount} layers (of {CustomLayers.Length} total)");
    }

    // ==================== Sorting Layer ====================

    private static void SetupSortingLayers()
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty sortingLayers = tagManager.FindProperty("m_SortingLayers");

        int addedCount = 0;
        foreach (var layerName in SortingLayers)
        {
            if (!SortingLayerExists(sortingLayers, layerName))
            {
                AddSortingLayer(sortingLayers, layerName);
                addedCount++;
            }
        }

        tagManager.ApplyModifiedProperties();
        Debug.Log($"[LayerSetup] Added {addedCount} sorting layers (of {SortingLayers.Length} total)");
    }

    private static bool SortingLayerExists(SerializedProperty sortingLayers, string name)
    {
        for (int i = 0; i < sortingLayers.arraySize; i++)
        {
            var element = sortingLayers.GetArrayElementAtIndex(i);
            var nameProp = element.FindPropertyRelative("name");
            if (nameProp != null && nameProp.stringValue == name)
                return true;
        }
        return false;
    }

    private static void AddSortingLayer(SerializedProperty sortingLayers, string name)
    {
        sortingLayers.InsertArrayElementAtIndex(sortingLayers.arraySize);
        var newLayer = sortingLayers.GetArrayElementAtIndex(sortingLayers.arraySize - 1);

        var nameProp = newLayer.FindPropertyRelative("name");
        if (nameProp != null) nameProp.stringValue = name;

        var uniqueIDProp = newLayer.FindPropertyRelative("uniqueID");
        if (uniqueIDProp != null) uniqueIDProp.intValue = name.GetHashCode();
    }

    // ==================== Tag ====================

    private static void SetupTags()
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");

        int addedCount = 0;
        foreach (var tagName in CustomTags)
        {
            if (!TagExists(tags, tagName))
            {
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tagName;
                addedCount++;
            }
        }

        tagManager.ApplyModifiedProperties();
        Debug.Log($"[LayerSetup] Added {addedCount} tags (of {CustomTags.Length} total)");
    }

    private static bool TagExists(SerializedProperty tags, string name)
    {
        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == name)
                return true;
        }
        return false;
    }

    // ==================== Physics Matrix ====================

    private static void SetupPhysicsMatrix()
    {
        // 获取各层index
        int player = 9, enemy = 10, projectile = 11, puzzle = 12;
        int interactable = 13, trigger = 14, water = 15;
        int oneWay = 16, playerBullet = 17, enemyBullet = 18;
        int boss = 19, collectible = 20, hazard = 21;

        // 设置碰撞规则
        // Player碰撞：Ground, Enemy, Puzzle, Interactable, Water, OneWay, Boss, Collectible, Hazard, EnemyBullet
        // Player不碰：Projectile, PlayerBullet, Trigger

        // 玩家子弹不碰玩家
        IgnoreCollision(player, playerBullet, true);
        // 敌人子弹不碰敌人
        IgnoreCollision(enemy, enemyBullet, true);
        // 收集品不碰敌人
        IgnoreCollision(collectible, enemy, true);
        // 触发器之间不碰
        IgnoreCollision(trigger, trigger, true);
        // 玩家子弹和敌人子弹不碰
        IgnoreCollision(playerBullet, enemyBullet, true);
        // 收集品不碰弹射物
        IgnoreCollision(collectible, projectile, true);
        IgnoreCollision(collectible, playerBullet, true);
        IgnoreCollision(collectible, enemyBullet, true);
        // 谜题不碰敌人子弹
        IgnoreCollision(puzzle, enemyBullet, true);
        // Boss不碰敌人
        IgnoreCollision(boss, enemy, true);

        Debug.Log("[LayerSetup] Physics collision matrix configured");
    }

    private static void IgnoreCollision(int layer1, int layer2, bool ignore)
    {
        Physics2D.IgnoreLayerCollision(layer1, layer2, ignore);
    }
}
