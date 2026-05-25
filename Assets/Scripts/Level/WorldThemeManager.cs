using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

/// <summary>
/// 世界主题管理器 - 管理5个世界的视觉和音频主题
/// 光之森林、暗影洞窟、机械要塞、冰火交界、虚空深渊
/// 控制调色板、后处理、BGM、环境音、粒子效果
/// 关卡加载时自动应用对应主题
/// </summary>
public class WorldThemeManager : MonoBehaviour
{
    public static WorldThemeManager Instance { get; private set; }

    [Header("世界主题数据")]
    [SerializeField] private WorldTheme[] worldThemes;

    [Header("过渡")]
    [SerializeField] private float themeTransitionDuration = 1.5f;

    [Header("全局后处理")]
    [SerializeField] private UnityEngine.Rendering.Volume globalVolume;

    [System.Serializable]
    public class WorldTheme
    {
        public string worldName;
        public int chapter;

        [Header("颜色")]
        public Color ambientLightColor = Color.white;
        public Color fogColor = new Color(0.5f, 0.5f, 0.6f);
        public float fogDensity = 0.02f;
        public Color skyColor = new Color(0.4f, 0.6f, 0.9f);

        [Header("调色板")]
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.gray;
        public Color accentColor = Color.yellow;
        public Color dangerColor = Color.red;

        [Header("光照")]
        public float globalLightIntensity = 1f;
        public Color globalLightColor = Color.white;

        [Header("音频")]
        public string bgmKey;
        public string ambientKey;
        public float bgmPitch = 1f;

        [Header("粒子")]
        public Color particlePrimaryColor = Color.white;
        public Color particleSecondaryColor = Color.gray;

        [Header("UI调色")]
        public Color uiPrimaryColor = new Color(0.2f, 0.6f, 1f);
        public Color uiSecondaryColor = new Color(0.1f, 0.3f, 0.6f);
    }

    private int currentChapter = -1;
    private WorldTheme currentTheme;

