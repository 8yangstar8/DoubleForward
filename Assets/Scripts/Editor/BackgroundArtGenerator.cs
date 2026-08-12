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

        Create("BgSky", 128, 96, 8, DrawSky);
        Create("BgHills", 192, 64, 8, DrawHills);
        Create("BgTrees", 192, 80, 8, DrawTrees);
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
            ApplyToOpenScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            done++;
        }
        Debug.Log($"[BgArt] background layers + drifting clouds applied to {done} level scenes");
    }

    // ==================== 绘制 ====================

    /// <summary>天空: 由深到浅的竖直渐变 + 顶部一轮柔光</summary>
    private static void DrawSky(Texture2D t)
    {
        int w = t.width, h = t.height;
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            var c = Color.Lerp(new Color(0.30f, 0.34f, 0.52f),   // 地平线附近
                               new Color(0.08f, 0.10f, 0.22f), v); // 天顶
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
                t.SetPixel(x, y, Color.Lerp(cur, new Color(1f, 0.90f, 0.62f), a));
            }
        t.Apply();
    }

    /// <summary>远山: 两层重叠的起伏剪影,越远越淡</summary>
    private static void DrawHills(Texture2D t)
    {
        int w = t.width, h = t.height;
        Clear(t);
        DrawRidge(t, 0.62f, 0.10f, 0.9f, new Color(0.17f, 0.21f, 0.34f));
        DrawRidge(t, 0.44f, 0.16f, 1.7f, new Color(0.12f, 0.15f, 0.26f));
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
        var trunk = new Color(0.10f, 0.09f, 0.13f);
        var leaf = new Color(0.09f, 0.20f, 0.16f);
        var rng = new System.Random(77);

        for (int i = 0; i < 14; i++)
        {
            int cx = 6 + i * (w - 12) / 13;
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

    private static void ApplyToOpenScene()
    {
        AssignLayer("ParallaxLayer_0", "BgSky", new Color(1f, 1f, 1f), -30);
        AssignLayer("ParallaxLayer_1", "BgHills", new Color(1f, 1f, 1f), -20);
        AssignLayer("ParallaxLayer_2", "BgTrees", new Color(1f, 1f, 1f), -10);

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

    private static void AssignLayer(string objectName, string spriteName, Color tint, int order)
    {
        var go = GameObject.Find(objectName);
        if (go == null) { Debug.LogWarning($"[BgArt] {objectName} not found"); return; }

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{spriteName}.png");
        sr.color = tint;
        sr.sortingOrder = order;
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
