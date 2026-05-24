using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 按键重映射UI - 显示当前绑定、监听新输入、冲突提示
/// 嵌入设置面板中使用
/// </summary>
public class InputRemapUI : MonoBehaviour
{
    [Header("绑定行模板")]
    [SerializeField] private GameObject bindingRowPrefab;
    [SerializeField] private RectTransform bindingRowParent;

    [Header("重置按钮")]
    [SerializeField] private Button resetDefaultsButton;

    [Header("提示")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject listeningOverlay;
    [SerializeField] private TextMeshProUGUI listeningText;

    [Header("冲突对话框")]
    [SerializeField] private GameObject conflictDialog;
    [SerializeField] private TextMeshProUGUI conflictMessage;
    [SerializeField] private Button conflictSwapButton;
    [SerializeField] private Button conflictCancelButton;

    [Header("动画")]
    [SerializeField] private float flashDuration = 0.3f;
    [SerializeField] private Color flashColor = new Color(0.2f, 0.8f, 0.2f);

    // 运行时数据
    private Dictionary<InputRemapSystem.GameAction, BindingRow> rows = new Dictionary<InputRemapSystem.GameAction, BindingRow>();

    private class BindingRow
    {
        public GameObject obj;
        public TextMeshProUGUI actionLabel;
        public TextMeshProUGUI buttonLabel;
        public Button remapButton;
        public Image background;
    }

    void Start()
    {
        BuildUI();

        if (resetDefaultsButton != null)
            resetDefaultsButton.onClick.AddListener(OnResetDefaults);

        if (conflictSwapButton != null)
            conflictSwapButton.onClick.AddListener(OnConflictSwap);

        if (conflictCancelButton != null)
            conflictCancelButton.onClick.AddListener(OnConflictCancel);

        HideOverlays();
    }

    void OnEnable()
    {
        if (InputRemapSystem.Instance != null)
            InputRemapSystem.Instance.OnBindingChanged += OnBindingChanged;

        RefreshAll();
    }

    void OnDisable()
    {
        if (InputRemapSystem.Instance != null)
        {
            InputRemapSystem.Instance.OnBindingChanged -= OnBindingChanged;
            if (InputRemapSystem.Instance.IsListening)
                InputRemapSystem.Instance.CancelRemap();
        }
    }

    // ============ UI构建 ============

    private void BuildUI()
    {
        if (bindingRowPrefab == null || bindingRowParent == null) return;

        foreach (InputRemapSystem.GameAction action in System.Enum.GetValues(typeof(InputRemapSystem.GameAction)))
        {
            var obj = Instantiate(bindingRowPrefab, bindingRowParent);
            var row = new BindingRow
            {
                obj = obj,
                actionLabel = obj.transform.Find("ActionLabel")?.GetComponent<TextMeshProUGUI>(),
                buttonLabel = obj.transform.Find("ButtonLabel")?.GetComponent<TextMeshProUGUI>(),
                remapButton = obj.transform.Find("RemapButton")?.GetComponent<Button>(),
                background = obj.GetComponent<Image>()
            };

            if (row.actionLabel != null && InputRemapSystem.Instance != null)
                row.actionLabel.text = InputRemapSystem.Instance.GetActionDisplayName(action);

            if (row.remapButton != null)
            {
                var capturedAction = action;
                row.remapButton.onClick.AddListener(() => OnRemapClicked(capturedAction));
            }

            rows[action] = row;
        }

        RefreshAll();
    }

    private void RefreshAll()
    {
        if (InputRemapSystem.Instance == null) return;

        foreach (var kvp in rows)
        {
            var binding = InputRemapSystem.Instance.GetBinding(kvp.Key);
            UpdateRowDisplay(kvp.Key, binding);
        }
    }

    private void UpdateRowDisplay(InputRemapSystem.GameAction action, InputRemapSystem.GamepadButton button)
    {
        if (!rows.TryGetValue(action, out var row)) return;
        if (row.buttonLabel == null || InputRemapSystem.Instance == null) return;

        row.buttonLabel.text = InputRemapSystem.Instance.GetButtonDisplayName(button);
    }

    // ============ 重映射流程 ============

    private InputRemapSystem.GameAction pendingAction;
    private InputRemapSystem.GamepadButton pendingButton;
    private InputRemapSystem.GameAction? pendingConflict;

    private void OnRemapClicked(InputRemapSystem.GameAction action)
    {
        if (InputRemapSystem.Instance == null) return;

        // 显示监听覆盖层
        if (listeningOverlay != null)
            listeningOverlay.SetActive(true);

        if (listeningText != null)
            listeningText.text = $"请按下要绑定到「{InputRemapSystem.Instance.GetActionDisplayName(action)}」的按钮...\n（按Select取消）";

        pendingAction = action;

        InputRemapSystem.Instance.StartRemap(action,
            onComplete: OnRemapDone,
            onCancel: OnRemapCancelled
        );
    }

    private void OnRemapDone(InputRemapSystem.GameAction action, InputRemapSystem.GamepadButton button)
    {
        HideOverlays();

        // 高亮变更的行
        StartCoroutine(FlashRow(action));

        if (statusText != null)
            statusText.text = $"已绑定：{InputRemapSystem.Instance.GetActionDisplayName(action)} → {InputRemapSystem.Instance.GetButtonDisplayName(button)}";
    }

    private void OnRemapCancelled()
    {
        HideOverlays();

        if (statusText != null)
            statusText.text = "已取消绑定";
    }

    // ============ 冲突处理 ============

    private void ShowConflictDialog(InputRemapSystem.GameAction conflictAction, InputRemapSystem.GamepadButton button)
    {
        if (conflictDialog == null || InputRemapSystem.Instance == null) return;

        pendingConflict = conflictAction;
        conflictDialog.SetActive(true);

        if (conflictMessage != null)
        {
            string actionName = InputRemapSystem.Instance.GetActionDisplayName(conflictAction);
            string btnName = InputRemapSystem.Instance.GetButtonDisplayName(button);
            conflictMessage.text = $"按钮 {btnName} 已被「{actionName}」使用。\n是否交换绑定？";
        }
    }

    private void OnConflictSwap()
    {
        if (pendingConflict.HasValue && InputRemapSystem.Instance != null)
        {
            InputRemapSystem.Instance.SwapBindings(pendingAction, pendingConflict.Value);
        }

        HideOverlays();
        RefreshAll();
    }

    private void OnConflictCancel()
    {
        HideOverlays();
    }

    // ============ 重置 ============

    private void OnResetDefaults()
    {
        if (InputRemapSystem.Instance == null) return;

        InputRemapSystem.Instance.ResetToDefaults();
        RefreshAll();

        if (statusText != null)
            statusText.text = "已重置为默认绑定";

        // 闪烁所有行
        foreach (var action in rows.Keys)
            StartCoroutine(FlashRow(action));
    }

    // ============ 视觉效果 ============

    private void HideOverlays()
    {
        if (listeningOverlay != null)
            listeningOverlay.SetActive(false);

        if (conflictDialog != null)
            conflictDialog.SetActive(false);
    }

    private IEnumerator FlashRow(InputRemapSystem.GameAction action)
    {
        if (!rows.TryGetValue(action, out var row)) yield break;
        if (row.background == null) yield break;

        Color originalColor = row.background.color;
        row.background.color = flashColor;

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / flashDuration;
            row.background.color = Color.Lerp(flashColor, originalColor, t);
            yield return null;
        }

        row.background.color = originalColor;
    }

    // ============ 事件 ============

    private void OnBindingChanged(InputRemapSystem.GameAction action, InputRemapSystem.GamepadButton button)
    {
        UpdateRowDisplay(action, button);
    }
}
