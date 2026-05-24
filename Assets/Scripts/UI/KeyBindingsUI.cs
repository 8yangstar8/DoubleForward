using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 按键绑定设置UI - 显示和修改输入映射
/// 支持监听模式、冲突检测、重置默认
/// </summary>
public class KeyBindingsUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform bindingListContainer;
    [SerializeField] private GameObject bindingRowPrefab;

    [Header("玩家切换")]
    [SerializeField] private Button player1Tab;
    [SerializeField] private Button player2Tab;
    [SerializeField] private Image player1TabBg;
    [SerializeField] private Image player2TabBg;
    [SerializeField] private Color activeTabColor = Color.white;
    [SerializeField] private Color inactiveTabColor = new Color(0.6f, 0.6f, 0.6f);

    [Header("按钮")]
    [SerializeField] private Button resetButton;
    [SerializeField] private Button backButton;

    [Header("监听提示")]
    [SerializeField] private GameObject listeningOverlay;
    [SerializeField] private TextMeshProUGUI listeningText;

    private int currentPlayer = 0;
    private List<BindingRowUI> bindingRows = new List<BindingRowUI>();

    private class BindingRowUI
    {
        public GameObject gameObject;
        public TextMeshProUGUI actionName;
        public Button primaryButton;
        public TextMeshProUGUI primaryText;
        public Button secondaryButton;
        public TextMeshProUGUI secondaryText;
        public InputRemapper.GameAction action;
    }

    void Awake()
    {
        if (player1Tab != null) player1Tab.onClick.AddListener(() => SelectPlayer(0));
        if (player2Tab != null) player2Tab.onClick.AddListener(() => SelectPlayer(1));
        if (resetButton != null) resetButton.onClick.AddListener(OnReset);
        if (backButton != null) backButton.onClick.AddListener(Hide);

        if (listeningOverlay != null) listeningOverlay.SetActive(false);
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>
    /// 显示绑定UI
    /// </summary>
    public void Show()
    {
        if (panel != null) panel.SetActive(true);
        SelectPlayer(0);
    }

    /// <summary>
    /// 隐藏绑定UI
    /// </summary>
    public void Hide()
    {
        if (InputRemapper.Instance != null && InputRemapper.Instance.IsListeningForKey)
            InputRemapper.Instance.CancelListening();

        if (listeningOverlay != null) listeningOverlay.SetActive(false);
        if (panel != null) panel.SetActive(false);
    }

    private void SelectPlayer(int playerIndex)
    {
        currentPlayer = playerIndex;

        // 更新选项卡视觉
        if (player1TabBg != null) player1TabBg.color = playerIndex == 0 ? activeTabColor : inactiveTabColor;
        if (player2TabBg != null) player2TabBg.color = playerIndex == 1 ? activeTabColor : inactiveTabColor;

        RefreshBindingList();
    }

    private void RefreshBindingList()
    {
        // 清理旧行
        foreach (var row in bindingRows)
        {
            if (row.gameObject != null)
                Destroy(row.gameObject);
        }
        bindingRows.Clear();

        if (InputRemapper.Instance == null || bindingRowPrefab == null || bindingListContainer == null) return;

        var actions = InputRemapper.Instance.GetAllActions();
        foreach (var action in actions)
        {
            var rowObj = Instantiate(bindingRowPrefab, bindingListContainer);
            var row = new BindingRowUI
            {
                gameObject = rowObj,
                action = action
            };

            // 查找子组件
            var texts = rowObj.GetComponentsInChildren<TextMeshProUGUI>();
            var buttons = rowObj.GetComponentsInChildren<Button>();

            if (texts.Length >= 3)
            {
                row.actionName = texts[0];
                row.primaryText = texts[1];
                row.secondaryText = texts[2];
            }

            if (buttons.Length >= 2)
            {
                row.primaryButton = buttons[0];
                row.secondaryButton = buttons[1];
            }

            // 设置显示
            if (row.actionName != null)
                row.actionName.text = InputRemapper.Instance.GetActionDisplayName(action);

            if (row.primaryText != null)
                row.primaryText.text = InputRemapper.Instance.GetKeyDisplayName(currentPlayer, action, true);

            if (row.secondaryText != null)
                row.secondaryText.text = InputRemapper.Instance.GetKeyDisplayName(currentPlayer, action, false);

            // 绑定按钮事件
            var capturedAction = action;
            if (row.primaryButton != null)
                row.primaryButton.onClick.AddListener(() => StartRebind(capturedAction, true));

            if (row.secondaryButton != null)
                row.secondaryButton.onClick.AddListener(() => StartRebind(capturedAction, false));

            bindingRows.Add(row);
        }
    }

    private void StartRebind(InputRemapper.GameAction action, bool isPrimary)
    {
        if (InputRemapper.Instance == null) return;

        // 显示监听提示
        if (listeningOverlay != null) listeningOverlay.SetActive(true);
        if (listeningText != null)
        {
            string actionName = InputRemapper.Instance.GetActionDisplayName(action);
            string slot = isPrimary ? "Primary" : "Secondary";

            if (LocalizationSystem.Instance != null)
                listeningText.text = LocalizationSystem.Instance.Get("input_press_key",
                    $"Press a key for [{actionName}] ({slot})\nESC to cancel");
            else
                listeningText.text = $"Press a key for [{actionName}] ({slot})\nESC to cancel";
        }

        // 开始监听
        InputRemapper.Instance.StartListening(currentPlayer, action, isPrimary);

        // 监听完成回调
        InputRemapper.Instance.OnKeyRebound += OnKeyRebound;
    }

    private void OnKeyRebound(int playerIndex, InputRemapper.GameAction action, KeyCode key)
    {
        InputRemapper.Instance.OnKeyRebound -= OnKeyRebound;

        if (listeningOverlay != null) listeningOverlay.SetActive(false);

        // 刷新列表
        RefreshBindingList();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_confirm");
    }

    private void OnReset()
    {
        if (InputRemapper.Instance == null) return;

        InputRemapper.Instance.ResetToDefaults(currentPlayer);
        RefreshBindingList();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    void Update()
    {
        // 如果监听被取消（按ESC），隐藏提示
        if (listeningOverlay != null && listeningOverlay.activeSelf)
        {
            if (InputRemapper.Instance != null && !InputRemapper.Instance.IsListeningForKey)
            {
                listeningOverlay.SetActive(false);
                InputRemapper.Instance.OnKeyRebound -= OnKeyRebound;
            }
        }
    }
}
