using UnityEngine;

public abstract class PlayerAbilityBase : MonoBehaviour
{
    [SerializeField] protected float cooldown = 2f;
    [SerializeField] protected float duration = 3f;
    [SerializeField] protected string abilityName = "Ability";

    protected float cooldownTimer;
    protected bool isActive;

    public bool IsReady => cooldownTimer <= 0f;
    public bool IsActive => isActive;
    public float CooldownProgress => cooldown > 0 ? Mathf.Clamp01(cooldownTimer / cooldown) : 0f;

    public event System.Action OnAbilityActivated;
    public event System.Action OnAbilityEnded;

    protected virtual void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    public void TryActivate()
    {
        if (!IsReady || isActive) return;
        isActive = true;
        cooldownTimer = cooldown;
        OnAbilityActivated?.Invoke();
        Activate();
    }

    protected void EndAbility()
    {
        isActive = false;
        OnAbilityEnded?.Invoke();
    }

    protected abstract void Activate();

    /// <summary>
    /// 动画事件回调 — 在技能释放帧触发
    /// 子类可重写以在精确的动画帧执行效果
    /// </summary>
    public virtual void OnCastFrameTriggered() { }
}
