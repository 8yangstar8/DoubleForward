using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 玩家羁绊系统 - 追踪Lux和Nox之间的合作质量
/// 通过合作行为（同步动作、复活、技能组合、近距离配合等）提升羁绊等级
/// 影响结局分支、对话变体、特殊技能解锁
/// 支持5级羁绊: 陌生→认识→伙伴→挚友→灵魂共鸣
/// </summary>
public class PlayerBondSystem : MonoBehaviour
{
    public static PlayerBondSystem Instance { get; private set; }

    [Header("羁绊设置")]
    [SerializeField] private int maxBondLevel = 5;
    [SerializeField] private float[] bondThresholds = { 0, 100, 300, 600, 1000 };

    [Header("羁绊点数来源")]
    [SerializeField] private float syncActionPoints = 5f;
    [SerializeField] private float perfectSyncPoints = 15f;
    [SerializeField] private float coopAbilityPoints = 20f;
    [SerializeField] private float revivePoints = 30f;
    [SerializeField] private float proximityBonusRate = 0.5f;    // 近距离时每秒获得
    [SerializeField] private float comboChainPoints = 3f;        // 连击中每次命中
    [SerializeField] private float puzzleSolvedPoints = 10f;
    [SerializeField] private float secretFoundPoints = 15f;
    [SerializeField] private float levelCompletePoints = 25f;
    [SerializeField] private float bossDefeatedPoints = 50f;

    [Header("羁绊衰减")]
    [SerializeField] private float deathPenalty = 10f;           // 队友死亡扣分
    [SerializeField] private float separationDecayRate = 0.2f;   // 远离时每秒衰减
    [SerializeField] private float separationDistance = 15f;      // 判定远离距离

    [Header("羁绊对话")]
    [SerializeField] private List<BondDialogue> bondDialogues = new List<BondDialogue>();

    // 运行时
    private float currentBondPoints;
    private int currentBondLevel;
    private float proximityTimer;
    private float sessionProximityTime;       // 本局近距离时间
    private float sessionSeparationTime;      // 本局远离时间
    private bool bondLevelUpPending;
    private int sessionSyncCount;
    private int sessionReviveCount;

    // 持久化key
    private const string BOND_POINTS_KEY = "player_bond_points";
    private const string BOND_LEVEL_KEY = "player_bond_level";
    private const string BOND_DIALOGUES_KEY = "bond_dialogues_seen";

    // 公共属性
    public int CurrentBondLevel => currentBondLevel;
    public float CurrentBondPoints => currentBondPoints;
    public float BondProgress => GetProgressToNextLevel();
    public string BondLevelName => GetBondLevelName(currentBondLevel);
    public bool IsMaxBond => currentBondLevel >= maxBondLevel;

    // 事件
    public event System.Action<int> OnBondLevelUp;              // newLevel
    public event System.Action<float, string> OnBondPointsGained; // points, source
    public event System.Action<BondDialogue> OnBondDialogueTrigger;

    [System.Serializable]
    public class BondDialogue
    {
        public string dialogueId;
        public int requiredBondLevel;
        public BondDialogueTrigger trigger;
        public string dialogueKey;           // 本地化key
        public string fallbackTextLux;       // Lux的台词
        public string fallbackTextNox;       // Nox的台词
        public bool oneTime = true;          // 只触发一次

        public enum BondDialogueTrigger
        {
            LevelUp,           // 羁绊升级时
            LevelStart,        // 关卡开始时
            BossEncounter,     // Boss战开始
            Revive,            // 复活队友时
            NearDeath,         // 濒死时
            Victory,           // 胜利时
            SecretFound,       // 发现隐藏区域
            Idle               // 闲置一段时间
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadBondData();
        InitializeDefaultDialogues();
    }

    void Start()
    {
        // 订阅事件
        EventBus.Subscribe<CoopReviveEvent>(OnCoopRevive);
        EventBus.Subscribe<CoopAbilityUsedEvent>(OnCoopAbilityUsed);
        EventBus.Subscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Subscribe<AchievementUnlockedEvent>(OnAchievementUnlocked);
    }

