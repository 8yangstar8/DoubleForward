using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

/// <summary>
/// URP渲染管线配置工厂 - 创建2D Renderer、Pipeline Asset、全局Volume
/// 确保项目使用正确的URP 2D渲染管线
/// 菜单：DoubleForward / Setup URP Pipeline
/// </summary>
public static class URPSetupFactory
{
    private const string SETTINGS_DIR = "Assets/Settings";
    private const string PREFAB_DIR = "Assets/Prefabs";

    [MenuItem("DoubleForward/Setup URP Pipeline", false, 3)]
    public static void SetupAll()
    {
        EnsureDir(SETTINGS_DIR);

        int created = 0;

        EditorUtility.DisplayProgressBar("Setting up URP...", "Creating Renderer2D...", 0.2f);
        created += CreateRenderer2DAsset();

        EditorUtility.DisplayProgressBar("Setting up URP...", "Creating Pipeline Asset...", 0.4f);
        created += CreatePipelineAsset();

        EditorUtility.DisplayProgressBar("Setting up URP...", "Creating Volume Profile...", 0.6f);
        created += CreateGlobalVolumeProfile();

        EditorUtility.DisplayProgressBar("Setting up URP...", "Creating Volume prefab...", 0.8f);
        created += CreateGlobalVolumePrefab();

        EditorUtility.DisplayProgressBar("Setting up URP...", "Applying settings...", 0.95f);
        ApplyPipelineSettings();

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[URPSetupFactory] URP pipeline configured. Created {created} assets.");
        EditorUtility.DisplayDialog("URP配置完成",
            $"已配置URP 2D渲染管线：\n\n" +
            "• Renderer2D Data（2D光照）\n" +
            "• Universal Render Pipeline Asset\n" +
            "• Global Volume Profile（Bloom + Vignette）\n" +
            "• GlobalVolume预制体\n" +
            "• 已设置为项目默认渲染管线",
            "OK");
    }

    // ==================== Renderer2D ====================

    private static int CreateRenderer2DAsset()
    {
        string path = $"{SETTINGS_DIR}/Renderer2D_Data.asset";
        if (File.Exists(path)) return 0;

        // 创建Renderer2D Data
        var rendererData = ScriptableObject.CreateInstance<Renderer2DData>();

        // 通过SerializedObject配置2D光照
        var so = new SerializedObject(rendererData);

        // HDR Color Buffer（启用后处理效果更好）
        var hdrProp = so.FindProperty("m_HDRColorBufferPrecision");
        if (hdrProp != null)
            hdrProp.intValue = 1; // _32Bits

        // 默认材质类型
        var defaultMatTypeProp = so.FindProperty("m_DefaultMaterialType");
        if (defaultMatTypeProp != null)
            defaultMatTypeProp.intValue = 0; // Lit

        so.ApplyModifiedProperties();

        AssetDatabase.CreateAsset(rendererData, path);
        Debug.Log($"[URP] Created Renderer2D Data: {path}");
        return 1;
    }

    // ==================== Pipeline Asset ====================

    private static int CreatePipelineAsset()
    {
        string path = $"{SETTINGS_DIR}/URP_PipelineAsset.asset";
        if (File.Exists(path)) return 0;

        // 加载Renderer2D
        string rendererPath = $"{SETTINGS_DIR}/Renderer2D_Data.asset";
        var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);

        // 创建Pipeline Asset
        // 尝试公开API，失败则用反射
        UniversalRenderPipelineAsset pipelineAsset = null;

        try
        {
            // Unity 2022.3 公开方法
            pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
        }
        catch
        {
            // 回退：直接创建实例并通过SerializedObject关联Renderer
            pipelineAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
        }

        if (pipelineAsset == null)
        {
            Debug.LogError("[URP] Failed to create Pipeline Asset");
            return 0;
        }

        // 通过SerializedObject微调和关联Renderer
        var so = new SerializedObject(pipelineAsset);

        // 关联Renderer（如果通过反射创建需要这步）
        var rendererListProp = so.FindProperty("m_RendererDataList");
        if (rendererListProp != null && rendererData != null)
        {
            rendererListProp.arraySize = 1;
            rendererListProp.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
        }

