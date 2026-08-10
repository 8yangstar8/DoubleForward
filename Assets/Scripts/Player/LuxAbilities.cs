using UnityEngine;

public class LuxAbilities : PlayerAbilityBase
{
    [Header("Light Beam")]
    [SerializeField] private GameObject lightBeamPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float beamLength = 8f;

    [Header("Light Bridge")]
    [SerializeField] private GameObject lightBridgePrefab;
    [SerializeField] private float bridgeDuration = 3f;
    [SerializeField] private float bridgeCooldown = 5f;

    private GameObject activeBeam;
    private PlayerController controller;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        abilityName = "Light Beam";
    }

    protected override void Activate()
    {
        if (activeBeam != null)
            Destroy(activeBeam);

        // 发布技能使用事件
        EventBus.Publish(new AbilityUsedEvent
        {
            abilityName = "light_beam",
            playerIndex = controller.PlayerIndex,
            position = transform.position
        });

        float dir = controller.IsFacingRight ? 1f : -1f;
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

        activeBeam = lightBeamPrefab != null
            ? Instantiate(lightBeamPrefab, spawnPos, Quaternion.identity)
            : CreateFallbackBeam(spawnPos);
        activeBeam.transform.SetParent(transform);
        activeBeam.transform.localScale = new Vector3(dir * beamLength, 1, 1);

        var beamCollider = activeBeam.GetComponent<BoxCollider2D>();
        if (beamCollider == null)
            beamCollider = activeBeam.AddComponent<BoxCollider2D>();
        beamCollider.isTrigger = true;
        activeBeam.tag = "LightZone";

        Invoke(nameof(DeactivateBeam), duration);
    }

    /// <summary>
    /// lightBeamPrefab 未配置时的运行时兜底光束 - 否则技能会静默失效,
    /// 光敏机关永远点不亮(能力互补门的基础)
    /// </summary>
    private static GameObject CreateFallbackBeam(Vector3 pos)
    {
        var beam = new GameObject("LightBeam");
        beam.transform.position = pos;
        var sr = beam.AddComponent<SpriteRenderer>();
        // 轴心在左侧中点,便于按朝向用负缩放翻转
        sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
            new Vector2(0f, 0.5f), 1f);
        sr.color = new Color(1f, 0.95f, 0.5f, 0.45f);
        sr.sortingOrder = 4;
        return beam;
    }

    private void DeactivateBeam()
    {
        if (activeBeam != null)
            Destroy(activeBeam);
        activeBeam = null;
        EndAbility();
    }

    /// <summary>
    /// lightBridgePrefab 未配置时的运行时兜底光桥 - 必须是实体平台(非触发器、
    /// 且在玩家会碰撞的Ground层),否则队友站不上去,光桥就失去意义
    /// </summary>
    private static GameObject CreateFallbackBridge(Vector3 pos)
    {
        var bridge = new GameObject("LightBridge");
        bridge.transform.position = pos;
        bridge.transform.localScale = new Vector3(3f, 0.3f, 1f);

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0) bridge.layer = groundLayer;

        var sr = bridge.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f), 1f);
        sr.color = new Color(1f, 0.95f, 0.6f, 0.7f);
        sr.sortingOrder = 3;

        bridge.AddComponent<BoxCollider2D>();
        return bridge;
    }

    public void CreateLightBridge()
    {
        float dir = controller.IsFacingRight ? 1f : -1f;
        Vector3 bridgePos = transform.position + new Vector3(dir * 1.5f, -0.5f, 0);

        var bridge = lightBridgePrefab != null
            ? Instantiate(lightBridgePrefab, bridgePos, Quaternion.identity)
            : CreateFallbackBridge(bridgePos);
        bridge.name = "LightBridge"; // 统一命名,预制体实例化会带"(Clone)"后缀
        bridge.tag = "LightZone";
        Destroy(bridge, bridgeDuration);

        // VFX
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.LightBridge, bridgePos);

        // 发布技能事件
        EventBus.Publish(new AbilityUsedEvent
        {
            abilityName = "light_bridge",
            playerIndex = controller.PlayerIndex,
            position = bridgePos
        });
    }

    void OnDestroy()
    {
        if (activeBeam != null)
            Destroy(activeBeam);
    }
}
