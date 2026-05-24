using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 资源审计工具 - 检查项目中的资源问题
/// 查找大纹理、未使用资源、重复资源、不正确的导入设置等
/// 帮助优化Android包体积
/// </summary>
public class AssetAuditor : EditorWindow
{
    private Vector2 scrollPos;
    private List<AuditResult> results = new List<AuditResult>();
    private bool hasRun;
    private int auditTab;
    private string[] tabNames = { "Textures", "Audio", "Prefabs", "Unused", "Summary" };

    private class AuditResult
    {
        public string category;
        public string severity; // Info, Warning, Error
        public string message;
        public string assetPath;
        public long fileSize;
        public Object asset;
    }

    [MenuItem("DoubleForward/Asset Auditor", false, 202)]
    public static void ShowWindow()
    {
        var window = GetWindow<AssetAuditor>("Asset Auditor");
        window.minSize = new Vector2(600, 500);
    }

    void OnGUI()
    {
        GUILayout.Label("Asset Auditor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scans project assets for optimization opportunities.\n" +
            "Focus: mobile (Android) build size and performance.",
            MessageType.Info);

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Run Full Audit", GUILayout.Height(35)))
            RunFullAudit();
        if (GUILayout.Button("Quick Scan", GUILayout.Height(35)))
            RunQuickScan();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (!hasRun)
        {
            EditorGUILayout.HelpBox("Click 'Run Full Audit' to scan all project assets.", MessageType.None);
            return;
        }

        // 选项卡
        auditTab = GUILayout.Toolbar(auditTab, tabNames);
        EditorGUILayout.Space(5);

        // 统计
        string filter = auditTab < tabNames.Length - 1 ? tabNames[auditTab] : "";
        var filtered = string.IsNullOrEmpty(filter) ?
            results : results.Where(r => r.category == filter).ToList();

        int errors = filtered.Count(r => r.severity == "Error");
        int warnings = filtered.Count(r => r.severity == "Warning");
        int infos = filtered.Count(r => r.severity == "Info");
        EditorGUILayout.LabelField($"Results: {errors} errors, {warnings} warnings, {infos} info");

        // 结果列表
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (auditTab == tabNames.Length - 1)
        {
            DrawSummary();
        }
        else
        {
            foreach (var result in filtered)
            {
                DrawResultRow(result);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawResultRow(AuditResult result)
    {
        string icon = result.severity == "Error" ? "X" : result.severity == "Warning" ? "!" : "i";
        MessageType msgType = result.severity == "Error" ? MessageType.Error :
                              result.severity == "Warning" ? MessageType.Warning :
                              MessageType.Info;

        EditorGUILayout.BeginHorizontal();

        string sizeStr = result.fileSize > 0 ? $" ({FormatSize(result.fileSize)})" : "";
        EditorGUILayout.HelpBox($"[{icon}] {result.message}{sizeStr}", msgType);

        if (result.asset != null)
        {
            if (GUILayout.Button("Select", GUILayout.Width(55), GUILayout.Height(38)))
            {
                Selection.activeObject = result.asset;
                EditorGUIUtility.PingObject(result.asset);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSummary()
    {
        // 总体统计
        long totalTextureSize = results.Where(r => r.category == "Textures").Sum(r => r.fileSize);
        long totalAudioSize = results.Where(r => r.category == "Audio").Sum(r => r.fileSize);
        int textureIssues = results.Count(r => r.category == "Textures" && r.severity != "Info");
        int audioIssues = results.Count(r => r.category == "Audio" && r.severity != "Info");

        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField($"Texture Issues: {textureIssues}");
        EditorGUILayout.LabelField($"Audio Issues: {audioIssues}");
        EditorGUILayout.LabelField($"Total Texture Size: {FormatSize(totalTextureSize)}");
        EditorGUILayout.LabelField($"Total Audio Size: {FormatSize(totalAudioSize)}");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Recommendations", EditorStyles.boldLabel);

        if (totalTextureSize > 50 * 1024 * 1024)
            EditorGUILayout.HelpBox("Total texture size exceeds 50MB. Consider using texture compression (ASTC for Android).",
                MessageType.Warning);

        if (totalAudioSize > 20 * 1024 * 1024)
            EditorGUILayout.HelpBox("Total audio size exceeds 20MB. Consider using Vorbis compression for BGM.",
                MessageType.Warning);
    }

    // ==================== 扫描逻辑 ====================

    private void RunFullAudit()
    {
        results.Clear();
        hasRun = true;

        EditorUtility.DisplayProgressBar("Asset Audit", "Scanning textures...", 0.2f);
        AuditTextures();

        EditorUtility.DisplayProgressBar("Asset Audit", "Scanning audio...", 0.4f);
        AuditAudio();

        EditorUtility.DisplayProgressBar("Asset Audit", "Scanning prefabs...", 0.6f);
        AuditPrefabs();

        EditorUtility.DisplayProgressBar("Asset Audit", "Finding unused assets...", 0.8f);
        FindUnusedAssets();

        EditorUtility.ClearProgressBar();
        Repaint();
    }

    private void RunQuickScan()
    {
        results.Clear();
        hasRun = true;

        AuditTextures();
        AuditAudio();

        Repaint();
    }

    private void AuditTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            long fileSize = new FileInfo(path).Length;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            // 检查纹理大小
            if (texture != null && (texture.width > 2048 || texture.height > 2048))
            {
                results.Add(new AuditResult
                {
                    category = "Textures",
                    severity = "Warning",
                    message = $"Large texture: {texture.width}x{texture.height} - {Path.GetFileName(path)}",
                    assetPath = path,
                    fileSize = fileSize,
                    asset = texture
                });
            }

            // 检查非2次方
            if (texture != null && (!IsPowerOfTwo(texture.width) || !IsPowerOfTwo(texture.height)))
            {
                if (importer.textureType == TextureImporterType.Sprite) continue; // Sprite可以不是2次方

                results.Add(new AuditResult
                {
                    category = "Textures",
                    severity = "Info",
                    message = $"Non-power-of-2: {texture.width}x{texture.height} - {Path.GetFileName(path)}",
                    assetPath = path,
                    fileSize = fileSize,
                    asset = texture
                });
            }

            // 检查文件大小 > 1MB
            if (fileSize > 1024 * 1024)
            {
                results.Add(new AuditResult
                {
                    category = "Textures",
                    severity = "Warning",
                    message = $"Large file: {Path.GetFileName(path)}",
                    assetPath = path,
                    fileSize = fileSize,
                    asset = texture
                });
            }

            // 检查是否使用了压缩
            var androidSettings = importer.GetPlatformTextureSettings("Android");
            if (!androidSettings.overridden && fileSize > 256 * 1024)
            {
                results.Add(new AuditResult
                {
                    category = "Textures",
                    severity = "Warning",
                    message = $"No Android-specific compression: {Path.GetFileName(path)}",
                    assetPath = path,
                    fileSize = fileSize,
                    asset = texture
                });
            }
        }
    }

    private void AuditAudio()
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;

            long fileSize = new FileInfo(path).Length;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            // 检查长音频不使用流式加载
            if (clip != null && clip.length > 30f)
            {
                var sampleSettings = importer.defaultSampleSettings;
                if (sampleSettings.loadType != AudioClipLoadType.Streaming)
                {
                    results.Add(new AuditResult
                    {
                        category = "Audio",
                        severity = "Warning",
                        message = $"Long clip ({clip.length:F0}s) not streaming: {Path.GetFileName(path)}",
                        assetPath = path,
                        fileSize = fileSize,
                        asset = clip
                    });
                }
            }

            // 检查大文件
            if (fileSize > 2 * 1024 * 1024)
            {
                results.Add(new AuditResult
                {
                    category = "Audio",
                    severity = "Warning",
                    message = $"Large audio file: {Path.GetFileName(path)}",
                    assetPath = path,
                    fileSize = fileSize,
                    asset = clip
                });
            }

            // 检查立体声（手机通常不需要）
            if (clip != null && clip.channels > 1 && clip.length < 5f)
            {
                results.Add(new AuditResult
                {
                    category = "Audio",
                    severity = "Info",
                    message = $"Stereo SFX (mono saves space): {Path.GetFileName(path)}",
                    assetPath = path,
                    fileSize = fileSize,
                    asset = clip
                });
            }
        }
    }

    private void AuditPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            // 检查组件数量
            var components = prefab.GetComponentsInChildren<Component>(true);
            if (components.Length > 50)
            {
                results.Add(new AuditResult
                {
                    category = "Prefabs",
                    severity = "Warning",
                    message = $"Complex prefab ({components.Length} components): {Path.GetFileName(path)}",
                    assetPath = path,
                    asset = prefab
                });
            }

            // 检查缺失引用
            int missingCount = 0;
            foreach (var comp in components)
            {
                if (comp == null) missingCount++;
            }

            if (missingCount > 0)
            {
                results.Add(new AuditResult
                {
                    category = "Prefabs",
                    severity = "Error",
                    message = $"Missing components ({missingCount}): {Path.GetFileName(path)}",
                    assetPath = path,
                    asset = prefab
                });
            }
        }
    }

