using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 排行榜管理器 - 本地排行榜
/// 记录每关最佳时间、最高分、最少死亡等
/// 支持多个排名维度和好友排行
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    [Header("配置")]
    [SerializeField] private int maxEntriesPerBoard = 100;

    private const string LEADERBOARD_DIR = "Leaderboards";

    [System.Serializable]
    public class LeaderboardEntry
    {
        public string playerName;
        public int score;
        public float time;
        public int stars;
        public int deaths;
        public string date;
        public string playerId;
    }

    [System.Serializable]
    public class LeaderboardData
    {
        public string boardId;
        public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
    }

    public enum BoardType { BestTime, HighScore, LeastDeaths, MostStars }

    // 缓存已加载的排行榜
    private Dictionary<string, LeaderboardData> loadedBoards = new Dictionary<string, LeaderboardData>();

    public event System.Action<string, int> OnNewHighScore;      // boardId, rank
    public event System.Action<string, int> OnNewBestTime;       // boardId, rank

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureDirectory();
    }

    void OnEnable()
    {
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 提交成绩到排行榜
    /// </summary>
    public int SubmitScore(int chapter, int level, int score, float time, int stars, int deaths)
    {
        string boardId = GetBoardId(chapter, level);
        var board = LoadBoard(boardId);

        string playerName = GetPlayerName();
        string playerId = GetPlayerId();

        // 检查是否更新已有记录
        var existing = board.entries.FirstOrDefault(e => e.playerId == playerId);

        if (existing != null)
        {
            // 更新最佳记录
            bool updated = false;
            if (score > existing.score)
            {
                existing.score = score;
                updated = true;
            }
            if (time < existing.time || existing.time <= 0)
            {
                existing.time = time;
                updated = true;
            }
            if (stars > existing.stars)
            {
                existing.stars = stars;
                updated = true;
            }
            if (deaths < existing.deaths)
            {
                existing.deaths = deaths;
                updated = true;
            }

            if (updated)
            {
                existing.date = System.DateTime.Now.ToString("yyyy-MM-dd");
                existing.playerName = playerName;
            }
        }
        else
        {
            // 新记录
            board.entries.Add(new LeaderboardEntry
            {
                playerName = playerName,
                playerId = playerId,
                score = score,
                time = time,
                stars = stars,
                deaths = deaths,
                date = System.DateTime.Now.ToString("yyyy-MM-dd")
            });
        }

        // 按分数排序
        board.entries.Sort((a, b) => b.score.CompareTo(a.score));

        // 限制条目数
        if (board.entries.Count > maxEntriesPerBoard)
            board.entries = board.entries.Take(maxEntriesPerBoard).ToList();

        SaveBoard(board);

        // 返回排名
        int rank = board.entries.FindIndex(e => e.playerId == playerId) + 1;

        if (rank <= 3)
            OnNewHighScore?.Invoke(boardId, rank);

        return rank;
    }

    /// <summary>
    /// 获取排行榜数据
    /// </summary>
    public List<LeaderboardEntry> GetLeaderboard(int chapter, int level,
        BoardType sortBy = BoardType.HighScore, int limit = 20)
    {
        string boardId = GetBoardId(chapter, level);
        var board = LoadBoard(boardId);

        List<LeaderboardEntry> sorted;

        switch (sortBy)
        {
            case BoardType.BestTime:
                sorted = board.entries
                    .Where(e => e.time > 0)
                    .OrderBy(e => e.time)
                    .ToList();
                break;

            case BoardType.LeastDeaths:
                sorted = board.entries
                    .OrderBy(e => e.deaths)
                    .ThenByDescending(e => e.score)
                    .ToList();
                break;

            case BoardType.MostStars:
                sorted = board.entries
                    .OrderByDescending(e => e.stars)
                    .ThenByDescending(e => e.score)
                    .ToList();
                break;

            case BoardType.HighScore:
            default:
                sorted = board.entries
                    .OrderByDescending(e => e.score)
                    .ToList();
                break;
        }

        return sorted.Take(limit).ToList();
    }

    /// <summary>
    /// 获取玩家在排行榜的排名
    /// </summary>
    public int GetPlayerRank(int chapter, int level, BoardType sortBy = BoardType.HighScore)
    {
        var entries = GetLeaderboard(chapter, level, sortBy, maxEntriesPerBoard);
        string playerId = GetPlayerId();

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].playerId == playerId)
                return i + 1;
        }

        return -1; // 未上榜
    }

    /// <summary>
    /// 获取玩家的最佳记录
    /// </summary>
    public LeaderboardEntry GetPlayerBest(int chapter, int level)
    {
        string boardId = GetBoardId(chapter, level);
        var board = LoadBoard(boardId);
        string playerId = GetPlayerId();

        return board.entries.FirstOrDefault(e => e.playerId == playerId);
    }

    /// <summary>
    /// 获取全局统计（所有关卡总计）
    /// </summary>
    public GlobalStats GetGlobalStats()
    {
        var stats = new GlobalStats();
        string playerId = GetPlayerId();

        // 遍历所有关卡
        for (int ch = 1; ch <= 5; ch++)
        {
            int[] levelsPerChapter = { 4, 4, 4, 4, 4 };
            if (ch - 1 >= levelsPerChapter.Length) continue;

            for (int lv = 1; lv <= levelsPerChapter[ch - 1]; lv++)
            {
                var best = GetPlayerBest(ch, lv);
                if (best != null)
                {
                    stats.totalScore += best.score;
                    stats.totalTime += best.time;
                    stats.totalStars += best.stars;
                    stats.totalDeaths += best.deaths;
                    stats.levelsPlayed++;
                }
            }
        }

        return stats;
    }

    /// <summary>
    /// 清除指定关卡的排行榜
    /// </summary>
    public void ClearBoard(int chapter, int level)
    {
        string boardId = GetBoardId(chapter, level);
        string path = GetBoardPath(boardId);

        if (File.Exists(path))
            File.Delete(path);

        if (loadedBoards.ContainsKey(boardId))
            loadedBoards.Remove(boardId);
    }

    // ==================== 内部方法 ====================

    private void OnLevelComplete(LevelCompleteEvent e)
    {
        int deaths = 0;
        if (SaveSystem.Instance != null)
            deaths = SaveSystem.Instance.Data.totalDeaths;

        int score = e.stars * 100 + e.collectibles * 50 +
            Mathf.Max(0, 500 - Mathf.FloorToInt(e.time));

        SubmitScore(e.chapter, e.level, score, e.time, e.stars, deaths);
    }

    private string GetBoardId(int chapter, int level)
    {
        return $"ch{chapter}_lv{level}";
    }

    private string GetBoardPath(string boardId)
    {
        return Path.Combine(Application.persistentDataPath, LEADERBOARD_DIR, $"{boardId}.json");
    }

    private void EnsureDirectory()
    {
        string dir = Path.Combine(Application.persistentDataPath, LEADERBOARD_DIR);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    private LeaderboardData LoadBoard(string boardId)
    {
        if (loadedBoards.ContainsKey(boardId))
            return loadedBoards[boardId];

        string path = GetBoardPath(boardId);

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                var board = JsonUtility.FromJson<LeaderboardData>(json);
                loadedBoards[boardId] = board;
                return board;
            }
            catch
            {
                // 文件损坏
            }
        }

        var newBoard = new LeaderboardData { boardId = boardId };
        loadedBoards[boardId] = newBoard;
        return newBoard;
    }

    private void SaveBoard(LeaderboardData board)
    {
        try
        {
            string json = JsonUtility.ToJson(board, true);
            string path = GetBoardPath(board.boardId);
            File.WriteAllText(path, json);
            loadedBoards[board.boardId] = board;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Leaderboard] Save failed: {e.Message}");
        }
    }

    private string GetPlayerName()
    {
        if (SaveSystem.Instance != null && !string.IsNullOrEmpty(SaveSystem.Instance.Data.slotName))
            return SaveSystem.Instance.Data.slotName;
        return "Player";
    }

    private string GetPlayerId()
    {
        return SystemInfo.deviceUniqueIdentifier;
    }
}

/// <summary>
/// 全局统计数据
/// </summary>
public class GlobalStats
{
    public int totalScore;
    public float totalTime;
    public int totalStars;
    public int totalDeaths;
    public int levelsPlayed;
}
