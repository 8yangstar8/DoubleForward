using UnityEngine;
using System.Collections;

/// <summary>
/// 增益道具拾取 - 玩家触碰后获得临时增益效果
/// 支持多种道具类型：加速、护盾、回血、攻击增强等
/// 可配置为单人或双人均有效
/// 使用ObjectPool回收
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class PowerUpPickup : MonoBehaviour
{
    [Header("道具类型")]
    [SerializeField] private PowerUpType powerUpType = PowerUpType.SpeedBoost;
    [SerializeField] private float effectMagnitude = 0.5f;
    [SerializeField] private float effectDuration = 10f;

    [Header("拾取规则")]
    [SerializeField] private bool affectBothPlayers = false;
    [SerializeField] private bool singleUse = true;
    [SerializeField] private float respawnTime = 30f;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private ParticleSystem idleParticles;
    [SerializeField] private ParticleSystem pickupParticles;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobAmount = 0.2f;
    [SerializeField] private float rotateSpeed = 45f;

    [Header("光环")]
    [SerializeField] private SpriteRenderer glowRenderer;
    [SerializeField] private float glowPulseSpeed = 2f;

    [Header("音效")]
    [SerializeField] private string pickupSound = "powerup_pickup";

    public enum PowerUpType
    {
        SpeedBoost,         // 加速
        DamageBoost,        // 攻击力提升
        Shield,             // 护盾（短暂无敌）
        HealthRestore,      // 回血（非状态效果）
        Regeneration,       // 持续回血
        DoubleJump,         // 额外跳跃（功能性）
        Magnet,             // 收集品磁铁
        ScoreMultiplier,    // 分数倍率
        LightBoost,         // Lux光源增强
        ShadowBoost         // Nox暗影增强
    }

    private Vector3 startPosition;
    private bool isCollected;
    private Collider2D col;

    void Awake()
    {
        col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
    }

    void Start()
    {
        startPosition = transform.position;

        if (idleParticles != null)
            idleParticles.Play();

        // 设置颜色
        if (iconRenderer != null)
            iconRenderer.color = GetPowerUpColor();
        if (glowRenderer != null)
            glowRenderer.color = GetPowerUpColor() * 0.5f;
    }

    void Update()
    {
        if (isCollected) return;

        // 上下浮动
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        transform.position = startPosition + Vector3.up * bob;

        // 缓慢旋转
        if (iconRenderer != null)
            iconRenderer.transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);

        // 光环脉冲
        if (glowRenderer != null)
        {
            float pulse = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;
            Color c = glowRenderer.color;
            c.a = Mathf.Lerp(0.2f, 0.6f, pulse);
            glowRenderer.color = c;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        Collect(player);
    }

    // ==================== 拾取逻辑 ====================

    private void Collect(PlayerController player)
    {
        isCollected = true;

        // 施加效果
        if (affectBothPlayers)
        {
            var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var p in allPlayers)
                ApplyEffectToPlayer(p);
        }
        else
        {
            ApplyEffectToPlayer(player);
        }

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(pickupSound);

        // 触觉
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Light();

        // 拾取粒子
        if (pickupParticles != null)
        {
            pickupParticles.transform.SetParent(null);
            pickupParticles.Play();
            Destroy(pickupParticles.gameObject, 2f);
        }

        // VFX
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.Collect, transform.position);

        // 隐藏
        if (iconRenderer != null) iconRenderer.enabled = false;
        if (glowRenderer != null) glowRenderer.enabled = false;
        if (idleParticles != null) idleParticles.Stop();
        col.enabled = false;

        // 重生或销毁
        if (singleUse)
        {
            Destroy(gameObject, 0.5f);
        }
        else
        {
            StartCoroutine(RespawnAfterDelay());
        }
    }

    private void ApplyEffectToPlayer(PlayerController player)
    {
        var statusEffect = player.GetComponent<PlayerStatusEffect>();
        var health = player.GetComponent<PlayerHealth>();

        switch (powerUpType)
        {
            case PowerUpType.HealthRestore:
                // 直接回血，不是状态效果
                if (health != null)
                    health.Heal(Mathf.CeilToInt(effectMagnitude));
                break;

            case PowerUpType.SpeedBoost:
                if (statusEffect != null)
                    statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.SpeedBoost,
                        effectMagnitude, effectDuration, "powerup_speed");
                break;

            case PowerUpType.DamageBoost:
                if (statusEffect != null)
                    statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.DamageBoost,
                        effectMagnitude, effectDuration, "powerup_damage");
                break;

            case PowerUpType.Shield:
                if (statusEffect != null)
                    statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.Shield,
                        1f, effectDuration, "powerup_shield");
                break;

            case PowerUpType.Regeneration:
                if (statusEffect != null)
                    statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.Regeneration,
                        effectMagnitude, effectDuration, "powerup_regen");
                break;

            case PowerUpType.ScoreMultiplier:
                if (ScoreManager.Instance != null)
                    ScoreManager.Instance.SetTemporaryMultiplier(effectMagnitude, effectDuration);
                break;

            case PowerUpType.LightBoost:
                if (DarknessSystem.Instance != null && player.Type == PlayerController.PlayerType.Lux)
                    DarknessSystem.Instance.BoostLuxLight(effectMagnitude * 3f, effectDuration);
                if (statusEffect != null)
                    statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.LightAura,
                        effectMagnitude, effectDuration, "powerup_light");
                break;

            case PowerUpType.ShadowBoost:
                if (statusEffect != null && player.Type == PlayerController.PlayerType.Nox)
                    statusEffect.ApplyEffect(PlayerStatusEffect.EffectType.ShadowCloak,
                        effectMagnitude, effectDuration, "powerup_shadow");
                break;
        }
    }

    // ==================== 重生 ====================

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnTime);

        isCollected = false;
        if (iconRenderer != null) iconRenderer.enabled = true;
        if (glowRenderer != null) glowRenderer.enabled = true;
        if (idleParticles != null) idleParticles.Play();
        col.enabled = true;
    }

    // ==================== 颜色映射 ====================

    private Color GetPowerUpColor()
    {
        return powerUpType switch
        {
            PowerUpType.SpeedBoost => new Color(0.3f, 0.8f, 1f),
            PowerUpType.DamageBoost => new Color(1f, 0.4f, 0.2f),
            PowerUpType.Shield => new Color(1f, 0.9f, 0.3f),
            PowerUpType.HealthRestore => new Color(0.2f, 1f, 0.4f),
            PowerUpType.Regeneration => new Color(0.4f, 1f, 0.5f),
            PowerUpType.DoubleJump => new Color(0.6f, 0.4f, 1f),
            PowerUpType.Magnet => new Color(0.8f, 0.3f, 0.8f),
            PowerUpType.ScoreMultiplier => new Color(1f, 0.85f, 0.2f),
            PowerUpType.LightBoost => new Color(1f, 0.95f, 0.7f),
            PowerUpType.ShadowBoost => new Color(0.4f, 0.1f, 0.8f),
            _ => Color.white
        };
    }

    void OnDrawGizmos()
    {
        Gizmos.color = GetPowerUpColor();
        Gizmos.DrawWireSphere(transform.position, 0.4f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f,
            powerUpType.ToString());
#endif
    }
}
