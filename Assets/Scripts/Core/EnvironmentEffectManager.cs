using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 环境效果管理器 - 天气粒子、昼夜光照、环境氛围
/// 每个世界有不同的环境效果预设
/// 与WorldThemeManager配合使用
/// </summary>
public class EnvironmentEffectManager : MonoBehaviour
{
    public static EnvironmentEffectManager Instance { get; private set; }

    [Header("天气系统")]
    [SerializeField] private ParticleSystem rainParticles;
    [SerializeField] private ParticleSystem snowParticles;
    [SerializeField] private ParticleSystem dustParticles;
    [SerializeField] private ParticleSystem firefliesParticles;
    [SerializeField] private ParticleSystem sparkleParticles;
    [SerializeField] private ParticleSystem fogParticles;

    [Header("光照")]
    [SerializeField] private UnityEngine.Rendering.Universal.Light2D globalLight;
    [SerializeField] private float dayIntensity = 1f;
    [SerializeField] private float nightIntensity = 0.3f;
    [SerializeField] private float transitionDuration = 2f;
    [SerializeField] private Gradient dayNightGradient;

    [Header("环境色")]
    [SerializeField] private Color defaultAmbientColor = Color.white;
    [SerializeField] private float ambientTransitionSpeed = 1f;

    [Header("世界预设")]
    [SerializeField] private WorldEnvironmentPreset[] worldPresets;

    // 运行时
    private WeatherType currentWeather = WeatherType.None;
    private float lightTransitionProgress;
    private bool isTransitioningLight;
    private Color targetAmbientColor;
    private Coroutine weatherCoroutine;
    private int currentWorldIndex = -1;

    public enum WeatherType
    {
        None,
        Rain,
        Snow,
        Dust,
        Fireflies,
        Sparkles,
        Fog,
        Storm
    }

    [System.Serializable]
    public class WorldEnvironmentPreset
    {
        public int chapter;
        public string presetName;
        public WeatherType weather = WeatherType.None;
        public Color ambientColor = Color.white;
        public float lightIntensity = 1f;
        public Color lightColor = Color.white;
        public float weatherIntensity = 1f;         // 粒子密度系数
        public bool enableFog;
        public float fogDensity = 0.02f;
        public Color fogColor = new Color(0.5f, 0.5f, 0.6f);
        public float windStrength;
        public float windDirection;                  // 度
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        targetAmbientColor = defaultAmbientColor;
        StopAllWeather();
    }

