using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 地形渲染升级 - 把地面和平台从"拉伸一张图"改成"按原尺寸平铺"。
///
/// 之前的做法是给物体一张 1x1 世界单位的贴图,再用 transform.localScale 拉到
/// 60 单位宽 —— 一个像素被拉成两米,画面上就是一条扁平色带,这是当前观感最差
/// 的地方(地面占的屏幕面积最大)。
///
/// 改法: SpriteRenderer.drawMode = Tiled,尺寸记在 sr.size 上,localScale 恒为1,
/// 纹理按16像素的原尺寸重复。碰撞体尺寸同步显式写死,几何完全不变。
///
/// 注意: LevelBootstrap.SetupLevelBoundaries 原本按 localScale 算地面宽度,
/// 已改为读碰撞体范围,否则边界墙会立到地面中间。
///
/// 用法: -executeMethod TerrainRenderUpgrade.UpgradeAll
/// </summary>
public static class TerrainRenderUpgrade
{
    private const string ArtDir = "Assets/Resources/Art/";
    private const string GrassStripName = "GroundGrassTop";

    [MenuItem("DoubleForward/Upgrade Terrain Rendering", false, 18)]
    public static void UpgradeAll()
    {
        // 平铺渲染要求贴图是 FullRect 网格
        foreach (var n in new[] { "TileDirt", "TileGrass", "TileStone" })
            ImportTile(ArtDir + n + ".png");

        int scenes = 0, objects = 0;
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            int n = UpgradeOpenScene();
            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                scenes++; objects += n;
            }
        }
        Debug.Log($"[Terrain] {objects} objects switched to tiled rendering across {scenes} scenes");
    }

    private static int UpgradeOpenScene()
    {
        int count = 0;
        var dirt = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "TileDirt.png");
        var grass = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "TileGrass.png");
        var stone = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "TileStone.png");
        if (dirt == null || grass == null || stone == null)
        {
            Debug.LogError("[Terrain] tile sprites missing");
            return 0;
        }

        var ground = GameObject.Find("Ground");
        if (ground != null)
        {
            var b = MakeTiled(ground, dirt, 0);
            count++;

            // 地面顶部铺一条草皮(纯渲染,不加碰撞体)
            var strip = GameObject.Find(GrassStripName);
            if (strip == null)
            {
                strip = new GameObject(GrassStripName);
                strip.AddComponent<SpriteRenderer>();
            }
            const float grassHeight = 0.6f;
            var ssr = strip.GetComponent<SpriteRenderer>();
            ssr.sprite = grass;
            ssr.drawMode = SpriteDrawMode.Tiled;
            ssr.size = new Vector2(b.size.x, grassHeight);
            ssr.color = Color.white;
            ssr.sortingOrder = 1;                       // 压在泥土之上
            strip.transform.localScale = Vector3.one;
            strip.transform.position = new Vector3(b.center.x, b.max.y - grassHeight * 0.5f, 0f);
            count++;
        }

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == null || !go.name.StartsWith("Platform_")) continue;
            MakeTiled(go, stone, 1);
            count++;
        }

        // 地面本身只有2单位厚,下方会露出天空。补一层平铺的泥土填充(纯渲染,无碰撞)
        if (ground != null)
        {
            var gcol = ground.GetComponent<Collider2D>();
            if (gcol != null)
            {
                var gb = gcol.bounds;
                const float fillDepth = 24f;
                var fill = GameObject.Find("GroundFill");
                if (fill == null)
                {
                    fill = new GameObject("GroundFill");
                    fill.AddComponent<SpriteRenderer>();
                }
                var fsr = fill.GetComponent<SpriteRenderer>();
                fsr.sprite = dirt;
                fsr.drawMode = SpriteDrawMode.Tiled;
                fsr.tileMode = SpriteTileMode.Continuous;
                fsr.size = new Vector2(gb.size.x, fillDepth);
                fsr.color = new Color(0.72f, 0.72f, 0.72f);   // 略暗,做出深度
                fsr.sortingOrder = -1;
                fill.transform.localScale = Vector3.one;
                fill.transform.position = new Vector3(gb.center.x, gb.min.y - fillDepth * 0.5f, 0f);
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 改为平铺渲染,几何保持不变: 先记下原有世界尺寸,再把尺寸从 localScale
    /// 转移到 sr.size 和碰撞体上。
    /// </summary>
    private static Bounds MakeTiled(GameObject go, Sprite tile, int sortingOrder)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        var col = go.GetComponent<BoxCollider2D>();

        // 原世界尺寸优先按碰撞体算,没有碰撞体就按缩放算
        Vector2 worldSize = col != null
            ? new Vector2(col.size.x * go.transform.localScale.x, col.size.y * go.transform.localScale.y)
            : new Vector2(go.transform.localScale.x, go.transform.localScale.y);

        go.transform.localScale = Vector3.one;

        sr.sprite = tile;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = worldSize;
        sr.color = Color.white;
        sr.sortingOrder = sortingOrder;

        if (col != null)
        {
            col.size = worldSize;      // 缩放已归1,碰撞体尺寸要显式写成世界尺寸
            col.offset = Vector2.zero;
        }

        return new Bounds(go.transform.position, new Vector3(worldSize.x, worldSize.y, 0f));
    }

    private static void ImportTile(string path)
    {
        AssetDatabase.ImportAsset(path);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;

        imp.textureType = TextureImporterType.Sprite;
        imp.spritePixelsPerUnit = 16;      // 16x16 图块 = 1 世界单位
        imp.filterMode = FilterMode.Point;
        imp.alphaIsTransparency = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.wrapMode = TextureWrapMode.Repeat;

        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;   // 平铺渲染必须
        imp.SetTextureSettings(settings);

        imp.SaveAndReimport();
    }
}
