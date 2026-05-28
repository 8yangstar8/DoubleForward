using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;

/// <summary>
/// 触控UI工厂 - 创建移动端虚拟摇杆和按钮预制体
/// 为InputManager生成完整的双人触控界面
/// 菜单：DoubleForward / Create Touch Controls
/// </summary>
public static class InputControlsFactory
{
    private const string PREFAB_DIR = "Assets/Prefabs";
    private const string UI_DIR = "Assets/Prefabs/UI";

    // 按钮配色
    private static readonly Color COLOR_JUMP = new Color(0.3f, 0.8f, 0.4f, 0.7f);
    private static readonly Color COLOR_ATTACK = new Color(0.9f, 0.3f, 0.3f, 0.7f);
    private static readonly Color COLOR_DASH = new Color(0.3f, 0.6f, 1f, 0.7f);
    private static readonly Color COLOR_SKILL1 = new Color(1f, 0.7f, 0.2f, 0.7f);
    private static readonly Color COLOR_SKILL2 = new Color(0.7f, 0.3f, 0.9f, 0.7f);
    private static readonly Color COLOR_INTERACT = new Color(0.8f, 0.8f, 0.2f, 0.7f);
    private static readonly Color COLOR_JOYSTICK_BG = new Color(0.2f, 0.2f, 0.2f, 0.4f);
    private static readonly Color COLOR_JOYSTICK_KNOB = new Color(0.9f, 0.9f, 0.9f, 0.6f);

    [MenuItem("DoubleForward/Create Touch Controls", false, 8)]
    public static void CreateAll()
    {
        EnsureDir(UI_DIR);

        EditorUtility.DisplayProgressBar("Creating Touch Controls", "Building P1 controls...", 0.2f);
        CreateTouchControlsPrefab();

        EditorUtility.DisplayProgressBar("Creating Touch Controls", "Updating InputManager...", 0.7f);
        UpdateInputManagerPrefab();

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[InputControlsFactory] Touch controls created and wired to InputManager.");
        EditorUtility.DisplayDialog("触控UI创建完成",
            "已创建：\n" +
            "• TouchControlsCanvas 预制体\n" +
            "  - P1: 摇杆 + 6个按钮\n" +
            "  - P2: 摇杆 + 6个按钮\n" +
            "• InputManager预制体已更新关联\n\n" +
            "P2控件默认隐藏，本地合作模式下自动显示。",
            "OK");
    }

    // ==================== 主预制体 ====================

