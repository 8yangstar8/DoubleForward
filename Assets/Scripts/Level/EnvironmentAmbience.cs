using UnityEngine;
using System.Collections;

/// <summary>
/// 环境氛围管理器 - 控制每个关卡/世界的视觉氛围
/// 管理天气粒子、环境光变化、背景滚动、雾效等
/// 每个关卡场景放置一个，由LevelBootstrap初始化
/// </summary>
public class EnvironmentAmbience : MonoBehaviour
{
    public static EnvironmentAmbience Instance { get; private set; }

    [Header("世界主题")]
    [SerializeField] private WorldTheme theme = WorldTheme.LightRuins;

    [Header("环境光")]
    [SerializeField] private Color ambientColor = new Color(0.6f, 0.6f, 0.7f);
    [SerializeField] private float ambientIntensity = 1f;
    [SerializeField] private bool dynamicLighting = false;
    [SerializeField] private Gradient dayNightCycle;
    [SerializeField] private float dayNightSpeed = 0.01f;

    [Header("粒子系统")]
    [SerializeField] private ParticleSystem ambientParticles;   // 飘浮粒子
    [SerializeField] private ParticleSystem weatherParticles;   // 天气效果
    [SerializeField] private int baseParticleCount = 20;

    [Header("背景")]
    [SerializeField] private SpriteRenderer[] parallaxLayers;
    [SerializeField] private float[] parallaxSpeeds;
    [SerializeField] private Transform backgroundFollowTarget; // 跟随摄像机

    [Header("雾效")]
    [SerializeField] private bool enableFog = false;
    [SerializeField] private Color fogColor = new Color(0.5f, 0.5f, 0.6f, 0.3f);
    [SerializeField] private float fogDensity = 0.02f;
    [SerializeField] private SpriteRenderer fogLayer;

    [Header("环境音")]
    [SerializeField] private AudioClip ambientLoop;
    [SerializeField] private AudioClip[] randomSounds;        // 随机环境音
    [SerializeField] private float randomSoundInterval = 15f;
    [SerializeField] private float randomSoundVariance = 10f;

    public enum WorldTheme
    {
        LightRuins,     // Ch.1 光影遗迹
        IceFire,        // Ch.2 冰火熔炉
        DesertStorm,    // Ch.3 沙漠风暴
        DeepAbyss,      // Ch.4 深渊暗流
        SkyPeak         // Ch.5 天空之巅
    }

    private float dayNightTimer;
    private Camera mainCamera;
    private Coroutine randomSoundCoroutine;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        mainCamera = Camera.main;
        ApplyTheme();

        if (ambientLoop != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayAmbient(ambientLoop);

        if (randomSounds != null && randomSounds.Length > 0)
            randomSoundCoroutine = StartCoroutine(PlayRandomSounds());
    }

    void Update()
    {
        UpdateParallax();

        if (dynamicLighting)
            UpdateDynamicLighting();
    }

    // ==================== 主题初始化 ====================

    private void ApplyTheme()
    {
        switch (theme)
        {
            case WorldTheme.LightRuins:
                ApplyLightRuinsTheme();
                break;
            case WorldTheme.IceFire:
                ApplyIceFireTheme();
                break;
            case WorldTheme.DesertStorm:
                ApplyDesertStormTheme();
                break;
            case WorldTheme.DeepAbyss:
                ApplyDeepAbyssTheme();
                break;
            case WorldTheme.SkyPeak:
                ApplySkyPeakTheme();
                break;
        }

        // 应用环境光
        RenderSettings.ambientLight = ambientColor * ambientIntensity;

        // 雾效
        if (enableFog && fogLayer != null)
        {
            fogLayer.color = fogColor;
            fogLayer.gameObject.SetActive(true);
        }
    }

    private void ApplyLightRuinsTheme()
    {
        // 柔和的金色光芒，飘浮的光尘
        ambientColor = new Color(0.85f, 0.8f, 0.6f);
        SetupParticles(new Color(1f, 0.95f, 0.7f, 0.5f), 25, 0.5f, 1.5f);
    }

    private void ApplyIceFireTheme()
    {
        // 冰蓝与火红交替，雪花/火星
        ambientColor = new Color(0.6f, 0.7f, 0.9f);
        SetupParticles(new Color(0.8f, 0.9f, 1f, 0.6f), 30, 0.8f, 2.0f);
    }

    private void ApplyDesertStormTheme()
    {
        // 沙尘暴，黄色调
        ambientColor = new Color(0.9f, 0.8f, 0.5f);
        SetupParticles(new Color(0.9f, 0.8f, 0.5f, 0.4f), 40, 1.5f, 3.0f);
        enableFog = true;
        fogColor = new Color(0.9f, 0.8f, 0.5f, 0.15f);
    }

    private void ApplyDeepAbyssTheme()
    {
        // 深海蓝紫，气泡粒子
        ambientColor = new Color(0.2f, 0.2f, 0.4f);
        ambientIntensity = 0.6f;
        SetupParticles(new Color(0.3f, 0.5f, 0.8f, 0.4f), 20, 0.3f, 0.8f);
        enableFog = true;
        fogColor = new Color(0.1f, 0.1f, 0.3f, 0.25f);
    }

