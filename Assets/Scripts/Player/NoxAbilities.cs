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

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        playerCollider = GetComponent<Collider2D>();
        abilityName = "Shadow Phase";
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

        rb.linearVelocity = new Vector2(dir * phaseDistance / phaseDuration, 0f);
        rb.gravityScale = 0f;

        yield return new WaitForSeconds(phaseDuration);

        gameObject.layer = originalLayer;
        rb.gravityScale = 2.5f;

        if (sr != null)
            sr.color = originalColor;

        EndAbility();
    }

    public void CreateShadowZone()
    {
        if (shadowZonePrefab == null) return;

        var zone = Instantiate(shadowZonePrefab, transform.position, Quaternion.identity);
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

    public void ShadowPush()
    {
        float dir = controller.IsFacingRight ? 1f : -1f;
        Vector2 pushOrigin = (Vector2)transform.position + new Vector2(dir * 0.5f, 0);
        var hits = Physics2D.OverlapCircleAll(pushOrigin, pushRange, pushableLayer);

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
