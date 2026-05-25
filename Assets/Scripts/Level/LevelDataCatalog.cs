using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 关卡目录 - 集中管理所有20个关卡的配置
/// 提供按章/关查询、解锁状态、顺序遍历等功能
/// 与SaveSystem联动确定关卡解锁进度
/// </summary>
[CreateAssetMenu(fileName = "LevelDataCatalog", menuName = "DoubleForward/Level Data Catalog")]
public class LevelDataCatalog : ScriptableObject
{
    [System.Serializable]
    public class ChapterInfo
    {
        public int chapterIndex;
        public string chapterName;
        public string chapterNameKey; // 本地化键
        public string worldTheme;    // 世界主题描述
        public Sprite chapterIcon;
        public Color themeColor = Color.white;
        public List<LevelEntry> levels = new List<LevelEntry>();
    }

    [System.Serializable]
    public class LevelEntry
    {
        public LevelData levelData;         // 引用LevelData ScriptableObject
        public LevelConfig levelConfig;     // 引用LevelConfig ScriptableObject
        public string sceneName;            // 场景名称（冗余字段方便查询）
        public bool isBossLevel;
        public bool isOptional;             // 可选关卡（隐藏关卡）
        public int unlockRequirement;       // 需要收集的星星数量才能解锁

        [Header("显示信息")]
        public string levelNameKey;         // 本地化关卡名
        public Sprite thumbnail;            // 关卡缩略图
        public Sprite backgroundPreview;    // 关卡预览背景

        [Header("奖励")]
        public int coinReward = 100;
        public string specialRewardId;      // 首次完成特殊奖励
    }

    [Header("章节配置")]
    [SerializeField] private List<ChapterInfo> chapters = new List<ChapterInfo>();

    [Header("全局设置")]
    [SerializeField] private int starsPerLevel = 3;
    [SerializeField] private int levelsPerChapter = 4;

