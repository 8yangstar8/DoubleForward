using UnityEngine;
using System.Collections;

/// <summary>
/// 关卡环境配置 - 每个关卡场景放置一个
/// 覆盖世界默认环境设置，添加关卡特有效果
/// 支持中途环境变化（如进入洞穴、Boss触发等）
/// </summary>
public class LevelEnvironmentSetup : MonoBehaviour
{
    [Header("环境覆盖")]
    [SerializeField] private bool overrideWorldPreset = false;
    [SerializeField] private EnvironmentEffectManager.WeatherType weatherOverride;
    [SerializeField] private float weatherIntensity = 1f;
    [SerializeField] private Color ambientColorOverride = Color.white;
    [SerializeField] private float lightIntensityOverride = 1f;
    [SerializeField] private Color lightColorOverride = Color.white;

    [Header("音乐层覆盖")]
    [SerializeField] private bool overrideMusicLayers = false;
    [SerializeField] private MusicLayerSystem.MusicLayer[] musicLayerOverrides;

    [Header("关卡特效")]
    [SerializeField] private bool enableScreenVignette = false;
    [SerializeField] private float vignetteIntensity = 0.3f;
    [SerializeField] private bool enableChromaticAberration = false;
    [SerializeField] private float chromaticIntensity = 0.1f;

    [Header("动态区域")]
    [SerializeField] private EnvironmentZone[] environmentZones;

    [Header("剧情触发环境变化")]
    [SerializeField] private EnvironmentTrigger[] storyTriggers;

    [System.Serializable]
    public class EnvironmentZone
    {
        public string zoneId;
        public BoxCollider2D zoneTrigger;
        public EnvironmentEffectManager.WeatherType zoneWeather;
        public Color zoneAmbientColor = Color.white;
        public float zoneLightIntensity = 1f;
        public AudioClip zoneAmbientClip;
        public bool isDark;                 // 进入黑暗区域
    }

    [System.Serializable]
    public class EnvironmentTrigger
    {
        public string triggerId;
        public float triggerAtProgress;      // 0~1 关卡进度触发
        public EnvironmentEffectManager.WeatherType weatherChange;
        public Color ambientColorChange;
        public float lightIntensityChange;
        public bool triggered;
    }

    void Start()
    {
        StartCoroutine(SetupEnvironment());
    }

    private IEnumerator SetupEnvironment()
    {
        // 等待环境系统就绪
        while (EnvironmentEffectManager.Instance == null)
            yield return null;

        if (overrideWorldPreset)
        {
            EnvironmentEffectManager.Instance.SetWeather(weatherOverride, weatherIntensity);
            EnvironmentEffectManager.Instance.SetAmbientColor(ambientColorOverride);
            EnvironmentEffectManager.Instance.SetGlobalLight(lightIntensityOverride, lightColorOverride);
        }

        // 音乐层覆盖
        if (overrideMusicLayers && MusicLayerSystem.Instance != null && musicLayerOverrides != null)
        {
            MusicLayerSystem.Instance.LoadTrackSet($"level_custom", musicLayerOverrides);
        }

        // 屏幕效果
        if (enableScreenVignette && CameraEffects.Instance != null)
            CameraEffects.Instance.SetVignette(vignetteIntensity);

        // 设置区域触发器
        SetupZoneTriggers();
    }

    void Update()
    {
        // 检查剧情触发
        CheckStoryTriggers();
    }

    // ==================== 区域系统 ====================

    private void SetupZoneTriggers()
    {
        if (environmentZones == null) return;

        foreach (var zone in environmentZones)
        {
            if (zone.zoneTrigger == null) continue;
            zone.zoneTrigger.isTrigger = true;

            // 添加区域检测组件
            var detector = zone.zoneTrigger.gameObject.AddComponent<EnvironmentZoneDetector>();
            detector.Initialize(zone);
        }
    }

    private void CheckStoryTriggers()
    {
        if (storyTriggers == null || LevelManager.Instance == null) return;

        float elapsed = LevelManager.Instance.ElapsedTime;
        float parTime = 120f; // 默认标准时间
        if (LevelManager.Instance.CurrentLevel != null)
            parTime = LevelManager.Instance.CurrentLevel.parTime;

        float progress = Mathf.Clamp01(elapsed / parTime);

        foreach (var trigger in storyTriggers)
        {
            if (trigger.triggered) continue;
            if (progress < trigger.triggerAtProgress) continue;

            trigger.triggered = true;
            ApplyEnvironmentTrigger(trigger);
        }
    }

    private void ApplyEnvironmentTrigger(EnvironmentTrigger trigger)
    {
        if (EnvironmentEffectManager.Instance == null) return;

        if (trigger.weatherChange != EnvironmentEffectManager.WeatherType.None)
            EnvironmentEffectManager.Instance.SetWeather(trigger.weatherChange);

        if (trigger.ambientColorChange != Color.clear)
            EnvironmentEffectManager.Instance.SetAmbientColor(trigger.ambientColorChange);

        if (trigger.lightIntensityChange > 0)
            EnvironmentEffectManager.Instance.SetGlobalLight(
                trigger.lightIntensityChange, Color.white);
    }

    // ==================== 公共方法 ====================

    /// <summary>
    /// 手动触发环境变化（由BossArena/剧情系统调用）
    /// </summary>
    public void TriggerEnvironmentChange(string triggerId)
    {
        if (storyTriggers == null) return;

        foreach (var trigger in storyTriggers)
        {
            if (trigger.triggerId == triggerId && !trigger.triggered)
            {
                trigger.triggered = true;
                ApplyEnvironmentTrigger(trigger);
                return;
            }
        }
    }
}

/// <summary>
/// 环境区域检测器 - 自动添加到区域触发器上
/// 检测玩家进出并切换环境效果
/// </summary>
public class EnvironmentZoneDetector : MonoBehaviour
{
    private LevelEnvironmentSetup.EnvironmentZone zoneData;
    private bool playerInside;

    public void Initialize(LevelEnvironmentSetup.EnvironmentZone zone)
    {
        zoneData = zone;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (zoneData == null) return;
        if (other.GetComponent<PlayerController>() == null) return;
        if (playerInside) return;

        playerInside = true;

        if (EnvironmentEffectManager.Instance != null)
        {
            EnvironmentEffectManager.Instance.SetWeather(zoneData.zoneWeather);
            EnvironmentEffectManager.Instance.SetAmbientColor(zoneData.zoneAmbientColor);
            EnvironmentEffectManager.Instance.SetGlobalLight(
                zoneData.zoneLightIntensity, Color.white);

            if (zoneData.isDark)
                EnvironmentEffectManager.Instance.TransitionToDarkness(1f);
        }

        if (zoneData.zoneAmbientClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayAmbient(zoneData.zoneAmbientClip);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (zoneData == null) return;
        if (other.GetComponent<PlayerController>() == null) return;

        playerInside = false;
        // 注意：退出区域时不自动恢复，由下一个区域或默认设置负责
    }
}
