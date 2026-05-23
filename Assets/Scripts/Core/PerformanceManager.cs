using UnityEngine;
using System.Collections;

/// <summary>
/// Android 性能管理器 - 动态调整画质、帧率、分辨率
/// 根据设备性能自动适配最佳设置
/// </summary>
public class PerformanceManager : MonoBehaviour
{
    public static PerformanceManager Instance { get; private set; }

    public enum PerformanceLevel
    {
        Low,
        Medium,
        High
    }

    [Header("帧率设置")]
    [SerializeField] private int targetFrameRate = 60;
    [SerializeField] private int lowEndFrameRate = 30;
    [SerializeField] private float fpsCheckInterval = 5f;
    [SerializeField] private float lowFpsThreshold = 25f;

    [Header("分辨率缩放")]
    [SerializeField] private float highResScale = 1.0f;
    [SerializeField] private float mediumResScale = 0.85f;
    [SerializeField] private float lowResScale = 0.7f;

    [Header("自动适配")]
    [SerializeField] private bool autoAdjust = true;
    [SerializeField] private int autoAdjustSamples = 10;

    private PerformanceLevel currentLevel = PerformanceLevel.High;
    private float[] fpsSamples;
    private int sampleIndex;
    private float fpsTimer;
    private int frameCount;
    private float currentFps;

    private const string PERF_LEVEL_KEY = "performance_level";
    private const string AUTO_ADJUST_KEY = "auto_adjust";

    public PerformanceLevel CurrentLevel => currentLevel;
    public float CurrentFPS => currentFps;

    public event System.Action<PerformanceLevel> OnPerformanceLevelChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        fpsSamples = new float[autoAdjustSamples];
        LoadSettings();
        ApplySettings();
    }

    void Update()
    {
        TrackFPS();

        if (autoAdjust)
            AutoAdjustPerformance();
    }

    private void TrackFPS()
    {
        frameCount++;
        fpsTimer += Time.unscaledDeltaTime;

        if (fpsTimer >= 1f)
        {
            currentFps = frameCount / fpsTimer;
            frameCount = 0;
            fpsTimer = 0;

            fpsSamples[sampleIndex] = currentFps;
            sampleIndex = (sampleIndex + 1) % fpsSamples.Length;
        }
    }

    private void AutoAdjustPerformance()
    {
        // 每隔一段时间检查平均FPS
        if (Time.frameCount % (int)(fpsCheckInterval * 60) != 0) return;

        float avgFps = GetAverageFPS();
        if (avgFps <= 0) return;

        if (avgFps < lowFpsThreshold && currentLevel != PerformanceLevel.Low)
        {
            // FPS太低，降低画质
            PerformanceLevel newLevel = currentLevel == PerformanceLevel.High ?
                PerformanceLevel.Medium : PerformanceLevel.Low;
            SetPerformanceLevel(newLevel);
        }
    }

    private float GetAverageFPS()
    {
        float sum = 0;
        int count = 0;
        for (int i = 0; i < fpsSamples.Length; i++)
        {
            if (fpsSamples[i] > 0)
            {
                sum += fpsSamples[i];
                count++;
            }
        }
        return count > 0 ? sum / count : 0;
    }

    /// <summary>
    /// 手动设置性能等级
    /// </summary>
    public void SetPerformanceLevel(PerformanceLevel level)
    {
        if (currentLevel == level) return;
        currentLevel = level;
        ApplySettings();
        SaveSettings();
        OnPerformanceLevelChanged?.Invoke(level);
    }

    public void SetAutoAdjust(bool enabled)
    {
        autoAdjust = enabled;
        PlayerPrefs.SetInt(AUTO_ADJUST_KEY, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplySettings()
    {
        switch (currentLevel)
        {
            case PerformanceLevel.Low:
                ApplyLowSettings();
                break;
            case PerformanceLevel.Medium:
                ApplyMediumSettings();
                break;
            case PerformanceLevel.High:
                ApplyHighSettings();
                break;
        }
    }

    private void ApplyLowSettings()
    {
        Application.targetFrameRate = lowEndFrameRate;
        QualitySettings.SetQualityLevel(0);
        SetResolutionScale(lowResScale);
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.antiAliasing = 0;
        QualitySettings.vSyncCount = 0;
    }

    private void ApplyMediumSettings()
    {
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.SetQualityLevel(1);
        SetResolutionScale(mediumResScale);
        QualitySettings.shadows = ShadowQuality.HardOnly;
        QualitySettings.antiAliasing = 2;
        QualitySettings.vSyncCount = 0;
    }

    private void ApplyHighSettings()
    {
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.SetQualityLevel(2);
        SetResolutionScale(highResScale);
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.antiAliasing = 4;
        QualitySettings.vSyncCount = 0;
    }

    private void SetResolutionScale(float scale)
    {
        int width = (int)(Screen.currentResolution.width * scale);
        int height = (int)(Screen.currentResolution.height * scale);
        Screen.SetResolution(width, height, true);
    }

    /// <summary>
    /// 根据设备性能自动选择初始等级
    /// </summary>
    public static PerformanceLevel DetectDeviceLevel()
    {
        int processorCount = SystemInfo.processorCount;
        int systemMemoryMB = SystemInfo.systemMemorySize;
        int graphicsMemoryMB = SystemInfo.graphicsMemorySize;

        // 高端设备: 6+核心, 4GB+内存, 2GB+显存
        if (processorCount >= 6 && systemMemoryMB >= 4000 && graphicsMemoryMB >= 2000)
            return PerformanceLevel.High;

        // 中端设备: 4+核心, 3GB+内存
        if (processorCount >= 4 && systemMemoryMB >= 3000)
            return PerformanceLevel.Medium;

        return PerformanceLevel.Low;
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetInt(PERF_LEVEL_KEY, (int)currentLevel);
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        if (PlayerPrefs.HasKey(PERF_LEVEL_KEY))
        {
            currentLevel = (PerformanceLevel)PlayerPrefs.GetInt(PERF_LEVEL_KEY);
        }
        else
        {
            currentLevel = DetectDeviceLevel();
        }

        autoAdjust = PlayerPrefs.GetInt(AUTO_ADJUST_KEY, 1) == 1;
    }
}
