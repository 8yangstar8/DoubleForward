using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 故事回顾系统 - 收集和回放已解锁的故事片段
/// 玩家可以在主菜单的"故事回顾"中重新观看已触发的剧情
/// 包含：章节间过场、Boss战前对话、结局动画、羁绊对话
/// </summary>
public class StoryRecapSystem : MonoBehaviour
{
    public static StoryRecapSystem Instance { get; private set; }

    [Header("故事条目")]
    [SerializeField] private List<StoryEntry> storyEntries = new List<StoryEntry>();

    // 持久化
    private const string UNLOCKED_STORIES_KEY = "unlocked_stories";
    private HashSet<string> unlockedStories = new HashSet<string>();

    public int TotalEntries => storyEntries.Count;
    public int UnlockedCount => unlockedStories.Count;
    public float CompletionPercent => storyEntries.Count > 0
        ? unlockedStories.Count / (float)storyEntries.Count * 100f : 0;

    public event System.Action<string> OnStoryUnlocked; // storyId

    [System.Serializable]
    public class StoryEntry
    {
        public string storyId;
        public string titleKey;             // 本地化key
        public string titleFallback;        // 默认标题
        public StoryCategory category;
        public int chapter;                 // 所属章节 (0=通用)
        public int sortOrder;               // 排序优先级
        public Sprite thumbnail;            // 缩略图
        public StoryContentType contentType;
        public string contentData;          // 对话序列ID / 过场动画名 / 结局类型

        public enum StoryCategory
        {
            ChapterIntro,       // 章节开场
            ChapterOutro,       // 章节结尾
            BossIntro,          // Boss战前
            BossDefeat,         // Boss击败后
            BondDialogue,       // 羁绊对话
            SecretLore,         // 隐藏传说
            Ending,             // 结局
            SpecialEvent        // 特殊事件
        }

        public enum StoryContentType
        {
            Dialogue,           // 对话序列
            Cutscene,           // 过场动画
            Illustration,       // 插图 + 文字
            Ending              // 结局演出
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadUnlockedStories();
        InitializeDefaultEntries();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 解锁故事条目
    /// </summary>
    public void UnlockStory(string storyId)
    {
        if (unlockedStories.Contains(storyId)) return;

        unlockedStories.Add(storyId);
        SaveUnlockedStories();

        OnStoryUnlocked?.Invoke(storyId);
        Debug.Log($"[StoryRecap] Unlocked: {storyId}");
    }

    /// <summary>
    /// 检查是否已解锁
    /// </summary>
    public bool IsStoryUnlocked(string storyId)
    {
        return unlockedStories.Contains(storyId);
    }

    /// <summary>
    /// 获取指定章节的故事条目
    /// </summary>
    public List<StoryEntry> GetEntriesByChapter(int chapter)
    {
        var result = storyEntries.FindAll(e => e.chapter == chapter || e.chapter == 0);
        result.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        return result;
    }

    /// <summary>
    /// 获取指定分类的故事条目
    /// </summary>
    public List<StoryEntry> GetEntriesByCategory(StoryEntry.StoryCategory category)
    {
        var result = storyEntries.FindAll(e => e.category == category);
        result.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        return result;
    }

    /// <summary>
    /// 获取所有已解锁条目
    /// </summary>
    public List<StoryEntry> GetUnlockedEntries()
    {
        var result = storyEntries.FindAll(e => unlockedStories.Contains(e.storyId));
        result.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        return result;
    }

    /// <summary>
    /// 获取所有条目（包含锁定状态）
    /// </summary>
    public List<(StoryEntry entry, bool unlocked)> GetAllEntriesWithStatus()
    {
        var result = new List<(StoryEntry, bool)>();
        var sorted = new List<StoryEntry>(storyEntries);
        sorted.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));

        foreach (var entry in sorted)
        {
            result.Add((entry, unlockedStories.Contains(entry.storyId)));
        }
        return result;
    }

    /// <summary>
    /// 播放故事条目
    /// </summary>
    public void PlayStory(string storyId)
    {
        var entry = storyEntries.Find(e => e.storyId == storyId);
        if (entry == null || !unlockedStories.Contains(storyId)) return;

        switch (entry.contentType)
        {
            case StoryEntry.StoryContentType.Dialogue:
                PlayDialogueStory(entry);
                break;
            case StoryEntry.StoryContentType.Cutscene:
                PlayCutsceneStory(entry);
                break;
            case StoryEntry.StoryContentType.Ending:
                PlayEndingStory(entry);
                break;
            case StoryEntry.StoryContentType.Illustration:
                PlayIllustrationStory(entry);
                break;
        }

        if (AnalyticsTracker.Instance != null)
            AnalyticsTracker.Instance.TrackEvent("story_replay", ("storyId", storyId));
    }

