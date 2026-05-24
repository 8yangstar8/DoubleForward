using UnityEngine;
using System;

/// <summary>
/// 本地推送通知调度器 - Android本地通知
/// 支持离线提醒、每日奖励提醒、回归提醒
/// 使用Unity Mobile Notifications包或Android原生API
/// </summary>
public class NotificationScheduler : MonoBehaviour
{
    public static NotificationScheduler Instance { get; private set; }

    [Header("通知配置")]
    [SerializeField] private bool enableNotifications = true;
    [SerializeField] private int dailyRewardHour = 10;         // 每日奖励提醒时间
    [SerializeField] private int returnReminderDays = 3;       // 几天没玩发回归提醒
    [SerializeField] private string channelId = "doubleforward_general";
    [SerializeField] private string channelName = "Double Forward";

    [Header("通知文案（key对应本地化）")]
    [SerializeField] private string dailyRewardTitleKey = "notif_daily_title";
    [SerializeField] private string dailyRewardBodyKey = "notif_daily_body";
    [SerializeField] private string returnTitleKey = "notif_return_title";
    [SerializeField] private string returnBodyKey = "notif_return_body";
    [SerializeField] private string energyFullTitleKey = "notif_energy_title";
    [SerializeField] private string energyFullBodyKey = "notif_energy_body";

    // 默认文案
    private const string DEFAULT_DAILY_TITLE = "Daily Reward Ready!";
    private const string DEFAULT_DAILY_BODY = "Your daily reward is waiting. Come claim it!";
    private const string DEFAULT_RETURN_TITLE = "We miss you!";
    private const string DEFAULT_RETURN_BODY = "Lux and Nox need your help. Come back and continue the adventure!";

