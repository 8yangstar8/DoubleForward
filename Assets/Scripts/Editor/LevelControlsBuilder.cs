using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.IO;

/// <summary>
/// 屏幕操作按钮 - 给关卡加上可见、可点、标注了按键的攻击/跳跃按钮。
///
/// 背景: 项目里本来有 TouchControlsCanvas 预制体(含 AttackP1/JumpP1 等),
/// 但它嵌在 InputManager 预制体里,运行时根本没进场景 —— 实测场景里只有
/// PauseCanvas / HUDCanvas / LevelCompleteCanvas 三个 Canvas。
/// 玩家因此完全不知道怎么攻击。
///
/// 而且就算把它显示出来也没用: PlayerController.HandleInput 检测到键盘就
/// 直接 return,触屏分支读不到,PC上点了没反应。所以这里的按钮走
/// PlayerActionButton 直接调用玩家方法,手机和PC都有效。
///
/// 用法: -executeMethod LevelControlsBuilder.BuildAll
/// </summary>
public static class LevelControlsBuilder
{
    private const string FontPath = "Assets/Fonts/ChineseFont_SDF.asset";
    private const string RootName = "ControlsCanvas";

    private static readonly Color LuxTint = new Color(0.95f, 0.72f, 0.20f, 0.85f);
    private static readonly Color NoxTint = new Color(0.48f, 0.28f, 0.72f, 0.85f);

    [MenuItem("DoubleForward/Build On-Screen Controls", false, 17)]
    public static void BuildAll()
    {
        int done = 0;
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            BuildInOpenScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            done++;
        }
        Debug.Log($"[Controls] on-screen attack/jump buttons added to {done} scenes");
    }

    private static void BuildInOpenScene()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        var canvasGO = new GameObject(RootName);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;              // 在HUD之上,暂停面板(100)之下
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // P1 / Lux —— 右下角
        MakeButton(canvasGO.transform, font, "Btn_Lux_Attack", "攻击\nJ",
            new Vector2(1f, 0f), new Vector2(-140f, 130f), LuxTint,
            0, PlayerActionButton.Action.Attack);
        MakeButton(canvasGO.transform, font, "Btn_Lux_Jump", "跳跃\n空格",
            new Vector2(1f, 0f), new Vector2(-300f, 130f), LuxTint,
            0, PlayerActionButton.Action.Jump);

        // P2 / Nox —— 左下角
        MakeButton(canvasGO.transform, font, "Btn_Nox_Attack", "攻击\n右Ctrl",
            new Vector2(0f, 0f), new Vector2(300f, 130f), NoxTint,
            1, PlayerActionButton.Action.Attack);
        MakeButton(canvasGO.transform, font, "Btn_Nox_Jump", "跳跃\n右Shift",
            new Vector2(0f, 0f), new Vector2(140f, 130f), NoxTint,
            1, PlayerActionButton.Action.Jump);

        // 底部一行按键说明
        MakeLegend(canvasGO.transform, font);
    }

    private static void MakeButton(Transform parent, TMP_FontAsset font, string name, string label,
        Vector2 anchor, Vector2 offset, Color tint, int playerIndex, PlayerActionButton.Action action)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(140f, 140f);

        var img = go.AddComponent<Image>();
        img.color = tint;

        go.AddComponent<Button>();
        go.AddComponent<PlayerActionButton>().Configure(playerIndex, action);

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 30f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 1f, 0.95f);
        tmp.raycastTarget = false;                 // 别挡住按钮自己的点击
        if (font != null) tmp.font = font;
    }

    private static void MakeLegend(Transform parent, TMP_FontAsset font)
    {
        var go = new GameObject("ControlLegend");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 24f);
        rt.sizeDelta = new Vector2(1500f, 46f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "Lux(金): WASD 移动 · 空格 跳 · J 攻击        Nox(紫): 方向键 移动 · 右Shift 跳 · 右Ctrl 攻击";
        tmp.fontSize = 28f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.93f, 0.80f, 0.9f);
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
    }
}
