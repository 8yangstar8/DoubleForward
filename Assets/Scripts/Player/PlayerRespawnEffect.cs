using UnityEngine;
using System.Collections;

/// <summary>
/// 玩家重生效果 - 处理重生时的视觉/音频反馈
/// 光/暗特效、无敌闪烁、着地冲击
/// 自动订阅PlayerHealth.OnRespawned事件
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerController))]
public class PlayerRespawnEffect : MonoBehaviour
{
    [Header("重生动画")]
    [SerializeField] private float respawnAnimDuration = 1f;
    [SerializeField] private float floatHeight = 2f;
    [SerializeField] private float landImpactDuration = 0.2f;

    [Header("无敌闪烁")]
    [SerializeField] private float blinkRate = 10f;
    [SerializeField] private float blinkDuration = 1.5f;

    [Header("视觉")]
    [SerializeField] private ParticleSystem respawnParticles;
    [SerializeField] private ParticleSystem landParticles;
    [SerializeField] private Color luxRespawnColor = new Color(1f, 0.95f, 0.6f);
    [SerializeField] private Color noxRespawnColor = new Color(0.4f, 0.2f, 0.8f);

    [Header("音效")]
    [SerializeField] private string respawnSound = "respawn";
    [SerializeField] private string landSound = "land";

    private PlayerHealth health;
    private PlayerController controller;
    private PlayerDeathEffect deathEffect;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;

    void Awake()
    {
        health = GetComponent<PlayerHealth>();
        controller = GetComponent<PlayerController>();
        deathEffect = GetComponent<PlayerDeathEffect>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        if (health != null)
            health.OnRespawned += OnPlayerRespawned;
    }

    void OnDisable()
    {
        if (health != null)
            health.OnRespawned -= OnPlayerRespawned;
    }

    private void OnPlayerRespawned()
    {
        // 重置死亡效果
        if (deathEffect != null)
            deathEffect.OnRespawnReset();

        StartCoroutine(RespawnSequence());
    }

    private IEnumerator RespawnSequence()
    {
        // 1. 确保精灵可见
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 0f;
            spriteRenderer.color = c;
        }

        // 2. 禁用物理
        if (rb != null)
        {
            rb.gravityScale = 0;
            rb.linearVelocity = Vector2.zero;
        }

        // 3. 从上方降落
        Vector3 targetPos = transform.position;
        transform.position = targetPos + Vector3.up * floatHeight;

        // 4. VFX
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.PlayerRespawn, transform.position);

        Color respawnColor = controller.Type == PlayerController.PlayerType.Lux
            ? luxRespawnColor : noxRespawnColor;

        if (respawnParticles != null)
        {
            var main = respawnParticles.main;
            main.startColor = respawnColor;
            respawnParticles.Play();
        }

        // 5. 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(respawnSound);

        // 6. 淡入 + 降落动画
        float elapsed = 0;
        Vector3 startPos = transform.position;

        while (elapsed < respawnAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / respawnAnimDuration;

            // 缓出降落
            float easeT = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(startPos, targetPos, easeT);

            // 淡入
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = Mathf.Lerp(0f, 1f, t * 2f); // 快速淡入
                spriteRenderer.color = c;

                // 发光效果
                spriteRenderer.color = Color.Lerp(respawnColor, spriteRenderer.color, t);
            }

            yield return null;
        }

        transform.position = targetPos;

        // 7. 着地冲击
        if (rb != null)
            rb.gravityScale = 2.5f;

        if (landParticles != null)
            landParticles.Play();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(landSound);

        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.DustLand, targetPos);

        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Light();

        // 8. 无敌闪烁
        yield return InvincibilityBlink();

        // 9. 确保最终状态正确
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 1f;
            spriteRenderer.color = c;
            spriteRenderer.enabled = true;
        }
    }

    private IEnumerator InvincibilityBlink()
    {
        if (spriteRenderer == null) yield break;

        float elapsed = 0;
        while (elapsed < blinkDuration)
        {
            elapsed += Time.deltaTime;
            float blink = Mathf.Sin(elapsed * blinkRate * Mathf.PI * 2f);
            spriteRenderer.enabled = blink > 0;
            yield return null;
        }

        spriteRenderer.enabled = true;
    }
}
