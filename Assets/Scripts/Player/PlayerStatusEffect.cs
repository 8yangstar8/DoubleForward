using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 玩家状态效果系统 - 管理临时增减益效果
/// 支持速度提升/降低、护盾、中毒、灼烧、冰冻等
/// 效果可叠加，有持续时间，可被清除
/// 与DarknessSystem联动（Nox在暗影中获得增益）
/// </summary>
public class PlayerStatusEffect : MonoBehaviour
{
    [Header("视觉")]
    [SerializeField] private SpriteRenderer playerSprite;
    [SerializeField] private ParticleSystem statusParticles;

    [Header("效果上限")]
    [SerializeField] private int maxActiveEffects = 5;

    // 活跃效果列表
    private List<StatusEffect> activeEffects = new List<StatusEffect>();
    private PlayerController controller;
    private PlayerHealth health;

    // 当前总修正
    private float speedMultiplier = 1f;
    private float damageMultiplier = 1f;
    private float damageTakenMultiplier = 1f;
    private bool isInvulnerable;
    private bool isRooted;

    // 公共属性
    public float SpeedMultiplier => speedMultiplier;
    public float DamageMultiplier => damageMultiplier;
    public float DamageTakenMultiplier => damageTakenMultiplier;
    public bool IsInvulnerable => isInvulnerable;
    public bool IsRooted => isRooted;
    public int ActiveEffectCount => activeEffects.Count;

    public event System.Action<StatusEffect> OnEffectApplied;
    public event System.Action<StatusEffect> OnEffectRemoved;

    [System.Serializable]
    public class StatusEffect
    {
        public string id;
        public EffectType type;
        public float magnitude;
        public float duration;
        public float remainingTime;
        public bool isPermanent;
        public Color tintColor;
        public int stackCount;

        public StatusEffect Clone()
        {
            return new StatusEffect
            {
                id = id, type = type, magnitude = magnitude,
                duration = duration, remainingTime = duration,
                isPermanent = isPermanent, tintColor = tintColor,
                stackCount = stackCount
            };
        }
    }

    public enum EffectType
    {
        SpeedBoost,         // 加速
        SpeedSlow,          // 减速
        DamageBoost,        // 攻击力提升
        DamageReduction,    // 受伤减免
        Shield,             // 护盾（无敌）
        Poison,             // 中毒（持续掉血）
        Burn,               // 灼烧（持续掉血+减速）
        Freeze,             // 冰冻（定身）
        Regeneration,       // 持续回血
        ShadowCloak,        // 暗影隐匿（Nox专属）
        LightAura,          // 光明光环（Lux专属）
        Root                // 定身
    }

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        health = GetComponent<PlayerHealth>();

        if (playerSprite == null)
            playerSprite = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        UpdateEffects();
        RecalculateModifiers();
        ApplyPeriodicEffects();
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 施加状态效果
    /// </summary>
    public bool ApplyEffect(StatusEffect effect)
    {
        if (effect == null) return false;

        // 检查免疫（护盾状态免疫负面效果）
        if (isInvulnerable && IsNegativeEffect(effect.type))
            return false;

        // 检查上限
        if (activeEffects.Count >= maxActiveEffects)
        {
            // 移除最早的非永久效果
            RemoveOldestNonPermanent();
        }

        // 检查同类效果叠加
        var existing = activeEffects.Find(e => e.id == effect.id);
        if (existing != null)
        {
            // 刷新持续时间，叠加层数
            existing.remainingTime = Mathf.Max(existing.remainingTime, effect.duration);
            existing.stackCount = Mathf.Min(existing.stackCount + 1, 5);
            existing.magnitude = effect.magnitude * (1f + existing.stackCount * 0.2f);
            return true;
        }

        var newEffect = effect.Clone();
        newEffect.stackCount = 1;
        activeEffects.Add(newEffect);

        OnEffectApplied?.Invoke(newEffect);

        // 视觉反馈
        ApplyVisualEffect(newEffect);

        // 音效
        if (SoundFeedback.Instance != null)
        {
            if (IsNegativeEffect(effect.type))
                SoundFeedback.Instance.Play("debuff_apply");
            else
                SoundFeedback.Instance.Play("buff_apply");
        }

        return true;
    }

    /// <summary>
    /// 快速施加效果
    /// </summary>
    public void ApplyEffect(EffectType type, float magnitude, float duration, string id = "")
    {
        if (string.IsNullOrEmpty(id))
            id = type.ToString();

        ApplyEffect(new StatusEffect
        {
            id = id,
            type = type,
            magnitude = magnitude,
            duration = duration,
            remainingTime = duration,
            tintColor = GetDefaultColor(type)
        });
    }

    /// <summary>
    /// 移除指定效果
    /// </summary>
    public void RemoveEffect(string effectId)
    {
        var effect = activeEffects.Find(e => e.id == effectId);
        if (effect != null)
        {
            activeEffects.Remove(effect);
            OnEffectRemoved?.Invoke(effect);
        }
    }

    /// <summary>
    /// 移除所有负面效果
    /// </summary>
    public void ClearDebuffs()
    {
        activeEffects.RemoveAll(e =>
        {
            if (IsNegativeEffect(e.type))
            {
                OnEffectRemoved?.Invoke(e);
                return true;
            }
            return false;
        });
    }

    /// <summary>
    /// 清除所有效果
    /// </summary>
    public void ClearAll()
    {
        foreach (var e in activeEffects)
            OnEffectRemoved?.Invoke(e);
        activeEffects.Clear();
    }