    // 公共访问当前主题
    public WorldTheme CurrentTheme => currentTheme;
    public Color PrimaryColor => currentTheme?.primaryColor ?? Color.white;
    public Color AccentColor => currentTheme?.accentColor ?? Color.yellow;
    public Color UIColor => currentTheme?.uiPrimaryColor ?? Color.blue;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 初始化默认主题数据
        if (worldThemes == null || worldThemes.Length == 0)
            InitializeDefaultThemes();
    }

    void OnEnable()
    {
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
    }

    private void OnLevelStart(LevelStartEvent e)
    {
        ApplyTheme(e.chapter);
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 应用指定章节的世界主题
    /// </summary>
    public void ApplyTheme(int chapter)
    {
        if (chapter == currentChapter) return;
        currentChapter = chapter;

        var theme = GetTheme(chapter);
        if (theme == null) return;

        currentTheme = theme;
        StartCoroutine(TransitionToTheme(theme));
    }

    /// <summary>
    /// 获取指定章节的主题
    /// </summary>
    public WorldTheme GetTheme(int chapter)
    {
        if (worldThemes == null) return null;

        foreach (var theme in worldThemes)
        {
            if (theme.chapter == chapter)
                return theme;
        }

        return worldThemes.Length > 0 ? worldThemes[0] : null;
    }

    /// <summary>
    /// 获取世界名称
    /// </summary>
    public string GetWorldName(int chapter)
    {
        var theme = GetTheme(chapter);
        return theme?.worldName ?? $"World {chapter}";
    }

    // ==================== 主题过渡 ====================

    private IEnumerator TransitionToTheme(WorldTheme theme)
    {
        float elapsed = 0;

        // 缓存旧值
        Color oldAmbient = RenderSettings.ambientLight;
        Color oldFogColor = RenderSettings.fogColor;
        float oldFogDensity = RenderSettings.fogDensity;

        while (elapsed < themeTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / themeTransitionDuration;
            float smoothT = Mathf.SmoothStep(0, 1, t);

            // 环境光
            RenderSettings.ambientLight = Color.Lerp(oldAmbient, theme.ambientLightColor, smoothT);

            // 雾效
            RenderSettings.fogColor = Color.Lerp(oldFogColor, theme.fogColor, smoothT);
            RenderSettings.fogDensity = Mathf.Lerp(oldFogDensity, theme.fogDensity, smoothT);

            yield return null;
        }

        // 最终值
        RenderSettings.ambientLight = theme.ambientLightColor;
        RenderSettings.fogColor = theme.fogColor;
        RenderSettings.fogDensity = theme.fogDensity;

        // 应用全局2D光照
        ApplyGlobalLight(theme);

        // 切换BGM
        if (!string.IsNullOrEmpty(theme.bgmKey) && AudioManager.Instance != null)
        {
            AudioManager.Instance.CrossfadeBGM(null, themeTransitionDuration);
            AudioManager.Instance.PlayBGM(theme.bgmKey);
        }

        // 环境音
        if (!string.IsNullOrEmpty(theme.ambientKey) && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAmbient(theme.ambientKey);
        }

        Debug.Log($"[WorldTheme] Applied theme: {theme.worldName} (Chapter {theme.chapter})");
    }

    private void ApplyGlobalLight(WorldTheme theme)
    {
        // 尝试找到全局2D光源
        var light2D = FindAnyObjectByType<Light2D>();
        if (light2D != null)
        {
            light2D.intensity = theme.globalLightIntensity;
            light2D.color = theme.globalLightColor;
        }
    }

    // ==================== 默认主题初始化 ====================

    private void InitializeDefaultThemes()
    {
        worldThemes = new WorldTheme[]
        {
            // 第1章 - 光之森林
            new WorldTheme
            {
                worldName = "光之森林",
                chapter = 1,
                ambientLightColor = new Color(0.7f, 0.8f, 0.6f),
                fogColor = new Color(0.6f, 0.8f, 0.5f, 0.3f),
                fogDensity = 0.01f,
                skyColor = new Color(0.5f, 0.8f, 1f),
                primaryColor = new Color(0.3f, 0.8f, 0.4f),
                secondaryColor = new Color(0.6f, 0.4f, 0.2f),
                accentColor = new Color(1f, 0.9f, 0.3f),
                dangerColor = new Color(0.8f, 0.2f, 0.1f),
                globalLightIntensity = 1.0f,
                globalLightColor = new Color(1f, 0.95f, 0.85f),
                bgmKey = "bgm_forest",
                ambientKey = "ambient_forest",
                particlePrimaryColor = new Color(0.4f, 0.9f, 0.5f),
                particleSecondaryColor = new Color(1f, 1f, 0.6f),
                uiPrimaryColor = new Color(0.3f, 0.7f, 0.4f),
                uiSecondaryColor = new Color(0.2f, 0.5f, 0.3f)
            },
            // 第2章 - 暗影洞窟
            new WorldTheme
            {
                worldName = "暗影洞窟",
                chapter = 2,
                ambientLightColor = new Color(0.2f, 0.15f, 0.3f),
                fogColor = new Color(0.1f, 0.05f, 0.2f),
                fogDensity = 0.04f,
                skyColor = new Color(0.05f, 0.02f, 0.1f),
                primaryColor = new Color(0.4f, 0.2f, 0.7f),
                secondaryColor = new Color(0.15f, 0.1f, 0.25f),
                accentColor = new Color(0.6f, 0.3f, 1f),
                dangerColor = new Color(0.8f, 0f, 0.3f),
                globalLightIntensity = 0.4f,
                globalLightColor = new Color(0.5f, 0.4f, 0.8f),
                bgmKey = "bgm_cave",
                ambientKey = "ambient_cave",
                particlePrimaryColor = new Color(0.5f, 0.3f, 0.8f),
                particleSecondaryColor = new Color(0.2f, 0.1f, 0.4f),
                uiPrimaryColor = new Color(0.5f, 0.3f, 0.8f),
                uiSecondaryColor = new Color(0.3f, 0.1f, 0.5f)
            },
            // 第3章 - 机械要塞
            new WorldTheme
            {
                worldName = "机械要塞",
                chapter = 3,
                ambientLightColor = new Color(0.5f, 0.5f, 0.55f),
                fogColor = new Color(0.4f, 0.4f, 0.45f),
                fogDensity = 0.02f,
                skyColor = new Color(0.35f, 0.35f, 0.4f),
                primaryColor = new Color(0.6f, 0.65f, 0.7f),
                secondaryColor = new Color(0.3f, 0.3f, 0.35f),
                accentColor = new Color(1f, 0.6f, 0.1f),
                dangerColor = new Color(1f, 0.3f, 0f),
                globalLightIntensity = 0.8f,
                globalLightColor = new Color(0.9f, 0.85f, 0.75f),
                bgmKey = "bgm_fortress",
                ambientKey = "ambient_machine",
                particlePrimaryColor = new Color(1f, 0.7f, 0.2f),
                particleSecondaryColor = new Color(0.6f, 0.6f, 0.7f),
                uiPrimaryColor = new Color(0.8f, 0.5f, 0.1f),
                uiSecondaryColor = new Color(0.5f, 0.3f, 0.1f)
            },
            // 第4章 - 冰火交界
            new WorldTheme
            {
                worldName = "冰火交界",
                chapter = 4,
                ambientLightColor = new Color(0.5f, 0.5f, 0.7f),
                fogColor = new Color(0.6f, 0.5f, 0.5f),
                fogDensity = 0.025f,
                skyColor = new Color(0.4f, 0.3f, 0.5f),
                primaryColor = new Color(0.5f, 0.7f, 1f),
                secondaryColor = new Color(1f, 0.4f, 0.2f),
                accentColor = new Color(0.8f, 0.9f, 1f),
                dangerColor = new Color(1f, 0.2f, 0f),
                globalLightIntensity = 0.7f,
                globalLightColor = new Color(0.7f, 0.65f, 0.8f),
                bgmKey = "bgm_icefire",
                ambientKey = "ambient_icefire",
                particlePrimaryColor = new Color(0.5f, 0.8f, 1f),
                particleSecondaryColor = new Color(1f, 0.5f, 0.2f),
                uiPrimaryColor = new Color(0.4f, 0.6f, 0.9f),
                uiSecondaryColor = new Color(0.7f, 0.3f, 0.2f)
            },
            // 第5章 - 虚空深渊
            new WorldTheme
            {
                worldName = "虚空深渊",
                chapter = 5,
                ambientLightColor = new Color(0.1f, 0.05f, 0.15f),
                fogColor = new Color(0.05f, 0f, 0.1f),
                fogDensity = 0.05f,
                skyColor = new Color(0.02f, 0f, 0.05f),
                primaryColor = new Color(0.1f, 0f, 0.2f),
                secondaryColor = new Color(0.05f, 0f, 0.1f),
                accentColor = new Color(0.7f, 0f, 0.9f),
                dangerColor = new Color(0.5f, 0f, 0.7f),
                globalLightIntensity = 0.3f,
                globalLightColor = new Color(0.4f, 0.2f, 0.6f),
                bgmKey = "bgm_void",
                ambientKey = "ambient_void",
                particlePrimaryColor = new Color(0.5f, 0f, 0.8f),
                particleSecondaryColor = new Color(0.2f, 0f, 0.3f),
                uiPrimaryColor = new Color(0.5f, 0.1f, 0.7f),
                uiSecondaryColor = new Color(0.3f, 0f, 0.4f)
            }
        };
    }
}
