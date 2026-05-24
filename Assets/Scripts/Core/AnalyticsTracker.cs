using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 分析追踪系统 - 记录玩家行为数据用于优化关卡设计
/// 本地存储模式，不依赖第三方SDK
/// 可扩展为接入Firebase Analytics等
/// </summary>
public class AnalyticsTracker : MonoBehaviour
{
    public static AnalyticsTracker Instance { get; private set; }

    [Header("设置")]
    [SerializeField] private bool enableTracking = true;
    [SerializeField] private bool logToConsole = false;
    [SerializeField] private int maxEventsInMemory = 500;

    // 事件缓存
    private List<AnalyticsEvent> eventBuffer = new List<AnalyticsEvent>();
    private float sessionStartTime;
    private string sessionId;

    [System.Serializable]
    public class AnalyticsEvent
    {
        public string eventName;
        public float timestamp;
        public Dictionary<string, string> parameters;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sessionId = System.Guid.NewGuid().ToString().Substring(0, 8);
        sessionStartTime = Time.realtimeSinceStartup;

        TrackEvent("session_start", new Dictionary<string, string>
        {
            {"device", SystemInfo.deviceModel},
            {"os", SystemInfo.operatingSystem},
            {"language", Application.systemLanguage.ToString()},
            {"version", Application.version}
        });
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) FlushEvents();
    }

    void OnApplicationQuit()
    {
        float duration = Time.realtimeSinceStartup - sessionStartTime;
        TrackEvent("session_end", new Dictionary<string, string>
        {
            {"duration_seconds", duration.ToString("F0")}
        });
        FlushEvents();
    }

    // ============ 通用追踪 ============

    /// <summary>
    /// 记录自定义事件
    /// </summary>
    public void TrackEvent(string eventName, Dictionary<string, string> parameters = null)
    {
        if (!enableTracking) return;

        var evt = new AnalyticsEvent
        {
            eventName = eventName,
            timestamp = Time.realtimeSinceStartup - sessionStartTime,
            parameters = parameters ?? new Dictionary<string, string>()
        };

        eventBuffer.Add(evt);

        if (logToConsole)
        {
            string paramStr = "";
            foreach (var kvp in evt.parameters)
                paramStr += $" {kvp.Key}={kvp.Value}";
            Debug.Log($"[Analytics] {eventName}{paramStr}");
        }

        if (eventBuffer.Count > maxEventsInMemory)
            FlushEvents();
    }

    // ============ 便捷事件方法 ============

    /// <summary>
    /// 关卡开始
    /// </summary>
    public void TrackLevelStart(int chapter, int level)
    {
        TrackEvent("level_start", new Dictionary<string, string>
        {
            {"chapter", chapter.ToString()},
            {"level", level.ToString()},
            {"difficulty", DifficultyManager.Instance?.DifficultyModifier.ToString("F2") ?? "1.0"}
        });
    }

    /// <summary>
    /// 关卡完成
    /// </summary>
    public void TrackLevelComplete(int chapter, int level, int stars, float time, int deaths)
    {
        TrackEvent("level_complete", new Dictionary<string, string>
        {
            {"chapter", chapter.ToString()},
            {"level", level.ToString()},
            {"stars", stars.ToString()},
            {"time", time.ToString("F1")},
            {"deaths", deaths.ToString()}
        });
    }

    /// <summary>
    /// 关卡放弃
    /// </summary>
    public void TrackLevelQuit(int chapter, int level, float timeSpent, int deaths)
    {
        TrackEvent("level_quit", new Dictionary<string, string>
        {
            {"chapter", chapter.ToString()},
            {"level", level.ToString()},
            {"time_spent", timeSpent.ToString("F1")},
            {"deaths", deaths.ToString()}
        });
    }

    /// <summary>
    /// 玩家死亡
    /// </summary>
    public void TrackPlayerDeath(int chapter, int level, Vector3 position, string cause)
    {
        TrackEvent("player_death", new Dictionary<string, string>
        {
            {"chapter", chapter.ToString()},
            {"level", level.ToString()},
            {"x", position.x.ToString("F1")},
            {"y", position.y.ToString("F1")},
            {"cause", cause}
        });
    }

    /// <summary>
    /// 谜题卡关
    /// </summary>
    public void TrackPuzzleStuck(string puzzleId, float stuckDuration)
    {
        TrackEvent("puzzle_stuck", new Dictionary<string, string>
        {
            {"puzzle_id", puzzleId},
            {"duration", stuckDuration.ToString("F1")}
        });
    }

    /// <summary>
    /// 提示使用
    /// </summary>
    public void TrackHintUsed(string puzzleId, int hintLevel)
    {
        TrackEvent("hint_used", new Dictionary<string, string>
        {
            {"puzzle_id", puzzleId},
            {"hint_level", hintLevel.ToString()}
        });
    }

    /// <summary>
    /// 成就解锁
    /// </summary>
    public void TrackAchievement(string achievementId)
    {
        TrackEvent("achievement_unlocked", new Dictionary<string, string>
        {
            {"achievement_id", achievementId}
        });
    }

    /// <summary>
    /// 难度调整
    /// </summary>
    public void TrackDifficultyChange(float oldModifier, float newModifier, string reason)
    {
        TrackEvent("difficulty_change", new Dictionary<string, string>
        {
            {"old", oldModifier.ToString("F2")},
            {"new", newModifier.ToString("F2")},
            {"reason", reason}
        });
    }

    /// <summary>
    /// 合作技能使用
    /// </summary>
    public void TrackCoopAbility(string abilityName)
    {
        TrackEvent("coop_ability", new Dictionary<string, string>
        {
            {"ability", abilityName}
        });
    }

    /// <summary>
    /// 追踪菜单导航
    /// </summary>
    public void TrackScreenView(string screenName)
    {
        TrackEvent("screen_view", new Dictionary<string, string>
        {
            {"screen", screenName}
        });
    }

    // ============ 数据持久化 ============

    /// <summary>
    /// 将缓冲区事件刷写到磁盘
    /// </summary>
    private void FlushEvents()
    {
        if (eventBuffer.Count == 0) return;

        try
        {
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "Analytics");
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            string filename = $"events_{sessionId}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
            string filePath = System.IO.Path.Combine(dir, filename);

            // 序列化
            var wrapper = new EventListWrapper { events = new List<SerializableEvent>() };
            foreach (var evt in eventBuffer)
            {
                var se = new SerializableEvent
                {
                    name = evt.eventName,
                    time = evt.timestamp,
                    @params = evt.parameters
                };
                wrapper.events.Add(se);
            }

            string json = JsonUtility.ToJson(wrapper, true);

            // JsonUtility不支持Dictionary，手动序列化
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{\"session\":\"" + sessionId + "\",\"events\":[");
            for (int i = 0; i < eventBuffer.Count; i++)
            {
                var evt = eventBuffer[i];
                sb.Append("{\"name\":\"").Append(evt.eventName)
                  .Append("\",\"time\":").Append(evt.timestamp.ToString("F2"));

                if (evt.parameters.Count > 0)
                {
                    sb.Append(",\"params\":{");
                    bool first = true;
                    foreach (var kvp in evt.parameters)
                    {
                        if (!first) sb.Append(",");
                        sb.Append("\"").Append(kvp.Key).Append("\":\"")
                          .Append(kvp.Value.Replace("\"", "\\\"")).Append("\"");
                        first = false;
                    }
                    sb.Append("}");
                }

                sb.Append("}");
                if (i < eventBuffer.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("]}");

            System.IO.File.WriteAllText(filePath, sb.ToString());

            if (logToConsole)
                Debug.Log($"[Analytics] Flushed {eventBuffer.Count} events to {filePath}");

            eventBuffer.Clear();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Analytics] Failed to flush events: {e.Message}");
        }
    }

    /// <summary>
    /// 获取本地分析数据文件夹大小
    /// </summary>
    public long GetAnalyticsStorageSize()
    {
        string dir = System.IO.Path.Combine(Application.persistentDataPath, "Analytics");
        if (!System.IO.Directory.Exists(dir)) return 0;

        long size = 0;
        foreach (var file in System.IO.Directory.GetFiles(dir, "*.json"))
        {
            size += new System.IO.FileInfo(file).Length;
        }
        return size;
    }

    /// <summary>
    /// 清除本地分析数据
    /// </summary>
    public void ClearAnalyticsData()
    {
        string dir = System.IO.Path.Combine(Application.persistentDataPath, "Analytics");
        if (System.IO.Directory.Exists(dir))
        {
            foreach (var file in System.IO.Directory.GetFiles(dir, "*.json"))
            {
                try { System.IO.File.Delete(file); }
                catch { /* 忽略 */ }
            }
        }
    }

    // 序列化辅助（JsonUtility需要）
    [System.Serializable]
    private class EventListWrapper
    {
        public List<SerializableEvent> events;
    }

    [System.Serializable]
    private class SerializableEvent
    {
        public string name;
        public float time;
        public Dictionary<string, string> @params;
    }
}
