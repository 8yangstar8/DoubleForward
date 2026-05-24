using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 氧气条UI - 在玩家进入水下时显示氧气量
/// 自动隐藏/显示，低氧气时闪烁警告
/// </summary>
public class OxygenBarUI : MonoBehaviour
{
    [Header("UI元素")]
    [SerializeField] private GameObject oxygenBarContainer;
    [SerializeField] private Image oxygenFillBar;
    [SerializeField] private Image oxygenBarBackground;
    [SerializeField] private TextMeshProUGUI oxygenPercentText;
    [SerializeField] private Image warningIcon;

    [Header("颜色")]
    [SerializeField] private Color fullColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color lowColor = new Color(1f, 0.3f, 0.2f);
    [SerializeField] private float lowOxygenThreshold = 0.3f;

    [Header("动画")]
    [SerializeField] private float showSpeed = 5f;
    [SerializeField] private float hideDelay = 1f;
    [SerializeField] private float warningFlashSpeed = 3f;

    private CanvasGroup canvasGroup;
    private bool shouldShow;
    private float hideTimer;
    private float currentOxygen = 1f;
    private float displayedOxygen = 1f;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;

        if (oxygenBarContainer != null)
            oxygenBarContainer.SetActive(true);

        if (warningIcon != null)
            warningIcon.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        EventBus.Subscribe<WaterEnteredEvent>(OnWaterEntered);
        EventBus.Subscribe<WaterExitedEvent>(OnWaterExited);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<WaterEnteredEvent>(OnWaterEntered);
        EventBus.Unsubscribe<WaterExitedEvent>(OnWaterExited);
    }

    void Update()
    {
        // 查询氧气值
        UpdateOxygenFromWater();

        // 平滑显示
        displayedOxygen = Mathf.Lerp(displayedOxygen, currentOxygen, Time.deltaTime * 10f);
        UpdateDisplay();

        // 显示/隐藏过渡
        float targetAlpha = shouldShow ? 1f : 0f;
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * showSpeed);

        // 隐藏延迟
        if (!shouldShow && canvasGroup.alpha < 0.01f)
            canvasGroup.alpha = 0f;

        // 低氧气警告闪烁
        if (currentOxygen <= lowOxygenThreshold && shouldShow)
        {
            if (warningIcon != null)
            {
                warningIcon.gameObject.SetActive(true);
                float flash = Mathf.PingPong(Time.time * warningFlashSpeed, 1f);
                warningIcon.color = new Color(1f, 0.3f, 0.2f, flash);
            }
        }
        else
        {
            if (warningIcon != null)
                warningIcon.gameObject.SetActive(false);
        }
    }

    private void UpdateOxygenFromWater()
    {
        // 查找当前活跃的WaterSwimming区域
        var waters = FindObjectsByType<WaterSwimming>(FindObjectsSortMode.None);
        float minOxygen = 1f;
        bool inWater = false;

        // 查找P1的PlayerController
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        PlayerController localPlayer = null;
        foreach (var p in players)
        {
            if (p.PlayerIndex == 0)
            {
                localPlayer = p;
                break;
            }
        }

        if (localPlayer != null)
        {
            foreach (var water in waters)
            {
                if (water.IsPlayerInWater(localPlayer))
                {
                    inWater = true;
                    float oxy = water.GetOxygenPercent(localPlayer);
                    if (oxy < minOxygen)
                        minOxygen = oxy;
                }
            }
        }

        currentOxygen = minOxygen;

        if (inWater)
        {
            shouldShow = true;
            hideTimer = hideDelay;
        }
        else
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0)
                shouldShow = false;
        }
    }

    private void UpdateDisplay()
    {
        if (oxygenFillBar != null)
        {
            oxygenFillBar.fillAmount = displayedOxygen;
            oxygenFillBar.color = Color.Lerp(lowColor, fullColor, displayedOxygen);
        }

        if (oxygenPercentText != null)
        {
            int percent = Mathf.RoundToInt(displayedOxygen * 100f);
            oxygenPercentText.text = $"{percent}%";
        }
    }

    private void OnWaterEntered(WaterEnteredEvent evt)
    {
        if (evt.playerIndex == 0)
        {
            shouldShow = true;
            hideTimer = hideDelay;
        }
    }

    private void OnWaterExited(WaterExitedEvent evt)
    {
        if (evt.playerIndex == 0)
        {
            hideTimer = hideDelay;
        }
    }
}
