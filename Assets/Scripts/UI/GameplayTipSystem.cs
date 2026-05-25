using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 游戏提示系统 - 情境感知的游戏提示
/// 根据玩家行为（多次死亡、长时间卡关、首次遭遇机制）显示帮助
/// 提示只在第一次遇到时显示，除非玩家陷入困境
/// </summary>
public class GameplayTipSystem : MonoBehaviour
{
    public static GameplayTipSystem Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject tipPanel;
    [SerializeField] private TextMeshProUGUI tipText;
    [SerializeField] private Image tipIcon;
    [SerializeField] private Button dismissButton;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("动画")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float displayDuration = 5f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("触发条件")]
    [SerializeField] private int deathsBeforeHint = 3;     // 连续死N次提示
    [SerializeField] private float idleTimeBeforeHint = 30f; // 卡关超时提示
    [SerializeField] private float tipCooldown = 15f;       // 提示冷却

    [Header("提示数据")]
    [SerializeField] private TipData[] tips;

    // 运行时
    private Queue<string> pendingTips = new Queue<string>();
    private HashSet<string> shownTips = new HashSet<string>();
    private bool isShowingTip;
    private float lastTipTime;
    private int consecutiveDeaths;
    private float idleTimer;
    private Vector3 lastPlayerPosition;

    private const string SHOWN_TIPS_KEY = "shown_gameplay_tips";

    [System.Serializable]
    public class TipData
    {
        public string tipId;
        public string textKey;           // 本地化key
        public string fallbackText;      // 无本地化时的默认文本
        public TipTrigger trigger;
        public Sprite icon;
        public int priority;             // 高优先级先显示
        public bool showOnce = true;     // 只显示一次
    }

    public enum TipTrigger
    {
        FirstEnemy,          // 首次遇到敌人
        FirstPuzzle,         // 首次遇到谜题
        FirstBoss,           // 首次遇到Boss
        FirstDeath,          // 首次死亡
        RepeatedDeath,       // 多次死亡
        IdleTooLong,         // 长时间不动
        NearCoopMechanism,   // 靠近合作机关
        LowHealth,           // 低血量
        NewAbility,          // 解锁新技能
        FirstCollectible,    // 首次收集品
        Manual,              // 手动触发
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        LoadShownTips();
    }

    void Start()
    {
        EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Subscribe<PuzzleSolvedEvent>(OnPuzzleSolved);

        if (tipPanel != null) tipPanel.SetActive(false);
        dismissButton?.onClick.AddListener(DismissCurrentTip);

        // 初始化默认提示
        if (tips == null || tips.Length == 0)
            InitializeDefaultTips();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Unsubscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // 检测玩家是否静止（卡关检测）
        UpdateIdleDetection();

        // 处理待显示的提示
        if (!isShowingTip && pendingTips.Count > 0 &&
            Time.time - lastTipTime >= tipCooldown)
        {
            ShowNextTip();
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 手动显示提示
    /// </summary>
    public void ShowTip(string tipId)
    {
        var tip = FindTip(tipId);
        if (tip == null) return;

        if (tip.showOnce && shownTips.Contains(tipId)) return;

        pendingTips.Enqueue(tipId);
    }

    /// <summary>
    /// 立即显示提示文本（不走队列）
    /// </summary>
    public void ShowTipImmediate(string text, Sprite icon = null, float duration = 0f)
    {
        if (duration <= 0) duration = displayDuration;
        StartCoroutine(ShowTipRoutine(text, icon, duration));
    }

    /// <summary>
    /// 触发特定场景的提示检查
    /// </summary>
    public void TriggerCheck(TipTrigger trigger)
    {
        if (tips == null) return;

        foreach (var tip in tips)
        {
            if (tip.trigger != trigger) continue;
            if (tip.showOnce && shownTips.Contains(tip.tipId)) continue;

            pendingTips.Enqueue(tip.tipId);
            break; // 每次触发只排一个
        }
    }

    /// <summary>
    /// 重置所有已显示的提示（新游戏时）
    /// </summary>
    public void ResetAll()
    {
        shownTips.Clear();
        SaveShownTips();
    }

    // ==================== 事件处理 ====================

    private void OnPlayerDeath(PlayerDeathEvent e)
    {
        consecutiveDeaths++;

        if (consecutiveDeaths == 1)
            TriggerCheck(TipTrigger.FirstDeath);

        if (consecutiveDeaths >= deathsBeforeHint)
        {
            TriggerCheck(TipTrigger.RepeatedDeath);
            consecutiveDeaths = 0;
        }
    }

    private void OnPlayerDamaged(PlayerDamagedEvent e)
    {
        if (e.remainingHealth <= 1)
            TriggerCheck(TipTrigger.LowHealth);
    }

    private void OnEnemyDefeated(EnemyDefeatedEvent e)
    {
        consecutiveDeaths = 0; // 击杀重置死亡计数
    }

    private void OnPuzzleSolved(PuzzleSolvedEvent e)
    {
        consecutiveDeaths = 0;
    }

    // ==================== 卡关检测 ====================

    private void UpdateIdleDetection()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        if (players.Length == 0) return;

        Vector3 currentPos = players[0].transform.position;

        if (Vector3.Distance(currentPos, lastPlayerPosition) < 1f)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= idleTimeBeforeHint)
            {
                TriggerCheck(TipTrigger.IdleTooLong);
                idleTimer = 0;
            }
        }
        else
        {
            idleTimer = 0;
            lastPlayerPosition = currentPos;
        }
    }

    // ==================== 显示逻辑 ====================

    private void ShowNextTip()
    {
        if (pendingTips.Count == 0) return;

        string tipId = pendingTips.Dequeue();
        var tip = FindTip(tipId);
        if (tip == null) return;

        if (tip.showOnce && shownTips.Contains(tipId)) return;

        string text = GetTipText(tip);
        StartCoroutine(ShowTipRoutine(text, tip.icon, displayDuration));

        shownTips.Add(tipId);
        SaveShownTips();
    }

    private IEnumerator ShowTipRoutine(string text, Sprite icon, float duration)
    {
        if (tipPanel == null) yield break;

        isShowingTip = true;
        lastTipTime = Time.time;

        tipPanel.SetActive(true);
        if (tipText != null) tipText.text = text;
        if (tipIcon != null)
        {
            if (icon != null)
            {
                tipIcon.sprite = icon;
                tipIcon.gameObject.SetActive(true);
            }
            else
            {
                tipIcon.gameObject.SetActive(false);
            }
        }

        // 淡入
        yield return FadeCanvasGroup(0f, 1f, fadeInDuration);

        // 显示
        yield return new WaitForSecondsRealtime(duration);

        // 淡出
        yield return FadeCanvasGroup(1f, 0f, fadeOutDuration);

        tipPanel.SetActive(false);
        isShowingTip = false;
    }

    private IEnumerator FadeCanvasGroup(float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    private void DismissCurrentTip()
    {
        StopAllCoroutines();
        if (tipPanel != null) tipPanel.SetActive(false);
        isShowingTip = false;
        lastTipTime = Time.time;
    }

    // ==================== 辅助 ====================

    private TipData FindTip(string tipId)
    {
        if (tips == null) return null;
        foreach (var tip in tips)
        {
            if (tip.tipId == tipId) return tip;
        }
        return null;
    }

    private string GetTipText(TipData tip)
    {
        if (LocalizationSystem.Instance != null)
        {
            string localized = LocalizationSystem.Instance.GetText(tip.textKey);
            if (localized != tip.textKey) return localized;
        }
        return tip.fallbackText;
    }

    private void SaveShownTips()
    {
        string data = string.Join(",", shownTips);
        PlayerPrefs.SetString(SHOWN_TIPS_KEY, data);
        PlayerPrefs.Save();
    }

    private void LoadShownTips()
    {
        string data = PlayerPrefs.GetString(SHOWN_TIPS_KEY, "");
        if (!string.IsNullOrEmpty(data))
        {
            foreach (var id in data.Split(','))
                shownTips.Add(id);
        }
    }

    private void InitializeDefaultTips()
    {
        tips = new TipData[]
        {
            new TipData
            {
                tipId = "tip_first_death",
                textKey = "tip_first_death",
                fallbackText = "别灰心！队友可以靠近你按住交互键复活你。两人互相保护才能走得更远。",
                trigger = TipTrigger.FirstDeath,
                priority = 10,
                showOnce = true
            },
            new TipData
            {
                tipId = "tip_repeated_death",
                textKey = "tip_repeated_death",
                fallbackText = "这里有点难？试着换个角度思考——Lux的光束和Nox的暗影可能有意想不到的用途。",
                trigger = TipTrigger.RepeatedDeath,
                priority = 9,
                showOnce = false
            },
            new TipData
            {
                tipId = "tip_idle",
                textKey = "tip_idle",
                fallbackText = "看起来需要两人配合！试试让Lux照亮区域，Nox穿过暗影墙壁。",
                trigger = TipTrigger.IdleTooLong,
                priority = 8,
                showOnce = false
            },
            new TipData
            {
                tipId = "tip_low_health",
                textKey = "tip_low_health",
                fallbackText = "血量过低！小心规避敌人攻击，找到安全区域。",
                trigger = TipTrigger.LowHealth,
                priority = 7,
                showOnce = true
            },
            new TipData
            {
                tipId = "tip_coop_mechanism",
                textKey = "tip_coop_mechanism",
                fallbackText = "这个机关需要两人同时操作！一人踩住压力板，另一人通过打开的门。",
                trigger = TipTrigger.NearCoopMechanism,
                priority = 6,
                showOnce = true
            },
            new TipData
            {
                tipId = "tip_first_boss",
                textKey = "tip_first_boss",
                fallbackText = "Boss战！观察攻击模式寻找弱点。合作技能能量满时按下合体键释放强力技能！",
                trigger = TipTrigger.FirstBoss,
                priority = 10,
                showOnce = true
            }
        };
    }
}
