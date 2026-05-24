using UnityEngine;

/// <summary>
/// 无障碍/辅助功能系统 - 色盲模式、字体大小、操作辅助
/// 让更多玩家能享受游戏
/// </summary>
public class AccessibilityManager : MonoBehaviour
{
    public static AccessibilityManager Instance { get; private set; }

    public enum ColorBlindMode
    {
        None,
        Protanopia,     // 红色盲
        Deuteranopia,   // 绿色盲
        Tritanopia      // 蓝色盲
    }

    [Header("色盲模式")]
    [SerializeField] private ColorBlindMode colorBlindMode = ColorBlindMode.None;
    [SerializeField] private Material colorBlindMaterial; // 后处理材质

    [Header("字体")]
    [SerializeField] private float textSizeMultiplier = 1f;
    [SerializeField] private float minTextSize = 0.8f;
    [SerializeField] private float maxTextSize = 1.5f;

    [Header("操作辅助")]
    [SerializeField] private bool autoAimAssist = false;        // 自动瞄准辅助
    [SerializeField] private bool extendedTimers = false;        // 延长计时
    [SerializeField] private bool reducedMotion = false;         // 减少动画
    [SerializeField] private bool oneTouchMode = false;          // 简化操作
    [SerializeField] private float holdTimeMultiplier = 1f;      // 长按时间倍率

    [Header("视觉辅助")]
    [SerializeField] private bool highContrastUI = false;        // 高对比度UI
    [SerializeField] private bool showInteractIcons = true;      // 显示可交互图标
    [SerializeField] private bool screenFlashReduction = false;  // 减少闪屏
    [SerializeField] private bool subtitlesEnabled = true;       // 字幕

    [Header("音频辅助")]
    [SerializeField] private bool visualSoundIndicators = false; // 声音可视化提示
    [SerializeField] private bool monoAudio = false;             // 单声道

    private const string PREFS_PREFIX = "accessibility_";

    public ColorBlindMode CurrentColorBlindMode => colorBlindMode;
    public float TextSizeMultiplier => textSizeMultiplier;
    public bool AutoAimAssist => autoAimAssist;
    public bool ExtendedTimers => extendedTimers;
    public bool ReducedMotion => reducedMotion;
    public bool OneTouchMode => oneTouchMode;
    public float HoldTimeMultiplier => holdTimeMultiplier;
    public bool HighContrastUI => highContrastUI;
    public bool ShowInteractIcons => showInteractIcons;
    public bool ScreenFlashReduction => screenFlashReduction;
    public bool SubtitlesEnabled => subtitlesEnabled;
    public bool VisualSoundIndicators => visualSoundIndicators;
    public bool MonoAudio => monoAudio;

