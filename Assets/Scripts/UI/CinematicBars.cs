using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 电影黑边效果 - Boss登场、过场动画时的上下黑边
/// 支持渐入渐出、自定义比例、配合TimeManager暂停
/// </summary>
public class CinematicBars : MonoBehaviour
{
    public static CinematicBars Instance { get; private set; }

    [Header("UI引用")]
    [SerializeField] private RectTransform topBar;
    [SerializeField] private RectTransform bottomBar;
    [SerializeField] private Image topBarImage;
    [SerializeField] private Image bottomBarImage;

    [Header("默认设置")]
    [SerializeField] private float defaultBarHeight = 80f;
    [SerializeField] private float defaultTransitionDuration = 0.5f;
    [SerializeField] private Color barColor = Color.black;

    [Header("宽银幕比例")]
    [SerializeField] private float cinematicAspect = 2.35f; // 21:9
    [SerializeField] private bool autoCalculateHeight = true;

    private Coroutine currentTransition;
    private bool isShowing;
    private float currentBarHeight;
    private Canvas parentCanvas;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        parentCanvas = GetComponentInParent<Canvas>();
        SetupBars();
    }

    private void SetupBars()
    {
        if (autoCalculateHeight && parentCanvas != null)
        {
            var rt = parentCanvas.GetComponent<RectTransform>();
            if (rt != null)
            {
                float screenWidth = rt.rect.width;
                float screenHeight = rt.rect.height;
                float currentAspect = screenWidth / screenHeight;

                if (currentAspect < cinematicAspect)
                {
                    // 需要裁切上下以达到宽银幕比例
                    float targetHeight = screenWidth / cinematicAspect;
                    defaultBarHeight = (screenHeight - targetHeight) / 2f;
                }
            }
        }

        // 初始隐藏
        if (topBar != null)
        {
            topBar.anchorMin = new Vector2(0, 1);
            topBar.anchorMax = new Vector2(1, 1);
            topBar.pivot = new Vector2(0.5f, 1);
            topBar.sizeDelta = new Vector2(0, 0);
        }

        if (bottomBar != null)
        {
            bottomBar.anchorMin = new Vector2(0, 0);
            bottomBar.anchorMax = new Vector2(1, 0);
            bottomBar.pivot = new Vector2(0.5f, 0);
            bottomBar.sizeDelta = new Vector2(0, 0);
        }

        if (topBarImage != null) topBarImage.color = barColor;
        if (bottomBarImage != null) bottomBarImage.color = barColor;

        isShowing = false;
        currentBarHeight = 0f;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 显示电影黑边
    /// </summary>
    public void Show(float duration = -1f)
    {
        float d = duration < 0 ? defaultTransitionDuration : duration;
        TransitionTo(defaultBarHeight, d);
        isShowing = true;
    }

    /// <summary>
    /// 隐藏电影黑边
    /// </summary>
    public void Hide(float duration = -1f)
    {
        float d = duration < 0 ? defaultTransitionDuration : duration;
        TransitionTo(0f, d);
        isShowing = false;
    }

    /// <summary>
    /// 显示指定高度的黑边
    /// </summary>
    public void ShowWithHeight(float height, float duration = -1f)
    {
        float d = duration < 0 ? defaultTransitionDuration : duration;
        TransitionTo(height, d);
        isShowing = true;
    }

    /// <summary>
    /// 立即显示/隐藏（无动画）
    /// </summary>
    public void SetImmediate(bool show)
    {
        if (currentTransition != null)
            StopCoroutine(currentTransition);

        float target = show ? defaultBarHeight : 0f;
        SetBarHeight(target);
        isShowing = show;
    }

    /// <summary>
    /// 电影模式 - 黑边 + 暂停游戏输入
    /// </summary>
    public void EnterCinematicMode(float duration = -1f)
    {
        Show(duration);

        // 禁用玩家输入
        if (InputManager.Instance != null)
            InputManager.Instance.SetInputEnabled(false);
    }

    /// <summary>
    /// 退出电影模式
    /// </summary>
    public void ExitCinematicMode(float duration = -1f)
    {
        Hide(duration);

        // 恢复玩家输入
        if (InputManager.Instance != null)
            InputManager.Instance.SetInputEnabled(true);
    }

    /// <summary>
    /// Boss登场演出（黑边 + 摄像机聚焦 + 减速）
    /// </summary>
    public void PlayBossIntro(Transform bossTransform, float introDuration = 3f)
    {
        StartCoroutine(BossIntroSequence(bossTransform, introDuration));
    }

    public bool IsShowing => isShowing;

    // ==================== 内部实现 ====================

    private void TransitionTo(float targetHeight, float duration)
    {
        if (currentTransition != null)
            StopCoroutine(currentTransition);

        currentTransition = StartCoroutine(AnimateBars(targetHeight, duration));
    }

    private IEnumerator AnimateBars(float targetHeight, float duration)
    {
        float startHeight = currentBarHeight;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // 不受Time.timeScale影响
            float t = elapsed / duration;
            t = EaseInOutQuad(t);

            float height = Mathf.Lerp(startHeight, targetHeight, t);
            SetBarHeight(height);

            yield return null;
        }

        SetBarHeight(targetHeight);
        currentTransition = null;
    }

    private void SetBarHeight(float height)
    {
        currentBarHeight = height;

        if (topBar != null)
            topBar.sizeDelta = new Vector2(0, height);

        if (bottomBar != null)
            bottomBar.sizeDelta = new Vector2(0, height);
    }

    private IEnumerator BossIntroSequence(Transform boss, float duration)
    {
        // 1. 黑边滑入
        EnterCinematicMode(0.4f);
        yield return new WaitForSecondsRealtime(0.5f);

        // 2. 减速效果
        if (TimeManager.Instance != null)
            TimeManager.Instance.SlowMotion(0.3f, duration * 0.6f);

        // 3. 摄像机聚焦Boss
        if (CameraController.Instance != null && boss != null)
            CameraController.Instance.FocusOnTarget(boss, duration * 0.7f);

        yield return new WaitForSecondsRealtime(duration * 0.7f);

        // 4. 摄像机回归
        if (CameraController.Instance != null)
            CameraController.Instance.ReturnToPlayers();

        yield return new WaitForSecondsRealtime(duration * 0.3f);

        // 5. 黑边退出
        ExitCinematicMode(0.4f);
    }

    private float EaseInOutQuad(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
