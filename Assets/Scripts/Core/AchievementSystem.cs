using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class AchievementSystem : MonoBehaviour
{
    public static AchievementSystem Instance { get; private set; }

    [System.Serializable]
    public class Achievement
    {
        public string id;
        public string title;
        public string description;
        public Sprite icon;
        public bool isHidden;
        public bool isUnlocked;
        public float progress;
        public float targetProgress = 1f;
        public string category; // "story", "challenge", "secret"
    }

    [SerializeField] private List<Achievement> achievements = new List<Achievement>();
    [SerializeField] private GameObject notificationPrefab;
    [SerializeField] private Transform notificationParent;
    [SerializeField] private AudioClip unlockSound;

    private const string SAVE_KEY = "achievements_data";

    public event System.Action<Achievement> OnAchievementUnlocked;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadProgress();
    }

    public void UpdateProgress(string achievementId, float amount = 1f)
    {
        var achievement = achievements.Find(a => a.id == achievementId);
        if (achievement == null || achievement.isUnlocked) return;

        achievement.progress = Mathf.Min(achievement.progress + amount, achievement.targetProgress);

        if (achievement.progress >= achievement.targetProgress)
            Unlock(achievement);

        SaveProgress();
    }

    public void SetProgress(string achievementId, float value)
    {
        var achievement = achievements.Find(a => a.id == achievementId);
        if (achievement == null || achievement.isUnlocked) return;

        achievement.progress = Mathf.Min(value, achievement.targetProgress);

        if (achievement.progress >= achievement.targetProgress)
            Unlock(achievement);

        SaveProgress();
    }

    private void Unlock(Achievement achievement)
    {
        if (achievement.isUnlocked) return;

        achievement.isUnlocked = true;
        OnAchievementUnlocked?.Invoke(achievement);
        ShowNotification(achievement);

        if (unlockSound != null)
            AudioManager.Instance?.PlaySFX(unlockSound);

        SaveProgress();
    }

    private void ShowNotification(Achievement achievement)
    {
        if (notificationPrefab == null || notificationParent == null) return;

        var notif = Instantiate(notificationPrefab, notificationParent);
        var ui = notif.GetComponent<AchievementNotificationUI>();
        if (ui != null)
            ui.Show(achievement.title, achievement.description, achievement.icon);

        Destroy(notif, 4f);
    }

    public bool IsUnlocked(string achievementId)
    {
        var achievement = achievements.Find(a => a.id == achievementId);
        return achievement?.isUnlocked ?? false;
    }

    public float GetProgress(string achievementId)
    {
        var achievement = achievements.Find(a => a.id == achievementId);
        if (achievement == null) return 0;
        return achievement.progress / achievement.targetProgress;
    }

    public List<Achievement> GetAllAchievements() => new List<Achievement>(achievements);
    public List<Achievement> GetUnlocked() => achievements.FindAll(a => a.isUnlocked);
    public List<Achievement> GetByCategory(string cat) => achievements.FindAll(a => a.category == cat);

    public int GetUnlockedCount() => achievements.FindAll(a => a.isUnlocked).Count;
    public int GetTotalCount() => achievements.Count;

    private void SaveProgress()
    {
        var data = new AchievementSaveData();
        foreach (var a in achievements)
        {
            data.entries.Add(new AchievementEntry
            {
                id = a.id,
                isUnlocked = a.isUnlocked,
                progress = a.progress
            });
        }
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    private void LoadProgress()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY)) return;

        string json = PlayerPrefs.GetString(SAVE_KEY);
        var data = JsonUtility.FromJson<AchievementSaveData>(json);
        if (data == null) return;

        foreach (var entry in data.entries)
        {
            var achievement = achievements.Find(a => a.id == entry.id);
            if (achievement != null)
            {
                achievement.isUnlocked = entry.isUnlocked;
                achievement.progress = entry.progress;
            }
        }
    }

    [System.Serializable]
    private class AchievementSaveData
    {
        public List<AchievementEntry> entries = new List<AchievementEntry>();
    }

    [System.Serializable]
    private class AchievementEntry
    {
        public string id;
        public bool isUnlocked;
        public float progress;
    }
}
