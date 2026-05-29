using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 拍照模式 - 游戏内截图功能
/// 隐藏UI、应用滤镜、添加水印、保存/分享
/// </summary>
public class PhotoMode : MonoBehaviour
{
    public static PhotoMode Instance { get; private set; }

    [Header("UI面板")]
    [SerializeField] private GameObject photoModePanel;
    [SerializeField] private GameObject controlsHint;
    [SerializeField] private Button captureButton;
    [SerializeField] private Button filterButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button shareButton;
    [SerializeField] private TextMeshProUGUI filterNameText;

    [Header("预览")]
    [SerializeField] private RawImage previewImage;
    [SerializeField] private GameObject previewPanel;
    [SerializeField] private Image flashOverlay;

    [Header("水印")]
    [SerializeField] private bool addWatermark = true;
    [SerializeField] private Texture2D watermarkTexture;
    [SerializeField] private Vector2 watermarkPosition = new Vector2(0.95f, 0.05f); // 右下角
    [SerializeField] private float watermarkScale = 0.1f;

    [Header("滤镜")]
    [SerializeField] private PhotoFilter[] filters;

    [System.Serializable]
    public class PhotoFilter
    {
        public string name;
        public string displayKey;  // 本地化key
        public Color tintColor = Color.white;
        public float brightness = 1f;
        public float contrast = 1f;
        public float saturation = 1f;
        public float vignette = 0f;
    }

    // 默认滤镜
    private static readonly PhotoFilter[] defaultFilters = new PhotoFilter[]
    {
        new PhotoFilter { name = "None", displayKey = "filter_none", tintColor = Color.white },
        new PhotoFilter { name = "Warm", displayKey = "filter_warm", tintColor = new Color(1f, 0.95f, 0.85f), brightness = 1.05f },
        new PhotoFilter { name = "Cool", displayKey = "filter_cool", tintColor = new Color(0.85f, 0.9f, 1f), saturation = 0.9f },
        new PhotoFilter { name = "Vintage", displayKey = "filter_vintage", tintColor = new Color(1f, 0.9f, 0.75f), saturation = 0.6f, contrast = 1.1f, vignette = 0.4f },
        new PhotoFilter { name = "BW", displayKey = "filter_bw", saturation = 0f, contrast = 1.2f },
        new PhotoFilter { name = "Dramatic", displayKey = "filter_dramatic", contrast = 1.3f, vignette = 0.5f, brightness = 0.95f },
        new PhotoFilter { name = "Neon", displayKey = "filter_neon", tintColor = new Color(0.9f, 0.85f, 1f), brightness = 1.1f, saturation = 1.3f },
    };

    // 状态
    private bool isActive;
    private int currentFilterIndex;
    private Texture2D capturedPhoto;
    private Canvas[] hiddenCanvases;
    private string lastSavedPath;

    // 缓存
    private float originalTimeScale;

    public bool IsActive => isActive;

    public event System.Action OnPhotoModeEntered;
    public event System.Action OnPhotoModeExited;
    public event System.Action<string> OnPhotoCaptured; // filePath

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (filters == null || filters.Length == 0)
            filters = defaultFilters;

        if (captureButton != null) captureButton.onClick.AddListener(CapturePhoto);
        if (filterButton != null) filterButton.onClick.AddListener(CycleFilter);
        if (exitButton != null) exitButton.onClick.AddListener(ExitPhotoMode);
        if (shareButton != null) shareButton.onClick.AddListener(ShareLastPhoto);

        if (photoModePanel != null) photoModePanel.SetActive(false);
        if (previewPanel != null) previewPanel.SetActive(false);
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 进入拍照模式
    /// </summary>
    public void EnterPhotoMode()
    {
        if (isActive) return;

        isActive = true;
        currentFilterIndex = 0;

        // 暂停游戏
        originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        // 隐藏游戏UI（保留拍照UI）
        HideGameUI();

        // 显示拍照控件
        if (photoModePanel != null) photoModePanel.SetActive(true);
        if (previewPanel != null) previewPanel.SetActive(false);

        UpdateFilterDisplay();
        OnPhotoModeEntered?.Invoke();

        if (AnalyticsTracker.Instance != null)
            AnalyticsTracker.Instance.TrackEvent("photo_mode_entered");
    }

