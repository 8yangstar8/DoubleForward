using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 合作指示器UI - 显示队友位置/状态的屏幕边缘指示器
/// 当队友超出屏幕范围时，在边缘显示方向箭头和距离
/// 同时显示队友的生命值和倒地状态
/// </summary>
public class CoopIndicatorUI : MonoBehaviour
{
    [Header("指示器")]
    [SerializeField] private RectTransform luxIndicator;
    [SerializeField] private RectTransform noxIndicator;

    [Header("箭头元素")]
    [SerializeField] private Image luxArrowImage;
    [SerializeField] private Image noxArrowImage;
    [SerializeField] private TextMeshProUGUI luxDistanceText;
    [SerializeField] private TextMeshProUGUI noxDistanceText;

    [Header("状态元素")]
    [SerializeField] private Image luxHealthBar;
    [SerializeField] private Image noxHealthBar;
    [SerializeField] private GameObject luxDownedIcon;
    [SerializeField] private GameObject noxDownedIcon;

    [Header("合作能量")]
    [SerializeField] private Image coopMeterFill;
    [SerializeField] private TextMeshProUGUI coopMeterText;
    [SerializeField] private GameObject coopReadyIndicator;
    [SerializeField] private Animator coopReadyAnimator;

    [Header("连接线指示")]
    [SerializeField] private Image connectionStrengthIcon;
    [SerializeField] private Color nearColor = new Color(0.3f, 1f, 0.5f);
    [SerializeField] private Color farColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private float maxIndicatorDistance = 10f;

    [Header("设置")]
    [SerializeField] private float edgePadding = 50f;
    [SerializeField] private float minAlpha = 0.3f;
    [SerializeField] private float pulseSpeed = 3f;

    // 引用
    private Camera mainCamera;
    private PlayerController luxPlayer;
    private PlayerController noxPlayer;
    private PlayerHealth luxHealth;
    private PlayerHealth noxHealth;
    private Canvas parentCanvas;
    private RectTransform canvasRect;

    void Start()
    {
        mainCamera = Camera.main;
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
            canvasRect = parentCanvas.GetComponent<RectTransform>();

        FindPlayers();

        // 订阅事件
        if (CoopAbilitySystem.Instance != null)
        {
            CoopAbilitySystem.Instance.OnMeterChanged += UpdateCoopMeter;
            CoopAbilitySystem.Instance.OnCoopReady += OnCoopReady;
            CoopAbilitySystem.Instance.OnCoopEnded += OnCoopEnded;
        }

        // 初始隐藏
        if (coopReadyIndicator != null) coopReadyIndicator.SetActive(false);
        if (luxDownedIcon != null) luxDownedIcon.SetActive(false);
        if (noxDownedIcon != null) noxDownedIcon.SetActive(false);
    }

    void OnDestroy()
    {
        if (CoopAbilitySystem.Instance != null)
        {
            CoopAbilitySystem.Instance.OnMeterChanged -= UpdateCoopMeter;
            CoopAbilitySystem.Instance.OnCoopReady -= OnCoopReady;
            CoopAbilitySystem.Instance.OnCoopEnded -= OnCoopEnded;
        }
    }

    void Update()
    {
        if (mainCamera == null) return;

        if (luxPlayer == null || noxPlayer == null)
            FindPlayers();

        if (luxPlayer == null || noxPlayer == null) return;

        // 更新各指示器
        UpdatePlayerIndicator(luxPlayer, noxPlayer, luxIndicator,
            luxArrowImage, luxDistanceText, luxHealthBar);
        UpdatePlayerIndicator(noxPlayer, luxPlayer, noxIndicator,
            noxArrowImage, noxDistanceText, noxHealthBar);

        // 更新生命值
        UpdateHealthBars();

        // 更新倒地状态
        UpdateDownedStatus();

        // 更新连接强度
        UpdateConnectionStrength();
    }

    // ==================== 指示器更新 ====================

