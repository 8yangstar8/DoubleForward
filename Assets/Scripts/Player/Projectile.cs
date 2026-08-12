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
        float step = speed * Time.deltaTime;

        // 扫掠检测: 只靠Translate+OnTriggerEnter2D,低帧率下一帧位移好几个单位就会
        // 直接穿过目标(批处理里实测一帧可长达0.3秒,中端安卓掉帧时同理)。
        // 先沿本帧位移扫一遍,命中就在命中点结算
        if (step > 0f)
        {
            foreach (var hit in Physics2D.RaycastAll(transform.position, Vector2.right * direction, step))
            {
                if (hit.collider == null || hit.collider.gameObject == gameObject) continue;
                // 扫掠必须遵守层碰撞矩阵,否则会打到触发器路径本来会忽略的东西
                // (例如射手自己 —— Player x PlayerBullet 是设为忽略的)
                if (Physics2D.GetIgnoreLayerCollision(gameObject.layer, hit.collider.gameObject.layer))
                    continue;
                if (TryHandleHit(hit.collider)) return;
            }
        }

        transform.Translate(Vector3.right * direction * speed * Time.deltaTime);

        timer -= Time.deltaTime;
        if (timer <= 0)
            DestroyProjectile();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryHandleHit(other);
    }

    /// <summary>命中结算。返回true表示这一发已消耗掉</summary>
    private bool TryHandleHit(Collider2D other)
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
            return true;
        }

        // 可破坏物
        var breakable = other.GetComponent<Breakable>();
        if (breakable != null)
        {
            breakable.TakeDamage(damage, "");
            SpawnHitEffect();
            DestroyProjectile();
            return true;
        }

        // 撞墙
        if (((1 << other.gameObject.layer) & hitLayers) != 0)
        {
            SpawnHitEffect();
            DestroyProjectile();
            return true;
        }

        return false;
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
