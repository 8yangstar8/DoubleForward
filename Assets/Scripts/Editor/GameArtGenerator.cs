using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 游戏美术生成器 - 程序化生成带明暗/渐变/描边的精灵,替换早期的单色平涂占位图。
/// 输出到 Resources/Art 以便运行时兜底代码(光束/光桥/影区)也能 Resources.Load。
/// 用法: -executeMethod GameArtGenerator.GenerateAll
/// </summary>
public static class GameArtGenerator
{
    private const string ArtDir = "Assets/Resources/Art";

    [MenuItem("DoubleForward/Generate Game Art", false, 51)]
    public static void GenerateAll()
    {
        if (!Directory.Exists(ArtDir)) Directory.CreateDirectory(ArtDir);

        // ===== 能力特效(运行时按玩法参数缩放,做成1x1世界单位) =====
        Create("LightBeam", 64, 64, 64, SpriteAlignment.LeftCenter, DrawLightBeam);
        Create("LightBridge", 64, 64, 64, SpriteAlignment.Center, DrawLightBridge);
        Create("ShadowZone", 64, 64, 64, SpriteAlignment.Center, DrawShadowZone);

        // ===== 合作关卡机关(精灵自带尺寸,场景里不再拉伸) =====
        Create("ShadowWallTile", 24, 160, 40, SpriteAlignment.Center, DrawShadowWall);   // 0.6 x 4
        Create("PressurePlateArt", 64, 12, 40, SpriteAlignment.Center, DrawPressurePlate); // 1.6 x 0.3
        Create("LightSensorArt", 36, 36, 40, SpriteAlignment.Center, DrawLightSensor);   // 0.9 x 0.9
        Create("GateDoorArt", 32, 160, 40, SpriteAlignment.Center, DrawGateDoor);        // 0.8 x 4

        // ===== 地形 =====
        Create("GroundTile", 32, 32, 16, SpriteAlignment.Center, DrawGroundTile);        // 2 x 2

        AssetDatabase.Refresh();
        Debug.Log("[GameArt] Generated 8 shaded sprites into " + ArtDir);
    }

    // ==================== 绘制 ====================

    /// <summary>光束: 中心亮芯 + 上下衰减, 向尖端渐隐</summary>
    private static void DrawLightBeam(Texture2D t)
    {
        int w = t.width, h = t.height;
        for (int y = 0; y < h; y++)
        {
            float dy = Mathf.Abs(y - (h - 1) * 0.5f) / (h * 0.5f);   // 0中心 → 1边缘
            float profile = Mathf.Clamp01(1f - dy);
            float falloff = profile * profile * profile;              // 更陡的上下衰减
            bool core = dy < 0.10f;                                   // 中间的亮芯线
            for (int x = 0; x < w; x++)
            {
                float tip = 1f - (float)x / w * 0.5f;                 // 越远越淡
                float a = (core ? 1f : falloff) * tip;
                if (a < 0.02f) { t.SetPixel(x, y, Color.clear); continue; }
                // 外缘深琥珀 → 内芯近白, 拉开明度差
                var c = core
                    ? new Color(1f, 0.99f, 0.90f)
                    : Color.Lerp(new Color(0.95f, 0.45f, 0.05f), new Color(1f, 0.88f, 0.45f), falloff);
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
            }
        }
        t.Apply();
    }

