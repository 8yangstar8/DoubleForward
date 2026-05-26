using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 游戏统计数据与本地排行榜
/// 追踪总游玩时间、击杀数、收集品、最佳记录等
/// </summary>
public class GameStats : MonoBehaviour
{
    public static GameStats Instance { get; private set; }

    [System.Serializable]
    public class StatsData
    {
        // 总计统计
        public float totalPlayTime;
        public int totalDeaths;
        public int totalEnemiesDefeated;
        public int totalCollectiblesFound;
        public int totalLevelsCompleted;
        public int totalBossesDefeated;
        public int highestCombo;
        public int totalJumps;
        public int totalDashes;
        public int totalAbilitiesUsed;
        public float totalDistanceTraveled;

        // Lux 统计
        public int luxLightBeamUses;
        public int luxLightBridgeUses;
        public int luxDoubleJumps;

        // Nox 统计
        public int noxShadowPhaseUses;
        public int noxShadowZoneUses;
        public int noxDashCount;

        // 每关最佳记录
        public List<LevelRecord> levelRecords = new List<LevelRecord>();
    }

    [System.Serializable]
    public class LevelRecord
    {
        public string levelId;
        public float bestTime;
        public int bestScore;
        public int bestStars;
        public int bestCombo;
        public int completionCount;
    }

    private StatsData stats = new StatsData();
    private float sessionStartTime;
    private Vector3 lastPlayerPos;
    private const string STATS_SAVE_KEY = "game_stats";

    public StatsData Stats => stats;
    public int BestCombo => stats.highestCombo;
    public int TotalDeaths => stats.totalDeaths;
    public int TotalEnemiesDefeated => stats.totalEnemiesDefeated;
    public int TotalCollectiblesFound => stats.totalCollectiblesFound;
    public int TotalLevelsCompleted => stats.totalLevelsCompleted;
    public float TotalPlayTime => stats.totalPlayTime + (Time.time - sessionStartTime);

    public event System.Action<string, int> OnStatChanged; // statName, newValue

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadStats();
        sessionStartTime = Time.time;
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) SaveStats();
    }

    void OnApplicationQuit()
    {
        stats.totalPlayTime += Time.time - sessionStartTime;
        SaveStats();
    }

    // ============ 追踪方法 ============

    public void RecordDeath()
    {
        stats.totalDeaths++;
        OnStatChanged?.Invoke("deaths", stats.totalDeaths);
    }

    public void RecordEnemyKill()
    {
        stats.totalEnemiesDefeated++;
        OnStatChanged?.Invoke("enemies_defeated", stats.totalEnemiesDefeated);
    }

    public void RecordCollectible()
    {
        stats.totalCollectiblesFound++;
        OnStatChanged?.Invoke("collectibles", stats.totalCollectiblesFound);
    }

    public void RecordJump()
    {
        stats.totalJumps++;
    }

    public void RecordDash()
    {
        stats.totalDashes++;
        stats.noxDashCount++;
    }

    public void RecordAbilityUse(string abilityName)
    {
        stats.totalAbilitiesUsed++;

        switch (abilityName)
        {
            case "light_beam": stats.luxLightBeamUses++; break;
            case "light_bridge": stats.luxLightBridgeUses++; break;
            case "double_jump": stats.luxDoubleJumps++; break;
            case "shadow_phase": stats.noxShadowPhaseUses++; break;
            case "shadow_zone": stats.noxShadowZoneUses++; break;
        }
    }

    public void RecordCombo(int combo)
    {
        if (combo > stats.highestCombo)
        {
            stats.highestCombo = combo;
            OnStatChanged?.Invoke("highest_combo", stats.highestCombo);
        }
    }

    public void RecordBossDefeat()
    {
        stats.totalBossesDefeated++;
        OnStatChanged?.Invoke("bosses_defeated", stats.totalBossesDefeated);
    }

    public void RecordDistance(float distance)
    {
        stats.totalDistanceTraveled += distance;
    }

    /// <summary>
    /// 记录关卡完成
    /// </summary>
    public void RecordLevelComplete(string levelId, float time, int score, int stars, int combo)
    {
        stats.totalLevelsCompleted++;

        var record = stats.levelRecords.Find(r => r.levelId == levelId);
        if (record == null)
        {
            record = new LevelRecord { levelId = levelId };
            stats.levelRecords.Add(record);
        }

        record.completionCount++;

        if (time < record.bestTime || record.bestTime <= 0)
            record.bestTime = time;
        if (score > record.bestScore)
            record.bestScore = score;
        if (stars > record.bestStars)
            record.bestStars = stars;
        if (combo > record.bestCombo)
            record.bestCombo = combo;

        SaveStats();
    }

    /// <summary>
    /// 获取关卡排行（按分数排序）
    /// </summary>
    public List<LevelRecord> GetLeaderboard(int topN = 10)
    {
        var sorted = new List<LevelRecord>(stats.levelRecords);
        sorted.Sort((a, b) => b.bestScore.CompareTo(a.bestScore));
        if (sorted.Count > topN)
            sorted.RemoveRange(topN, sorted.Count - topN);
        return sorted;
    }

    /// <summary>
    /// 获取指定关卡记录
    /// </summary>
    public LevelRecord GetLevelRecord(string levelId)
    {
        return stats.levelRecords.Find(r => r.levelId == levelId);
    }

    /// <summary>
    /// 获取格式化统计文本
    /// </summary>
    public string GetFormattedStats()
    {
        float totalTime = stats.totalPlayTime + (Time.time - sessionStartTime);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"总游玩时间: {SaveSystem.FormatPlayTime(totalTime)}");
        sb.AppendLine($"关卡完成: {stats.totalLevelsCompleted}");
        sb.AppendLine($"Boss击败: {stats.totalBossesDefeated}");
        sb.AppendLine($"敌人消灭: {stats.totalEnemiesDefeated}");
        sb.AppendLine($"收集品: {stats.totalCollectiblesFound}");
        sb.AppendLine($"最高连击: {stats.highestCombo}");
        sb.AppendLine($"死亡次数: {stats.totalDeaths}");
        sb.AppendLine($"总跳跃: {stats.totalJumps}");
        sb.AppendLine($"总冲刺: {stats.totalDashes}");
        sb.AppendLine($"技能使用: {stats.totalAbilitiesUsed}");
        sb.AppendLine($"移动距离: {stats.totalDistanceTraveled:F0}m");
        return sb.ToString();
    }

    // ============ 存档 ============

    private void SaveStats()
    {
        string json = JsonUtility.ToJson(stats, true);
        PlayerPrefs.SetString(STATS_SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    private void LoadStats()
    {
        if (PlayerPrefs.HasKey(STATS_SAVE_KEY))
        {
            string json = PlayerPrefs.GetString(STATS_SAVE_KEY);
            stats = JsonUtility.FromJson<StatsData>(json);
            if (stats == null) stats = new StatsData();
        }
    }

    /// <summary>
    /// 重置统计
    /// </summary>
    public void ResetStats()
    {
        stats = new StatsData();
        SaveStats();
    }
}
