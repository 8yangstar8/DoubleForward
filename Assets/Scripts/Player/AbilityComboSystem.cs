using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 技能连携系统 - 当Lux和Nox在短时间内配合使用技能时触发强力连携攻击
/// 例如: Lux光束 + Nox暗影区域 = 光暗爆发
/// 连携技需要合作能量条满足阈值才可触发
/// </summary>
public class AbilityComboSystem : MonoBehaviour
{
    public static AbilityComboSystem Instance { get; private set; }

    [Header("连携窗口")]
    [SerializeField] private float comboWindowDuration = 1.5f;    // 两个技能需要在此时间内使用
    [SerializeField] private float comboCooldown = 15f;           // 连携攻击冷却
    [SerializeField] private float requiredCoopMeter = 30f;       // 需要的合作能量

    [Header("连携技配置")]
    [SerializeField] private List<AbilityComboData> combos = new List<AbilityComboData>();

    [Header("特效")]
    [SerializeField] private float slowMotionScale = 0.2f;
    [SerializeField] private float slowMotionDuration = 0.5f;

    // 运行时状态
    private readonly List<AbilityRecord> recentAbilities = new List<AbilityRecord>();
    private float lastComboTime = -999f;
    private bool comboInProgress;

    // 统计
    private int totalCombosTriggered;
    private Dictionary<string, int> comboUsageCount = new Dictionary<string, int>();

    public int TotalCombos => totalCombosTriggered;
    public bool IsOnCooldown => Time.time - lastComboTime < comboCooldown;
    public float CooldownRemaining => Mathf.Max(0, comboCooldown - (Time.time - lastComboTime));

    public event System.Action<AbilityComboData> OnComboTriggered;
    public event System.Action<AbilityComboData> OnComboReady;   // 首个技能命中后提示可连携

    [System.Serializable]
    public class AbilityComboData
    {
        public string comboId;
        public string nameKey;                   // 本地化
        public string descriptionKey;

        [Header("触发条件")]
        public string luxAbilityId;              // Lux需要使用的技能
        public string noxAbilityId;              // Nox需要使用的技能
        public bool orderMatters;                // 是否需要特定顺序

        [Header("效果")]
        public ComboEffectType effectType;
        public float damage = 50f;
        public float radius = 5f;
        public float duration = 2f;
        public float coopMeterCost = 30f;

        [Header("视觉")]
        public string vfxKey;
        public string sfxKey;
        public AudioClip comboSound;
        public Color comboColor = Color.white;
    }

    public enum ComboEffectType
    {
        LightDarkExplosion,    // 光暗爆发 - 大范围伤害
        PrismShield,           // 棱镜护盾 - 双人无敌+反弹
        TwilightBeam,          // 黄昏光线 - 穿透射线
        ShadowLightWave,       // 暗影光波 - 击飞所有敌人
        HarmonyRestore,        // 和谐恢复 - 双人全回复
        EclipseField           // 日蚀领域 - 时间减速区域
    }

    private struct AbilityRecord
    {
        public int playerIndex;        // 0=Lux, 1=Nox
        public string abilityId;
        public float timestamp;
        public Vector3 position;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        InitializeDefaultCombos();
    }

