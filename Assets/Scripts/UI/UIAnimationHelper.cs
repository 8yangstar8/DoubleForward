using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// UI动画工具 - 提供常用的UI动画效果
/// 弹入弹出、淡入淡出、缩放脉冲、滑入滑出、摇晃等
/// 所有动画使用unscaledTime以在暂停时正常工作
/// </summary>
public class UIAnimationHelper : MonoBehaviour
{
    public static UIAnimationHelper Instance { get; private set; }

    [Header("默认曲线")]
    [SerializeField] private AnimationCurve easeOutBack = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve easeOutElastic = AnimationCurve.EaseInOut(0, 0, 1, 1);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 初始化默认曲线（如果Inspector未配置）
        InitDefaultCurves();
    }

    private void InitDefaultCurves()
    {
        // EaseOutBack: 超出后回弹
        easeOutBack = new AnimationCurve();
        easeOutBack.AddKey(new Keyframe(0f, 0f, 0f, 0f));
        easeOutBack.AddKey(new Keyframe(0.6f, 1.1f, 2f, 2f));
        easeOutBack.AddKey(new Keyframe(1f, 1f, 0f, 0f));

        // EaseOutElastic: 弹性效果
        easeOutElastic = new AnimationCurve();
        easeOutElastic.AddKey(new Keyframe(0f, 0f, 0f, 4f));
        easeOutElastic.AddKey(new Keyframe(0.4f, 1.05f, 0f, 0f));
        easeOutElastic.AddKey(new Keyframe(0.65f, 0.97f, 0f, 0f));
        easeOutElastic.AddKey(new Keyframe(0.85f, 1.01f, 0f, 0f));
        easeOutElastic.AddKey(new Keyframe(1f, 1f, 0f, 0f));
    }

    // ==================== 弹入弹出 ====================

    /// <summary>
    /// 从零缩放弹入到原始大小
    /// </summary>
    public Coroutine PopIn(RectTransform target, float duration = 0.3f, System.Action onComplete = null)
    {
        return StartCoroutine(PopInRoutine(target, duration, onComplete));
    }

    private IEnumerator PopInRoutine(RectTransform target, float duration, System.Action onComplete)
    {
        if (target == null) yield break;

        target.gameObject.SetActive(true);
        target.localScale = Vector3.zero;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = easeOutBack.Evaluate(t);
            target.localScale = Vector3.one * scale;
            yield return null;
        }

        target.localScale = Vector3.one;
        onComplete?.Invoke();
    }

    /// <summary>
    /// 缩放弹出到零
    /// </summary>
    public Coroutine PopOut(RectTransform target, float duration = 0.2f, System.Action onComplete = null)
    {
        return StartCoroutine(PopOutRoutine(target, duration, onComplete));
    }

    private IEnumerator PopOutRoutine(RectTransform target, float duration, System.Action onComplete)
    {
        if (target == null) yield break;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // EaseInBack效果
            float scale = 1f - (t * t * (2.7f * t - 1.7f));
            target.localScale = Vector3.one * Mathf.Max(scale, 0f);
            yield return null;
        }

        target.localScale = Vector3.zero;
        target.gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    // ==================== 淡入淡出 ====================

    /// <summary>
    /// CanvasGroup淡入
    /// </summary>
    public Coroutine FadeIn(CanvasGroup group, float duration = 0.25f, System.Action onComplete = null)
    {
        return StartCoroutine(FadeRoutine(group, 0f, 1f, duration, onComplete));
    }

    /// <summary>
    /// CanvasGroup淡出
    /// </summary>
    public Coroutine FadeOut(CanvasGroup group, float duration = 0.25f, System.Action onComplete = null)
    {
        return StartCoroutine(FadeRoutine(group, 1f, 0f, duration, onComplete));
    }

    private IEnumerator FadeRoutine(CanvasGroup group, float from, float to, float duration, System.Action onComplete)
    {
        if (group == null) yield break;

        group.gameObject.SetActive(true);
        group.alpha = from;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // SmoothStep缓动
            t = t * t * (3f - 2f * t);
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        group.alpha = to;

        if (to <= 0.01f)
            group.gameObject.SetActive(false);

        onComplete?.Invoke();
    }

    // ==================== 滑入滑出 ====================

    public enum SlideDirection { Left, Right, Top, Bottom }

    /// <summary>
    /// 从指定方向滑入
    /// </summary>
    public Coroutine SlideIn(RectTransform target, SlideDirection direction, float distance = 300f,
        float duration = 0.35f, System.Action onComplete = null)
    {
        return StartCoroutine(SlideRoutine(target, direction, distance, duration, true, onComplete));
    }

    /// <summary>
    /// 向指定方向滑出
    /// </summary>
    public Coroutine SlideOut(RectTransform target, SlideDirection direction, float distance = 300f,
        float duration = 0.25f, System.Action onComplete = null)
    {
        return StartCoroutine(SlideRoutine(target, direction, distance, duration, false, onComplete));
    }

    private IEnumerator SlideRoutine(RectTransform target, SlideDirection direction, float distance,
        float duration, bool slideIn, System.Action onComplete)
    {
        if (target == null) yield break;

        target.gameObject.SetActive(true);
        Vector2 originalPos = target.anchoredPosition;

        Vector2 offset = direction switch
        {
            SlideDirection.Left => new Vector2(-distance, 0),
            SlideDirection.Right => new Vector2(distance, 0),
            SlideDirection.Top => new Vector2(0, distance),
            SlideDirection.Bottom => new Vector2(0, -distance),
            _ => Vector2.zero
        };

        Vector2 startPos = slideIn ? originalPos + offset : originalPos;
        Vector2 endPos = slideIn ? originalPos : originalPos + offset;

        target.anchoredPosition = startPos;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (slideIn)
                t = easeOutBack.Evaluate(t);
            else
                t = t * t; // EaseIn

            target.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        target.anchoredPosition = endPos;

        if (!slideIn)
            target.gameObject.SetActive(false);

        onComplete?.Invoke();
    }

    // ==================== 脉冲缩放 ====================

    /// <summary>
    /// 缩放脉冲效果（用于强调按钮、收集物等）
    /// </summary>
    public Coroutine Pulse(RectTransform target, float scaleMultiplier = 1.2f,
        float duration = 0.3f, int pulseCount = 1)
    {
        return StartCoroutine(PulseRoutine(target, scaleMultiplier, duration, pulseCount));
    }

    private IEnumerator PulseRoutine(RectTransform target, float scaleMultiplier,
        float duration, int pulseCount)
    {
        if (target == null) yield break;

        Vector3 originalScale = target.localScale;
        float halfDuration = duration / 2f;

        for (int i = 0; i < pulseCount; i++)
        {
            // 放大
            float elapsed = 0;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                t = Mathf.Sin(t * Mathf.PI * 0.5f); // EaseOutSine
                target.localScale = Vector3.Lerp(originalScale, originalScale * scaleMultiplier, t);
                yield return null;
            }

            // 缩回
            elapsed = 0;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                t = Mathf.Sin(t * Mathf.PI * 0.5f);
                target.localScale = Vector3.Lerp(originalScale * scaleMultiplier, originalScale, t);
                yield return null;
            }
        }

        target.localScale = originalScale;
    }

    // ==================== 摇晃 ====================

    /// <summary>
    /// UI元素摇晃（错误提示、受击反馈等）
    /// </summary>
    public Coroutine Shake(RectTransform target, float intensity = 10f,
        float duration = 0.3f, System.Action onComplete = null)
    {
        return StartCoroutine(ShakeRoutine(target, intensity, duration, onComplete));
    }

    private IEnumerator ShakeRoutine(RectTransform target, float intensity,
        float duration, System.Action onComplete)
    {
        if (target == null) yield break;

        Vector2 originalPos = target.anchoredPosition;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float damping = 1f - (elapsed / duration);
            float offsetX = Random.Range(-1f, 1f) * intensity * damping;
            float offsetY = Random.Range(-1f, 1f) * intensity * damping * 0.5f;
            target.anchoredPosition = originalPos + new Vector2(offsetX, offsetY);
            yield return null;
        }

        target.anchoredPosition = originalPos;
        onComplete?.Invoke();
    }

    // ==================== 数字滚动 ====================

    /// <summary>
    /// 数字从from滚动到to（用于分数、金币等）
    /// </summary>
    public Coroutine CountTo(TMPro.TextMeshProUGUI text, int from, int to,
        float duration = 0.5f, string format = "{0}")
    {
        return StartCoroutine(CountToRoutine(text, from, to, duration, format));
    }

    private IEnumerator CountToRoutine(TMPro.TextMeshProUGUI text, int from, int to,
        float duration, string format)
    {
        if (text == null) yield break;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // EaseOutQuad
            t = 1f - (1f - t) * (1f - t);
            int current = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
            text.text = string.Format(format, current);
            yield return null;
        }

        text.text = string.Format(format, to);
    }

    // ==================== 填充动画 ====================

    /// <summary>
    /// Image填充动画（血条、进度条等）
    /// </summary>
    public Coroutine FillTo(Image image, float targetFill, float duration = 0.3f,
        System.Action onComplete = null)
    {
        return StartCoroutine(FillToRoutine(image, targetFill, duration, onComplete));
    }

    private IEnumerator FillToRoutine(Image image, float targetFill, float duration,
        System.Action onComplete)
    {
        if (image == null) yield break;

        float startFill = image.fillAmount;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t); // SmoothStep
            image.fillAmount = Mathf.Lerp(startFill, targetFill, t);
            yield return null;
        }

        image.fillAmount = targetFill;
        onComplete?.Invoke();
    }

    // ==================== 颜色闪烁 ====================

    /// <summary>
    /// UI颜色闪烁（受伤红闪、拾取金色闪烁等）
    /// </summary>
    public Coroutine ColorFlash(Graphic target, Color flashColor,
        float duration = 0.2f, int flashCount = 2)
    {
        return StartCoroutine(ColorFlashRoutine(target, flashColor, duration, flashCount));
    }

    private IEnumerator ColorFlashRoutine(Graphic target, Color flashColor,
        float duration, int flashCount)
    {
        if (target == null) yield break;

        Color originalColor = target.color;
        float halfDuration = duration / (flashCount * 2);

        for (int i = 0; i < flashCount; i++)
        {
            // 闪到目标颜色
            float elapsed = 0;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / halfDuration;
                target.color = Color.Lerp(originalColor, flashColor, t);
                yield return null;
            }

            // 闪回原始颜色
            elapsed = 0;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / halfDuration;
                target.color = Color.Lerp(flashColor, originalColor, t);
                yield return null;
            }
        }

        target.color = originalColor;
    }

    // ==================== 打字机效果 ====================

    /// <summary>
    /// 逐字显示文本（对话系统、教程等）
    /// </summary>
    public Coroutine TypeText(TMPro.TextMeshProUGUI text, string fullText,
        float charDelay = 0.03f, System.Action onComplete = null)
    {
        return StartCoroutine(TypeTextRoutine(text, fullText, charDelay, onComplete));
    }

    private IEnumerator TypeTextRoutine(TMPro.TextMeshProUGUI text, string fullText,
        float charDelay, System.Action onComplete)
    {
        if (text == null) yield break;

        text.text = "";

        for (int i = 0; i < fullText.Length; i++)
        {
            text.text = fullText.Substring(0, i + 1);

            // 标点符号后稍微停顿
            if (fullText[i] == '。' || fullText[i] == '.' ||
                fullText[i] == '！' || fullText[i] == '!' ||
                fullText[i] == '？' || fullText[i] == '?')
            {
                yield return new WaitForSecondsRealtime(charDelay * 5f);
            }
            else if (fullText[i] == '，' || fullText[i] == ',' ||
                     fullText[i] == '、' || fullText[i] == ';')
            {
                yield return new WaitForSecondsRealtime(charDelay * 3f);
            }
            else
            {
                yield return new WaitForSecondsRealtime(charDelay);
            }
        }

        onComplete?.Invoke();
    }

    // ==================== 星级评分动画 ====================

    /// <summary>
    /// 依次亮起星星（关卡结算时使用）
    /// </summary>
    public Coroutine ShowStars(Image[] stars, int count, float delayBetween = 0.3f,
        System.Action onComplete = null)
    {
        return StartCoroutine(ShowStarsRoutine(stars, count, delayBetween, onComplete));
    }

    private IEnumerator ShowStarsRoutine(Image[] stars, int count, float delayBetween,
        System.Action onComplete)
    {
        if (stars == null) yield break;

        // 先全部暗淡
        foreach (var star in stars)
        {
            if (star != null)
                star.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        }

        yield return new WaitForSecondsRealtime(0.3f);

        // 依次亮起
        for (int i = 0; i < Mathf.Min(count, stars.Length); i++)
        {
            if (stars[i] == null) continue;

            var rt = stars[i].rectTransform;

            // 缩放弹入 + 颜色变亮
            rt.localScale = Vector3.one * 0.3f;
            stars[i].color = Color.white;

            float elapsed = 0;
            float animDuration = 0.25f;
            while (elapsed < animDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animDuration);
                float scale = easeOutElastic.Evaluate(t);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }

            rt.localScale = Vector3.one;

            // 播放星星音效
            if (AudioManager.Instance != null && SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play("star_earn");

            yield return new WaitForSecondsRealtime(delayBetween);
        }

        onComplete?.Invoke();
    }
}
