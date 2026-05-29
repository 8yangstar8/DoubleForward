using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Player 1 Controls")]
    [SerializeField] private VirtualJoystick joystickP1;
    [SerializeField] private TouchButton jumpButtonP1;
    [SerializeField] private TouchButton skill1ButtonP1;
    [SerializeField] private TouchButton skill2ButtonP1;

    [Header("Player 2 Controls (Split Screen)")]
    [SerializeField] private VirtualJoystick joystickP2;
    [SerializeField] private TouchButton jumpButtonP2;
    [SerializeField] private TouchButton skill1ButtonP2;
    [SerializeField] private TouchButton skill2ButtonP2;

    [Header("交互按钮")]
    [SerializeField] private TouchButton interactButtonP1;
    [SerializeField] private TouchButton interactButtonP2;

    [Header("攻击按钮")]
    [SerializeField] private TouchButton attackButtonP1;
    [SerializeField] private TouchButton attackButtonP2;

    [Header("冲刺按钮")]
    [SerializeField] private TouchButton dashButtonP1;
    [SerializeField] private TouchButton dashButtonP2;

    [Header("键盘设置")]
    [SerializeField] private bool enableKeyboard = true;

    public enum PlayMode { SinglePlayer, LocalSplitScreen, Network }
    public PlayMode CurrentMode { get; private set; } = PlayMode.SinglePlayer;

    private bool inputEnabled = true;
    private Keyboard kb;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        enableKeyboard = true;
    }

    void Update()
    {
        kb = Keyboard.current;
    }

    void Start()
    {
        Debug.Log($"[InputManager] Keyboard enabled: {enableKeyboard}, Keyboard.current: {(Keyboard.current != null ? "OK" : "NULL")}");
    }

    public void SetPlayMode(PlayMode mode)
    {
        CurrentMode = mode;
        bool showP2 = mode == PlayMode.LocalSplitScreen;
        if (joystickP2 != null) joystickP2.gameObject.SetActive(showP2);
        if (jumpButtonP2 != null) jumpButtonP2.gameObject.SetActive(showP2);
        if (skill1ButtonP2 != null) skill1ButtonP2.gameObject.SetActive(showP2);
        if (skill2ButtonP2 != null) skill2ButtonP2.gameObject.SetActive(showP2);
    }

    private bool KeyHeld(UnityEngine.InputSystem.Key k) => kb != null && kb[k].isPressed;
    private bool KeyPressed(UnityEngine.InputSystem.Key k) => kb != null && kb[k].wasPressedThisFrame;

    // ============ 移动 ============

    public Vector2 GetMoveInput(int playerIndex)
    {
        if (!inputEnabled) return Vector2.zero;

        if (playerIndex == 0)
        {
            Vector2 touch = joystickP1 != null ? joystickP1.Direction : Vector2.zero;
            if (enableKeyboard && touch == Vector2.zero && kb != null)
            {
                float x = 0, y = 0;
                if (KeyHeld(UnityEngine.InputSystem.Key.A) || KeyHeld(UnityEngine.InputSystem.Key.LeftArrow)) x = -1;
                else if (KeyHeld(UnityEngine.InputSystem.Key.D) || KeyHeld(UnityEngine.InputSystem.Key.RightArrow)) x = 1;
                if (KeyHeld(UnityEngine.InputSystem.Key.W) || KeyHeld(UnityEngine.InputSystem.Key.UpArrow)) y = 1;
                else if (KeyHeld(UnityEngine.InputSystem.Key.S) || KeyHeld(UnityEngine.InputSystem.Key.DownArrow)) y = -1;
                return new Vector2(x, y);
            }
            return touch;
        }
        else
        {
            Vector2 touch = joystickP2 != null ? joystickP2.Direction : Vector2.zero;
            if (enableKeyboard && touch == Vector2.zero && kb != null)
            {
                float x = 0, y = 0;
                if (KeyHeld(UnityEngine.InputSystem.Key.J)) x = -1;
                else if (KeyHeld(UnityEngine.InputSystem.Key.L)) x = 1;
                if (KeyHeld(UnityEngine.InputSystem.Key.I)) y = 1;
                else if (KeyHeld(UnityEngine.InputSystem.Key.K)) y = -1;
                return new Vector2(x, y);
            }
            return touch;
        }
    }

    // ============ 跳跃 ============

    public bool GetJumpPressed(int playerIndex)
    {
        if (!inputEnabled) return false;
        if (playerIndex == 0)
        {
            bool touch = jumpButtonP1 != null && jumpButtonP1.WasPressedThisFrame;
            bool key = enableKeyboard && (KeyPressed(UnityEngine.InputSystem.Key.Space) || KeyPressed(UnityEngine.InputSystem.Key.W) || KeyPressed(UnityEngine.InputSystem.Key.UpArrow));
            return touch || key;
        }
        else
        {
            bool touch = jumpButtonP2 != null && jumpButtonP2.WasPressedThisFrame;
            bool key = enableKeyboard && KeyPressed(UnityEngine.InputSystem.Key.I);
            return touch || key;
        }
    }

    // ============ 攻击 ============

    public bool GetAttackPressed(int playerIndex)
    {
        if (!inputEnabled) return false;
        if (playerIndex == 0)
        {
            bool touch = attackButtonP1 != null && attackButtonP1.WasPressedThisFrame;
            bool key = enableKeyboard && KeyPressed(UnityEngine.InputSystem.Key.J);
            bool mouse = enableKeyboard && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            return touch || key || mouse;
        }
        else
        {
            bool touch = attackButtonP2 != null && attackButtonP2.WasPressedThisFrame;
            bool key = enableKeyboard && KeyPressed(UnityEngine.InputSystem.Key.Numpad1);
            return touch || key;
        }
    }

    public bool GetAttackHeld(int playerIndex)
    {
        if (!inputEnabled) return false;
        if (playerIndex == 0)
        {
            bool touch = attackButtonP1 != null && attackButtonP1.IsPressed;
            bool key = enableKeyboard && KeyHeld(UnityEngine.InputSystem.Key.J);
            return touch || key;
        }
        else
        {
            bool touch = attackButtonP2 != null && attackButtonP2.IsPressed;
            bool key = enableKeyboard && KeyHeld(UnityEngine.InputSystem.Key.Numpad1);
            return touch || key;
        }
    }

    // ============ 技能 ============

    public bool GetSkill1Pressed(int playerIndex)
    {
        if (!inputEnabled) return false;
        if (playerIndex == 0)
        {
            bool touch = skill1ButtonP1 != null && skill1ButtonP1.WasPressedThisFrame;
            bool key = enableKeyboard && KeyPressed(UnityEngine.InputSystem.Key.Q);
            return touch || key;
        }
        else
        {
            bool touch = skill1ButtonP2 != null && skill1ButtonP2.WasPressedThisFrame;
            bool key = enableKeyboard && KeyPressed(UnityEngine.InputSystem.Key.Numpad4);
            return touch || key;
        }
    }

    public bool GetSkill2Pressed(int playerIndex)
    {
        if (!inputEnabled) return false;
        if (playerIndex == 0)
        {
            bool touch = skill2ButtonP1 != null && skill2ButtonP1.WasPressedThisFrame;
            bool key = enableKeyboard && KeyPressed(UnityEngine.InputSystem.Key.E);
            return touch || key;
        }
        else
        {
            bool touch = skill2ButtonP2 != null && skill2ButtonP2.WasPressedThisFrame;
            bool key = enableKeyboard && KeyPressed(UnityEngine.InputSystem.Key.Numpad6);
            return touch || key;
        }
    }

    public bool GetSkill1Held(int playerIndex)
    {
        if (!inputEnabled) return false;
        if (playerIndex == 0)
        {
            bool touch = skill1ButtonP1 != null && skill1ButtonP1.IsPressed;
            bool key = enableKeyboard && KeyHeld(UnityEngine.InputSystem.Key.Q);
            return touch || key;
        }
        else
        {
            bool touch = skill1ButtonP2 != null && skill1ButtonP2.IsPressed;
            bool key = enableKeyboard && KeyHeld(UnityEngine.InputSystem.Key.Numpad4);
            return touch || key;
        }
    }

    // ============ 交互 ============

    public bool GetInteractPressed(int playerIndex)
    {
        if (!inputEnabled) return false;
        if (playerIndex == 0)
        {
            bool touch = interactButtonP1 != null && interactButtonP1.WasPressedThisFrame;
            bool key = enableKeyboard && KeyPressed(UnityEngine.InputSystem.Key.F);
            return touch || key;
        }
        else
        {
            bool touch = interactButtonP2 != null && interactButtonP2.WasPressedThisFrame;
            bool key = enableKeyboard && KeyPressed(UnityEngine.InputSystem.Key.Numpad5);
            return touch || key;
        }
    }

    public bool GetInteractHeld(int playerIndex)
    {
        if (!inputEnabled) return false;
        if (playerIndex == 0)
        {
            bool touch = interactButtonP1 != null && interactButtonP1.IsPressed;
            bool key = enableKeyboard && KeyHeld(UnityEngine.InputSystem.Key.F);
            return touch || key;
        }
        else
        {
            bool touch = interactButtonP2 != null && interactButtonP2.IsPressed;
            bool key = enableKeyboard && KeyHeld(UnityEngine.InputSystem.Key.Numpad5);
            return touch || key;
        }
    }

    // ============ 冲刺 ============

    public bool GetDashPressed(int playerIndex)
    {
        if (!inputEnabled) return false;
        if (playerIndex == 0)
        {
            bool touch = dashButtonP1 != null && dashButtonP1.WasPressedThisFrame;
            bool key = enableKeyboard && (KeyPressed(UnityEngine.InputSystem.Key.LeftShift) || KeyPressed(UnityEngine.InputSystem.Key.K));
            return touch || key;
        }
        else
        {
            bool touch = dashButtonP2 != null && dashButtonP2.WasPressedThisFrame;
            bool key = enableKeyboard && KeyPressed(UnityEngine.InputSystem.Key.Numpad2);
            return touch || key;
        }
    }

    // ============ *Down 别名 ============

    public bool GetJumpDown(int playerIndex) => GetJumpPressed(playerIndex);
    public bool GetAttackDown(int playerIndex) => GetAttackPressed(playerIndex);
    public bool GetSkill1Down(int playerIndex) => GetSkill1Pressed(playerIndex);
    public bool GetSkill2Down(int playerIndex) => GetSkill2Pressed(playerIndex);
    public bool GetInteractDown(int playerIndex) => GetInteractPressed(playerIndex);
    public bool GetDashDown(int playerIndex) => GetDashPressed(playerIndex);

    // ============ 全局输入控制 ============

    public void SetInputEnabled(bool enabled) { inputEnabled = enabled; }
    public bool IsInputEnabled => inputEnabled;
}
