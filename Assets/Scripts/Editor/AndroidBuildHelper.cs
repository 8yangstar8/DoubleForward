using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;

/// <summary>
/// Android构建助手 - 一键配置和打包APK/AAB
/// 自动设置所有Android相关的PlayerSettings
/// </summary>
public class AndroidBuildHelper : EditorWindow
{
    private string versionName = "1.0.0";
    private int bundleVersionCode = 1;
    private string packageName = "com.yourstudio.doubleforward";
    private string productName = "Double Forward";
    private string keystorePath = "";
    private string keystorePass = "";
    private string keyAliasName = "doubleforward";
    private string keyAliasPass = "";
    private bool buildAAB = false;
    private bool developmentBuild = false;
    private bool il2cpp = true;
    private AndroidArchitecture targetArch = AndroidArchitecture.ARM64;

    private Vector2 scrollPos;

    [MenuItem("DoubleForward/Android Build Helper", false, 100)]
    public static void ShowWindow()
    {
        var window = GetWindow<AndroidBuildHelper>("Android Build");
        window.minSize = new Vector2(400, 600);
    }

    void OnEnable()
    {
        // 读取当前设置
        versionName = PlayerSettings.bundleVersion;
        bundleVersionCode = PlayerSettings.Android.bundleVersionCode;
        packageName = PlayerSettings.applicationIdentifier;
        productName = PlayerSettings.productName;
        keystorePath = PlayerSettings.Android.keystoreName ?? "";
        keyAliasName = PlayerSettings.Android.keyaliasName ?? "doubleforward";
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Android Build Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // ===== 基本信息 =====
        EditorGUILayout.LabelField("基本信息", EditorStyles.boldLabel);
        productName = EditorGUILayout.TextField("产品名称", productName);
        packageName = EditorGUILayout.TextField("包名 (Package)", packageName);
        versionName = EditorGUILayout.TextField("版本号", versionName);
        bundleVersionCode = EditorGUILayout.IntField("版本Code", bundleVersionCode);

        EditorGUILayout.Space(10);

        // ===== 构建设置 =====
        EditorGUILayout.LabelField("构建设置", EditorStyles.boldLabel);
        buildAAB = EditorGUILayout.Toggle("构建 AAB (Google Play)", buildAAB);
        developmentBuild = EditorGUILayout.Toggle("开发版本", developmentBuild);
        il2cpp = EditorGUILayout.Toggle("使用 IL2CPP", il2cpp);
        targetArch = (AndroidArchitecture)EditorGUILayout.EnumFlagsField("目标架构", targetArch);

        EditorGUILayout.Space(10);

        // ===== 签名 =====
        EditorGUILayout.LabelField("签名设置", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        keystorePath = EditorGUILayout.TextField("Keystore路径", keystorePath);
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string path = EditorUtility.OpenFilePanel("Select Keystore", "", "keystore,jks");
            if (!string.IsNullOrEmpty(path)) keystorePath = path;
        }
        EditorGUILayout.EndHorizontal();
        keystorePass = EditorGUILayout.PasswordField("Keystore密码", keystorePass);
        keyAliasName = EditorGUILayout.TextField("Key别名", keyAliasName);
        keyAliasPass = EditorGUILayout.PasswordField("Key密码", keyAliasPass);

        EditorGUILayout.Space(20);

        // ===== 操作按钮 =====
        EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);

        if (GUILayout.Button("应用设置 (不构建)", GUILayout.Height(30)))
        {
            ApplySettings();
        }

