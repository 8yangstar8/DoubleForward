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
        // 禁止Play模式下运行
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("提示", "请先停止Play模式再运行此工具！", "OK");
            return;
        }

        // 1. 确保字体文件存在
        if (!Directory.Exists(FONT_DIR)) Directory.CreateDirectory(FONT_DIR);

        string ttfPath = $"{FONT_DIR}/simhei.ttf";
        if (!File.Exists(ttfPath))
        {
            string sysFont = "C:/Windows/Fonts/simhei.ttf";
            if (!File.Exists(sysFont))
            {
                EditorUtility.DisplayDialog("Error", "simhei.ttf not found", "OK");
                return;
            }
            File.Copy(sysFont, ttfPath, true);
        }

        AssetDatabase.ImportAsset(ttfPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.Refresh();

        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            EditorUtility.DisplayDialog("Error", "Cannot load font file", "OK");
            return;
        }

        // 2. 创建或加载TMP字体资产
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);

        if (fontAsset == null)
        {
            // 使用5参数版本确保atlas正确创建
            fontAsset = TMP_FontAsset.CreateFontAsset(
                font, 32, 4,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                512, 512
            );

            if (fontAsset == null)
            {
                EditorUtility.DisplayDialog("Error", "CreateFontAsset failed", "OK");
                return;
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            // 确保atlas texture存在
            if (fontAsset.atlasTexture == null)
            {
                var tex = new Texture2D(512, 512, TextureFormat.Alpha8, false);
                fontAsset.atlasTextures = new Texture2D[] { tex };
                AssetDatabase.AddObjectToAsset(tex, FONT_ASSET_PATH.Replace(".asset", "_temp.asset"));
            }

            AssetDatabase.CreateAsset(fontAsset, FONT_ASSET_PATH);

            // 保存atlas texture为子资产
            if (fontAsset.atlasTextures != null)
            {
                foreach (var tex in fontAsset.atlasTextures)
                {
                    if (tex != null && !AssetDatabase.Contains(tex))
                        AssetDatabase.AddObjectToAsset(tex, fontAsset);
                }
            }
            if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            AssetDatabase.SaveAssets();
            Debug.Log("[FontSetup] Created TMP font asset with atlas");
        }

        // 3. 设置为TMP默认字体
        SetAsDefault(fontAsset);

        // 4. 更新所有场景
        UpdateAllScenes(fontAsset);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完成", "中文字体设置完成！\n请用 Play Boot Scene 测试。", "OK");
    }

    public static void SetupFromCommandLine() { Setup(); }

    private static void SetAsDefault(TMP_FontAsset fontAsset)
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_Settings");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[FontSetup] TMP_Settings not found. Run Window > TMP > Import TMP Essential Resources first.");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(path);
        if (settings == null) return;

        var so = new SerializedObject(settings);
        var prop = so.FindProperty("m_defaultFontAsset");
        if (prop != null)
        {
            prop.objectReferenceValue = fontAsset;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            Debug.Log("[FontSetup] Set as TMP default font");
        }

        // 同时设为fallback字体
        var fallbackProp = so.FindProperty("m_fallbackFontAssets");
        if (fallbackProp != null && fallbackProp.isArray)
        {
            bool alreadyIn = false;
            for (int i = 0; i < fallbackProp.arraySize; i++)
            {
                if (fallbackProp.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset)
                { alreadyIn = true; break; }
            }
            if (!alreadyIn)
            {
                fallbackProp.InsertArrayElementAtIndex(fallbackProp.arraySize);
                fallbackProp.GetArrayElementAtIndex(fallbackProp.arraySize - 1).objectReferenceValue = fontAsset;
                so.ApplyModifiedProperties();
            }
        }
    }

    private static void UpdateAllScenes(TMP_FontAsset fontAsset)
    {
        string currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
        int updated = 0;

        string[] sceneFiles = Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories);
        foreach (var file in sceneFiles)
        {
            string scenePath = file.Replace('\\', '/');
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            var texts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);

            foreach (var text in texts)
            {
                text.font = fontAsset;
                EditorUtility.SetDirty(text);
                updated++;
            }

            if (texts.Length > 0)
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        // 回到原场景
        if (!string.IsNullOrEmpty(currentScene) && File.Exists(currentScene))
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(currentScene);

        Debug.Log($"[FontSetup] Updated {updated} TMP texts across {sceneFiles.Length} scenes");
    }
}
