using UnityEngine;

/// <summary>
/// 设置持久化 - 统一管理所有游戏设置的保存与加载
/// 整合音量、画质、语言、振动、辅助功能等设置
/// 在Boot阶段加载，任何改动即时保存
/// </summary>
public class SettingsPersistence : MonoBehaviour
{
    public static SettingsPersistence Instance { get; private set; }

    // 键名常量
    private const string KEY_BGM_VOLUME = "Settings_BGM";
    private const string KEY_SFX_VOLUME = "Settings_SFX";
    private const string KEY_LANGUAGE = "Settings_Language";
    private const string KEY_QUALITY = "Settings_Quality";
    private const string KEY_VSYNC = "Settings_VSync";
    private const string KEY_VIBRATION = "Settings_Vibration";
    private const string KEY_SCREEN_SHAKE = "Settings_ScreenShake";
    private const string KEY_COLORBLIND = "Settings_Colorblind";
    private const string KEY_SUBTITLE_SIZE = "Settings_SubtitleSize";
    private const string KEY_SHOW_TIMER = "Settings_ShowTimer";
    private const string KEY_SHOW_FPS = "Settings_ShowFPS";
    private const string KEY_TUTORIAL_ENABLED = "Settings_Tutorial";
    private const string KEY_AUTO_SAVE = "Settings_AutoSave";
    private const string KEY_FIRST_RUN = "Settings_FirstRun";

    // 当前设置值（缓存以减少PlayerPrefs读取）
    public float BGMVolume { get; private set; } = 0.7f;
    public float SFXVolume { get; private set; } = 1f;
    public int LanguageIndex { get; private set; } = 0;
    public int QualityLevel { get; private set; } = 2;
    public bool VSync { get; private set; } = true;
    public bool Vibration { get; private set; } = true;
    public bool ScreenShake { get; private set; } = true;
    public int ColorblindMode { get; private set; } = 0;
    public float SubtitleSize { get; private set; } = 1f;
    public bool ShowTimer { get; private set; } = true;
    public bool ShowFPS { get; private set; } = false;
    public bool TutorialEnabled { get; private set; } = true;
    public bool AutoSave { get; private set; } = true;
    public bool IsFirstRun { get; private set; } = true;

