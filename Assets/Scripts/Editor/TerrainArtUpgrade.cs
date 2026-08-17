using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 地形美术升级 - 换成成套的地形拼接块,并在地面上种植被。
///
/// 之前 TerrainRenderUpgrade 已经把地面/平台改成平铺渲染,但用的是单块纯填充图,
/// 再在顶上盖一条草皮色带 —— 边缘是硬切的直角,没有崖壁、没有圆角收边,
/// 远看就是一根色条。
///
/// 这里换成 Generic Platformer Tiles (CC0) 的成套地形块,并利用 Unity 九宫格:
/// 一张 96x64 的图 + spriteBorder(32,0,32,32),配 SpriteDrawMode.Tiled,
/// 引擎就会自己做到"四角不拉伸、上边横向平铺、中心双向平铺"。
/// 也就是说不需要 Tilemap、不需要每格一个物件,单个 SpriteRenderer 就够。
///
/// 底边故意不留边框(bottom border = 0),这样侧壁会一直向下平铺,
/// 地面下方接 GroundFill 时看不出接缝。
///
/// 几何完全不变: 只改 sprite / border,不碰 sr.size 和碰撞体。
///
/// 用法: -executeMethod TerrainArtUpgrade.UpgradeAll
/// </summary>
public static class TerrainArtUpgrade
{
    private const string ArtDir = "Assets/Art/External/GenericPlatformer/";
    private const string SceneryRoot = "Scenery";
    private const int PPU = 32;                 // 32像素图块 = 1世界单位,与既有 16@16 同尺度

    // 排序层参照: -30天空 -25云 -20远山 -10远景树 | -5植被 -2小装饰 -1地下填充 0地面
    private const int OrderTree = -5;
    private const int OrderProp = -2;

