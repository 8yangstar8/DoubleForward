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

    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject settingsPanel;

    private const string KEY_MASTER_VOL = "MasterVolume";
    private const string KEY_BGM_VOL = "BGMVolume";
    private const string KEY_SFX_VOL = "SFXVolume";
    private const string KEY_QUALITY = "QualityLevel";
    private const string KEY_VSYNC = "VSync";
    private const string KEY_VIBRATION = "Vibration";

    void Start()
    {
        InitializeLanguageDropdown();
        InitializePerformanceDropdown();
        LoadSettings();

        masterVolumeSlider?.onValueChanged.AddListener(OnMasterVolumeChanged);
        bgmVolumeSlider?.onValueChanged.AddListener(OnBGMVolumeChanged);
        sfxVolumeSlider?.onValueChanged.AddListener(OnSFXVolumeChanged);
        qualityDropdown?.onValueChanged.AddListener(OnQualityChanged);
        vsyncToggle?.onValueChanged.AddListener(OnVSyncChanged);
        languageDropdown?.onValueChanged.AddListener(OnLanguageChanged);
        performanceDropdown?.onValueChanged.AddListener(OnPerformanceChanged);
        if (autoPerformanceToggle != null)
            autoPerformanceToggle.onValueChanged.AddListener(OnAutoPerformanceChanged);
        vibrationToggle?.onValueChanged.AddListener(OnVibrationChanged);
        backButton?.onClick.AddListener(OnBack);
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
        if (masterVolumeSlider != null)
            masterVolumeSlider.value = PlayerPrefs.GetFloat(KEY_MASTER_VOL, 1f);
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.value = PlayerPrefs.GetFloat(KEY_BGM_VOL, 0.7f);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = PlayerPrefs.GetFloat(KEY_SFX_VOL, 1f);
        if (qualityDropdown != null)
            qualityDropdown.value = PlayerPrefs.GetInt(KEY_QUALITY, QualitySettings.GetQualityLevel());
        if (vsyncToggle != null)
            vsyncToggle.isOn = PlayerPrefs.GetInt(KEY_VSYNC, 1) == 1;
        if (vibrationToggle != null)
            vibrationToggle.isOn = PlayerPrefs.GetInt(KEY_VIBRATION, 1) == 1;
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

    private void OnBack()
    {
        PlayerPrefs.Save();
        settingsPanel?.SetActive(false);
    }

    public void Show()
    {
        settingsPanel?.SetActive(true);
        LoadSettings();
    }
}