        // HDR
        var hdrProp = so.FindProperty("m_SupportsHDR");
        if (hdrProp != null) hdrProp.boolValue = true;

        // MSAA - 移动端关闭以提高性能
        var msaaProp = so.FindProperty("m_MSAA");
        if (msaaProp != null) msaaProp.intValue = 1; // No MSAA

        // 阴影距离（2D游戏不需要太远）
        var shadowDistProp = so.FindProperty("m_MainLightShadowmapResolution");
        if (shadowDistProp != null) shadowDistProp.intValue = 1024;

        // SRP Batcher（性能优化）
        var srpBatcherProp = so.FindProperty("m_UseSRPBatcher");
        if (srpBatcherProp != null) srpBatcherProp.boolValue = true;

        so.ApplyModifiedProperties();

        AssetDatabase.CreateAsset(pipelineAsset, path);
        Debug.Log($"[URP] Created Pipeline Asset: {path}");
        return 1;
    }

    // ==================== Volume Profile ====================

    private static int CreateGlobalVolumeProfile()
    {
        string path = $"{SETTINGS_DIR}/GlobalVolumeProfile.asset";
        if (File.Exists(path)) return 0;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        // Bloom
        var bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.5f);
        bloom.scatter.Override(0.7f);

        // Vignette
        var vignette = profile.Add<Vignette>(true);
        vignette.active = true;
        vignette.intensity.Override(0.2f);
        vignette.smoothness.Override(0.4f);
        vignette.color.Override(Color.black);

        // Color Adjustments（稍微提亮画面）
        var colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.active = true;
        colorAdj.postExposure.Override(0.1f);
        colorAdj.contrast.Override(5f);
        colorAdj.saturation.Override(10f);

        AssetDatabase.CreateAsset(profile, path);
        Debug.Log($"[URP] Created Volume Profile: {path}");
        return 1;
    }

    // ==================== Global Volume Prefab ====================

    private static int CreateGlobalVolumePrefab()
    {
        EnsureDir($"{PREFAB_DIR}/Managers");
        string path = $"{PREFAB_DIR}/Managers/GlobalVolume.prefab";
        if (File.Exists(path)) return 0;

        var obj = new GameObject("GlobalVolume");

        var volume = obj.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0;

        // 关联VolumeProfile
        string profilePath = $"{SETTINGS_DIR}/GlobalVolumeProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile != null)
            volume.profile = profile;

        // 添加全局2D光源
        var lightObj = new GameObject("GlobalLight2D");
        lightObj.transform.SetParent(obj.transform);
        lightObj.transform.localPosition = Vector3.zero;

        var light2D = lightObj.AddComponent<Light2D>();
        light2D.lightType = Light2D.LightType.Global;
        light2D.intensity = 0.85f;
        light2D.color = new Color(1f, 0.98f, 0.95f); // 略暖色

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
        Debug.Log($"[URP] Created GlobalVolume prefab: {path}");
        return 1;
    }

    // ==================== 应用设置 ====================

    private static void ApplyPipelineSettings()
    {
        string pipelinePath = $"{SETTINGS_DIR}/URP_PipelineAsset.asset";
        var pipelineAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(pipelinePath);

        if (pipelineAsset != null)
        {
            // 设置为项目默认渲染管线
            if (GraphicsSettings.defaultRenderPipeline != pipelineAsset)
            {
                GraphicsSettings.defaultRenderPipeline = pipelineAsset;
                Debug.Log("[URP] Set as default render pipeline");
            }

            // Quality Settings也设置
            QualitySettings.renderPipeline = pipelineAsset;
            Debug.Log("[URP] Set quality render pipeline");
        }
    }

    /// <summary>
    /// 检查URP是否已配置
    /// </summary>
    public static bool HasURPSetup()
    {
        return File.Exists($"{SETTINGS_DIR}/URP_PipelineAsset.asset")
            && File.Exists($"{SETTINGS_DIR}/Renderer2D_Data.asset");
    }

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
