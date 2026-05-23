using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public abstract class BossBase : MonoBehaviour
{
    [Header("Boss Stats")]
    [SerializeField] protected int maxHealth = 20;
    [SerializeField] protected string bossName = "Boss";

    [Header("Phase Settings")]
    [SerializeField] protected List<BossPhase> phases = new List<BossPhase>();

    [Header("References")]
    [SerializeField] protected Transform bossCenter;
    [SerializeField] protected SpriteRenderer spriteRenderer;

    [System.Serializable]
    public class BossPhase
    {
        public string phaseName;
        [Range(0f, 1f)] public float healthThreshold = 0.5f;
        public float attackInterval = 2f;
        public float moveSpeed = 3f;
        public Color phaseColor = Color.white;
    }

    public int CurrentHealth { get; protected set; }
    public int CurrentPhaseIndex { get; protected set; }
    public bool IsAlive => CurrentHealth > 0;
    public bool IsInvincible { get; protected set; }
    public bool IsBattleActive { get; protected set; }

    public event System.Action<int, int> OnHealthChanged; // current, max
    public event System.Action<int> OnPhaseChanged;
    public event System.Action OnBossDefeated;
    public event System.Action OnBattleStart;

    protected float attackTimer;
    protected Rigidbody2D rb;
    protected bool isAttacking;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        CurrentHealth = maxHealth;
    }

    public virtual void StartBattle()
    {
        IsBattleActive = true;
        CurrentPhaseIndex = 0;
        attackTimer = phases.Count > 0 ? phases[0].attackInterval : 2f;
        OnBattleStart?.Invoke();
        OnPhaseEnter(0);
    }

    protected virtual void Update()
    {
        if (!IsBattleActive || !IsAlive) return;

        CheckPhaseTransition();

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0 && !isAttacking)
        {
            var phase = GetCurrentPhase();
            attackTimer = phase?.attackInterval ?? 2f;
            StartCoroutine(ExecuteAttackPattern());
        }

        UpdateMovement();
    }

    private void CheckPhaseTransition()
    {
        float healthPercent = (float)CurrentHealth / maxHealth;

        for (int i = phases.Count - 1; i > CurrentPhaseIndex; i--)
        {
            if (healthPercent <= phases[i].healthThreshold)
            {
                int oldPhase = CurrentPhaseIndex;
                CurrentPhaseIndex = i;
                OnPhaseExit(oldPhase);
                OnPhaseEnter(i);
                OnPhaseChanged?.Invoke(i);
                break;
            }
        }
    }

    public virtual void TakeDamage(int damage)
    {
        if (!IsAlive || IsInvincible) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        StartCoroutine(DamageFlash());

        if (CurrentHealth <= 0)
        {
            StartCoroutine(DefeatSequence());
        }
    }

    private IEnumerator DamageFlash()
    {
        if (spriteRenderer == null) yield break;
        Color original = spriteRenderer.color;
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = original;
    }

    protected virtual IEnumerator DefeatSequence()
    {
        IsBattleActive = false;
        IsInvincible = true;

        // 死亡动画：闪烁后消失
        for (int i = 0; i < 10; i++)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(0.15f);
        }

        OnBossDefeated?.Invoke();
        gameObject.SetActive(false);
    }

    protected BossPhase GetCurrentPhase()
    {
        if (CurrentPhaseIndex < phases.Count)
            return phases[CurrentPhaseIndex];
        return null;
    }

    protected virtual void OnPhaseEnter(int phaseIndex)
    {
        var phase = phases[phaseIndex];
        if (spriteRenderer != null)
            spriteRenderer.color = phase.phaseColor;
        IsInvincible = true;
        StartCoroutine(PhaseTransitionInvincibility());
    }

    protected virtual void OnPhaseExit(int phaseIndex) { }

    private IEnumerator PhaseTransitionInvincibility()
    {
        yield return new WaitForSeconds(1f);
        IsInvincible = false;
    }

    protected abstract IEnumerator ExecuteAttackPattern();
    protected abstract void UpdateMovement();

    // 寻找最近的玩家
    protected Transform FindNearestPlayer()
    {
        var lux = LevelManager.Instance?.LuxPlayer;
        var nox = LevelManager.Instance?.NoxPlayer;

        if (lux == null && nox == null) return null;
        if (lux == null) return nox.transform;
        if (nox == null) return lux.transform;

        float dLux = Vector2.Distance(transform.position, lux.transform.position);
        float dNox = Vector2.Distance(transform.position, nox.transform.position);
        return dLux < dNox ? lux.transform : nox.transform;
    }

    protected Transform GetLuxPlayer()
    {
        return LevelManager.Instance?.LuxPlayer?.transform;
    }

    protected Transform GetNoxPlayer()
    {
        return LevelManager.Instance?.NoxPlayer?.transform;
    }
}
