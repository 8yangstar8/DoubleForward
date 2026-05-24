using UnityEngine;

/// <summary>
/// 死亡区域 - 玩家接触后立即死亡或受伤
/// 用于关卡边界（坠落）、岩浆、毒雾等
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class DeathZone : MonoBehaviour
{
    public enum DeathType
    {
        InstantKill,   // 立即死亡（坠落深渊）
        DamageOverTime, // 持续伤害（岩浆、毒雾）
        ForcedRespawn   // 强制回检查点（不扣血）
    }

    [Header("死亡类型")]
    [SerializeField] private DeathType deathType = DeathType.InstantKill;

    [Header("持续伤害设置")]
    [SerializeField] private int damagePerTick = 1;
    [SerializeField] private float tickInterval = 0.5f;

    [Header("效果")]
    [SerializeField] private string deathSoundKey = "player_death";
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.3f);

    [Header("强制重生")]
    [SerializeField] private Transform customRespawnPoint;

    private float tickTimer;

    void Awake()
    {
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        switch (deathType)
        {
            case DeathType.InstantKill:
                KillPlayer(other);
                break;
            case DeathType.ForcedRespawn:
                ForceRespawn(other);
                break;
            case DeathType.DamageOverTime:
                // 第一次接触立即伤害
                DamagePlayer(other);
                tickTimer = tickInterval;
                break;
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (deathType != DeathType.DamageOverTime) return;
        if (!other.CompareTag("Player")) return;

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0)
        {
            tickTimer = tickInterval;
            DamagePlayer(other);
        }
    }

    private void KillPlayer(Collider2D playerCol)
    {
        var health = playerCol.GetComponent<PlayerHealth>();
        if (health == null || !health.IsAlive) return;

        // 播放效果
        SpawnDeathEffect(playerCol.transform.position);
        AudioManager.Instance?.PlaySFX(deathSoundKey);

        // 直接设置生命为0（绕过无敌状态）
        health.TakeDamage(999);

        // 发布死亡事件
        var controller = playerCol.GetComponent<PlayerController>();
        if (controller != null)
        {
            EventBus.Publish(new PlayerDeathEvent
            {
                playerIndex = controller.PlayerIndex,
                deathPosition = playerCol.transform.position
            });
        }
    }

    private void DamagePlayer(Collider2D playerCol)
    {
        var health = playerCol.GetComponent<PlayerHealth>();
        if (health == null || !health.IsAlive) return;

        health.TakeDamage(damagePerTick);
    }

    private void ForceRespawn(Collider2D playerCol)
    {
        var controller = playerCol.GetComponent<PlayerController>();
        if (controller == null) return;

        Vector3 respawnPos;
        if (customRespawnPoint != null)
        {
            respawnPos = customRespawnPoint.position;
        }
        else
        {
            // 使用PlayerHealth中的检查点
            var health = playerCol.GetComponent<PlayerHealth>();
            if (health != null)
            {
                // 不扣血，只回位
                controller.Respawn(controller.transform.position); // 临时回位
            }
            return;
        }

        controller.Respawn(respawnPos);
        SpawnDeathEffect(playerCol.transform.position);
    }

    private void SpawnDeathEffect(Vector3 position)
    {
        if (deathEffectPrefab != null)
        {
            var effect = Instantiate(deathEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 3f);
        }
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Gizmos.color = gizmoColor;
        Vector3 center = transform.position + (Vector3)col.offset;
        Vector3 size = Vector3.Scale(col.size, transform.lossyScale);
        Gizmos.DrawCube(center, size);

        // 边框
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.9f);
        Gizmos.DrawWireCube(center, size);

        // 显示类型图标
        switch (deathType)
        {
            case DeathType.InstantKill:
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(center, 0.5f);
                break;
            case DeathType.DamageOverTime:
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(center, 0.3f);
                break;
            case DeathType.ForcedRespawn:
                Gizmos.color = Color.cyan;
                if (customRespawnPoint != null)
                    Gizmos.DrawLine(center, customRespawnPoint.position);
                break;
        }
    }
}