        EditorGUILayout.Space(5);

        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(keystorePath) && !developmentBuild);
        if (GUILayout.Button("一键构建", GUILayout.Height(40)))
        {
            ApplySettings();
            BuildAndroid();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(10);

        if (GUILayout.Button("自动配置推荐设置", GUILayout.Height(25)))
        {
            AutoConfigureRecommended();
        }

        EditorGUILayout.Space(10);

        // ===== 诊断 =====
        EditorGUILayout.LabelField("诊断信息", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"Unity: {Application.unityVersion}\n" +
            $"当前平台: {EditorUserBuildSettings.activeBuildTarget}\n" +
            $"脚本后端: {PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)}\n" +
            $"最低API: {PlayerSettings.Android.minSdkVersion}\n" +
            $"目标API: {PlayerSettings.Android.targetSdkVersion}\n" +
            $"屏幕方向: {PlayerSettings.defaultInterfaceOrientation}",
            MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private void ApplySettings()
    {
        PlayerSettings.productName = productName;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, packageName);
        PlayerSettings.bundleVersion = versionName;
        PlayerSettings.Android.bundleVersionCode = bundleVersionCode;

        // 脚本后端
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,
            il2cpp ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);

        // 架构
        PlayerSettings.Android.targetArchitectures = targetArch;

        // 签名
        if (!string.IsNullOrEmpty(keystorePath))
        {
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = keyAliasName;
            PlayerSettings.Android.keyaliasPass = keyAliasPass;
        }

        Debug.Log("[AndroidBuild] Settings applied successfully");
        EditorUtility.DisplayDialog("完成", "Android设置已应用", "OK");
    }

    private void AutoConfigureRecommended()
    {
        // 推荐设置
        PlayerSettings.productName = "Double Forward";
        PlayerSettings.companyName = "YourStudio";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.yourstudio.doubleforward");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        // 图形
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
            new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.openGLRequireES31 = false;
        PlayerSettings.openGLRequireES31AEP = false;
        PlayerSettings.openGLRequireES32 = false;

        // Android特定
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24; // Android 7.0
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33; // Android 13
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Medium);

        // 图标和启动画面
        PlayerSettings.SplashScreen.show = false; // 使用自定义启动画面

        // 性能
        PlayerSettings.gcIncremental = true;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        // 更新UI
        versionName = PlayerSettings.bundleVersion;
        packageName = PlayerSettings.applicationIdentifier;
        productName = PlayerSettings.productName;
        il2cpp = true;
        targetArch = AndroidArchitecture.ARM64;

        Debug.Log("[AndroidBuild] Recommended settings applied");
        EditorUtility.DisplayDialog("完成", "推荐Android设置已应用！\n\n包含：横屏、IL2CPP、ARM64、API24-33", "OK");
        Repaint();
    }

    private void BuildAndroid()
    {
        // 收集场景
        var scenes = new System.Collections.Generic.List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
                scenes.Add(scene.path);
        }

        if (scenes.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "Build Settings中没有场景！请先添加场景。", "OK");
            return;
        }

        // 输出路径
        string extension = buildAAB ? "aab" : "apk";
        string defaultName = $"DoubleForward_v{versionName}_{bundleVersionCode}.{extension}";
        string buildDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "Android");

        if (!Directory.Exists(buildDir))
            Directory.CreateDirectory(buildDir);

        string outputPath = Path.Combine(buildDir, defaultName);

        // 构建选项
        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        if (developmentBuild)
            options.options |= BuildOptions.Development;

        EditorUserBuildSettings.buildAppBundle = buildAAB;

        // 执行构建
        Debug.Log($"[AndroidBuild] Starting build: {outputPath}");
        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            long sizeBytes = report.summary.totalSize;
            string sizeMB = (sizeBytes / (1024f * 1024f)).ToString("F1");
            EditorUtility.DisplayDialog("构建成功",
                $"构建完成！\n路径：{outputPath}\n大小：{sizeMB} MB\n耗时：{report.summary.totalTime.TotalSeconds:F0}秒",
                "打开文件夹");
            EditorUtility.RevealInFinder(outputPath);
        }
        else
        {
            EditorUtility.DisplayDialog("构建失败",
                $"构建失败：{report.summary.result}\n共 {report.summary.totalErrors} 个错误", "OK");
        }
    }
}

/// <summary>
/// 构建前自动处理 - 验证配置和清理
/// </summary>
public class BuildPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android)
        {
            Debug.Log("[Build] Pre-processing Android build...");

            // 验证场景列表
            var scenes = EditorBuildSettings.scenes;
            foreach (var scene in scenes)
            {
                if (scene.enabled && !File.Exists(scene.path))
                {
                    Debug.LogWarning($"[Build] Missing scene: {scene.path}");
                }
            }

            // 确保 Scripting Define Symbols 正确
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            Debug.Log($"[Build] Scripting defines: {defines}");
        }
    }
}
