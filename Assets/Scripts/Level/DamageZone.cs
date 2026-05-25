using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 伤害区域 - 持续对区域内玩家造成伤害
/// 支持多种类型：酸液池、熔岩、毒沼、电击区域等
/// 可配置伤害频率、状态效果、视觉反馈
/// 与PlayerStatusEffect联动施加debuff
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DamageZone : MonoBehaviour
{
    [Header("区域类型")]
    [SerializeField] private ZoneType zoneType = ZoneType.Lava;

    [Header("伤害")]
    [SerializeField] private int damagePerTick = 1;
    [SerializeField] private float damageInterval = 0.5f;
    [SerializeField] private bool instantKill = false;

    [Header("状态效果")]
    [SerializeField] private bool applyStatusEffect = true;
    [SerializeField] private float statusEffectDuration = 3f;
    [SerializeField] private float statusEffectMagnitude = 1f;

    [Header("击退")]
    [SerializeField] private bool applyKnockback = true;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private Vector2 knockbackDirection = Vector2.up;

    [Header("视觉")]
    [SerializeField] private ParticleSystem zoneParticles;
    [SerializeField] private ParticleSystem contactParticles;
    [SerializeField] private SpriteRenderer zoneRenderer;
    [SerializeField] private float pulseSpeed = 2f;

    [Header("音效")]
    [SerializeField] private string contactSound = "hazard_contact";
    [SerializeField] private string ambientSound = "";

    [Header("选择性")]
    [SerializeField] private bool affectsLux = true;
    [SerializeField] private bool affectsNox = true;
    [SerializeField] private bool affectsEnemies = false;

    public enum ZoneType
    {
        Lava,           // 熔岩 — 灼烧
        Acid,           // 酸液 — 中毒+减速
        Electric,       // 电击 — 定身
        Ice,            // 冰冻 — 减速+冰冻
        Darkness,       // 黑暗 — 对Lux造成持续伤害
        Light,          // 光明 — 对Nox造成持续伤害
        Poison,         // 毒沼 — 中毒
        Void            // 虚空 — 即死
    }

    private Dictionary<int, float> damageCooldowns = new Dictionary<int, float>();
    private HashSet<int> entitiesInZone = new HashSet<int>();

    void Start()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (zoneType == ZoneType.Void)
            instantKill = true;

        if (zoneParticles != null)
            zoneParticles.Play();

        if (!string.IsNullOrEmpty(ambientSound) && AudioManager.Instance != null)
            AudioManager.Instance.PlayAmbient(ambientSound);
    }

    void Update()
    {
        // 脉冲视觉
        if (zoneRenderer != null)
        {
            Color c = zoneRenderer.color;
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            c.a = Mathf.Lerp(0.3f, 0.6f, pulse);
            zoneRenderer.color = c;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        int id = other.GetInstanceID();
        entitiesInZone.Add(id);

        // 首次接触效果
        var player = other.GetComponent<PlayerController>();
        if (player != null && CanAffectPlayer(player))
        {
            if (contactParticles != null)
            {
                contactParticles.transform.position = other.transform.position;
                contactParticles.Play();
            }

            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play(contactSound);
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        int id = other.GetInstanceID();

        // 检查冷却
        if (damageCooldowns.ContainsKey(id) && Time.time < damageCooldowns[id])
            return;

        // 玩家伤害
        var player = other.GetComponent<PlayerController>();
        if (player != null && CanAffectPlayer(player))
        {
            DealDamageToPlayer(other.gameObject, player);
            damageCooldowns[id] = Time.time + damageInterval;
            return;
        }

        // 敌人伤害
        if (affectsEnemies)
        {
            var enemy = other.GetComponent<EnemyBase>();
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(damagePerTick);
                damageCooldowns[id] = Time.time + damageInterval;
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        int id = other.GetInstanceID();
        entitiesInZone.Remove(id);
        damageCooldowns.Remove(id);
    }

    // ==================== 伤害逻辑 ====================

    private void DealDamageToPlayer(GameObject playerObj, PlayerController controller)
    {
        var health = playerObj.GetComponent<PlayerHealth>();
        if (health == null || !health.IsAlive) return;

        if (instantKill)
        {
            health.TakeDamage(999);
            return;
        }

        // 基础伤害
        health.TakeDamage(damagePerTick);

        // 击退
        if (applyKnockback)
        {
            var rb = playerObj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 knockDir = knockbackDirection.normalized;
                if (knockDir == Vector2.zero)
                    knockDir = (playerObj.transform.position - transform.position).normalized;
                rb.AddForce(knockDir * knockbackForce, ForceMode2D.Impulse);
            }
        }

        // 状态效果
        if (applyStatusEffect)
        {
            var statusEffect = playerObj.GetComponent<PlayerStatusEffect>();
            if (statusEffect != null)
            {
                ApplyZoneStatusEffect(statusEffect);
            }
        }

        // 触觉
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Light();
    }

    private void ApplyZoneStatusEffect(PlayerStatusEffect statusEffect)
    {
        switch (zoneType)
        {
            case ZoneType.Lava:
                statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.Burn,
                    statusEffectMagnitude, statusEffectDuration, "zone_lava");
                break;

            case ZoneType.Acid:
            case ZoneType.Poison:
                statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.Poison,
                    statusEffectMagnitude, statusEffectDuration, "zone_poison");
                statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.SpeedSlow,
                    0.3f, statusEffectDuration, "zone_acid_slow");
                break;

            case ZoneType.Electric:
                statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.Root,
                    1f, 0.5f, "zone_electric_stun");
                break;

            case ZoneType.Ice:
                statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.SpeedSlow,
                    0.5f, statusEffectDuration, "zone_ice_slow");
                if (Random.value < 0.2f) // 20%几率冰冻
                    statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.Freeze,
                        1f, 1f, "zone_freeze");
                break;

            case ZoneType.Darkness:
                statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.SpeedSlow,
                    0.2f, statusEffectDuration, "zone_dark_slow");
                break;

            case ZoneType.Light:
                statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.SpeedSlow,
                    0.2f, statusEffectDuration, "zone_light_slow");
                break;
        }
    }

    private bool CanAffectPlayer(PlayerController player)
    {
        if (player.Type == PlayerController.PlayerType.Lux && !affectsLux) return false;
        if (player.Type == PlayerController.PlayerType.Nox && !affectsNox) return false;

        // 光暗区域的角色免疫
        if (zoneType == ZoneType.Light && player.Type == PlayerController.PlayerType.Lux) return false;
        if (zoneType == ZoneType.Darkness && player.Type == PlayerController.PlayerType.Nox) return false;

        return true;
    }

    // ==================== Gizmos ====================

    void OnDrawGizmos()
    {
        Color gizmoColor = zoneType switch
        {
            ZoneType.Lava => new Color(1f, 0.3f, 0f, 0.3f),
            ZoneType.Acid => new Color(0.3f, 0.9f, 0.1f, 0.3f),
            ZoneType.Electric => new Color(1f, 1f, 0.2f, 0.3f),
            ZoneType.Ice => new Color(0.5f, 0.8f, 1f, 0.3f),
            ZoneType.Darkness => new Color(0.2f, 0f, 0.3f, 0.3f),
            ZoneType.Light => new Color(1f, 1f, 0.7f, 0.3f),
            ZoneType.Poison => new Color(0.5f, 0.1f, 0.6f, 0.3f),
            ZoneType.Void => new Color(0f, 0f, 0f, 0.5f),
            _ => new Color(1f, 0f, 0f, 0.3f)
        };

        Gizmos.color = gizmoColor;
        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Vector3 center = transform.position + (Vector3)col.offset;
            Vector3 size = Vector3.Scale(col.size, transform.lossyScale);
            Gizmos.DrawCube(center, size);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1f,
            $"⚠ {zoneType} ({damagePerTick}dmg/{damageInterval}s)");
#endif
    }
}
