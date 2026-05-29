using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 屏幕过渡UI - 场景切换时的视觉过渡效果
/// 自动注入到SceneLoader的过渡委托中
/// 支持多种过渡效果：淡入淡出、圆形擦除、菱形擦除
/// </summary>
public class ScreenTransitionUI : MonoBehaviour
{
    public static ScreenTransitionUI Instance { get; private set; }

    [Header("过渡遮罩")]
    [SerializeField] private CanvasGroup transitionCanvas;
    [SerializeField] private Image fadeImage;
    [SerializeField] private Image wipeImage; // 用于圆形/菱形擦除的遮罩

    [Header("过渡设置")]
    [SerializeField] private TransitionType defaultTransition = TransitionType.Fade;
    [SerializeField] private float transitionDuration = 0.5f;
    [SerializeField] private Color fadeColor = Color.black;

    [Header("加载画面")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMPro.TextMeshProUGUI loadingText;
    [SerializeField] private TMPro.TextMeshProUGUI tipText;

    [Header("提示文本")]
    [SerializeField] private string[] loadingTips;

    public enum TransitionType
    {
        Fade,           // 黑屏淡入淡出
        CircleWipe,     // 圆形擦除
        DiamondWipe,    // 菱形擦除
        SlideLeft,      // 向左滑入
        SlideRight      // 向右滑入
    }

    private Material wipeMaterial;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (transitionCanvas != null)
        {
            transitionCanvas.alpha = 0;
            transitionCanvas.gameObject.SetActive(false);
        }

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        // 注入到SceneLoader
        RegisterWithSceneLoader();
    }

    void Start()
    {
        RegisterWithSceneLoader();
    }

