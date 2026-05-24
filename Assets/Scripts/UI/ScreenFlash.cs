using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 屏幕闪烁效果 - 受伤红闪、治愈绿闪、Boss阶段白闪
/// 全局单例，覆盖在UI最上层
/// </summary>
public class ScreenFlash : MonoBehaviour
{
    public static ScreenFlash Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private Image flashImage;

    [Header("预设颜色")]
    [SerializeField] private Color damageColor = new Color(1f, 0f, 0f, 0.4f);
    [SerializeField] private Color healColor = new Color(0f, 1f, 0.3f, 0.3f);
    [SerializeField] private Color whiteFlashColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color deathColor = new Color(0.8f, 0f, 0f, 0.6f);
    [SerializeField] private Color collectColor = new Color(1f, 0.9f, 0f, 0.2f);

    private Coroutine currentFlash;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (flashImage != null)
        {
            flashImage.color = Color.clear;
            flashImage.raycastTarget = false;
        }
    }

    void OnEnable()
    {
        EventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Subscribe<PlayerHealEvent>(OnPlayerHeal);
        EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Subscribe<BossPhaseChangedEvent>(OnBossPhaseChanged);
        EventBus.Subscribe<CollectiblePickedEvent>(OnCollectible);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Unsubscribe<PlayerHealEvent>(OnPlayerHeal);
        EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
        EventBus.Unsubscribe<BossPhaseChangedEvent>(OnBossPhaseChanged);
        EventBus.Unsubscribe<CollectiblePickedEvent>(OnCollectible);
    }

    // ============ 事件响应 ============

    private void OnPlayerDamaged(PlayerDamagedEvent evt)
    {
        Flash(damageColor, 0.2f);
    }

    private void OnPlayerHeal(PlayerHealEvent evt)
    {
        Flash(healColor, 0.3f);
    }

    private void OnPlayerDeath(PlayerDeathEvent evt)
    {
        Flash(deathColor, 0.5f);
    }

    private void OnBossPhaseChanged(BossPhaseChangedEvent evt)
    {
        Flash(whiteFlashColor, 0.4f);
    }

    private void OnCollectible(CollectiblePickedEvent evt)
    {
        Flash(collectColor, 0.15f);
    }

    // ============ 公共API ============

    /// <summary>
    /// 自定义颜色闪烁
    /// </summary>
    public void Flash(Color color, float duration = 0.3f)
    {
        if (flashImage == null) return;

        if (currentFlash != null)
            StopCoroutine(currentFlash);

        currentFlash = StartCoroutine(DoFlash(color, duration));
    }

    /// <summary>
    /// 受伤闪烁
    /// </summary>
    public void FlashDamage()
    {
        Flash(damageColor, 0.2f);
    }

    /// <summary>
    /// 治愈闪烁
    /// </summary>
    public void FlashHeal()
    {
        Flash(healColor, 0.3f);
    }

    /// <summary>
    /// 白色闪烁（Boss阶段变化、大型事件）
    /// </summary>
    public void FlashWhite()
    {
        Flash(whiteFlashColor, 0.4f);
    }

    /// <summary>
    /// 全屏变暗再恢复（场景过渡感）
    /// </summary>
    public void FadeInOut(Color color, float fadeInTime, float holdTime, float fadeOutTime)
    {
        if (flashImage == null) return;

        if (currentFlash != null)
            StopCoroutine(currentFlash);

        currentFlash = StartCoroutine(DoFadeInOut(color, fadeInTime, holdTime, fadeOutTime));
    }

    // ============ 协程 ============

    private IEnumerator DoFlash(Color color, float duration)
    {
        flashImage.color = color;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            // 快速出现，慢速消失
            float alpha;
            if (t < 0.2f)
                alpha = color.a; // 保持
            else
                alpha = Mathf.Lerp(color.a, 0f, (t - 0.2f) / 0.8f);

            flashImage.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        flashImage.color = Color.clear;
        currentFlash = null;
    }

    private IEnumerator DoFadeInOut(Color color, float fadeIn, float hold, float fadeOut)
    {
        // Fade in
        float elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / fadeIn;
            flashImage.color = new Color(color.r, color.g, color.b, t * color.a);
            yield return null;
        }

        // Hold
        flashImage.color = color;
        yield return new WaitForSecondsRealtime(hold);

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeOut)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / fadeOut;
            flashImage.color = new Color(color.r, color.g, color.b, (1f - t) * color.a);
            yield return null;
        }

        flashImage.color = Color.clear;
        currentFlash = null;
    }
}