    void Start()
    {
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Subscribe<BossPhaseChangedEvent>(OnBossPhaseChanged);

        InitializeDefaultPresets();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Unsubscribe<BossPhaseChangedEvent>(OnBossPhaseChanged);
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        UpdateAmbientColor();
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 应用世界环境预设
    /// </summary>
    public void ApplyWorldPreset(int chapter)
    {
        if (currentWorldIndex == chapter) return;
        currentWorldIndex = chapter;

        var preset = GetPreset(chapter);
        if (preset == null) return;

        // 天气
        SetWeather(preset.weather, preset.weatherIntensity);

        // 光照
        SetGlobalLight(preset.lightIntensity, preset.lightColor);

        // 环境色
        SetAmbientColor(preset.ambientColor);

        // 风
        SetWindForAllParticles(preset.windStrength, preset.windDirection);

        // 雾
        if (preset.enableFog && fogParticles != null)
        {
            var emission = fogParticles.emission;
            emission.rateOverTime = preset.fogDensity * 100f;
            fogParticles.Play();

            var main = fogParticles.main;
            main.startColor = preset.fogColor;
        }

        Debug.Log($"[EnvEffect] Applied world preset: {preset.presetName}");
    }

    /// <summary>
    /// 设置天气效果
    /// </summary>
    public void SetWeather(WeatherType weather, float intensity = 1f)
    {
        if (currentWeather == weather) return;

        StopAllWeather();
        currentWeather = weather;

        ParticleSystem targetSystem = GetWeatherParticleSystem(weather);
        if (targetSystem == null) return;

        var emission = targetSystem.emission;
        emission.rateOverTime = emission.rateOverTime.constant * intensity;
        targetSystem.Play();

        // 暴风雨特殊处理
        if (weather == WeatherType.Storm)
        {
            if (weatherCoroutine != null) StopCoroutine(weatherCoroutine);
            weatherCoroutine = StartCoroutine(StormRoutine());
        }
    }

    /// <summary>
    /// 停止所有天气
    /// </summary>
    public void StopAllWeather()
    {
        currentWeather = WeatherType.None;
        SafeStop(rainParticles);
        SafeStop(snowParticles);
        SafeStop(dustParticles);
        SafeStop(firefliesParticles);
        SafeStop(sparkleParticles);
        SafeStop(fogParticles);

        if (weatherCoroutine != null)
        {
            StopCoroutine(weatherCoroutine);
            weatherCoroutine = null;
        }
    }

    /// <summary>
    /// 设置全局光照强度和颜色
    /// </summary>
    public void SetGlobalLight(float intensity, Color color)
    {
        if (globalLight == null) return;
        StartCoroutine(TransitionLight(intensity, color));
    }

    /// <summary>
    /// 设置环境色（渐变过渡）
    /// </summary>
    public void SetAmbientColor(Color color)
    {
        targetAmbientColor = color;
    }

    /// <summary>
    /// 闪电效果
    /// </summary>
    public void TriggerLightning()
    {
        if (globalLight == null) return;
        StartCoroutine(LightningFlash());
    }

    /// <summary>
    /// Boss战氛围切换
    /// </summary>
    public void SetBossAtmosphere(bool active)
    {
        if (active)
        {
            SetAmbientColor(new Color(0.6f, 0.2f, 0.2f));
            if (globalLight != null)
            {
                globalLight.intensity *= 0.6f;
            }
        }
        else
        {
            // 恢复当前世界预设
            if (currentWorldIndex > 0)
                ApplyWorldPreset(currentWorldIndex);
        }
    }

    /// <summary>
    /// 过渡到黑暗（用于深渊章节）
    /// </summary>
    public void TransitionToDarkness(float duration = 2f)
    {
        StartCoroutine(DarknessTransition(duration));
    }

    // ==================== 事件处理 ====================

    private void OnLevelStart(LevelStartEvent e)
    {
        ApplyWorldPreset(e.chapter);
    }

    private void OnBossPhaseChanged(BossPhaseChangedEvent e)
    {
        // Boss换阶段时增强氛围
        if (e.bossHealthPercent < 0.3f)
        {
            SetAmbientColor(new Color(0.8f, 0.15f, 0.1f));
            if (currentWeather == WeatherType.None)
                SetWeather(WeatherType.Dust, 0.5f);
        }
    }

    // ==================== 内部方法 ====================

    private void UpdateAmbientColor()
    {
        if (globalLight == null) return;

        // 平滑过渡到目标颜色（Shader环境色）
        RenderSettings.ambientLight = Color.Lerp(
            RenderSettings.ambientLight,
            targetAmbientColor,
            Time.deltaTime * ambientTransitionSpeed
        );
    }

    private IEnumerator TransitionLight(float targetIntensity, Color targetColor)
    {
        if (globalLight == null) yield break;

        float startIntensity = globalLight.intensity;
        Color startColor = globalLight.color;
        float elapsed = 0;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;
            t = t * t * (3f - 2f * t); // smoothstep

            globalLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            globalLight.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        globalLight.intensity = targetIntensity;
        globalLight.color = targetColor;
    }

    private IEnumerator LightningFlash()
    {
        if (globalLight == null) yield break;

        float originalIntensity = globalLight.intensity;
        Color originalColor = globalLight.color;

        // 闪光
        globalLight.intensity = dayIntensity * 3f;
        globalLight.color = Color.white;
        yield return new WaitForSeconds(0.05f);

        globalLight.intensity = originalIntensity;
        globalLight.color = originalColor;
        yield return new WaitForSeconds(0.1f);

        // 第二次闪光（更弱）
        globalLight.intensity = dayIntensity * 1.5f;
        globalLight.color = Color.white;
        yield return new WaitForSeconds(0.03f);

        globalLight.intensity = originalIntensity;
        globalLight.color = originalColor;

        // 相机震动
        if (CameraEffects.Instance != null)
            CameraEffects.Instance.Shake(0.3f, 0.15f);

        // 雷声
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("thunder");
    }

    private IEnumerator StormRoutine()
    {
        // 启动雨
        if (rainParticles != null)
        {
            var emission = rainParticles.emission;
            emission.rateOverTime = 200f;
            rainParticles.Play();
        }

        // 周期性闪电
        while (currentWeather == WeatherType.Storm)
        {
            float delay = Random.Range(3f, 8f);
            yield return new WaitForSeconds(delay);

            if (currentWeather == WeatherType.Storm)
                TriggerLightning();
        }
    }

    private IEnumerator DarknessTransition(float duration)
    {
        if (globalLight == null) yield break;

        float startIntensity = globalLight.intensity;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            globalLight.intensity = Mathf.Lerp(startIntensity, nightIntensity * 0.5f, elapsed / duration);
            yield return null;
        }
    }

