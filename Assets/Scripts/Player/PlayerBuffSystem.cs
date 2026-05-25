using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 玩家增益/减益系统 - 管理所有临时状态修正
/// 支持叠加、刷新、优先级、可视化
/// 增益来源：道具拾取、合作技能、Boss战机制、谜题奖励
/// </summary>
public class PlayerBuffSystem : MonoBehaviour
{
    [Header("最大增益数量")]
    [SerializeField] private int maxActiveBuffs = 8;

    // 活跃增益列表
    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
    private PlayerController controller;
    private PlayerHealth health;
    private PlayerCombat combat;
    private Rigidbody2D rb;

    // 缓存的总修正值
    private float cachedSpeedMultiplier = 1f;
    private float cachedJumpMultiplier = 1f;
    private float cachedDamageMultiplier = 1f;
    private float cachedDamageReduction = 0f;
    private bool cachedInvincible = false;

    public float SpeedMultiplier => cachedSpeedMultiplier;
    public float JumpMultiplier => cachedJumpMultiplier;
    public float DamageMultiplier => cachedDamageMultiplier;
    public float DamageReduction => cachedDamageReduction;
    public bool IsBuffInvincible => cachedInvincible;

    public IReadOnlyList<ActiveBuff> Buffs => activeBuffs;

    public event System.Action<ActiveBuff> OnBuffAdded;
    public event System.Action<ActiveBuff> OnBuffRemoved;
    public event System.Action OnBuffsChanged;

    // ==================== 增益定义 ====================

    public enum BuffType
    {
        SpeedBoost,         // 移动加速
        JumpBoost,          // 跳跃增强
        DamageBoost,        // 攻击力提升
        Shield,             // 减伤护盾
        Invincibility,      // 无敌
        Magnetism,          // 收集品吸引
        DoubleJump,         // 二段跳
        SlowFall,           // 缓降
        Regeneration,       // 生命回复
        SpeedDown,          // 减速（debuff）
        DamageDown,         // 攻击力降低（debuff）
        Poisoned,           // 中毒持续掉血（debuff）
        Frozen,             // 冰冻无法移动（debuff）
        Burning,            // 燃烧持续伤害（debuff）
    }

    public enum BuffSource
    {
        Pickup,             // 场景拾取
        CoopAbility,        // 合作技能
        BossMechanic,       // Boss战机制
        Environment,        // 环境效果
        Puzzle,             // 谜题奖励
        Skill,              // 技能树被动
    }

    [System.Serializable]
    public class ActiveBuff
    {
        public BuffType type;
        public BuffSource source;
        public float duration;       // 总持续时间
        public float remaining;      // 剩余时间
        public float magnitude;      // 效果强度
        public bool stackable;       // 是否可叠加
        public int stacks;           // 当前层数
        public int maxStacks;        // 最大层数
        public Sprite icon;          // UI图标
        public string displayName;   // 显示名称

        public float NormalizedTime => duration > 0 ? remaining / duration : 0f;
        public bool IsDebuff => type >= BuffType.SpeedDown;
    }

    // ==================== 生命周期 ====================

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        health = GetComponent<PlayerHealth>();
        combat = GetComponent<PlayerCombat>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        UpdateBuffTimers();
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 添加增益
    /// </summary>
    public void AddBuff(BuffType type, float duration, float magnitude = 1f,
        BuffSource source = BuffSource.Pickup, bool stackable = false, int maxStacks = 3,
        Sprite icon = null, string displayName = null)
    {
        // 检查是否已有该增益
        var existing = activeBuffs.Find(b => b.type == type);

        if (existing != null)
        {
            if (stackable && existing.stacks < existing.maxStacks)
            {
                // 叠加
                existing.stacks++;
                existing.remaining = duration; // 刷新时间
                existing.magnitude = magnitude * existing.stacks;
                RecalculateModifiers();
                OnBuffsChanged?.Invoke();
                return;
            }
            else if (!stackable)
            {
                // 刷新时间
                existing.remaining = Mathf.Max(existing.remaining, duration);
                existing.magnitude = Mathf.Max(existing.magnitude, magnitude);
                RecalculateModifiers();
                OnBuffsChanged?.Invoke();
                return;
            }
        }

        // 容量检查
        if (activeBuffs.Count >= maxActiveBuffs)
        {
            // 移除最短持续时间的增益
            activeBuffs.Sort((a, b) => a.remaining.CompareTo(b.remaining));
            RemoveBuff(activeBuffs[0]);
        }

        // 创建新增益
        var buff = new ActiveBuff
        {
            type = type,
            source = source,
            duration = duration,
            remaining = duration,
            magnitude = magnitude,
            stackable = stackable,
            stacks = 1,
            maxStacks = maxStacks,
            icon = icon,
            displayName = displayName ?? GetDefaultName(type)
        };

        activeBuffs.Add(buff);
        ApplyBuffEffect(buff, true);
        RecalculateModifiers();

        OnBuffAdded?.Invoke(buff);
        OnBuffsChanged?.Invoke();

        // 视觉效果
        if (VFXManager.Instance != null)
        {
            string vfx = buff.IsDebuff ? VFXManager.Effects.DebuffApply : VFXManager.Effects.BuffApply;
            VFXManager.Instance.Play(vfx, transform.position);
        }
    }

    /// <summary>
    /// 便捷方法：添加常用增益
    /// </summary>
    public void AddSpeedBoost(float duration = 5f, float multiplier = 1.5f)
        => AddBuff(BuffType.SpeedBoost, duration, multiplier);

    public void AddDamageBoost(float duration = 5f, float multiplier = 2f)
        => AddBuff(BuffType.DamageBoost, duration, multiplier);

