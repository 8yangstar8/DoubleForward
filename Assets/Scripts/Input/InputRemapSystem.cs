using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// 输入重映射系统 - 支持手柄按键自定义绑定
/// 冲突检测、PlayerPrefs持久化、默认重置
/// </summary>
public class InputRemapSystem : MonoBehaviour
{
    public static InputRemapSystem Instance { get; private set; }

    /// <summary>
    /// 可重映射的动作
    /// </summary>
    public enum GameAction
    {
        Jump,
        Skill1,
        Skill2,
        Pause,
        Interact
    }

    /// <summary>
    /// 手柄按钮枚举（不依赖InputSystem的底层绑定路径）
    /// </summary>
    public enum GamepadButton
    {
        South,   // A / Cross
        East,    // B / Circle
        West,    // X / Square
        North,   // Y / Triangle
        L1,      // Left Bumper
        R1,      // Right Bumper
        L2,      // Left Trigger
        R2,      // Right Trigger
        L3,      // Left Stick Press
        R3,      // Right Stick Press
        DPadUp,
        DPadDown,
        DPadLeft,
        DPadRight,
        Start,
        Select
    }

    // 默认映射
    private static readonly Dictionary<GameAction, GamepadButton> DefaultBindings = new Dictionary<GameAction, GamepadButton>
    {
        { GameAction.Jump,     GamepadButton.South },
        { GameAction.Skill1,   GamepadButton.West },
        { GameAction.Skill2,   GamepadButton.East },
        { GameAction.Pause,    GamepadButton.Start },
        { GameAction.Interact, GamepadButton.North }
    };

    // 当前映射
    private Dictionary<GameAction, GamepadButton> currentBindings = new Dictionary<GameAction, GamepadButton>();

    // 重映射状态
    private bool isListening;
    private GameAction listeningAction;
    private System.Action<GameAction, GamepadButton> onRemapComplete;
    private System.Action onRemapCancelled;
    private float listenTimeout = 5f;
    private float listenTimer;

