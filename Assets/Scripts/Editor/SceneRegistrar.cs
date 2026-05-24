using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 场景注册工具 - 自动将项目中的场景添加到Build Settings
/// 支持拖拽排序、按章节分组、一键配置
/// </summary>
public class SceneRegistrar : EditorWindow
{
    private Vector2 scrollPos;
    private List<SceneEntry> sceneEntries = new List<SceneEntry>();
    private bool showUnregistered = true;
    private string searchFilter = "";

    private class SceneEntry
    {
        public string path;
        public string name;
        public bool isRegistered;
        public bool isEnabled;
        public int buildIndex;
        public string group; // 分组标签
    }

    [MenuItem("DoubleForward/Scene Registrar", false, 201)]
    public static void ShowWindow()
    {
        var window = GetWindow<SceneRegistrar>("Scene Registrar");
        window.minSize = new Vector2(500, 400);
    }

    void OnEnable()
    {
        RefreshSceneList();
    }

    void OnGUI()
    {
        DrawHeader();
        DrawToolbar();
        DrawSceneList();
    }

    private void DrawHeader()
    {
        GUILayout.Label("Scene Registrar", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Manage which scenes are included in the build. " +
            "Scenes are auto-detected from the Assets folder.",
            MessageType.Info);
        EditorGUILayout.Space(5);
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            RefreshSceneList();

        if (GUILayout.Button("Auto-Configure", EditorStyles.toolbarButton, GUILayout.Width(100)))
            AutoConfigure();

        if (GUILayout.Button("Clear All", EditorStyles.toolbarButton, GUILayout.Width(60)))
            ClearAll();

        GUILayout.FlexibleSpace();

        showUnregistered = GUILayout.Toggle(showUnregistered, "Show Unregistered",
            EditorStyles.toolbarButton, GUILayout.Width(120));

        searchFilter = EditorGUILayout.TextField(searchFilter,
            EditorStyles.toolbarSearchField, GUILayout.Width(150));

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);
    }

    private void DrawSceneList()
    {
        // 统计
        int registered = sceneEntries.Count(s => s.isRegistered);
        int total = sceneEntries.Count;
        EditorGUILayout.LabelField($"Scenes: {registered} registered / {total} total");
        EditorGUILayout.Space(3);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        string currentGroup = "";

        foreach (var scene in sceneEntries)
        {
            // 搜索过滤
            if (!string.IsNullOrEmpty(searchFilter) &&
                !scene.name.ToLower().Contains(searchFilter.ToLower()) &&
                !scene.path.ToLower().Contains(searchFilter.ToLower()))
                continue;

            if (!showUnregistered && !scene.isRegistered)
                continue;

            // 分组标题
            if (scene.group != currentGroup)
            {
                currentGroup = scene.group;
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(currentGroup, EditorStyles.boldLabel);
            }

            DrawSceneRow(scene);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSceneRow(SceneEntry scene)
    {
        EditorGUILayout.BeginHorizontal();

        // 启用/禁用复选框
        bool newEnabled = EditorGUILayout.Toggle(scene.isRegistered, GUILayout.Width(20));
        if (newEnabled != scene.isRegistered)
        {
            if (newEnabled)
                AddSceneToBuild(scene.path);
            else
                RemoveSceneFromBuild(scene.path);

            RefreshSceneList();
        }

        // 场景名称
        GUIStyle nameStyle = scene.isRegistered ? EditorStyles.boldLabel : EditorStyles.label;
        Color origColor = GUI.color;
        if (!scene.isRegistered) GUI.color = Color.gray;

        EditorGUILayout.LabelField(scene.name, nameStyle, GUILayout.MinWidth(150));

        GUI.color = origColor;

        // Build Index
        if (scene.isRegistered)
        {
            EditorGUILayout.LabelField($"[{scene.buildIndex}]", GUILayout.Width(35));
        }
        else
        {
            EditorGUILayout.LabelField("", GUILayout.Width(35));
        }

        // 路径（缩短显示）
        string shortPath = scene.path.Replace("Assets/", "");
        EditorGUILayout.LabelField(shortPath, EditorStyles.miniLabel);

        // 打开按钮
        if (GUILayout.Button("Open", GUILayout.Width(45)))
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(scene.path);
        }

        // 定位按钮
        if (GUILayout.Button("Ping", GUILayout.Width(35)))
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(scene.path);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    // ==================== 逻辑 ====================

    private void RefreshSceneList()
    {
        sceneEntries.Clear();

        // 查找所有场景文件
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        var buildScenes = EditorBuildSettings.scenes;
        var buildPaths = new HashSet<string>(buildScenes.Select(s => s.path));

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);

            var entry = new SceneEntry
            {
                path = path,
                name = name,
                isRegistered = buildPaths.Contains(path),
                group = ClassifyScene(path, name)
            };

            if (entry.isRegistered)
            {
                for (int i = 0; i < buildScenes.Length; i++)
                {
                    if (buildScenes[i].path == path)
                    {
                        entry.buildIndex = i;
                        entry.isEnabled = buildScenes[i].enabled;
                        break;
                    }
                }
            }

            sceneEntries.Add(entry);
        }

        // 排序: 已注册优先，然后按分组、名称
        sceneEntries.Sort((a, b) =>
        {
            if (a.isRegistered != b.isRegistered)
                return b.isRegistered.CompareTo(a.isRegistered);

            int groupCompare = string.Compare(a.group, b.group);
            if (groupCompare != 0) return groupCompare;

            return string.Compare(a.name, b.name);
        });
    }

    private string ClassifyScene(string path, string name)
    {
        string lower = path.ToLower();
        string lowerName = name.ToLower();

        if (lowerName.Contains("menu") || lowerName.Contains("title") || lowerName.Contains("splash"))
            return "01 - Menu";
        if (lowerName.Contains("lobby") || lowerName.Contains("network"))
            return "02 - Lobby";
        if (lower.Contains("chapter1") || lower.Contains("ch1") || lower.Contains("forest"))
            return "03 - Chapter 1 (Forest)";
        if (lower.Contains("chapter2") || lower.Contains("ch2") || lower.Contains("factory") || lower.Contains("gear"))
            return "04 - Chapter 2 (Factory)";
        if (lower.Contains("chapter3") || lower.Contains("ch3") || lower.Contains("abyss") || lower.Contains("ocean"))
            return "05 - Chapter 3 (Abyss)";
        if (lower.Contains("chapter4") || lower.Contains("ch4") || lower.Contains("ruin"))
            return "06 - Chapter 4 (Ruins)";
        if (lower.Contains("chapter5") || lower.Contains("ch5") || lower.Contains("void") || lower.Contains("final"))
            return "07 - Chapter 5 (Void)";
        if (lower.Contains("boss"))
            return "08 - Boss";
        if (lower.Contains("test") || lower.Contains("debug") || lower.Contains("prototype"))
            return "99 - Test";
        if (lower.Contains("cutscene") || lower.Contains("story"))
            return "09 - Cutscenes";

        return "10 - Other";
    }

    /// <summary>
    /// 自动配置推荐的场景顺序
    /// </summary>
    private void AutoConfigure()
    {
        if (!EditorUtility.DisplayDialog("Auto-Configure Scenes",
            "This will reset the Build Settings scene list with recommended ordering:\n\n" +
            "0: MainMenu\n" +
            "1: Lobby\n" +
            "2-20: Level scenes by chapter\n" +
            "21+: Boss scenes\n\n" +
            "Continue?", "Yes", "Cancel"))
            return;

        var orderedScenes = new List<EditorBuildSettingsScene>();

        // 优先级搜索
        AddSceneByName(orderedScenes, "MainMenu", "Title", "SplashScreen");
        AddSceneByName(orderedScenes, "Lobby", "NetworkLobby");
        AddSceneByName(orderedScenes, "Loading", "LoadingScreen");

        // 按章节添加关卡
        for (int chapter = 1; chapter <= 5; chapter++)
        {
            string[] searchPatterns = {
                $"Chapter{chapter}", $"Ch{chapter}", $"Level_{chapter}",
                $"chapter{chapter}", $"ch{chapter}", $"level_{chapter}"
            };

            var chapterScenes = sceneEntries
                .Where(s => searchPatterns.Any(p => s.name.Contains(p) || s.path.Contains(p)))
                .OrderBy(s => s.name)
                .ToList();

            foreach (var scene in chapterScenes)
            {
                if (!orderedScenes.Any(s => s.path == scene.path))
                    orderedScenes.Add(new EditorBuildSettingsScene(scene.path, true));
            }
        }

        // Boss场景
        var bossScenes = sceneEntries
            .Where(s => s.name.ToLower().Contains("boss"))
            .OrderBy(s => s.name);

        foreach (var scene in bossScenes)
        {
            if (!orderedScenes.Any(s => s.path == scene.path))
                orderedScenes.Add(new EditorBuildSettingsScene(scene.path, true));
        }

        // 过场场景
        var cutsceneScenes = sceneEntries
            .Where(s => s.name.ToLower().Contains("cutscene") || s.name.ToLower().Contains("story"))
            .OrderBy(s => s.name);

        foreach (var scene in cutsceneScenes)
        {
            if (!orderedScenes.Any(s => s.path == scene.path))
                orderedScenes.Add(new EditorBuildSettingsScene(scene.path, true));
        }

        EditorBuildSettings.scenes = orderedScenes.ToArray();
        RefreshSceneList();

        Debug.Log($"[SceneRegistrar] Auto-configured {orderedScenes.Count} scenes in Build Settings.");
    }

    private void AddSceneByName(List<EditorBuildSettingsScene> list, params string[] names)
    {
        foreach (string name in names)
        {
            var found = sceneEntries.FirstOrDefault(s =>
                s.name.ToLower().Contains(name.ToLower()));

            if (found != null && !list.Any(s => s.path == found.path))
            {
                list.Add(new EditorBuildSettingsScene(found.path, true));
                return;
            }
        }
    }

    private void AddSceneToBuild(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Any(s => s.path == path))
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private void RemoveSceneFromBuild(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == path);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private void ClearAll()
    {
        if (!EditorUtility.DisplayDialog("Clear All Scenes",
            "Remove ALL scenes from Build Settings?", "Yes", "Cancel"))
            return;

        EditorBuildSettings.scenes = new EditorBuildSettingsScene[0];
        RefreshSceneList();
    }
}
