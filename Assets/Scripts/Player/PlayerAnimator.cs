using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    private Animator animator;
    private PlayerController controller;

    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int VelocityY = Animator.StringToHash("VelocityY");
    private static readonly int IsDashing = Animator.StringToHash("IsDashing");
    private static readonly int TriggerJump = Animator.StringToHash("Jump");
    private static readonly int TriggerSkill = Animator.StringToHash("Skill");
    private static readonly int TriggerDeath = Animator.StringToHash("Death");
    private static readonly int TriggerRespawn = Animator.StringToHash("Respawn");
    private static readonly int TriggerAttack = Animator.StringToHash("Attack");
    private static readonly int AttackCombo = Animator.StringToHash("AttackCombo");

    void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<PlayerController>();
    }

    void OnEnable()
    {
        controller.OnJumped += PlayJump;
        controller.OnDashed += PlayDash;
    }

    void OnDisable()
    {
        controller.OnJumped -= PlayJump;
        controller.OnDashed -= PlayDash;
    }

    void Update()
    {
        animator.SetBool(IsRunning, Mathf.Abs(controller.Velocity.x) > 0.1f);
        animator.SetBool(IsGrounded, controller.IsGrounded);
        animator.SetFloat(VelocityY, controller.Velocity.y);
        animator.SetBool(IsDashing, controller.IsDashing);
    }

    private void PlayJump() => animator.SetTrigger(TriggerJump);
    private void PlayDash() => animator.SetTrigger(TriggerSkill);
    public void PlayDeath() => animator.SetTrigger(TriggerDeath);
    public void PlayRespawn() => animator.SetTrigger(TriggerRespawn);

    public void PlayAttack(int comboStep = 0)
    {
        animator.SetInteger(AttackCombo, comboStep);
        animator.SetTrigger(TriggerAttack);
    }
}
