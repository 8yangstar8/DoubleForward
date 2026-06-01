using UnityEngine;

/// <summary>
/// 子弹/投射物组件 - 玩家光弹和敌人弹射物
/// 必须独立成文件（Unity要求MonoBehaviour在同名文件中才能正确序列化为组件）
/// </summary>
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private GameObject hitEffectPrefab;

    private float direction;
    private float speed;
    private int damage;
    private int ownerPlayerIndex;
    private float timer;

    public void Initialize(float dir, float spd, int dmg, int playerIdx)
    {
        direction = dir;
        speed = spd;
        damage = dmg;
        ownerPlayerIndex = playerIdx;
        timer = lifetime;

        // 翻转Sprite
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.flipX = dir < 0;
    }

    void Update()
    {
        transform.Translate(Vector3.right * direction * speed * Time.deltaTime);

        timer -= Time.deltaTime;
        if (timer <= 0)
            DestroyProjectile();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 检查是否命中敌人
        var enemy = other.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);

            EventBus.Publish(new EnemyHitEvent
            {
                playerIndex = ownerPlayerIndex,
                damage = damage,
                position = other.transform.position
            });

            if (ComboSystem.Instance != null)
                ComboSystem.Instance.OnHit();

            SpawnHitEffect();
            DestroyProjectile();
            return;
        }

        // 可破坏物
        var breakable = other.GetComponent<Breakable>();
        if (breakable != null)
        {
            breakable.TakeDamage(damage, "");
            SpawnHitEffect();
            DestroyProjectile();
            return;
        }

        // 撞墙
        if (((1 << other.gameObject.layer) & hitLayers) != 0)
        {
            SpawnHitEffect();
            DestroyProjectile();
        }
    }

    private void SpawnHitEffect()
    {
        if (hitEffectPrefab == null) return;

        if (ObjectPool.Instance != null)
            ObjectPool.Instance.Get(hitEffectPrefab, transform.position, Quaternion.identity);
        else
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
    }

    private void DestroyProjectile()
    {
        if (ObjectPool.Instance != null)
            ObjectPool.Instance.Return(gameObject);
        else
            Destroy(gameObject);
    }
}
