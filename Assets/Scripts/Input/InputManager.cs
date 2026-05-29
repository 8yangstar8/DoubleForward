using UnityEngine;
using Kbd = UnityEngine.InputSystem.Keyboard;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Player 1 Controls")]
    [SerializeField] private VirtualJoystick joystickP1;
    [SerializeField] private TouchButton jumpButtonP1;
    [SerializeField] private TouchButton skill1ButtonP1;
    [SerializeField] private TouchButton skill2ButtonP1;

    [Header("Player 2 Controls")]
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

    public enum PlayMode { SinglePlayer, LocalSplitScreen, Network }
    public PlayMode CurrentMode { get; private set; } = PlayMode.SinglePlayer;
    private bool inputEnabled = true;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
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

    public Vector2 GetMoveInput(int playerIndex)
    {
        if (!inputEnabled) return Vector2.zero;
        var kb = Kbd.current;

        if (playerIndex == 0)
        {
            Vector2 touch = joystickP1 != null ? joystickP1.Direction : Vector2.zero;
            if (touch == Vector2.zero && kb != null)
            {
                float x = 0, y = 0;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x = -1;
                else if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x = 1;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y = 1;
                else if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y = -1;
                return new Vector2(x, y);
            }
            return touch;
        }
        return joystickP2 != null ? joystickP2.Direction : Vector2.zero;
    }

    public bool GetJumpPressed(int playerIndex)
    {
        if (!inputEnabled) return false;
        var kb = Kbd.current;
        if (playerIndex == 0)
            return (jumpButtonP1 != null && jumpButtonP1.WasPressedThisFrame)
                || (kb != null && kb.spaceKey.wasPressedThisFrame);
        return jumpButtonP2 != null && jumpButtonP2.WasPressedThisFrame;
    }

    public bool GetAttackPressed(int playerIndex)
    {
        if (!inputEnabled) return false;
        var kb = Kbd.current;
        if (playerIndex == 0)
            return (attackButtonP1 != null && attackButtonP1.WasPressedThisFrame)
                || (kb != null && kb.jKey.wasPressedThisFrame);
        return attackButtonP2 != null && attackButtonP2.WasPressedThisFrame;
    }

    public bool GetAttackHeld(int playerIndex)
    {
        if (!inputEnabled) return false;
        var kb = Kbd.current;
        if (playerIndex == 0)
            return (attackButtonP1 != null && attackButtonP1.IsPressed)
                || (kb != null && kb.jKey.isPressed);
        return attackButtonP2 != null && attackButtonP2.IsPressed;
    }

    public bool GetSkill1Pressed(int playerIndex)
    {
        if (!inputEnabled) return false;
        var kb = Kbd.current;
        if (playerIndex == 0)
            return (skill1ButtonP1 != null && skill1ButtonP1.WasPressedThisFrame)
                || (kb != null && kb.qKey.wasPressedThisFrame);
        return skill1ButtonP2 != null && skill1ButtonP2.WasPressedThisFrame;
    }

    public bool GetSkill2Pressed(int playerIndex)
    {
        if (!inputEnabled) return false;
        var kb = Kbd.current;
        if (playerIndex == 0)
            return (skill2ButtonP1 != null && skill2ButtonP1.WasPressedThisFrame)
                || (kb != null && kb.eKey.wasPressedThisFrame);
        return skill2ButtonP2 != null && skill2ButtonP2.WasPressedThisFrame;
    }

    public bool GetSkill1Held(int playerIndex)
    {
        if (!inputEnabled) return false;
        var kb = Kbd.current;
        if (playerIndex == 0)
            return (skill1ButtonP1 != null && skill1ButtonP1.IsPressed)
                || (kb != null && kb.qKey.isPressed);
        return skill1ButtonP2 != null && skill1ButtonP2.IsPressed;
    }

    public bool GetInteractPressed(int playerIndex)
    {
        if (!inputEnabled) return false;
        var kb = Kbd.current;
        if (playerIndex == 0)
            return (interactButtonP1 != null && interactButtonP1.WasPressedThisFrame)
                || (kb != null && kb.fKey.wasPressedThisFrame);
        return interactButtonP2 != null && interactButtonP2.WasPressedThisFrame;
    }

    public bool GetInteractHeld(int playerIndex)
    {
        if (!inputEnabled) return false;
        var kb = Kbd.current;
        if (playerIndex == 0)
            return (interactButtonP1 != null && interactButtonP1.IsPressed)
                || (kb != null && kb.fKey.isPressed);
        return interactButtonP2 != null && interactButtonP2.IsPressed;
    }

    public bool GetDashPressed(int playerIndex)
    {
        if (!inputEnabled) return false;
        var kb = Kbd.current;
        if (playerIndex == 0)
            return (dashButtonP1 != null && dashButtonP1.WasPressedThisFrame)
                || (kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame));
        return dashButtonP2 != null && dashButtonP2.WasPressedThisFrame;
    }

    public bool GetJumpDown(int playerIndex) => GetJumpPressed(playerIndex);
    public bool GetAttackDown(int playerIndex) => GetAttackPressed(playerIndex);
    public bool GetSkill1Down(int playerIndex) => GetSkill1Pressed(playerIndex);
    public bool GetSkill2Down(int playerIndex) => GetSkill2Pressed(playerIndex);
    public bool GetInteractDown(int playerIndex) => GetInteractPressed(playerIndex);
    public bool GetDashDown(int playerIndex) => GetDashPressed(playerIndex);

    public void SetInputEnabled(bool enabled) { inputEnabled = enabled; }
    public bool IsInputEnabled => inputEnabled;
}