    /// <summary>
    /// 退出拍照模式
    /// </summary>
    public void ExitPhotoMode()
    {
        if (!isActive) return;

        isActive = false;

        // 恢复游戏
        Time.timeScale = originalTimeScale;

        // 恢复UI
        ShowGameUI();

        // 隐藏拍照控件
        if (photoModePanel != null) photoModePanel.SetActive(false);
        if (previewPanel != null) previewPanel.SetActive(false);

        // 清除滤镜
        RemovePostProcessFilter();

        // 清除缓存
        if (capturedPhoto != null)
        {
            Destroy(capturedPhoto);
            capturedPhoto = null;
        }

        OnPhotoModeExited?.Invoke();
    }

    /// <summary>
    /// 拍照
    /// </summary>
    public void CapturePhoto()
    {
        StartCoroutine(CaptureRoutine());
    }

    /// <summary>
    /// 切换滤镜
    /// </summary>
    public void CycleFilter()
    {
        currentFilterIndex = (currentFilterIndex + 1) % filters.Length;
        ApplyPostProcessFilter(filters[currentFilterIndex]);
        UpdateFilterDisplay();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    /// <summary>
    /// 分享最后拍的照片
    /// </summary>
    public void ShareLastPhoto()
    {
        if (string.IsNullOrEmpty(lastSavedPath) || !File.Exists(lastSavedPath)) return;

        if (MobileServices.Instance != null)
            MobileServices.Instance.ShareScreenshot(lastSavedPath);
    }

    // ==================== 内部实现 ====================

    private IEnumerator CaptureRoutine()
    {
        // 临时隐藏拍照UI
        if (photoModePanel != null) photoModePanel.SetActive(false);

        // 闪光效果
        if (flashOverlay != null)
        {
            flashOverlay.gameObject.SetActive(true);
            flashOverlay.color = new Color(1, 1, 1, 0.8f);
        }

        // 等待渲染
        yield return new WaitForEndOfFrame();

        // 截图
        int width = Screen.width;
        int height = Screen.height;

        capturedPhoto = new Texture2D(width, height, TextureFormat.RGB24, false);
        capturedPhoto.ReadPixels(new Rect(0, 0, width, height), 0, 0);

        // 应用滤镜到截图
        if (currentFilterIndex > 0)
            ApplyFilterToTexture(capturedPhoto, filters[currentFilterIndex]);

        // 添加水印
        if (addWatermark && watermarkTexture != null)
            ApplyWatermark(capturedPhoto);

        capturedPhoto.Apply();

        // 保存
        lastSavedPath = SavePhotoToFile(capturedPhoto);

        // 显示预览
        if (previewImage != null)
            previewImage.texture = capturedPhoto;
        if (previewPanel != null)
            previewPanel.SetActive(true);

        // 恢复拍照UI
        if (photoModePanel != null) photoModePanel.SetActive(true);

        // 闪光消退
        if (flashOverlay != null)
        {
            float t = 0;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                flashOverlay.color = new Color(1, 1, 1, Mathf.Lerp(0.8f, 0, t / 0.3f));
                yield return null;
            }
            flashOverlay.gameObject.SetActive(false);
        }

        // 快门音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("camera_shutter");

        OnPhotoCaptured?.Invoke(lastSavedPath);

        if (AnalyticsTracker.Instance != null)
            AnalyticsTracker.Instance.TrackEvent("photo_captured",
                ("filter", filters[currentFilterIndex].name));
    }

    private string SavePhotoToFile(Texture2D photo)
    {
        string dir = Path.Combine(Application.persistentDataPath, "Screenshots");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"DoubleForward_{timestamp}.png";
        string path = Path.Combine(dir, filename);

        byte[] bytes = photo.EncodeToPNG();
        File.WriteAllBytes(path, bytes);

        Debug.Log($"[PhotoMode] Photo saved: {path}");
        return path;
    }

    private void ApplyFilterToTexture(Texture2D tex, PhotoFilter filter)
    {
        Color[] pixels = tex.GetPixels();

        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];

            // 色调
            c *= filter.tintColor;

            // 亮度
            c *= filter.brightness;

