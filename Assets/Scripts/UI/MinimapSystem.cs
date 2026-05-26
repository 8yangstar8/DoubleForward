using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 小地图系统 - 显示玩家位置、敌人、收集品、出口
/// 支持缩放、跟随和全屏切换
/// 适配移动端触摸操作
/// </summary>
public class MinimapSystem : MonoBehaviour
{
    public static MinimapSystem Instance { get; private set; }

    [Header("小地图UI")]
    [SerializeField] private RectTransform minimapContainer;
    [SerializeField] private RawImage minimapImage;
    [SerializeField] private RectTransform iconsParent;

    [Header("相机")]
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private float defaultZoom = 20f;
    [SerializeField] private float fullscreenZoom = 50f;
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float followSmooth = 5f;

    [Header("图标")]
    [SerializeField] private GameObject playerIconPrefab;
    [SerializeField] private GameObject enemyIconPrefab;
    [SerializeField] private GameObject collectibleIconPrefab;
    [SerializeField] private GameObject checkpointIconPrefab;
    [SerializeField] private GameObject exitIconPrefab;
    [SerializeField] private GameObject bossIconPrefab;

    [Header("颜色")]
    [SerializeField] private Color luxColor = new Color(1f, 0.9f, 0.3f);
    [SerializeField] private Color noxColor = new Color(0.5f, 0.3f, 0.8f);
    [SerializeField] private Color enemyColor = Color.red;
    [SerializeField] private Color collectibleColor = Color.green;
    [SerializeField] private Color exitColor = Color.cyan;

    [Header("切换")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private Vector2 miniSize = new Vector2(200, 200);
    [SerializeField] private Vector2 fullSize = new Vector2(600, 400);
    [SerializeField] private float toggleAnimDuration = 0.3f;

    [Header("设置")]
    [SerializeField] private float iconUpdateInterval = 0.2f;   // 图标位置刷新间隔
    [SerializeField] private float enemyDetectRadius = 30f;     // 敌人检测范围
    [SerializeField] private bool showEnemies = true;
    [SerializeField] private bool showCollectibles = true;

    // 运行时
    private Dictionary<int, RectTransform> trackedIcons = new Dictionary<int, RectTransform>();
    private RectTransform luxIcon;
    private RectTransform noxIcon;
    private bool isFullscreen;
    private float targetZoom;
    private float iconUpdateTimer;
    private RenderTexture minimapRT;
    private bool isEnabled = true;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        targetZoom = defaultZoom;
    }

    void Start()
    {
        SetupMinimapCamera();
        SetupToggle();

        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        if (minimapRT != null) minimapRT.Release();
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        if (!isEnabled || minimapCamera == null) return;

        UpdateCameraFollow();
        UpdateCameraZoom();

        iconUpdateTimer -= Time.deltaTime;
        if (iconUpdateTimer <= 0)
        {
            iconUpdateTimer = iconUpdateInterval;
            UpdateAllIcons();
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 启用/禁用小地图
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        if (minimapContainer != null)
            minimapContainer.gameObject.SetActive(enabled);
        if (minimapCamera != null)
            minimapCamera.enabled = enabled;
    }

    /// <summary>
    /// 切换全屏模式
    /// </summary>
    public void ToggleFullscreen()
    {
        isFullscreen = !isFullscreen;
        targetZoom = isFullscreen ? fullscreenZoom : defaultZoom;

        if (minimapContainer != null)
        {
            Vector2 targetSize = isFullscreen ? fullSize : miniSize;
            StartCoroutine(AnimateResize(targetSize));
        }
    }

    /// <summary>
    /// 在小地图上闪烁标记位置
    /// </summary>
    public void PingLocation(Vector3 worldPos, Color color, float duration = 2f)
    {
        StartCoroutine(PingRoutine(worldPos, color, duration));
    }

    /// <summary>
    /// 添加自定义追踪图标
    /// </summary>
    public void TrackObject(GameObject obj, GameObject iconPrefab, Color color)
    {
        if (obj == null || iconPrefab == null || iconsParent == null) return;

        int id = obj.GetInstanceID();
        if (trackedIcons.ContainsKey(id)) return;

        var iconObj = Instantiate(iconPrefab, iconsParent);
        var iconRect = iconObj.GetComponent<RectTransform>();
        var img = iconObj.GetComponent<Image>();
        if (img != null) img.color = color;

        trackedIcons[id] = iconRect;
    }

    /// <summary>
    /// 移除追踪
    /// </summary>
    public void UntrackObject(GameObject obj)
    {
        if (obj == null) return;
        int id = obj.GetInstanceID();
        if (trackedIcons.ContainsKey(id))
        {
            if (trackedIcons[id] != null)
                Destroy(trackedIcons[id].gameObject);
            trackedIcons.Remove(id);
        }
    }

    // ==================== 内部方法 ====================

    private void SetupMinimapCamera()
    {
        if (minimapCamera == null) return;

        // 创建RenderTexture
        minimapRT = new RenderTexture(256, 256, 0);
        minimapRT.filterMode = FilterMode.Bilinear;
        minimapCamera.targetTexture = minimapRT;

        if (minimapImage != null)
            minimapImage.texture = minimapRT;

        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = defaultZoom;
    }

    private void SetupToggle()
    {
        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleFullscreen);
    }