    private void SetWindForAllParticles(float strength, float directionDegrees)
    {
        if (strength <= 0) return;

        Vector3 windDir = Quaternion.Euler(0, 0, directionDegrees) * Vector3.right * strength;

        ApplyWindToSystem(rainParticles, windDir);
        ApplyWindToSystem(snowParticles, windDir);
        ApplyWindToSystem(dustParticles, windDir);
    }

    private void ApplyWindToSystem(ParticleSystem ps, Vector3 wind)
    {
        if (ps == null) return;
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(wind.x);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(wind.y);
    }

    private ParticleSystem GetWeatherParticleSystem(WeatherType weather)
    {
        return weather switch
        {
            WeatherType.Rain => rainParticles,
            WeatherType.Snow => snowParticles,
            WeatherType.Dust => dustParticles,
            WeatherType.Fireflies => firefliesParticles,
            WeatherType.Sparkles => sparkleParticles,
            WeatherType.Fog => fogParticles,
            WeatherType.Storm => rainParticles,
            _ => null
        };
    }

    private void SafeStop(ParticleSystem ps)
    {
        if (ps != null && ps.isPlaying)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void InitializeDefaultPresets()
    {
        if (worldPresets != null && worldPresets.Length > 0) return;

        worldPresets = new WorldEnvironmentPreset[]
        {
            new WorldEnvironmentPreset
            {
                chapter = 1, presetName = "光明森林",
                weather = WeatherType.Fireflies,
                ambientColor = new Color(0.9f, 1f, 0.85f),
                lightIntensity = 1f,
                lightColor = new Color(1f, 0.98f, 0.9f),
                weatherIntensity = 0.8f
            },
            new WorldEnvironmentPreset
            {
                chapter = 2, presetName = "水晶洞窟",
                weather = WeatherType.Sparkles,
                ambientColor = new Color(0.6f, 0.7f, 0.95f),
                lightIntensity = 0.6f,
                lightColor = new Color(0.7f, 0.8f, 1f),
                weatherIntensity = 0.5f,
                enableFog = true,
                fogDensity = 0.015f,
                fogColor = new Color(0.3f, 0.4f, 0.6f)
            },
            new WorldEnvironmentPreset
            {
                chapter = 3, presetName = "深渊",
                weather = WeatherType.Fog,
                ambientColor = new Color(0.2f, 0.25f, 0.4f),
                lightIntensity = 0.35f,
                lightColor = new Color(0.4f, 0.45f, 0.6f),
                weatherIntensity = 1.2f,
                enableFog = true,
                fogDensity = 0.04f,
                fogColor = new Color(0.15f, 0.15f, 0.25f)
            },
            new WorldEnvironmentPreset
            {
                chapter = 4, presetName = "天空城",
                weather = WeatherType.Dust,
                ambientColor = new Color(0.95f, 0.9f, 0.8f),
                lightIntensity = 1.1f,
                lightColor = new Color(1f, 0.95f, 0.85f),
                weatherIntensity = 0.3f,
                windStrength = 2f,
                windDirection = 180f
            },
            new WorldEnvironmentPreset
            {
                chapter = 5, presetName = "黄昏境界",
                weather = WeatherType.Sparkles,
                ambientColor = new Color(0.7f, 0.5f, 0.6f),
                lightIntensity = 0.7f,
                lightColor = new Color(0.9f, 0.6f, 0.5f),
                weatherIntensity = 0.7f,
                enableFog = true,
                fogDensity = 0.02f,
                fogColor = new Color(0.5f, 0.3f, 0.4f)
            }
        };
    }

    private WorldEnvironmentPreset GetPreset(int chapter)
    {
        if (worldPresets == null) return null;
        foreach (var preset in worldPresets)
        {
            if (preset.chapter == chapter) return preset;
        }
        return null;
    }
}