            // 饱和度
            float gray = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            c.r = Mathf.Lerp(gray, c.r, filter.saturation);
            c.g = Mathf.Lerp(gray, c.g, filter.saturation);
            c.b = Mathf.Lerp(gray, c.b, filter.saturation);

            // 对比度
            c.r = (c.r - 0.5f) * filter.contrast + 0.5f;
            c.g = (c.g - 0.5f) * filter.contrast + 0.5f;
            c.b = (c.b - 0.5f) * filter.contrast + 0.5f;

            // 暗角（简单径向暗化）
            if (filter.vignette > 0)
            {
                int x = i % tex.width;
                int y = i / tex.width;
                float dx = (x / (float)tex.width - 0.5f) * 2f;
                float dy = (y / (float)tex.height - 0.5f) * 2f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float vig = 1f - Mathf.Clamp01(dist * filter.vignette);
                c *= vig;
            }

            c.r = Mathf.Clamp01(c.r);
            c.g = Mathf.Clamp01(c.g);
            c.b = Mathf.Clamp01(c.b);

            pixels[i] = c;
        }

        tex.SetPixels(pixels);
    }

    private void ApplyWatermark(Texture2D photo)
    {
        if (watermarkTexture == null) return;

        int wmWidth = Mathf.RoundToInt(photo.width * watermarkScale);
        int wmHeight = Mathf.RoundToInt(wmWidth * ((float)watermarkTexture.height / watermarkTexture.width));

        int startX = Mathf.RoundToInt(photo.width * watermarkPosition.x) - wmWidth;
        int startY = Mathf.RoundToInt(photo.height * watermarkPosition.y);

        startX = Mathf.Clamp(startX, 0, photo.width - wmWidth);
        startY = Mathf.Clamp(startY, 0, photo.height - wmHeight);

        for (int y = 0; y < wmHeight; y++)
        {
            for (int x = 0; x < wmWidth; x++)
            {
                float u = x / (float)wmWidth;
                float v = y / (float)wmHeight;

                Color wmColor = watermarkTexture.GetPixelBilinear(u, v);
                if (wmColor.a < 0.1f) continue;

                int px = startX + x;
                int py = startY + y;

                Color bgColor = photo.GetPixel(px, py);
                Color blended = Color.Lerp(bgColor, wmColor, wmColor.a * 0.5f);
                photo.SetPixel(px, py, blended);
            }
        }
    }

    // ==================== UI管理 ====================

    private void HideGameUI()
    {
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var toHide = new System.Collections.Generic.List<Canvas>();

        foreach (var canvas in allCanvases)
        {
            // 不隐藏拍照模式自身的Canvas
            if (photoModePanel != null && canvas.gameObject == photoModePanel)
                continue;
            if (canvas.transform.IsChildOf(transform))
                continue;

            if (canvas.enabled)
            {
                canvas.enabled = false;
                toHide.Add(canvas);
            }
        }

        hiddenCanvases = toHide.ToArray();
    }

    private void ShowGameUI()
    {
        if (hiddenCanvases == null) return;

        foreach (var canvas in hiddenCanvases)
        {
            if (canvas != null)
                canvas.enabled = true;
        }

        hiddenCanvases = null;
    }

    private void UpdateFilterDisplay()
    {
        if (filterNameText == null) return;

        var filter = filters[currentFilterIndex];
        if (LocalizationSystem.Instance != null && !string.IsNullOrEmpty(filter.displayKey))
            filterNameText.text = LocalizationSystem.Instance.Get(filter.displayKey, filter.name);
        else
            filterNameText.text = filter.name;
    }

    private void ApplyPostProcessFilter(PhotoFilter filter)
    {
        // 通过事件通知Camera系统应用滤镜（避免跨程序集直接引用）
        EventBus.Publish(new PhotoFilterChangedEvent
        {
            tintColor = filter.tintColor,
            vignette = filter.vignette,
            brightness = filter.brightness,
            saturation = filter.saturation
        });
    }

    private void RemovePostProcessFilter()
    {
        EventBus.Publish(new PhotoFilterChangedEvent
        {
            tintColor = Color.white,
            vignette = 0f,
            brightness = 1f,
            saturation = 1f
        });
    }

    void OnDestroy()
    {
        if (capturedPhoto != null)
            Destroy(capturedPhoto);
    }
}