    private void UpdateCameraFollow()
    {
        // 跟随两个玩家的中点
        Vector3 targetPos = Vector3.zero;
        int playerCount = 0;

        if (LevelManager.Instance != null)
        {
            if (LevelManager.Instance.LuxPlayer != null)
            {
                targetPos += LevelManager.Instance.LuxPlayer.transform.position;
                playerCount++;
            }
            if (LevelManager.Instance.NoxPlayer != null)
            {
                targetPos += LevelManager.Instance.NoxPlayer.transform.position;
                playerCount++;
            }
        }

        if (playerCount > 0)
        {
            targetPos /= playerCount;
            Vector3 camPos = minimapCamera.transform.position;
            camPos.x = Mathf.Lerp(camPos.x, targetPos.x, Time.deltaTime * followSmooth);
            camPos.y = Mathf.Lerp(camPos.y, targetPos.y, Time.deltaTime * followSmooth);
            minimapCamera.transform.position = camPos;
        }
    }

    private void UpdateCameraZoom()
    {
        if (minimapCamera == null) return;
        minimapCamera.orthographicSize = Mathf.Lerp(
            minimapCamera.orthographicSize, targetZoom, Time.deltaTime * zoomSpeed);
    }

    private void UpdateAllIcons()
    {
        // 更新玩家图标
        UpdatePlayerIcons();

        // 更新敌人图标
        if (showEnemies)
            UpdateEnemyIcons();

        // 清理已销毁的追踪对象
        var toRemove = new List<int>();
        foreach (var kvp in trackedIcons)
        {
            if (kvp.Value == null)
                toRemove.Add(kvp.Key);
        }
        foreach (var key in toRemove)
            trackedIcons.Remove(key);
    }

    private void UpdatePlayerIcons()
    {
        if (iconsParent == null) return;

        // Lux
        if (LevelManager.Instance?.LuxPlayer != null)
        {
            if (luxIcon == null && playerIconPrefab != null)
            {
                var obj = Instantiate(playerIconPrefab, iconsParent);
                luxIcon = obj.GetComponent<RectTransform>();
                var img = obj.GetComponent<Image>();
                if (img != null) img.color = luxColor;
            }

            if (luxIcon != null)
                UpdateIconPosition(luxIcon, LevelManager.Instance.LuxPlayer.transform.position);
        }

        // Nox
        if (LevelManager.Instance?.NoxPlayer != null)
        {
            if (noxIcon == null && playerIconPrefab != null)
            {
                var obj = Instantiate(playerIconPrefab, iconsParent);
                noxIcon = obj.GetComponent<RectTransform>();
                var img = obj.GetComponent<Image>();
                if (img != null) img.color = noxColor;
            }

            if (noxIcon != null)
                UpdateIconPosition(noxIcon, LevelManager.Instance.NoxPlayer.transform.position);
        }
    }

    private void UpdateEnemyIcons()
    {
        // 性能考虑：使用已注册的EnemyDirector活跃列表
        var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);

        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;

            int id = enemy.GetInstanceID();
            if (!trackedIcons.ContainsKey(id))
            {
                if (enemyIconPrefab != null && iconsParent != null)
                {
                    var obj = Instantiate(enemyIconPrefab, iconsParent);
                    var iconRect = obj.GetComponent<RectTransform>();
                    var img = obj.GetComponent<Image>();
                    if (img != null) img.color = enemyColor;
                    trackedIcons[id] = iconRect;
                }
            }

            if (trackedIcons.ContainsKey(id))
                UpdateIconPosition(trackedIcons[id], enemy.transform.position);
        }
    }

    private void UpdateIconPosition(RectTransform icon, Vector3 worldPos)
    {
        if (minimapCamera == null || icon == null) return;

        Vector3 viewportPos = minimapCamera.WorldToViewportPoint(worldPos);
        if (minimapContainer != null)
        {
            Vector2 containerSize = minimapContainer.rect.size;
            icon.anchoredPosition = new Vector2(
                (viewportPos.x - 0.5f) * containerSize.x,
                (viewportPos.y - 0.5f) * containerSize.y
            );
        }

        // 隐藏视野外的图标
        bool inView = viewportPos.x >= 0 && viewportPos.x <= 1 &&
                      viewportPos.y >= 0 && viewportPos.y <= 1;
        icon.gameObject.SetActive(inView);
    }

    private System.Collections.IEnumerator AnimateResize(Vector2 targetSize)
    {
        if (minimapContainer == null) yield break;

        Vector2 startSize = minimapContainer.sizeDelta;
        float elapsed = 0;

        while (elapsed < toggleAnimDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / toggleAnimDuration;
            t = t * t * (3f - 2f * t); // smoothstep
            minimapContainer.sizeDelta = Vector2.Lerp(startSize, targetSize, t);
            yield return null;
        }

        minimapContainer.sizeDelta = targetSize;
    }

    private System.Collections.IEnumerator PingRoutine(Vector3 worldPos, Color color, float duration)
    {
        if (iconsParent == null || playerIconPrefab == null) yield break;

        var pingObj = Instantiate(playerIconPrefab, iconsParent);
        var pingRect = pingObj.GetComponent<RectTransform>();
        var img = pingObj.GetComponent<Image>();
        if (img != null) img.color = color;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            UpdateIconPosition(pingRect, worldPos);

            // 脉动效果
            float pulse = 1f + Mathf.Sin(elapsed * 8f) * 0.3f;
            pingRect.localScale = Vector3.one * pulse;

            // 渐隐
            if (img != null)
            {
                float alpha = 1f - (elapsed / duration);
                img.color = new Color(color.r, color.g, color.b, alpha);
            }

            yield return null;
        }

        Destroy(pingObj);
    }

    private void OnLevelStart(LevelStartEvent e)
    {
        // 清理旧图标
        foreach (var kvp in trackedIcons)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value.gameObject);
        }
        trackedIcons.Clear();

        if (luxIcon != null) { Destroy(luxIcon.gameObject); luxIcon = null; }
        if (noxIcon != null) { Destroy(noxIcon.gameObject); noxIcon = null; }
    }
}
