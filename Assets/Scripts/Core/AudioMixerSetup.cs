using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

/// <summary>
/// 音频混音器配置 - 管理BGM/SFX/环境音的音量和效果
/// 与AudioManager和SettingsUI配合使用
/// </summary>
public class AudioMixerSetup : MonoBehaviour
{
    public static AudioMixerSetup Instance { get; private set; }

    [Header("Mixer引用")]
    [SerializeField] private AudioMixer mainMixer;

    [Header("参数名（对应AudioMixer中的Exposed Parameters）")]
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string bgmVolumeParam = "BGMVolume";
    [SerializeField] private string sfxVolumeParam = "SFXVolume";
    [SerializeField] private string ambientVolumeParam = "AmbientVolume";
    [SerializeField] private string uiVolumeParam = "UIVolume";

    [Header("低通滤波（暂停时使用）")]
    [SerializeField] private string lowPassParam = "LowPassCutoff";
    [SerializeField] private float pausedLowPassFreq = 800f;
    [SerializeField] private float normalLowPassFreq = 22000f;

    [Header("快照")]
    [SerializeField] private AudioMixerSnapshot normalSnapshot;
    [SerializeField] private AudioMixerSnapshot pausedSnapshot;
    [SerializeField] private AudioMixerSnapshot underwaterSnapshot;
    [SerializeField] private AudioMixerSnapshot bossSnapshot;
    [SerializeField] private float snapshotTransitionTime = 0.5f;

