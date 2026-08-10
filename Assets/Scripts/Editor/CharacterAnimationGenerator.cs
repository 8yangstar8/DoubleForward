using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 角色逐帧动画生成器 - 程序化画出 Lux/Nox 的待机/奔跑/跳跃/下落帧,
/// 并把内容写进 AnimatorFactory already 创建好的那些占位 .anim 里
/// (控制器按GUID引用剪辑,覆写内容即可生效,不必重新接线)。
///
/// 用法: -executeMethod CharacterAnimationGenerator.GenerateAll
/// </summary>
public static class CharacterAnimationGenerator
{
    private const string FrameDir = "Assets/Art/Characters";
    private const string ClipDir = "Assets/Animations/Clips";
    private const int W = 32, H = 48, PPU = 16;

    [MenuItem("DoubleForward/Generate Character Animation", false, 52)]
    public static void GenerateAll()
    {
        if (!Directory.Exists(FrameDir)) Directory.CreateDirectory(FrameDir);

        Build("Lux", new Color(1f, 0.82f, 0.30f), new Color(1f, 0.97f, 0.75f));
        Build("Nox", new Color(0.36f, 0.16f, 0.58f), new Color(0.72f, 0.45f, 0.95f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CharAnim] Lux/Nox idle+run+jump+fall clips rebuilt from generated frames");
    }

    private static void Build(string name, Color body, Color rim)
    {
        // 待机: 轻微起伏
        var idle = new[] { Frame(name, "Idle", 0, body, rim, 0f, 0f, 0),
                           Frame(name, "Idle", 1, body, rim, 0f, 0f, -1) };
        // 奔跑: 四帧腿部循环, 手臂反向摆动
        var run = new List<Sprite>();
        for (int i = 0; i < 4; i++)
        {
            float phase = i * Mathf.PI * 0.5f;
            run.Add(Frame(name, "Run", i, body, rim,
                Mathf.Sin(phase), -Mathf.Sin(phase), (i % 2 == 0) ? 0 : -1));
        }
        var jump = new[] { Frame(name, "Jump", 0, body, rim, 0.7f, -0.9f, 1) };
        var fall = new[] { Frame(name, "Fall", 0, body, rim, -0.6f, 0.8f, -1) };

        WriteClip($"{ClipDir}/{name}_Idle.anim", idle, 0.5f, true);
        WriteClip($"{ClipDir}/{name}_Run.anim", run.ToArray(), 0.12f, true);
        WriteClip($"{ClipDir}/{name}_Jump.anim", jump, 0.4f, false);
        WriteClip($"{ClipDir}/{name}_Fall.anim", fall, 0.4f, false);
    }

    // ==================== 单帧绘制 ====================

    /// <param name="legSwing">-1..1 前后摆腿</param>
    /// <param name="armSwing">-1..1 前后摆臂</param>
    /// <param name="bob">整体上下像素偏移</param>
    private static Sprite Frame(string name, string clip, int index, Color body, Color rim,
        float legSwing, float armSwing, int bob)
    {
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                t.SetPixel(x, y, Color.clear);

        var dark = body * 0.6f; dark.a = 1f;
        int cx = W / 2;

        // 腿(从胯部到脚)
        int hipY = 18 + bob;
        Limb(t, cx - 3, hipY, cx - 3 + Mathf.RoundToInt(legSwing * 5f), 3, 3, dark);
        Limb(t, cx + 2, hipY, cx + 2 - Mathf.RoundToInt(legSwing * 5f), 3, 3, dark);

        // 躯干
        FillRect(t, cx - 5, hipY, cx + 4, hipY + 14, body);
        // 右侧受光更暗一点,做出体积
        FillRect(t, cx + 2, hipY, cx + 4, hipY + 14, body * 0.78f);

        // 手臂
        int shoulderY = hipY + 12;
        Limb(t, cx - 6, shoulderY, cx - 6 + Mathf.RoundToInt(armSwing * 4f), shoulderY - 8, 2, dark);
        Limb(t, cx + 5, shoulderY, cx + 5 - Mathf.RoundToInt(armSwing * 4f), shoulderY - 8, 2, dark);

        // 头
        FillCircle(t, cx, hipY + 20, 5, body);
        FillCircle(t, cx - 2, hipY + 22, 2, rim);   // 高光

        // 轮廓光(左上)
        for (int y = 1; y < H - 1; y++)
            for (int x = 1; x < W - 1; x++)
            {
                if (t.GetPixel(x, y).a > 0.5f) continue;
                if (t.GetPixel(x + 1, y).a > 0.5f || t.GetPixel(x, y - 1).a > 0.5f)
                    t.SetPixel(x, y, new Color(rim.r, rim.g, rim.b, 0.55f));
            }

        t.Apply();
        string path = $"{FrameDir}/{name}_{clip}_{index}.png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);

        AssetDatabase.ImportAsset(path);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = PPU;
            imp.filterMode = FilterMode.Point;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ==================== 剪辑写入 ====================

    /// <summary>把精灵序列写成 SpriteRenderer.m_Sprite 的关键帧曲线</summary>
    private static void WriteClip(string path, Sprite[] frames, float frameTime, bool loop)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool isNew = clip == null;
        if (isNew) clip = new AnimationClip();

        clip.ClearCurves();
        clip.frameRate = Mathf.Max(1f, 1f / frameTime);

        var binding = new EditorCurveBinding
        {
            path = "",
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i * frameTime, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (isNew) AssetDatabase.CreateAsset(clip, path);
        else EditorUtility.SetDirty(clip);
    }

    // ==================== 像素绘制工具 ====================

    private static void FillRect(Texture2D t, int x0, int y0, int x1, int y1, Color c)
    {
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(t.height - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(t.width - 1, x1); x++)
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
    }

    private static void FillCircle(Texture2D t, int cx, int cy, int r, Color c)
    {
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= t.width || y >= t.height) continue;
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                    t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
            }
    }

    /// <summary>从(x0,y0)到(x1,y1)画一条有粗细的肢体</summary>
    private static void Limb(Texture2D t, int x0, int y0, int x1, int y1, int thickness, Color c)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 1);
        for (int i = 0; i <= steps; i++)
        {
            float f = (float)i / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, f));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, f));
            FillRect(t, x, y, x + thickness - 1, y + thickness - 1, c);
        }
    }
}
