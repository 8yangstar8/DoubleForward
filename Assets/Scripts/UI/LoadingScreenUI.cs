using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 加载界面 - 显示进度条、随机提示、角色动画
/// 支持场景异步加载期间的过渡展示
/// </summary>
public class LoadingScreenUI : MonoBehaviour
{
    public static LoadingScreenUI Instance { get; private set; }

    [Header("UI元素")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI tipText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("提示文本键")]
    [SerializeField] private string[] tipKeys = new string[]
    {
        "loading_tip_1", "loading_tip_2", "loading_tip_3",
        "loading_tip_4", "loading_tip_5"
    };

    [Header("背景图片（按章节）")]
    [SerializeField] private Sprite[] chapterBackgrounds;

    [Header("动画设置")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float minDisplayTime = 1.5f;
    [SerializeField] private float tipChangeInterval = 4f;

    [Header("进度条动画")]
    [SerializeField] private float progressSmoothSpeed = 3f;

    private float targetProgress;
    private float displayProgress;
    private bool isLoading;
    private Coroutine tipCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (loadingPanel != null)
            loadingPanel.SetActive(false);
    }

    void OnEnable()
    {
        // 自动注册到SceneLoader事件
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.OnLoadStart += OnSceneLoadStart;
            SceneLoader.Instance.OnLoadProgress += SetProgress;
            SceneLoader.Instance.OnLoadFinished += OnSceneLoadFinished;
        }
    }

    void OnDisable()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.OnLoadStart -= OnSceneLoadStart;
            SceneLoader.Instance.OnLoadProgress -= SetProgress;
            SceneLoader.Instance.OnLoadFinished -= OnSceneLoadFinished;
        }
    }

    private void OnSceneLoadStart(int chapterIndex)
    {
        Show(chapterIndex);
    }

    private void OnSceneLoadFinished()
    {
        Hide();
    }

    /// <summary>
    /// 显示加载界面
    /// </summary>
    public void Show(int chapterIndex = -1)
    {
        if (loadingPanel == null) return;

        isLoading = true;
        targetProgress = 0;
        displayProgress = 0;
        loadingPanel.SetActive(true);

        // 设置章节背景
        if (backgroundImage != null && chapterBackgrounds != null &&
            chapterIndex >= 0 && chapterIndex < chapterBackgrounds.Length)
        {
            backgroundImage.sprite = chapterBackgrounds[chapterIndex];
        }

        UpdateProgressUI(0);
        ShowRandomTip();

        // 开始循环显示提示
        if (tipCoroutine != null) StopCoroutine(tipCoroutine);
        tipCoroutine = StartCoroutine(CycleTips());

        // 淡入
        StartCoroutine(FadeIn());
    }

    /// <summary>
    /// 隐藏加载界面
    /// </summary>
    public void Hide()
    {
        if (loadingPanel == null || !isLoading) return;

        isLoading = false;
        if (tipCoroutine != null)
        {
            StopCoroutine(tipCoroutine);
            tipCoroutine = null;
        }

        StartCoroutine(FadeOutAndHide());
    }

    /// <summary>
    /// 更新加载进度 (0~1)
    /// </summary>
    public void SetProgress(float progress)
    {
        targetProgress = Mathf.Clamp01(progress);
    }

    void Update()
    {
        if (!isLoading) return;

        // 平滑进度条
        if (displayProgress < targetProgress)
        {
            displayProgress = Mathf.MoveTowards(displayProgress, targetProgress,
                progressSmoothSpeed * Time.unscaledDeltaTime);
            UpdateProgressUI(displayProgress);
        }
    }

    private void UpdateProgressUI(float progress)
    {
        if (progressBar != null)
            progressBar.value = progress;

        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
    }

    private void ShowRandomTip()
    {
        if (tipText == null || tipKeys.Length == 0) return;

        string key = tipKeys[Random.Range(0, tipKeys.Length)];

        if (LocalizationSystem.Instance != null)
            tipText.text = LocalizationSystem.Instance.Get(key, key);
        else
            tipText.text = key;
    }

    private IEnumerator CycleTips()
    {
        while (isLoading)
        {
            yield return new WaitForSecondsRealtime(tipChangeInterval);
            if (isLoading) ShowRandomTip();
        }
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null) yield break;

        canvasGroup.alpha = 0;
        float t = 0;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(0, 1, t / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1;
    }

    private IEnumerator FadeOutAndHide()
    {
        // 等待最小显示时间
        yield return new WaitForSecondsRealtime(minDisplayTime);

        // 填满进度条
        targetProgress = 1f;
        while (displayProgress < 0.99f)
        {
            displayProgress = Mathf.MoveTowards(displayProgress, 1f,
                progressSmoothSpeed * 2f * Time.unscaledDeltaTime);
            UpdateProgressUI(displayProgress);
            yield return null;
        }
        UpdateProgressUI(1f);

        yield return new WaitForSecondsRealtime(0.3f);

        // 淡出
        if (canvasGroup != null)
        {
            float t = 0;
            while (t < fadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(1, 0, t / fadeOutDuration);
                yield return null;
            }
            canvasGroup.alpha = 0;
        }

        if (loadingPanel != null)
            loadingPanel.SetActive(false);
    }
}