    private void RegisterWithSceneLoader()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.TransitionInFunc = () => StartCoroutine(TransitionIn());
            SceneLoader.Instance.TransitionOutFunc = () => StartCoroutine(TransitionOut());
            SceneLoader.Instance.OnLoadProgress += UpdateProgress;
            SceneLoader.Instance.OnLoadStart += OnLoadStart;
            SceneLoader.Instance.OnLoadFinished += OnLoadFinished;
        }
    }

    // ==================== 过渡动画 ====================

    private IEnumerator TransitionIn()
    {
        switch (defaultTransition)
        {
            case TransitionType.Fade:
                yield return FadeIn();
                break;
            case TransitionType.CircleWipe:
                yield return CircleWipeIn();
                break;
            case TransitionType.DiamondWipe:
                yield return DiamondWipeIn();
                break;
            case TransitionType.SlideLeft:
            case TransitionType.SlideRight:
                yield return SlideIn();
                break;
        }
    }

    private IEnumerator TransitionOut()
    {
        switch (defaultTransition)
        {
            case TransitionType.Fade:
                yield return FadeOut();
                break;
            case TransitionType.CircleWipe:
                yield return CircleWipeOut();
                break;
            case TransitionType.DiamondWipe:
                yield return DiamondWipeOut();
                break;
            case TransitionType.SlideLeft:
            case TransitionType.SlideRight:
                yield return SlideOut();
                break;
        }
    }

    // ---- 淡入淡出 ----

    private IEnumerator FadeIn()
    {
        if (transitionCanvas == null) yield break;

        if (fadeImage != null)
            fadeImage.color = fadeColor;

        transitionCanvas.gameObject.SetActive(true);
        float elapsed = 0;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            transitionCanvas.alpha = Mathf.Clamp01(elapsed / transitionDuration);
            yield return null;
        }

        transitionCanvas.alpha = 1f;
    }

    private IEnumerator FadeOut()
    {
        if (transitionCanvas == null) yield break;

        float elapsed = 0;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            transitionCanvas.alpha = 1f - Mathf.Clamp01(elapsed / transitionDuration);
            yield return null;
        }

        transitionCanvas.alpha = 0;
        transitionCanvas.gameObject.SetActive(false);
    }

    // ---- 圆形擦除 ----

    private IEnumerator CircleWipeIn()
    {
        if (wipeImage == null || wipeImage.material == null)
        {
            yield return FadeIn();
            yield break;
        }

        wipeMaterial = wipeImage.material;
        transitionCanvas.gameObject.SetActive(true);
        transitionCanvas.alpha = 1f;
        wipeImage.gameObject.SetActive(true);

        float elapsed = 0;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            wipeMaterial.SetFloat("_Cutoff", 1f - t); // 从1到0 = 从全显到全遮
            yield return null;
        }

        wipeMaterial.SetFloat("_Cutoff", 0f);
    }

    private IEnumerator CircleWipeOut()
    {
        if (wipeImage == null || wipeImage.material == null)
        {
            yield return FadeOut();
            yield break;
        }

        float elapsed = 0;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            wipeMaterial.SetFloat("_Cutoff", t); // 从0到1 = 从全遮到全显
            yield return null;
        }

        wipeMaterial.SetFloat("_Cutoff", 1f);
        wipeImage.gameObject.SetActive(false);
        transitionCanvas.gameObject.SetActive(false);
    }

    // ---- 菱形擦除（使用相同Shader不同遮罩纹理） ----

    private IEnumerator DiamondWipeIn()
    {
        yield return CircleWipeIn(); // 共享实现，不同遮罩纹理由Material配置
    }

    private IEnumerator DiamondWipeOut()
    {
        yield return CircleWipeOut();
    }

    // ---- 滑动 ----

    private IEnumerator SlideIn()
    {
        if (fadeImage == null)
        {
            yield return FadeIn();
            yield break;
        }

        transitionCanvas.gameObject.SetActive(true);
        transitionCanvas.alpha = 1f;

        var rt = fadeImage.rectTransform;
        bool slideLeft = defaultTransition == TransitionType.SlideLeft;
        float screenWidth = rt.rect.width;
        Vector2 startPos = new Vector2(slideLeft ? -screenWidth : screenWidth, 0);
        Vector2 endPos = Vector2.zero;

        rt.anchoredPosition = startPos;
        fadeImage.color = fadeColor;

        float elapsed = 0;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            t = t * t * (3f - 2f * t); // SmoothStep
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        rt.anchoredPosition = endPos;
    }

    private IEnumerator SlideOut()
    {
        if (fadeImage == null)
        {
            yield return FadeOut();
            yield break;
        }

        var rt = fadeImage.rectTransform;
        bool slideLeft = defaultTransition == TransitionType.SlideLeft;
        float screenWidth = rt.rect.width;
        Vector2 startPos = Vector2.zero;
        Vector2 endPos = new Vector2(slideLeft ? screenWidth : -screenWidth, 0);

        float elapsed = 0;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            t = t * t * (3f - 2f * t);
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        transitionCanvas.gameObject.SetActive(false);
        rt.anchoredPosition = Vector2.zero;
    }

    // ==================== 加载画面 ====================

    private void OnLoadStart(int chapterIndex)
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);

            if (progressBar != null)
                progressBar.value = 0;

            if (loadingText != null)
                loadingText.text = LocalizationSystem.Instance != null
                    ? LocalizationSystem.Instance.Get("loading", "Loading...")
                    : "Loading...";

            // 显示随机提示
            if (tipText != null && loadingTips != null && loadingTips.Length > 0)
            {
                string tipKey = loadingTips[Random.Range(0, loadingTips.Length)];
                tipText.text = LocalizationSystem.Instance != null
                    ? LocalizationSystem.Instance.Get(tipKey, tipKey)
                    : tipKey;
            }
        }
    }

    private void UpdateProgress(float progress)
    {
        if (progressBar != null)
            progressBar.value = progress;

        if (loadingText != null)
            loadingText.text = $"{Mathf.RoundToInt(progress * 100)}%";
    }

    private void OnLoadFinished()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 设置过渡类型（下次加载生效）
    /// </summary>
    public void SetTransitionType(TransitionType type)
    {
        defaultTransition = type;
    }

    /// <summary>
    /// 手动执行淡入（不经SceneLoader时使用）
    /// </summary>
    public Coroutine ManualFadeIn(float duration = -1f)
    {
        if (duration > 0) transitionDuration = duration;
        return StartCoroutine(FadeIn());
    }

    /// <summary>
    /// 手动执行淡出
    /// </summary>
    public Coroutine ManualFadeOut(float duration = -1f)
    {
        if (duration > 0) transitionDuration = duration;
        return StartCoroutine(FadeOut());
    }

    void OnDestroy()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.OnLoadProgress -= UpdateProgress;
            SceneLoader.Instance.OnLoadStart -= OnLoadStart;
            SceneLoader.Instance.OnLoadFinished -= OnLoadFinished;
        }
    }
}
