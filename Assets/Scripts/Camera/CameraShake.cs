using UnityEngine;
using System.Collections;

/// <summary>
/// 相机震动系统 - 可叠加、可衰减的屏幕震动
/// 支持多来源震动请求（Boss攻击、受伤、着地等）
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("全局设置")]
    [SerializeField] private float maxShakeIntensity = 1f;
    [SerializeField] private bool enableShake = true;

    private float currentIntensity;
    private float currentDuration;
    private float shakeTimer;
    private Vector3 originalLocalPosition;
    private bool isShaking;

    // 持续性震动（如站在不稳定平台上）
    private float continuousIntensity;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        originalLocalPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        if (!enableShake) return;

        float totalIntensity = 0f;

        // 衰减式震动
        if (isShaking)
        {
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0)
            {
                isShaking = false;
            }
            else
            {
                float decay = shakeTimer / currentDuration;
                totalIntensity += currentIntensity * decay;
            }
        }

        // 持续性震动
        totalIntensity += continuousIntensity;

        // 钳制
        totalIntensity = Mathf.Min(totalIntensity, maxShakeIntensity);

        if (totalIntensity > 0.001f)
        {
            float x = Random.Range(-totalIntensity, totalIntensity);
            float y = Random.Range(-totalIntensity, totalIntensity);
            transform.localPosition = originalLocalPosition + new Vector3(x, y, 0);
        }
        else
        {
            transform.localPosition = originalLocalPosition;
        }
    }

    // ============ 公共API ============

    /// <summary>
    /// 触发一次性震动（自动衰减）
    /// </summary>
    public void Shake(float intensity = 0.3f, float duration = 0.3f)
    {
        if (!enableShake) return;

        // 如果新震动更强，覆盖；否则只延长时间
        if (intensity > currentIntensity || !isShaking)
        {
            currentIntensity = intensity;
            currentDuration = duration;
            shakeTimer = duration;
            isShaking = true;
        }
        else
        {
            shakeTimer = Mathf.Max(shakeTimer, duration * 0.5f);
        }
    }

    /// <summary>
    /// 轻微震动（收集品、跳跃着地）
    /// </summary>
    public void ShakeLight()
    {
        Shake(0.05f, 0.1f);
    }

    /// <summary>
    /// 中等震动（受伤、击中敌人）
    /// </summary>
    public void ShakeMedium()
    {
        Shake(0.15f, 0.2f);
    }

    /// <summary>
    /// 强烈震动（Boss攻击、爆炸）
    /// </summary>
    public void ShakeHeavy()
    {
        Shake(0.4f, 0.4f);
    }

    /// <summary>
    /// 极强震动（Boss阶段切换、大型环境变化）
    /// </summary>
    public void ShakeExtreme()
    {
        Shake(0.7f, 0.6f);
    }

    /// <summary>
    /// 设置持续性震动强度（0=停止）
    /// </summary>
    public void SetContinuousShake(float intensity)
    {
        continuousIntensity = Mathf.Clamp(intensity, 0f, maxShakeIntensity);
    }

    /// <summary>
    /// 停止所有震动
    /// </summary>
    public void StopAll()
    {
        isShaking = false;
        continuousIntensity = 0;
        transform.localPosition = originalLocalPosition;
    }

    /// <summary>
    /// 启用/禁用震动（辅助功能）
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        enableShake = enabled;
        if (!enabled)
            StopAll();
    }

    /// <summary>
    /// 定向震动（用于被击飞等方向性效果）
    /// </summary>
    public void ShakeDirectional(Vector2 direction, float intensity = 0.3f, float duration = 0.2f)
    {
        if (!enableShake) return;

        StartCoroutine(DoDirectionalShake(direction.normalized, intensity, duration));
    }

    private IEnumerator DoDirectionalShake(Vector2 dir, float intensity, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float decay = 1f - (elapsed / duration);
            float offset = Mathf.Sin(elapsed * 30f) * intensity * decay;
            transform.localPosition = originalLocalPosition + (Vector3)(dir * offset);
            yield return null;
        }
        // 不重置位置，让LateUpdate处理
    }
}