    void Update()
    {
        UpdateProximityTracking();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<CoopReviveEvent>(OnCoopRevive);
        EventBus.Unsubscribe<CoopAbilityUsedEvent>(OnCoopAbilityUsed);
        EventBus.Unsubscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Unsubscribe<AchievementUnlockedEvent>(OnAchievementUnlocked);

        if (Instance == this) Instance = null;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 添加羁绊点数
    /// </summary>
    public void AddBondPoints(float points, string source)
    {
        if (IsMaxBond && currentBondPoints >= GetMaxPoints()) return;

        // NG+加成
        float multiplier = 1f;
        if (NewGamePlusManager.Instance != null && NewGamePlusManager.Instance.IsNewGamePlus)
            multiplier = 1.2f;

        float actual = points * multiplier;
        currentBondPoints += actual;

        // 限制最大值
        float max = GetMaxPoints();
        if (currentBondPoints > max)
            currentBondPoints = max;

        OnBondPointsGained?.Invoke(actual, source);

        // 检查升级
        CheckBondLevelUp();

        // 定期保存
        SaveBondData();
    }

    /// <summary>
    /// 扣除羁绊点数（不会降级）
    /// </summary>
    public void RemoveBondPoints(float points, string source)
    {
        float levelFloor = currentBondLevel > 0 ? bondThresholds[currentBondLevel - 1] : 0;
        currentBondPoints = Mathf.Max(levelFloor, currentBondPoints - points);
        SaveBondData();
    }

    /// <summary>
    /// 获取当前羁绊等级名称
    /// </summary>
    public string GetBondLevelName(int level)
    {
        if (LocalizationSystem.Instance != null)
        {
            string key = $"bond_level_{level}";
            string localized = LocalizationSystem.Instance.GetText(key);
            if (localized != key) return localized;
        }

        return level switch
        {
            0 => "Strangers",
            1 => "Acquaintances",
            2 => "Partners",
            3 => "Close Friends",
            4 => "Soulbound",
            5 => "Soul Resonance",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// 获取升级进度 0~1
    /// </summary>
    public float GetProgressToNextLevel()
    {
        if (currentBondLevel >= maxBondLevel) return 1f;

        float currentThreshold = currentBondLevel > 0 ? bondThresholds[currentBondLevel - 1] : 0;
        float nextThreshold = bondThresholds[Mathf.Min(currentBondLevel, bondThresholds.Length - 1)];
        float range = nextThreshold - currentThreshold;

        if (range <= 0) return 1f;
        return Mathf.Clamp01((currentBondPoints - currentThreshold) / range);
    }

    /// <summary>
    /// 获取羁绊等级对对话的影响（返回对话变体后缀）
    /// </summary>
    public string GetDialogueVariant()
    {
        return currentBondLevel switch
        {
            0 => "_cold",
            1 => "_neutral",
            2 => "_friendly",
            3 => "_close",
            >= 4 => "_intimate",
            _ => ""
        };
    }

    /// <summary>
    /// 获取羁绊等级对合作技能的加成
    /// </summary>
    public float GetCoopAbilityBonus()
    {
        return currentBondLevel switch
        {
            0 => 1.0f,
            1 => 1.05f,
            2 => 1.1f,
            3 => 1.2f,
            4 => 1.3f,
            5 => 1.5f,
            _ => 1.0f
        };
    }

    /// <summary>
    /// 获取本局合作统计
    /// </summary>
    public (float proximityTime, float separationTime, int syncCount, int reviveCount) GetSessionStats()
    {
        return (sessionProximityTime, sessionSeparationTime, sessionSyncCount, sessionReviveCount);
    }

    /// <summary>
    /// 触发羁绊对话（如果有匹配的未播放对话）
    /// </summary>
    public void TryTriggerBondDialogue(BondDialogue.BondDialogueTrigger trigger)
    {
        var seenSet = GetSeenDialogues();

        foreach (var dialogue in bondDialogues)
        {
            if (dialogue.trigger != trigger) continue;
            if (dialogue.requiredBondLevel > currentBondLevel) continue;
            if (dialogue.oneTime && seenSet.Contains(dialogue.dialogueId)) continue;

            // 标记已看
            seenSet.Add(dialogue.dialogueId);
            SaveSeenDialogues(seenSet);

            OnBondDialogueTrigger?.Invoke(dialogue);
            break; // 每次只触发一个
        }
    }

    /// <summary>
    /// 获取羁绊信息文本（用于UI显示）
    /// </summary>
    public string GetBondInfo()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Bond Level: {currentBondLevel} - {BondLevelName}");
        sb.AppendLine($"Points: {currentBondPoints:F0}");

        if (!IsMaxBond)
        {
            float next = bondThresholds[Mathf.Min(currentBondLevel, bondThresholds.Length - 1)];
            sb.AppendLine($"Next Level: {next:F0}");
            sb.AppendLine($"Progress: {BondProgress * 100:F0}%");
        }
        else
        {
            sb.AppendLine("MAX BOND");
        }

        sb.AppendLine($"Coop Bonus: x{GetCoopAbilityBonus():F2}");
        return sb.ToString();
    }

    // ==================== 近距离追踪 ====================

    /// <summary>
    /// 由Player层的CoopAbilitySystem或PlayerCoopSync调用，通知当前近距离状态
    /// 避免Core→Player的跨程序集引用
    /// </summary>
    public void NotifyProximityState(bool isNear)
    {
        proximityTimer += Time.deltaTime;

        if (proximityTimer >= 1f)
        {
            proximityTimer = 0;

            if (isNear)
            {
                sessionProximityTime += 1f;
                AddBondPoints(proximityBonusRate, "proximity");
            }
            else
            {
                sessionSeparationTime += 1f;

                if (currentBondPoints > 0)
                    RemoveBondPoints(separationDecayRate, "separation");
            }
        }
    }

    private void UpdateProximityTracking()
    {
        // 近距离追踪由Player层系统通过NotifyProximityState调用
        // Core层不直接引用Player层组件
    }

    // ==================== 事件处理 ====================

    private void OnCoopRevive(CoopReviveEvent e)
    {
        sessionReviveCount++;
        AddBondPoints(revivePoints, "revive");
        TryTriggerBondDialogue(BondDialogue.BondDialogueTrigger.Revive);
    }

    private void OnCoopAbilityUsed(CoopAbilityUsedEvent e)
    {
        AddBondPoints(coopAbilityPoints, "coop_ability");
    }

    private void OnPuzzleSolved(PuzzleSolvedEvent e)
    {
        AddBondPoints(puzzleSolvedPoints, "puzzle");
    }

    private void OnEnemyDefeated(EnemyDefeatedEvent e)
    {
        if (e.isBoss)
        {
            AddBondPoints(bossDefeatedPoints, "boss_defeated");
        }
        else
        {
            // 普通敌人给少量羁绊
            AddBondPoints(comboChainPoints, "enemy_defeat");
        }
    }

    private void OnPlayerDeath(PlayerDeathEvent e)
    {
        RemoveBondPoints(deathPenalty, "death");
        TryTriggerBondDialogue(BondDialogue.BondDialogueTrigger.NearDeath);
    }

    private void OnLevelComplete(LevelCompleteEvent e)
    {
        AddBondPoints(levelCompletePoints, "level_complete");
        TryTriggerBondDialogue(BondDialogue.BondDialogueTrigger.Victory);
    }

    private void OnLevelStart(LevelStartEvent e)
    {
        // 重置本局统计
        sessionProximityTime = 0;
        sessionSeparationTime = 0;
        sessionSyncCount = 0;
        sessionReviveCount = 0;

        TryTriggerBondDialogue(BondDialogue.BondDialogueTrigger.LevelStart);
    }

    private void OnAchievementUnlocked(AchievementUnlockedEvent e)
    {
        // 共同解锁成就也加羁绊
        AddBondPoints(5f, "achievement");
    }

    // ==================== 内部方法 ====================

    private void CheckBondLevelUp()
    {
        if (currentBondLevel >= maxBondLevel) return;

        int targetIndex = Mathf.Min(currentBondLevel, bondThresholds.Length - 1);
        if (currentBondPoints >= bondThresholds[targetIndex])
        {
            currentBondLevel++;

            // 通知
            OnBondLevelUp?.Invoke(currentBondLevel);

            EventBus.Publish(new HintRequestEvent
            {
                textKey = $"bond_level_up_{currentBondLevel}",
                fallbackText = $"Bond Level Up! {GetBondLevelName(currentBondLevel)}",
                duration = 4f
            });

            // 成就
            if (AchievementSystem.Instance != null)
            {
                AchievementSystem.Instance.Unlock($"bond_level_{currentBondLevel}");
                if (currentBondLevel >= maxBondLevel)
                    AchievementSystem.Instance.Unlock("bond_max");
            }

            // 触发升级对话
            TryTriggerBondDialogue(BondDialogue.BondDialogueTrigger.LevelUp);

            // 音效
            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.PlayConfirm();

            SaveBondData();
            Debug.Log($"[Bond] Level up to {currentBondLevel}: {BondLevelName}");

            // 递归检查（可能跳级）
            CheckBondLevelUp();
        }
    }

    private float GetMaxPoints()
    {
        return bondThresholds.Length > 0 ? bondThresholds[bondThresholds.Length - 1] * 1.5f : 1500f;
    }

    // ==================== 持久化 ====================

    private void LoadBondData()
    {
        currentBondPoints = PlayerPrefs.GetFloat(BOND_POINTS_KEY, 0);
        currentBondLevel = PlayerPrefs.GetInt(BOND_LEVEL_KEY, 0);
    }

    private void SaveBondData()
    {
        PlayerPrefs.SetFloat(BOND_POINTS_KEY, currentBondPoints);
        PlayerPrefs.SetInt(BOND_LEVEL_KEY, currentBondLevel);
    }

    private HashSet<string> GetSeenDialogues()
    {
        var set = new HashSet<string>();
        string json = PlayerPrefs.GetString(BOND_DIALOGUES_KEY, "");
        if (!string.IsNullOrEmpty(json))
        {
            string[] ids = json.Split(',');
            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(id))
                    set.Add(id);
            }
        }
        return set;
    }

