using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 场景贴图 - 给 SceneFactory 生成但从没分配过精灵的物体补上贴图。
///
/// 背景: 实机截图发现地面/平台/敌人/终点/检查点/收集品的 SpriteRenderer 全是
/// m_Sprite: 0(空)。它们物理上存在(玩家站得住、测试全过),但一个都画不出来 ——
/// 玩家看到的是两个人悬在纯色背景里。这是"逻辑对但看不见"的典型。
///
/// 尺寸约定: 这些物体用 transform.localScale 当世界尺寸(如平台 scale 4x0.5),
/// 所以只能用 1x1 世界单位的 Unit* 贴图,乘上缩放后才正好盖住碰撞体。
///
/// 用法: -executeMethod SceneVisualDresser.DressAllLevels
/// </summary>
public static class SceneVisualDresser
{
    private const string ArtDir = "Assets/Resources/Art/";

    // 名字前缀 → 贴图。顺序有意义,先匹配到的先用
    private static readonly (string prefix, string sprite, int order)[] Mapping =
    {
        ("Ground",        "UnitGround",      0),
        ("Platform_",     "UnitPlatform",    1),
        ("Enemy_",        "UnitEnemy",       8),
        ("LevelGoal",     "UnitGoal",        5),
        ("Checkpoint_",   "UnitCheckpoint",  3),
        ("Collectible_",  "UnitCollectible", 4),
        ("PressurePlate", "UnitPlate",       2),
        ("PuzzleDoor",    "UnitDoor",        2),
        ("LightSensor",   "UnitSensor",      2),
    };

    [MenuItem("DoubleForward/Dress Scene Visuals", false, 15)]
    public static void DressAllLevels()
    {
        int scenes = 0, dressed = 0;

        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            int n = DressOpenScene(out bool geometryChanged);
            if (n > 0 || geometryChanged)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                dressed += n;
                scenes++;
            }
        }

        Debug.Log($"[Dresser] assigned {dressed} sprites across {scenes} scenes");
    }

    private static int DressOpenScene(out bool geometryChanged)
    {
        int count = 0;

        // 地面加厚要在贴图判断之外做,否则第二次运行时会因为"已有贴图"被整个跳过
        var ground = GameObject.Find("Ground");
        geometryChanged = ground != null && FixGroundVisual(ground.transform);

        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (sr == null) continue;

            // 这三类之前误用了"自带世界尺寸"的贴图,又被 transform scale 二次放大
            // (门被撑成 0.64x12 单位,一根柱子横贯全屏),必须强制重贴
            bool forceRedress = sr.gameObject.name.StartsWith("PuzzleDoor")
                || sr.gameObject.name.StartsWith("PressurePlate")
                || sr.gameObject.name.StartsWith("LightSensor");
            if (sr.sprite != null && !forceRedress) continue;   // 其余已有贴图的不动(含Coop_系列)

            string name = sr.gameObject.name;
            foreach (var (prefix, spriteName, order) in Mapping)
            {
                if (!name.StartsWith(prefix)) continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + spriteName + ".png");
                if (sprite == null)
                {
                    Debug.LogWarning($"[Dresser] sprite not found: {spriteName}");
                    break;
                }

                sr.sprite = sprite;
                sr.color = Color.white;      // 之前可能被染过色
                sr.sortingOrder = order;

                count++;
                break;
            }
        }
        return count;
    }

    /// <summary>
    /// 地面只有2单位厚,下方是一片空洞(能看到天空)。
    ///
    /// 不能直接把地面拉厚: 贴图会跟着纵向拉伸,草皮变成一条两米厚的绿带。
    /// 改为地面保持原厚度(草皮比例正确),下方另放一块纯泥土填充,只有渲染没有碰撞。
    /// </summary>
    private static bool FixGroundVisual(Transform tf)
    {
        const float groundThickness = 2f;
        const float fillDepth = 24f;

        bool changed = false;
        float top = tf.position.y + tf.localScale.y * 0.5f;

        // 之前误把地面拉厚过,恢复回来
        if (!Mathf.Approximately(tf.localScale.y, groundThickness))
        {
            tf.localScale = new Vector3(tf.localScale.x, groundThickness, tf.localScale.z);
            tf.position = new Vector3(tf.position.x, top - groundThickness * 0.5f, tf.position.z);
            changed = true;
        }

        float groundBottom = top - groundThickness;
        var fill = GameObject.Find("GroundFill");
        if (fill == null)
        {
            fill = new GameObject("GroundFill");
            fill.AddComponent<SpriteRenderer>();
            changed = true;
        }

        var sr = fill.GetComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "UnitDirt.png");
        sr.color = Color.white;
        sr.sortingOrder = -1;                       // 在地面之后、背景之前
        fill.transform.localScale = new Vector3(tf.localScale.x, fillDepth, 1f);
        fill.transform.position = new Vector3(tf.position.x, groundBottom - fillDepth * 0.5f, 0f);
        return changed;
    }
}
