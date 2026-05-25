using UnityEngine;

/// <summary>
/// 玩家拖尾特效 - Lux留下光之轨迹，Nox留下暗影轨迹
/// 使用TrailRenderer实现，根据角色类型和移动状态调整
/// 冲刺、技能释放时增强拖尾效果
/// </summary>
public class PlayerTrailEffect : MonoBehaviour
{
    [Header("拖尾")]
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private TrailRenderer dashTrail;

    [Header("Lux配色")]
    [SerializeField] private Color luxStartColor = new Color(1f, 0.95f, 0.7f, 0.8f);
    [SerializeField] private Color luxEndColor = new Color(1f, 0.85f, 0.5f, 0f);

    [Header("Nox配色")]
    [SerializeField] private Color noxStartColor = new Color(0.4f, 0.1f, 0.8f, 0.8f);
    [SerializeField] private Color noxEndColor = new Color(0.2f, 0.05f, 0.4f, 0f);

    [Header("参数")]
    [SerializeField] private float normalWidth = 0.15f;
    [SerializeField] private float dashWidth = 0.4f;
    [SerializeField] private float trailTime = 0.3f;
    [SerializeField] private float dashTrailTime = 0.5f;
    [SerializeField] private float minSpeedForTrail = 1f;

    [Header("粒子（可选）")]
    [SerializeField] private ParticleSystem moveParticles;
    [SerializeField] private float particleEmissionRate = 10f;

    private PlayerController controller;
    private bool isSetup;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    void Start()
    {
        SetupColors();
    }

    void Update()
    {
        if (controller == null) return;

        float speed = Mathf.Abs(controller.Velocity.x);
        bool isMoving = speed > minSpeedForTrail;
        bool isDashing = controller.IsDashing;

        // 主拖尾
        if (trail != null)
        {
            trail.emitting = isMoving || isDashing;
            trail.time = isDashing ? dashTrailTime : trailTime;
            trail.startWidth = isDashing ? dashWidth : normalWidth;
        }

        // 冲刺专用拖尾
        if (dashTrail != null)
        {
            dashTrail.emitting = isDashing;
        }

        // 粒子
        if (moveParticles != null)
        {
            var emission = moveParticles.emission;
            emission.rateOverTime = isMoving ? particleEmissionRate : 0f;
        }
    }

    private void SetupColors()
    {
        if (controller == null) return;
        isSetup = true;

        Color startColor, endColor;

        if (controller.Type == PlayerController.PlayerType.Lux)
        {
            startColor = luxStartColor;
            endColor = luxEndColor;
        }
        else
        {
            startColor = noxStartColor;
            endColor = noxEndColor;
        }

        if (trail != null)
        {
            trail.startColor = startColor;
            trail.endColor = endColor;
            trail.time = trailTime;
            trail.startWidth = normalWidth;
            trail.endWidth = 0f;
        }

        if (dashTrail != null)
        {
            Color dashStart = startColor;
            dashStart.a = 1f;
            dashTrail.startColor = dashStart;
            dashTrail.endColor = endColor;
            dashTrail.time = dashTrailTime;
            dashTrail.startWidth = dashWidth;
            dashTrail.endWidth = normalWidth * 0.5f;
            dashTrail.emitting = false;
        }

        // 配置粒子颜色
        if (moveParticles != null)
        {
            var main = moveParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
        }
    }

    /// <summary>
    /// 技能释放时强化拖尾（由技能系统调用）
    /// </summary>
    public void BoostTrail(float duration, float widthMultiplier = 2f)
    {
        StartCoroutine(BoostTrailCoroutine(duration, widthMultiplier));
    }

    private System.Collections.IEnumerator BoostTrailCoroutine(float duration, float multiplier)
    {
        if (trail == null) yield break;

        float originalWidth = trail.startWidth;
        float originalTime = trail.time;

        trail.startWidth = originalWidth * multiplier;
        trail.time = originalTime * 1.5f;

        yield return new WaitForSeconds(duration);

        trail.startWidth = originalWidth;
        trail.time = originalTime;
    }
}
