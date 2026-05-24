using UnityEngine;

/// <summary>
/// 触觉反馈系统 - Android振动控制
/// 支持不同强度和模式的振动，用于战斗命中、UI交互、关卡完成等
/// 需要AndroidManifest中声明VIBRATE权限
/// </summary>
public class HapticFeedback : MonoBehaviour
{
    public static HapticFeedback Instance { get; private set; }

    [Header("设置")]
    [SerializeField] private bool enableHaptics = true;
    [SerializeField] private float globalIntensity = 1f;

    private const string HAPTIC_ENABLED_KEY = "haptics_enabled";

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject vibrator;
    private bool hasVibrator;
    private int androidVersion;
#endif

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        enableHaptics = PlayerPrefs.GetInt(HAPTIC_ENABLED_KEY, 1) == 1;
        InitializeVibrator();
    }

    private void InitializeVibrator()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                hasVibrator = vibrator.Call<bool>("hasVibrator");

                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    androidVersion = version.GetStatic<int>("SDK_INT");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Haptic] Init failed: {e.Message}");
            hasVibrator = false;
        }
#endif
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 启用/禁用触觉反馈
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        enableHaptics = enabled;
        PlayerPrefs.SetInt(HAPTIC_ENABLED_KEY, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public bool IsEnabled() => enableHaptics;

    /// <summary>
    /// 轻触反馈（UI点击、收集品拾取）
    /// </summary>
    public void Light()
    {
        Vibrate(20, 0.3f);
    }

    /// <summary>
    /// 中等反馈（普通攻击命中、跳跃着地）
    /// </summary>
    public void Medium()
    {
        Vibrate(40, 0.6f);
    }

    /// <summary>
    /// 重击反馈（蓄力攻击、Boss攻击）
    /// </summary>
    public void Heavy()
    {
        Vibrate(80, 1f);
    }

    /// <summary>
    /// 成功反馈（关卡完成、成就解锁）
    /// </summary>
    public void Success()
    {
        VibratePattern(new long[] { 0, 30, 50, 60 }, new int[] { 0, 128, 0, 200 });
    }

    /// <summary>
    /// 失败反馈（玩家死亡、Game Over）
    /// </summary>
    public void Failure()
    {
        VibratePattern(new long[] { 0, 100, 80, 100, 80, 200 },
                       new int[] { 0, 180, 0, 120, 0, 255 });
    }

    /// <summary>
    /// 警告反馈（低血量、Boss出现）
    /// </summary>
    public void Warning()
    {
        VibratePattern(new long[] { 0, 50, 100, 50 }, new int[] { 0, 160, 0, 160 });
    }

    /// <summary>
    /// 连击反馈（连续命中，强度递增）
    /// </summary>
    public void ComboHit(int comboCount)
    {
        float intensity = Mathf.Clamp01(0.3f + comboCount * 0.1f);
        int duration = Mathf.Clamp(15 + comboCount * 5, 15, 80);
        Vibrate(duration, intensity);
    }

    /// <summary>
    /// 自定义振动
    /// </summary>
    public void Custom(int durationMs, float intensity = 1f)
    {
        Vibrate(durationMs, intensity);
    }

    /// <summary>
    /// 预设枚举（方便引用）
    /// </summary>
    public enum HapticPreset { Light, Medium, Heavy, Success, Failure, Warning }

    /// <summary>
    /// 通过预设枚举播放振动
    /// </summary>
    public void PlayPreset(HapticPreset preset)
    {
        switch (preset)
        {
            case HapticPreset.Light: Light(); break;
            case HapticPreset.Medium: Medium(); break;
            case HapticPreset.Heavy: Heavy(); break;
            case HapticPreset.Success: Success(); break;
            case HapticPreset.Failure: Failure(); break;
            case HapticPreset.Warning: Warning(); break;
        }
    }

    /// <summary>
    /// 取消振动
    /// </summary>
    public void Cancel()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (vibrator != null)
        {
            try { vibrator.Call("cancel"); }
            catch { }
        }
#endif
    }

    // ==================== 内部实现 ====================

    private void Vibrate(int durationMs, float intensity)
    {
        if (!enableHaptics) return;

        float adjustedIntensity = intensity * globalIntensity;
        int adjustedDuration = Mathf.Max(1, durationMs);

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!hasVibrator || vibrator == null) return;

        try
        {
            if (androidVersion >= 26) // Android 8.0+ (API 26)
            {
                // 使用VibrationEffect
                int amplitude = Mathf.Clamp(Mathf.RoundToInt(adjustedIntensity * 255), 1, 255);

                using (var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                {
                    using (var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot", (long)adjustedDuration, amplitude))
                    {
                        vibrator.Call("vibrate", effect);
                    }
                }
            }
            else
            {
                // 旧版API
                vibrator.Call("vibrate", (long)adjustedDuration);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Haptic] Vibrate failed: {e.Message}");
        }
#else
        // 编辑器中模拟
        Debug.Log($"[Haptic] Vibrate {adjustedDuration}ms @ {adjustedIntensity:F2}");
#endif
    }

    private void VibratePattern(long[] timings, int[] amplitudes)
    {
        if (!enableHaptics) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!hasVibrator || vibrator == null) return;

        try
        {
            if (androidVersion >= 26)
            {
                // 调整强度
                int[] adjusted = new int[amplitudes.Length];
                for (int i = 0; i < amplitudes.Length; i++)
                    adjusted[i] = Mathf.Clamp(Mathf.RoundToInt(amplitudes[i] * globalIntensity), 0, 255);

                using (var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                {
                    using (var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createWaveform", timings, adjusted, -1))
                    {
                        vibrator.Call("vibrate", effect);
                    }
                }
            }
            else
            {
                vibrator.Call("vibrate", timings, -1);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Haptic] VibratePattern failed: {e.Message}");
        }
#else
        Debug.Log($"[Haptic] Pattern vibrate ({timings.Length} segments)");
#endif
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (vibrator != null)
        {
            try { vibrator.Call("cancel"); }
            catch { }
            vibrator.Dispose();
            vibrator = null;
        }
#endif
    }
}
