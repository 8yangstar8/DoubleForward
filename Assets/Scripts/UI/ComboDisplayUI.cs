using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 连击显示UI - 在画面中央弹出连击数和得分
/// 支持缩放弹跳动画和渐隐
/// </summary>
public class ComboDisplayUI : MonoBehaviour
{
    [Header("连击显示")]
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private TextMeshProUGUI multiplierText;
    [SerializeField] private CanvasGroup comboGroup;
    [SerializeField] private RectTransform comboRect;

    [Header("得分弹出")]
    [SerializeField] private TextMeshProUGUI scorePopText;
    [SerializeField] private CanvasGroup scorePopGroup;
    [SerializeField] private RectTransform scorePopRect;

    [Header("连击条")]
    [SerializeField] private Slider comboTimerBar;
    [SerializeField] private Image comboTimerFill;

    [Header("动画")]
    [SerializeField] private float punchScale = 1.3f;
    [SerializeField] private float punchDuration = 0.15f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float scorePopDuration = 1f;
    [SerializeField] private float scorePopRiseDistance = 60f;

    [Header("颜色")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highComboColor = Color.yellow;
    [SerializeField] private Color maxComboColor = new Color(1f, 0.3f, 0f); // 橙色
    [SerializeField] private int highComboThreshold = 5;
    [SerializeField] private int maxComboThreshold = 10;

    private Coroutine fadeCoroutine;
    private Coroutine scorePopCoroutine;

    void OnEnable()
    {
        if (ComboSystem.Instance != null)
        {
            ComboSystem.Instance.OnComboChanged += OnComboChanged;
            ComboSystem.Instance.OnComboBreak += OnComboBreak;
            ComboSystem.Instance.OnActionScore += OnActionScore;
        }

        // 初始隐藏
        if (comboGroup != null) comboGroup.alpha = 0;
        if (scorePopGroup != null) scorePopGroup.alpha = 0;
    }

    void OnDisable()
    {
        if (ComboSystem.Instance != null)
        {
            ComboSystem.Instance.OnComboChanged -= OnComboChanged;
            ComboSystem.Instance.OnComboBreak -= OnComboBreak;
            ComboSystem.Instance.OnActionScore -= OnActionScore;
        }
    }

    void Update()
    {
        // 更新连击计时条
        if (comboTimerBar != null && ComboSystem.Instance != null)
        {
            comboTimerBar.gameObject.SetActive(ComboSystem.Instance.IsComboActive);
            comboTimerBar.value = ComboSystem.Instance.ComboTimerNormalized;
        }
    }

    private void OnComboChanged(int combo)
    {
        if (combo <= 0) return;

        // 更新连击文本
        if (comboText != null)
            comboText.text = $"{combo} HIT";

        if (multiplierText != null)
            multiplierText.text = $"x{ComboSystem.Instance.ComboMultiplier:F1}";

        // 设置颜色
        Color targetColor = normalColor;
        if (combo >= maxComboThreshold) targetColor = maxComboColor;
        else if (combo >= highComboThreshold) targetColor = highComboColor;

        if (comboText != null) comboText.color = targetColor;

        // 显示并播放弹跳动画
        if (comboGroup != null) comboGroup.alpha = 1;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        StartCoroutine(PunchAnimation());
    }

    private void OnComboBreak()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutCombo());
    }

    private void OnActionScore(string actionName, int score)
    {
        if (scorePopText == null || scorePopGroup == null) return;

        scorePopText.text = $"+{score}";
        if (scorePopCoroutine != null) StopCoroutine(scorePopCoroutine);
        scorePopCoroutine = StartCoroutine(ScorePopAnimation());
    }

    private IEnumerator PunchAnimation()
    {
        if (comboRect == null) yield break;

        Vector3 originalScale = Vector3.one;

        // 放大
        float t = 0;
        while (t < punchDuration * 0.5f)
        {
            t += Time.unscaledDeltaTime;
            float p = t / (punchDuration * 0.5f);
            comboRect.localScale = Vector3.Lerp(originalScale, originalScale * punchScale, p);
            yield return null;
        }

        // 缩回
        t = 0;
        while (t < punchDuration * 0.5f)
        {
            t += Time.unscaledDeltaTime;
            float p = t / (punchDuration * 0.5f);
            comboRect.localScale = Vector3.Lerp(originalScale * punchScale, originalScale, p);
            yield return null;
        }

        comboRect.localScale = originalScale;
    }

    private IEnumerator FadeOutCombo()
    {
        if (comboGroup == null) yield break;

        yield return new WaitForSeconds(0.5f);

        float t = 0;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            comboGroup.alpha = 1f - (t / fadeOutDuration);
            yield return null;
        }
        comboGroup.alpha = 0;
    }

    private IEnumerator ScorePopAnimation()
    {
        if (scorePopGroup == null || scorePopRect == null) yield break;

        scorePopGroup.alpha = 1;
        Vector2 startPos = scorePopRect.anchoredPosition;
        Vector2 endPos = startPos + Vector2.up * scorePopRiseDistance;

        float t = 0;
        while (t < scorePopDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / scorePopDuration;

            scorePopRect.anchoredPosition = Vector2.Lerp(startPos, endPos, p);
            scorePopGroup.alpha = 1f - Mathf.Pow(p, 2);
            yield return null;
        }

        scorePopGroup.alpha = 0;
        scorePopRect.anchoredPosition = startPos;
    }
}
