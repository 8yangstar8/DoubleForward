using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 输入重映射系统 - 允许玩家自定义按键/手柄绑定
/// 支持键盘+手柄双端，持久化到PlayerPrefs
/// </summary>
public class InputRemapper : MonoBehaviour
{
    public static InputRemapper Instance { get; private set; }

    public enum GameAction
    {
        MoveLeft,
        MoveRight,
        Jump,
        Dash,
        Skill1,
        Skill2,
        Interact,
        Pause,
        CoopAbility
    }

    [System.Serializable]
    public class KeyBinding
    {
        public GameAction action;
        public KeyCode primaryKey;
        public KeyCode secondaryKey;       // 备用键
        public string gamepadButton;        // 手柄按钮名 (Input Manager axis name)

        public KeyBinding(GameAction action, KeyCode primary, KeyCode secondary = KeyCode.None, string gamepad = "")
        {
            this.action = action;
            this.primaryKey = primary;
            this.secondaryKey = secondary;
            this.gamepadButton = gamepad;
        }
    }

    [Header("玩家1默认绑定")]
    [SerializeField]
    private List<KeyBinding> player1Defaults = new List<KeyBinding>
    {
        new KeyBinding(GameAction.MoveLeft, KeyCode.A, KeyCode.LeftArrow),
        new KeyBinding(GameAction.MoveRight, KeyCode.D, KeyCode.RightArrow),
        new KeyBinding(GameAction.Jump, KeyCode.W, KeyCode.UpArrow, "joystick button 0"),
        new KeyBinding(GameAction.Dash, KeyCode.LeftShift, KeyCode.None, "joystick button 1"),
        new KeyBinding(GameAction.Skill1, KeyCode.Q, KeyCode.None, "joystick button 2"),
        new KeyBinding(GameAction.Skill2, KeyCode.E, KeyCode.None, "joystick button 3"),
        new KeyBinding(GameAction.Interact, KeyCode.F, KeyCode.None, "joystick button 4"),
        new KeyBinding(GameAction.Pause, KeyCode.Escape, KeyCode.P, "joystick button 7"),
        new KeyBinding(GameAction.CoopAbility, KeyCode.R, KeyCode.None, "joystick button 5")
    };

    [Header("玩家2默认绑定")]
    [SerializeField]
    private List<KeyBinding> player2Defaults = new List<KeyBinding>
    {
        new KeyBinding(GameAction.MoveLeft, KeyCode.J),
        new KeyBinding(GameAction.MoveRight, KeyCode.L),
        new KeyBinding(GameAction.Jump, KeyCode.I, KeyCode.None, "joystick 2 button 0"),
        new KeyBinding(GameAction.Dash, KeyCode.RightShift, KeyCode.None, "joystick 2 button 1"),
        new KeyBinding(GameAction.Skill1, KeyCode.U, KeyCode.None, "joystick 2 button 2"),
        new KeyBinding(GameAction.Skill2, KeyCode.O, KeyCode.None, "joystick 2 button 3"),
        new KeyBinding(GameAction.Interact, KeyCode.H, KeyCode.None, "joystick 2 button 4"),
        new KeyBinding(GameAction.Pause, KeyCode.Escape),
        new KeyBinding(GameAction.CoopAbility, KeyCode.Y, KeyCode.None, "joystick 2 button 5")
    };

    private Dictionary<int, Dictionary<GameAction, KeyBinding>> playerBindings
        = new Dictionary<int, Dictionary<GameAction, KeyBinding>>();

    private bool isListeningForKey;
    private GameAction listeningAction;
    private int listeningPlayer;
    private bool listeningForPrimary;

    private const string PREFS_PREFIX = "keybind_";

