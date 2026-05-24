using UnityEngine;
using System.Collections;

/// <summary>
/// 时间管理器 - 子弹时间、击杀慢动作、Boss特写
/// 管理Time.timeScale和fixedDeltaTime的安全修改与恢复
/// </summary>
public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("慢动作预设")]
    [SerializeField] private float hitStopTimeScale = 0.05f;
    [SerializeField] private float hitStopDuration = 0.08f;
    [SerializeField] private float killSlowTimeScale = 0.3f;
    [SerializeField] private float killSlowDuration = 0.4f;
    [SerializeField] private float bossDeathTimeScale = 0.1f;
    [SerializeField] private float bossDeathDuration = 1.5f;

    private float targetTimeScale = 1f;
    private float originalFixedDeltaTime;
    private Coroutine currentEffect;
    private bool isPaused;
    private int pauseRequestCount; // 支持嵌套暂停

    public float CurrentTimeScale => Time.timeScale;
    public bool IsSlowMotion => Time.timeScale < 0.9f && !isPaused;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        originalFixedDeltaTime = Time.fixedDeltaTime;
    }

    void OnEnable()
    {
        EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
        EventBus.Subscribe<EnemyHitEvent>(OnEnemyHit);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        EventBus.Unsubscribe<EnemyHitEvent>(OnEnemyHit);

        // 确保恢复时间
        ResetTimeScale();
    }

    // ============ 事件响应 ============

    private void OnEnemyHit(EnemyHitEvent evt)
    {
        // 击中顿帧（hit stop）
        HitStop();
    }

    private void OnEnemyDefeated(EnemyDefeatedEvent evt)
    {
        // 击杀慢动作
        SlowMotion(killSlowTimeScale, killSlowDuration);
    }

    private void OnBossDefeated(BossDefeatedEvent evt)
    {
        // Boss死亡大慢动作
        SlowMotion(bossDeathTimeScale, bossDeathDuration);
    }

    // ============ 公共API ============

    /// <summary>
    /// 顿帧效果（击中瞬间的停顿感）
    /// </summary>
    public void HitStop(float duration = -1f)
    {
        if (isPaused) return;

        float dur = duration > 0 ? duration : hitStopDuration;
        DoTimeEffect(hitStopTimeScale, dur, false);
    }

    /// <summary>
    /// 慢动作效果
    /// </summary>
    public void SlowMotion(float timeScale = 0.3f, float duration = 0.5f)
    {
        if (isPaused) return;

        DoTimeEffect(timeScale, duration, true);
    }

    /// <summary>
    /// 渐变慢动作（平滑过渡到慢速再恢复）
    /// </summary>
    public void GradualSlowMotion(float targetScale, float slowDownTime, float holdTime, float speedUpTime)
    {
        if (isPaused) return;

        if (currentEffect != null)
            StopCoroutine(currentEffect);

        currentEffect = StartCoroutine(DoGradualEffect(targetScale, slowDownTime, holdTime, speedUpTime));
    }

    /// <summary>
    /// 暂停游戏
    /// </summary>
    public void Pause()
    {
        pauseRequestCount++;
        if (pauseRequestCount == 1)
        {
            isPaused = true;
            Time.timeScale = 0f;
        }
    }

    /// <summary>
    /// 恢复游戏
    /// </summary>
    public void Resume()
    {
        pauseRequestCount = Mathf.Max(0, pauseRequestCount - 1);
        if (pauseRequestCount == 0)
        {
            isPaused = false;
            Time.timeScale = targetTimeScale;
            Time.fixedDeltaTime = originalFixedDeltaTime * targetTimeScale;
        }
    }

    /// <summary>
    /// 强制恢复正常时间（跳过所有效果）
    /// </summary>
    public void ResetTimeScale()
    {
        if (currentEffect != null)
            StopCoroutine(currentEffect);

        targetTimeScale = 1f;
        pauseRequestCount = 0;
        isPaused = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = originalFixedDeltaTime;
    }

    /// <summary>
    /// 设置全局时间缩放（不自动恢复）
    /// </summary>
    public void SetTimeScale(float scale)
    {
        if (isPaused) return;

        targetTimeScale = Mathf.Clamp(scale, 0.01f, 3f);
        Time.timeScale = targetTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime * targetTimeScale;
    }

    // ============ 内部方法 ============

    private void DoTimeEffect(float scale, float duration, bool smooth)
    {
        if (currentEffect != null)
            StopCoroutine(currentEffect);

        if (smooth)
            currentEffect = StartCoroutine(DoSmoothEffect(scale, duration));
        else
            currentEffect = StartCoroutine(DoInstantEffect(scale, duration));
    }

    private IEnumerator DoInstantEffect(float scale, float duration)
    {
        SetTimeScaleInternal(scale);
        yield return new WaitForSecondsRealtime(duration);
        SetTimeScaleInternal(1f);
        currentEffect = null;
    }

    private IEnumerator DoSmoothEffect(float scale, float duration)
    {
        // 快速减速
        float slowDownTime = duration * 0.15f;
        float holdTime = duration * 0.5f;
        float speedUpTime = duration * 0.35f;

        // Slow down
        float elapsed = 0;
        while (elapsed < slowDownTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / slowDownTime;
            SetTimeScaleInternal(Mathf.Lerp(1f, scale, t));
            yield return null;
        }

        // Hold
        SetTimeScaleInternal(scale);
        yield return new WaitForSecondsRealtime(holdTime);

        // Speed up
        elapsed = 0;
        while (elapsed < speedUpTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / speedUpTime;
            t = t * t; // ease in
            SetTimeScaleInternal(Mathf.Lerp(scale, 1f, t));
            yield return null;
        }

        SetTimeScaleInternal(1f);
        currentEffect = null;
    }

    private IEnumerator DoGradualEffect(float targetScale, float slowDown, float hold, float speedUp)
    {
        float startScale = Time.timeScale;

        // Slow down
        float elapsed = 0;
        while (elapsed < slowDown)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / slowDown;
            SetTimeScaleInternal(Mathf.Lerp(startScale, targetScale, t));
            yield return null;
        }

        // Hold
        SetTimeScaleInternal(targetScale);
        yield return new WaitForSecondsRealtime(hold);

        // Speed up
        elapsed = 0;
        while (elapsed < speedUp)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / speedUp;
            SetTimeScaleInternal(Mathf.Lerp(targetScale, 1f, t));
            yield return null;
        }

        SetTimeScaleInternal(1f);
        currentEffect = null;
    }

    private void SetTimeScaleInternal(float scale)
    {
        if (isPaused) return;

        targetTimeScale = scale;
        Time.timeScale = scale;
        Time.fixedDeltaTime = originalFixedDeltaTime * Mathf.Max(scale, 0.01f);
    }
}