    /// <summary>
    /// 检查是否有指定类型的效果
    /// </summary>
    public bool HasEffect(EffectType type)
    {
        return activeEffects.Exists(e => e.type == type);
    }

    /// <summary>
    /// 获取所有活跃效果信息（供UI显示）
    /// </summary>
    public List<StatusEffect> GetActiveEffects()
    {
        return new List<StatusEffect>(activeEffects);
    }

    // ==================== 内部更新 ====================

    private void UpdateEffects()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeEffects[i];
            if (effect.isPermanent) continue;

            effect.remainingTime -= Time.deltaTime;

            if (effect.remainingTime <= 0)
            {
                OnEffectRemoved?.Invoke(effect);
                activeEffects.RemoveAt(i);
            }
        }
    }

    private void RecalculateModifiers()
    {
        speedMultiplier = 1f;
        damageMultiplier = 1f;
        damageTakenMultiplier = 1f;
        isInvulnerable = false;
        isRooted = false;

        foreach (var effect in activeEffects)
        {
            switch (effect.type)
            {
                case EffectType.SpeedBoost:
                    speedMultiplier += effect.magnitude;
                    break;
                case EffectType.SpeedSlow:
                    speedMultiplier -= effect.magnitude;
                    break;
                case EffectType.DamageBoost:
                    damageMultiplier += effect.magnitude;
                    break;
                case EffectType.DamageReduction:
                    damageTakenMultiplier -= effect.magnitude;
                    break;
                case EffectType.Shield:
                    isInvulnerable = true;
                    break;
                case EffectType.Burn:
                    speedMultiplier -= effect.magnitude * 0.3f;
                    break;
                case EffectType.Freeze:
                case EffectType.Root:
                    isRooted = true;
                    speedMultiplier = 0f;
                    break;
                case EffectType.ShadowCloak:
                    speedMultiplier += effect.magnitude * 0.5f;
                    damageTakenMultiplier -= effect.magnitude * 0.3f;
                    break;
                case EffectType.LightAura:
                    damageMultiplier += effect.magnitude * 0.3f;
                    break;
            }
        }

        speedMultiplier = Mathf.Max(0f, speedMultiplier);
        damageTakenMultiplier = Mathf.Max(0.1f, damageTakenMultiplier);
    }

    private void ApplyPeriodicEffects()
    {
        if (health == null) return;

        foreach (var effect in activeEffects)
        {
            switch (effect.type)
            {
                case EffectType.Poison:
                    // 每秒掉血
                    health.TakeDamage(Mathf.CeilToInt(effect.magnitude * Time.deltaTime));
                    break;

                case EffectType.Burn:
                    // 每秒灼烧
                    health.TakeDamage(Mathf.CeilToInt(effect.magnitude * 1.5f * Time.deltaTime));
                    break;

                case EffectType.Regeneration:
                    // 每秒回血
                    health.Heal(Mathf.CeilToInt(effect.magnitude * Time.deltaTime));
                    break;
            }
        }
    }

    // ==================== 视觉 ====================

    private void ApplyVisualEffect(StatusEffect effect)
    {
        if (playerSprite == null) return;

        // 叠加染色
        Color tint = GetDefaultColor(effect.type);
        StartCoroutine(FlashTint(tint, 0.3f));

        // 粒子
        if (statusParticles != null)
        {
            var main = statusParticles.main;
            main.startColor = tint;
            statusParticles.Emit(5);
        }
    }

    private System.Collections.IEnumerator FlashTint(Color tint, float duration)
    {
        if (playerSprite == null) yield break;

        Color original = playerSprite.color;
        playerSprite.color = Color.Lerp(original, tint, 0.5f);

        yield return new WaitForSeconds(duration);

        if (playerSprite != null)
            playerSprite.color = original;
    }

    // ==================== 辅助 ====================

    private bool IsNegativeEffect(EffectType type)
    {
        return type == EffectType.SpeedSlow ||
               type == EffectType.Poison ||
               type == EffectType.Burn ||
               type == EffectType.Freeze ||
               type == EffectType.Root;
    }

    private Color GetDefaultColor(EffectType type)
    {
        switch (type)
        {
            case EffectType.SpeedBoost: return new Color(0.3f, 0.8f, 1f, 0.6f);
            case EffectType.SpeedSlow: return new Color(0.5f, 0.5f, 0.8f, 0.6f);
            case EffectType.DamageBoost: return new Color(1f, 0.4f, 0.2f, 0.6f);
            case EffectType.DamageReduction: return new Color(0.3f, 0.6f, 1f, 0.6f);
            case EffectType.Shield: return new Color(1f, 0.9f, 0.3f, 0.8f);
            case EffectType.Poison: return new Color(0.3f, 0.8f, 0.2f, 0.6f);
            case EffectType.Burn: return new Color(1f, 0.3f, 0.1f, 0.7f);
            case EffectType.Freeze: return new Color(0.5f, 0.8f, 1f, 0.7f);
            case EffectType.Regeneration: return new Color(0.2f, 1f, 0.4f, 0.5f);
            case EffectType.ShadowCloak: return new Color(0.3f, 0.1f, 0.5f, 0.5f);
            case EffectType.LightAura: return new Color(1f, 0.95f, 0.7f, 0.5f);
            case EffectType.Root: return new Color(0.5f, 0.3f, 0.1f, 0.6f);
            default: return Color.white;
        }
    }

    private void RemoveOldestNonPermanent()
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (!activeEffects[i].isPermanent)
            {
                OnEffectRemoved?.Invoke(activeEffects[i]);
                activeEffects.RemoveAt(i);
                return;
            }
        }
    }

    void OnDestroy()
    {
        activeEffects.Clear();
    }
}
