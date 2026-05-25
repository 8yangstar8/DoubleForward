using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 粒子特效预设库 - ScriptableObject存储所有特效配置
/// 与VFXManager配合使用，提供可视化编辑的特效参数
/// 每个世界有不同的视觉风格变体
/// </summary>
[CreateAssetMenu(fileName = "ParticleEffectLibrary", menuName = "DoubleForward/Particle Effect Library")]
public class ParticleEffectLibrary : ScriptableObject
{
    [System.Serializable]
    public class EffectPreset
    {
        public string effectId;
        public string displayName;
        public EffectCategory category;
        public GameObject prefab;

        [Header("基础参数")]
        public float duration = 2f;
        public int poolSize = 5;
        public float defaultScale = 1f;

        [Header("颜色变体")]
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.white;
        public bool useWorldTint = false; // 是否应用世界主题色调

        [Header("音效联动")]
        public AudioClip sfxOnPlay;
        [Range(0f, 1f)] public float sfxVolume = 0.5f;

        [Header("屏幕效果")]
        public bool triggerScreenShake = false;
        public float shakeIntensity = 0.2f;
        public float shakeDuration = 0.15f;
        public bool triggerSlowMotion = false;
        public float slowMotionScale = 0.3f;
        public float slowMotionDuration = 0.15f;
    }

    public enum EffectCategory
    {
        Player,         // 玩家相关（跑步灰尘、跳跃、着陆）
        Combat,         // 战斗相关（命中、死亡、爆炸）
        Ability,        // 技能相关（光束、暗影、合作技）
        Environment,    // 环境相关（检查点、传送门、陷阱）
        UI,             // UI特效（收集品、升级、成就）
        Boss            // Boss战专用
    }

    [Header("全局特效预设")]
    [SerializeField] private List<EffectPreset> presets = new List<EffectPreset>();

    [Header("世界色调配置")]
    [SerializeField] private WorldTintConfig[] worldTints = new WorldTintConfig[5];

    [System.Serializable]
    public class WorldTintConfig
    {
        public string worldName;
        public Color primaryTint = Color.white;
        public Color secondaryTint = Color.white;
        public Color ambientParticleColor = Color.white;
    }