    /// <summary>光桥: 可站立的发光板, 顶面最亮, 底面渐隐</summary>
    private static void DrawLightBridge(Texture2D t)
    {
        int w = t.width, h = t.height;
        // 玩家要站在上面,做成实心板:上表面亮、板身琥珀、底面暗,而不是一团半透明雾
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);                 // 0底 → 1顶
            Color c;
            if (v > 0.82f) c = new Color(1f, 0.97f, 0.80f);                       // 受光顶面
            else if (v > 0.28f) c = Color.Lerp(new Color(0.86f, 0.55f, 0.12f),
                                               new Color(1f, 0.82f, 0.35f), (v - 0.28f) / 0.54f);
            else c = Color.Lerp(new Color(0.30f, 0.16f, 0.04f),
                                new Color(0.62f, 0.36f, 0.08f), v / 0.28f);       // 背光底面
            float a = v < 0.12f ? Mathf.InverseLerp(0f, 0.12f, v) * 0.9f : 0.92f;
            for (int x = 0; x < w; x++)
            {
                float edge = Mathf.Clamp01(Mathf.Min(x, w - 1 - x) / (w * 0.08f)); // 两端收窄
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, a * edge));
            }
        }
        t.Apply();
    }

    /// <summary>阴影区: 径向暗雾, 边缘柔和</summary>
    private static void DrawShadowZone(Texture2D t)
    {
        int w = t.width, h = t.height;
        float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f, r = w * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                if (d > 1f) { t.SetPixel(x, y, Color.clear); continue; }
                float a = Mathf.Pow(1f - d, 1.8f) * 0.75f;
                var c = Color.Lerp(new Color(0.30f, 0.12f, 0.45f), new Color(0.06f, 0.02f, 0.12f), d);
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
            }
        t.Apply();
    }

    /// <summary>影墙: 深紫砖块 + 缝隙, 带描边</summary>
    private static void DrawShadowWall(Texture2D t)
    {
        int w = t.width, h = t.height;
        var brick = new Color(0.20f, 0.09f, 0.30f);
        var brickAlt = new Color(0.26f, 0.12f, 0.38f);
        var mortar = new Color(0.10f, 0.04f, 0.16f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int row = y / 12;
                int offset = (row % 2 == 0) ? 0 : 6;
                bool isMortar = (y % 12 == 0) || ((x + offset) % 12 == 0);
                var c = isMortar ? mortar : ((row % 2 == 0) ? brick : brickAlt);
                // 左侧受光
                c = Color.Lerp(c * 1.35f, c * 0.75f, (float)x / (w - 1));
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
            }
        Outline(t, new Color(0.55f, 0.30f, 0.80f, 1f));
        t.Apply();
    }

    /// <summary>压力板: 金属板, 上缘高光下缘阴影</summary>
    private static void DrawPressurePlate(Texture2D t)
    {
        int w = t.width, h = t.height;
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            var c = Color.Lerp(new Color(0.30f, 0.30f, 0.36f), new Color(0.72f, 0.74f, 0.82f), v);
            for (int x = 0; x < w; x++)
            {
                // 两端稍微收进去,像嵌在地面里
                bool cap = x < 2 || x >= w - 2;
                t.SetPixel(x, y, cap ? c * 0.6f : c);
            }
        }
        for (int x = 0; x < w; x++) t.SetPixel(x, h - 1, new Color(0.95f, 0.96f, 1f));
        t.Apply();
    }

    /// <summary>光敏机关: 菱形水晶, 有切面高光</summary>
    private static void DrawLightSensor(Texture2D t)
    {
        int w = t.width, h = t.height;
        float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Abs(x - cx) / cx + Mathf.Abs(y - cy) / cy; // 菱形
                if (d > 1f) { t.SetPixel(x, y, Color.clear); continue; }
                bool leftFacet = (x - cx) < 0;
                var baseCol = leftFacet ? new Color(0.85f, 0.82f, 0.45f) : new Color(0.55f, 0.52f, 0.28f);
                var c = Color.Lerp(new Color(1f, 0.98f, 0.80f), baseCol, d);
                if (d > 0.88f) c *= 0.5f; // 描边
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
            }
        t.Apply();
    }

    /// <summary>大门: 石门, 竖向分格 + 铆钉</summary>
    private static void DrawGateDoor(Texture2D t)
    {
        int w = t.width, h = t.height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = new Color(0.42f, 0.35f, 0.22f);
                if (y % 24 < 2) c *= 0.55f;                            // 横向分格
                c = Color.Lerp(c * 1.3f, c * 0.7f, (float)x / (w - 1)); // 左侧受光
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
            }
        // 铆钉
        for (int y = 12; y < h; y += 24)
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    t.SetPixel(w / 2 + dx, y + dy, new Color(0.80f, 0.72f, 0.45f));
        Outline(t, new Color(0.20f, 0.15f, 0.08f, 1f));
        t.Apply();
    }

    /// <summary>地面砖: 上层草皮 + 下层泥土颗粒</summary>
    private static void DrawGroundTile(Texture2D t)
    {
        int w = t.width, h = t.height;
        var rng = new System.Random(1234);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c;
                if (y >= h - 6)
                    c = Color.Lerp(new Color(0.22f, 0.48f, 0.22f), new Color(0.34f, 0.66f, 0.30f),
                        (float)(y - (h - 6)) / 6f);
                else
                {
                    float v = (float)y / (h - 6);
                    c = Color.Lerp(new Color(0.24f, 0.16f, 0.11f), new Color(0.40f, 0.28f, 0.18f), v);
                    if (rng.NextDouble() < 0.06) c *= 0.82f; // 颗粒感
                }
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
            }
        t.Apply();
    }

    // ==================== 工具 ====================

    /// <summary>给不透明区域描一圈边</summary>
    private static void Outline(Texture2D t, Color color)
    {
        int w = t.width, h = t.height;
        for (int x = 0; x < w; x++) { t.SetPixel(x, 0, color); t.SetPixel(x, h - 1, color); }
        for (int y = 0; y < h; y++) { t.SetPixel(0, y, color); t.SetPixel(w - 1, y, color); }
    }

    private static void Create(string name, int width, int height, int pixelsPerUnit,
        SpriteAlignment pivot, System.Action<Texture2D> draw)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        draw(tex);

        string path = $"{ArtDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            // spriteAlignment 只在 TextureImporterSettings 上
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)pivot;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
