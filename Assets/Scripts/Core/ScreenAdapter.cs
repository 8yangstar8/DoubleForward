using UnityEngine;

/// <summary>
/// 屏幕适配管理器 - 处理不同分辨率、刘海屏、折叠屏
/// 支持安全区域适配和UI缩放
/// </summary>
public class ScreenAdapter : MonoBehaviour
{
    public static ScreenAdapter Instance { get; private set; }

    [Header("设计参考分辨率")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);
    [SerializeField] private float referenceAspect = 16f / 9f;

    [Header("安全区域")]
    [SerializeField] private bool applySafeArea = true;
    [SerializeField] private RectTransform[] safeAreaPanels;

    private Rect lastSafeArea = Rect.zero;
    private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

    public float ScreenScale { get; private set; } = 1f;
    public Rect SafeArea => Screen.safeArea;
    public float AspectRatio => (float)Screen.width / Screen.height;
    public bool IsWideScreen => AspectRatio > 2f;
    public bool IsTablet => Mathf.Min(Screen.width, Screen.height) >= 1200;

    public event System.Action OnScreenChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CalculateScale();
        ApplySafeArea();
    }

    void Update()
    {
        // 检测屏幕变化（旋转、折叠屏展开等）
        if (Screen.safeArea != lastSafeArea || Screen.orientation != lastOrientation)
        {
            lastSafeArea = Screen.safeArea;
            lastOrientation = Screen.orientation;
            CalculateScale();
            ApplySafeArea();
            OnScreenChanged?.Invoke();
        }
    }

    private void CalculateScale()
    {
        float currentAspect = AspectRatio;
        ScreenScale = currentAspect / referenceAspect;
    }

    /// <summary>
    /// 将UI面板适配到安全区域（避开刘海、圆角等）
    /// </summary>
    public void ApplySafeArea()
    {
        if (!applySafeArea || safeAreaPanels == null) return;

        Rect safeArea = Screen.safeArea;

        // 归一化安全区域坐标
        Vector2 anchorMin = new Vector2(
            safeArea.x / Screen.width,
            safeArea.y / Screen.height
        );
        Vector2 anchorMax = new Vector2(
            (safeArea.x + safeArea.width) / Screen.width,
            (safeArea.y + safeArea.height) / Screen.height
        );

        foreach (var panel in safeAreaPanels)
        {
            if (panel == null) continue;
            panel.anchorMin = anchorMin;
            panel.anchorMax = anchorMax;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// 手动添加安全区域面板
    /// </summary>
    public void RegisterSafeAreaPanel(RectTransform panel)
    {
        if (panel == null) return;

        var list = safeAreaPanels != null ?
            new System.Collections.Generic.List<RectTransform>(safeAreaPanels) :
            new System.Collections.Generic.List<RectTransform>();

        if (!list.Contains(panel))
        {
            list.Add(panel);
            safeAreaPanels = list.ToArray();
            ApplySafeArea();
        }
    }

    /// <summary>
    /// 获取适配后的UI缩放值
    /// </summary>
    public float GetUIScale()
    {
        if (IsTablet)
            return 1.2f; // 平板适当放大
        if (IsWideScreen)
            return 0.9f; // 超宽屏适当缩小
        return 1f;
    }

    /// <summary>
    /// 获取推荐的分屏模式
    /// </summary>
    public SplitScreenMode GetRecommendedSplitMode()
    {
        if (AspectRatio >= 2f)
            return SplitScreenMode.Vertical; // 超宽屏用竖分
        if (IsTablet)
            return SplitScreenMode.Vertical; // 平板用竖分
        return SplitScreenMode.Horizontal;   // 一般手机用横分
    }

    public enum SplitScreenMode
    {
        Horizontal, // 上下分屏
        Vertical    // 左右分屏
    }
}