    private void SaveSeenDialogues(HashSet<string> seen)
    {
        PlayerPrefs.SetString(BOND_DIALOGUES_KEY, string.Join(",", seen));
    }

    // ==================== 默认对话 ====================

    private void InitializeDefaultDialogues()
    {
        if (bondDialogues.Count > 0) return;

        // 羁绊升级对话
        bondDialogues.Add(new BondDialogue
        {
            dialogueId = "bond_up_1",
            requiredBondLevel = 1,
            trigger = BondDialogue.BondDialogueTrigger.LevelUp,
            dialogueKey = "bond_dialogue_acquaintance",
            fallbackTextLux = "I'm starting to understand how you think.",
            fallbackTextNox = "...You're not as annoying as I thought.",
            oneTime = true
        });

        bondDialogues.Add(new BondDialogue
        {
            dialogueId = "bond_up_2",
            requiredBondLevel = 2,
            trigger = BondDialogue.BondDialogueTrigger.LevelUp,
            dialogueKey = "bond_dialogue_partner",
            fallbackTextLux = "Together we can do anything!",
            fallbackTextNox = "I suppose... we do make a decent team.",
            oneTime = true
        });

        bondDialogues.Add(new BondDialogue
        {
            dialogueId = "bond_up_3",
            requiredBondLevel = 3,
            trigger = BondDialogue.BondDialogueTrigger.LevelUp,
            dialogueKey = "bond_dialogue_close",
            fallbackTextLux = "I can't imagine doing this without you.",
            fallbackTextNox = "Neither can I... don't tell anyone I said that.",
            oneTime = true
        });

        bondDialogues.Add(new BondDialogue
        {
            dialogueId = "bond_up_4",
            requiredBondLevel = 4,
            trigger = BondDialogue.BondDialogueTrigger.LevelUp,
            dialogueKey = "bond_dialogue_soulbound",
            fallbackTextLux = "We are two halves of the same light.",
            fallbackTextNox = "Light and shadow... inseparable.",
            oneTime = true
        });

        bondDialogues.Add(new BondDialogue
        {
            dialogueId = "bond_up_5",
            requiredBondLevel = 5,
            trigger = BondDialogue.BondDialogueTrigger.LevelUp,
            dialogueKey = "bond_dialogue_resonance",
            fallbackTextLux = "Our souls resonate as one!",
            fallbackTextNox = "The boundary between us has vanished.",
            oneTime = true
        });

        // 复活对话
        bondDialogues.Add(new BondDialogue
        {
            dialogueId = "revive_bond2",
            requiredBondLevel = 2,
            trigger = BondDialogue.BondDialogueTrigger.Revive,
            dialogueKey = "bond_dialogue_revive",
            fallbackTextLux = "I've got you! Stay with me!",
            fallbackTextNox = "...Thanks.",
            oneTime = false
        });

        // Boss战开始对话
        bondDialogues.Add(new BondDialogue
        {
            dialogueId = "boss_bond3",
            requiredBondLevel = 3,
            trigger = BondDialogue.BondDialogueTrigger.BossEncounter,
            dialogueKey = "bond_dialogue_boss",
            fallbackTextLux = "Let's take it down together!",
            fallbackTextNox = "Stay focused. Follow my lead.",
            oneTime = false
        });

        // 隐藏区域对话
        bondDialogues.Add(new BondDialogue
        {
            dialogueId = "secret_bond2",
            requiredBondLevel = 2,
            trigger = BondDialogue.BondDialogueTrigger.SecretFound,
            dialogueKey = "bond_dialogue_secret",
            fallbackTextLux = "Look what we found! This is amazing!",
            fallbackTextNox = "Interesting... the shadows whisper here.",
            oneTime = false
        });

        // 胜利对话
        bondDialogues.Add(new BondDialogue
        {
            dialogueId = "victory_bond1",
            requiredBondLevel = 1,
            trigger = BondDialogue.BondDialogueTrigger.Victory,
            dialogueKey = "bond_dialogue_victory",
            fallbackTextLux = "We did it!",
            fallbackTextNox = "Hmph. Barely.",
            oneTime = false
        });

        // 闲置对话
        bondDialogues.Add(new BondDialogue
        {
            dialogueId = "idle_bond3",
            requiredBondLevel = 3,
            trigger = BondDialogue.BondDialogueTrigger.Idle,
            dialogueKey = "bond_dialogue_idle",
            fallbackTextLux = "It's peaceful here... I like these moments.",
            fallbackTextNox = "...The silence isn't so bad with you around.",
            oneTime = false
        });
    }
}
