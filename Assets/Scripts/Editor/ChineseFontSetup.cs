using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;

public static class ChineseFontSetup
{
    private const string FONT_DIR = "Assets/Fonts";
    private const string FONT_ASSET_PATH = "Assets/Fonts/ChineseFont_SDF.asset";

    [MenuItem("DoubleForward/Setup Chinese Font", false, 65)]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("提示", "请先停止Play模式！", "OK");
            return;
        }

        if (!Directory.Exists(FONT_DIR)) Directory.CreateDirectory(FONT_DIR);

        string ttfPath = $"{FONT_DIR}/simhei.ttf";
        if (!File.Exists(ttfPath))
        {
            File.Copy("C:/Windows/Fonts/simhei.ttf", ttfPath, true);
        }
        AssetDatabase.ImportAsset(ttfPath, ImportAssetOptions.ForceSynchronousImport);

        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            EditorUtility.DisplayDialog("Error", "Cannot load simhei.ttf", "OK");
            return;
        }

        // 删除旧资产重建
        if (File.Exists(FONT_ASSET_PATH))
        {
            AssetDatabase.DeleteAsset(FONT_ASSET_PATH);
        }

        // 创建高质量字体资产（大采样+大图集=清晰文字）
        var fontAsset = TMP_FontAsset.CreateFontAsset(
            font,
            72,                              // 采样点大小（越大越清晰）
            6,                               // 填充
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
            2048,                            // 图集宽度
            2048                             // 图集高度
        );
        if (fontAsset == null)
        {
            EditorUtility.DisplayDialog("Error", "CreateFontAsset failed", "OK");
            return;
        }

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

        // 先保存主资产
        AssetDatabase.CreateAsset(fontAsset, FONT_ASSET_PATH);

        // 将atlas texture和material作为子资产保存
        if (fontAsset.atlasTextures != null)
        {
            for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
            {
                var tex = fontAsset.atlasTextures[i];
                if (tex != null)
                {
                    tex.name = $"ChineseFont_SDF Atlas {i}";
                    AssetDatabase.AddObjectToAsset(tex, fontAsset);
                }
            }
        }

        if (fontAsset.material != null)
        {
            fontAsset.material.name = "ChineseFont_SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FONT_ASSET_PATH);

        // 验证
        var check = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);
        if (check == null || check.atlasTextures == null || check.atlasTextures.Length == 0 || check.atlasTextures[0] == null)
        {
            Debug.LogError("[FontSetup] Atlas texture still missing after save!");
            EditorUtility.DisplayDialog("Error", "Font atlas validation failed", "OK");
            return;
        }
        Debug.Log($"[FontSetup] Font asset validated: atlas={check.atlasTextures[0].name}");

        // 添加TMP默认字体(LiberationSans)为fallback，支持♥⏸等符号
        AddDefaultFontAsFallback(check);

        // 设为默认字体
        SetAsDefault(check);

        // 更新场景
        UpdateAllScenes(check);

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("完成", "中文字体设置完成！\n请用 Play Boot Scene 测试。", "OK");
    }

    public static void SetupFromCommandLine() { Setup(); }

    private static void AddDefaultFontAsFallback(TMP_FontAsset fontAsset)
    {
        // 找到LiberationSans SDF（TMP自带，含♥⏸等符号）
        string[] guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
        if (guids.Length == 0) return;

        var libFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (libFont == null) return;

        if (fontAsset.fallbackFontAssetTable == null)
            fontAsset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();

        if (!fontAsset.fallbackFontAssetTable.Contains(libFont))
        {
            fontAsset.fallbackFontAssetTable.Add(libFont);
            EditorUtility.SetDirty(fontAsset);
            Debug.Log("[FontSetup] Added LiberationSans SDF as fallback for symbols");
        }
    }

    private static void SetAsDefault(TMP_FontAsset fontAsset)
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_Settings");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[FontSetup] TMP_Settings not found");
            return;
        }

        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (settings == null) return;

        var so = new SerializedObject(settings);
        var prop = so.FindProperty("m_defaultFontAsset");
        if (prop != null)
        {
            prop.objectReferenceValue = fontAsset;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
        }

        var fallback = so.FindProperty("m_fallbackFontAssets");
        if (fallback != null && fallback.isArray)
        {
            bool found = false;
            for (int i = 0; i < fallback.arraySize; i++)
                if (fallback.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset)
                { found = true; break; }
            if (!found)
            {
                fallback.InsertArrayElementAtIndex(fallback.arraySize);
                fallback.GetArrayElementAtIndex(fallback.arraySize - 1).objectReferenceValue = fontAsset;
                so.ApplyModifiedProperties();
            }
        }

        Debug.Log("[FontSetup] Set as TMP default + fallback font");
    }

    private static void UpdateAllScenes(TMP_FontAsset fontAsset)
    {
        string currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
        int updated = 0;

        string[] sceneFiles = Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories);
        foreach (var file in sceneFiles)
        {
            string path = file.Replace('\\', '/');
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
            var texts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (var text in texts)
            {
                text.font = fontAsset;
                if (!text.enabled) text.enabled = true;
                // 增大字体使文字清晰
                if (text.fontSize < 28) text.fontSize = 28;
                EditorUtility.SetDirty(text);
                updated++;
            }

            // 修复CanvasScaler为横屏分辨率
            var scalers = Object.FindObjectsByType<UnityEngine.UI.CanvasScaler>(FindObjectsSortMode.None);
            foreach (var scaler in scalers)
            {
                if (scaler.referenceResolution.x < scaler.referenceResolution.y)
                {
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    EditorUtility.SetDirty(scaler);
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrEmpty(currentScene) && File.Exists(currentScene))
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(currentScene);

        Debug.Log($"[FontSetup] Updated {updated} TMP texts across {sceneFiles.Length} scenes");
    }
}
