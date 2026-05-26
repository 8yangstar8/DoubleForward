using UnityEngine;

/// <summary>
/// 遗物拾取触发器 - 放置在关卡中的遗物物体
/// 玩家触碰后收集遗物，播放收集动画
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class RelicPickup : MonoBehaviour
{
    [Header("遗物配置")]
    [SerializeField] private string relicId;
    [SerializeField] private bool requireBothPlayers;    // 需要两个玩家都在附近

    [Header("视觉")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private SpriteRenderer glowRenderer;
    [SerializeField] private float bobSpeed = 1.5f;
    [SerializeField] private float bobHeight = 0.3f;
    [SerializeField] private float rotateSpeed = 30f;
    [SerializeField] private GameObject collectVFX;

    [Header("提示")]
    [SerializeField] private float detectionRadius = 3f;   // 靠近时显示提示
    [SerializeField] private GameObject promptUI;           // 拾取提示UI

    // 运行时
    private Vector3 startPos;
    private bool collected;
    private bool playerNearby;

    void Start()
    {
        startPos = transform.position;

        var collider = GetComponent<BoxCollider2D>();
        if (collider != null) collider.isTrigger = true;

        // 已收集则隐藏
        if (RelicSystem.Instance != null && RelicSystem.Instance.IsCollected(relicId))
        {
            collected = true;
            gameObject.SetActive(false);
            return;
        }

        // 设置图标
        if (iconRenderer != null && RelicSystem.Instance != null)
        {
            var data = RelicSystem.Instance.GetRelicData(relicId);
            if (data != null && data.icon != null)
                iconRenderer.sprite = data.icon;

            // 根据稀有度设置光晕颜色
            if (glowRenderer != null && data != null)
            {
                glowRenderer.color = GetRarityColor(data.rarity);
            }
        }

        if (promptUI != null)
            promptUI.SetActive(false);
    }

    void Update()
    {
        if (collected) return;

        // 浮动动画
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPos + Vector3.up * bob;

        // 缓慢旋转
        if (iconRenderer != null)
            iconRenderer.transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);

        // 光晕脉冲
        if (glowRenderer != null)
        {
            float pulse = 0.6f + Mathf.Sin(Time.time * 2f) * 0.4f;
            var c = glowRenderer.color;
            c.a = pulse;
            glowRenderer.color = c;
        }

        // 检查玩家距离显示提示
        CheckPlayerProximity();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (requireBothPlayers)
        {
            // 检查两人都在附近
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            int nearCount = 0;
            var col = GetComponent<BoxCollider2D>();

            foreach (var p in players)
            {
                float dist = Vector2.Distance(p.transform.position, transform.position);
                if (dist < detectionRadius)
                    nearCount++;
            }

            if (nearCount < 2) return;
        }

        Collect();
    }

    private void Collect()
    {
        if (collected) return;
        collected = true;

        // 注册到遗物系统
        if (RelicSystem.Instance != null)
            RelicSystem.Instance.CollectRelic(relicId);

        // 收集特效
        if (collectVFX != null)
        {
            var vfx = Instantiate(collectVFX, transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        // 粒子效果
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play("relic_collect", transform.position);

        // 相机效果
        if (CameraEffects.Instance != null)
        {
            CameraEffects.Instance.Shake(0.15f, 0.2f);
            CameraEffects.Instance.ChromaticPulse(0.6f, 0.3f);
        }

        // 隐藏
        if (promptUI != null) promptUI.SetActive(false);
        gameObject.SetActive(false);
    }

    private void CheckPlayerProximity()
    {
        if (promptUI == null) return;

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        bool anyNear = false;

        foreach (var p in players)
        {
            float dist = Vector2.Distance(p.transform.position, transform.position);
            if (dist < detectionRadius)
            {
                anyNear = true;
                break;
            }
        }

        if (anyNear != playerNearby)
        {
            playerNearby = anyNear;
            promptUI.SetActive(anyNear);
        }
    }

    private Color GetRarityColor(RelicSystem.RelicRarity rarity)
    {
        return rarity switch
        {
            RelicSystem.RelicRarity.Common => new Color(0.8f, 0.8f, 0.8f, 0.6f),
            RelicSystem.RelicRarity.Rare => new Color(0.3f, 0.6f, 1f, 0.6f),
            RelicSystem.RelicRarity.Epic => new Color(0.7f, 0.3f, 1f, 0.6f),
            RelicSystem.RelicRarity.Legendary => new Color(1f, 0.8f, 0.2f, 0.7f),
            _ => Color.white
        };
    }
}
