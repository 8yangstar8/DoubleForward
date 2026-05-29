using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.IO;

/// <summary>
/// 场景UI关联工厂 - 将SceneFactory创建的UI对象关联到对应脚本的SerializeField
/// 修复MainMenu/Level/Pause等场景中UI组件引用为空的问题
/// </summary>
public static class SceneUIWiringFactory
{
    [MenuItem("DoubleForward/Wire All Scene UI References", false, 60)]
    public static void WireAll()
    {
        int totalWired = 0;

        EditorUtility.DisplayProgressBar("Wiring UI", "MainMenu...", 0.1f);
        totalWired += WireMainMenuScene();

        EditorUtility.DisplayProgressBar("Wiring UI", "Level scenes...", 0.3f);
        for (int ch = 1; ch <= 5; ch++)
        {
            for (int lv = 1; lv <= 4; lv++)
            {
                float p = 0.3f + (float)((ch - 1) * 4 + lv) / 20f * 0.6f;
                EditorUtility.DisplayProgressBar("Wiring UI", $"Level_{ch}_{lv}...", p);
                totalWired += WireLevelScene(ch, lv);
            }
        }

        EditorUtility.ClearProgressBar();
        Debug.Log($"[UIWiring] Done! Wired {totalWired} references across all scenes.");
    }

    /// <summary>
    /// CLI入口
    /// </summary>
    public static void WireAllFromCommandLine()
    {
        WireAll();
    }

    // ==================== MainMenu ====================

    private static int WireMainMenuScene()
    {
        string path = "Assets/Scenes/MainMenu.unity";
        if (!File.Exists(path)) return 0;

        EditorSceneManager.OpenScene(path);
        int wired = 0;

        var menuUI = Object.FindAnyObjectByType<MainMenuUI>();
        if (menuUI == null)
        {
            Debug.LogWarning("[UIWiring] MainMenuUI not found in MainMenu scene");
            return 0;
        }

        var so = new SerializedObject(menuUI);

        // 按钮关联
        wired += WireButton(so, "continueButton", "ContinueButton");
        wired += WireButton(so, "newGameButton", "NewGameButton");
        wired += WireButton(so, "localPlayButton", "LocalPlayButton");
        wired += WireButton(so, "onlinePlayButton", "OnlinePlayButton");
        wired += WireButton(so, "settingsButton", "SettingsButton");

        // 通关后按钮
        wired += WireButton(so, "newGamePlusButton", "NGPlusButton");
        wired += WireButton(so, "bossRushButton", "BossRushButton");
        wired += WireButton(so, "storyRecapButton", "StoryRecapButton");

        // 面板
        wired += WireGameObject(so, "mainPanel", "ButtonPanel");
        wired += WireGameObject(so, "postGameGroup", "PostGamePanel");

        // 文本
        wired += WireTMP(so, "versionText", "SubtitleText"); // 副标题可复用为版本号

        // CanvasGroup
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            var cg = canvas.GetComponent<CanvasGroup>();
            if (cg == null) cg = canvas.gameObject.AddComponent<CanvasGroup>();
            var cgProp = so.FindProperty("mainCanvasGroup");
            if (cgProp != null && cgProp.objectReferenceValue == null)
            {
                cgProp.objectReferenceValue = cg;
                wired++;
            }
        }

        so.ApplyModifiedProperties();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log($"[UIWiring] MainMenu: wired {wired} references");
        return wired;
    }

    // ==================== Level Scenes ====================

    private static int WireLevelScene(int chapter, int level)
    {
        string path = $"Assets/Scenes/Chapter{chapter}/Level_{chapter}_{level}.unity";
        if (!File.Exists(path)) return 0;

        EditorSceneManager.OpenScene(path);
        int wired = 0;

        // HUDManager
        var hud = Object.FindAnyObjectByType<HUDManager>();
        if (hud != null)
        {
            var so = new SerializedObject(hud);
            wired += WireTMP(so, "levelNameText", "LevelName");
            wired += WireTMP(so, "timerText", "Timer");
            wired += WireTMP(so, "collectibleText", "Collectibles");
            so.ApplyModifiedProperties();
        }

        // PauseMenuUI
        var pause = Object.FindAnyObjectByType<PauseMenuUI>();
        if (pause != null)
        {
            var so = new SerializedObject(pause);
            wired += WireButton(so, "resumeButton", "ResumeButton");
            wired += WireButton(so, "settingsButton", "SettingsButton");
            wired += WireButton(so, "restartButton", "RestartButton");
            wired += WireButton(so, "mainMenuButton", "QuitButton");
            wired += WireGameObject(so, "pausePanel", "PausePanel");
            so.ApplyModifiedProperties();
        }

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        return wired;
    }

    // ==================== 辅助方法 ====================

    private static int WireButton(SerializedObject so, string propName, string goName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null || prop.objectReferenceValue != null) return 0;

        var go = FindInScene(goName);
        if (go == null) return 0;

        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();

        prop.objectReferenceValue = btn;
        return 1;
    }

    private static int WireGameObject(SerializedObject so, string propName, string goName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null || prop.objectReferenceValue != null) return 0;

        var go = FindInScene(goName);
        if (go == null) return 0;

        prop.objectReferenceValue = go;
        return 1;
    }

    private static int WireTMP(SerializedObject so, string propName, string goName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null || prop.objectReferenceValue != null) return 0;

        var go = FindInScene(goName);
        if (go == null) return 0;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) return 0;

        prop.objectReferenceValue = tmp;
        return 1;
    }

    private static GameObject FindInScene(string name)
    {
        // 全场景搜索同名对象
        var allObjects = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in allObjects)
        {
            if (t.name == name)
                return t.gameObject;
        }
        return null;
    }
}
