using UnityEngine;

/// <summary>
/// 环境特效集合 - 为关卡环境提供氛围粒子和交互反馈
/// 包含：水花、尘土飞扬、火焰、萤火虫、落叶等
/// 挂载在关卡中，根据区域类型自动配置
/// </summary>
public class EnvironmentVFX : MonoBehaviour
{
    [Header("区域类型")]
    [SerializeField] private AreaType areaType = AreaType.Forest;
    [SerializeField] private bool autoPlay = true;

    [Header("氛围粒子")]
    [SerializeField] private ParticleSystem ambientParticles;
    [SerializeField] private float ambientEmissionRate = 5f;

    [Header("触发粒子")]
    [SerializeField] private ParticleSystem triggerParticles;

    [Header("天气粒子")]
    [SerializeField] private ParticleSystem weatherParticles;
    [SerializeField] private WeatherType weather = WeatherType.None;

    [Header("互动")]
    [SerializeField] private float interactionRadius = 2f;
    [SerializeField] private float interactionCooldown = 0.5f;

    public enum AreaType
    {
        Forest,         // 森林 — 落叶、萤火虫
        Cave,           // 洞穴 — 尘埃、矿石微光
        Water,          // 水域 — 水泡、波纹
        Lava,           // 熔岩 — 火星、热浪
        Shadow,         // 暗影区 — 暗紫色尘埃
        Light,          // 光明区 — 金色光粒
        Ice,            // 冰雪区 — 雪花、冰晶
        Mechanical      // 机械区 — 火花、蒸汽
    }

    public enum WeatherType
    {
        None,
        Rain,
        Snow,
        Sandstorm,
        Fog
    }

    private float interactionTimer;

    void Start()
    {
        if (autoPlay)
            ConfigureAmbientParticles();

        ConfigureWeather();
    }

    void Update()
    {
        if (interactionTimer > 0)
            interactionTimer -= Time.deltaTime;

        // 检查玩家靠近时触发互动粒子
        CheckPlayerProximity();
    }

    // ==================== 配置 ====================

    private void ConfigureAmbientParticles()
    {
        if (ambientParticles == null) return;

        var main = ambientParticles.main;
        var emission = ambientParticles.emission;
        emission.rateOverTime = ambientEmissionRate;

        switch (areaType)
        {
            case AreaType.Forest:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.4f, 0.8f, 0.2f, 0.6f),
                    new Color(0.9f, 0.7f, 0.1f, 0.4f));
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                main.gravityModifier = 0.1f;
                break;

            case AreaType.Cave:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.6f, 0.6f, 0.5f, 0.3f),
                    new Color(0.8f, 0.7f, 0.5f, 0.2f));
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                main.gravityModifier = -0.02f; // 轻微上升
                break;

            case AreaType.Water:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.3f, 0.6f, 1f, 0.5f),
                    new Color(0.5f, 0.8f, 1f, 0.3f));
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
                main.gravityModifier = -0.1f;
                break;

            case AreaType.Lava:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.5f, 0f, 0.8f),
                    new Color(1f, 0.2f, 0f, 0.6f));
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
                main.gravityModifier = -0.3f;
                break;

            case AreaType.Shadow:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.3f, 0.1f, 0.5f, 0.5f),
                    new Color(0.1f, 0.05f, 0.2f, 0.3f));
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
                main.gravityModifier = -0.05f;
                break;

            case AreaType.Light:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.95f, 0.7f, 0.7f),
                    new Color(1f, 0.85f, 0.5f, 0.4f));
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
                main.gravityModifier = -0.08f;
                break;

            case AreaType.Ice:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.7f, 0.9f, 1f, 0.5f),
                    new Color(0.9f, 0.95f, 1f, 0.3f));
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
                main.gravityModifier = 0.15f;
                break;

            case AreaType.Mechanical:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.7f, 0.2f, 0.8f),
                    new Color(0.8f, 0.5f, 0.1f, 0.5f));
                main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
                main.gravityModifier = 0.2f;
                break;
        }

        ambientParticles.Play();
    }

    private void ConfigureWeather()
    {
        if (weatherParticles == null || weather == WeatherType.None) return;

        var main = weatherParticles.main;
        var emission = weatherParticles.emission;

        switch (weather)
        {
            case WeatherType.Rain:
                main.startColor = new Color(0.5f, 0.6f, 0.8f, 0.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 8f);
                main.startLifetime = 2f;
                main.gravityModifier = 1f;
                emission.rateOverTime = 50f;
                break;

            case WeatherType.Snow:
                main.startColor = new Color(0.95f, 0.95f, 1f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                main.startLifetime = 5f;
                main.gravityModifier = 0.2f;
                emission.rateOverTime = 20f;
                break;

            case WeatherType.Sandstorm:
                main.startColor = new Color(0.8f, 0.7f, 0.4f, 0.3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
                main.startLifetime = 3f;
                main.gravityModifier = 0f;
                emission.rateOverTime = 30f;
                break;

            case WeatherType.Fog:
                main.startColor = new Color(0.8f, 0.8f, 0.8f, 0.15f);
                main.startSize = new ParticleSystem.MinMaxCurve(1f, 3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                main.startLifetime = 8f;
                main.gravityModifier = 0f;
                emission.rateOverTime = 3f;
                break;
        }

        weatherParticles.Play();
    }

    // ==================== 玩家互动 ====================

    private void CheckPlayerProximity()
    {
        if (triggerParticles == null || interactionTimer > 0) return;

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            float dist = Vector2.Distance(transform.position, p.transform.position);
            if (dist < interactionRadius)
            {
                TriggerInteractionParticles(p.transform.position);
                interactionTimer = interactionCooldown;
                break;
            }
        }
    }

    private void TriggerInteractionParticles(Vector3 playerPos)
    {
        if (triggerParticles == null) return;

        switch (areaType)
        {
            case AreaType.Forest:
                // 落叶飞散
                triggerParticles.transform.position = playerPos;
                triggerParticles.Emit(5);
                break;

            case AreaType.Water:
                // 水花溅起
                triggerParticles.transform.position = playerPos + Vector3.down * 0.3f;
                triggerParticles.Emit(8);
                break;

            case AreaType.Ice:
                // 冰晶碎裂
                triggerParticles.transform.position = playerPos + Vector3.down * 0.2f;
                triggerParticles.Emit(4);
                break;

            case AreaType.Shadow:
                // 暗影扰动
                triggerParticles.transform.position = playerPos;
                triggerParticles.Emit(6);
                break;

            default:
                triggerParticles.transform.position = playerPos;
                triggerParticles.Emit(3);
                break;
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 切换区域类型（用于区域过渡）
    /// </summary>
    public void SetAreaType(AreaType type)
    {
        areaType = type;
        if (ambientParticles != null)
        {
            ambientParticles.Stop();
            ConfigureAmbientParticles();
        }
    }

    /// <summary>
    /// 设置天气效果
    /// </summary>
    public void SetWeather(WeatherType type)
    {
        weather = type;
        if (weatherParticles != null)
        {
            weatherParticles.Stop();
            ConfigureWeather();
        }
    }

    /// <summary>
    /// 暂停/恢复所有粒子
    /// </summary>
    public void SetPaused(bool paused)
    {
        if (ambientParticles != null)
        {
            if (paused) ambientParticles.Pause();
            else ambientParticles.Play();
        }

        if (weatherParticles != null)
        {
            if (paused) weatherParticles.Pause();
            else weatherParticles.Play();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
