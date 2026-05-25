using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 存档系统 - 支持多存档槽位、自动存档、存档导出
/// </summary>
public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    private const string SAVE_DIR = "Saves";
    private const string SAVE_PREFIX = "save_slot_";
    private const string SAVE_EXT = ".json";
    private const string ACTIVE_SLOT_KEY = "active_save_slot";
    public const int MAX_SLOTS = 3;

    [System.Serializable]
    public class SaveData
    {
        public string slotName = "";
        public int lastChapter = 1;
        public int lastLevel = 1;
        public bool[] levelsCompleted = new bool[20];
        public int[] levelStars = new int[20];          // 每关星级 (0-3)
        public float[] levelBestTimes = new float[20];  // 每关最佳时间
        public int[] levelCollectibles = new int[20];   // 每关收集品数量
        public float totalPlayTime;
        public int totalDeaths;
        public int totalCollectibles;
        public string lastSaveDateTime = "";
        public float completionPercent;

        // 计算属性（不序列化）
        /// <summary>已完成关卡总数</summary>
        public int levelsCompletedCount
        {
            get
            {
                int count = 0;
                foreach (bool b in levelsCompleted) if (b) count++;
                return count;
            }
        }

        /// <summary>总获得星数</summary>
        public int totalStars
        {
            get
            {
                int sum = 0;
                foreach (int s in levelStars) sum += s;
                return sum;
            }
        }

        // 设置数据一起存
        public float bgmVolume = 0.7f;
        public float sfxVolume = 1f;
        public int qualityLevel = 2;
    }

    [System.Serializable]
    public class SaveSlotInfo
    {
        public int slotIndex;
        public bool isEmpty;
        public string slotName;
        public int chapter;
        public int level;
        public float playTime;
        public float completionPercent;
        public string lastSaveDate;
    }

    public SaveData Data { get; private set; } = new SaveData();
    public int ActiveSlot { get; private set; } = 0;

    public event System.Action OnSaveCompleted;
    public event System.Action OnLoadCompleted;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureSaveDirectory();
        ActiveSlot = PlayerPrefs.GetInt(ACTIVE_SLOT_KEY, 0);
        Load(ActiveSlot);
    }

    private void EnsureSaveDirectory()
    {
        string dir = Path.Combine(Application.persistentDataPath, SAVE_DIR);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    private string GetSlotPath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, SAVE_DIR, $"{SAVE_PREFIX}{slot}{SAVE_EXT}");
    }

    /// <summary>
    /// 切换活跃存档槽位
    /// </summary>
    public void SetActiveSlot(int slot)
    {
        if (slot < 0 || slot >= MAX_SLOTS) return;
        ActiveSlot = slot;
        PlayerPrefs.SetInt(ACTIVE_SLOT_KEY, slot);
        PlayerPrefs.Save();
        Load(slot);
    }

    /// <summary>
    /// 保存到当前槽位
    /// </summary>
    public void Save()
    {
        Save(ActiveSlot);
    }

    /// <summary>
    /// 保存到指定槽位
    /// </summary>
    public void Save(int slot)
    {
        if (slot < 0 || slot >= MAX_SLOTS) return;

        Data.lastSaveDateTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        Data.completionPercent = CalculateCompletion();

        string json = JsonUtility.ToJson(Data, true);
        string path = GetSlotPath(slot);

        try
        {
            File.WriteAllText(path, json);
            OnSaveCompleted?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveSystem] 保存失败: {e.Message}");
        }
    }

    /// <summary>
    /// 从指定槽位加载
    /// </summary>
    public void Load(int slot)
    {
        if (slot < 0 || slot >= MAX_SLOTS) return;

        string path = GetSlotPath(slot);
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                Data = JsonUtility.FromJson<SaveData>(json);
                OnLoadCompleted?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] 加载失败: {e.Message}");
                Data = new SaveData();
            }
        }
        else
        {
            Data = new SaveData();
        }
    }

    /// <summary>
    /// 获取所有槽位信息（用于UI显示）
    /// </summary>
    public List<SaveSlotInfo> GetAllSlotInfos()
    {
        var infos = new List<SaveSlotInfo>();

        for (int i = 0; i < MAX_SLOTS; i++)
        {
            var info = new SaveSlotInfo { slotIndex = i };
            string path = GetSlotPath(i);

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<SaveData>(json);
                    info.isEmpty = false;
                    info.slotName = string.IsNullOrEmpty(data.slotName) ? $"存档 {i + 1}" : data.slotName;
                    info.chapter = data.lastChapter;
                    info.level = data.lastLevel;
                    info.playTime = data.totalPlayTime;
                    info.completionPercent = data.completionPercent;
                    info.lastSaveDate = data.lastSaveDateTime;
                }
                catch
                {
                    info.isEmpty = true;
                    info.slotName = $"存档 {i + 1}";
                }
            }
            else
            {
                info.isEmpty = true;
                info.slotName = $"存档 {i + 1}";
            }

            infos.Add(info);
        }

        return infos;
    }

    /// <summary>
    /// 检查槽位是否有数据
    /// </summary>
    public bool IsSlotEmpty(int slot)
    {
        return !File.Exists(GetSlotPath(slot));
    }

    public void MarkLevelComplete(int chapter, int level, int stars = 1, float time = 0, int collectibles = 0)
    {
        int index = GetLevelIndex(chapter, level);
        if (index >= 0 && index < Data.levelsCompleted.Length)
        {
            Data.levelsCompleted[index] = true;

            // 只保留更高星级
            if (stars > Data.levelStars[index])
                Data.levelStars[index] = stars;

            // 只保留更好时间
            if (time > 0 && (Data.levelBestTimes[index] <= 0 || time < Data.levelBestTimes[index]))
                Data.levelBestTimes[index] = time;

            // 只保留更多收集品
            if (collectibles > Data.levelCollectibles[index])
                Data.levelCollectibles[index] = collectibles;

            Data.lastChapter = chapter;
            Data.lastLevel = level;
            Save();
        }
    }

    public bool IsLevelCompleted(int chapter, int level)
    {
        int index = GetLevelIndex(chapter, level);
        return index >= 0 && index < Data.levelsCompleted.Length && Data.levelsCompleted[index];
    }

    public int GetLevelStars(int chapter, int level)
    {
        int index = GetLevelIndex(chapter, level);
        if (index >= 0 && index < Data.levelStars.Length)
            return Data.levelStars[index];
        return 0;
    }

    public float GetLevelBestTime(int chapter, int level)
    {
        int index = GetLevelIndex(chapter, level);
        if (index >= 0 && index < Data.levelBestTimes.Length)
            return Data.levelBestTimes[index];
        return 0;
    }

    private int GetLevelIndex(int chapter, int level)
    {
        int[] levelsPerChapter = { 3, 4, 4, 5, 4 };
        int index = 0;
        for (int i = 0; i < chapter - 1 && i < levelsPerChapter.Length; i++)
            index += levelsPerChapter[i];
        return index + level - 1;
    }

    /// <summary>
    /// 计算完成度百分比
    /// </summary>
    private float CalculateCompletion()
    {
        int completed = 0;
        int totalStars = 0;
        for (int i = 0; i < Data.levelsCompleted.Length; i++)
        {
            if (Data.levelsCompleted[i]) completed++;
            totalStars += Data.levelStars[i];
        }

        // 完成度 = 关卡完成50% + 星级收集50%
        float levelPercent = (float)completed / Data.levelsCompleted.Length;
        float starPercent = (float)totalStars / (Data.levelsCompleted.Length * 3);
        return (levelPercent * 0.5f + starPercent * 0.5f) * 100f;
    }

    /// <summary>
    /// 删除指定槽位
    /// </summary>
    public void DeleteSlot(int slot)
    {
        string path = GetSlotPath(slot);
        if (File.Exists(path))
            File.Delete(path);

        if (slot == ActiveSlot)
            Data = new SaveData();
    }

    /// <summary>
    /// 更新游玩时间
    /// </summary>
    public void AddPlayTime(float seconds)
    {
        Data.totalPlayTime += seconds;
    }

    /// <summary>
    /// 设置存档名称
    /// </summary>
    public void SetSlotName(string name)
    {
        Data.slotName = name;
        Save();
    }

    /// <summary>
    /// 格式化游玩时间
    /// </summary>
    public static string FormatPlayTime(float seconds)
    {
        int hours = Mathf.FloorToInt(seconds / 3600);
        int minutes = Mathf.FloorToInt((seconds % 3600) / 60);
        if (hours > 0)
            return $"{hours}h {minutes}m";
        return $"{minutes}m";
    }
}
