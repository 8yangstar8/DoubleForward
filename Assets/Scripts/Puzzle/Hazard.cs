using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Hazard : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private bool instantKill;

    void OnTriggerEnter2D(Collider2D other)
    {
        DealDamage(other);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        DealDamage(collision.collider);
    }

    private void DealDamage(Collider2D target)
    {
        var health = target.GetComponent<PlayerHealth>();
        if (health == null || !health.IsAlive || health.IsInvincible) return;

        if (instantKill)
        {
            health.TakeDamage(999);
        }
        else
        {
            health.TakeDamage(damage);

            var rb = target.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 knockback = (target.transform.position - transform.position).normalized;
                knockback.y = Mathf.Max(knockback.y, 0.5f);
                rb.AddForce(knockback * knockbackForce, ForceMode2D.Impulse);
            }
        }
    }
}
