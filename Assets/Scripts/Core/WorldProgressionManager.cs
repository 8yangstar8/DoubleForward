using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 世界进度管理器 - 管理5个世界的解锁条件和进度追踪
/// 星级门槛、隐藏关卡解锁、章节特殊奖励
/// 与SaveSystem和WorldMapUI配合使用
/// </summary>
public class WorldProgressionManager : MonoBehaviour
{
    public static WorldProgressionManager Instance { get; private set; }

    [Header("章节配置")]
    [SerializeField] private ChapterConfig[] chapters;

    [Header("解锁通知")]
    [SerializeField] private float unlockCheckDelay = 0.5f;

    // 缓存
    private Dictionary<int, ChapterProgress> progressCache;
    private HashSet<string> notifiedUnlocks;
    private const string NOTIFIED_KEY = "notified_world_unlocks";

    [System.Serializable]
    public class ChapterConfig
    {
        public int chapter;
        public string nameKey = "";
        public int totalLevels = 4;
        public int starsToUnlock;           // 需要的总星数来解锁此章节（0=第一章默认解锁）
        public bool requirePreviousBoss;    // 是否需要前一章Boss通关
        public int bonusLevelStarReq;       // 隐藏奖励关卡所需星数
        public bool hasBonusLevel;          // 是否有隐藏关卡
    }

    [System.Serializable]
    public class ChapterProgress
    {
        public int chapter;
        public bool isUnlocked;
        public int completedLevels;
        public int totalStars;
        public int maxStars;
        public float completionPercent;
        public bool bossDefeated;
        public bool bonusUnlocked;
        public bool allStarsCollected;
    }

    public event System.Action<int> OnWorldUnlocked;            // chapter
    public event System.Action<int> OnBonusLevelUnlocked;       // chapter
    public event System.Action<int, ChapterProgress> OnProgressUpdated; // chapter, progress

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        progressCache = new Dictionary<int, ChapterProgress>();
        notifiedUnlocks = new HashSet<string>();

