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

        float dir = controller.IsFacingRight ? 1f : -1f;
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

        activeBeam = Instantiate(lightBeamPrefab, spawnPos, Quaternion.identity);
        activeBeam.transform.SetParent(transform);
        activeBeam.transform.localScale = new Vector3(dir * beamLength, 1, 1);

        var beamCollider = activeBeam.GetComponent<BoxCollider2D>();
        if (beamCollider == null)
            beamCollider = activeBeam.AddComponent<BoxCollider2D>();
        beamCollider.isTrigger = true;
        activeBeam.tag = "LightZone";

        Invoke(nameof(DeactivateBeam), duration);
    }

    private void DeactivateBeam()
    {
        if (activeBeam != null)
            Destroy(activeBeam);
        activeBeam = null;
        EndAbility();
    }

    public void CreateLightBridge()
    {
        if (lightBridgePrefab == null) return;

        float dir = controller.IsFacingRight ? 1f : -1f;
        Vector3 bridgePos = transform.position + new Vector3(dir * 1.5f, -0.5f, 0);

        var bridge = Instantiate(lightBridgePrefab, bridgePos, Quaternion.identity);
        bridge.tag = "LightZone";
        Destroy(bridge, bridgeDuration);
    }

    void OnDestroy()
    {
        if (activeBeam != null)
            Destroy(activeBeam);
    }
}
