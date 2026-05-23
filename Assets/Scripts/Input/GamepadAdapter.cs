using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// 手柄适配器 - 管理手柄连接/断开、按键映射、振动反馈
/// 支持多手柄本地双人合作
/// </summary>
public class GamepadAdapter : MonoBehaviour
{
    public static GamepadAdapter Instance { get; private set; }

    [Header("手柄设置")]
    [SerializeField] private bool enableVibration = true;
    [SerializeField] private float vibrationIntensity = 0.5f;

    [Header("UI提示图标")]
    [SerializeField] private Sprite xboxButtonA;
    [SerializeField] private Sprite xboxButtonB;
    [SerializeField] private Sprite xboxButtonX;
    [SerializeField] private Sprite xboxButtonY;
    [SerializeField] private Sprite psButtonCross;
    [SerializeField] private Sprite psButtonCircle;
    [SerializeField] private Sprite psButtonSquare;
    [SerializeField] private Sprite psButtonTriangle;

    public enum GamepadType
    {
        Unknown,
        Xbox,
        PlayStation,
        Switch,
        Generic
    }

    public enum ControlScheme
    {
        Touch,
        Gamepad
    }

    private Dictionary<int, Gamepad> playerGamepads = new Dictionary<int, Gamepad>();
    private ControlScheme currentScheme = ControlScheme.Touch;
    private GamepadType detectedType = GamepadType.Unknown;

    public ControlScheme CurrentScheme => currentScheme;
    public GamepadType DetectedGamepadType => detectedType;
    public int ConnectedGamepadCount => Gamepad.all.Count;

    public event System.Action<ControlScheme> OnControlSchemeChanged;
    public event System.Action<Gamepad> OnGamepadConnected;
    public event System.Action<Gamepad> OnGamepadDisconnected;

    private const string VIBRATION_KEY = "gamepad_vibration";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        enableVibration = PlayerPrefs.GetInt(VIBRATION_KEY, 1) == 1;

