using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 场景过渡动画 - 提供多种过渡效果
/// 圆形遮罩、淡入淡出、滑动等
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    public enum TransitionType
    {
        Fade,           // 淡入淡出
        CircleWipe,     // 圆形遮罩
        SlideLeft,      // 向左滑动
        SlideRight,     // 向右滑动
        DiamondWipe     // 菱形遮罩
    }

    [Header("UI组件")]
    [SerializeField] private CanvasGroup fadePanel;
    [SerializeField] private Image wipeImage;
    [SerializeField] private RectTransform slidePanel;

    [Header("过渡材质")]
    [SerializeField] private Material circleWipeMaterial;
    [SerializeField] private Material diamondWipeMaterial;

    [Header("设置")]
    [SerializeField] private float transitionDuration = 0.5f;
    [SerializeField] private TransitionType defaultType = TransitionType.Fade;
    [SerializeField] private Color fadeColor = Color.black;

    private bool isTransitioning;

    public bool IsTransitioning => isTransitioning;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 初始隐藏
        if (fadePanel != null) { fadePanel.alpha = 0; fadePanel.gameObject.SetActive(false); }
        if (wipeImage != null) wipeImage.gameObject.SetActive(false);
        if (slidePanel != null) slidePanel.gameObject.SetActive(false);
    }

    /// <summary>
    /// 播放过渡入场动画（画面变黑）
    /// </summary>
    public Coroutine TransitionIn(TransitionType? type = null)
    {
        return StartCoroutine(DoTransitionIn(type ?? defaultType));
    }

    /// <summary>
    /// 播放过渡出场动画（画面恢复）
    /// </summary>
    public Coroutine TransitionOut(TransitionType? type = null)
    {
        return StartCoroutine(DoTransitionOut(type ?? defaultType));
    }

    /// <summary>
    /// 完整过渡：入场 → 执行回调 → 出场
    /// </summary>
    public Coroutine DoFullTransition(System.Action onMidpoint, TransitionType? type = null)
    {
        return StartCoroutine(FullTransitionCoroutine(onMidpoint, type ?? defaultType));
    }

    private IEnumerator FullTransitionCoroutine(System.Action onMidpoint, TransitionType type)
    {
        yield return DoTransitionIn(type);
        onMidpoint?.Invoke();
        yield return new WaitForSecondsRealtime(0.1f);
        yield return DoTransitionOut(type);
    }

    private IEnumerator DoTransitionIn(TransitionType type)
    {
        if (isTransitioning) yield break;
        isTransitioning = true;

        switch (type)
        {
            case TransitionType.Fade:
                yield return FadeTransition(0f, 1f);
                break;
            case TransitionType.CircleWipe:
                yield return WipeTransition(circleWipeMaterial, 1f, 0f);
                break;
            case TransitionType.DiamondWipe:
                yield return WipeTransition(diamondWipeMaterial, 1f, 0f);
                break;
            case TransitionType.SlideLeft:
                yield return SlideTransition(Vector2.right, Vector2.zero);
                break;
            case TransitionType.SlideRight:
                yield return SlideTransition(Vector2.left, Vector2.zero);
                break;
        }

        isTransitioning = false;
    }

    private IEnumerator DoTransitionOut(TransitionType type)
    {
        if (isTransitioning) yield break;
        isTransitioning = true;

        switch (type)
        {
            case TransitionType.Fade:
                yield return FadeTransition(1f, 0f);
                break;
            case TransitionType.CircleWipe:
                yield return WipeTransition(circleWipeMaterial, 0f, 1f);
                break;
            case TransitionType.DiamondWipe:
                yield return WipeTransition(diamondWipeMaterial, 0f, 1f);
                break;
            case TransitionType.SlideLeft:
                yield return SlideTransition(Vector2.zero, Vector2.left);
                break;
            case TransitionType.SlideRight:
                yield return SlideTransition(Vector2.zero, Vector2.right);
                break;
        }

        isTransitioning = false;
    }

    private IEnumerator FadeTransition(float from, float to)
    {
        if (fadePanel == null) yield break;

        fadePanel.gameObject.SetActive(true);
        fadePanel.alpha = from;

        float t = 0;
        while (t < transitionDuration)
        {
            t += Time.unscaledDeltaTime;
            fadePanel.alpha = Mathf.Lerp(from, to, EaseInOutQuad(t / transitionDuration));
            yield return null;
        }

        fadePanel.alpha = to;
        if (to <= 0) fadePanel.gameObject.SetActive(false);
    }

    private IEnumerator WipeTransition(Material mat, float from, float to)
    {
        if (wipeImage == null || mat == null) yield break;

        wipeImage.gameObject.SetActive(true);
        wipeImage.material = mat;

        float t = 0;
        while (t < transitionDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Lerp(from, to, EaseInOutQuad(t / transitionDuration));
            mat.SetFloat("_Progress", progress);
            yield return null;
        }

        mat.SetFloat("_Progress", to);
        if (to >= 1f) wipeImage.gameObject.SetActive(false);
    }

    private IEnumerator SlideTransition(Vector2 fromNorm, Vector2 toNorm)
    {
        if (slidePanel == null) yield break;

        slidePanel.gameObject.SetActive(true);
        var parentRect = slidePanel.parent as RectTransform;
        if (parentRect == null) yield break;

        float width = parentRect.rect.width;
        float height = parentRect.rect.height;

        Vector2 from = new Vector2(fromNorm.x * width, fromNorm.y * height);
        Vector2 to = new Vector2(toNorm.x * width, toNorm.y * height);

        float t = 0;
        while (t < transitionDuration)
        {
            t += Time.unscaledDeltaTime;
            slidePanel.anchoredPosition = Vector2.Lerp(from, to, EaseInOutQuad(t / transitionDuration));
            yield return null;
        }

        slidePanel.anchoredPosition = to;
        bool offScreen = Mathf.Abs(to.x) > 0 || Mathf.Abs(to.y) > 0;
        if (offScreen) slidePanel.gameObject.SetActive(false);
    }

    private float EaseInOutQuad(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }
}