    public bool IsListening => isListening;
    public event System.Action<GameAction, GamepadButton> OnBindingChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadBindings();
    }

    void Update()
    {
        if (!isListening) return;

        listenTimer += Time.unscaledDeltaTime;
        if (listenTimer >= listenTimeout)
        {
            CancelRemap();
            return;
        }

        // 检测所有按钮
        if (Gamepad.current == null) return;

        foreach (GamepadButton btn in System.Enum.GetValues(typeof(GamepadButton)))
        {
            if (IsButtonPressed(btn))
            {
                // 不允许绑定到Start（保留为暂停键，除非正在绑定Pause动作）
                if (btn == GamepadButton.Start && listeningAction != GameAction.Pause)
                    continue;

                CompleteRemap(btn);
                return;
            }
        }

        // 按Select/Back取消
        if (Gamepad.current.selectButton.wasPressedThisFrame)
        {
            CancelRemap();
        }
    }

    // ============ 公共API ============

    /// <summary>
    /// 获取某动作当前绑定的按钮
    /// </summary>
    public GamepadButton GetBinding(GameAction action)
    {
        if (currentBindings.TryGetValue(action, out var btn))
            return btn;
        return DefaultBindings[action];
    }

    /// <summary>
    /// 开始监听新按钮输入（用于重映射）
    /// </summary>
    public void StartRemap(GameAction action, System.Action<GameAction, GamepadButton> onComplete = null, System.Action onCancel = null)
    {
        isListening = true;
        listeningAction = action;
        onRemapComplete = onComplete;
        onRemapCancelled = onCancel;
        listenTimer = 0f;

        Debug.Log($"[InputRemap] 正在监听 {action} 的新按键...");
    }

    /// <summary>
    /// 取消重映射
    /// </summary>
    public void CancelRemap()
    {
        isListening = false;
        onRemapCancelled?.Invoke();
        onRemapComplete = null;
        onRemapCancelled = null;

        Debug.Log("[InputRemap] 重映射已取消");
    }

    /// <summary>
    /// 重置所有绑定为默认
    /// </summary>
    public void ResetToDefaults()
    {
        currentBindings.Clear();
        foreach (var kvp in DefaultBindings)
            currentBindings[kvp.Key] = kvp.Value;

        SaveBindings();
        Debug.Log("[InputRemap] 已重置为默认绑定");
    }

    /// <summary>
    /// 检测是否有冲突（同一按钮绑定了多个动作）
    /// </summary>
    public GameAction? GetConflict(GameAction action, GamepadButton newButton)
    {
        foreach (var kvp in currentBindings)
        {
            if (kvp.Key != action && kvp.Value == newButton)
                return kvp.Key;
        }
        return null;
    }

    /// <summary>
    /// 交换两个动作的绑定（解决冲突）
    /// </summary>
    public void SwapBindings(GameAction action1, GameAction action2)
    {
        var temp = currentBindings[action1];
        currentBindings[action1] = currentBindings[action2];
        currentBindings[action2] = temp;

        SaveBindings();
        OnBindingChanged?.Invoke(action1, currentBindings[action1]);
        OnBindingChanged?.Invoke(action2, currentBindings[action2]);
    }

    /// <summary>
    /// 检查某动作的按钮是否在当前帧被按下
    /// </summary>
    public bool IsActionPressed(GameAction action)
    {
        if (Gamepad.current == null) return false;
        var btn = GetBinding(action);
        return IsButtonPressed(btn);
    }

    /// <summary>
    /// 检查某动作的按钮是否正在被按住
    /// </summary>
    public bool IsActionHeld(GameAction action)
    {
        if (Gamepad.current == null) return false;
        var btn = GetBinding(action);
        return IsButtonHeld(btn);
    }

    /// <summary>
    /// 获取按钮的显示名称
    /// </summary>
    public string GetButtonDisplayName(GamepadButton button)
    {
        var gamepadType = GamepadAdapter.Instance?.DetectedGamepadType ?? GamepadAdapter.GamepadType.Xbox;

        switch (button)
        {
            case GamepadButton.South:
                return gamepadType == GamepadAdapter.GamepadType.PlayStation ? "X" : "A";
            case GamepadButton.East:
                return gamepadType == GamepadAdapter.GamepadType.PlayStation ? "O" : "B";
            case GamepadButton.West:
                return gamepadType == GamepadAdapter.GamepadType.PlayStation ? "□" : "X";
            case GamepadButton.North:
                return gamepadType == GamepadAdapter.GamepadType.PlayStation ? "△" : "Y";
            case GamepadButton.L1: return "L1";
            case GamepadButton.R1: return "R1";
            case GamepadButton.L2: return "L2";
            case GamepadButton.R2: return "R2";
            case GamepadButton.L3: return "L3";
            case GamepadButton.R3: return "R3";
            case GamepadButton.DPadUp: return "↑";
            case GamepadButton.DPadDown: return "↓";
            case GamepadButton.DPadLeft: return "←";
            case GamepadButton.DPadRight: return "→";
            case GamepadButton.Start: return "Start";
            case GamepadButton.Select: return "Select";
            default: return button.ToString();
        }
    }

    /// <summary>
    /// 获取动作的本地化显示名称
    /// </summary>
    public string GetActionDisplayName(GameAction action)
    {
        switch (action)
        {
            case GameAction.Jump: return "跳跃";
            case GameAction.Skill1: return "技能1";
            case GameAction.Skill2: return "技能2";
            case GameAction.Pause: return "暂停";
            case GameAction.Interact: return "互动";
            default: return action.ToString();
        }
    }

    // ============ 内部方法 ============

    private void CompleteRemap(GamepadButton newButton)
    {
        // 冲突检测
        var conflict = GetConflict(listeningAction, newButton);
        if (conflict.HasValue)
        {
            // 自动交换
            var oldButton = currentBindings[listeningAction];
            currentBindings[conflict.Value] = oldButton;
            OnBindingChanged?.Invoke(conflict.Value, oldButton);
            Debug.Log($"[InputRemap] 冲突：{conflict.Value} 已交换到 {GetButtonDisplayName(oldButton)}");
        }

        currentBindings[listeningAction] = newButton;
        SaveBindings();

        isListening = false;
        onRemapComplete?.Invoke(listeningAction, newButton);
        onRemapComplete = null;
        onRemapCancelled = null;

        OnBindingChanged?.Invoke(listeningAction, newButton);
        Debug.Log($"[InputRemap] {listeningAction} → {GetButtonDisplayName(newButton)}");
    }

    private bool IsButtonPressed(GamepadButton button)
    {
        if (Gamepad.current == null) return false;
        return GetButtonControl(button)?.wasPressedThisFrame ?? false;
    }

    private bool IsButtonHeld(GamepadButton button)
    {
        if (Gamepad.current == null) return false;
        return GetButtonControl(button)?.isPressed ?? false;
    }

    private UnityEngine.InputSystem.Controls.ButtonControl GetButtonControl(GamepadButton button)
    {
        var gp = Gamepad.current;
        if (gp == null) return null;

        switch (button)
        {
            case GamepadButton.South: return gp.buttonSouth;
            case GamepadButton.East: return gp.buttonEast;
            case GamepadButton.West: return gp.buttonWest;
            case GamepadButton.North: return gp.buttonNorth;
            case GamepadButton.L1: return gp.leftShoulder;
            case GamepadButton.R1: return gp.rightShoulder;
            case GamepadButton.L2: return gp.leftTrigger;
            case GamepadButton.R2: return gp.rightTrigger;
            case GamepadButton.L3: return gp.leftStickButton;
            case GamepadButton.R3: return gp.rightStickButton;
            case GamepadButton.DPadUp: return gp.dpad.up;
            case GamepadButton.DPadDown: return gp.dpad.down;
            case GamepadButton.DPadLeft: return gp.dpad.left;
            case GamepadButton.DPadRight: return gp.dpad.right;
            case GamepadButton.Start: return gp.startButton;
            case GamepadButton.Select: return gp.selectButton;
            default: return null;
        }
    }

    // ============ 持久化 ============

    private const string BINDING_PREFIX = "InputRemap_";

    private void SaveBindings()
    {
        foreach (var kvp in currentBindings)
        {
            PlayerPrefs.SetInt(BINDING_PREFIX + kvp.Key.ToString(), (int)kvp.Value);
        }
        PlayerPrefs.Save();
    }

    private void LoadBindings()
    {
        currentBindings.Clear();

        foreach (GameAction action in System.Enum.GetValues(typeof(GameAction)))
        {
            string key = BINDING_PREFIX + action.ToString();
            if (PlayerPrefs.HasKey(key))
            {
                currentBindings[action] = (GamepadButton)PlayerPrefs.GetInt(key);
            }
            else
            {
                currentBindings[action] = DefaultBindings[action];
            }
        }
    }
}
