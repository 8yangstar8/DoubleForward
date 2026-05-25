using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

/// <summary>
/// 相机后处理特效管理器 - 统一管理URP后处理效果
/// 提供便捷接口：受伤红屏、Boss战氛围、能力激活特效等
/// </summary>
public class CameraEffects : MonoBehaviour
{
    public static CameraEffects Instance { get; private set; }

    [Header("后处理Volume")]
    [SerializeField] private Volume globalVolume;

    [Header("默认值")]
    [SerializeField] private float defaultVignette = 0.2f;
    [SerializeField] private float defaultSaturation = 0f;
    [SerializeField] private float defaultContrast = 0f;

    // 后处理组件引用
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private ChromaticAberration chromaticAberration;
    private Bloom bloom;
    private LensDistortion lensDistortion;

    private Coroutine vignetteCoroutine;
    private Coroutine saturationCoroutine;
    private Coroutine aberrationCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        CachePostProcessComponents();
    }

    private void CachePostProcessComponents()
    {
        if (globalVolume == null) return;

        var profile = globalVolume.profile;
        if (profile == null) return;

        profile.TryGet(out vignette);
        profile.TryGet(out colorAdjustments);
        profile.TryGet(out chromaticAberration);
        profile.TryGet(out bloom);
        profile.TryGet(out lensDistortion);
    }

    // ============ 暗角效果 ============

    /// <summary>
    /// 受伤红屏闪烁
    /// </summary>
    public void DamageFlash(float intensity = 0.5f, float duration = 0.3f)
    {
        if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
        vignetteCoroutine = StartCoroutine(VignetteFlash(Color.red, intensity, duration));
    }

    /// <summary>
    /// 治疗绿屏
    /// </summary>
    public void HealFlash(float duration = 0.5f)
    {
        if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
        vignetteCoroutine = StartCoroutine(VignetteFlash(new Color(0.2f, 1f, 0.3f), 0.3f, duration));
    }

    /// <summary>
    /// 低血量持续暗角
    /// </summary>
    public void SetLowHealthVignette(bool active)
    {
        if (vignette == null) return;

        if (active)
        {
            vignette.intensity.value = 0.45f;
            vignette.color.value = new Color(0.5f, 0f, 0f);
        }
        else
        {
            vignette.intensity.value = defaultVignette;
            vignette.color.value = Color.black;
        }
    }

    private IEnumerator VignetteFlash(Color color, float intensity, float duration)
    {
        if (vignette == null) yield break;

        vignette.color.value = color;
        vignette.intensity.value = intensity;

        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            vignette.intensity.value = Mathf.Lerp(intensity, defaultVignette, t / duration);
            yield return null;
        }

        vignette.intensity.value = defaultVignette;
        vignette.color.value = Color.black;
    }

    // ============ 色彩调整 ============

    /// <summary>
    /// Boss战氛围 - 降低饱和度增加对比度
    /// </summary>
    public void SetBossAtmosphere(bool active)
    {
        if (saturationCoroutine != null) StopCoroutine(saturationCoroutine);
        saturationCoroutine = StartCoroutine(TransitionColorAdjustments(
            active ? -30f : defaultSaturation,
            active ? 15f : defaultContrast,
            0.8f
        ));
    }

    /// <summary>
    /// 暗影能力氛围 - 冷色调
    /// </summary>
    public void SetShadowAbilityEffect(bool active)
    {
        if (colorAdjustments == null) return;

        if (active)
        {
            colorAdjustments.colorFilter.value = new Color(0.7f, 0.7f, 1f); // 偏蓝
            colorAdjustments.saturation.value = -15f;
        }
        else
        {
            colorAdjustments.colorFilter.value = Color.white;
            colorAdjustments.saturation.value = defaultSaturation;
        }
    }

    /// <summary>
    /// 光明能力氛围 - 暖色调
    /// </summary>
    public void SetLightAbilityEffect(bool active)
    {
        if (colorAdjustments == null) return;

        if (active)
        {
            colorAdjustments.colorFilter.value = new Color(1f, 0.95f, 0.8f); // 偏暖
            if (bloom != null) bloom.intensity.value = 1.5f;
        }
        else
        {
            colorAdjustments.colorFilter.value = Color.white;
            if (bloom != null) bloom.intensity.value = 0.5f;
        }
    }

    private IEnumerator TransitionColorAdjustments(float targetSat, float targetContrast, float duration)
    {
        if (colorAdjustments == null) yield break;

        float startSat = colorAdjustments.saturation.value;
        float startContrast = colorAdjustments.contrast.value;

        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / duration;
            colorAdjustments.saturation.value = Mathf.Lerp(startSat, targetSat, p);
            colorAdjustments.contrast.value = Mathf.Lerp(startContrast, targetContrast, p);
            yield return null;
        }
    }

    // ============ 屏幕震动 ============

    /// <summary>
    /// 屏幕震动 - 委托给CameraShake，没有则自行实现简易版
    /// </summary>
    public void Shake(float intensity, float duration)
    {
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(intensity, duration);
        }
        else
        {
            StartCoroutine(SimpleShake(intensity, duration));
        }
    }

    private IEnumerator SimpleShake(float intensity, float duration)
    {
        Vector3 originalPos = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float decay = 1f - (elapsed / duration);
            float x = Random.Range(-1f, 1f) * intensity * decay;
            float y = Random.Range(-1f, 1f) * intensity * decay;
            transform.localPosition = originalPos + new Vector3(x, y, 0);
            yield return null;
        }

        transform.localPosition = originalPos;
    }

    // ============ 屏幕闪烁 ============

    /// <summary>
    /// 全屏闪光效果（死亡/爆炸/技能释放）
    /// </summary>
    public void Flash(Color color, float duration)
    {
        if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
        vignetteCoroutine = StartCoroutine(FullScreenFlash(color, duration));
    }

    private IEnumerator FullScreenFlash(Color color, float duration)
    {
        if (vignette == null) yield break;

        // 暂时用暗角+bloom模拟全屏闪光
        vignette.color.value = color;
        vignette.intensity.value = 0.6f;

        if (bloom != null)
            bloom.intensity.value = 3f;

        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / duration;
            vignette.intensity.value = Mathf.Lerp(0.6f, defaultVignette, p);
            if (bloom != null)
                bloom.intensity.value = Mathf.Lerp(3f, 0.5f, p);
            yield return null;
        }

        vignette.intensity.value = defaultVignette;
        vignette.color.value = Color.black;
        if (bloom != null)
            bloom.intensity.value = 0.5f;
    }

    // ============ 色差效果 ============

    /// <summary>
    /// 冲刺/传送色差
    /// </summary>
    public void ChromaticPulse(float intensity = 0.5f, float duration = 0.2f)
    {
        if (aberrationCoroutine != null) StopCoroutine(aberrationCoroutine);
        aberrationCoroutine = StartCoroutine(ChromaticAberrationPulse(intensity, duration));
    }

    /// <summary>
    /// 死亡/GameOver强色差
    /// </summary>
    public void DeathEffect()
    {
        ChromaticPulse(1f, 0.8f);

        if (colorAdjustments != null)
        {
            StartCoroutine(TransitionColorAdjustments(-80f, -20f, 0.5f));
        }
    }

    /// <summary>
    /// 重置所有特效到默认
    /// </summary>
    public void ResetAllEffects()
    {
        if (vignette != null)
        {
            vignette.intensity.value = defaultVignette;
            vignette.color.value = Color.black;
        }
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value = defaultSaturation;
            colorAdjustments.contrast.value = defaultContrast;
            colorAdjustments.colorFilter.value = Color.white;
        }
        if (chromaticAberration != null)
            chromaticAberration.intensity.value = 0;
        if (bloom != null)
            bloom.intensity.value = 0.5f;
        if (lensDistortion != null)
            lensDistortion.intensity.value = 0;
    }

    private IEnumerator ChromaticAberrationPulse(float intensity, float duration)
    {
        if (chromaticAberration == null) yield break;

        chromaticAberration.intensity.value = intensity;

        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            chromaticAberration.intensity.value = Mathf.Lerp(intensity, 0, t / duration);
            yield return null;
        }
        chromaticAberration.intensity.value = 0;
    }

    // ============ 时间效果 ============

    /// <summary>
    /// 子弹时间/慢动作
    /// </summary>
    public void SlowMotion(float timeScale = 0.3f, float duration = 0.5f)
    {
        StartCoroutine(SlowMotionCoroutine(timeScale, duration));
    }

    private IEnumerator SlowMotionCoroutine(float targetScale, float duration)
    {
        Time.timeScale = targetScale;
        Time.fixedDeltaTime = 0.02f * targetScale;

        // 用unscaled time等待
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    /// <summary>
    /// 击杀帧冻结
    /// </summary>
    public void HitFreeze(float duration = 0.05f)
    {
        StartCoroutine(HitFreezeCoroutine(duration));
    }

    private IEnumerator HitFreezeCoroutine(float duration)
    {
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1;
    }
}