    private void FindUnusedAssets()
    {
        // 简化版：检查不在任何场景或Prefab引用中的资源
        // 完整版需要深度依赖分析，这里只检查明显的孤立文件

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        var usedGuids = new HashSet<string>();

        // 收集场景和Prefab引用的资源
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string[] dependencies = AssetDatabase.GetDependencies(path, true);
            foreach (string dep in dependencies)
            {
                string depGuid = AssetDatabase.AssetPathToGUID(dep);
                usedGuids.Add(depGuid);
            }
        }

        // 场景依赖
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string[] dependencies = AssetDatabase.GetDependencies(path, true);
            foreach (string dep in dependencies)
            {
                string depGuid = AssetDatabase.AssetPathToGUID(dep);
                usedGuids.Add(depGuid);
            }
        }

        // 标记未使用的
        foreach (string guid in textureGuids)
        {
            if (!usedGuids.Contains(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // 排除编辑器资源
                if (path.Contains("/Editor/")) continue;
                if (path.Contains("/Resources/")) continue; // Resources会被动态加载

                long fileSize = File.Exists(path) ? new FileInfo(path).Length : 0;

                results.Add(new AuditResult
                {
                    category = "Unused",
                    severity = "Info",
                    message = $"Potentially unused: {Path.GetFileName(path)}",
                    assetPath = path,
                    fileSize = fileSize,
                    asset = AssetDatabase.LoadAssetAtPath<Object>(path)
                });
            }
        }
    }

    // ==================== 工具 ====================

    private bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }

    private string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024f * 1024f):F1} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024f:F1} KB";
        return $"{bytes} B";
    }
}
