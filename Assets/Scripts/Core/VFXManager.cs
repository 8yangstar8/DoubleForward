using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 粒子特效管理器 - 统一管理游戏中所有视觉特效
/// 集成对象池，支持按类型分类、自动回收、屏幕震动
/// </summary>
public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [System.Serializable]
    public class VFXEntry
    {
        public string effectName;
        public GameObject prefab;
        public int poolSize = 5;
        public float defaultDuration = 2f;
    }

    [Header("特效配置")]
    [SerializeField] private List<VFXEntry> vfxEntries = new List<VFXEntry>();

    [Header("屏幕震动")]
    [SerializeField] private float defaultShakeIntensity = 0.3f;
    [SerializeField] private float defaultShakeDuration = 0.2f;

    private Dictionary<string, VFXEntry> entryMap = new Dictionary<string, VFXEntry>();
    private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
    private Dictionary<string, Transform> containers = new Dictionary<string, Transform>();

    // 屏幕震动
    private float shakeTimer;
    private float shakeIntensity;
    private Vector3 originalCamPos;
    private bool isShaking;
    private Camera shakeCamera;

    public event System.Action<float, float> OnScreenShake; // intensity, duration

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializePools();
    }

    void Update()
    {
        UpdateScreenShake();
    }

    private void InitializePools()
    {
        foreach (var entry in vfxEntries)
        {
            if (entry.prefab == null) continue;

            entryMap[entry.effectName] = entry;
            pools[entry.effectName] = new Queue<GameObject>();

            var container = new GameObject($"VFX_{entry.effectName}");
            container.transform.SetParent(transform);
            containers[entry.effectName] = container.transform;

            for (int i = 0; i < entry.poolSize; i++)
            {
                var obj = Instantiate(entry.prefab, container.transform);
                obj.SetActive(false);
                pools[entry.effectName].Enqueue(obj);
            }
        }
    }

    /// <summary>
    /// 在指定位置播放特效
    /// </summary>
    public GameObject Play(string effectName, Vector3 position, Quaternion rotation = default)
    {
        if (rotation == default) rotation = Quaternion.identity;

        if (!pools.ContainsKey(effectName))
        {
            Debug.LogWarning($"[VFX] 特效不存在: {effectName}");
            return null;
        }

        var pool = pools[effectName];
        var entry = entryMap[effectName];
        GameObject obj;

        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
            if (obj == null)
                obj = Instantiate(entry.prefab, containers[effectName]);
        }
        else
        {
            obj = Instantiate(entry.prefab, containers[effectName]);
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        // 重启粒子系统
        var ps = obj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear();
            ps.Play();
        }

        // 自动回收
        StartCoroutine(ReturnToPool(effectName, obj, entry.defaultDuration));

        return obj;
    }

    /// <summary>
    /// 在指定位置播放特效（跟随目标）
    /// </summary>
    public GameObject PlayAttached(string effectName, Transform parent, Vector3 localOffset = default)
    {
        var obj = Play(effectName, parent.position + localOffset);
        if (obj != null)
            obj.transform.SetParent(parent);
        return obj;
    }

    /// <summary>
    /// 播放并缩放特效
    /// </summary>
    public GameObject PlayScaled(string effectName, Vector3 position, float scale)
    {
        var obj = Play(effectName, position);
        if (obj != null)
            obj.transform.localScale = Vector3.one * scale;
        return obj;
    }

    private System.Collections.IEnumerator ReturnToPool(string effectName, GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (obj == null) yield break;

        obj.SetActive(false);
        obj.transform.SetParent(containers.ContainsKey(effectName) ? containers[effectName] : transform);
        obj.transform.localScale = Vector3.one;

        if (pools.ContainsKey(effectName))
            pools[effectName].Enqueue(obj);
    }

    // ============ 屏幕震动 ============

    /// <summary>
    /// 触发屏幕震动
    /// </summary>
    public void ShakeScreen(float intensity = -1f, float duration = -1f)
    {
        if (intensity < 0) intensity = defaultShakeIntensity;
        if (duration < 0) duration = defaultShakeDuration;

        shakeCamera = Camera.main;
        if (shakeCamera == null) return;

        if (!isShaking)
            originalCamPos = shakeCamera.transform.localPosition;

        shakeIntensity = intensity;
        shakeTimer = duration;
        isShaking = true;

        OnScreenShake?.Invoke(intensity, duration);

        // 手柄振动联动
        if (GamepadAdapter.Instance != null)
        {
            if (intensity > 0.5f)
                GamepadAdapter.Instance.VibrateHeavy(0);
            else
                GamepadAdapter.Instance.VibrateMedium(0);
        }
    }

    /// <summary>
    /// 轻微震动（如收集物品）
    /// </summary>
    public void ShakeLight()
    {
        ShakeScreen(0.1f, 0.1f);
    }

    /// <summary>
    /// 中等震动（如受击）
    /// </summary>
    public void ShakeMedium()
    {
        ShakeScreen(0.3f, 0.2f);
    }

    /// <summary>
    /// 强烈震动（如Boss攻击）
    /// </summary>
    public void ShakeHeavy()
    {
        ShakeScreen(0.6f, 0.4f);
    }

    private void UpdateScreenShake()
    {
        if (!isShaking || shakeCamera == null) return;

        if (shakeTimer > 0)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            float damping = shakeTimer / defaultShakeDuration;
            Vector2 randomOffset = Random.insideUnitCircle * shakeIntensity * damping;
            shakeCamera.transform.localPosition = originalCamPos + new Vector3(randomOffset.x, randomOffset.y, 0);
        }
        else
        {
            shakeCamera.transform.localPosition = originalCamPos;
            isShaking = false;
        }
    }

    // ============ 预制常用特效名称 ============

    public static class Effects
    {
        public const string PlayerHit = "player_hit";
        public const string PlayerDeath = "player_death";
        public const string PlayerRespawn = "player_respawn";
        public const string EnemyHit = "enemy_hit";
        public const string EnemyDeath = "enemy_death";
        public const string Collect = "collect";
        public const string LightBeam = "light_beam_impact";
        public const string ShadowPhase = "shadow_phase_trail";
        public const string LightBridge = "light_bridge_glow";
        public const string ShadowZone = "shadow_zone_ambient";
        public const string CheckpointActivate = "checkpoint_activate";
        public const string LevelComplete = "level_complete";
        public const string BossDefeat = "boss_defeat";
        public const string PressurePlateActivate = "pressure_plate";
        public const string PortalEnter = "portal_enter";
        public const string DustLand = "dust_land";
        public const string DustRun = "dust_run";
        public const string DashTrail = "dash_trail";
        public const string HealEffect = "heal";
    }
}
