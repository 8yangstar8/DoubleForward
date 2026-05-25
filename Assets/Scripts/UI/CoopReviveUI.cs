using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 合作复活UI - 显示倒地玩家的复活进度和倒计时
/// 当有玩家倒下时自动出现，引导存活的玩家前往复活
/// 支持CoopReviveSystem和RespawnSystem两种复活系统
/// </summary>
public class CoopReviveUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject revivePanel;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("P1 倒地指示")]
    [SerializeField] private GameObject p1DownedGroup;
    [SerializeField] private Image p1ReviveProgressFill;
    [SerializeField] private TextMeshProUGUI p1BleedoutTimerText;
    [SerializeField] private TextMeshProUGUI p1StatusText;
    [SerializeField] private Image p1Icon;

    [Header("P2 倒地指示")]
    [SerializeField] private GameObject p2DownedGroup;
    [SerializeField] private Image p2ReviveProgressFill;
    [SerializeField] private TextMeshProUGUI p2BleedoutTimerText;
    [SerializeField] private TextMeshProUGUI p2StatusText;
    [SerializeField] private Image p2Icon;

    [Header("提示")]
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private RectTransform arrowIndicator;

    [Header("世界空间指示器")]
    [SerializeField] private GameObject worldReviveBarPrefab;

    [Header("视觉")]
    [SerializeField] private Color normalProgressColor = new Color(0.2f, 1f, 0.4f);
    [SerializeField] private Color urgentProgressColor = new Color(1f, 0.3f, 0.2f);
    [SerializeField] private float urgentThreshold = 5f;
    [SerializeField] private float pulseSpeed = 3f;

    [Header("动画")]
    [SerializeField] private float fadeSpeed = 5f;
    [SerializeField] private float slideInDistance = 50f;

    // 运行时
    private float targetAlpha;
    private bool isShowing;
    private GameObject[] worldReviveBars = new GameObject[2];
    private Image[] worldReviveFills = new Image[2];
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;

        if (revivePanel != null)
            revivePanel.SetActive(false);
        if (p1DownedGroup != null)
            p1DownedGroup.SetActive(false);
        if (p2DownedGroup != null)
            p2DownedGroup.SetActive(false);

        // 订阅事件
        if (CoopReviveSystem.Instance != null)
        {
            CoopReviveSystem.Instance.OnPlayerDowned += OnPlayerDowned;
            CoopReviveSystem.Instance.OnPlayerRevived += OnPlayerRevived;
            CoopReviveSystem.Instance.OnBothPlayersDowned += OnBothDowned;
        }

        // RespawnSystem事件
        if (RespawnSystem.Instance != null)
        {
            RespawnSystem.Instance.OnPlayerRespawned += OnPlayerRespawned;
            RespawnSystem.Instance.OnReviveProgress += OnReviveProgressUpdate;
        }
    }

    void OnDestroy()
    {
        if (CoopReviveSystem.Instance != null)
        {
            CoopReviveSystem.Instance.OnPlayerDowned -= OnPlayerDowned;
            CoopReviveSystem.Instance.OnPlayerRevived -= OnPlayerRevived;
            CoopReviveSystem.Instance.OnBothPlayersDowned -= OnBothDowned;
        }

        if (RespawnSystem.Instance != null)
        {
            RespawnSystem.Instance.OnPlayerRespawned -= OnPlayerRespawned;
            RespawnSystem.Instance.OnReviveProgress -= OnReviveProgressUpdate;
        }

        // 清理世界空间指示器
        for (int i = 0; i < 2; i++)
        {
            if (worldReviveBars[i] != null)
                Destroy(worldReviveBars[i]);
        }
    }

    void Update()
    {
        // 面板显隐动画
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
        }

        if (!isShowing) return;

        // 更新CoopReviveSystem数据
        UpdateCoopReviveData();

        // 更新RespawnSystem数据
        UpdateRespawnData();

        // 更新指引箭头
        UpdateArrowIndicator();

        // 检查是否所有人都活着了
        CheckAllAlive();
    }

    // ==================== 事件处理 ====================

    private void OnPlayerDowned(int playerIndex)
    {
        ShowPanel();
        SetPlayerDownedUI(playerIndex, true);

        // 更新提示文字
        UpdateInstructionText(playerIndex);

        // 创建世界空间复活条
        CreateWorldReviveBar(playerIndex);
    }

    private void OnPlayerRevived(int revivedIndex, int reviverIndex)
    {
        SetPlayerDownedUI(revivedIndex, false);
        DestroyWorldReviveBar(revivedIndex);

        // 播放复活完成反馈
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.HealBurst, Vector3.zero);
    }

    private void OnPlayerRespawned(int playerIndex)
    {
        SetPlayerDownedUI(playerIndex, false);
        DestroyWorldReviveBar(playerIndex);
    }

    private void OnReviveProgressUpdate(int playerIndex, float progress)
    {
        SetReviveProgress(playerIndex, progress);
    }

    private void OnBothDowned()
    {
        // 两人都倒下，显示紧急提示
        if (instructionText != null)
        {
            string text = "revive_both_downed";
            if (LocalizationSystem.Instance != null)
                text = LocalizationSystem.Instance.Get("revive_both_downed", "Both players down!");
            instructionText.text = text;
            instructionText.color = urgentProgressColor;
        }
    }

    // ==================== UI更新 ====================

    private void UpdateCoopReviveData()
    {
        if (CoopReviveSystem.Instance == null) return;

        for (int i = 0; i < 2; i++)
        {
            if (!CoopReviveSystem.Instance.IsPlayerDowned(i)) continue;

            // 更新倒计时
            float remaining = CoopReviveSystem.Instance.GetBleedoutTimeRemaining(i);
            SetBleedoutTimer(i, remaining);

            // 更新复活进度
            float progress = CoopReviveSystem.Instance.ReviveProgress;
            if (i == 0 && p1ReviveProgressFill != null)
            {
                p1ReviveProgressFill.fillAmount = progress;
                p1ReviveProgressFill.color = remaining < urgentThreshold ? urgentProgressColor : normalProgressColor;
            }
            if (i == 1 && p2ReviveProgressFill != null)
            {
                p2ReviveProgressFill.fillAmount = progress;
                p2ReviveProgressFill.color = remaining < urgentThreshold ? urgentProgressColor : normalProgressColor;
            }

            // 世界空间复活条
            UpdateWorldReviveBar(i, progress, remaining);
        }
    }

    private void UpdateRespawnData()
    {
        if (RespawnSystem.Instance == null) return;

        for (int i = 0; i < 2; i++)
        {
            if (!RespawnSystem.Instance.IsPlayerDead(i)) continue;

            float remaining = 0f;
            float progress = RespawnSystem.Instance.GetReviveProgress(i);

            SetReviveProgress(i, progress);
        }
    }

    private void SetPlayerDownedUI(int playerIndex, bool downed)
    {
        if (playerIndex == 0 && p1DownedGroup != null)
        {
            p1DownedGroup.SetActive(downed);
            if (p1StatusText != null)
            {
                string text = downed ? "revive_downed" : "revive_alive";
                if (LocalizationSystem.Instance != null)
                    text = LocalizationSystem.Instance.Get(text, downed ? "Downed!" : "Active");
                p1StatusText.text = text;
            }
        }

        if (playerIndex == 1 && p2DownedGroup != null)
        {
            p2DownedGroup.SetActive(downed);
            if (p2StatusText != null)
            {
                string text = downed ? "revive_downed" : "revive_alive";
                if (LocalizationSystem.Instance != null)
                    text = LocalizationSystem.Instance.Get(text, downed ? "Downed!" : "Active");
                p2StatusText.text = text;
            }
        }
    }

    private void SetBleedoutTimer(int playerIndex, float remaining)
    {
        var timerText = playerIndex == 0 ? p1BleedoutTimerText : p2BleedoutTimerText;
        if (timerText == null) return;

        int seconds = Mathf.CeilToInt(remaining);
        timerText.text = $"{seconds}s";

        // 紧急时闪烁
        if (remaining < urgentThreshold)
        {
            float pulse = (Mathf.Sin(Time.time * pulseSpeed * 2f) + 1f) * 0.5f;
            timerText.color = Color.Lerp(Color.white, urgentProgressColor, pulse);
        }
        else
        {
            timerText.color = Color.white;
        }
    }

    private void SetReviveProgress(int playerIndex, float progress)
    {
        var fill = playerIndex == 0 ? p1ReviveProgressFill : p2ReviveProgressFill;
        if (fill != null)
        {
            fill.fillAmount = progress;
            fill.color = normalProgressColor;
        }
    }

    private void UpdateInstructionText(int downedIndex)
    {
        if (instructionText == null) return;

        int aliveIndex = 1 - downedIndex;
        string playerName = aliveIndex == 0 ? "Lux" : "Nox";

        string text = "revive_instruction";
        if (LocalizationSystem.Instance != null)
            text = LocalizationSystem.Instance.Get("revive_instruction",
                $"{playerName}, go revive your partner!");
        instructionText.text = text;
        instructionText.color = Color.white;
    }

    // ==================== 箭头指引 ====================

    private void UpdateArrowIndicator()
    {
        if (arrowIndicator == null || mainCam == null) return;

        if (CoopReviveSystem.Instance == null) return;

        // 找到存活玩家和倒地玩家的位置
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        PlayerController alive = null;
        PlayerController downed = null;

        foreach (var p in players)
        {
            if (CoopReviveSystem.Instance.IsPlayerDowned(p.PlayerIndex))
                downed = p;
            else
                alive = p;
        }

        if (alive == null || downed == null)
        {
            arrowIndicator.gameObject.SetActive(false);
            return;
        }

        // 方向箭头从存活玩家指向倒地玩家
        Vector3 screenAlive = mainCam.WorldToScreenPoint(alive.transform.position);
        Vector3 screenDowned = mainCam.WorldToScreenPoint(downed.transform.position);
        Vector3 dir = (screenDowned - screenAlive).normalized;

        arrowIndicator.gameObject.SetActive(true);
        arrowIndicator.position = screenAlive + dir * 80f;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        arrowIndicator.rotation = Quaternion.Euler(0, 0, angle);
    }

    // ==================== 世界空间复活条 ====================

    private void CreateWorldReviveBar(int playerIndex)
    {
        if (worldReviveBarPrefab == null) return;

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        Transform target = null;
        foreach (var p in players)
        {
            if (p.PlayerIndex == playerIndex)
            {
                target = p.transform;
                break;
            }
        }

        if (target == null) return;

        worldReviveBars[playerIndex] = Instantiate(worldReviveBarPrefab,
            target.position + Vector3.up * 1.8f, Quaternion.identity);

        // 获取进度条Image
        worldReviveFills[playerIndex] = worldReviveBars[playerIndex].GetComponentInChildren<Image>();
    }

    private void UpdateWorldReviveBar(int playerIndex, float progress, float remaining)
    {
        if (worldReviveBars[playerIndex] == null) return;

        // 跟随玩家位置
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.PlayerIndex == playerIndex)
            {
                worldReviveBars[playerIndex].transform.position =
                    p.transform.position + Vector3.up * 1.8f;
                break;
            }
        }

        // 更新进度条
        if (worldReviveFills[playerIndex] != null)
        {
            worldReviveFills[playerIndex].fillAmount = progress;
            worldReviveFills[playerIndex].color =
                remaining < urgentThreshold ? urgentProgressColor : normalProgressColor;
        }
    }

    private void DestroyWorldReviveBar(int playerIndex)
    {
        if (worldReviveBars[playerIndex] != null)
        {
            Destroy(worldReviveBars[playerIndex]);
            worldReviveBars[playerIndex] = null;
            worldReviveFills[playerIndex] = null;
        }
    }

    // ==================== 面板控制 ====================

    private void ShowPanel()
    {
        if (revivePanel != null)
            revivePanel.SetActive(true);
        targetAlpha = 1f;
        isShowing = true;
    }

    private void HidePanel()
    {
        targetAlpha = 0f;
        isShowing = false;

        // 延迟隐藏面板
        if (revivePanel != null)
            Invoke(nameof(DisablePanel), 0.5f);
    }

    private void DisablePanel()
    {
        if (revivePanel != null && !isShowing)
            revivePanel.SetActive(false);
    }

    private void CheckAllAlive()
    {
        bool anyDowned = false;

        if (CoopReviveSystem.Instance != null)
            anyDowned = CoopReviveSystem.Instance.IsAnyoneDowned;
        else if (RespawnSystem.Instance != null)
            anyDowned = RespawnSystem.Instance.IsPlayerDead(0) || RespawnSystem.Instance.IsPlayerDead(1);

        if (!anyDowned && isShowing)
            HidePanel();
    }
}
