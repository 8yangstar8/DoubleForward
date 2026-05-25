using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float invincibleDuration = 1.5f;
    [SerializeField] private float deathRespawnDelay = 1f;

    public int CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0;
    public bool IsInvincible { get; set; }

    public event System.Action<int> OnHealthChanged;
    public event System.Action OnDeath;
    public event System.Action OnRespawned;

    public int MaxHealth => maxHealth;

    private float invincibleTimer;
    private Vector3 lastCheckpointPosition;
    private PlayerController controller;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        CurrentHealth = maxHealth;
    }

    void Update()
    {
        if (IsInvincible)
        {
            invincibleTimer -= Time.deltaTime;
            if (invincibleTimer <= 0)
                IsInvincible = false;
        }
    }

    public void TakeDamage(int damage = 1)
    {
        if (!IsAlive || IsInvincible) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        OnHealthChanged?.Invoke(CurrentHealth);

        // 发布事件
        EventBus.Publish(new PlayerDamagedEvent
        {
            damage = damage,
            remainingHealth = CurrentHealth,
            playerIndex = controller != null ? controller.PlayerIndex : 0,
            hitDirection = Vector2.zero
        });

        if (CurrentHealth <= 0)
        {
            Die();
        }
        else
        {
            IsInvincible = true;
            invincibleTimer = invincibleDuration;
        }
    }

    /// <summary>
    /// 带击退方向的伤害重载（敌人攻击使用）
    /// </summary>
    public void TakeDamage(float damage, Vector2 knockback)
    {
        if (!IsAlive || IsInvincible) return;

        int intDamage = Mathf.CeilToInt(damage);
        CurrentHealth = Mathf.Max(0, CurrentHealth - intDamage);
        OnHealthChanged?.Invoke(CurrentHealth);

        // 施加击退
        if (knockback != Vector2.zero)
        {
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.AddForce(knockback.normalized * 5f, ForceMode2D.Impulse);
        }

        // 发布事件
        EventBus.Publish(new PlayerDamagedEvent
        {
            damage = intDamage,
            remainingHealth = CurrentHealth,
            playerIndex = controller != null ? controller.PlayerIndex : 0,
            hitDirection = knockback
        });

        if (CurrentHealth <= 0)
        {
            Die();
        }
        else
        {
            IsInvincible = true;
            invincibleTimer = invincibleDuration;
        }
    }

    public void Heal(int amount = 1)
    {
        if (!IsAlive) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth);
    }

    private void Die()
    {
        OnDeath?.Invoke();
        GetComponent<PlayerAnimator>()?.PlayDeath();
        Invoke(nameof(Respawn), deathRespawnDelay);
    }

    private void Respawn()
    {
        CurrentHealth = maxHealth;
        controller.Respawn(lastCheckpointPosition);
        GetComponent<PlayerAnimator>()?.PlayRespawn();

        IsInvincible = true;
        invincibleTimer = invincibleDuration;

        OnHealthChanged?.Invoke(CurrentHealth);
        OnRespawned?.Invoke();
    }

    public void SetCheckpoint(Vector3 position)
    {
        lastCheckpointPosition = position;
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        IsInvincible = false;
        OnHealthChanged?.Invoke(CurrentHealth);
    }
}
