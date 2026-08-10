using UnityEngine;
using System.Collections;

public class NoxAbilities : PlayerAbilityBase
{
    [Header("Shadow Phase")]
    [SerializeField] private float phaseDuration = 0.3f;
    [SerializeField] private float phaseDistance = 4f;
    [SerializeField] private LayerMask shadowWallLayer;

    [Header("Shadow Zone")]
    [SerializeField] private GameObject shadowZonePrefab;
    [SerializeField] private float zoneDuration = 3f;
    [SerializeField] private float zoneRadius = 2f;

    [Header("Shadow Push")]
    [SerializeField] private float pushForce = 8f;
    [SerializeField] private float pushRange = 2f;
    [SerializeField] private LayerMask pushableLayer;

    private PlayerController controller;
    private Collider2D playerCollider;
    private Rigidbody2D rb;
    private bool isPhasing;
    private float phaseSpeed;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        playerCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        abilityName = "Shadow Phase";
    }

    void FixedUpdate()
    {
        if (isPhasing && rb != null)
            rb.velocity = new Vector2(phaseSpeed, 0f);
    }

    protected override void Activate()
    {
        // 发布技能使用事件
        EventBus.Publish(new AbilityUsedEvent
        {
            abilityName = "shadow_phase",
            playerIndex = controller.PlayerIndex,
            position = transform.position
        });

        StartCoroutine(ShadowPhase());
    }

    private IEnumerator ShadowPhase()
    {
        var rb = GetComponent<Rigidbody2D>();
        float dir = controller.IsFacingRight ? 1f : -1f;

        int originalLayer = gameObject.layer;
        gameObject.layer = LayerMask.NameToLayer("PhaseThrough");

        Color originalColor = GetComponent<SpriteRenderer>()?.color ?? Color.white;
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = new Color(0.3f, 0.1f, 0.5f, 0.5f);

        rb.gravityScale = 0f;

        // 冲刺速度改由FixedUpdate持续施加: PlayerController.SetMoveInput每帧Update
        // 会用移动输入覆盖水平速度,而协程的WaitForFixedUpdate在物理步之后才恢复,
        // 只有FixedUpdate能抢在物理模拟前把速度重新写回去
        phaseSpeed = dir * phaseDistance / phaseDuration;
        isPhasing = true;

        yield return new WaitForSeconds(phaseDuration);

        isPhasing = false;

        gameObject.layer = originalLayer;
        rb.gravityScale = 2.5f;

        if (sr != null)
            sr.color = originalColor;

        EndAbility();
    }

    public void CreateShadowZone()
    {
        var zone = shadowZonePrefab != null
            ? Instantiate(shadowZonePrefab, transform.position, Quaternion.identity)
            : CreateFallbackZone(transform.position);
        zone.transform.localScale = Vector3.one * zoneRadius;
        zone.tag = "ShadowZone";

        var trigger = zone.GetComponent<CircleCollider2D>();
        if (trigger == null)
            trigger = zone.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;

        Destroy(zone, zoneDuration);

        // VFX
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.ShadowZone, transform.position);

        // 发布技能事件
        EventBus.Publish(new AbilityUsedEvent
        {
            abilityName = "shadow_zone",
            playerIndex = controller.PlayerIndex,
            position = transform.position
        });
    }

    /// <summary>
    /// shadowZonePrefab 未配置时的运行时兜底阴影区 - 否则技能静默失效
    /// </summary>
    private static GameObject CreateFallbackZone(Vector3 pos)
    {
        var zone = new GameObject("ShadowZone");
        zone.transform.position = pos;
        var sr = zone.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f), 1f);
        sr.color = new Color(0.15f, 0.05f, 0.3f, 0.45f);
        sr.sortingOrder = 3;
        return zone;
    }

    public void ShadowPush()
    {
        float dir = controller.IsFacingRight ? 1f : -1f;
        Vector2 pushOrigin = (Vector2)transform.position + new Vector2(dir * 0.5f, 0);
        // pushableLayer 未配置(m_Bits=0)时会匹配不到任何东西,退回到可推动物体所在的层
        int mask = pushableLayer.value != 0
            ? pushableLayer.value
            : LayerMask.GetMask("Default", "Puzzle", "Interactable");
        var hits = Physics2D.OverlapCircleAll(pushOrigin, pushRange, mask);

        foreach (var hit in hits)
        {
            var pushRb = hit.GetComponent<Rigidbody2D>();
            if (pushRb != null)
            {
                Vector2 pushDir = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
                pushRb.AddForce(pushDir * pushForce, ForceMode2D.Impulse);
            }
        }

        // 发布技能事件
        EventBus.Publish(new AbilityUsedEvent
        {
            abilityName = "shadow_push",
            playerIndex = controller.PlayerIndex,
            position = transform.position
        });
    }
}