    public event System.Action OnSettingsChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
        ApplySettings();
    }

    /// <summary>
    /// 设置色盲模式
    /// </summary>
    public void SetColorBlindMode(ColorBlindMode mode)
    {
        colorBlindMode = mode;
        ApplyColorBlindFilter();
        SaveAndNotify();
    }

    /// <summary>
    /// 设置文字大小倍率
    /// </summary>
    public void SetTextSize(float multiplier)
    {
        textSizeMultiplier = Mathf.Clamp(multiplier, minTextSize, maxTextSize);
        SaveAndNotify();
    }

    /// <summary>
    /// 设置自动瞄准辅助
    /// </summary>
    public void SetAutoAimAssist(bool enabled)
    {
        autoAimAssist = enabled;
        SaveAndNotify();
    }

    /// <summary>
    /// 设置延长计时
    /// </summary>
    public void SetExtendedTimers(bool enabled)
    {
        extendedTimers = enabled;
        SaveAndNotify();
    }

    /// <summary>
    /// 设置减少动画
    /// </summary>
    public void SetReducedMotion(bool enabled)
    {
        reducedMotion = enabled;
        SaveAndNotify();
    }

    /// <summary>
    /// 设置单手模式
    /// </summary>
    public void SetOneTouchMode(bool enabled)
    {
        oneTouchMode = enabled;
        SaveAndNotify();
    }

    /// <summary>
    /// 设置高对比度UI
    /// </summary>
    public void SetHighContrast(bool enabled)
    {
        highContrastUI = enabled;
        SaveAndNotify();
    }

    /// <summary>
    /// 设置减少闪屏
    /// </summary>
    public void SetScreenFlashReduction(bool enabled)
    {
        screenFlashReduction = enabled;
        SaveAndNotify();
    }

    /// <summary>
    /// 设置字幕
    /// </summary>
    public void SetSubtitles(bool enabled)
    {
        subtitlesEnabled = enabled;
        SaveAndNotify();
    }

    /// <summary>
    /// 设置声音可视化
    /// </summary>
    public void SetVisualSoundIndicators(bool enabled)
    {
        visualSoundIndicators = enabled;
        SaveAndNotify();
    }

    /// <summary>
    /// 设置单声道
    /// </summary>
    public void SetMonoAudio(bool enabled)
    {
        monoAudio = enabled;
        ApplyAudioSettings();
        SaveAndNotify();
    }

    /// <summary>
    /// 获取辅助后的计时倍率
    /// </summary>
    public float GetTimerMultiplier()
    {
        return extendedTimers ? 1.5f : 1f;
    }

    /// <summary>
    /// 获取辅助后的按压容差
    /// </summary>
    public float GetHoldTolerance()
    {
        return holdTimeMultiplier;
    }

    /// <summary>
    /// 是否应该播放屏幕闪烁效果
    /// </summary>
    public bool ShouldShowFlash()
    {
        return !screenFlashReduction;
    }

    /// <summary>
    /// 是否应该播放动画过渡
    /// </summary>
    public bool ShouldShowAnimation()
    {
        return !reducedMotion;
    }

    /// <summary>
    /// 色盲模式颜色映射
    /// </summary>
    public Color RemapColor(Color original)
    {
        switch (colorBlindMode)
        {
            case ColorBlindMode.Protanopia:
                // 红色盲：红色→蓝色
                return new Color(original.b, original.g, original.r, original.a);
            case ColorBlindMode.Deuteranopia:
                // 绿色盲：绿色→蓝色
                return new Color(original.r, original.b, original.g, original.a);
            case ColorBlindMode.Tritanopia:
                // 蓝色盲：蓝色→红色
                return new Color(original.g, original.r, original.b, original.a);
            default:
                return original;
        }
    }

    private void ApplySettings()
    {
        ApplyColorBlindFilter();
        ApplyAudioSettings();
    }

    private void ApplyColorBlindFilter()
    {
        if (colorBlindMaterial == null) return;

        // 根据色盲模式设置材质参数
        switch (colorBlindMode)
        {
            case ColorBlindMode.None:
                colorBlindMaterial.SetFloat("_ColorBlindMode", 0);
                break;
            case ColorBlindMode.Protanopia:
                colorBlindMaterial.SetFloat("_ColorBlindMode", 1);
                break;
            case ColorBlindMode.Deuteranopia:
                colorBlindMaterial.SetFloat("_ColorBlindMode", 2);
                break;
            case ColorBlindMode.Tritanopia:
                colorBlindMaterial.SetFloat("_ColorBlindMode", 3);
                break;
        }
    }

    private void ApplyAudioSettings()
    {
        // Unity的AudioConfiguration
        var config = AudioSettings.GetConfiguration();
        config.speakerMode = monoAudio ? AudioSpeakerMode.Mono : AudioSpeakerMode.Stereo;
        AudioSettings.Reset(config);
    }

    private void SaveAndNotify()
    {
        SaveSettings();
        OnSettingsChanged?.Invoke();
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetInt(PREFS_PREFIX + "colorblind", (int)colorBlindMode);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "textsize", textSizeMultiplier);
        PlayerPrefs.SetInt(PREFS_PREFIX + "autoaim", autoAimAssist ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "extendtimers", extendedTimers ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "reducedmotion", reducedMotion ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "onetouch", oneTouchMode ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "highcontrast", highContrastUI ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "flashreduce", screenFlashReduction ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "subtitles", subtitlesEnabled ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "visualsound", visualSoundIndicators ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "monoaudio", monoAudio ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        colorBlindMode = (ColorBlindMode)PlayerPrefs.GetInt(PREFS_PREFIX + "colorblind", 0);
        textSizeMultiplier = PlayerPrefs.GetFloat(PREFS_PREFIX + "textsize", 1f);
        autoAimAssist = PlayerPrefs.GetInt(PREFS_PREFIX + "autoaim", 0) == 1;
        extendedTimers = PlayerPrefs.GetInt(PREFS_PREFIX + "extendtimers", 0) == 1;
        reducedMotion = PlayerPrefs.GetInt(PREFS_PREFIX + "reducedmotion", 0) == 1;
        oneTouchMode = PlayerPrefs.GetInt(PREFS_PREFIX + "onetouch", 0) == 1;
        highContrastUI = PlayerPrefs.GetInt(PREFS_PREFIX + "highcontrast", 0) == 1;
        screenFlashReduction = PlayerPrefs.GetInt(PREFS_PREFIX + "flashreduce", 0) == 1;
        subtitlesEnabled = PlayerPrefs.GetInt(PREFS_PREFIX + "subtitles", 1) == 1;
        visualSoundIndicators = PlayerPrefs.GetInt(PREFS_PREFIX + "visualsound", 0) == 1;
        monoAudio = PlayerPrefs.GetInt(PREFS_PREFIX + "monoaudio", 0) == 1;
    }
}