    public bool IsListeningForKey => isListeningForKey;
    public event System.Action<int, GameAction, KeyCode> OnKeyRebound;
    public event System.Action OnBindingsReset;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeBindings();
        LoadBindings();
    }

    private void InitializeBindings()
    {
        // 玩家1
        var p1Dict = new Dictionary<GameAction, KeyBinding>();
        foreach (var binding in player1Defaults)
            p1Dict[binding.action] = new KeyBinding(binding.action, binding.primaryKey,
                binding.secondaryKey, binding.gamepadButton);
        playerBindings[0] = p1Dict;

        // 玩家2
        var p2Dict = new Dictionary<GameAction, KeyBinding>();
        foreach (var binding in player2Defaults)
            p2Dict[binding.action] = new KeyBinding(binding.action, binding.primaryKey,
                binding.secondaryKey, binding.gamepadButton);
        playerBindings[1] = p2Dict;
    }

    void Update()
    {
        if (isListeningForKey)
        {
            ListenForKeyPress();
        }
    }

    // ============ 查询绑定 ============

    /// <summary>
    /// 检查动作是否被按下
    /// </summary>
    public bool GetActionDown(int playerIndex, GameAction action)
    {
        var binding = GetBinding(playerIndex, action);
        if (binding == null) return false;

        if (Input.GetKeyDown(binding.primaryKey)) return true;
        if (binding.secondaryKey != KeyCode.None && Input.GetKeyDown(binding.secondaryKey)) return true;

        return false;
    }

    /// <summary>
    /// 检查动作是否被持续按住
    /// </summary>
    public bool GetActionHeld(int playerIndex, GameAction action)
    {
        var binding = GetBinding(playerIndex, action);
        if (binding == null) return false;

        if (Input.GetKey(binding.primaryKey)) return true;
        if (binding.secondaryKey != KeyCode.None && Input.GetKey(binding.secondaryKey)) return true;

        return false;
    }

    /// <summary>
    /// 检查动作是否被释放
    /// </summary>
    public bool GetActionUp(int playerIndex, GameAction action)
    {
        var binding = GetBinding(playerIndex, action);
        if (binding == null) return false;

        if (Input.GetKeyUp(binding.primaryKey)) return true;
        if (binding.secondaryKey != KeyCode.None && Input.GetKeyUp(binding.secondaryKey)) return true;

        return false;
    }

    /// <summary>
    /// 获取绑定
    /// </summary>
    public KeyBinding GetBinding(int playerIndex, GameAction action)
    {
        if (playerBindings.ContainsKey(playerIndex) &&
            playerBindings[playerIndex].ContainsKey(action))
            return playerBindings[playerIndex][action];
        return null;
    }

    /// <summary>
    /// 获取按键显示名称
    /// </summary>
    public string GetKeyDisplayName(int playerIndex, GameAction action, bool primary = true)
    {
        var binding = GetBinding(playerIndex, action);
        if (binding == null) return "---";

        KeyCode key = primary ? binding.primaryKey : binding.secondaryKey;
        if (key == KeyCode.None) return "---";

        return FormatKeyName(key);
    }

    // ============ 重新绑定 ============

    /// <summary>
    /// 开始监听按键（UI调用后等待玩家按键）
    /// </summary>
    public void StartListening(int playerIndex, GameAction action, bool isPrimary = true)
    {
        isListeningForKey = true;
        listeningPlayer = playerIndex;
        listeningAction = action;
        listeningForPrimary = isPrimary;
    }

    /// <summary>
    /// 取消监听
    /// </summary>
    public void CancelListening()
    {
        isListeningForKey = false;
    }

    /// <summary>
    /// 直接设置绑定
    /// </summary>
    public void SetBinding(int playerIndex, GameAction action, KeyCode key, bool isPrimary = true)
    {
        if (!playerBindings.ContainsKey(playerIndex)) return;

        if (playerBindings[playerIndex].ContainsKey(action))
        {
            if (isPrimary)
                playerBindings[playerIndex][action].primaryKey = key;
            else
                playerBindings[playerIndex][action].secondaryKey = key;
        }

        SaveBindings();
        OnKeyRebound?.Invoke(playerIndex, action, key);
    }

    /// <summary>
    /// 恢复默认绑定
    /// </summary>
    public void ResetToDefaults(int playerIndex = -1)
    {
        if (playerIndex < 0)
        {
            // 重置所有
            InitializeBindings();
        }
        else
        {
            var defaults = playerIndex == 0 ? player1Defaults : player2Defaults;
            var dict = new Dictionary<GameAction, KeyBinding>();
            foreach (var binding in defaults)
                dict[binding.action] = new KeyBinding(binding.action, binding.primaryKey,
                    binding.secondaryKey, binding.gamepadButton);
            playerBindings[playerIndex] = dict;
        }

        ClearSavedBindings();
        OnBindingsReset?.Invoke();
    }

    // ============ 内部方法 ============

    private void ListenForKeyPress()
    {
        // 遍历所有KeyCode检测按下
        foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
        {
            // 跳过鼠标和不可绑定的键
            if (key == KeyCode.None) continue;
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) continue;

            // Escape取消
            if (key == KeyCode.Escape)
            {
                CancelListening();
                return;
            }

            if (Input.GetKeyDown(key))
            {
                // 检查冲突
                CheckAndResolveConflict(listeningPlayer, listeningAction, key);

                SetBinding(listeningPlayer, listeningAction, key, listeningForPrimary);
                isListeningForKey = false;
                return;
            }
        }
    }

    private void CheckAndResolveConflict(int playerIndex, GameAction newAction, KeyCode key)
    {
        if (!playerBindings.ContainsKey(playerIndex)) return;

        foreach (var kvp in playerBindings[playerIndex])
        {
            if (kvp.Key == newAction) continue;

            if (kvp.Value.primaryKey == key)
                kvp.Value.primaryKey = KeyCode.None;

            if (kvp.Value.secondaryKey == key)
                kvp.Value.secondaryKey = KeyCode.None;
        }
    }

    private string FormatKeyName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftShift: return "L.Shift";
            case KeyCode.RightShift: return "R.Shift";
            case KeyCode.LeftControl: return "L.Ctrl";
            case KeyCode.RightControl: return "R.Ctrl";
            case KeyCode.LeftAlt: return "L.Alt";
            case KeyCode.RightAlt: return "R.Alt";
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
            case KeyCode.Space: return "Space";
            case KeyCode.Return: return "Enter";
            case KeyCode.Escape: return "Esc";
            default: return key.ToString();
        }
    }

    // ============ 持久化 ============

    private void SaveBindings()
    {
        foreach (var player in playerBindings)
        {
            foreach (var binding in player.Value)
            {
                string key = $"{PREFS_PREFIX}p{player.Key}_{binding.Key}";
                PlayerPrefs.SetInt(key + "_primary", (int)binding.Value.primaryKey);
                PlayerPrefs.SetInt(key + "_secondary", (int)binding.Value.secondaryKey);
            }
        }
        PlayerPrefs.Save();
    }

    private void LoadBindings()
    {
        foreach (var player in playerBindings)
        {
            foreach (var binding in player.Value)
            {
                string key = $"{PREFS_PREFIX}p{player.Key}_{binding.Key}";

                if (PlayerPrefs.HasKey(key + "_primary"))
                {
                    binding.Value.primaryKey = (KeyCode)PlayerPrefs.GetInt(key + "_primary");
                    binding.Value.secondaryKey = (KeyCode)PlayerPrefs.GetInt(key + "_secondary");
                }
            }
        }
    }

    private void ClearSavedBindings()
    {
        foreach (var player in playerBindings)
        {
            foreach (var binding in player.Value)
            {
                string key = $"{PREFS_PREFIX}p{player.Key}_{binding.Key}";
                PlayerPrefs.DeleteKey(key + "_primary");
                PlayerPrefs.DeleteKey(key + "_secondary");
            }
        }
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 获取所有动作列表（用于UI显示）
    /// </summary>
    public GameAction[] GetAllActions()
    {
        return (GameAction[])System.Enum.GetValues(typeof(GameAction));
    }

    /// <summary>
    /// 获取动作本地化名称
    /// </summary>
    public string GetActionDisplayName(GameAction action)
    {
        string key = $"input_{action.ToString().ToLower()}";
        if (LocalizationSystem.Instance != null)
            return LocalizationSystem.Instance.Get(key, action.ToString());

        switch (action)
        {
            case GameAction.MoveLeft: return "Move Left";
            case GameAction.MoveRight: return "Move Right";
            case GameAction.Jump: return "Jump";
            case GameAction.Dash: return "Dash";
            case GameAction.Skill1: return "Skill 1";
            case GameAction.Skill2: return "Skill 2";
            case GameAction.Interact: return "Interact";
            case GameAction.Pause: return "Pause";
            case GameAction.CoopAbility: return "Co-op Ability";
            default: return action.ToString();
        }
    }
}
