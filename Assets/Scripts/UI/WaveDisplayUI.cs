using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 波次战斗UI - 显示当前波次、剩余敌人数、波次标题动画
/// 订阅WaveStartedEvent/WaveCompletedEvent事件
/// </summary>
public class WaveDisplayUI : MonoBehaviour
{
    [Header("波次标题")]
    [SerializeField] private RectTransform waveTitlePanel;
    [SerializeField] private TextMeshProUGUI waveTitleText;
    [SerializeField] private TextMeshProUGUI waveSubtitleText;
    [SerializeField] private CanvasGroup waveTitleGroup;

    [Header("状态栏")]
    [SerializeField] private RectTransform waveStatusBar;
    [SerializeField] private TextMeshProUGUI waveCountText;
    [SerializeField] private TextMeshProUGUI enemyCountText;
    [SerializeField] private Image waveProgressFill;

    [Header("动画")]
    [SerializeField] private float titleShowDuration = 2.5f;
    [SerializeField] private float titleFadeInTime = 0.3f;
    [SerializeField] private float titleFadeOutTime = 0.5f;
    [SerializeField] private float titleSlideDistance = 100f;

    [Header("完成庆祝")]
    [SerializeField] private RectTransform completeBanner;
    [SerializeField] private TextMeshProUGUI completeText;
    [SerializeField] private float completeBannerDuration = 3f;

    private EnemyWaveManager currentWaveManager;
    private Coroutine titleCoroutine;

    void Start()
    {
        // 隐藏所有UI
        if (waveTitleGroup != null) waveTitleGroup.alpha = 0f;
        if (waveStatusBar != null) waveStatusBar.gameObject.SetActive(false);
        if (completeBanner != null) completeBanner.gameObject.SetActive(false);

        EventBus.Subscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
    }

    void Update()
    {
        UpdateStatusBar();
    }

    // ==================== 事件处理 ====================

    private void OnWaveStarted(WaveStartedEvent e)
    {
        // 查找WaveManager
        if (currentWaveManager == null)
            currentWaveManager = FindFirstObjectByType<EnemyWaveManager>();

        // 显示波次标题
        if (titleCoroutine != null) StopCoroutine(titleCoroutine);
        titleCoroutine = StartCoroutine(ShowWaveTitle(e));

        // 显示状态栏
        if (waveStatusBar != null)
            waveStatusBar.gameObject.SetActive(true);

        UpdateWaveCount(e.waveIndex, e.totalWaves);
    }

    private void OnWaveCompleted(WaveCompletedEvent e)
    {
        if (e.allCleared)
        {
            // 全部清除庆祝
            StartCoroutine(ShowCompleteBanner());

            // 隐藏状态栏
            if (waveStatusBar != null)
                waveStatusBar.gameObject.SetActive(false);
        }
    }

    // ==================== UI更新 ====================

    private void UpdateStatusBar()
    {
        if (currentWaveManager == null) return;
        if (!currentWaveManager.IsActive) return;

        // 敌人数量
        if (enemyCountText != null)
        {
            int remaining = currentWaveManager.EnemiesRemaining;
            enemyCountText.text = remaining.ToString();

            // 数字变色
            enemyCountText.color = remaining <= 1
                ? new Color(1f, 0.3f, 0.3f)
                : Color.white;
        }

        // 进度条
        if (waveProgressFill != null && currentWaveManager.TotalWaves > 0)
        {
            float progress = (float)(currentWaveManager.CurrentWave + 1) / currentWaveManager.TotalWaves;
            waveProgressFill.fillAmount = Mathf.Lerp(waveProgressFill.fillAmount, progress, 5f * Time.deltaTime);
        }
    }

    private void UpdateWaveCount(int current, int total)
    {
        if (waveCountText != null)
        {
            string label = LocalizationSystem.Instance != null
                ? LocalizationSystem.Instance.GetText($"wave_{current + 1}")
                : $"Wave {current + 1}";
            waveCountText.text = $"{label} / {total}";
        }
    }

    // ==================== 动画 ====================

    private IEnumerator ShowWaveTitle(WaveStartedEvent e)
    {
        if (waveTitleGroup == null) yield break;

        // 设置文本
        string waveName = e.waveName;
        if (LocalizationSystem.Instance != null)
        {
            string localized = LocalizationSystem.Instance.GetText($"wave_{e.waveIndex + 1}");
            if (!localized.StartsWith("[")) waveName = localized;
        }

        if (waveTitleText != null)
            waveTitleText.text = waveName;
        if (waveSubtitleText != null)
            waveSubtitleText.text = $"{e.waveIndex + 1} / {e.totalWaves}";

        // 从左滑入
        Vector3 startPos = waveTitlePanel != null
            ? waveTitlePanel.localPosition + Vector3.left * titleSlideDistance
            : Vector3.zero;
        Vector3 centerPos = waveTitlePanel != null
            ? waveTitlePanel.localPosition
            : Vector3.zero;

        // 淡入 + 滑入
        float elapsed = 0f;
        while (elapsed < titleFadeInTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / titleFadeInTime;
            float eased = EaseOutBack(t);

            waveTitleGroup.alpha = t;
            if (waveTitlePanel != null)
                waveTitlePanel.localPosition = Vector3.Lerp(startPos, centerPos, eased);

            yield return null;
        }

        waveTitleGroup.alpha = 1f;

        // 保持显示
        yield return new WaitForSecondsRealtime(titleShowDuration);

        // 淡出
        elapsed = 0f;
        while (elapsed < titleFadeOutTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / titleFadeOutTime;

            waveTitleGroup.alpha = 1f - t;

            yield return null;
        }

        waveTitleGroup.alpha = 0f;
    }

    private IEnumerator ShowCompleteBanner()
    {
        if (completeBanner == null) yield break;

        // 设置文本
        if (completeText != null)
        {
            completeText.text = LocalizationSystem.Instance != null
                ? LocalizationSystem.Instance.GetText("wave_all_clear")
                : "All Waves Cleared!";
        }

        completeBanner.gameObject.SetActive(true);

        // 缩放弹入
        completeBanner.localScale = Vector3.zero;
        float elapsed = 0f;
        float scaleInTime = 0.4f;

        while (elapsed < scaleInTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / scaleInTime;
            float scale = EaseOutBack(t);
            completeBanner.localScale = Vector3.one * scale;
            yield return null;
        }

        completeBanner.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(completeBannerDuration);

        // 淡出
        var group = completeBanner.GetComponent<CanvasGroup>();
        if (group != null)
        {
            elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = 1f - (elapsed / 0.5f);
                yield return null;
            }
            group.alpha = 1f;
        }

        completeBanner.gameObject.SetActive(false);
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);
    }
}