    private static void CreateTouchControlsPrefab()
    {
        string path = $"{UI_DIR}/TouchControlsCanvas.prefab";
        if (File.Exists(path)) return;

        // Canvas
        var canvasObj = new GameObject("TouchControlsCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // P1控件（左侧摇杆 + 右侧按钮）
        var p1Group = CreatePlayerControls(canvasObj.transform, "P1Controls", false);

        // P2控件（右侧摇杆 + 左侧按钮）- 默认隐藏
        var p2Group = CreatePlayerControls(canvasObj.transform, "P2Controls", true);
        p2Group.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(canvasObj, path);
        Object.DestroyImmediate(canvasObj);
        Debug.Log("[InputControlsFactory] Created TouchControlsCanvas prefab");
    }

    private static GameObject CreatePlayerControls(Transform parent, string name, bool isP2)
    {
        var group = new GameObject(name);
        group.transform.SetParent(parent, false);
        var groupRect = group.AddComponent<RectTransform>();
        groupRect.anchorMin = Vector2.zero;
        groupRect.anchorMax = Vector2.one;
        groupRect.offsetMin = Vector2.zero;
        groupRect.offsetMax = Vector2.zero;

        // ========= 摇杆 =========
        // P1: 左下角, P2: 右下角(镜像)
        float joyX = isP2 ? 1680f : 240f;
        float joyY = 200f;

        var joystickObj = CreateJoystick(group.transform, isP2 ? "JoystickP2" : "JoystickP1",
            new Vector2(joyX, joyY));

        // ========= 动作按钮 =========
        // P1: 右下角, P2: 左下角(镜像)
        float btnBaseX = isP2 ? 240f : 1680f;
        float btnBaseY = 200f;

        // 按钮布局（菱形排列）
        // 右侧：     Attack
        //         Jump    Dash
        //            Skill1
        // 上方两个技能按钮
        float spacing = 120f;

        var jumpBtn = CreateTouchButton(group.transform, isP2 ? "JumpP2" : "JumpP1",
            "JUMP", COLOR_JUMP,
            new Vector2(btnBaseX - spacing * 0.8f, btnBaseY), 80f);

        var attackBtn = CreateTouchButton(group.transform, isP2 ? "AttackP2" : "AttackP1",
            "ATK", COLOR_ATTACK,
            new Vector2(btnBaseX, btnBaseY + spacing * 0.8f), 80f);

        var dashBtn = CreateTouchButton(group.transform, isP2 ? "DashP2" : "DashP1",
            "DASH", COLOR_DASH,
            new Vector2(btnBaseX + spacing * 0.8f, btnBaseY), 75f);

        var skill1Btn = CreateTouchButton(group.transform, isP2 ? "Skill1P2" : "Skill1P1",
            "S1", COLOR_SKILL1,
            new Vector2(btnBaseX - spacing * 1.2f, btnBaseY + spacing * 1.2f), 70f);

        var skill2Btn = CreateTouchButton(group.transform, isP2 ? "Skill2P2" : "Skill2P1",
            "S2", COLOR_SKILL2,
            new Vector2(btnBaseX + spacing * 1.2f, btnBaseY + spacing * 1.2f), 70f);

        // 交互按钮（稍微靠上一点独立位置）
        var interactBtn = CreateTouchButton(group.transform, isP2 ? "InteractP2" : "InteractP1",
            "ACT", COLOR_INTERACT,
            new Vector2(btnBaseX, btnBaseY + spacing * 2.0f), 65f);

        return group;
    }

    // ==================== 摇杆 ====================

    private static GameObject CreateJoystick(Transform parent, string name, Vector2 position)
    {
        float bgSize = 240f;
        float knobSize = 100f;

        // 背景
        var bgObj = new GameObject(name);
        bgObj.transform.SetParent(parent, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.zero;
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = position;
        bgRect.sizeDelta = new Vector2(bgSize, bgSize);

        var bgImage = bgObj.AddComponent<Image>();
        bgImage.color = COLOR_JOYSTICK_BG;
        bgImage.raycastTarget = true;

        // 旋钮
        var knobObj = new GameObject("Knob");
        knobObj.transform.SetParent(bgObj.transform, false);
        var knobRect = knobObj.AddComponent<RectTransform>();
        knobRect.anchorMin = new Vector2(0.5f, 0.5f);
        knobRect.anchorMax = new Vector2(0.5f, 0.5f);
        knobRect.pivot = new Vector2(0.5f, 0.5f);
        knobRect.anchoredPosition = Vector2.zero;
        knobRect.sizeDelta = new Vector2(knobSize, knobSize);

        var knobImage = knobObj.AddComponent<Image>();
        knobImage.color = COLOR_JOYSTICK_KNOB;

        // VirtualJoystick组件
        var joystick = bgObj.AddComponent<VirtualJoystick>();
        // 通过SerializedObject设置私有字段
        var so = new SerializedObject(joystick);
        so.FindProperty("background").objectReferenceValue = bgRect;
        so.FindProperty("knob").objectReferenceValue = knobRect;
        so.FindProperty("maxRadius").floatValue = (bgSize - knobSize) / 2f;
        so.FindProperty("isDynamic").boolValue = false;
        so.ApplyModifiedProperties();

        return bgObj;
    }

    // ==================== 触控按钮 ====================

    private static GameObject CreateTouchButton(Transform parent, string name,
        string label, Color color, Vector2 position, float size)
    {
        var btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = Vector2.zero;
        btnRect.anchorMax = Vector2.zero;
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = position;
        btnRect.sizeDelta = new Vector2(size, size);

        // 背景圆形
        var bgImage = btnObj.AddComponent<Image>();
        bgImage.color = color;
        bgImage.raycastTarget = true;

        // TouchButton组件
        btnObj.AddComponent<TouchButton>();

        // 标签
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(btnObj.transform, false);

        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var text = labelObj.AddComponent<TMPro.TextMeshProUGUI>();
        text.text = label;
        text.fontSize = size * 0.35f;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontStyle = TMPro.FontStyles.Bold;
        text.raycastTarget = false;

        return btnObj;
    }

    // ==================== InputManager关联 ====================

    private static void UpdateInputManagerPrefab()
    {
        string inputMgrPath = $"{PREFAB_DIR}/Managers/InputManager.prefab";
        if (!File.Exists(inputMgrPath))
        {
            Debug.LogWarning("[InputControlsFactory] InputManager prefab not found. Run PrefabFactory first.");
            return;
        }

        string touchCanvasPath = $"{UI_DIR}/TouchControlsCanvas.prefab";
        if (!File.Exists(touchCanvasPath))
        {
            Debug.LogWarning("[InputControlsFactory] TouchControlsCanvas prefab not found.");
            return;
        }

        // 加载InputManager预制体并修改
        var inputMgrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(inputMgrPath);
        if (inputMgrPrefab == null) return;

        // 实例化以修改
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(inputMgrPrefab);

        // 如果还没有TouchControlsCanvas子物体，嵌入一个
        var existingCanvas = instance.transform.Find("TouchControlsCanvas");
        if (existingCanvas == null)
        {
            // 加载TouchControlsCanvas并作为子物体实例化
            var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(touchCanvasPath);
            if (canvasPrefab != null)
            {
                var canvasInstance = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab);
                canvasInstance.name = "TouchControlsCanvas";
                canvasInstance.transform.SetParent(instance.transform, false);

                // 关联InputManager的SerializedField
                WireInputManager(instance, canvasInstance);
            }
        }

        // 保存回预制体
        PrefabUtility.SaveAsPrefabAsset(instance, inputMgrPath);
        Object.DestroyImmediate(instance);
        Debug.Log("[InputControlsFactory] Updated InputManager prefab with touch controls");
    }

    private static void WireInputManager(GameObject inputMgrObj, GameObject canvasObj)
    {
        var inputManager = inputMgrObj.GetComponent<InputManager>();
        if (inputManager == null) return;

        var so = new SerializedObject(inputManager);

        // P1 Controls
        var p1 = canvasObj.transform.Find("P1Controls");
        if (p1 != null)
        {
            WireComponent<VirtualJoystick>(so, "joystickP1", p1, "JoystickP1");
            WireComponent<TouchButton>(so, "jumpButtonP1", p1, "JumpP1");
            WireComponent<TouchButton>(so, "attackButtonP1", p1, "AttackP1");
            WireComponent<TouchButton>(so, "dashButtonP1", p1, "DashP1");
            WireComponent<TouchButton>(so, "skill1ButtonP1", p1, "Skill1P1");
            WireComponent<TouchButton>(so, "skill2ButtonP1", p1, "Skill2P1");
            WireComponent<TouchButton>(so, "interactButtonP1", p1, "InteractP1");
        }

        // P2 Controls
        var p2 = canvasObj.transform.Find("P2Controls");
        if (p2 != null)
        {
            WireComponent<VirtualJoystick>(so, "joystickP2", p2, "JoystickP2");
            WireComponent<TouchButton>(so, "jumpButtonP2", p2, "JumpP2");
            WireComponent<TouchButton>(so, "attackButtonP2", p2, "AttackP2");
            WireComponent<TouchButton>(so, "dashButtonP2", p2, "DashP2");
            WireComponent<TouchButton>(so, "skill1ButtonP2", p2, "Skill1P2");
            WireComponent<TouchButton>(so, "skill2ButtonP2", p2, "Skill2P2");
            WireComponent<TouchButton>(so, "interactButtonP2", p2, "InteractP2");
        }

        so.ApplyModifiedProperties();
    }

    private static void WireComponent<T>(SerializedObject so, string propName,
        Transform parent, string childName) where T : Component
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;

        var child = parent.Find(childName);
        if (child == null) return;

        var component = child.GetComponent<T>();
        if (component != null)
            prop.objectReferenceValue = component;
    }

    // ==================== 辅助 ====================

    /// <summary>
    /// 检查触控UI是否已创建
    /// </summary>
    public static bool HasTouchControls()
    {
        return File.Exists($"{UI_DIR}/TouchControlsCanvas.prefab");
    }

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