    public event System.Action OnSettingsChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAll();
    }

    // ============ 加载 ============

    public void LoadAll()
    {
        BGMVolume = PlayerPrefs.GetFloat(KEY_BGM_VOLUME, 0.7f);
        SFXVolume = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, 1f);
        LanguageIndex = PlayerPrefs.GetInt(KEY_LANGUAGE, 0);
        QualityLevel = PlayerPrefs.GetInt(KEY_QUALITY, 2);
        VSync = PlayerPrefs.GetInt(KEY_VSYNC, 1) == 1;
        Vibration = PlayerPrefs.GetInt(KEY_VIBRATION, 1) == 1;
        ScreenShake = PlayerPrefs.GetInt(KEY_SCREEN_SHAKE, 1) == 1;
        ColorblindMode = PlayerPrefs.GetInt(KEY_COLORBLIND, 0);
        SubtitleSize = PlayerPrefs.GetFloat(KEY_SUBTITLE_SIZE, 1f);
        ShowTimer = PlayerPrefs.GetInt(KEY_SHOW_TIMER, 1) == 1;
        ShowFPS = PlayerPrefs.GetInt(KEY_SHOW_FPS, 0) == 1;
        TutorialEnabled = PlayerPrefs.GetInt(KEY_TUTORIAL_ENABLED, 1) == 1;
        AutoSave = PlayerPrefs.GetInt(KEY_AUTO_SAVE, 1) == 1;
        IsFirstRun = PlayerPrefs.GetInt(KEY_FIRST_RUN, 1) == 1;

        ApplyAll();
    }

    // ============ 应用设置到各系统 ============

    public void ApplyAll()
    {
        // 音量
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBGMVolume(BGMVolume);
            AudioManager.Instance.SetSFXVolume(SFXVolume);
        }

        // 画质
        QualitySettings.SetQualityLevel(QualityLevel);

        // 垂直同步
        QualitySettings.vSyncCount = VSync ? 1 : 0;

        // 语言
        if (LocalizationSystem.Instance != null)
            LocalizationSystem.Instance.SetLanguage(LanguageIndex);

        // 振动
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.SetEnabled(Vibration);

        // 屏幕震动
        if (CameraShake.Instance != null)
            CameraShake.Instance.SetEnabled(ScreenShake);

        // 辅助功能
        if (AccessibilityManager.Instance != null)
            AccessibilityManager.Instance.SetColorBlindMode((AccessibilityManager.ColorBlindMode)ColorblindMode);
    }

    // ============ 设置修改（每个立即保存） ============

    public void SetBGMVolume(float volume)
    {
        BGMVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(KEY_BGM_VOLUME, BGMVolume);
        Save();
        AudioManager.Instance?.SetBGMVolume(BGMVolume);
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(KEY_SFX_VOLUME, SFXVolume);
        Save();
        AudioManager.Instance?.SetSFXVolume(SFXVolume);
    }

    public void SetLanguage(int index)
    {
        LanguageIndex = index;
        PlayerPrefs.SetInt(KEY_LANGUAGE, index);
        Save();
        LocalizationSystem.Instance?.SetLanguage(index);
        EventBus.Publish(new LanguageChangedEvent { languageIndex = index });
    }

    public void SetQuality(int level)
    {
        QualityLevel = Mathf.Clamp(level, 0, 5);
        PlayerPrefs.SetInt(KEY_QUALITY, QualityLevel);
        Save();
        QualitySettings.SetQualityLevel(QualityLevel);
    }

    public void SetVSync(bool enabled)
    {
        VSync = enabled;
        PlayerPrefs.SetInt(KEY_VSYNC, enabled ? 1 : 0);
        Save();
        QualitySettings.vSyncCount = enabled ? 1 : 0;
    }

    public void SetVibration(bool enabled)
    {
        Vibration = enabled;
        PlayerPrefs.SetInt(KEY_VIBRATION, enabled ? 1 : 0);
        Save();
        HapticFeedback.Instance?.SetEnabled(enabled);
    }

    public void SetScreenShake(bool enabled)
    {
        ScreenShake = enabled;
        PlayerPrefs.SetInt(KEY_SCREEN_SHAKE, enabled ? 1 : 0);
        Save();
        CameraShake.Instance?.SetEnabled(enabled);
    }

    public void SetColorblindMode(int mode)
    {
        ColorblindMode = mode;
        PlayerPrefs.SetInt(KEY_COLORBLIND, mode);
        Save();
        AccessibilityManager.Instance?.SetColorblindMode(mode);
    }

    public void SetSubtitleSize(float size)
    {
        SubtitleSize = Mathf.Clamp(size, 0.5f, 2f);
        PlayerPrefs.SetFloat(KEY_SUBTITLE_SIZE, SubtitleSize);
        Save();
    }

    public void SetShowTimer(bool show)
    {
        ShowTimer = show;
        PlayerPrefs.SetInt(KEY_SHOW_TIMER, show ? 1 : 0);
        Save();
    }

    public void SetShowFPS(bool show)
    {
        ShowFPS = show;
        PlayerPrefs.SetInt(KEY_SHOW_FPS, show ? 1 : 0);
        Save();
    }

    public void SetTutorialEnabled(bool enabled)
    {
        TutorialEnabled = enabled;
        PlayerPrefs.SetInt(KEY_TUTORIAL_ENABLED, enabled ? 1 : 0);
        Save();
    }

    public void SetAutoSave(bool enabled)
    {
        AutoSave = enabled;
        PlayerPrefs.SetInt(KEY_AUTO_SAVE, enabled ? 1 : 0);
        Save();
    }

    public void MarkFirstRunDone()
    {
        IsFirstRun = false;
        PlayerPrefs.SetInt(KEY_FIRST_RUN, 0);
        Save();
    }

    /// <summary>
    /// 重置所有设置为默认值
    /// </summary>
    public void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(KEY_BGM_VOLUME);
        PlayerPrefs.DeleteKey(KEY_SFX_VOLUME);
        PlayerPrefs.DeleteKey(KEY_LANGUAGE);
        PlayerPrefs.DeleteKey(KEY_QUALITY);
        PlayerPrefs.DeleteKey(KEY_VSYNC);
        PlayerPrefs.DeleteKey(KEY_VIBRATION);
        PlayerPrefs.DeleteKey(KEY_SCREEN_SHAKE);
        PlayerPrefs.DeleteKey(KEY_COLORBLIND);
        PlayerPrefs.DeleteKey(KEY_SUBTITLE_SIZE);
        PlayerPrefs.DeleteKey(KEY_SHOW_TIMER);
        PlayerPrefs.DeleteKey(KEY_SHOW_FPS);
        PlayerPrefs.DeleteKey(KEY_TUTORIAL_ENABLED);
        PlayerPrefs.DeleteKey(KEY_AUTO_SAVE);
        PlayerPrefs.Save();

        LoadAll();
    }

    private void Save()
    {
        PlayerPrefs.Save();
        OnSettingsChanged?.Invoke();
    }
}