    [MenuItem("DoubleForward/Upgrade Terrain Art", false, 20)]
    public static void UpgradeAll()
    {
        ImportTerrain("terrain_ground", new Vector4(32, 0, 32, 32));
        ImportTerrain("terrain_ledge", new Vector4(32, 0, 32, 0));
        ImportTerrain("terrain_wall", new Vector4(32, 0, 32, 0));
        foreach (var n in PropNames) ImportProp(n);
        ImportProp("cloud");

        int scenes = 0, props = 0;
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            props += UpgradeOpenScene(Path.GetFileNameWithoutExtension(entry.path));
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            scenes++;
        }
        Debug.Log($"[TerrainArt] retiled terrain and planted {props} props across {scenes} scenes");
    }

    private static readonly string[] PropNames =
        { "tree_big", "tree_small", "bush", "grass_tuft", "flower", "mushroom_small", "mushroom_big" };

    private static int UpgradeOpenScene(string sceneName)
    {
        var ground9 = Load("terrain_ground");
        var ledge = Load("terrain_ledge");
        var wall = Load("terrain_wall");
        if (ground9 == null || ledge == null || wall == null)
        {
            Debug.LogError("[TerrainArt] terrain sprites missing");
            return 0;
        }

        var ground = GameObject.Find("Ground");
        var groundCol = ground != null ? ground.GetComponent<Collider2D>() : null;
        if (groundCol == null) return 0;

        Retile(ground, ground9);

        // 草皮现在画在地形图块里了,旧的独立草皮色带会盖成两层
        var oldStrip = GameObject.Find("GroundGrassTop");
        if (oldStrip != null) Object.DestroyImmediate(oldStrip);

        // 地面下方的填充改用侧壁块,左右两边的崖壁就能和地面接上
        var fill = GameObject.Find("GroundFill");
        if (fill != null)
        {
            var fsr = fill.GetComponent<SpriteRenderer>();
            // 地下压暗: 这一层在镜头拉远时能占到近半个屏幕,和地面同亮度会糊成一片
            if (fsr != null) { fsr.sprite = wall; fsr.color = new Color(0.60f, 0.58f, 0.64f); }
        }

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name.StartsWith("Platform_")) Retile(go, ledge);

        ReskinClouds();
        return PlantScenery(sceneName, groundCol.bounds);
    }

    /// <summary>只换图和边框,尺寸/碰撞体一律不动。</summary>
    private static void Retile(GameObject go, Sprite sprite)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.color = Color.white;
    }

    private static void ReskinClouds()
    {
        var cloud = Load("cloud");
        if (cloud == null) return;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == null || !go.name.StartsWith("BgCloud_")) continue;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            sr.sprite = cloud;
            sr.color = Color.white;
            go.transform.localScale = Vector3.one * 1.6f;
        }
    }

    // ==================== 植被 ====================

    /// <summary>
    /// 沿地面顶面撒植被。位置由场景名做种子,同一场景每次跑结果一致,
    /// 免得每次重跑都产生一份不同的场景 diff。
    /// 植被没有碰撞体,排序层在玩法物件之后,所以只需要给"必须一眼看清"的
    /// 目标点(终点/门)让位,不用躲开沿路的宝石和敌人 —— 一开始按所有碰撞体
    /// 让位,结果 Level_1_1 满地的宝石触发器把整条地面全占了,一株都没种上。
    /// </summary>
    private static int PlantScenery(string sceneName, Bounds ground)
    {
        var old = GameObject.Find(SceneryRoot);
        if (old != null) Object.DestroyImmediate(old);

        var occupied = new List<Vector2>();   // 需要让位的 x 区间 (min,max)
        foreach (var col in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (col == null) continue;
            var n = col.gameObject.name;
            if (!n.StartsWith("Goal") && !n.StartsWith("Door") && !n.StartsWith("Exit")) continue;
            var b = col.bounds;
            occupied.Add(new Vector2(b.min.x - 1.5f, b.max.x + 1.5f));
        }

        var root = new GameObject(SceneryRoot);
        var rng = new System.Random(sceneName.GetHashCode());
        float top = ground.max.y;
        int planted = 0;

        for (float x = ground.min.x + 1.5f; x < ground.max.x - 1.5f; x += 1.2f + (float)rng.NextDouble() * 1.8f)
        {
            string name = PickProp(rng);
            var sprite = Load(name);
            if (sprite == null) continue;

            float halfW = sprite.bounds.extents.x;
            bool blocked = false;
            foreach (var o in occupied)
                if (x + halfW > o.x && x - halfW < o.y) { blocked = true; break; }
            if (blocked) continue;

            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            // 底边贴地。树的下部会被地面挡住一点,看起来是长在土里的
            go.transform.position = new Vector3(x, top + sprite.bounds.extents.y - 0.1f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = name.StartsWith("tree") ? OrderTree : OrderProp;
            if (rng.Next(2) == 0) sr.flipX = true;
            planted++;
        }
        return planted;
    }

    private static string PickProp(System.Random rng)
    {
        int r = rng.Next(100);
        if (r < 12) return "tree_big";
        if (r < 30) return "tree_small";
        if (r < 50) return "bush";
        if (r < 72) return "grass_tuft";
        if (r < 84) return "flower";
        if (r < 93) return "mushroom_small";
        return "mushroom_big";
    }

    // ==================== 导入设置 ====================

    private static Sprite Load(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + name + ".png");

    private static void ImportTerrain(string name, Vector4 border)
    {
        var imp = Configure(name);
        if (imp == null) return;
        imp.spriteBorder = border;       // 九宫格: 角块不拉伸,边和中心平铺
        imp.SaveAndReimport();
    }

    private static void ImportProp(string name)
    {
        var imp = Configure(name);
        if (imp != null) imp.SaveAndReimport();
    }

    private static TextureImporter Configure(string name)
    {
        string path = ArtDir + name + ".png";
        AssetDatabase.ImportAsset(path);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Debug.LogWarning($"[TerrainArt] missing {path}"); return null; }

        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = PPU;
        imp.filterMode = FilterMode.Point;
        imp.alphaIsTransparency = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;

        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;   // 平铺/九宫格必须
        imp.SetTextureSettings(settings);
        return imp;
    }
}
