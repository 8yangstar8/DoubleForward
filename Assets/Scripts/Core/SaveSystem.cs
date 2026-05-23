using UnityEngine;
using System.IO;

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    private const string SAVE_FILE = "save_data.json";

    [System.Serializable]
    public class SaveData
    {
        public int lastChapter = 1;
        public int lastLevel = 1;
        public bool[] levelsCompleted = new bool[20];
        public float totalPlayTime;
    }

    public SaveData Data { get; private set; } = new SaveData();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public void Save()
    {
        string json = JsonUtility.ToJson(Data, true);
        string path = Path.Combine(Application.persistentDataPath, SAVE_FILE);
        File.WriteAllText(path, json);
    }

    public void Load()
    {
        string path = Path.Combine(Application.persistentDataPath, SAVE_FILE);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            Data = JsonUtility.FromJson<SaveData>(json);
        }
        else
        {
            Data = new SaveData();
        }
    }

    public void MarkLevelComplete(int chapter, int level)
    {
        int index = GetLevelIndex(chapter, level);
        if (index >= 0 && index < Data.levelsCompleted.Length)
        {
            Data.levelsCompleted[index] = true;
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

    private int GetLevelIndex(int chapter, int level)
    {
        int[] levelsPerChapter = { 3, 4, 4, 5, 4 };
        int index = 0;
        for (int i = 0; i < chapter - 1 && i < levelsPerChapter.Length; i++)
            index += levelsPerChapter[i];
        return index + level - 1;
    }

    public void DeleteSave()
    {
        string path = Path.Combine(Application.persistentDataPath, SAVE_FILE);
        if (File.Exists(path))
            File.Delete(path);
        Data = new SaveData();
    }
}
