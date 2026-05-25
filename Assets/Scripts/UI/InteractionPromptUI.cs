using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 交互提示UI - 在可交互物体附近显示提示气泡
/// 监听PlayerInteraction组件的交互目标变化
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private RectTransform promptPanel;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private Image buttonIcon;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Canvas parentCanvas;

    [Header("位置")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0, 2f, 0);
    [SerializeField] private Camera uiCamera;

    [Header("动画")]
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobAmount = 5f;
    [SerializeField] private float scaleInSpeed = 10f;

    private PlayerInteraction interaction;
    private bool isShowing;
    private float targetAlpha;
    private float currentScale;
    private Transform currentTargetTransform;

    void Start()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (promptPanel != null) promptPanel.localScale = Vector3.zero;
    }

    void Update()
    {
        // 寻找本地交互组件
        if (interaction == null)
        {
            var players = FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);
            if (players.Length > 0) interaction = players[0];
        }

        if (interaction == null) return;

        // 检查交互目标
        var target = interaction.GetCurrentTarget();
        bool hasTarget = target != null;

        if (hasTarget)
        {
            // 更新提示文本
            string key = target.GetInteractPrompt();
            string displayText = LocalizationSystem.Instance != null
                ? LocalizationSystem.Instance.GetText(key)
                : key;

            if (promptText != null)
                promptText.text = displayText;

            // 获取目标位置
            if (target is MonoBehaviour mb)
                currentTargetTransform = mb.transform;

            targetAlpha = 1f;
        }
        else
        {
            targetAlpha = 0f;
            currentTargetTransform = null;
        }

        // 淡入淡出
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha,
                fadeSpeed * Time.deltaTime);
        }

        // 缩放
        float targetScale = hasTarget ? 1f : 0f;
        currentScale = Mathf.MoveTowards(currentScale, targetScale, scaleInSpeed * Time.deltaTime);
        if (promptPanel != null)
            promptPanel.localScale = Vector3.one * currentScale;

        // 跟随目标位置
        if (currentTargetTransform != null && parentCanvas != null)
        {
            Camera cam = uiCamera != null ? uiCamera : Camera.main;
            if (cam != null)
            {
                Vector3 worldPos = currentTargetTransform.position + worldOffset;

                // 浮动效果
                float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;

                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

                if (promptPanel != null)
                {
                    promptPanel.position = screenPos;
                    promptPanel.anchoredPosition += new Vector2(0, bob);
                }
            }
        }
    }
}
