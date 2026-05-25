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

        InitializeDefaultAchievements();
        LoadProgress();
    }

    /// <summary>
    /// 初始化默认成就定义（仅当Inspector中未配置时生效）
    /// </summary>
    private void InitializeDefaultAchievements()
    {
        if (achievements.Count > 0) return; // 已在Inspector中配置

        // ====== 剧情类 ======
        AddDefault("ch1_complete", "光暗初现", "完成第一章", "story", 1);
        AddDefault("ch2_complete", "冰火淬炼", "完成第二章", "story", 1);
        AddDefault("ch3_complete", "沙海迷途", "完成第三章", "story", 1);
        AddDefault("ch4_complete", "暗影深渊", "完成第四章", "story", 1);
        AddDefault("ch5_complete", "双向归一", "完成第五章", "story", 1);
        AddDefault("all_chapters", "双向前行", "完成所有章节", "story", 5);
        AddDefault("boss_ch1", "石像崩塌", "击败第一章Boss", "story", 1);
        AddDefault("boss_ch2", "冰霜消融", "击败第二章Boss", "story", 1);
        AddDefault("boss_ch3", "风暴平息", "击败第三章Boss", "story", 1);
        AddDefault("boss_ch4", "暗潮退去", "击败第四章Boss", "story", 1);
        AddDefault("boss_ch5", "光暗合一", "击败最终Boss", "story", 1);

        // ====== 挑战类 ======
        AddDefault("all_stars", "完美通关", "所有关卡获得3星", "challenge", 60); // 20 levels × 3 stars
        AddDefault("speed_demon", "闪电侠", "任意关卡在60秒内完成", "challenge", 1);
        AddDefault("no_death_chapter", "不死之身", "无死亡完成一个完整章节", "challenge", 1);
        AddDefault("combo_master", "连击大师", "达成50连击", "challenge", 1);
        AddDefault("combo_legend", "连击传奇", "达成100连击", "challenge", 1);
        AddDefault("collect_all", "收藏家", "收集所有收藏品", "challenge", 100);
        AddDefault("defeat_100", "百人斩", "击败100个敌人", "challenge", 100);
        AddDefault("defeat_500", "千军万马", "击败500个敌人", "challenge", 500);
        AddDefault("revive_partner_10", "生死与共", "复活搭档10次", "challenge", 10);
        AddDefault("no_hit_boss", "完美闪避", "无伤击败任意Boss", "challenge", 1);

        // ====== 探索类 ======
        AddDefault("first_secret", "好奇心", "发现第一个隐藏区域", "secret", 1);
        AddDefault("all_secrets", "探索者", "发现所有隐藏区域", "secret", 20);
        AddDefault("coop_ability_first", "心有灵犀", "首次使用合作技能", "secret", 1);
        AddDefault("use_all_abilities", "全能战士", "使用所有技能各一次", "secret", 6);
        AddDefault("wall_jump_chain", "壁虎功", "连续墙跳5次", "secret", 1);

        // ====== 社交/杂项 ======
        AddDefault("first_purchase", "买买买", "首次在商店购买物品", "social", 1);
        AddDefault("weekly_login", "坚持不懈", "连续登录7天", "social", 1);
        AddDefault("monthly_login", "铁杆玩家", "连续登录30天", "social", 1);
        AddDefault("share_first", "分享快乐", "首次分享游戏截图", "social", 1);
        AddDefault("photo_mode_10", "摄影师", "使用拍照模式10次", "social", 10);
    }

    private void AddDefault(string id, string title, string desc, string category, float target)
    {
        achievements.Add(new Achievement
        {
            id = id,
            title = title,
            description = desc,
            category = category,
            targetProgress = target,
            isHidden = category == "secret"
        });
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

    /// <summary>
    /// 通过ID直接解锁成就（外部系统调用）
    /// </summary>
    public void Unlock(string achievementId)
    {
        var achievement = achievements.Find(a => a.id == achievementId);
        if (achievement != null)
            Unlock(achievement);
    }

    private void Unlock(Achievement achievement)
    {
        if (achievement.isUnlocked) return;

        achievement.isUnlocked = true;
        OnAchievementUnlocked?.Invoke(achievement);
        ShowNotification(achievement);

        if (unlockSound != null)
            AudioManager.Instance?.PlaySFX(unlockSound);

        // Google Play Games 联动
        EventBus.Publish(new AchievementUnlockedEvent { achievementId = achievement.id });

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