    private const string NOTIF_ENABLED_KEY = "notifications_enabled";
    private const string LAST_SESSION_KEY = "last_session_time";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        enableNotifications = PlayerPrefs.GetInt(NOTIF_ENABLED_KEY, 1) == 1;
    }

    void Start()
    {
        if (!enableNotifications) return;

        InitializeNotificationChannel();
        RecordSessionTime();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            // 应用进入后台 - 调度通知
            RecordSessionTime();
            ScheduleAllNotifications();
        }
        else
        {
            // 应用恢复 - 取消所有待发通知
            CancelAllNotifications();
        }
    }

    void OnApplicationQuit()
    {
        RecordSessionTime();
        ScheduleAllNotifications();
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 启用/禁用通知
    /// </summary>
    public void SetNotificationsEnabled(bool enabled)
    {
        enableNotifications = enabled;
        PlayerPrefs.SetInt(NOTIF_ENABLED_KEY, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (!enabled)
            CancelAllNotifications();
    }

    public bool IsNotificationsEnabled() => enableNotifications;

    /// <summary>
    /// 调度自定义通知
    /// </summary>
    public void ScheduleNotification(string title, string body, int delaySeconds, int notifId = -1)
    {
        if (!enableNotifications) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        ScheduleAndroidNotification(title, body, delaySeconds, notifId >= 0 ? notifId : GenerateNotifId());
#else
        Debug.Log($"[Notification] Scheduled: \"{title}\" in {delaySeconds}s");
#endif
    }

    /// <summary>
    /// 取消所有待发通知
    /// </summary>
    public void CancelAllNotifications()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        CancelAllAndroidNotifications();
#else
        Debug.Log("[Notification] All notifications cancelled");
#endif
    }

    // ==================== 调度逻辑 ====================

    private void ScheduleAllNotifications()
    {
        if (!enableNotifications) return;

        CancelAllNotifications();
        ScheduleDailyRewardReminder();
        ScheduleReturnReminder();
    }

    private void ScheduleDailyRewardReminder()
    {
        // 计算到明天指定时间的延迟
        DateTime now = DateTime.Now;
        DateTime nextReminder = now.Date.AddDays(1).AddHours(dailyRewardHour);
        int delay = Mathf.Max(60, (int)(nextReminder - now).TotalSeconds);

        string title = GetLocalizedText(dailyRewardTitleKey, DEFAULT_DAILY_TITLE);
        string body = GetLocalizedText(dailyRewardBodyKey, DEFAULT_DAILY_BODY);

        ScheduleNotification(title, body, delay, 1001);
    }

    private void ScheduleReturnReminder()
    {
        int delay = returnReminderDays * 24 * 3600; // 天数转秒

        string title = GetLocalizedText(returnTitleKey, DEFAULT_RETURN_TITLE);
        string body = GetLocalizedText(returnBodyKey, DEFAULT_RETURN_BODY);

        ScheduleNotification(title, body, delay, 1002);
    }

    private void RecordSessionTime()
    {
        PlayerPrefs.SetString(LAST_SESSION_KEY, DateTime.UtcNow.ToString("o"));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 获取上次游玩时间
    /// </summary>
    public DateTime GetLastSessionTime()
    {
        string stored = PlayerPrefs.GetString(LAST_SESSION_KEY, "");
        if (DateTime.TryParse(stored, out DateTime result))
            return result;
        return DateTime.UtcNow;
    }

    /// <summary>
    /// 获取离线天数
    /// </summary>
    public int GetDaysSinceLastSession()
    {
        return (DateTime.UtcNow - GetLastSessionTime()).Days;
    }

    // ==================== Android原生实现 ====================

    private void InitializeNotificationChannel()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            {
                // 获取NotificationManager
                using (var notifManager = context.Call<AndroidJavaObject>("getSystemService", "notification"))
                {
                    if (GetAndroidVersion() >= 26) // Android 8.0+
                    {
                        using (var channelClass = new AndroidJavaClass("android.app.NotificationChannel"))
                        {
                            int importance = 3; // IMPORTANCE_DEFAULT
                            using (var channel = new AndroidJavaObject("android.app.NotificationChannel",
                                channelId, channelName, importance))
                            {
                                channel.Call("setDescription", "Game notifications");
                                notifManager.Call("createNotificationChannel", channel);
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Notification] Channel creation failed: {e.Message}");
        }
#endif
    }

    private void ScheduleAndroidNotification(string title, string body, int delaySeconds, int notifId)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            {
                // 创建Intent
                using (var intent = new AndroidJavaObject("android.content.Intent",
                    context, new AndroidJavaClass("com.unity3d.player.UnityPlayerActivity")))
                {
                    intent.Call<AndroidJavaObject>("putExtra", "notif_id", notifId);

                    // PendingIntent
                    using (var pendingIntentClass = new AndroidJavaClass("android.app.PendingIntent"))
                    {
                        int flags = 0x04000000; // FLAG_IMMUTABLE
                        using (var pendingIntent = pendingIntentClass.CallStatic<AndroidJavaObject>(
                            "getActivity", context, notifId, intent, flags))
                        {
                            // AlarmManager
                            using (var alarmManager = context.Call<AndroidJavaObject>("getSystemService", "alarm"))
                            {
                                long triggerTime = GetCurrentTimeMillis() + (long)delaySeconds * 1000;
                                alarmManager.Call("set", 0, triggerTime, pendingIntent); // RTC
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Notification] Schedule failed: {e.Message}");
        }
#endif
    }

    private void CancelAllAndroidNotifications()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            using (var notifManager = context.Call<AndroidJavaObject>("getSystemService", "notification"))
            {
                notifManager.Call("cancelAll");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Notification] Cancel failed: {e.Message}");
        }
#endif
    }

    private int GetAndroidVersion()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
        {
            return version.GetStatic<int>("SDK_INT");
        }
#else
        return 0;
#endif
    }

    private long GetCurrentTimeMillis()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var system = new AndroidJavaClass("java.lang.System"))
        {
            return system.CallStatic<long>("currentTimeMillis");
        }
#else
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
#endif
    }

    private string GetLocalizedText(string key, string fallback)
    {
        if (LocalizationSystem.Instance != null)
            return LocalizationSystem.Instance.Get(key, fallback);
        return fallback;
    }

    private int GenerateNotifId()
    {
        return UnityEngine.Random.Range(2000, 9999);
    }
}