    // 运行时缓存
    private Dictionary<string, EffectPreset> presetMap;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        presetMap = new Dictionary<string, EffectPreset>();
        foreach (var preset in presets)
        {
            if (!string.IsNullOrEmpty(preset.effectId))
                presetMap[preset.effectId] = preset;
        }
    }

    /// <summary>
    /// 获取特效预设
    /// </summary>
    public EffectPreset GetPreset(string effectId)
    {
        if (presetMap == null) BuildLookup();
        return presetMap.TryGetValue(effectId, out var preset) ? preset : null;
    }

    /// <summary>
    /// 获取指定类别的所有预设
    /// </summary>
    public List<EffectPreset> GetByCategory(EffectCategory category)
    {
        return presets.FindAll(p => p.category == category);
    }

    /// <summary>
    /// 获取世界色调配置
    /// </summary>
    public WorldTintConfig GetWorldTint(int chapterIndex)
    {
        if (chapterIndex < 0 || chapterIndex >= worldTints.Length)
            return null;
        return worldTints[chapterIndex];
    }

    /// <summary>
    /// 应用世界色调到粒子系统
    /// </summary>
    public void ApplyWorldTint(ParticleSystem ps, int chapterIndex)
    {
        var tint = GetWorldTint(chapterIndex);
        if (tint == null || ps == null) return;

        var main = ps.main;
        var startColor = main.startColor;

        if (startColor.mode == ParticleSystemGradientMode.Color)
        {
            Color original = startColor.color;
            main.startColor = original * tint.primaryTint;
        }
    }

    /// <summary>
    /// 获取所有预设数量
    /// </summary>
    public int Count => presets.Count;

    /// <summary>
    /// 初始化默认预设列表（编辑器工具调用）
    /// </summary>
    public void InitializeDefaults()
    {
        if (presets.Count > 0) return;

        // 玩家特效
        AddPreset("dust_land", "着陆灰尘", EffectCategory.Player, 1.0f, 8);
        AddPreset("dust_run", "跑步灰尘", EffectCategory.Player, 0.5f, 10);
        AddPreset("dash_trail", "冲刺拖尾", EffectCategory.Player, 0.8f, 5);
        AddPreset("player_hit", "玩家受击", EffectCategory.Player, 1.0f, 5, true, 0.15f);
        AddPreset("player_death", "玩家死亡", EffectCategory.Player, 2.0f, 3, true, 0.4f);
        AddPreset("player_respawn", "玩家重生", EffectCategory.Player, 2.5f, 3);

        // 战斗特效
        AddPreset("enemy_hit", "敌人受击", EffectCategory.Combat, 0.8f, 10, true, 0.1f);
        AddPreset("enemy_death", "敌人死亡", EffectCategory.Combat, 1.5f, 8, true, 0.2f);
        AddPreset("explosion", "爆炸", EffectCategory.Combat, 2.0f, 5, true, 0.5f);
        AddPreset("shield_block", "护盾格挡", EffectCategory.Combat, 1.0f, 5);
        AddPreset("shield_break", "护盾破碎", EffectCategory.Combat, 1.5f, 3, true, 0.3f);

        // 技能特效
        AddPreset("light_beam_impact", "光束击中", EffectCategory.Ability, 1.5f, 5, true, 0.15f);
        AddPreset("shadow_phase_trail", "暗影穿越拖尾", EffectCategory.Ability, 1.0f, 5);
        AddPreset("light_bridge_glow", "光桥发光", EffectCategory.Ability, 3.0f, 3);
        AddPreset("shadow_zone_ambient", "暗影区域", EffectCategory.Ability, 5.0f, 3);
        AddPreset("heal", "治愈", EffectCategory.Ability, 2.0f, 5);
        AddPreset("heal_burst", "治愈爆发", EffectCategory.Ability, 1.5f, 3);
        AddPreset("buff_apply", "增益施加", EffectCategory.Ability, 1.5f, 5);
        AddPreset("debuff_apply", "减益施加", EffectCategory.Ability, 1.5f, 5);
        AddPreset("revive_complete", "复活完成", EffectCategory.Ability, 2.0f, 3);

        // 环境特效
        AddPreset("checkpoint_activate", "检查点激活", EffectCategory.Environment, 2.0f, 5);
        AddPreset("collect", "收集品", EffectCategory.Environment, 1.0f, 10);
        AddPreset("pressure_plate", "压力板", EffectCategory.Environment, 1.0f, 5);
        AddPreset("portal_enter", "传送门", EffectCategory.Environment, 1.5f, 3);

        // UI特效
        AddPreset("level_complete", "关卡完成", EffectCategory.UI, 3.0f, 2);

        // Boss特效
        AddPreset("boss_defeat", "Boss击败", EffectCategory.Boss, 4.0f, 2, true, 0.6f);

        // 初始化世界色调
        worldTints = new WorldTintConfig[]
        {
            new WorldTintConfig
            {
                worldName = "光影遗迹",
                primaryTint = new Color(1f, 0.95f, 0.8f),
                secondaryTint = new Color(0.7f, 0.7f, 1f),
                ambientParticleColor = new Color(1f, 1f, 0.85f, 0.5f)
            },
            new WorldTintConfig
            {
                worldName = "冰火熔炉",
                primaryTint = new Color(0.7f, 0.85f, 1f),
                secondaryTint = new Color(1f, 0.5f, 0.3f),
                ambientParticleColor = new Color(0.8f, 0.9f, 1f, 0.5f)
            },
            new WorldTintConfig
            {
                worldName = "沙漠风暴",
                primaryTint = new Color(1f, 0.9f, 0.6f),
                secondaryTint = new Color(0.9f, 0.75f, 0.5f),
                ambientParticleColor = new Color(1f, 0.95f, 0.7f, 0.4f)
            },
            new WorldTintConfig
            {
                worldName = "深渊暗流",
                primaryTint = new Color(0.4f, 0.3f, 0.7f),
                secondaryTint = new Color(0.2f, 0.8f, 0.6f),
                ambientParticleColor = new Color(0.3f, 0.2f, 0.5f, 0.5f)
            },
            new WorldTintConfig
            {
                worldName = "天空之巅",
                primaryTint = new Color(1f, 1f, 1f),
                secondaryTint = new Color(0.9f, 0.85f, 1f),
                ambientParticleColor = new Color(1f, 1f, 1f, 0.3f)
            }
        };
    }

    private void AddPreset(string id, string name, EffectCategory category,
        float duration, int poolSize, bool shake = false, float shakeIntensity = 0f)
    {
        presets.Add(new EffectPreset
        {
            effectId = id,
            displayName = name,
            category = category,
            duration = duration,
            poolSize = poolSize,
            triggerScreenShake = shake,
            shakeIntensity = shakeIntensity,
            shakeDuration = shake ? 0.15f : 0f
        });
    }
}
