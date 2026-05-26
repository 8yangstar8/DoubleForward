using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

/// <summary>
/// 屏幕效果控制器 - 集中管理后处理效果开关
/// 根据PerformanceManager等级自动调整后处理强度
/// 低端设备禁用花哨效果，高端设备全开
/// </summary>
public class ScreenEffectsController : MonoBehaviour
{
    public static ScreenEffectsController Instance { get; private set; }

    [Header("Volume引用")]
    [SerializeField] private Volume globalVolume;

    [Header("效果层级")]
    [SerializeField] private EffectPreset lowPreset;
    [SerializeField] private EffectPreset mediumPreset;
    [SerializeField] private EffectPreset highPreset;

    [Header("过渡")]
    [SerializeField] private float transitionDuration = 0.5f;

    // 后处理组件引用（运行时缓存）
    private Bloom bloom;
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private ChromaticAberration chromaticAberration;
    private MotionBlur motionBlur;
    private FilmGrain filmGrain;

    // 状态
    private EffectPreset currentPreset;
    private bool effectsEnabled = true;

    [System.Serializable]
    public class EffectPreset
    {
        public string presetName;
        public bool enableBloom = true;
        public float bloomIntensity = 1f;
        public float bloomThreshold = 0.9f;
        public bool enableVignette = true;
        public float vignetteIntensity = 0.25f;
        public bool enableChromaticAberration;
        public bool enableMotionBlur;
        public float motionBlurIntensity = 0.1f;
        public bool enableFilmGrain;
        public float filmGrainIntensity = 0.1f;
        public bool enableColorAdjustments = true;
        public float saturation = 10f;
        public float contrast = 10f;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        CacheVolumeComponents();
        InitializeDefaultPresets();
    }

    void Start()
    {
        // 根据性能等级自动选择
        ApplyPerformancePreset();

        // 监听性能等级变化
        if (PerformanceManager.Instance != null)
            PerformanceManager.Instance.OnPerformanceLevelChanged += OnPerformanceLevelChanged;
    }

    void OnDestroy()
    {
        if (PerformanceManager.Instance != null)
            PerformanceManager.Instance.OnPerformanceLevelChanged -= OnPerformanceLevelChanged;
        if (Instance == this) Instance = null;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 启用/禁用所有后处理效果
    /// </summary>
    public void SetEffectsEnabled(bool enabled)
    {
        effectsEnabled = enabled;
        if (globalVolume != null)
            globalVolume.enabled = enabled;
    }

    /// <summary>
    /// 应用预设等级
    /// </summary>
    public void ApplyPreset(EffectPreset preset)
    {
        if (preset == null) return;
        currentPreset = preset;
        StartCoroutine(TransitionToPreset(preset));
    }

    /// <summary>
    /// 设置Bloom强度（战斗/Boss增强）
    /// </summary>
    public void SetBloomIntensity(float intensity)
    {
        if (bloom != null)
            bloom.intensity.value = intensity;
    }

    /// <summary>
    /// 设置饱和度（死亡灰度、胜利鲜艳）
    /// </summary>
    public void SetSaturation(float value)
    {
        if (colorAdjustments != null)
            colorAdjustments.saturation.value = value;
    }

    /// <summary>
    /// 设置对比度
    /// </summary>
    public void SetContrast(float value)
    {
        if (colorAdjustments != null)
            colorAdjustments.contrast.value = value;
    }

    /// <summary>
    /// 死亡灰度效果
    /// </summary>
    public void ApplyDeathEffect()
    {
        StartCoroutine(DeathEffectRoutine());
    }

    /// <summary>
    /// 恢复正常色彩
    /// </summary>
    public void RestoreNormalColors()
    {
        if (currentPreset == null) return;
        StartCoroutine(RestoreColorsRoutine());
    }

    /// <summary>
    /// 低血量警示效果（暗角加重+微红）
    /// </summary>
    public void SetLowHealthVisual(bool active)
    {
        if (vignette == null) return;

        if (active)
        {
            vignette.intensity.value = 0.5f;
            vignette.color.value = new Color(0.6f, 0f, 0f);
        }
        else
        {
            vignette.intensity.value = currentPreset?.vignetteIntensity ?? 0.25f;
            vignette.color.value = Color.black;
        }
    }

    /// <summary>
    /// 水下视觉效果
    /// </summary>
    public void SetUnderwaterVisual(bool active)
    {
        if (active)
        {
            if (colorAdjustments != null)
            {
                colorAdjustments.colorFilter.value = new Color(0.6f, 0.8f, 1f);
                colorAdjustments.saturation.value = -20f;
            }
            if (vignette != null)
            {
                vignette.intensity.value = 0.4f;
                vignette.color.value = new Color(0f, 0.1f, 0.3f);
            }
        }
        else
        {
            RestoreNormalColors();
        }
    }

    // ==================== 内部方法 ====================

    private void CacheVolumeComponents()
    {
        if (globalVolume == null) return;
        var profile = globalVolume.profile;
        if (profile == null) return;

        profile.TryGet(out bloom);
        profile.TryGet(out vignette);
        profile.TryGet(out colorAdjustments);
        profile.TryGet(out chromaticAberration);
        profile.TryGet(out motionBlur);
        profile.TryGet(out filmGrain);
    }

    private void ApplyPerformancePreset()
    {
        if (PerformanceManager.Instance == null)
        {
            ApplyPreset(mediumPreset);
            return;
        }

        var level = PerformanceManager.Instance.CurrentLevel;
        switch (level)
        {
            case PerformanceManager.PerformanceLevel.Low:
                ApplyPreset(lowPreset);
                break;
            case PerformanceManager.PerformanceLevel.Medium:
                ApplyPreset(mediumPreset);
                break;
            case PerformanceManager.PerformanceLevel.High:
                ApplyPreset(highPreset);
                break;
            default:
                ApplyPreset(mediumPreset);
                break;
        }
    }

    private void OnPerformanceLevelChanged(PerformanceManager.PerformanceLevel level)
    {
        ApplyPerformancePreset();
    }

    private IEnumerator TransitionToPreset(EffectPreset preset)
    {
        float elapsed = 0;

        // 缓存起始值
        float startBloom = bloom?.intensity.value ?? 0;
        float startVignette = vignette?.intensity.value ?? 0;
        float startSaturation = colorAdjustments?.saturation.value ?? 0;
        float startContrast = colorAdjustments?.contrast.value ?? 0;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / transitionDuration;
            t = t * t * (3f - 2f * t); // smoothstep

            if (bloom != null)
            {
                bloom.active = preset.enableBloom;
                bloom.intensity.value = Mathf.Lerp(startBloom, preset.bloomIntensity, t);
                bloom.threshold.value = preset.bloomThreshold;
            }

            if (vignette != null)
            {
                vignette.active = preset.enableVignette;
                vignette.intensity.value = Mathf.Lerp(startVignette, preset.vignetteIntensity, t);
            }

            if (colorAdjustments != null)
            {
                colorAdjustments.active = preset.enableColorAdjustments;
                colorAdjustments.saturation.value = Mathf.Lerp(startSaturation, preset.saturation, t);
                colorAdjustments.contrast.value = Mathf.Lerp(startContrast, preset.contrast, t);
            }

            yield return null;
        }

        // 最终值
        if (chromaticAberration != null)
            chromaticAberration.active = preset.enableChromaticAberration;

        if (motionBlur != null)
        {
            motionBlur.active = preset.enableMotionBlur;
            motionBlur.intensity.value = preset.motionBlurIntensity;
        }

        if (filmGrain != null)
        {
            filmGrain.active = preset.enableFilmGrain;
            filmGrain.intensity.value = preset.filmGrainIntensity;
        }
    }

