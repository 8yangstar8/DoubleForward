using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 每日奖励系统 - 7天循环奖励日历
/// 连续登录有额外奖励，断签重置进度
/// </summary>
public class DailyRewardSystem : MonoBehaviour
{
    public static DailyRewardSystem Instance { get; private set; }

    private const string LAST_CLAIM_DATE_KEY = "daily_last_claim";
    private const string STREAK_KEY = "daily_streak";
    private const string TOTAL_CLAIMS_KEY = "daily_total_claims";

    [Header("奖励周期")]
    [SerializeField] private int cycleDays = 7;
    [SerializeField] private bool resetOnMiss = true;   // 断签重置

    [Header("7天奖励配置")]
    [SerializeField] private DailyReward[] rewards = new DailyReward[]
    {
        new DailyReward { coins = 50,  gems = 0, description = "day_1" },
        new DailyReward { coins = 75,  gems = 0, description = "day_2" },
        new DailyReward { coins = 100, gems = 1, description = "day_3" },
        new DailyReward { coins = 125, gems = 0, description = "day_4" },
        new DailyReward { coins = 150, gems = 2, description = "day_5" },
        new DailyReward { coins = 200, gems = 0, description = "day_6" },
        new DailyReward { coins = 300, gems = 5, description = "day_7_bonus" }
    };

    [System.Serializable]
    public class DailyReward
    {
        public int coins;
        public int gems;
        public string description;  // 本地化key
    }

    // 状态
    public int CurrentStreak { get; private set; }
    public int TotalClaims { get; private set; }
    public bool CanClaimToday { get; private set; }
    public int CurrentDay => (CurrentStreak % cycleDays);  // 0~6
    public DailyReward TodayReward => rewards[CurrentDay];

    // 事件
    public event System.Action<DailyReward, int> OnRewardClaimed;  // reward, streak
    public event System.Action OnStreakReset;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadState();
        CheckDailyReset();
    }

    /// <summary>
    /// 领取今日奖励
    /// </summary>
    public bool ClaimDailyReward()
    {
        if (!CanClaimToday) return false;

        DailyReward reward = TodayReward;

        // 发放奖励
        if (CurrencyManager.Instance != null)
        {
            if (reward.coins > 0)
                CurrencyManager.Instance.AddCoins(reward.coins, "daily_reward");
            if (reward.gems > 0)
                CurrencyManager.Instance.AddGems(reward.gems, "daily_reward");
        }

        // 更新状态
        CurrentStreak++;
        TotalClaims++;
        CanClaimToday = false;

        // 保存
        PlayerPrefs.SetString(LAST_CLAIM_DATE_KEY, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        PlayerPrefs.SetInt(STREAK_KEY, CurrentStreak);
        PlayerPrefs.SetInt(TOTAL_CLAIMS_KEY, TotalClaims);
        PlayerPrefs.Save();

        OnRewardClaimed?.Invoke(reward, CurrentStreak);

        // 成就检查
        if (AchievementSystem.Instance != null)
        {
            if (CurrentStreak >= 7)
                AchievementSystem.Instance.Unlock("weekly_login");
            if (CurrentStreak >= 30)
                AchievementSystem.Instance.Unlock("monthly_login");
        }

        // 分析
        if (AnalyticsTracker.Instance != null)
            AnalyticsTracker.Instance.TrackEvent("daily_reward_claimed",
                ("day", CurrentDay.ToString()),
                ("streak", CurrentStreak.ToString()),
                ("coins", reward.coins.ToString()));

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("reward_claim");

        return true;
    }

    /// <summary>
    /// 获取所有奖励信息（用于日历UI显示）
    /// </summary>
    public List<DailyRewardInfo> GetCalendarInfo()
    {
        var infos = new List<DailyRewardInfo>();

        for (int i = 0; i < rewards.Length; i++)
        {
            var info = new DailyRewardInfo
            {
                dayIndex = i,
                reward = rewards[i],
                isClaimed = i < CurrentDay || (i == CurrentDay && !CanClaimToday),
                isToday = i == CurrentDay && CanClaimToday,
                isLocked = i > CurrentDay
            };
            infos.Add(info);
        }

        return infos;
    }

    // ==================== 内部逻辑 ====================

    private void LoadState()
    {
        CurrentStreak = PlayerPrefs.GetInt(STREAK_KEY, 0);
        TotalClaims = PlayerPrefs.GetInt(TOTAL_CLAIMS_KEY, 0);
    }

    private void CheckDailyReset()
    {
        string lastClaimStr = PlayerPrefs.GetString(LAST_CLAIM_DATE_KEY, "");

        if (string.IsNullOrEmpty(lastClaimStr))
        {
            // 首次登录
            CanClaimToday = true;
            return;
        }

        if (!DateTime.TryParse(lastClaimStr, out DateTime lastClaim))
        {
            CanClaimToday = true;
            return;
        }

        DateTime today = DateTime.UtcNow.Date;
        DateTime lastClaimDate = lastClaim.Date;

        int daysSince = (today - lastClaimDate).Days;

        if (daysSince == 0)
        {
            // 今天已经领过
            CanClaimToday = false;
        }
        else if (daysSince == 1)
        {
            // 连续签到
            CanClaimToday = true;
        }
        else if (daysSince > 1)
        {
            // 断签
            if (resetOnMiss)
            {
                CurrentStreak = 0;
                PlayerPrefs.SetInt(STREAK_KEY, 0);
                PlayerPrefs.Save();
                OnStreakReset?.Invoke();
            }
            CanClaimToday = true;
        }
    }

    /// <summary>
    /// 获取距离下次可领取的剩余时间
    /// </summary>
    public TimeSpan GetTimeUntilNextReward()
    {
        if (CanClaimToday) return TimeSpan.Zero;

        DateTime tomorrow = DateTime.UtcNow.Date.AddDays(1);
        return tomorrow - DateTime.UtcNow;
    }

    /// <summary>
    /// 格式化倒计时
    /// </summary>
    public string GetCountdownString()
    {
        var remaining = GetTimeUntilNextReward();
        if (remaining <= TimeSpan.Zero) return "";
        return $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }
}

/// <summary>
/// 日历UI显示用数据
/// </summary>
public class DailyRewardInfo
{
    public int dayIndex;
    public DailyRewardSystem.DailyReward reward;
    public bool isClaimed;
    public bool isToday;
    public bool isLocked;
}