    void Start()
    {
        EventBus.Subscribe<AbilityUsedEvent>(OnAbilityUsed);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<AbilityUsedEvent>(OnAbilityUsed);
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // 清理过期记录
        float cutoff = Time.time - comboWindowDuration;
        recentAbilities.RemoveAll(r => r.timestamp < cutoff);
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 手动记录技能使用（供PlayerController调用）
    /// </summary>
    public void RecordAbility(int playerIndex, string abilityId, Vector3 position)
    {
        if (comboInProgress) return;

        recentAbilities.Add(new AbilityRecord
        {
            playerIndex = playerIndex,
            abilityId = abilityId,
            timestamp = Time.time,
            position = position
        });

        // 检查是否可以触发连携
        CheckForCombo();
    }

    /// <summary>
    /// 获取当前可用的连携技（有足够能量且不在冷却中）
    /// </summary>
    public List<AbilityComboData> GetAvailableCombos()
    {
        var available = new List<AbilityComboData>();
        if (IsOnCooldown) return available;

        float coopMeter = CoopAbilitySystem.Instance?.CurrentMeter ?? 0;

        foreach (var combo in combos)
        {
            if (coopMeter >= combo.coopMeterCost)
                available.Add(combo);
        }
        return available;
    }

    /// <summary>
    /// 获取连携使用统计
    /// </summary>
    public int GetComboUsageCount(string comboId)
    {
        return comboUsageCount.TryGetValue(comboId, out int count) ? count : 0;
    }

    // ==================== 事件处理 ====================

    private void OnAbilityUsed(AbilityUsedEvent e)
    {
        RecordAbility(e.playerIndex, e.abilityName, e.position);
    }

    // ==================== 连携检测 ====================

    private void CheckForCombo()
    {
        if (IsOnCooldown) return;
        if (comboInProgress) return;

        // 需要至少两条记录（一个Lux一个Nox）
        AbilityRecord? luxRecord = null;
        AbilityRecord? noxRecord = null;

        foreach (var record in recentAbilities)
        {
            if (record.playerIndex == 0) luxRecord = record;
            else if (record.playerIndex == 1) noxRecord = record;
        }

        if (!luxRecord.HasValue || !noxRecord.HasValue) return;

        // 查找匹配的连携
        foreach (var combo in combos)
        {
            if (MatchesCombo(combo, luxRecord.Value, noxRecord.Value))
            {
                float coopMeter = CoopAbilitySystem.Instance?.CurrentMeter ?? 0;
                if (coopMeter >= combo.coopMeterCost)
                {
                    StartCoroutine(ExecuteCombo(combo, luxRecord.Value, noxRecord.Value));
                    return;
                }
            }
        }

        // 检查是否有第一个技能命中，提示可连携
        CheckForComboReady();
    }

    private bool MatchesCombo(AbilityComboData combo, AbilityRecord luxRec, AbilityRecord noxRec)
    {
        bool luxMatches = luxRec.abilityId == combo.luxAbilityId;
        bool noxMatches = noxRec.abilityId == combo.noxAbilityId;

        if (!luxMatches || !noxMatches) return false;

        if (combo.orderMatters)
        {
            // Lux必须先于Nox
            return luxRec.timestamp <= noxRec.timestamp;
        }

        return true;
    }

    private void CheckForComboReady()
    {
        if (recentAbilities.Count == 0) return;

        var latest = recentAbilities[recentAbilities.Count - 1];

        foreach (var combo in combos)
        {
            bool isLuxAbility = latest.playerIndex == 0 && latest.abilityId == combo.luxAbilityId;
            bool isNoxAbility = latest.playerIndex == 1 && latest.abilityId == combo.noxAbilityId;

            if (isLuxAbility || isNoxAbility)
            {
                OnComboReady?.Invoke(combo);
                break;
            }
        }
    }

    // ==================== 连携执行 ====================

    private IEnumerator ExecuteCombo(AbilityComboData combo, AbilityRecord luxRec, AbilityRecord noxRec)
    {
        comboInProgress = true;
        lastComboTime = Time.time;
        recentAbilities.Clear();

        // 消耗合作能量
        CoopAbilitySystem.Instance?.SpendMeter(combo.coopMeterCost);

        // 慢动作
        if (CameraEffects.Instance != null)
            CameraEffects.Instance.SlowMotion(slowMotionScale, slowMotionDuration);

        yield return new WaitForSecondsRealtime(slowMotionDuration * 0.5f);

        // 计算中点
        Vector3 midPoint = (luxRec.position + noxRec.position) * 0.5f;

        // 执行效果
        switch (combo.effectType)
        {
            case ComboEffectType.LightDarkExplosion:
                yield return ExecuteExplosion(combo, midPoint);
                break;
            case ComboEffectType.PrismShield:
                yield return ExecuteShield(combo, midPoint);
                break;
            case ComboEffectType.TwilightBeam:
                yield return ExecuteBeam(combo, luxRec.position, noxRec.position);
                break;
            case ComboEffectType.ShadowLightWave:
                yield return ExecuteWave(combo, midPoint);
                break;
            case ComboEffectType.HarmonyRestore:
                yield return ExecuteRestore(combo);
                break;
            case ComboEffectType.EclipseField:
                yield return ExecuteField(combo, midPoint);
                break;
        }

        // 音效
        if (combo.comboSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(combo.comboSound);
        else if (!string.IsNullOrEmpty(combo.sfxKey) && SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(combo.sfxKey);

        // 视觉
        if (!string.IsNullOrEmpty(combo.vfxKey) && VFXManager.Instance != null)
            VFXManager.Instance.Play(combo.vfxKey, midPoint);

        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeHeavy();

        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Heavy();

        // 相机效果
        if (CameraEffects.Instance != null)
            CameraEffects.Instance.ChromaticPulse(1f, 0.5f);

        // 连击加成
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.PerfectAction("ability_combo", 1000);

        // 统计
        totalCombosTriggered++;
        if (!comboUsageCount.ContainsKey(combo.comboId))
            comboUsageCount[combo.comboId] = 0;
        comboUsageCount[combo.comboId]++;

        // 成就
        if (AchievementSystem.Instance != null)
        {
            AchievementSystem.Instance.UpdateProgress("combo_attack", 1);
            if (totalCombosTriggered >= 10)
                AchievementSystem.Instance.Unlock("combo_master_10");
        }

        // 通知
        OnComboTriggered?.Invoke(combo);

        EventBus.Publish(new HintRequestEvent
        {
            textKey = combo.nameKey,
            fallbackText = $"连携技发动！",
            duration = 2f
        });

        Debug.Log($"[AbilityCombo] Triggered: {combo.comboId}");

        comboInProgress = false;
    }

    // ==================== 连携效果 ====================

    private IEnumerator ExecuteExplosion(AbilityComboData combo, Vector3 center)
    {
        // 范围伤害
        var hits = Physics2D.OverlapCircleAll(center, combo.radius);
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponent<EnemyBase>();
            if (enemy != null)
                enemy.TakeDamage(Mathf.CeilToInt(combo.damage), center);
        }
        yield return null;
    }

    private IEnumerator ExecuteShield(AbilityComboData combo, Vector3 center)
    {
        // 通过EventBus通知护盾激活
        EventBus.Publish(new ShieldActivatedEvent
        {
            duration = combo.duration,
            position = center
        });
        yield return new WaitForSeconds(combo.duration);
    }

    private IEnumerator ExecuteBeam(AbilityComboData combo, Vector3 from, Vector3 to)
    {
        // 穿透光线
        Vector2 direction = (to - from).normalized;
        var hits = Physics2D.RaycastAll(from, direction, combo.radius * 3f);
        foreach (var hit in hits)
        {
            var enemy = hit.collider.GetComponent<EnemyBase>();
            if (enemy != null)
                enemy.TakeDamage(Mathf.CeilToInt(combo.damage * 0.8f), hit.point);
        }
        yield return null;
    }

    private IEnumerator ExecuteWave(AbilityComboData combo, Vector3 center)
    {
        // 击飞波
        var hits = Physics2D.OverlapCircleAll(center, combo.radius);
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(Mathf.CeilToInt(combo.damage * 0.5f), center);
                var rb = hit.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Vector2 knockDir = ((Vector2)hit.transform.position - (Vector2)center).normalized;
                    rb.AddForce(knockDir * combo.damage * 10f, ForceMode2D.Impulse);
                }
            }
        }
        yield return null;
    }

    private IEnumerator ExecuteRestore(AbilityComboData combo)
    {
        // 全回复 - 通过事件通知
        EventBus.Publish(new HealAllPlayersEvent
        {
            healAmount = Mathf.CeilToInt(combo.damage),
            source = "harmony_restore"
        });
        yield return null;
    }

    private IEnumerator ExecuteField(AbilityComboData combo, Vector3 center)
    {
        // 时间减速场
        if (TimeManager.Instance != null)
            TimeManager.Instance.SetTimeScale(0.3f);

        // 场内敌人减速
        float elapsed = 0;
        while (elapsed < combo.duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var hits = Physics2D.OverlapCircleAll(center, combo.radius);
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyBase>();
                if (enemy != null)
                    enemy.TakeDamage(1, center); // 持续微弱伤害
            }
            yield return null;
        }

        if (TimeManager.Instance != null)
            TimeManager.Instance.SetTimeScale(1f);
    }

    // ==================== 默认连携 ====================

    private void InitializeDefaultCombos()
    {
        if (combos.Count > 0) return;

        combos.Add(new AbilityComboData
        {
            comboId = "light_dark_explosion",
            nameKey = "combo_explosion",
            descriptionKey = "combo_explosion_desc",
            luxAbilityId = "light_beam",
            noxAbilityId = "shadow_zone",
            effectType = ComboEffectType.LightDarkExplosion,
            damage = 80f, radius = 6f, coopMeterCost = 30f,
            vfxKey = "combo_explosion",
            sfxKey = "combo_explosion",
            comboColor = new Color(1f, 0.8f, 0.3f)
        });

        combos.Add(new AbilityComboData
        {
            comboId = "prism_shield",
            nameKey = "combo_shield",
            descriptionKey = "combo_shield_desc",
            luxAbilityId = "light_bridge",
            noxAbilityId = "shadow_phase",
            effectType = ComboEffectType.PrismShield,
            duration = 5f, coopMeterCost = 40f,
            vfxKey = "combo_shield",
            sfxKey = "combo_shield",
            comboColor = new Color(0.5f, 0.8f, 1f)
        });

        combos.Add(new AbilityComboData
        {
            comboId = "twilight_beam",
            nameKey = "combo_beam",
            descriptionKey = "combo_beam_desc",
            luxAbilityId = "light_beam",
            noxAbilityId = "shadow_phase",
            orderMatters = true,
            effectType = ComboEffectType.TwilightBeam,
            damage = 100f, radius = 10f, coopMeterCost = 35f,
            vfxKey = "combo_beam",
            sfxKey = "combo_beam",
            comboColor = new Color(0.7f, 0.4f, 0.9f)
        });

        combos.Add(new AbilityComboData
        {
            comboId = "shadow_light_wave",
            nameKey = "combo_wave",
            descriptionKey = "combo_wave_desc",
            luxAbilityId = "light_bridge",
            noxAbilityId = "shadow_zone",
            effectType = ComboEffectType.ShadowLightWave,
            damage = 40f, radius = 8f, coopMeterCost = 25f,
            vfxKey = "combo_wave",
            sfxKey = "combo_wave",
            comboColor = new Color(0.3f, 0.3f, 0.8f)
        });

        combos.Add(new AbilityComboData
        {
            comboId = "harmony_restore",
            nameKey = "combo_restore",
            descriptionKey = "combo_restore_desc",
            luxAbilityId = "double_jump",
            noxAbilityId = "dash",
            effectType = ComboEffectType.HarmonyRestore,
            damage = 50f, coopMeterCost = 50f,
            vfxKey = "combo_heal",
            sfxKey = "combo_heal",
            comboColor = new Color(0.4f, 1f, 0.5f)
        });
    }
}

// 事件定义均在 EventBus.cs (Core) 中：
// AbilityUsedEvent, ShieldActivatedEvent, HealAllPlayersEvent
