using UnityEngine;

/// <summary>
/// 环境音区域 - 玩家进入不同区域时切换环境音
/// 支持渐入渐出、多音源混合
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AmbientSoundZone : MonoBehaviour
{
    [Header("环境音")]
    [SerializeField] private AudioClip ambientClip;
    [SerializeField] [Range(0f, 1f)] private float volume = 0.5f;
    [SerializeField] private bool loop = true;

    [Header("渐变")]
    [SerializeField] private float fadeInDuration = 1f;
    [SerializeField] private float fadeOutDuration = 1.5f;

    [Header("可选BGM切换")]
    [SerializeField] private AudioClip zoneBGM;
    [SerializeField] private bool changeBGMOnEnter;

    private AudioSource audioSource;
    private float targetVolume;
    private float currentFadeSpeed;
    private bool isPlayerInside;
    private AudioClip previousBGM;

    void Awake()
    {
        // 确保Collider是Trigger
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        // 创建音源
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = ambientClip;
        audioSource.loop = loop;
        audioSource.volume = 0;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0; // 2D音效
    }

    void Update()
    {
        if (audioSource == null) return;

        // 平滑音量过渡
        if (Mathf.Abs(audioSource.volume - targetVolume) > 0.01f)
        {
            audioSource.volume = Mathf.MoveTowards(audioSource.volume, targetVolume,
                currentFadeSpeed * Time.deltaTime);

            // 音量降到0时停止播放
            if (targetVolume <= 0 && audioSource.volume <= 0.01f)
            {
                audioSource.Stop();
                audioSource.volume = 0;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (isPlayerInside) return;

        isPlayerInside = true;

        // 淡入环境音
        if (ambientClip != null && !audioSource.isPlaying)
        {
            audioSource.clip = ambientClip;
            audioSource.Play();
        }

        targetVolume = volume;
        currentFadeSpeed = volume / fadeInDuration;

        // 切换BGM
        if (changeBGMOnEnter && zoneBGM != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(zoneBGM);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInside = false;

        // 淡出环境音
        targetVolume = 0;
        currentFadeSpeed = volume / fadeOutDuration;
    }

    void OnDisable()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.volume = 0;
        }
    }

    /// <summary>
    /// 手动设置音量
    /// </summary>
    public void SetVolume(float vol)
    {
        volume = Mathf.Clamp01(vol);
        if (isPlayerInside) targetVolume = volume;
    }
}
