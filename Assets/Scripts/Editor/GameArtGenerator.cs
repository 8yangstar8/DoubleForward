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

        Create("CrateArt", 40, 40, 40, SpriteAlignment.Center, DrawCrate);        // 1 x 1

        // ===== 单位贴图(正好1x1世界单位) =====
        // 场景里这些物体用 transform.localScale 当世界尺寸(如平台 scale 4x0.5),
        // 贴图必须是 1x1 世界单位,乘上缩放后才正好盖住碰撞体。
        // 会被横向拉伸的(地面/平台)一律做成横向均匀的图案,拉开也不难看。
        Create("UnitGround", 32, 32, 32, SpriteAlignment.Center, DrawUnitGround);
        Create("UnitDirt", 32, 32, 32, SpriteAlignment.Center, DrawUnitDirt);
        Create("UnitPlatform", 32, 32, 32, SpriteAlignment.Center, DrawUnitPlatform);
        Create("UnitEnemy", 32, 32, 32, SpriteAlignment.Center, DrawUnitEnemy);
        Create("UnitGoal", 32, 32, 32, SpriteAlignment.Center, DrawUnitGoal);
        Create("UnitCheckpoint", 32, 32, 32, SpriteAlignment.Center, DrawUnitCheckpoint);
        Create("UnitCollectible", 32, 32, 32, SpriteAlignment.Center, DrawUnitCollectible);
        Create("UnitDoor", 32, 32, 32, SpriteAlignment.Center, DrawUnitDoor);
        Create("UnitPlate", 32, 32, 32, SpriteAlignment.Center, DrawUnitPlate);
        Create("UnitSensor", 32, 32, 32, SpriteAlignment.Center, DrawUnitSensor);

        // ===== 地形 =====
        Create("GroundTile", 32, 32, 16, SpriteAlignment.Center, DrawGroundTile);        // 2 x 2

        AssetDatabase.Refresh();
        Debug.Log("[GameArt] Generated shaded sprites into " + ArtDir);
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

    /// <summary>地面(1x1): 顶部草皮 + 下方泥土, 横向完全均匀,拉多长都不会花</summary>
    private static void DrawUnitGround(Texture2D t)
    {
        int w = t.width, h = t.height;
        for (int y = 0; y < h; y++)
        {
            Color c;
            if (y >= h - 5)                       // 草皮
                c = Color.Lerp(new Color(0.20f, 0.44f, 0.21f), new Color(0.33f, 0.63f, 0.29f),
                    (float)(y - (h - 5)) / 5f);
            else if (y >= h - 7)                  // 草土交界的暗线
                c = new Color(0.17f, 0.28f, 0.15f);
            else
                c = Color.Lerp(new Color(0.20f, 0.14f, 0.10f), new Color(0.37f, 0.26f, 0.17f),
                    (float)y / (h - 7));
            for (int x = 0; x < w; x++) t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
        }
        t.Apply();
    }

    /// <summary>纯泥土(1x1): 给地面下方填充用,没有草皮,纵向拉伸也不会出现第二条草线</summary>
    private static void DrawUnitDirt(Texture2D t)
    {
        int w = t.width, h = t.height;
        for (int y = 0; y < h; y++)
        {
            var c = Color.Lerp(new Color(0.13f, 0.09f, 0.06f), new Color(0.30f, 0.21f, 0.14f),
                (float)y / (h - 1));
            for (int x = 0; x < w; x++) t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
        }
        t.Apply();
    }

    /// <summary>平台(1x1): 石板, 顶面高光 + 底面阴影, 横向均匀</summary>
    private static void DrawUnitPlatform(Texture2D t)
    {
        int w = t.width, h = t.height;
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            Color c;
            if (v > 0.86f) c = new Color(0.62f, 0.60f, 0.55f);          // 受光顶面
            else if (v < 0.16f) c = new Color(0.20f, 0.19f, 0.20f);      // 底面阴影
            else c = Color.Lerp(new Color(0.33f, 0.31f, 0.31f), new Color(0.48f, 0.46f, 0.43f), v);
            for (int x = 0; x < w; x++) t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
        }
        t.Apply();
    }

    /// <summary>敌人(1x1): 深紫史莱姆, 两只发光的眼睛</summary>
    private static void DrawUnitEnemy(Texture2D t)
    {
        int w = t.width, h = t.height;
        Clear(t);
        float cx = (w - 1) * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // 下宽上圆的水滴形
                float ny = (float)y / (h - 1);
                float halfW = Mathf.Lerp(w * 0.46f, w * 0.20f, Mathf.Pow(ny, 1.6f));
                if (Mathf.Abs(x - cx) > halfW || ny > 0.86f) continue;
                var c = Color.Lerp(new Color(0.34f, 0.12f, 0.42f), new Color(0.55f, 0.24f, 0.62f), ny);
                if (x - cx < -halfW * 0.55f) c *= 1.25f;   // 左侧受光
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
            }
        // 眼睛
        foreach (int ex in new[] { (int)cx - 5, (int)cx + 4 })
            for (int dy = 0; dy < 3; dy++)
                for (int dx = 0; dx < 3; dx++)
                    t.SetPixel(ex + dx, (int)(h * 0.55f) + dy, new Color(1f, 0.85f, 0.4f));
        t.Apply();
    }

    /// <summary>终点(1x1): 旗杆 + 金色旗面</summary>
    private static void DrawUnitGoal(Texture2D t)
    {
        int w = t.width, h = t.height;
        Clear(t);
        for (int y = 2; y < h - 2; y++)                       // 旗杆
            for (int x = 6; x < 9; x++)
                t.SetPixel(x, y, new Color(0.62f, 0.60f, 0.56f));
        for (int y = h - 20; y < h - 4; y++)                  // 旗面
        {
            int len = 16 - Mathf.Abs((y - (h - 12)) / 2);
            for (int x = 9; x < 9 + len && x < w; x++)
            {
                float f = (float)(x - 9) / Mathf.Max(1, len);
                var c = Color.Lerp(new Color(1f, 0.82f, 0.25f), new Color(0.95f, 0.55f, 0.10f), f);
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
            }
        }
        t.Apply();
    }

    /// <summary>检查点(1x1): 石座 + 绿色光柱</summary>
    private static void DrawUnitCheckpoint(Texture2D t)
    {
        int w = t.width, h = t.height;
        Clear(t);
        for (int y = 0; y < 6; y++)                            // 底座
            for (int x = 8; x < w - 8; x++)
                t.SetPixel(x, y, new Color(0.35f, 0.34f, 0.32f));
        for (int y = 6; y < h - 4; y++)                        // 光柱
            for (int x = 13; x < 19; x++)
            {
                float a = 1f - (float)(y - 6) / (h - 10) * 0.6f;
                t.SetPixel(x, y, new Color(0.30f, 0.85f, 0.45f, a));
            }
        t.Apply();
    }

    /// <summary>收集品(1x1): 菱形宝石</summary>
    private static void DrawUnitCollectible(Texture2D t)
    {
        int w = t.width, h = t.height;
        Clear(t);
        float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Abs(x - cx) / (w * 0.32f) + Mathf.Abs(y - cy) / (h * 0.42f);
                if (d > 1f) continue;
                var c = Color.Lerp(new Color(1f, 0.98f, 0.75f), new Color(0.95f, 0.72f, 0.15f), d);
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
            }
        t.Apply();
    }

    /// <summary>门(1x1): 木板 + 铆钉, 纵向拉伸也不难看</summary>
    private static void DrawUnitDoor(Texture2D t)
    {
        int w = t.width, h = t.height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = new Color(0.44f, 0.34f, 0.20f);
                if (x % 10 < 1) c *= 0.6f;                              // 竖向板缝
                c = Color.Lerp(c * 1.28f, c * 0.72f, (float)x / (w - 1));
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
            }
        Outline(t, new Color(0.20f, 0.14f, 0.07f, 1f));
        t.Apply();
    }

    /// <summary>压板(1x1): 金属板,上缘高光</summary>
    private static void DrawUnitPlate(Texture2D t)
    {
        int w = t.width, h = t.height;
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            var c = Color.Lerp(new Color(0.28f, 0.28f, 0.34f), new Color(0.74f, 0.76f, 0.84f), v);
            for (int x = 0; x < w; x++) t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
        }
        t.Apply();
    }

    /// <summary>光敏机关(1x1): 菱形水晶</summary>
    private static void DrawUnitSensor(Texture2D t)
    {
        int w = t.width, h = t.height;
        Clear(t);
        float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Abs(x - cx) / cx + Mathf.Abs(y - cy) / cy;
                if (d > 1f) continue;
                var c = Color.Lerp(new Color(1f, 0.98f, 0.80f), new Color(0.72f, 0.68f, 0.35f), d);
                if (d > 0.86f) c *= 0.55f;
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
            }
        t.Apply();
    }

    /// <summary>木箱: 板材纹理 + 对角加固条 + 描边</summary>
    private static void DrawCrate(Texture2D t)
    {
        int w = t.width, h = t.height;
        var wood = new Color(0.52f, 0.36f, 0.19f);
        var plank = new Color(0.40f, 0.27f, 0.14f);
        var brace = new Color(0.63f, 0.45f, 0.24f);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = (y % 10 < 1) ? plank : wood;              // 板缝
                c = Color.Lerp(c * 1.25f, c * 0.72f, (float)x / (w - 1)); // 左侧受光
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
            }
        // 对角加固条
        for (int i = 0; i < w; i++)
        {
            for (int d = -1; d <= 1; d++)
            {
                int y1 = Mathf.Clamp(i + d, 0, h - 1);
                int y2 = Mathf.Clamp(h - 1 - i + d, 0, h - 1);
                t.SetPixel(i, y1, brace);
                t.SetPixel(i, y2, brace);
            }
        }
        Outline(t, new Color(0.20f, 0.13f, 0.06f, 1f));
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

    private static void Clear(Texture2D t)
    {
        for (int y = 0; y < t.height; y++)
            for (int x = 0; x < t.width; x++)
                t.SetPixel(x, y, Color.clear);
    }

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