    // PlayerPrefs keys
    private const string PREFS_MASTER = "audio_master";
    private const string PREFS_BGM = "audio_bgm";
    private const string PREFS_SFX = "audio_sfx";
    private const string PREFS_AMBIENT = "audio_ambient";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadVolumeSettings();
    }

    void OnEnable()
    {
        // 订阅游戏流程事件
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.OnStateChanged += OnGameStateChanged;
    }

    void OnDisable()
    {
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.OnStateChanged -= OnGameStateChanged;
    }

    // ============ 音量控制 ============

    /// <summary>
    /// 设置主音量 (0-1)
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        SetMixerVolume(masterVolumeParam, volume);
        PlayerPrefs.SetFloat(PREFS_MASTER, volume);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 设置BGM音量 (0-1)
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        SetMixerVolume(bgmVolumeParam, volume);
        PlayerPrefs.SetFloat(PREFS_BGM, volume);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 设置音效音量 (0-1)
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        SetMixerVolume(sfxVolumeParam, volume);
        PlayerPrefs.SetFloat(PREFS_SFX, volume);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 设置环境音量 (0-1)
    /// </summary>
    public void SetAmbientVolume(float volume)
    {
        SetMixerVolume(ambientVolumeParam, volume);
        PlayerPrefs.SetFloat(PREFS_AMBIENT, volume);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 获取当前音量值
    /// </summary>
    public float GetMasterVolume() => PlayerPrefs.GetFloat(PREFS_MASTER, 1f);
    public float GetBGMVolume() => PlayerPrefs.GetFloat(PREFS_BGM, 0.8f);
    public float GetSFXVolume() => PlayerPrefs.GetFloat(PREFS_SFX, 1f);
    public float GetAmbientVolume() => PlayerPrefs.GetFloat(PREFS_AMBIENT, 0.7f);

    // ============ 快照切换 ============

    /// <summary>
    /// 切换到暂停音频状态（低通+降音量）
    /// </summary>
    public void TransitionToPaused()
    {
        if (pausedSnapshot != null)
            pausedSnapshot.TransitionTo(snapshotTransitionTime);
        else
            SetLowPassFilter(pausedLowPassFreq);
    }

    /// <summary>
    /// 恢复正常音频
    /// </summary>
    public void TransitionToNormal()
    {
        if (normalSnapshot != null)
            normalSnapshot.TransitionTo(snapshotTransitionTime);
        else
            SetLowPassFilter(normalLowPassFreq);
    }

    /// <summary>
    /// 水下效果（第三章深渊）
    /// </summary>
    public void TransitionToUnderwater()
    {
        if (underwaterSnapshot != null)
            underwaterSnapshot.TransitionTo(snapshotTransitionTime);
    }

    /// <summary>
    /// Boss战音频（增强低音、提升音量）
    /// </summary>
    public void TransitionToBoss()
    {
        if (bossSnapshot != null)
            bossSnapshot.TransitionTo(snapshotTransitionTime);
    }

    // ============ 特殊效果 ============

    /// <summary>
    /// 低生命值心跳效果 - 降低BGM音量，突出心跳SFX
    /// </summary>
    public void SetLowHealthAudio(bool active)
    {
        if (mainMixer == null) return;

        if (active)
        {
            mainMixer.SetFloat(bgmVolumeParam, VolumeToDecibel(0.3f));
        }
        else
        {
            float savedBGM = PlayerPrefs.GetFloat(PREFS_BGM, 0.8f);
            mainMixer.SetFloat(bgmVolumeParam, VolumeToDecibel(savedBGM));
        }
    }

    /// <summary>
    /// 临时静音所有音频（如过场前）
    /// </summary>
    public void MuteAll(bool mute)
    {
        if (mainMixer == null) return;
        mainMixer.SetFloat(masterVolumeParam, mute ? -80f : VolumeToDecibel(GetMasterVolume()));
    }

    /// <summary>
    /// 淡入BGM
    /// </summary>
    public void FadeInBGM(float duration = 1f)
    {
        StartCoroutine(FadeVolume(bgmVolumeParam, -80f,
            VolumeToDecibel(GetBGMVolume()), duration));
    }

    /// <summary>
    /// 淡出BGM
    /// </summary>
    public void FadeOutBGM(float duration = 1f)
    {
        StartCoroutine(FadeVolume(bgmVolumeParam,
            VolumeToDecibel(GetBGMVolume()), -80f, duration));
    }

    // ============ 内部方法 ============

    private void SetMixerVolume(string parameter, float normalizedVolume)
    {
        if (mainMixer == null) return;
        mainMixer.SetFloat(parameter, VolumeToDecibel(normalizedVolume));
    }

    private void SetLowPassFilter(float frequency)
    {
        if (mainMixer == null) return;
        mainMixer.SetFloat(lowPassParam, frequency);
    }

    /// <summary>
    /// 将0-1的线性音量转换为分贝值
    /// </summary>
    private float VolumeToDecibel(float volume)
    {
        if (volume <= 0.0001f) return -80f;
        return Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f;
    }

    /// <summary>
    /// 将分贝转换为0-1线性音量
    /// </summary>
    private float DecibelToVolume(float db)
    {
        return Mathf.Pow(10f, db / 20f);
    }

    private System.Collections.IEnumerator FadeVolume(string param, float from, float to, float duration)
    {
        if (mainMixer == null) yield break;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float value = Mathf.Lerp(from, to, t);
            mainMixer.SetFloat(param, value);
            yield return null;
        }
        mainMixer.SetFloat(param, to);
    }

    private void LoadVolumeSettings()
    {
        SetMixerVolume(masterVolumeParam, GetMasterVolume());
        SetMixerVolume(bgmVolumeParam, GetBGMVolume());
        SetMixerVolume(sfxVolumeParam, GetSFXVolume());
        SetMixerVolume(ambientVolumeParam, GetAmbientVolume());
    }

    private void OnGameStateChanged(GameFlowManager.FlowState oldState, GameFlowManager.FlowState newState)
    {
        switch (newState)
        {
            case GameFlowManager.FlowState.Paused:
                TransitionToPaused();
                break;
            case GameFlowManager.FlowState.Playing:
                TransitionToNormal();
                break;
            case GameFlowManager.FlowState.BossBattle:
                TransitionToBoss();
                break;
        }
    }
}
