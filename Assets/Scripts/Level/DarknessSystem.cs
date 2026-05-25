using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 黑暗系统 - 管理关卡中的光暗区域
/// Lux角色周围发光照亮区域，Nox在黑暗中获得增益
/// 核心光影二元机制的基础
/// </summary>
public class DarknessSystem : MonoBehaviour
{
    public static DarknessSystem Instance { get; private set; }

    [Header("全局黑暗")]
    [SerializeField] private float globalDarkness = 0.8f; // 0=全亮, 1=全黑
    [SerializeField] private bool enableDarkness = true;

    [Header("Lux光源")]
    [SerializeField] private float luxLightRadius = 6f;
    [SerializeField] private float luxLightIntensity = 1f;
    [SerializeField] private Color luxLightColor = new Color(1f, 0.95f, 0.8f);
    [SerializeField] private float lightPulseSpeed = 1f;
    [SerializeField] private float lightPulseAmount = 0.1f;

    [Header("Nox暗影增益")]
    [SerializeField] private float noxDarkBonusDamage = 0.2f;
    [SerializeField] private float noxDarkBonusSpeed = 0.1f;
    [SerializeField] private float noxVisibilityInDark = 8f; // Nox在黑暗中的视野

    [Header("关卡光源")]
    [SerializeField] private float torchRadius = 3f;
    [SerializeField] private Color torchColor = new Color(1f, 0.7f, 0.3f);

    // 动态光源列表
    private List<LightSource> lightSources = new List<LightSource>();

    /// <summary>
    /// 只读光源列表（供DarknessFogRenderer等外部系统渲染使用）
    /// </summary>
    public IReadOnlyList<LightSource> ActiveLightSources => lightSources;
    private PlayerController luxPlayer;
    private PlayerController noxPlayer;

    [System.Serializable]
    public class LightSource
    {
        public Transform transform;
        public float radius;
        public float intensity;
        public Color color;
        public bool isActive;
        public LightSourceType type;
    }

    public enum LightSourceType
    {
        PlayerLux,
        Torch,
        Crystal,
        Dynamic,
        Skill
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        FindPlayers();
    }

    void Update()
    {
        if (!enableDarkness) return;

        // 如果还没有找到玩家，继续查找
        if (luxPlayer == null || noxPlayer == null)
            FindPlayers();

        UpdateLuxLight();
        UpdateNoxBuffs();
    }

    // ============ 玩家相关 ============

    private void FindPlayers()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Type == PlayerController.PlayerType.Lux)
                luxPlayer = p;
            else if (p.Type == PlayerController.PlayerType.Nox)
                noxPlayer = p;
        }

        // 为Lux注册自动光源
        if (luxPlayer != null && !HasLightSource(luxPlayer.transform))
        {
            RegisterLightSource(luxPlayer.transform, luxLightRadius, luxLightIntensity, luxLightColor, LightSourceType.PlayerLux);
        }
    }

    private void UpdateLuxLight()
    {
        if (luxPlayer == null) return;

        // 光源脉冲效果
        float pulse = 1f + Mathf.Sin(Time.time * lightPulseSpeed) * lightPulseAmount;
        var source = GetLightSource(luxPlayer.transform);
        if (source != null)
        {
            source.radius = luxLightRadius * pulse;
        }
    }

    private void UpdateNoxBuffs()
    {
        if (noxPlayer == null) return;

        // 检查Nox是否在黑暗中
        bool inDarkness = !IsPointLit(noxPlayer.transform.position);

        // 暗影增益通过Tag方式已有处理（PlayerController中的IsInShadowZone）
        // 这里主要管理视觉效果
    }

    // ============ 光源管理 ============

    /// <summary>
    /// 注册一个光源
    /// </summary>
    public void RegisterLightSource(Transform source, float radius, float intensity, Color color, LightSourceType type = LightSourceType.Dynamic)
    {
        lightSources.Add(new LightSource
        {
            transform = source,
            radius = radius,
            intensity = intensity,
            color = color,
            isActive = true,
            type = type
        });
    }

    /// <summary>
    /// 注销光源
    /// </summary>
    public void UnregisterLightSource(Transform source)
    {
        lightSources.RemoveAll(ls => ls.transform == source);
    }

    /// <summary>
    /// 检查某个点是否被照亮
    /// </summary>
    public bool IsPointLit(Vector3 point)
    {
        foreach (var ls in lightSources)
        {
            if (ls.transform == null || !ls.isActive) continue;
            float dist = Vector2.Distance(point, ls.transform.position);
            if (dist <= ls.radius)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 获取某点的光照强度 (0-1)
    /// </summary>
    public float GetLightIntensityAt(Vector3 point)
    {
        float maxIntensity = 0f;

        foreach (var ls in lightSources)
        {
            if (ls.transform == null || !ls.isActive) continue;
            float dist = Vector2.Distance(point, ls.transform.position);
            if (dist <= ls.radius)
            {
                float falloff = 1f - (dist / ls.radius);
                float intensity = falloff * ls.intensity;
                maxIntensity = Mathf.Max(maxIntensity, intensity);
            }
        }

        return maxIntensity;
    }

    /// <summary>
    /// 获取某点的光照颜色
    /// </summary>
    public Color GetLightColorAt(Vector3 point)
    {
        Color result = Color.black;
        float totalWeight = 0f;

        foreach (var ls in lightSources)
        {
            if (ls.transform == null || !ls.isActive) continue;
            float dist = Vector2.Distance(point, ls.transform.position);
            if (dist <= ls.radius)
            {
                float weight = (1f - (dist / ls.radius)) * ls.intensity;
                result += ls.color * weight;
                totalWeight += weight;
            }
        }

        if (totalWeight > 0)
            result /= totalWeight;

        return result;
    }

    private LightSource GetLightSource(Transform t)
    {
        foreach (var ls in lightSources)
            if (ls.transform == t) return ls;
        return null;
    }

    private bool HasLightSource(Transform t)
    {
        return GetLightSource(t) != null;
    }

    /// <summary>
    /// 临时增加Lux光源范围（技能效果）
    /// </summary>
    public void BoostLuxLight(float bonusRadius, float duration)
    {
        StartCoroutine(TemporaryLightBoost(bonusRadius, duration));
    }

    private System.Collections.IEnumerator TemporaryLightBoost(float bonus, float duration)
    {
        float original = luxLightRadius;
        luxLightRadius += bonus;
        yield return new WaitForSeconds(duration);
        luxLightRadius = original;
    }

    /// <summary>
    /// 设置全局黑暗度
    /// </summary>
    public void SetGlobalDarkness(float darkness)
    {
        globalDarkness = Mathf.Clamp01(darkness);
    }

    /// <summary>
    /// 获取全局黑暗度
    /// </summary>
    public float GetGlobalDarkness() => globalDarkness;

    // ============ 清理 ============

    void OnDestroy()
    {
        lightSources.Clear();
        if (Instance == this) Instance = null;
    }

    void OnDrawGizmosSelected()
    {
        // 画所有光源
        foreach (var ls in lightSources)
        {
            if (ls.transform == null) continue;

            Gizmos.color = new Color(ls.color.r, ls.color.g, ls.color.b, 0.3f);
            Gizmos.DrawSphere(ls.transform.position, ls.radius);
            Gizmos.color = new Color(ls.color.r, ls.color.g, ls.color.b, 0.8f);
            Gizmos.DrawWireSphere(ls.transform.position, ls.radius);
        }
    }
}