    /// <summary>
    /// 更新单个玩家的屏幕外指示器
    /// viewerPlayer: 当前视角玩家, targetPlayer: 要指示的队友
    /// </summary>
    private void UpdatePlayerIndicator(PlayerController targetPlayer,
        PlayerController viewerPlayer, RectTransform indicator,
        Image arrowImage, TextMeshProUGUI distText, Image healthBar)
    {
        if (indicator == null || targetPlayer == null) return;

        Vector3 screenPos = mainCamera.WorldToViewportPoint(targetPlayer.transform.position);

        bool isOnScreen = screenPos.x > 0.05f && screenPos.x < 0.95f
            && screenPos.y > 0.05f && screenPos.y < 0.95f
            && screenPos.z > 0;

        // 在屏幕内则隐藏指示器
        indicator.gameObject.SetActive(!isOnScreen);

        if (isOnScreen) return;

        // 计算边缘位置
        Vector2 dir = new Vector2(screenPos.x - 0.5f, screenPos.y - 0.5f).normalized;
        float halfW = canvasRect != null ? canvasRect.rect.width * 0.5f - edgePadding : 400f;
        float halfH = canvasRect != null ? canvasRect.rect.height * 0.5f - edgePadding : 300f;

        // 限制在屏幕边缘
        float scaleX = Mathf.Abs(dir.x) > 0.001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
        float scaleY = Mathf.Abs(dir.y) > 0.001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
        float scale = Mathf.Min(scaleX, scaleY);

        Vector2 edgePos = dir * scale;
        indicator.anchoredPosition = edgePos;

        // 旋转箭头
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        indicator.rotation = Quaternion.Euler(0, 0, angle - 90f);

        // 距离文本
        if (distText != null && viewerPlayer != null)
        {
            float dist = Vector2.Distance(
                viewerPlayer.transform.position,
                targetPlayer.transform.position);
            distText.text = $"{dist:F0}m";
        }

        // 透明度（距离越远越淡）
        if (arrowImage != null && viewerPlayer != null)
        {
            float dist = Vector2.Distance(
                viewerPlayer.transform.position,
                targetPlayer.transform.position);
            float alpha = Mathf.Lerp(1f, minAlpha,
                Mathf.Clamp01(dist / maxIndicatorDistance));

            // 脉冲效果
            if (CoopReviveSystem.Instance != null)
            {
                int targetIdx = targetPlayer.PlayerIndex;
                if (CoopReviveSystem.Instance.IsPlayerDowned(targetIdx))
                {
                    alpha = Mathf.Abs(Mathf.Sin(Time.time * pulseSpeed));
                }
            }

            var c = arrowImage.color;
            arrowImage.color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    // ==================== 生命值 ====================

    private void UpdateHealthBars()
    {
        if (luxHealthBar != null && luxHealth != null)
            luxHealthBar.fillAmount = luxHealth.HealthPercent;

        if (noxHealthBar != null && noxHealth != null)
            noxHealthBar.fillAmount = noxHealth.HealthPercent;
    }

    // ==================== 倒地状态 ====================

    private void UpdateDownedStatus()
    {
        if (CoopReviveSystem.Instance == null) return;

        if (luxDownedIcon != null)
            luxDownedIcon.SetActive(CoopReviveSystem.Instance.IsPlayerDowned(0));

        if (noxDownedIcon != null)
            noxDownedIcon.SetActive(CoopReviveSystem.Instance.IsPlayerDowned(1));
    }

    // ==================== 连接强度 ====================

    private void UpdateConnectionStrength()
    {
        if (connectionStrengthIcon == null) return;
        if (luxPlayer == null || noxPlayer == null) return;

        float dist = Vector2.Distance(
            luxPlayer.transform.position,
            noxPlayer.transform.position);

        float t = Mathf.Clamp01(dist / maxIndicatorDistance);
        connectionStrengthIcon.color = Color.Lerp(nearColor, farColor, t);
    }

    // ==================== 合作能量 ====================

    private void UpdateCoopMeter(float percent)
    {
        if (coopMeterFill != null)
            coopMeterFill.fillAmount = percent;

        if (coopMeterText != null)
            coopMeterText.text = $"{Mathf.FloorToInt(percent * 100)}%";
    }

    private void OnCoopReady()
    {
        if (coopReadyIndicator != null)
            coopReadyIndicator.SetActive(true);

        if (coopReadyAnimator != null)
            coopReadyAnimator.SetTrigger("Ready");
    }

    private void OnCoopEnded()
    {
        if (coopReadyIndicator != null)
            coopReadyIndicator.SetActive(false);
    }

    // ==================== 初始化 ====================

    private void FindPlayers()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.PlayerIndex == 0)
            {
                luxPlayer = p;
                luxHealth = p.GetComponent<PlayerHealth>();
            }
            else if (p.PlayerIndex == 1)
            {
                noxPlayer = p;
                noxHealth = p.GetComponent<PlayerHealth>();
            }
        }
    }
}
