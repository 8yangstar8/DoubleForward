using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 音效反馈系统 - 集中管理游戏交互音效
/// 配合AudioManager使用，提供语义化的音效播放接口
/// </summary>
public class SoundFeedback : MonoBehaviour
{
    public static SoundFeedback Instance { get; private set; }

    [System.Serializable]
    public class SoundEntry
    {
        public string soundName;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.5f, 2f)] public float pitchMin = 0.95f;
        [Range(0.5f, 2f)] public float pitchMax = 1.05f;
        public bool randomizePitch = true;
    }

    [Header("玩家音效")]
    [SerializeField] private SoundEntry jumpSound;
    [SerializeField] private SoundEntry doubleJumpSound;
    [SerializeField] private SoundEntry landSound;
    [SerializeField] private SoundEntry dashSound;
    [SerializeField] private SoundEntry hurtSound;
    [SerializeField] private SoundEntry deathSound;
    [SerializeField] private SoundEntry respawnSound;
    [SerializeField] private SoundEntry healSound;

    [Header("技能音效")]
    [SerializeField] private SoundEntry lightBeamSound;
    [SerializeField] private SoundEntry lightBridgeSound;
    [SerializeField] private SoundEntry shadowPhaseSound;
    [SerializeField] private SoundEntry shadowZoneSound;
    [SerializeField] private SoundEntry skillCooldownReadySound;

    [Header("交互音效")]
    [SerializeField] private SoundEntry collectSound;
    [SerializeField] private SoundEntry checkpointSound;
    [SerializeField] private SoundEntry portalSound;
    [SerializeField] private SoundEntry pressurePlateOnSound;
    [SerializeField] private SoundEntry pressurePlateOffSound;
    [SerializeField] private SoundEntry doorOpenSound;
    [SerializeField] private SoundEntry doorCloseSound;
    [SerializeField] private SoundEntry gearRotateSound;
    [SerializeField] private SoundEntry switchSound;

    [Header("UI音效")]
    [SerializeField] private SoundEntry uiClickSound;
    [SerializeField] private SoundEntry uiHoverSound;
    [SerializeField] private SoundEntry uiBackSound;
    [SerializeField] private SoundEntry uiErrorSound;
    [SerializeField] private SoundEntry uiSuccessSound;
    [SerializeField] private SoundEntry levelCompleteSound;
    [SerializeField] private SoundEntry starRevealSound;
    [SerializeField] private SoundEntry achievementSound;
    [SerializeField] private SoundEntry comboHitSound;
    [SerializeField] private SoundEntry comboBreakSound;

    [Header("战斗音效")]
    [SerializeField] private SoundEntry footstepSound;
    [SerializeField] private SoundEntry meleeHitSound;
    [SerializeField] private SoundEntry rangedShootSound;

    [Header("环境音效")]
    [SerializeField] private SoundEntry waterSplashSound;
    [SerializeField] private SoundEntry windSound;
    [SerializeField] private SoundEntry thunderSound;
    [SerializeField] private SoundEntry machinerySound;
    [SerializeField] private SoundEntry crystalHumSound;

    private Dictionary<string, SoundEntry> soundMap = new Dictionary<string, SoundEntry>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildSoundMap();
    }

    private void BuildSoundMap()
    {
        // 注册所有音效到字典以支持按名查找
        RegisterSound("jump", jumpSound);
        RegisterSound("double_jump", doubleJumpSound);
        RegisterSound("land", landSound);
        RegisterSound("dash", dashSound);
        RegisterSound("hurt", hurtSound);
        RegisterSound("death", deathSound);
        RegisterSound("respawn", respawnSound);
        RegisterSound("heal", healSound);
        RegisterSound("light_beam", lightBeamSound);
        RegisterSound("light_bridge", lightBridgeSound);
        RegisterSound("shadow_phase", shadowPhaseSound);
        RegisterSound("shadow_zone", shadowZoneSound);
        RegisterSound("skill_ready", skillCooldownReadySound);
        RegisterSound("collect", collectSound);
        RegisterSound("checkpoint", checkpointSound);
        RegisterSound("portal", portalSound);
        RegisterSound("plate_on", pressurePlateOnSound);
        RegisterSound("plate_off", pressurePlateOffSound);
        RegisterSound("door_open", doorOpenSound);
        RegisterSound("door_close", doorCloseSound);
        RegisterSound("gear", gearRotateSound);
        RegisterSound("switch", switchSound);
        RegisterSound("ui_click", uiClickSound);
        RegisterSound("ui_hover", uiHoverSound);
        RegisterSound("ui_back", uiBackSound);
        RegisterSound("ui_error", uiErrorSound);
        RegisterSound("ui_success", uiSuccessSound);
        RegisterSound("level_complete", levelCompleteSound);
        RegisterSound("star_reveal", starRevealSound);
        RegisterSound("achievement", achievementSound);
        RegisterSound("combo_hit", comboHitSound);
        RegisterSound("combo_break", comboBreakSound);
        RegisterSound("footstep", footstepSound);
        RegisterSound("melee_hit", meleeHitSound);
        RegisterSound("ranged_shoot", rangedShootSound);
        RegisterSound("water_splash", waterSplashSound);
        RegisterSound("wind", windSound);
        RegisterSound("thunder", thunderSound);
        RegisterSound("machinery", machinerySound);
        RegisterSound("crystal", crystalHumSound);
    }

    private void RegisterSound(string name, SoundEntry entry)
    {
        if (entry != null && entry.clip != null)
        {
            entry.soundName = name;
            soundMap[name] = entry;
        }
    }

    /// <summary>
    /// 按名称播放音效
    /// </summary>
    public void Play(string soundName)
    {
        if (!soundMap.TryGetValue(soundName, out var entry)) return;
        PlayEntry(entry);
    }

    /// <summary>
    /// 播放音效实体（支持音量和随机音高）
    /// </summary>
    private void PlayEntry(SoundEntry entry)
    {
        if (entry == null || entry.clip == null || AudioManager.Instance == null) return;

        float pitch = 1f;
        if (entry.randomizePitch)
            pitch = Random.Range(entry.pitchMin, entry.pitchMax);

        AudioManager.Instance.PlaySFX(entry.clip, entry.volume, pitch);
    }

    /// <summary>
    /// 在指定世界位置播放音效（支持空间衰减）
    /// </summary>
    public void PlayAtPosition(string soundName, Vector3 position, float spatialBlend = 0.8f)
    {
        if (!soundMap.TryGetValue(soundName, out var entry)) return;
        if (entry == null || entry.clip == null) return;

        // 距离检查 - 超过30米不播放
        if (Camera.main != null)
        {
            float dist = Vector3.Distance(position, Camera.main.transform.position);
            if (dist > 30f) return;
        }

        float pitch = entry.randomizePitch
            ? Random.Range(entry.pitchMin, entry.pitchMax) : 1f;

        AudioManager.Instance.PlaySFX(entry.clip, entry.volume, pitch);
    }

    // ============ 便捷方法 ============

    // 玩家
    public void PlayJump() => Play("jump");
    public void PlayDoubleJump() => Play("double_jump");
    public void PlayLand() => Play("land");
    public void PlayDash() => Play("dash");
    public void PlayHurt() => Play("hurt");
    public void PlayDeath() => Play("death");
    public void PlayRespawn() => Play("respawn");
    public void PlayHeal() => Play("heal");

    // 技能
    public void PlayLightBeam() => Play("light_beam");
    public void PlayLightBridge() => Play("light_bridge");
    public void PlayShadowPhase() => Play("shadow_phase");
    public void PlayShadowZone() => Play("shadow_zone");
    public void PlaySkillReady() => Play("skill_ready");

    // 交互
    public void PlayCollect() => Play("collect");
    public void PlayCheckpoint() => Play("checkpoint");
    public void PlayPortal() => Play("portal");

    // 战斗
    public void PlayFootstep() => Play("footstep");
    public void PlayMeleeHit() => Play("melee_hit");
    public void PlayRangedShoot() => Play("ranged_shoot");

    // UI
    public void PlayUIClick() => Play("ui_click");
    public void PlayUIHover() => Play("ui_hover");
    public void PlayUIBack() => Play("ui_back");
    public void PlayUIError() => Play("ui_error");
    public void PlayUISuccess() => Play("ui_success");
    public void PlayLevelComplete() => Play("level_complete");
    public void PlayAchievement() => Play("achievement");

    // 别名（兼容其他系统调用）
    public void PlayClick() => PlayUIClick();
    public void PlayConfirm() => PlayUISuccess();
}