    private void ApplySkyPeakTheme()
    {
        // 明亮白金色，云朵飘浮
        ambientColor = new Color(0.95f, 0.95f, 1f);
        ambientIntensity = 1.2f;
        SetupParticles(new Color(1f, 1f, 1f, 0.3f), 15, 0.5f, 1.0f);
    }

    private void SetupParticles(Color color, int count, float minSpeed, float maxSpeed)
    {
        if (ambientParticles == null) return;

        var main = ambientParticles.main;
        main.startColor = color;
        main.maxParticles = count;

        var velocity = ambientParticles.velocityOverLifetime;
        velocity.enabled = true;

        var emission = ambientParticles.emission;
        emission.rateOverTime = count * 0.5f;

        ambientParticles.Play();
    }

    // ==================== 视差滚动 ====================

    private void UpdateParallax()
    {
        if (parallaxLayers == null || parallaxSpeeds == null) return;
        if (mainCamera == null) return;

        Vector3 camPos = mainCamera.transform.position;

        for (int i = 0; i < parallaxLayers.Length && i < parallaxSpeeds.Length; i++)
        {
            if (parallaxLayers[i] == null) continue;

            float speed = parallaxSpeeds[i];
            Vector3 bgPos = parallaxLayers[i].transform.position;
            bgPos.x = camPos.x * speed;
            bgPos.y = camPos.y * speed * 0.3f; // Y轴视差较弱
            parallaxLayers[i].transform.position = bgPos;
        }
    }

    // ==================== 动态光照 ====================

    private void UpdateDynamicLighting()
    {
        if (dayNightCycle == null) return;

        dayNightTimer += Time.deltaTime * dayNightSpeed;
        if (dayNightTimer > 1f) dayNightTimer -= 1f;

        Color cycleColor = dayNightCycle.Evaluate(dayNightTimer);
        RenderSettings.ambientLight = cycleColor * ambientIntensity;
    }

    // ==================== 随机环境音 ====================

    private IEnumerator PlayRandomSounds()
    {
        while (true)
        {
            float wait = randomSoundInterval + Random.Range(-randomSoundVariance, randomSoundVariance);
            yield return new WaitForSeconds(Mathf.Max(3f, wait));

            if (randomSounds.Length == 0 || AudioManager.Instance == null) continue;

            var clip = randomSounds[Random.Range(0, randomSounds.Length)];
            if (clip != null)
                AudioManager.Instance.PlaySFX(clip, Random.Range(0.2f, 0.5f));
        }
    }

    // ==================== 天气控制 ====================

    /// <summary>
    /// 开始天气效果（如Boss战时暴风雨加剧）
    /// </summary>
    public void SetWeatherIntensity(float intensity)
    {
        if (weatherParticles == null) return;

        var emission = weatherParticles.emission;
        emission.rateOverTime = baseParticleCount * intensity;

        if (intensity > 0 && !weatherParticles.isPlaying)
            weatherParticles.Play();
        else if (intensity <= 0 && weatherParticles.isPlaying)
            weatherParticles.Stop();
    }

    /// <summary>
    /// 过渡到新的环境光颜色（Boss战氛围切换等）
    /// </summary>
    public void TransitionAmbientColor(Color targetColor, float duration)
    {
        StartCoroutine(TransitionAmbient(targetColor, duration));
    }

    private IEnumerator TransitionAmbient(Color target, float duration)
    {
        Color startColor = RenderSettings.ambientLight;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            RenderSettings.ambientLight = Color.Lerp(startColor, target, elapsed / duration);
            yield return null;
        }

        RenderSettings.ambientLight = target;
    }

    /// <summary>
    /// 闪电效果（瞬间亮白 + 远雷声）
    /// </summary>
    public void TriggerLightning()
    {
        StartCoroutine(LightningFlash());
    }

    private IEnumerator LightningFlash()
    {
        Color originalAmbient = RenderSettings.ambientLight;

        RenderSettings.ambientLight = Color.white;
        yield return new WaitForSeconds(0.05f);
        RenderSettings.ambientLight = originalAmbient;
        yield return new WaitForSeconds(0.08f);
        RenderSettings.ambientLight = Color.white * 0.8f;
        yield return new WaitForSeconds(0.03f);
        RenderSettings.ambientLight = originalAmbient;

        // 延迟雷声
        yield return new WaitForSeconds(Random.Range(0.3f, 1.5f));
        if (AudioManager.Instance != null && randomSounds != null && randomSounds.Length > 0)
            AudioManager.Instance.PlaySFX(randomSounds[0], Random.Range(0.3f, 0.7f));
    }

    void OnDestroy()
    {
        if (randomSoundCoroutine != null)
            StopCoroutine(randomSoundCoroutine);

        // 还原环境光
        RenderSettings.ambientLight = Color.white;
    }
}