        InputSystem.onDeviceChange += OnDeviceChange;
        DetectInitialGamepads();
    }

    void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    void Update()
    {
        DetectControlSchemeChange();
    }

    private void DetectInitialGamepads()
    {
        foreach (var gamepad in Gamepad.all)
        {
            DetectGamepadType(gamepad);
        }

        // 自动分配手柄
        AutoAssignGamepads();

        // 如果有手柄连接，切换控制方案
        if (Gamepad.all.Count > 0)
            SetControlScheme(ControlScheme.Gamepad);
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is Gamepad gamepad)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                    DetectGamepadType(gamepad);
                    AutoAssignGamepads();
                    SetControlScheme(ControlScheme.Gamepad);
                    OnGamepadConnected?.Invoke(gamepad);
                    Debug.Log($"[GamepadAdapter] 手柄已连接: {gamepad.displayName}");
                    break;

                case InputDeviceChange.Removed:
                    RemoveGamepadAssignment(gamepad);
                    OnGamepadDisconnected?.Invoke(gamepad);
                    if (Gamepad.all.Count == 0)
                        SetControlScheme(ControlScheme.Touch);
                    Debug.Log($"[GamepadAdapter] 手柄已断开: {gamepad.displayName}");
                    break;
            }
        }
    }

    private void DetectGamepadType(Gamepad gamepad)
    {
        string name = gamepad.displayName.ToLower();

        if (name.Contains("xbox") || name.Contains("xinput"))
            detectedType = GamepadType.Xbox;
        else if (name.Contains("dualshock") || name.Contains("dualsense") || name.Contains("playstation"))
            detectedType = GamepadType.PlayStation;
        else if (name.Contains("switch") || name.Contains("joy-con") || name.Contains("pro controller"))
            detectedType = GamepadType.Switch;
        else
            detectedType = GamepadType.Generic;
    }

    private void DetectControlSchemeChange()
    {
        // 检测触摸输入
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            if (currentScheme != ControlScheme.Touch)
                SetControlScheme(ControlScheme.Touch);
        }

        // 检测手柄输入
        if (Gamepad.current != null)
        {
            bool anyButton = Gamepad.current.buttonSouth.isPressed ||
                             Gamepad.current.buttonNorth.isPressed ||
                             Gamepad.current.buttonEast.isPressed ||
                             Gamepad.current.buttonWest.isPressed ||
                             Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.1f;

            if (anyButton && currentScheme != ControlScheme.Gamepad)
                SetControlScheme(ControlScheme.Gamepad);
        }
    }

    private void SetControlScheme(ControlScheme scheme)
    {
        if (currentScheme == scheme) return;
        currentScheme = scheme;
        OnControlSchemeChanged?.Invoke(scheme);
    }

    /// <summary>
    /// 自动分配手柄给玩家
    /// </summary>
    private void AutoAssignGamepads()
    {
        playerGamepads.Clear();
        int playerIndex = 0;

        foreach (var gamepad in Gamepad.all)
        {
            playerGamepads[playerIndex] = gamepad;
            playerIndex++;
            if (playerIndex >= 2) break; // 最多2个玩家
        }
    }

    private void RemoveGamepadAssignment(Gamepad gamepad)
    {
        int removeKey = -1;
        foreach (var kvp in playerGamepads)
        {
            if (kvp.Value == gamepad)
            {
                removeKey = kvp.Key;
                break;
            }
        }
        if (removeKey >= 0) playerGamepads.Remove(removeKey);
    }

    /// <summary>
    /// 获取指定玩家的手柄
    /// </summary>
    public Gamepad GetPlayerGamepad(int playerIndex)
    {
        if (playerGamepads.TryGetValue(playerIndex, out Gamepad pad))
            return pad;
        return null;
    }

    /// <summary>
    /// 手柄振动
    /// </summary>
    public void Vibrate(int playerIndex, float lowFreq, float highFreq, float duration)
    {
        if (!enableVibration) return;

        var gamepad = GetPlayerGamepad(playerIndex);
        if (gamepad == null) return;

        gamepad.SetMotorSpeeds(lowFreq * vibrationIntensity, highFreq * vibrationIntensity);

        // 延迟停止振动
        StartCoroutine(StopVibrationAfter(gamepad, duration));
    }

    /// <summary>
    /// 轻振动（如收集物品）
    /// </summary>
    public void VibrateLight(int playerIndex)
    {
        Vibrate(playerIndex, 0.1f, 0.2f, 0.1f);
    }

    /// <summary>
    /// 中振动（如受伤）
    /// </summary>
    public void VibrateMedium(int playerIndex)
    {
        Vibrate(playerIndex, 0.3f, 0.5f, 0.2f);
    }

    /// <summary>
    /// 强振动（如Boss攻击）
    /// </summary>
    public void VibrateHeavy(int playerIndex)
    {
        Vibrate(playerIndex, 0.7f, 1f, 0.4f);
    }

    private System.Collections.IEnumerator StopVibrationAfter(Gamepad gamepad, float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        if (gamepad != null)
            gamepad.SetMotorSpeeds(0, 0);
    }

    /// <summary>
    /// 获取当前手柄类型对应的按键图标
    /// </summary>
    public Sprite GetConfirmButtonIcon()
    {
        switch (detectedType)
        {
            case GamepadType.PlayStation: return psButtonCross;
            default: return xboxButtonA;
        }
    }

    public Sprite GetCancelButtonIcon()
    {
        switch (detectedType)
        {
            case GamepadType.PlayStation: return psButtonCircle;
            default: return xboxButtonB;
        }
    }

    public void SetVibrationEnabled(bool enabled)
    {
        enableVibration = enabled;
        PlayerPrefs.SetInt(VIBRATION_KEY, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetVibrationIntensity(float intensity)
    {
        vibrationIntensity = Mathf.Clamp01(intensity);
    }
}