        LoadNotifiedUnlocks();
        InitializeDefaultChapters();
    }

    void Start()
    {
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);

        // 初始计算
        RefreshAllProgress();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        if (Instance == this) Instance = null;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 检查章节是否已解锁
    /// </summary>
    public bool IsChapterUnlocked(int chapter)
    {
        if (chapter <= 1) return true;

        var config = GetConfig(chapter);
        if (config == null) return false;

        // 检查星数要求
        int totalStars = GetTotalStarsEarned();
        if (totalStars < config.starsToUnlock) return false;

        // 检查前一章Boss要求
        if (config.requirePreviousBoss)
        {
            if (!IsBossDefeated(chapter - 1)) return false;
        }

        return true;
    }

    /// <summary>
    /// 检查某关卡是否已解锁
    /// </summary>
    public bool IsLevelUnlocked(int chapter, int level)
    {
        if (!IsChapterUnlocked(chapter)) return false;

        // 第一关总是解锁的
        if (level <= 1) return true;

        // 需要完成前一关
        return SaveSystem.Instance?.IsLevelCompleted(chapter, level - 1) ?? false;
    }

    /// <summary>
    /// 检查隐藏关卡是否已解锁
    /// </summary>
    public bool IsBonusLevelUnlocked(int chapter)
    {
        var config = GetConfig(chapter);
        if (config == null || !config.hasBonusLevel) return false;

        int chapterStars = GetChapterStars(chapter);
        return chapterStars >= config.bonusLevelStarReq;
    }

    /// <summary>
    /// 获取章节进度
    /// </summary>
    public ChapterProgress GetChapterProgress(int chapter)
    {
        if (progressCache.ContainsKey(chapter))
            return progressCache[chapter];

        return CalculateChapterProgress(chapter);
    }

    /// <summary>
    /// 获取总星数
    /// </summary>
    public int GetTotalStarsEarned()
    {
        if (SaveSystem.Instance == null) return 0;
        return SaveSystem.Instance.Data.totalStars;
    }

    /// <summary>
    /// 获取某章节获得的星数
    /// </summary>
    public int GetChapterStars(int chapter)
    {
        if (SaveSystem.Instance == null) return 0;

        var config = GetConfig(chapter);
        if (config == null) return 0;

        int stars = 0;
        for (int level = 1; level <= config.totalLevels; level++)
        {
            stars += SaveSystem.Instance.GetLevelStars(chapter, level);
        }
        return stars;
    }

    /// <summary>
    /// Boss是否已被击败
    /// </summary>
    public bool IsBossDefeated(int chapter)
    {
        var config = GetConfig(chapter);
        if (config == null) return false;

        // Boss是每章的最后一关
        return SaveSystem.Instance?.IsLevelCompleted(chapter, config.totalLevels) ?? false;
    }

    /// <summary>
    /// 获取下一个可玩关卡
    /// </summary>
    public (int chapter, int level) GetNextPlayableLevel()
    {
        if (SaveSystem.Instance == null) return (1, 1);

        for (int ch = 1; ch <= chapters.Length; ch++)
        {
            if (!IsChapterUnlocked(ch)) continue;

            var config = GetConfig(ch);
            if (config == null) continue;

            for (int lv = 1; lv <= config.totalLevels; lv++)
            {
                if (!SaveSystem.Instance.IsLevelCompleted(ch, lv))
                    return (ch, lv);
            }
        }

        // 全部完成
        return (1, 1);
    }

    /// <summary>
    /// 获取整体完成度（百分比）
    /// </summary>
    public float GetOverallCompletion()
    {
        int totalLevels = 0;
        int completedLevels = 0;
        int totalStars = 0;
        int maxStars = 0;

        foreach (var config in chapters)
        {
            totalLevels += config.totalLevels;
            maxStars += config.totalLevels * 3;

            for (int lv = 1; lv <= config.totalLevels; lv++)
            {
                if (SaveSystem.Instance != null && SaveSystem.Instance.IsLevelCompleted(config.chapter, lv))
                    completedLevels++;
                if (SaveSystem.Instance != null)
                    totalStars += SaveSystem.Instance.GetLevelStars(config.chapter, lv);
            }
        }

        if (totalLevels == 0) return 0f;

        float levelPercent = (float)completedLevels / totalLevels;
        float starPercent = maxStars > 0 ? (float)totalStars / maxStars : 0f;
        return (levelPercent * 0.6f + starPercent * 0.4f) * 100f;
    }

    /// <summary>
    /// 刷新所有进度缓存
    /// </summary>
    public void RefreshAllProgress()
    {
        progressCache.Clear();

        foreach (var config in chapters)
        {
            var progress = CalculateChapterProgress(config.chapter);
            progressCache[config.chapter] = progress;
        }

        // 检查解锁通知
        CheckUnlockNotifications();
    }

    // ==================== 事件处理 ====================

    private void OnLevelComplete(LevelCompleteEvent e)
    {
        Invoke(nameof(DelayedRefresh), unlockCheckDelay);
    }

    private void OnBossDefeated(BossDefeatedEvent e)
    {
        Invoke(nameof(DelayedRefresh), unlockCheckDelay);
    }

    private void DelayedRefresh()
    {
        RefreshAllProgress();
    }

    // ==================== 内部逻辑 ====================

    private ChapterProgress CalculateChapterProgress(int chapter)
    {
        var config = GetConfig(chapter);
        var progress = new ChapterProgress
        {
            chapter = chapter,
            isUnlocked = IsChapterUnlocked(chapter),
            maxStars = config != null ? config.totalLevels * 3 : 0
        };

        if (config == null || SaveSystem.Instance == null) return progress;

        for (int lv = 1; lv <= config.totalLevels; lv++)
        {
            if (SaveSystem.Instance.IsLevelCompleted(chapter, lv))
                progress.completedLevels++;
            progress.totalStars += SaveSystem.Instance.GetLevelStars(chapter, lv);
        }

        progress.bossDefeated = IsBossDefeated(chapter);
        progress.bonusUnlocked = IsBonusLevelUnlocked(chapter);
        progress.allStarsCollected = progress.totalStars >= progress.maxStars;

        if (config.totalLevels > 0)
            progress.completionPercent = (float)progress.completedLevels / config.totalLevels * 100f;

        return progress;
    }

    private void CheckUnlockNotifications()
    {
        foreach (var config in chapters)
        {
            // 世界解锁通知
            string worldKey = $"world_{config.chapter}";
            if (IsChapterUnlocked(config.chapter) && !notifiedUnlocks.Contains(worldKey))
            {
                // 第一章默认解锁不通知
                if (config.chapter > 1)
                {
                    notifiedUnlocks.Add(worldKey);
                    OnWorldUnlocked?.Invoke(config.chapter);

                    // 通过EventBus发送提示（避免Core→UI跨程序集引用）
                    string chapterName = config.nameKey;
                    if (LocalizationSystem.Instance != null)
                        chapterName = LocalizationSystem.Instance.GetText(config.nameKey);
                    EventBus.Publish(new HintRequestEvent
                    {
                        textKey = $"world_unlocked_{config.chapter}",
                        fallbackText = $"新世界已解锁：{chapterName}！",
                        duration = 4f
                    });
                }
            }

            // 隐藏关卡解锁通知
            string bonusKey = $"bonus_{config.chapter}";
            if (config.hasBonusLevel && IsBonusLevelUnlocked(config.chapter) && !notifiedUnlocks.Contains(bonusKey))
            {
                notifiedUnlocks.Add(bonusKey);
                OnBonusLevelUnlocked?.Invoke(config.chapter);
            }
        }

        SaveNotifiedUnlocks();
    }

    private ChapterConfig GetConfig(int chapter)
    {
        if (chapters == null) return null;
        foreach (var config in chapters)
        {
            if (config.chapter == chapter) return config;
        }
        return null;
    }

    private void InitializeDefaultChapters()
    {
        if (chapters != null && chapters.Length > 0) return;

        chapters = new ChapterConfig[]
        {
            new ChapterConfig
            {
                chapter = 1, nameKey = "chapter_light_forest",
                totalLevels = 3, starsToUnlock = 0,
                requirePreviousBoss = false, hasBonusLevel = false
            },
            new ChapterConfig
            {
                chapter = 2, nameKey = "chapter_crystal_cave",
                totalLevels = 4, starsToUnlock = 5,
                requirePreviousBoss = true, hasBonusLevel = true,
                bonusLevelStarReq = 10
            },
            new ChapterConfig
            {
                chapter = 3, nameKey = "chapter_deep_abyss",
                totalLevels = 4, starsToUnlock = 15,
                requirePreviousBoss = true, hasBonusLevel = true,
                bonusLevelStarReq = 20
            },
            new ChapterConfig
            {
                chapter = 4, nameKey = "chapter_sky_citadel",
                totalLevels = 5, starsToUnlock = 25,
                requirePreviousBoss = true, hasBonusLevel = true,
                bonusLevelStarReq = 35
            },
            new ChapterConfig
            {
                chapter = 5, nameKey = "chapter_twilight_realm",
                totalLevels = 4, starsToUnlock = 40,
                requirePreviousBoss = true, hasBonusLevel = true,
                bonusLevelStarReq = 50
            }
        };
    }

    private void SaveNotifiedUnlocks()
    {
        string data = string.Join(",", notifiedUnlocks);
        PlayerPrefs.SetString(NOTIFIED_KEY, data);
    }

    private void LoadNotifiedUnlocks()
    {
        string data = PlayerPrefs.GetString(NOTIFIED_KEY, "");
        if (!string.IsNullOrEmpty(data))
        {
            foreach (var key in data.Split(','))
                notifiedUnlocks.Add(key);
        }
    }
}
