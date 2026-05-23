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

    [Header("Controls")]
    [SerializeField] private Slider joystickSizeSlider;
    [SerializeField] private Toggle vibrationToggle;

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
        LoadSettings();

        masterVolumeSlider?.onValueChanged.AddListener(OnMasterVolumeChanged);
        bgmVolumeSlider?.onValueChanged.AddListener(OnBGMVolumeChanged);
        sfxVolumeSlider?.onValueChanged.AddListener(OnSFXVolumeChanged);
        qualityDropdown?.onValueChanged.AddListener(OnQualityChanged);
        vsyncToggle?.onValueChanged.AddListener(OnVSyncChanged);
        backButton?.onClick.AddListener(OnBack);
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
