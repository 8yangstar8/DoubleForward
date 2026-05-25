using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource ambientSource;

    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    private Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = bgmVolume * masterVolume;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        bgmSource.Stop();
    }

    public void PlaySFX(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip, sfxVolume * masterVolume);
    }

    /// <summary>
    /// 播放音效（自定义音量和音高）
    /// </summary>
    public void PlaySFX(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null) return;

        float originalPitch = sfxSource.pitch;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, volume * sfxVolume * masterVolume);
        sfxSource.pitch = originalPitch;
    }

    public void PlayAmbient(AudioClip clip)
    {
        ambientSource.clip = clip;
        ambientSource.loop = true;
        ambientSource.volume = sfxVolume * masterVolume * 0.5f;
        ambientSource.Play();
    }

    public void UpdateVolumes()
    {
        bgmSource.volume = bgmVolume * masterVolume;
        sfxSource.volume = sfxVolume * masterVolume;
        ambientSource.volume = sfxVolume * masterVolume * 0.5f;
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    /// <summary>
    /// 通过键名播放音效（从Resources/Audio/SFX加载）
    /// </summary>
    public void PlaySFX(string clipKey)
    {
        if (string.IsNullOrEmpty(clipKey)) return;

        if (!clipCache.TryGetValue(clipKey, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>($"Audio/SFX/{clipKey}");
            if (clip != null)
                clipCache[clipKey] = clip;
        }

        if (clip != null)
            PlaySFX(clip);
    }

    /// <summary>
    /// 当前播放的BGM
    /// </summary>
    public AudioClip CurrentBGM => bgmSource != null ? bgmSource.clip : null;

    /// <summary>
    /// 获取主音量
    /// </summary>
    public float GetMasterVolume() => masterVolume;

    /// <summary>
    /// BGM交叉淡入淡出
    /// </summary>
    public void CrossfadeBGM(AudioClip newClip, float duration = 1.5f)
    {
        if (newClip == null) return;
        if (bgmSource.clip == newClip && bgmSource.isPlaying) return;

        StartCoroutine(DoCrossfade(newClip, duration));
    }

    private System.Collections.IEnumerator DoCrossfade(AudioClip newClip, float duration)
    {
        float halfDuration = duration * 0.5f;
        float startVol = bgmSource.volume;

        // Fade out
        float elapsed = 0;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVol, 0, elapsed / halfDuration);
            yield return null;
        }

        // Switch clip
        bgmSource.clip = newClip;
        bgmSource.Play();

        // Fade in
        float targetVol = bgmVolume * masterVolume;
        elapsed = 0;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0, targetVol, elapsed / halfDuration);
            yield return null;
        }
        bgmSource.volume = targetVol;
    }

    /// <summary>
    /// 通过键名播放BGM（从Resources/Audio/BGM加载）
    /// </summary>
    public void PlayBGM(string clipKey, bool loop = true)
    {
        if (string.IsNullOrEmpty(clipKey)) return;

        if (!clipCache.TryGetValue(clipKey, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>($"Audio/BGM/{clipKey}");
            if (clip != null)
                clipCache[clipKey] = clip;
        }

        if (clip != null)
            PlayBGM(clip, loop);
    }

    /// <summary>
    /// 通过键名播放环境音（从Resources/Audio/Ambient加载）
    /// </summary>
    public void PlayAmbient(string clipKey)
    {
        if (string.IsNullOrEmpty(clipKey)) return;

        if (!clipCache.TryGetValue(clipKey, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>($"Audio/Ambient/{clipKey}");
            if (clip != null)
                clipCache[clipKey] = clip;
        }

        if (clip != null)
            PlayAmbient(clip);
    }

    /// <summary>
    /// 停止环境音
    /// </summary>
    public void StopAmbient()
    {
        if (ambientSource != null)
            ambientSource.Stop();
    }

    /// <summary>
    /// 清理缓存
    /// </summary>
    public void ClearCache()
    {
        clipCache.Clear();
    }
}