    // ==================== 播放方法 ====================

    private void PlayDialogueStory(StoryEntry entry)
    {
        // 通过EventBus通知UI层播放对话
        EventBus.Publish(new HintRequestEvent
        {
            textKey = entry.titleKey,
            fallbackText = entry.titleFallback,
            duration = 2f
        });

        // 实际对话播放需要DialogueSystem
        // contentData包含对话序列的ID
        Debug.Log($"[StoryRecap] Playing dialogue: {entry.contentData}");
    }

    private void PlayCutsceneStory(StoryEntry entry)
    {
        // contentData包含过场动画名称
        Debug.Log($"[StoryRecap] Playing cutscene: {entry.contentData}");
    }

    private void PlayEndingStory(StoryEntry entry)
    {
        // contentData包含结局类型
        if (GameEndingManager.Instance != null)
        {
            Debug.Log($"[StoryRecap] Playing ending: {entry.contentData}");
        }
    }

    private void PlayIllustrationStory(StoryEntry entry)
    {
        Debug.Log($"[StoryRecap] Playing illustration: {entry.contentData}");
    }

    // ==================== 持久化 ====================

    private void LoadUnlockedStories()
    {
        string json = PlayerPrefs.GetString(UNLOCKED_STORIES_KEY, "");
        if (!string.IsNullOrEmpty(json))
        {
            string[] ids = json.Split(',');
            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(id))
                    unlockedStories.Add(id);
            }
        }
    }

    private void SaveUnlockedStories()
    {
        PlayerPrefs.SetString(UNLOCKED_STORIES_KEY, string.Join(",", unlockedStories));
        PlayerPrefs.Save();
    }

    // ==================== 默认条目 ====================

    private void InitializeDefaultEntries()
    {
        if (storyEntries.Count > 0) return;

        // === 章节1：光明森林 ===
        storyEntries.Add(new StoryEntry
        {
            storyId = "ch1_intro",
            titleKey = "story_ch1_intro",
            titleFallback = "The Awakening",
            category = StoryEntry.StoryCategory.ChapterIntro,
            chapter = 1, sortOrder = 100,
            contentType = StoryEntry.StoryContentType.Cutscene,
            contentData = "cutscene_ch1_intro"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "ch1_boss_intro",
            titleKey = "story_ch1_boss",
            titleFallback = "Forest Guardian Awakens",
            category = StoryEntry.StoryCategory.BossIntro,
            chapter = 1, sortOrder = 150,
            contentType = StoryEntry.StoryContentType.Dialogue,
            contentData = "dialogue_boss1_intro"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "ch1_boss_defeat",
            titleKey = "story_ch1_boss_defeat",
            titleFallback = "The Guardian's Rest",
            category = StoryEntry.StoryCategory.BossDefeat,
            chapter = 1, sortOrder = 160,
            contentType = StoryEntry.StoryContentType.Dialogue,
            contentData = "dialogue_boss1_defeat"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "ch1_outro",
            titleKey = "story_ch1_outro",
            titleFallback = "Beyond the Trees",
            category = StoryEntry.StoryCategory.ChapterOutro,
            chapter = 1, sortOrder = 190,
            contentType = StoryEntry.StoryContentType.Cutscene,
            contentData = "cutscene_ch1_outro"
        });

        // === 章节2：水晶洞穴 ===
        storyEntries.Add(new StoryEntry
        {
            storyId = "ch2_intro",
            titleKey = "story_ch2_intro",
            titleFallback = "Into the Depths",
            category = StoryEntry.StoryCategory.ChapterIntro,
            chapter = 2, sortOrder = 200,
            contentType = StoryEntry.StoryContentType.Cutscene,
            contentData = "cutscene_ch2_intro"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "ch2_boss_intro",
            titleKey = "story_ch2_boss",
            titleFallback = "The Crystal Golem",
            category = StoryEntry.StoryCategory.BossIntro,
            chapter = 2, sortOrder = 250,
            contentType = StoryEntry.StoryContentType.Dialogue,
            contentData = "dialogue_boss2_intro"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "ch2_outro",
            titleKey = "story_ch2_outro",
            titleFallback = "Echoes of Light",
            category = StoryEntry.StoryCategory.ChapterOutro,
            chapter = 2, sortOrder = 290,
            contentType = StoryEntry.StoryContentType.Cutscene,
            contentData = "cutscene_ch2_outro"
        });

        // === 章节3：深渊之海 ===
        storyEntries.Add(new StoryEntry
        {
            storyId = "ch3_intro",
            titleKey = "story_ch3_intro",
            titleFallback = "The Sunken World",
            category = StoryEntry.StoryCategory.ChapterIntro,
            chapter = 3, sortOrder = 300,
            contentType = StoryEntry.StoryContentType.Cutscene,
            contentData = "cutscene_ch3_intro"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "ch3_boss_intro",
            titleKey = "story_ch3_boss",
            titleFallback = "Void Serpent Rises",
            category = StoryEntry.StoryCategory.BossIntro,
            chapter = 3, sortOrder = 350,
            contentType = StoryEntry.StoryContentType.Dialogue,
            contentData = "dialogue_boss3_intro"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "ch3_outro",
            titleKey = "story_ch3_outro",
            titleFallback = "Breaking the Surface",
            category = StoryEntry.StoryCategory.ChapterOutro,
            chapter = 3, sortOrder = 390,
            contentType = StoryEntry.StoryContentType.Cutscene,
            contentData = "cutscene_ch3_outro"
        });

        // === 章节4：天空之城 ===
        storyEntries.Add(new StoryEntry
        {
            storyId = "ch4_intro",
            titleKey = "story_ch4_intro",
            titleFallback = "Above the Clouds",
            category = StoryEntry.StoryCategory.ChapterIntro,
            chapter = 4, sortOrder = 400,
            contentType = StoryEntry.StoryContentType.Cutscene,
            contentData = "cutscene_ch4_intro"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "ch4_boss_intro",
            titleKey = "story_ch4_boss",
            titleFallback = "The Sky Warden",
            category = StoryEntry.StoryCategory.BossIntro,
            chapter = 4, sortOrder = 450,
            contentType = StoryEntry.StoryContentType.Dialogue,
            contentData = "dialogue_boss4_intro"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "ch4_outro",
            titleKey = "story_ch4_outro",
            titleFallback = "The Final Gate",
            category = StoryEntry.StoryCategory.ChapterOutro,
            chapter = 4, sortOrder = 490,
            contentType = StoryEntry.StoryContentType.Cutscene,
            contentData = "cutscene_ch4_outro"
        });

        // === 章节5：黄昏圣殿 ===
        storyEntries.Add(new StoryEntry
        {
            storyId = "ch5_intro",
            titleKey = "story_ch5_intro",
            titleFallback = "The Twilight Throne",
            category = StoryEntry.StoryCategory.ChapterIntro,
            chapter = 5, sortOrder = 500,
            contentType = StoryEntry.StoryContentType.Cutscene,
            contentData = "cutscene_ch5_intro"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "ch5_boss_intro",
            titleKey = "story_ch5_boss",
            titleFallback = "The Twilight King",
            category = StoryEntry.StoryCategory.BossIntro,
            chapter = 5, sortOrder = 550,
            contentType = StoryEntry.StoryContentType.Dialogue,
            contentData = "dialogue_boss5_intro"
        });

        // === 结局 ===
        storyEntries.Add(new StoryEntry
        {
            storyId = "ending_normal",
            titleKey = "story_ending_normal",
            titleFallback = "A New Dawn",
            category = StoryEntry.StoryCategory.Ending,
            chapter = 0, sortOrder = 900,
            contentType = StoryEntry.StoryContentType.Ending,
            contentData = "Normal"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "ending_good",
            titleKey = "story_ending_good",
            titleFallback = "United in Light",
            category = StoryEntry.StoryCategory.Ending,
            chapter = 0, sortOrder = 910,
            contentType = StoryEntry.StoryContentType.Ending,
            contentData = "Good"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "ending_perfect",
            titleKey = "story_ending_perfect",
            titleFallback = "Eternal Harmony",
            category = StoryEntry.StoryCategory.Ending,
            chapter = 0, sortOrder = 920,
            contentType = StoryEntry.StoryContentType.Ending,
            contentData = "Perfect"
        });

        // === 隐藏传说 ===
        storyEntries.Add(new StoryEntry
        {
            storyId = "lore_origin",
            titleKey = "story_lore_origin",
            titleFallback = "The Origin of Light and Shadow",
            category = StoryEntry.StoryCategory.SecretLore,
            chapter = 0, sortOrder = 800,
            contentType = StoryEntry.StoryContentType.Illustration,
            contentData = "lore_origin"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "lore_eclipse",
            titleKey = "story_lore_eclipse",
            titleFallback = "The Eclipse Prophecy",
            category = StoryEntry.StoryCategory.SecretLore,
            chapter = 0, sortOrder = 810,
            contentType = StoryEntry.StoryContentType.Illustration,
            contentData = "lore_eclipse"
        });

        storyEntries.Add(new StoryEntry
        {
            storyId = "lore_harmony",
            titleKey = "story_lore_harmony",
            titleFallback = "The Harmony Stone",
            category = StoryEntry.StoryCategory.SecretLore,
            chapter = 0, sortOrder = 820,
            contentType = StoryEntry.StoryContentType.Illustration,
            contentData = "lore_harmony"
        });
    }
}
