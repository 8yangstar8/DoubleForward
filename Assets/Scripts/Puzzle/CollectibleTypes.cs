using UnityEngine;

/// <summary>
/// 收集品类型 - 扩展基础Collectible，增加不同种类的拾取物
/// 金币、宝石、生命恢复、临时增益、隐藏道具
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class CollectibleTypes : MonoBehaviour
{
    public enum CollectibleType
    {
        Coin,           // 金币
        Gem,            // 宝石（稀有）
        HealthPickup,   // 生命恢复
        SpeedBoost,     // 速度增益
        ShieldPickup,   // 护盾
        ScoreMultiplier,// 分数倍率
        SecretItem,     // 隐藏道具
        KeyItem         // 钥匙道具（解锁特定门）
    }

    [Header("类型")]
    [SerializeField] private CollectibleType type = CollectibleType.Coin;

    [Header("数值")]
    [SerializeField] private int coinValue = 1;
    [SerializeField] private int gemValue = 1;
    [SerializeField] private int healAmount = 1;
    [SerializeField] private float boostDuration = 5f;
    [SerializeField] private float speedMultiplier = 1.5f;
    [SerializeField] private float scoreMultiplier = 2f;
    [SerializeField] private string keyId = "";

    [Header("视觉")]
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.3f;
    [SerializeField] private float rotateSpeed = 90f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private GameObject sparkleEffect;
    [SerializeField] private GameObject collectEffectPrefab;

    [Header("稀有度光环")]
    [SerializeField] private Color commonGlow = new Color(1f, 0.9f, 0f);
    [SerializeField] private Color rareGlow = new Color(0f, 0.8f, 1f);
    [SerializeField] private Color legendaryGlow = new Color(1f, 0.3f, 1f);

    [Header("音效")]
    [SerializeField] private string collectSoundKey = "coin_collect";

    [Header("磁吸")]
    [SerializeField] private bool magnetEnabled = false;
    [SerializeField] private float magnetRange = 3f;
    [SerializeField] private float magnetSpeed = 8f;

    private Vector3 startPos;
    private bool isCollected;
    private Transform magnetTarget;

    void Start()
    {
        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        startPos = transform.position;

        // 根据类型设置音效键
        switch (type)
        {
            case CollectibleType.Coin:
                collectSoundKey = "coin_collect";
                break;
            case CollectibleType.Gem:
                collectSoundKey = "gem_collect";
                magnetEnabled = false; // 宝石不吸附
                break;
            case CollectibleType.HealthPickup:
                collectSoundKey = "heal_collect";
                break;
            case CollectibleType.SecretItem:
                collectSoundKey = "secret_found";
                break;
        }

        // 稀有度光效
        if (sparkleEffect != null)
        {
            var ps = sparkleEffect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                switch (type)
                {
                    case CollectibleType.Gem:
                    case CollectibleType.ScoreMultiplier:
                        main.startColor = rareGlow;
                        break;
                    case CollectibleType.SecretItem:
                    case CollectibleType.KeyItem:
                        main.startColor = legendaryGlow;
                        break;
                    default:
                        main.startColor = commonGlow;
                        break;
                }
            }
        }
    }

    void Update()
    {
        if (isCollected) return;

        // 悬浮动画
        float yOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        Vector3 targetPos = startPos + Vector3.up * yOffset;

        // 磁吸效果
        if (magnetEnabled && magnetTarget != null)
        {
            float dist = Vector3.Distance(transform.position, magnetTarget.position);
            if (dist < magnetRange)
            {
                float t = 1f - (dist / magnetRange);
                targetPos = Vector3.Lerp(targetPos, magnetTarget.position, t * magnetSpeed * Time.deltaTime);
            }
        }

        transform.position = targetPos;
        transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);

        // 查找最近的玩家作为磁吸目标
        if (magnetEnabled && magnetTarget == null)
        {
            FindMagnetTarget();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;
        if (isCollected) return;

        isCollected = true;
        ApplyEffect(player);

        // 音效
        AudioManager.Instance?.PlaySFX(collectSoundKey);

        // 特效
        if (collectEffectPrefab != null)
        {
            var effect = Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        // 震动反馈
        HapticFeedback.Instance?.PlayPreset(HapticFeedback.HapticPreset.Light);
        CameraShake.Instance?.ShakeLight();

        // 发布事件
        EventBus.Publish(new CollectiblePickedEvent
        {
            collected = 1,
            total = 0,
            position = transform.position
        });

        Destroy(gameObject);
    }

    private void ApplyEffect(PlayerController player)
    {
        switch (type)
        {
            case CollectibleType.Coin:
                CurrencyManager.Instance?.AddCoins(coinValue);
                ScoreManager.Instance?.AddScore(coinValue * 10, "金币");
                break;

            case CollectibleType.Gem:
                CurrencyManager.Instance?.AddGems(gemValue);
                ScoreManager.Instance?.AddScore(gemValue * 50, "宝石");
                break;

            case CollectibleType.HealthPickup:
                var health = player.GetComponent<PlayerHealth>();
                if (health != null)
                    health.Heal(healAmount);
                break;

            case CollectibleType.SpeedBoost:
                // 通过事件通知PlayerController应用速度增益
                // 实际增益由PlayerController处理
                StartCoroutine(TemporaryBoost(player));
                break;

            case CollectibleType.ShieldPickup:
                var playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.IsInvincible = true;
                    // 持续一段时间
                    StartCoroutine(RemoveShield(playerHealth, boostDuration));
                }
                break;

            case CollectibleType.ScoreMultiplier:
                // 通知ComboSystem增加倍率
                if (ComboSystem.Instance != null)
                    ComboSystem.Instance.AddCombo(); // 增加连击
                break;

            case CollectibleType.SecretItem:
                ScoreManager.Instance?.AddScore(1000, "隐藏道具");
                LevelManager.Instance?.CollectItem();
                break;

            case CollectibleType.KeyItem:
                // 存储钥匙状态
                PlayerPrefs.SetInt("Key_" + keyId, 1);
                break;
        }
    }

    private System.Collections.IEnumerator TemporaryBoost(PlayerController player)
    {
        // 速度增益通过事件系统处理，这里只做计时
        yield return new WaitForSeconds(boostDuration);
    }

    private System.Collections.IEnumerator RemoveShield(PlayerHealth health, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (health != null)
            health.IsInvincible = false;
    }

    private void FindMagnetTarget()
    {
        float minDist = magnetRange;
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                magnetTarget = p.transform;
            }
        }
    }

    void OnDrawGizmos()
    {
        Color gizmoColor;
        switch (type)
        {
            case CollectibleType.Coin: gizmoColor = Color.yellow; break;
            case CollectibleType.Gem: gizmoColor = Color.cyan; break;
            case CollectibleType.HealthPickup: gizmoColor = Color.green; break;
            case CollectibleType.SpeedBoost: gizmoColor = Color.blue; break;
            case CollectibleType.ShieldPickup: gizmoColor = Color.white; break;
            case CollectibleType.SecretItem: gizmoColor = Color.magenta; break;
            case CollectibleType.KeyItem: gizmoColor = new Color(1f, 0.5f, 0f); break;
            default: gizmoColor = Color.gray; break;
        }

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, 0.3f);

        if (magnetEnabled)
        {
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.15f);
            Gizmos.DrawWireSphere(transform.position, magnetRange);
        }
    }
}
