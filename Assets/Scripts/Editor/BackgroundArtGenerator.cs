using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 背景美术生成 + 接入 - 生成天空/远山/近树三层视差图和云朵,
/// 挂到 Level_1_2 已有的 ParallaxLayer_0/1/2 上,并加会飘的云。
///
/// 用法: -executeMethod BackgroundArtGenerator.GenerateAndApply
/// </summary>
public static class BackgroundArtGenerator
{
    private const string ArtDir = "Assets/Resources/Art";
    private const string CloudPrefix = "BgCloud_";

    [MenuItem("DoubleForward/Generate Background Art", false, 53)]
    public static void GenerateAndApply()
    {
        if (!Directory.Exists(ArtDir)) Directory.CreateDirectory(ArtDir);

        // 每章一套背景。五章共用一张图的话,冰原/沙漠/深渊头顶都还是
        // 第一章那片阳光绿丘,章节主题在画面上完全立不住。
        for (int ch = 1; ch <= 5; ch++)
        {
            palette = PaletteFor(ch);
            Create($"BgSky_ch{ch}", 128, 96, 8, DrawSky);
            Create($"BgHills_ch{ch}", 192, 64, 8, DrawHills);
            Create($"BgTrees_ch{ch}", 192, 80, 8, DrawTrees);
        }
        Create("BgCloud", 64, 24, 12, DrawCloud);

        ApplyToAllLevels();
    }

