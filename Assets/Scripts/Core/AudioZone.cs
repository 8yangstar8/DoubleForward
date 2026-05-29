using UnityEngine;
using System.Collections;

/// <summary>
/// 音频区域 - 进入时切换BGM或增加环境音效层
/// 用于不同区域的氛围切换（洞穴、水下、Boss前奏等）
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class AudioZone : MonoBehaviour
{
    public enum AudioZoneType
    {
        BGMChange,          // 切换BGM
        AmbientLayer,       // 添加环境音效层
        VolumeModifier,     // 修改整体音量
        MusicIntensity,     // 音乐强度变化（战斗→探索）
        Reverb              // 混响效果（洞穴、大厅）
    }

    [Header("类型")]
    [SerializeField] private AudioZoneType zoneType = AudioZoneType.BGMChange;

    [Header("BGM切换")]
    [SerializeField] private AudioClip newBGM;
    [SerializeField] private float bgmCrossfadeDuration = 1.5f;
    [SerializeField] private bool restorePreviousBGM = true;

    [Header("环境音")]
    [SerializeField] private AudioClip ambientClip;
    [SerializeField] private float ambientVolume = 0.5f;
    [SerializeField] private bool loopAmbient = true;

    [Header("音量修改")]
    [SerializeField] private float volumeMultiplier = 0.5f;
    [SerializeField] private float fadeTime = 0.5f;

    [Header("通用")]
    [SerializeField] private float transitionDuration = 1f;

    private AudioSource ambientSource;
    private AudioClip previousBGM;
    private float previousVolume;
    private bool isActive;

    void Awake()
    {
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;

        // 为环境音创建AudioSource
        if (zoneType == AudioZoneType.AmbientLayer && ambientClip != null)
        {
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.clip = ambientClip;
            ambientSource.loop = loopAmbient;
            ambientSource.volume = 0;
            ambientSource.playOnAwake = false;
            ambientSource.spatialBlend = 0; // 2D
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (isActive) return;

        isActive = true;
        ApplyEffect();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (!isActive) return;

        isActive = false;
        if (restorePreviousBGM || zoneType != AudioZoneType.BGMChange)
            RemoveEffect();
    }

    private void ApplyEffect()
    {
        switch (zoneType)
        {
            case AudioZoneType.BGMChange:
                if (AudioManager.Instance != null && newBGM != null)
                {
                    previousBGM = AudioManager.Instance.CurrentBGM;
                    AudioManager.Instance.CrossfadeBGM(newBGM, bgmCrossfadeDuration);
                }
                break;

            case AudioZoneType.AmbientLayer:
                if (ambientSource != null)
                {
                    ambientSource.Play();
                    StartCoroutine(FadeAudioSource(ambientSource, 0, ambientVolume, transitionDuration));
                }
                break;

            case AudioZoneType.VolumeModifier:
                if (AudioManager.Instance != null)
                {
                    previousVolume = AudioManager.Instance.GetMasterVolume();
                    AudioManager.Instance.SetMasterVolume(previousVolume * volumeMultiplier);
                }
                break;
        }
    }

    private void RemoveEffect()
    {
        switch (zoneType)
        {
            case AudioZoneType.BGMChange:
                if (AudioManager.Instance != null && previousBGM != null)
                {
                    AudioManager.Instance.CrossfadeBGM(previousBGM, bgmCrossfadeDuration);
                }
                break;

            case AudioZoneType.AmbientLayer:
                if (ambientSource != null)
                {
                    StartCoroutine(FadeOutAndStop(ambientSource, transitionDuration));
                }
                break;

            case AudioZoneType.VolumeModifier:
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.SetMasterVolume(previousVolume);
                }
                break;
        }
    }

    private System.Collections.IEnumerator FadeAudioSource(AudioSource source, float from, float to, float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        source.volume = to;
    }

    private System.Collections.IEnumerator FadeOutAndStop(AudioSource source, float duration)
    {
        float startVol = source.volume;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVol, 0, elapsed / duration);
            yield return null;
        }
        source.volume = 0;
        source.Stop();
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Color color;
        switch (zoneType)
        {
            case AudioZoneType.BGMChange: color = new Color(1f, 0.5f, 0f, 0.2f); break;
            case AudioZoneType.AmbientLayer: color = new Color(0f, 0.5f, 1f, 0.2f); break;
            case AudioZoneType.VolumeModifier: color = new Color(0.5f, 0.5f, 0.5f, 0.2f); break;
            default: color = new Color(1f, 1f, 0f, 0.2f); break;
        }

        Gizmos.color = color;
        Vector3 center = transform.position + (Vector3)col.offset;
        Vector3 size = Vector3.Scale(col.size, transform.lossyScale);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(color.r, color.g, color.b, 0.8f);
        Gizmos.DrawWireCube(center, size);
    }
}