    private IEnumerator DeathEffectRoutine()
    {
        float elapsed = 0;
        float duration = 0.8f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            if (colorAdjustments != null)
            {
                colorAdjustments.saturation.value = Mathf.Lerp(
                    currentPreset?.saturation ?? 0, -100f, t);
            }

            if (vignette != null)
            {
                vignette.intensity.value = Mathf.Lerp(
                    currentPreset?.vignetteIntensity ?? 0.25f, 0.6f, t);
                vignette.color.value = Color.Lerp(Color.black, new Color(0.3f, 0, 0), t);
            }

            yield return null;
        }
    }

    private IEnumerator RestoreColorsRoutine()
    {
        float elapsed = 0;
        float duration = 0.5f;

        float startSat = colorAdjustments?.saturation.value ?? 0;
        float startVig = vignette?.intensity.value ?? 0;
        Color startVigColor = vignette?.color.value ?? Color.black;

        float targetSat = currentPreset?.saturation ?? 0;
        float targetVig = currentPreset?.vignetteIntensity ?? 0.25f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            if (colorAdjustments != null)
                colorAdjustments.saturation.value = Mathf.Lerp(startSat, targetSat, t);

            if (vignette != null)
            {
                vignette.intensity.value = Mathf.Lerp(startVig, targetVig, t);
                vignette.color.value = Color.Lerp(startVigColor, Color.black, t);
            }

            yield return null;
        }
    }

    private void InitializeDefaultPresets()
    {
        if (lowPreset == null)
        {
            lowPreset = new EffectPreset
            {
                presetName = "Low",
                enableBloom = false,
                enableVignette = false,
                enableChromaticAberration = false,
                enableMotionBlur = false,
                enableFilmGrain = false,
                enableColorAdjustments = false
            };
        }

        if (mediumPreset == null)
        {
            mediumPreset = new EffectPreset
            {
                presetName = "Medium",
                enableBloom = true,
                bloomIntensity = 0.5f,
                bloomThreshold = 1f,
                enableVignette = true,
                vignetteIntensity = 0.2f,
                enableChromaticAberration = false,
                enableMotionBlur = false,
                enableFilmGrain = false,
                enableColorAdjustments = true,
                saturation = 5f,
                contrast = 5f
            };
        }

        if (highPreset == null)
        {
            highPreset = new EffectPreset
            {
                presetName = "High",
                enableBloom = true,
                bloomIntensity = 1f,
                bloomThreshold = 0.9f,
                enableVignette = true,
                vignetteIntensity = 0.25f,
                enableChromaticAberration = false,
                enableMotionBlur = true,
                motionBlurIntensity = 0.08f,
                enableFilmGrain = true,
                filmGrainIntensity = 0.05f,
                enableColorAdjustments = true,
                saturation = 10f,
                contrast = 10f
            };
        }
    }
}
