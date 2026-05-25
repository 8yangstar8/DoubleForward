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
        if (!inputEnabled) return Vector2.zero;

        if (playerIndex == 0)
            return joystickP1 != null ? joystickP1.Direction : Vector2.zero;
        else
            return joystickP2 != null ? joystickP2.Direction : Vector2.zero;
    }

    public bool GetJumpPressed(int playerIndex)
    {
        if (!inputEnabled) return false;

        if (playerIndex == 0)
            return jumpButtonP1 != null && jumpButtonP1.WasPressedThisFrame;
        else
            return jumpButtonP2 != null && jumpButtonP2.WasPressedThisFrame;
    }

    public bool GetSkill1Pressed(int playerIndex)
    {
        if (!inputEnabled) return false;

        if (playerIndex == 0)
            return skill1ButtonP1 != null && skill1ButtonP1.WasPressedThisFrame;
        else
            return skill1ButtonP2 != null && skill1ButtonP2.WasPressedThisFrame;
    }

    public bool GetSkill2Pressed(int playerIndex)
    {
        if (!inputEnabled) return false;

        if (playerIndex == 0)
            return skill2ButtonP1 != null && skill2ButtonP1.WasPressedThisFrame;
        else
            return skill2ButtonP2 != null && skill2ButtonP2.WasPressedThisFrame;
    }

    public bool GetSkill1Held(int playerIndex)
    {
        if (!inputEnabled) return false;

        if (playerIndex == 0)
            return skill1ButtonP1 != null && skill1ButtonP1.IsPressed;
        else
            return skill1ButtonP2 != null && skill1ButtonP2.IsPressed;
    }

    // ============ 交互按键 ============

    [Header("交互按钮")]
    [SerializeField] private TouchButton interactButtonP1;
    [SerializeField] private TouchButton interactButtonP2;

    /// <summary>
    /// 交互键是否按下（拉杆、NPC对话、宝箱等）
    /// </summary>
    public bool GetInteractPressed(int playerIndex)
    {
        if (!inputEnabled) return false;

        if (playerIndex == 0)
            return interactButtonP1 != null && interactButtonP1.WasPressedThisFrame;
        else
            return interactButtonP2 != null && interactButtonP2.WasPressedThisFrame;
    }

    /// <summary>
    /// 交互键是否持续按住（复活队友时长按）
    /// </summary>
    public bool GetInteractHeld(int playerIndex)
    {
        if (!inputEnabled) return false;

        if (playerIndex == 0)
            return interactButtonP1 != null && interactButtonP1.IsPressed;
        else
            return interactButtonP2 != null && interactButtonP2.IsPressed;
    }

    // ============ 攻击按钮 ============

    [Header("攻击按钮")]
    [SerializeField] private TouchButton attackButtonP1;
    [SerializeField] private TouchButton attackButtonP2;

    public bool GetAttackPressed(int playerIndex)
    {
        if (!inputEnabled) return false;

        if (playerIndex == 0)
            return attackButtonP1 != null && attackButtonP1.WasPressedThisFrame;
        else
            return attackButtonP2 != null && attackButtonP2.WasPressedThisFrame;
    }

    public bool GetAttackHeld(int playerIndex)
    {
        if (!inputEnabled) return false;

        if (playerIndex == 0)
            return attackButtonP1 != null && attackButtonP1.IsPressed;
        else
            return attackButtonP2 != null && attackButtonP2.IsPressed;
    }

    // ============ 冲刺按钮 ============

    [Header("冲刺按钮")]
    [SerializeField] private TouchButton dashButtonP1;
    [SerializeField] private TouchButton dashButtonP2;

    public bool GetDashPressed(int playerIndex)
    {
        if (!inputEnabled) return false;

        if (playerIndex == 0)
            return dashButtonP1 != null && dashButtonP1.WasPressedThisFrame;
        else
            return dashButtonP2 != null && dashButtonP2.WasPressedThisFrame;
    }

    // ============ *Down 别名（供TutorialFlowManager等使用） ============

    /// <summary>跳跃键按下（GetJumpPressed别名）</summary>
    public bool GetJumpDown(int playerIndex) => GetJumpPressed(playerIndex);

    /// <summary>攻击键按下（GetAttackPressed别名）</summary>
    public bool GetAttackDown(int playerIndex) => GetAttackPressed(playerIndex);

    /// <summary>技能1按下（GetSkill1Pressed别名）</summary>
    public bool GetSkill1Down(int playerIndex) => GetSkill1Pressed(playerIndex);

    /// <summary>技能2按下（GetSkill2Pressed别名）</summary>
    public bool GetSkill2Down(int playerIndex) => GetSkill2Pressed(playerIndex);

    /// <summary>交互键按下（GetInteractPressed别名）</summary>
    public bool GetInteractDown(int playerIndex) => GetInteractPressed(playerIndex);

    /// <summary>冲刺键按下（GetDashPressed别名）</summary>
    public bool GetDashDown(int playerIndex) => GetDashPressed(playerIndex);

    // ============ 全局输入控制 ============

    private bool inputEnabled = true;

    /// <summary>
    /// 全局启用/禁用输入（过场动画、电影模式时禁用）
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    public bool IsInputEnabled => inputEnabled;
}
