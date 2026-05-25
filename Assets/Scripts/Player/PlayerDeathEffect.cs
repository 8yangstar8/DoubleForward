using UnityEngine;
using System.Collections;

/// <summary>
/// 玩家死亡效果 - 处理死亡时的视觉/音频/触觉反馈
/// 慢动作、屏幕闪白、粒子爆发、灵魂飘散
/// 自动订阅PlayerHealth.OnDeath事件
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerController))]
public class PlayerDeathEffect : MonoBehaviour
{
    [Header("慢动作")]
    [SerializeField] private bool enableSlowMotion = true;
    [SerializeField] private float slowMotionScale = 0.3f;
    [SerializeField] private float slowMotionDuration = 0.5f;

    [Header("屏幕效果")]
    [SerializeField] private bool enableScreenFlash = true;
    [SerializeField] private Color flashColor = new Color(1f, 0.2f, 0.1f, 0.5f);
    [SerializeField] private float flashDuration = 0.3f;

    [Header("视觉")]
    [SerializeField] private ParticleSystem deathBurstParticles;
    [SerializeField] private ParticleSystem soulParticles;
    [SerializeField] private float fadeOutDuration = 0.8f;
    [SerializeField] private float dissolveDuration = 1f;

    [Header("音效")]
    [SerializeField] private string deathSound = "player_death";
    [SerializeField] private string soulSound = "soul_rise";

    [Header("震动")]
    [SerializeField] private float shakeIntensity = 0.4f;
    [SerializeField] private float shakeDuration = 0.3f;

    private PlayerHealth health;
    private PlayerController controller;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;

    void Awake()
    {
        health = GetComponent<PlayerHealth>();
        controller = GetComponent<PlayerController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        if (health != null)
            health.OnDeath += OnPlayerDeath;
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDeath -= OnPlayerDeath;
    }

    private void OnPlayerDeath()
    {
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // 1. 慢动作
        if (enableSlowMotion)
        {
            Time.timeScale = slowMotionScale;
            Time.fixedDeltaTime = 0.02f * slowMotionScale;
        }

        // 2. 屏幕效果
        if (enableScreenFlash && CameraEffects.Instance != null)
            CameraEffects.Instance.Flash(flashColor, flashDuration);

        // 3. 屏幕震动
        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeMedium();

        // 4. 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(deathSound);

        // 5. 触觉
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Heavy();

        // 6. 粒子爆发
        if (deathBurstParticles != null)
        {
            deathBurstParticles.transform.SetParent(null);
            deathBurstParticles.transform.position = transform.position;

            // 根据角色类型设置粒子颜色
            var main = deathBurstParticles.main;
            if (controller.Type == PlayerController.PlayerType.Lux)
                main.startColor = new Color(1f, 0.9f, 0.5f);
            else
                main.startColor = new Color(0.4f, 0.1f, 0.8f);

            deathBurstParticles.Play();
        }

        // 7. VFX
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.PlayerDeath, transform.position);

        // 8. 禁用物理
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0;
        }

        // 9. 慢动作恢复
        if (enableSlowMotion)
        {
            yield return new WaitForSecondsRealtime(slowMotionDuration);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        // 10. 精灵淡出/溶解
        if (spriteRenderer != null)
        {
            float elapsed = 0;
            Color origColor = spriteRenderer.color;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;

                Color c = origColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                spriteRenderer.color = c;

                // 向上漂浮
                transform.position += Vector3.up * 0.5f * Time.deltaTime;

                yield return null;
            }

            spriteRenderer.color = new Color(origColor.r, origColor.g, origColor.b, 0f);
        }

        // 11. 灵魂粒子
        if (soulParticles != null)
        {
            soulParticles.transform.SetParent(null);
            soulParticles.Play();

            if (!string.IsNullOrEmpty(soulSound) && SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play(soulSound);

            Destroy(soulParticles.gameObject, 3f);
        }
    }

    /// <summary>
    /// 重生时重置效果
    /// </summary>
    public void OnRespawnReset()
    {
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 1f;
            spriteRenderer.color = c;
        }

        if (rb != null)
            rb.gravityScale = 2.5f;

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
}
