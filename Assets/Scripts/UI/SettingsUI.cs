using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsUI : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Graphics")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle vsyncToggle;

    [Header("Language")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    [Header("Controls")]
    [SerializeField] private Slider joystickSizeSlider;
    [SerializeField] private Toggle vibrationToggle;

    [Header("Performance")]
    [SerializeField] private TMP_Dropdown performanceDropdown;
    [SerializeField] private Toggle autoPerformanceToggle;

    [Header("无障碍")]
    [SerializeField] private Toggle colorblindToggle;
    [SerializeField] private TMP_Dropdown colorblindModeDropdown;
    [SerializeField] private Slider uiScaleSlider;
    [SerializeField] private Toggle screenShakeToggle;
    [SerializeField] private Toggle screenFlashToggle;
    [SerializeField] private Toggle subtitlesToggle;
    [SerializeField] private Slider subtitleSizeSlider;

    [Header("难度")]
    [SerializeField] private TMP_Dropdown difficultyDropdown;
    [SerializeField] private Toggle autoDifficultyToggle;

    [Header("选项卡")]
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private GameObject[] tabPanels;

    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private GameObject settingsPanel;

    private const string KEY_MASTER_VOL = "MasterVolume";
    private const string KEY_BGM_VOL = "BGMVolume";
    private const string KEY_SFX_VOL = "SFXVolume";
    private const string KEY_QUALITY = "QualityLevel";
    private const string KEY_VSYNC = "VSync";
    private const string KEY_VIBRATION = "Vibration";
    private const string KEY_COLORBLIND = "ColorblindMode";
    private const string KEY_UI_SCALE = "UIScale";
    private const string KEY_SCREEN_SHAKE = "ScreenShake";
    private const string KEY_SCREEN_FLASH = "ScreenFlash";
    private const string KEY_SUBTITLES = "Subtitles";
    private const string KEY_SUBTITLE_SIZE = "SubtitleSize";

    void Start()
    {
        InitializeLanguageDropdown();
        InitializePerformanceDropdown();
        InitializeDifficultyDropdown();
        InitializeColorblindDropdown();
        InitializeTabs();
        LoadSettings();

        // 音频
        masterVolumeSlider?.onValueChanged.AddListener(OnMasterVolumeChanged);
        bgmVolumeSlider?.onValueChanged.AddListener(OnBGMVolumeChanged);
        sfxVolumeSlider?.onValueChanged.AddListener(OnSFXVolumeChanged);

        // 图形
        qualityDropdown?.onValueChanged.AddListener(OnQualityChanged);
        vsyncToggle?.onValueChanged.AddListener(OnVSyncChanged);

        // 语言
        languageDropdown?.onValueChanged.AddListener(OnLanguageChanged);

        // 性能
        performanceDropdown?.onValueChanged.AddListener(OnPerformanceChanged);
        if (autoPerformanceToggle != null)
            autoPerformanceToggle.onValueChanged.AddListener(OnAutoPerformanceChanged);

        // 控制
        vibrationToggle?.onValueChanged.AddListener(OnVibrationChanged);

        // 无障碍
        colorblindToggle?.onValueChanged.AddListener(OnColorblindChanged);
        colorblindModeDropdown?.onValueChanged.AddListener(OnColorblindModeChanged);
        uiScaleSlider?.onValueChanged.AddListener(OnUIScaleChanged);
        screenShakeToggle?.onValueChanged.AddListener(OnScreenShakeChanged);
        screenFlashToggle?.onValueChanged.AddListener(OnScreenFlashChanged);
        subtitlesToggle?.onValueChanged.AddListener(OnSubtitlesChanged);
        subtitleSizeSlider?.onValueChanged.AddListener(OnSubtitleSizeChanged);

        // 难度
        difficultyDropdown?.onValueChanged.AddListener(OnDifficultyChanged);
        autoDifficultyToggle?.onValueChanged.AddListener(OnAutoDifficultyChanged);

        // 导航
        backButton?.onClick.AddListener(OnBack);
        resetButton?.onClick.AddListener(OnResetDefaults);
    }

    private void InitializeLanguageDropdown()
    {
        if (languageDropdown == null || LocalizationSystem.Instance == null) return;

        languageDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<string>();
        foreach (var lang in LocalizationSystem.Instance.GetSupportedLanguages())
        {
            options.Add(LocalizationSystem.GetLanguageDisplayName(lang));
        }
        languageDropdown.AddOptions(options);
        languageDropdown.value = (int)LocalizationSystem.Instance.CurrentLanguage;
    }

    private void InitializePerformanceDropdown()
    {
        if (performanceDropdown == null) return;

        performanceDropdown.ClearOptions();
        performanceDropdown.AddOptions(new System.Collections.Generic.List<string> { "低", "中", "高" });

        if (PerformanceManager.Instance != null)
            performanceDropdown.value = (int)PerformanceManager.Instance.CurrentLevel;
    }

    private void LoadSettings()
    {
        // 音频
        if (masterVolumeSlider != null)
            masterVolumeSlider.value = PlayerPrefs.GetFloat(KEY_MASTER_VOL, 1f);
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.value = PlayerPrefs.GetFloat(KEY_BGM_VOL, 0.7f);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = PlayerPrefs.GetFloat(KEY_SFX_VOL, 1f);

        // 图形
        if (qualityDropdown != null)
            qualityDropdown.value = PlayerPrefs.GetInt(KEY_QUALITY, QualitySettings.GetQualityLevel());
        if (vsyncToggle != null)
            vsyncToggle.isOn = PlayerPrefs.GetInt(KEY_VSYNC, 1) == 1;

        // 操作
        if (vibrationToggle != null)
            vibrationToggle.isOn = PlayerPrefs.GetInt(KEY_VIBRATION, 1) == 1;

        // 无障碍
        if (colorblindToggle != null)
            colorblindToggle.isOn = PlayerPrefs.GetInt(KEY_COLORBLIND, 0) != 0;
        if (colorblindModeDropdown != null)
        {
            colorblindModeDropdown.value = PlayerPrefs.GetInt(KEY_COLORBLIND, 0);
            colorblindModeDropdown.interactable = colorblindToggle?.isOn ?? false;
        }
        if (uiScaleSlider != null)
            uiScaleSlider.value = PlayerPrefs.GetFloat(KEY_UI_SCALE, 1f);
        if (screenShakeToggle != null)
            screenShakeToggle.isOn = PlayerPrefs.GetInt(KEY_SCREEN_SHAKE, 1) == 1;
        if (screenFlashToggle != null)
            screenFlashToggle.isOn = PlayerPrefs.GetInt(KEY_SCREEN_FLASH, 1) == 1;
        if (subtitlesToggle != null)
            subtitlesToggle.isOn = PlayerPrefs.GetInt(KEY_SUBTITLES, 1) == 1;
        if (subtitleSizeSlider != null)
            subtitleSizeSlider.value = PlayerPrefs.GetFloat(KEY_SUBTITLE_SIZE, 1f);
    }

    private void OnMasterVolumeChanged(float value)
    {
        AudioManager.Instance?.SetMasterVolume(value);
        PlayerPrefs.SetFloat(KEY_MASTER_VOL, value);
    }

    private void OnBGMVolumeChanged(float value)
    {
        AudioManager.Instance?.SetBGMVolume(value);
        PlayerPrefs.SetFloat(KEY_BGM_VOL, value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        AudioManager.Instance?.SetSFXVolume(value);
        PlayerPrefs.SetFloat(KEY_SFX_VOL, value);
    }

    private void OnQualityChanged(int index)
    {
        QualitySettings.SetQualityLevel(index);
        PlayerPrefs.SetInt(KEY_QUALITY, index);
    }

    private void OnVSyncChanged(bool enabled)
    {
        QualitySettings.vSyncCount = enabled ? 1 : 0;
        PlayerPrefs.SetInt(KEY_VSYNC, enabled ? 1 : 0);
    }

    private void OnLanguageChanged(int index)
    {
        if (LocalizationSystem.Instance != null)
            LocalizationSystem.Instance.SetLanguage((LocalizationSystem.Language)index);
    }

    private void OnPerformanceChanged(int index)
    {
        if (PerformanceManager.Instance != null)
            PerformanceManager.Instance.SetPerformanceLevel((PerformanceManager.PerformanceLevel)index);
    }

    private void OnAutoPerformanceChanged(bool enabled)
    {
        if (PerformanceManager.Instance != null)
            PerformanceManager.Instance.SetAutoAdjust(enabled);
    }

    private void OnVibrationChanged(bool enabled)
    {
        PlayerPrefs.SetInt(KEY_VIBRATION, enabled ? 1 : 0);
        if (GamepadAdapter.Instance != null)
            GamepadAdapter.Instance.SetVibrationEnabled(enabled);
    }

    // ==================== 无障碍 ====================

    private void InitializeColorblindDropdown()
    {
        if (colorblindModeDropdown == null) return;

        colorblindModeDropdown.ClearOptions();
        colorblindModeDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "无", "红绿色弱", "蓝黄色弱", "全色盲"
        });
    }

    private void InitializeDifficultyDropdown()
    {
        if (difficultyDropdown == null) return;

        difficultyDropdown.ClearOptions();
        difficultyDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "轻松", "普通", "困难"
        });

        if (DifficultyManager.Instance != null)
        {
            float mod = DifficultyManager.Instance.DifficultyModifier;
            if (mod <= 0.7f) difficultyDropdown.value = 0;
            else if (mod <= 1.1f) difficultyDropdown.value = 1;
            else difficultyDropdown.value = 2;
        }
    }

    private void InitializeTabs()
    {
        if (tabButtons == null || tabPanels == null) return;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            int index = i;
            tabButtons[i].onClick.AddListener(() => SwitchTab(index));
        }

        // 默认显示第一个选项卡
        SwitchTab(0);
    }

    private void SwitchTab(int index)
    {
        for (int i = 0; i < (tabPanels?.Length ?? 0); i++)
        {
            if (tabPanels[i] != null)
                tabPanels[i].SetActive(i == index);
        }

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    private void OnColorblindChanged(bool enabled)
    {
        if (colorblindModeDropdown != null)
            colorblindModeDropdown.interactable = enabled;

        if (!enabled && AccessibilityManager.Instance != null)
            AccessibilityManager.Instance.SetColorBlindMode(AccessibilityManager.ColorBlindMode.None);

        PlayerPrefs.SetInt(KEY_COLORBLIND, enabled ? 1 : 0);
    }

    private void OnColorblindModeChanged(int index)
    {
        if (AccessibilityManager.Instance != null)
            AccessibilityManager.Instance.SetColorBlindMode((AccessibilityManager.ColorBlindMode)index);

        PlayerPrefs.SetInt(KEY_COLORBLIND, index);
    }

    private void OnUIScaleChanged(float value)
    {
        if (AccessibilityManager.Instance != null)
            AccessibilityManager.Instance.SetTextSize(value);

        PlayerPrefs.SetFloat(KEY_UI_SCALE, value);
    }

    private void OnScreenShakeChanged(bool enabled)
    {
        if (CameraShake.Instance != null)
            CameraShake.Instance.SetEnabled(enabled);

        if (AccessibilityManager.Instance != null)
            AccessibilityManager.Instance.SetReducedMotion(!enabled);

        PlayerPrefs.SetInt(KEY_SCREEN_SHAKE, enabled ? 1 : 0);
    }

    private void OnScreenFlashChanged(bool enabled)
    {
        if (AccessibilityManager.Instance != null)
            AccessibilityManager.Instance.SetScreenFlashReduction(!enabled);

        PlayerPrefs.SetInt(KEY_SCREEN_FLASH, enabled ? 1 : 0);
    }

    private void OnSubtitlesChanged(bool enabled)
    {
        if (AccessibilityManager.Instance != null)
            AccessibilityManager.Instance.SetSubtitles(enabled);

        PlayerPrefs.SetInt(KEY_SUBTITLES, enabled ? 1 : 0);
    }

    private void OnSubtitleSizeChanged(float value)
    {
        if (AccessibilityManager.Instance != null)
            AccessibilityManager.Instance.SetTextSize(value);

        PlayerPrefs.SetFloat(KEY_SUBTITLE_SIZE, value);
    }

    // ==================== 难度 ====================

    private void OnDifficultyChanged(int index)
    {
        if (DifficultyManager.Instance == null) return;

        var level = index switch
        {
            0 => DifficultyManager.DifficultyLevel.Easy,
            1 => DifficultyManager.DifficultyLevel.Normal,
            2 => DifficultyManager.DifficultyLevel.Hard,
            _ => DifficultyManager.DifficultyLevel.Normal
        };

        DifficultyManager.Instance.SetDifficulty(level);
    }

    private void OnAutoDifficultyChanged(bool enabled)
    {
        if (DifficultyManager.Instance != null)
        {
            if (enabled)
                DifficultyManager.Instance.SetDifficulty(DifficultyManager.DifficultyLevel.Adaptive);
        }
    }

    // ==================== 导航 ====================

    private void OnBack()
    {
        PlayerPrefs.Save();
        settingsPanel?.SetActive(false);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    private void OnResetDefaults()
    {
        // 重置音量
        if (masterVolumeSlider != null) masterVolumeSlider.value = 1f;
        if (bgmVolumeSlider != null) bgmVolumeSlider.value = 0.7f;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = 1f;

        // 重置图形
        if (vsyncToggle != null) vsyncToggle.isOn = true;

        // 重置无障碍
        if (screenShakeToggle != null) screenShakeToggle.isOn = true;
        if (screenFlashToggle != null) screenFlashToggle.isOn = true;
        if (colorblindToggle != null) colorblindToggle.isOn = false;
        if (uiScaleSlider != null) uiScaleSlider.value = 1f;
        if (subtitlesToggle != null) subtitlesToggle.isOn = true;

        // 重置操作
        if (vibrationToggle != null) vibrationToggle.isOn = true;

        // 重置难度
        if (difficultyDropdown != null) difficultyDropdown.value = 1;
        if (autoDifficultyToggle != null) autoDifficultyToggle.isOn = true;

        PlayerPrefs.Save();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();
    }

    public void Show()
    {
        settingsPanel?.SetActive(true);
        LoadSettings();
    }
}