    // 运行时缓存
    private Dictionary<string, LevelEntry> levelMap;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        levelMap = new Dictionary<string, LevelEntry>();
        foreach (var chapter in chapters)
        {
            foreach (var level in chapter.levels)
            {
                string key = $"{chapter.chapterIndex}_{level.levelData?.levelIndex ?? 0}";
                levelMap[key] = level;
            }
        }
    }

    /// <summary>
    /// 初始化默认的20关卡目录结构
    /// </summary>
    public void InitializeDefaults()
    {
        if (chapters.Count > 0) return;

        chapters = new List<ChapterInfo>
        {
            CreateChapter(1, "光影遗迹", "chapter_1_name", "古代遗迹中的光与影", new Color(1f, 0.9f, 0.5f),
                new[] { "觉醒之路", "影中之门", "光之试炼", "石像守卫" },
                new[] { false, false, false, true },
                new[] { 120f, 120f, 150f, 180f },
                new[] { 5, 5, 8, 3 }),

            CreateChapter(2, "冰火熔炉", "chapter_2_name", "冰与火交织的矿洞", new Color(0.5f, 0.8f, 1f),
                new[] { "双面之境", "冰封回廊", "熔岩渡口", "冰火巨像" },
                new[] { false, false, false, true },
                new[] { 150f, 150f, 180f, 210f },
                new[] { 6, 8, 8, 3 }),

            CreateChapter(3, "沙漠风暴", "chapter_3_name", "荒漠中的古老机关", new Color(1f, 0.85f, 0.4f),
                new[] { "流沙之谷", "风暴神殿", "齿轮迷宫", "沙暴巨蟒" },
                new[] { false, false, false, true },
                new[] { 150f, 180f, 210f, 240f },
                new[] { 8, 8, 10, 3 }),

            CreateChapter(4, "深渊暗流", "chapter_4_name", "海底深渊的黑暗秘密", new Color(0.3f, 0.3f, 0.8f),
                new[] { "暗潮入口", "深海遗城", "光暗漩涡", "深渊巨兽" },
                new[] { false, false, false, true },
                new[] { 180f, 210f, 240f, 300f },
                new[] { 8, 10, 10, 5 }),

            CreateChapter(5, "天空之巅", "chapter_5_name", "光暗合一的最终之战", new Color(1f, 1f, 1f),
                new[] { "云端阶梯", "虚空走廊", "光暗祭坛", "最终合一" },
                new[] { false, false, false, true },
                new[] { 210f, 240f, 270f, 360f },
                new[] { 10, 10, 12, 5 })
        };
    }

    private ChapterInfo CreateChapter(int index, string name, string nameKey, string theme, Color color,
        string[] levelNames, bool[] bossFlags, float[] parTimes, int[] collectibles)
    {
        var chapter = new ChapterInfo
        {
            chapterIndex = index,
            chapterName = name,
            chapterNameKey = nameKey,
            worldTheme = theme,
            themeColor = color
        };

        for (int i = 0; i < levelNames.Length; i++)
        {
            chapter.levels.Add(new LevelEntry
            {
                sceneName = $"Ch{index}_Lv{i + 1}",
                isBossLevel = bossFlags[i],
                levelNameKey = $"level_{index}_{i + 1}_name",
                coinReward = 100 + (index - 1) * 50 + i * 25,
                unlockRequirement = i == 0 ? 0 : (index - 1) * 12 + i * 3 // 前面章节的星星
            });
        }

        return chapter;
    }

    // ==================== 查询接口 ====================

    /// <summary>
    /// 获取指定章节和关卡的条目
    /// </summary>
    public LevelEntry GetLevel(int chapter, int level)
    {
        if (levelMap == null) BuildLookup();
        string key = $"{chapter}_{level}";
        return levelMap.TryGetValue(key, out var entry) ? entry : null;
    }

    /// <summary>
    /// 获取指定章节信息
    /// </summary>
    public ChapterInfo GetChapter(int chapterIndex)
    {
        return chapters.Find(c => c.chapterIndex == chapterIndex);
    }

    /// <summary>
    /// 获取所有章节
    /// </summary>
    public List<ChapterInfo> GetAllChapters() => chapters;

    /// <summary>
    /// 总关卡数
    /// </summary>
    public int TotalLevels
    {
        get
        {
            int count = 0;
            foreach (var ch in chapters)
                count += ch.levels.Count;
            return count;
        }
    }

    /// <summary>
    /// 总可获得星数
    /// </summary>
    public int TotalStars => TotalLevels * starsPerLevel;

    /// <summary>
    /// 获取下一关信息
    /// </summary>
    public LevelEntry GetNextLevel(int currentChapter, int currentLevel)
    {
        var chapter = GetChapter(currentChapter);
        if (chapter == null) return null;

        int nextLevel = currentLevel + 1;
        if (nextLevel <= chapter.levels.Count)
        {
            return chapter.levels[nextLevel - 1];
        }
        else
        {
            // 进入下一章
            var nextChapter = GetChapter(currentChapter + 1);
            if (nextChapter != null && nextChapter.levels.Count > 0)
                return nextChapter.levels[0];
        }

        return null; // 已通关
    }

    /// <summary>
    /// 检查关卡是否解锁
    /// </summary>
    public bool IsLevelUnlocked(int chapter, int level)
    {
        var entry = GetLevel(chapter, level);
        if (entry == null) return false;
        if (entry.unlockRequirement <= 0) return true;

        // 需要SaveSystem获取总星星数
        if (SaveSystem.Instance != null)
            return SaveSystem.Instance.Data.totalStars >= entry.unlockRequirement;

        return chapter == 1 && level == 1; // 无存档系统时只解锁1-1
    }

    /// <summary>
    /// 根据场景名查找关卡
    /// </summary>
    public LevelEntry FindByScene(string sceneName)
    {
        foreach (var ch in chapters)
        {
            var found = ch.levels.Find(l => l.sceneName == sceneName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// 获取章节完成百分比
    /// </summary>
    public float GetChapterProgress(int chapterIndex)
    {
        var chapter = GetChapter(chapterIndex);
        if (chapter == null || chapter.levels.Count == 0) return 0;

        if (SaveSystem.Instance == null) return 0;

        int completed = 0;
        for (int i = 0; i < chapter.levels.Count; i++)
        {
            if (SaveSystem.Instance.IsLevelCompleted(chapterIndex, i + 1))
                completed++;
        }

        return (float)completed / chapter.levels.Count;
    }
}
