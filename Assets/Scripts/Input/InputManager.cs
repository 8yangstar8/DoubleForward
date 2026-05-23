using UnityEngine;

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

    public enum PlayMode { SinglePlayer, LocalSplitScreen, Network }
    public PlayMode CurrentMode { get; private set; } = PlayMode.SinglePlayer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
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
        if (playerIndex == 0)
            return joystickP1 != null ? joystickP1.Direction : Vector2.zero;
        else
            return joystickP2 != null ? joystickP2.Direction : Vector2.zero;
    }

    public bool GetJumpPressed(int playerIndex)
    {
        if (playerIndex == 0)
            return jumpButtonP1 != null && jumpButtonP1.WasPressedThisFrame;
        else
            return jumpButtonP2 != null && jumpButtonP2.WasPressedThisFrame;
    }

    public bool GetSkill1Pressed(int playerIndex)
    {
        if (playerIndex == 0)
            return skill1ButtonP1 != null && skill1ButtonP1.WasPressedThisFrame;
        else
            return skill1ButtonP2 != null && skill1ButtonP2.WasPressedThisFrame;
    }

    public bool GetSkill2Pressed(int playerIndex)
    {
        if (playerIndex == 0)
            return skill2ButtonP1 != null && skill2ButtonP1.WasPressedThisFrame;
        else
            return skill2ButtonP2 != null && skill2ButtonP2.WasPressedThisFrame;
    }

    public bool GetSkill1Held(int playerIndex)
    {
        if (playerIndex == 0)
            return skill1ButtonP1 != null && skill1ButtonP1.IsPressed;
        else
            return skill1ButtonP2 != null && skill1ButtonP2.IsPressed;
    }
}
