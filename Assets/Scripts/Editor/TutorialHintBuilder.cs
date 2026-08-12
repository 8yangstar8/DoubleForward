using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// 教学提示接入 - 给两关合作关卡建一套可用的提示UI并布下触发区。
///
/// 背景: 项目里 HintSystem 写好了(渐进提示/淡入淡出/自动隐藏),但
/// GameInitializer 从没实例化过它,而且 ShowHint 在 hintPanel 为空时静默返回
/// —— 整套系统一直是死代码。这里把 UI 建出来并接上。
///
/// 用法: -executeMethod TutorialHintBuilder.BuildAll
/// </summary>
public static class TutorialHintBuilder
{
    private const string FontPath = "Assets/Fonts/ChineseFont_SDF.asset";
    private const string Prefix = "Hint_";

    [MenuItem("DoubleForward/Build Tutorial Hints", false, 13)]
    public static void BuildAll()
    {
        Build("Assets/Scenes/Chapter1/Level_1_2.unity", new[]
        {
            ("Coop_ShadowWall", "紫色的影墙只有 Nox 能穿过 —— 用影穿冲进去"),
            ("Coop_Plate",      "Nox 踩住压板,影墙就会消失,Lux 才跟得上"),
            ("Coop_GateSensor", "光敏机关只有 Lux 的光束能点亮"),
        });

        Build("Assets/Scenes/Chapter1/Level_1_3.unity", new[]
        {
            ("Coop3_Crate",      "这个箱子只有 Nox 的影推能挪动"),
            ("Coop3_Plate",      "把箱子推上压板,机关就会一直开着,两人都能过"),
            ("Coop3_HighSensor", "机关太高,光束平射够不到 —— Lux 先造一座光桥站上去"),
        });
    }

    private static void Build(string scenePath, (string anchor, string text)[] hints)
    {
        EditorSceneManager.OpenScene(scenePath);

        // 幂等: 先清掉上一次生成的
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name.StartsWith(Prefix)) Object.DestroyImmediate(go);

        var texts = new string[hints.Length];
        for (int i = 0; i < hints.Length; i++) texts[i] = hints[i].text;
        var hintSystem = CreateHintUI(System.IO.Path.GetFileNameWithoutExtension(scenePath), texts);

        int built = 0;
        foreach (var (anchorName, text) in hints)
        {
            var anchor = GameObject.Find(anchorName);
            if (anchor == null)
            {
                Debug.LogWarning($"[Hints] anchor '{anchorName}' not found in {scenePath}");
                continue;
            }

            // 触发区放在机关左侧,玩家走到跟前之前就能看到提示
            var zone = new GameObject($"{Prefix}{anchorName}");
            zone.transform.position = anchor.transform.position + new Vector3(-3.5f, 0.5f, 0f);
            var col = zone.AddComponent<BoxCollider2D>();
            col.size = new Vector2(3f, 4f);
            col.isTrigger = true;
            // 可重复触发: 玩家没看清可以退回去再走一次
            zone.AddComponent<LevelHintZone>().Configure(text, 5f, false);
            built++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[Hints] {System.IO.Path.GetFileNameWithoutExtension(scenePath)}: " +
            $"hint UI + {built} zones (system={hintSystem != null})");
    }

    /// <summary>建一套自带Canvas的提示UI,并把引用接到 HintSystem 上</summary>
    private static HintSystem CreateHintUI(string levelId, string[] progressiveHints)
    {
        var canvasGO = new GameObject($"{Prefix}Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // 底部提示条
        var panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGO.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 120f);
        panelRect.sizeDelta = new Vector2(1200f, 130f);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.05f, 0.04f, 0.10f, 0.82f);
        var group = panel.AddComponent<CanvasGroup>();

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(panel.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(40f, 16f);
        textRect.offsetMax = new Vector2(-40f, -16f);
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = 42f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.96f, 0.86f);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;   // 中文必须用项目里的中文字体,否则显示方框
        else Debug.LogWarning("[Hints] ChineseFont_SDF not found, hints may render as boxes");

        panel.SetActive(false);   // 初始隐藏,由 HintSystem 控制

        // 右下角"提示"按钮: 玩家卡住时主动再看一次(HintSystem在Awake里自动绑定onClick)
        var btnGO = new GameObject("HintButton");
        btnGO.transform.SetParent(canvasGO.transform, false);
        var btnRect = btnGO.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 0f);
        btnRect.anchorMax = new Vector2(1f, 0f);
        btnRect.pivot = new Vector2(1f, 0f);
        btnRect.anchoredPosition = new Vector2(-40f, 40f);
        btnRect.sizeDelta = new Vector2(160f, 70f);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.22f, 0.20f, 0.34f, 0.90f);
        var button = btnGO.AddComponent<Button>();

        var btnTextGO = new GameObject("Label");
        btnTextGO.transform.SetParent(btnGO.transform, false);
        var btnTextRect = btnTextGO.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;
        var btnTmp = btnTextGO.AddComponent<TextMeshProUGUI>();
        btnTmp.text = "提示";
        btnTmp.fontSize = 34f;
        btnTmp.alignment = TextAlignmentOptions.Center;
        btnTmp.color = new Color(1f, 0.95f, 0.80f);
        var font2 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font2 != null) btnTmp.font = font2;

        var systemGO = new GameObject($"{Prefix}System");
        var hintSystem = systemGO.AddComponent<HintSystem>();
        var so = new SerializedObject(hintSystem);
        so.FindProperty("hintPanel").objectReferenceValue = panel;
        so.FindProperty("hintText").objectReferenceValue = tmp;
        so.FindProperty("hintCanvasGroup").objectReferenceValue = group;
        so.FindProperty("hintButton").objectReferenceValue = button;
        so.FindProperty("autoHintEnabled").boolValue = false;  // 关掉90秒卡关自动提示,免得干扰测试

        // 关卡提示数据: RequestHint() 在 currentLevelHints 为空时同样静默返回,
        // 不填的话"提示"按钮按了没反应。按顺序填进渐进提示,越按越往后
        var listProp = so.FindProperty("currentLevelHints");
        listProp.arraySize = 1;
        var entry = listProp.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("hintId").stringValue = levelId;
        entry.FindPropertyRelative("stuckTimeThreshold").floatValue = 60f;
        var arrProp = entry.FindPropertyRelative("progressiveHints");
        arrProp.arraySize = progressiveHints.Length;
        for (int i = 0; i < progressiveHints.Length; i++)
            arrProp.GetArrayElementAtIndex(i).stringValue = progressiveHints[i];

        so.ApplyModifiedProperties();

        return hintSystem;
    }
}