    /// <summary>给所有章节关卡铺背景,不只是1-2</summary>
    private static void ApplyToAllLevels()
    {
        int done = 0;
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!System.IO.File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            ApplyToOpenScene(ChapterOf(entry.path));
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            done++;
        }
        Debug.Log($"[BgArt] background layers + drifting clouds applied to {done} level scenes");
    }

    // ==================== 每章调色板 ====================

    private struct Palette
    {
        public Color skyHorizon, skyZenith, glow;
        public Color ridgeFar, ridgeNear;
        public Color leaf, trunk;
    }

    private static Palette palette = PaletteFor(1);

    /// <summary>章节配色。名称与 LevelDataCatalog 一致: 光影遗迹/冰火熔炉/沙漠风暴/深渊暗流/天空之巅</summary>
    private static Palette PaletteFor(int chapter)
    {
        switch (chapter)
        {
            case 2:  // 冰火熔炉 - 冷白天光,冰脊,熔岩暖光
                return new Palette {
                    skyHorizon = new Color(0.88f, 0.93f, 0.98f), skyZenith = new Color(0.52f, 0.72f, 0.92f),
                    glow = new Color(1f, 0.62f, 0.32f),
                    ridgeFar = new Color(0.78f, 0.87f, 0.94f), ridgeNear = new Color(0.58f, 0.72f, 0.85f),
                    leaf = new Color(0.72f, 0.84f, 0.92f), trunk = new Color(0.55f, 0.64f, 0.74f) };
            case 3:  // 沙漠风暴 - 沙尘天光
                return new Palette {
                    skyHorizon = new Color(0.98f, 0.90f, 0.72f), skyZenith = new Color(0.86f, 0.68f, 0.42f),
                    glow = new Color(1f, 0.92f, 0.68f),
                    ridgeFar = new Color(0.90f, 0.78f, 0.58f), ridgeNear = new Color(0.76f, 0.60f, 0.40f),
                    leaf = new Color(0.70f, 0.62f, 0.40f), trunk = new Color(0.55f, 0.46f, 0.32f) };
            case 4:  // 深渊暗流 - 海底幽光
                return new Palette {
                    skyHorizon = new Color(0.16f, 0.34f, 0.46f), skyZenith = new Color(0.05f, 0.12f, 0.24f),
                    glow = new Color(0.40f, 0.90f, 0.85f),
                    ridgeFar = new Color(0.18f, 0.38f, 0.46f), ridgeNear = new Color(0.10f, 0.24f, 0.34f),
                    leaf = new Color(0.16f, 0.44f, 0.46f), trunk = new Color(0.12f, 0.28f, 0.32f) };
            case 5:  // 天空之巅 - 高空,近乎无云的清透
                return new Palette {
                    skyHorizon = new Color(1f, 0.97f, 0.90f), skyZenith = new Color(0.42f, 0.62f, 0.92f),
                    glow = new Color(1f, 0.95f, 0.80f),
                    ridgeFar = new Color(0.92f, 0.94f, 1f), ridgeNear = new Color(0.76f, 0.82f, 0.95f),
                    leaf = new Color(0.86f, 0.90f, 0.98f), trunk = new Color(0.66f, 0.72f, 0.84f) };
            default: // 光影遗迹 - 现有的日照绿丘
                return new Palette {
                    skyHorizon = new Color(0.78f, 0.90f, 0.99f), skyZenith = new Color(0.33f, 0.71f, 1.00f),
                    glow = new Color(1f, 0.90f, 0.62f),
                    ridgeFar = new Color(0.66f, 0.86f, 0.84f), ridgeNear = new Color(0.48f, 0.76f, 0.62f),
                    leaf = new Color(0.31f, 0.60f, 0.40f), trunk = new Color(0.36f, 0.46f, 0.36f) };
        }
    }

    private static int ChapterOf(string scenePath)
    {
        var m = System.Text.RegularExpressions.Regex.Match(scenePath, @"/Chapter(\d+)/");
        return m.Success ? int.Parse(m.Groups[1].Value) : 1;
    }

    // ==================== 绘制 ====================

    /// <summary>天空: 由深到浅的竖直渐变 + 顶部一轮柔光</summary>
    private static void DrawSky(Texture2D t)
    {
        int w = t.width, h = t.height;
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            var c = Color.Lerp(palette.skyHorizon, palette.skyZenith, v);
            for (int x = 0; x < w; x++) t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
        }
        // 柔光(远处的光源,呼应Lux的设定)
        float gx = w * 0.72f, gy = h * 0.70f, gr = h * 0.32f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Sqrt((x - gx) * (x - gx) + (y - gy) * (y - gy)) / gr;
                if (d >= 1f) continue;
                float a = Mathf.Pow(1f - d, 2.2f) * 0.55f;
                var cur = t.GetPixel(x, y);
                t.SetPixel(x, y, Color.Lerp(cur, palette.glow, a));
            }
        t.Apply();
    }

    /// <summary>远山: 两层重叠的起伏剪影,越远越淡</summary>
    private static void DrawHills(Texture2D t)
    {
        int w = t.width, h = t.height;
        Clear(t);
        // 这一层会被拉到170世界单位宽,起伏频率太低的话镜头里只看得到一段直线,
        // 远山就变成一条生硬的色带。频率要按拉伸后的宽度来定。
        DrawRidge(t, 0.62f, 0.10f, 5f, palette.ridgeFar);
        DrawRidge(t, 0.44f, 0.16f, 9f, palette.ridgeNear);
        t.Apply();
    }

    private static void DrawRidge(Texture2D t, float baseH, float amp, float freq, Color c)
    {
        int w = t.width, h = t.height;
        for (int x = 0; x < w; x++)
        {
            float u = (float)x / w;
            float top = (baseH + Mathf.Sin(u * Mathf.PI * 2f * freq) * amp
                               + Mathf.Sin(u * Mathf.PI * 2f * freq * 2.7f) * amp * 0.35f) * h;
            for (int y = 0; y <= Mathf.Min(h - 1, Mathf.RoundToInt(top)); y++)
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
        }
    }

    /// <summary>近景树林: 一排三角树剪影</summary>
    private static void DrawTrees(Texture2D t)
    {
        int w = t.width, h = t.height;
        Clear(t);
        var trunk = palette.trunk;
        var leaf = palette.leaf;
        var rng = new System.Random(77);

        const int treeCount = 44;      // 拉到170单位宽后,14棵树每棵会有12单位粗
        for (int i = 0; i < treeCount; i++)
        {
            int cx = 3 + i * (w - 6) / (treeCount - 1);
            int th = 28 + rng.Next(0, 26);          // 树高
            int halfW = 7 + rng.Next(0, 5);

            for (int y = 0; y < th; y++)            // 树冠(三角)
            {
                float f = 1f - (float)y / th;
                int hw = Mathf.RoundToInt(halfW * f);
                for (int x = cx - hw; x <= cx + hw; x++)
                {
                    if (x < 0 || x >= w) continue;
                    int yy = y + 6;
                    if (yy < h) t.SetPixel(x, yy, new Color(leaf.r, leaf.g, leaf.b, 1f));
                }
            }
            for (int y = 0; y < 8; y++)             // 树干
                for (int x = cx - 1; x <= cx + 1; x++)
                    if (x >= 0 && x < w) t.SetPixel(x, y, new Color(trunk.r, trunk.g, trunk.b, 1f));
        }
        t.Apply();
    }

    /// <summary>云: 几个叠在一起的柔和椭圆</summary>
    private static void DrawCloud(Texture2D t)
    {
        int w = t.width, h = t.height;
        Clear(t);
        var lumps = new[] { new Vector3(0.28f, 0.42f, 0.24f), new Vector3(0.50f, 0.58f, 0.32f),
                            new Vector3(0.72f, 0.44f, 0.22f) };
        foreach (var l in lumps)
        {
            float cx = l.x * w, cy = l.y * h, r = l.z * w;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / r, dy = (y - cy) / (r * 0.62f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d >= 1f) continue;
                    float a = Mathf.Pow(1f - d, 1.4f) * 0.8f;
                    var cur = t.GetPixel(x, y);
                    float na = Mathf.Max(cur.a, a);
                    t.SetPixel(x, y, new Color(0.86f, 0.88f, 0.96f, na));
                }
        }
        t.Apply();
    }

    private static void Clear(Texture2D t)
    {
        for (int y = 0; y < t.height; y++)
            for (int x = 0; x < t.width; x++)
                t.SetPixel(x, y, Color.clear);
    }

    // ==================== 接入场景 ====================

    private static void ApplyToOpenScene(int chapter)
    {
        // 按目标世界尺寸反算缩放。场景原有的 scale=(80,15) 会把 16x12 的天空图
        // 撑成 1280x180 单位,摄像机只看到中间极小一片渐变,结果就是一片纯色
        AssignLayer("ParallaxLayer_0", $"BgSky_ch{chapter}", -30, 170f, 34f, 2f);
        // 山脊只填贴图下部,所以整层要压到地平线附近,否则那片实色会把蓝天全糊掉
        AssignLayer("ParallaxLayer_1", $"BgHills_ch{chapter}", -20, 170f, 11f, 0.6f);
        AssignLayer("ParallaxLayer_2", $"BgTrees_ch{chapter}", -10, 170f, 7f, 0.2f);  // 压低,树干藏进地面

        // 重复运行先清掉上一次的云
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name.StartsWith(CloudPrefix)) Object.DestroyImmediate(go);

        var cloudSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/BgCloud.png");
        var cam = Camera.main;
        float baseY = cam != null ? cam.transform.position.y + 4f : 5f;
        for (int i = 0; i < 4; i++)
        {
            var cloud = new GameObject($"{CloudPrefix}{i}");
            cloud.transform.position = new Vector3(-6f + i * 9f, baseY + (i % 2) * 1.8f, 12f);
            var sr = cloud.AddComponent<SpriteRenderer>();
            sr.sprite = cloudSprite;
            sr.sortingOrder = -25;
            cloud.AddComponent<CloudDrift>().Configure(0.25f + i * 0.08f, 44f);
        }
    }

    private static void AssignLayer(string objectName, string spriteName, int order,
        float targetWidth, float targetHeight, float centerY)
    {
        var go = GameObject.Find(objectName);
        if (go == null) { Debug.LogWarning($"[BgArt] {objectName} not found"); return; }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{spriteName}.png");
        if (sprite == null) { Debug.LogWarning($"[BgArt] sprite {spriteName} missing"); return; }

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.white;
        sr.sortingOrder = order;

        // 精灵原生世界尺寸 = 像素 / PPU
        var native = sprite.bounds.size;
        if (native.x <= 0f || native.y <= 0f) return;
        go.transform.localScale = new Vector3(targetWidth / native.x, targetHeight / native.y, 1f);

        var p = go.transform.position;
        go.transform.position = new Vector3(p.x, centerY, p.z);
    }

    private static void Create(string name, int width, int height, int ppu, System.Action<Texture2D> draw)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        draw(tex);

        string path = $"{ArtDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = ppu;
            imp.filterMode = FilterMode.Point;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
    }
}