    public void AddShield(float duration = 8f, float reduction = 0.5f)
        => AddBuff(BuffType.Shield, duration, reduction);

    public void AddInvincibility(float duration = 3f)
        => AddBuff(BuffType.Invincibility, duration, 1f);

    public void AddMagnetism(float duration = 10f, float range = 5f)
        => AddBuff(BuffType.Magnetism, duration, range);

    public void AddRegeneration(float duration = 10f, float healPerSecond = 0.5f)
        => AddBuff(BuffType.Regeneration, duration, healPerSecond);

    /// <summary>
    /// 移除指定类型的增益
    /// </summary>
    public void RemoveBuffByType(BuffType type)
    {
        var buff = activeBuffs.Find(b => b.type == type);
        if (buff != null) RemoveBuff(buff);
    }

    /// <summary>
    /// 清除所有减益
    /// </summary>
    public void ClearDebuffs()
    {
        var debuffs = activeBuffs.FindAll(b => b.IsDebuff);
        foreach (var debuff in debuffs)
            RemoveBuff(debuff);
    }

    /// <summary>
    /// 清除所有增益（死亡时调用）
    /// </summary>
    public void ClearAllBuffs()
    {
        while (activeBuffs.Count > 0)
            RemoveBuff(activeBuffs[0]);
    }

    /// <summary>
    /// 检查是否有指定增益
    /// </summary>
    public bool HasBuff(BuffType type)
    {
        return activeBuffs.Exists(b => b.type == type);
    }

    /// <summary>
    /// 获取收集品磁铁范围
    /// </summary>
    public float GetMagnetRange()
    {
        var mag = activeBuffs.Find(b => b.type == BuffType.Magnetism);
        return mag != null ? mag.magnitude : 0f;
    }

    // ==================== 内部逻辑 ====================

    private void UpdateBuffTimers()
    {
        bool anyExpired = false;

        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            var buff = activeBuffs[i];
            buff.remaining -= Time.deltaTime;

            // 持续效果 tick
            ApplyTickEffect(buff);

            if (buff.remaining <= 0)
            {
                RemoveBuff(buff);
                anyExpired = true;
            }
        }

        if (anyExpired)
            RecalculateModifiers();
    }

    private void RemoveBuff(ActiveBuff buff)
    {
        ApplyBuffEffect(buff, false);
        activeBuffs.Remove(buff);
        OnBuffRemoved?.Invoke(buff);
        OnBuffsChanged?.Invoke();
        RecalculateModifiers();
    }

    private void ApplyBuffEffect(ActiveBuff buff, bool applying)
    {
        switch (buff.type)
        {
            case BuffType.Invincibility:
                if (health != null)
                    health.SetInvincible(applying);
                break;

            case BuffType.Frozen:
                if (controller != null)
                    controller.SetFrozen(applying);
                break;
        }
    }

    private void ApplyTickEffect(ActiveBuff buff)
    {
        switch (buff.type)
        {
            case BuffType.Regeneration:
                // 每秒恢复生命
                if (health != null && health.IsAlive)
                {
                    float healAmount = buff.magnitude * Time.deltaTime;
                    // 积累到1再回复
                    if (Random.value < healAmount)
                        health.Heal(1);
                }
                break;

            case BuffType.Poisoned:
            case BuffType.Burning:
                // 每秒造成伤害
                if (health != null && health.IsAlive)
                {
                    float dmgChance = buff.magnitude * Time.deltaTime;
                    if (Random.value < dmgChance)
                        health.TakeDamage(1);
                }
                break;
        }
    }

    private void RecalculateModifiers()
    {
        cachedSpeedMultiplier = 1f;
        cachedJumpMultiplier = 1f;
        cachedDamageMultiplier = 1f;
        cachedDamageReduction = 0f;
        cachedInvincible = false;

        foreach (var buff in activeBuffs)
        {
            switch (buff.type)
            {
                case BuffType.SpeedBoost:
                    cachedSpeedMultiplier *= buff.magnitude;
                    break;
                case BuffType.SpeedDown:
                    cachedSpeedMultiplier *= Mathf.Max(0.1f, 1f - buff.magnitude);
                    break;
                case BuffType.JumpBoost:
                    cachedJumpMultiplier *= buff.magnitude;
                    break;
                case BuffType.DamageBoost:
                    cachedDamageMultiplier *= buff.magnitude;
                    break;
                case BuffType.DamageDown:
                    cachedDamageMultiplier *= Mathf.Max(0.1f, 1f - buff.magnitude);
                    break;
                case BuffType.Shield:
                    cachedDamageReduction = Mathf.Min(cachedDamageReduction + buff.magnitude, 0.9f);
                    break;
                case BuffType.Invincibility:
                    cachedInvincible = true;
                    break;
            }
        }
    }

    private string GetDefaultName(BuffType type)
    {
        return type switch
        {
            BuffType.SpeedBoost => "速度提升",
            BuffType.JumpBoost => "跳跃增强",
            BuffType.DamageBoost => "攻击强化",
            BuffType.Shield => "护盾",
            BuffType.Invincibility => "无敌",
            BuffType.Magnetism => "磁铁",
            BuffType.DoubleJump => "二段跳",
            BuffType.SlowFall => "缓降",
            BuffType.Regeneration => "回复",
            BuffType.SpeedDown => "减速",
            BuffType.DamageDown => "虚弱",
            BuffType.Poisoned => "中毒",
            BuffType.Frozen => "冰冻",
            BuffType.Burning => "燃烧",
            _ => type.ToString()
        };
    }
}
