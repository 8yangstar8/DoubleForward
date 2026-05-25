using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 玩家血条UI - 支持双角色独立血条、低血量警告、受伤闪烁、护盾显示
/// 可同时显示Lux和Nox的生命值，适应分屏和单屏模式
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Header("Player 1 - Lux")]
    [SerializeField] private RectTransform healthBarP1;
    [SerializeField] private Image healthFillP1;
    [SerializeField] private Image healthFillDelayP1;  // 延迟减少的白条
    [SerializeField] private Image healthFrameP1;
    [SerializeField] private TextMeshProUGUI healthTextP1;
    [SerializeField] private Image portraitP1;

    [Header("Player 2 - Nox")]
    [SerializeField] private RectTransform healthBarP2;
    [SerializeField] private Image healthFillP2;
    [SerializeField] private Image healthFillDelayP2;
    [SerializeField] private Image healthFrameP2;
    [SerializeField] private TextMeshProUGUI healthTextP2;
    [SerializeField] private Image portraitP2;

    [Header("颜色")]
    [SerializeField] private Color healthFullColor = new Color(0.3f, 1f, 0.4f);
    [SerializeField] private Color healthMidColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color healthLowColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] private Color delayBarColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private float lowHealthThreshold = 0.3f;

    [Header("动画")]
    [SerializeField] private float fillSpeed = 5f;
    [SerializeField] private float delayBarSpeed = 2f;
    [SerializeField] private float delayBarWait = 0.5f;
    [SerializeField] private float lowHealthPulseSpeed = 3f;
    [SerializeField] private float damageShakeIntensity = 5f;
    [SerializeField] private float damageShakeDuration = 0.15f;

    // 运行时
    private PlayerHealth healthP1;
    private PlayerHealth healthP2;
    private float targetFillP1 = 1f;
    private float targetFillP2 = 1f;
    private float currentFillP1 = 1f;
    private float currentFillP2 = 1f;
    private float delayFillP1 = 1f;
    private float delayFillP2 = 1f;
    private float delayTimerP1;
    private float delayTimerP2;
    private Vector3 originalPosP1;
    private Vector3 originalPosP2;

    void Start()
    {
        if (healthBarP1 != null) originalPosP1 = healthBarP1.localPosition;
        if (healthBarP2 != null) originalPosP2 = healthBarP2.localPosition;

        if (healthFillDelayP1 != null) healthFillDelayP1.color = delayBarColor;
        if (healthFillDelayP2 != null) healthFillDelayP2.color = delayBarColor;

        // 订阅事件
        EventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Subscribe<PlayerHealEvent>(OnPlayerHealed);

        FindPlayers();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Unsubscribe<PlayerHealEvent>(OnPlayerHealed);
    }

    void Update()
    {
        if (healthP1 == null || healthP2 == null)
            FindPlayers();

        UpdateBar(healthP1, healthFillP1, healthFillDelayP1, healthTextP1, healthBarP1,
            ref currentFillP1, ref targetFillP1, ref delayFillP1, ref delayTimerP1, originalPosP1);

        UpdateBar(healthP2, healthFillP2, healthFillDelayP2, healthTextP2, healthBarP2,
            ref currentFillP2, ref targetFillP2, ref delayFillP2, ref delayTimerP2, originalPosP2);
    }

    private void FindPlayers()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Type == PlayerController.PlayerType.Lux)
                healthP1 = p.GetComponent<PlayerHealth>();
            else if (p.Type == PlayerController.PlayerType.Nox)
                healthP2 = p.GetComponent<PlayerHealth>();
        }
    }

    private void UpdateBar(PlayerHealth health, Image fill, Image delayFill,
        TextMeshProUGUI text, RectTransform bar,
        ref float currentFill, ref float targetFill,
        ref float delayFillValue, ref float delayTimer,
        Vector3 originalPos)
    {
        if (health == null || fill == null) return;

        // 计算目标百分比
        float maxHp = health.MaxHealth;
        targetFill = maxHp > 0 ? (float)health.CurrentHealth / maxHp : 0f;

        // 平滑填充
        currentFill = Mathf.MoveTowards(currentFill, targetFill, fillSpeed * Time.deltaTime);
        fill.fillAmount = currentFill;

        // 延迟白条
        if (delayFill != null)
        {
            if (delayFillValue > currentFill)
            {
                delayTimer += Time.deltaTime;
                if (delayTimer >= delayBarWait)
                {
                    delayFillValue = Mathf.MoveTowards(delayFillValue, currentFill,
                        delayBarSpeed * Time.deltaTime);
                }
            }
            else
            {
                delayFillValue = currentFill;
                delayTimer = 0f;
            }
            delayFill.fillAmount = delayFillValue;
        }

        // 颜色渐变
        fill.color = GetHealthColor(currentFill);

        // 文本
        if (text != null)
            text.text = $"{health.CurrentHealth}/{health.MaxHealth}";

        // 低血量脉冲
        if (currentFill <= lowHealthThreshold && currentFill > 0)
        {
            float pulse = Mathf.PingPong(Time.time * lowHealthPulseSpeed, 0.3f);
            fill.color = Color.Lerp(healthLowColor, Color.white, pulse);
        }

        // 震动恢复
        if (bar != null)
        {
            bar.localPosition = Vector3.Lerp(bar.localPosition, originalPos,
                10f * Time.deltaTime);
        }
    }

    private Color GetHealthColor(float percent)
    {
        if (percent > 0.6f)
            return Color.Lerp(healthMidColor, healthFullColor, (percent - 0.6f) / 0.4f);
        else if (percent > 0.3f)
            return Color.Lerp(healthLowColor, healthMidColor, (percent - 0.3f) / 0.3f);
        else
            return healthLowColor;
    }

    // ==================== 事件响应 ====================

    private void OnPlayerDamaged(PlayerDamagedEvent e)
    {
        // 受伤震动效果
        RectTransform bar = e.playerIndex == 0 ? healthBarP1 : healthBarP2;
        if (bar != null)
        {
            Vector3 shake = new Vector3(
                Random.Range(-damageShakeIntensity, damageShakeIntensity),
                Random.Range(-damageShakeIntensity, damageShakeIntensity), 0);
            bar.localPosition += shake;
        }

        // 重置延迟计时器
        if (e.playerIndex == 0)
            delayTimerP1 = 0f;
        else
            delayTimerP2 = 0f;

        // 低血量后处理
        if (e.remainingHealth <= 1 && CameraEffects.Instance != null)
            CameraEffects.Instance.SetLowHealthVignette(true);
    }

    private void OnPlayerHealed(PlayerHealEvent e)
    {
        // 恢复后处理
        if (CameraEffects.Instance != null)
            CameraEffects.Instance.SetLowHealthVignette(false);
    }
}
