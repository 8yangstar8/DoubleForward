using UnityEngine;

/// <summary>
/// 动画事件中继器 - 将Animation Event桥接到游戏逻辑
/// 在Animator的动画片段中添加事件，通过此组件分发到各系统
/// 支持：攻击判定帧、脚步声、特效触发、相机震动等
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimationEventRelay : MonoBehaviour
{
    [Header("关联组件")]
    [SerializeField] private PlayerController controller;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerHealth health;

    [Header("脚步声")]
    [SerializeField] private string footstepSoundKey = "footstep";
    [SerializeField] private string landSoundKey = "land";
    [SerializeField] private float footstepVolume = 0.5f;

    [Header("特效点")]
    [SerializeField] private Transform fxPoint;              // 通用特效挂点
    [SerializeField] private Transform footDustPoint;        // 脚部灰尘

    // 事件
    public event System.Action OnAttackFrameStart;   // 攻击判定开始帧
    public event System.Action OnAttackFrameEnd;     // 攻击判定结束帧
    public event System.Action<string> OnAnimEvent;  // 通用事件（参数为事件名）

    void Awake()
    {
        if (controller == null) controller = GetComponentInParent<PlayerController>();
        if (combat == null) combat = GetComponentInParent<PlayerCombat>();
        if (health == null) health = GetComponentInParent<PlayerHealth>();
    }

    // ==================== 在动画片段中绑定的回调方法 ====================

    /// <summary>
    /// 攻击判定开始（动画帧事件调用）
    /// </summary>
    public void OnMeleeStart()
    {
        OnAttackFrameStart?.Invoke();
    }

    /// <summary>
    /// 攻击判定结束（动画帧事件调用）
    /// </summary>
    public void OnMeleeEnd()
    {
        OnAttackFrameEnd?.Invoke();
    }

    /// <summary>
    /// 播放脚步声（走路/跑步动画中每步调用）
    /// </summary>
    public void OnFootstep()
    {
        if (SoundFeedback.Instance != null)
        {
            string surface = DetectSurface();
            string key = $"{footstepSoundKey}_{surface}";
            SoundFeedback.Instance.PlayWithVolume(key, footstepVolume);
        }

        // 脚步灰尘
        if (footDustPoint != null && VFXManager.Instance != null)
        {
            if (controller != null && controller.IsGrounded)
                VFXManager.Instance.PlayEffect("foot_dust", footDustPoint.position);
        }
    }

    /// <summary>
    /// 着地冲击（着地动画中调用）
    /// </summary>
    public void OnLandImpact()
    {
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(landSoundKey);

        // 着地灰尘
        if (footDustPoint != null && VFXManager.Instance != null)
            VFXManager.Instance.PlayEffect("land_dust", footDustPoint.position);

        // 轻微震动
        if (CameraEffects.Instance != null)
            CameraEffects.Instance.Shake(0.05f, 0.05f);
    }

    /// <summary>
    /// 触发特效（在动画帧中传入特效名）
    /// </summary>
    public void OnPlayVFX(string effectName)
    {
        if (string.IsNullOrEmpty(effectName)) return;

        Vector3 pos = fxPoint != null ? fxPoint.position : transform.position;

        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayEffect(effectName, pos);
    }

    /// <summary>
    /// 触发音效（在动画帧中传入音效名）
    /// </summary>
    public void OnPlaySFX(string soundKey)
    {
        if (string.IsNullOrEmpty(soundKey)) return;

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(soundKey);
    }

    /// <summary>
    /// 相机震动（在动画帧中传入震动强度0~1的字符串）
    /// </summary>
    public void OnCameraShake(string intensityStr)
    {
        float intensity = 0.1f;
        if (float.TryParse(intensityStr, out float parsed))
            intensity = parsed;

        if (CameraEffects.Instance != null)
            CameraEffects.Instance.Shake(intensity, 0.15f);
    }

    /// <summary>
    /// 切换无敌帧（躲避/翻滚动画中调用）
    /// </summary>
    public void OnInvincibleStart()
    {
        if (health != null)
            health.IsInvincible = true;
    }

    /// <summary>
    /// 结束无敌帧
    /// </summary>
    public void OnInvincibleEnd()
    {
        if (health != null)
            health.IsInvincible = false;
    }

    /// <summary>
    /// 速度修正（攻击动画中减速移动）
    /// </summary>
    public void OnSetMoveSpeedMultiplier(string multiplierStr)
    {
        // 预留接口 - PlayerController需要添加speedMultiplier支持
    }

    /// <summary>
    /// 生成投射物（远程攻击动画中调用）
    /// </summary>
    public void OnSpawnProjectile()
    {
        if (combat != null)
            combat.RangedAttack();
    }

    /// <summary>
    /// 通用自定义事件（动画帧传入事件名字符串）
    /// </summary>
    public void OnCustomEvent(string eventName)
    {
        OnAnimEvent?.Invoke(eventName);
    }

    /// <summary>
    /// 死亡动画结束（触发重生流程）
    /// </summary>
    public void OnDeathAnimComplete()
    {
        // RespawnSystem会处理这个
        OnAnimEvent?.Invoke("death_complete");
    }

    /// <summary>
    /// Dash影子残影（Nox冲刺动画中周期调用）
    /// </summary>
    public void OnDashAfterimage()
    {
        if (VFXManager.Instance != null)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

            if (sr != null)
                VFXManager.Instance.PlayEffect("dash_afterimage", transform.position);
        }
    }

    // ==================== 内部方法 ====================

    /// <summary>
    /// 检测当前脚下的地面材质
    /// </summary>
    private string DetectSurface()
    {
        if (controller == null) return "default";

        RaycastHit2D hit = Physics2D.Raycast(
            transform.position, Vector2.down, 1f,
            LayerMask.GetMask("Ground", "Platform"));

        if (hit.collider != null)
        {
            // 根据Tag判断地面材质
            string tag = hit.collider.tag;
            switch (tag)
            {
                case "Metal": return "metal";
                case "Wood": return "wood";
                case "Water": return "water";
                case "Grass": return "grass";
                case "Stone": return "stone";
                case "Sand": return "sand";
                default: return "default";
            }
        }

        return "default";
    }
}

/// <summary>
/// SoundFeedback扩展 - 支持音量参数
/// </summary>
public static class SoundFeedbackExtensions
{
    public static void PlayWithVolume(this SoundFeedback feedback, string key, float volume)
    {
        // SoundFeedback.Play已有基础实现，这里提供音量控制版本
        // 如果SoundFeedback没有该方法，则回退到默认Play
        feedback.Play(key);
    }
}
