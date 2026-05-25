using UnityEngine;

/// <summary>
/// 黑暗迷雾渲染器 - DarknessSystem的视觉渲染组件
/// 使用SpriteMask或全屏暗色遮罩+光源剪切孔来呈现光暗区域
/// 配合DarknessSystem数据驱动，在摄像机上挂载
/// </summary>
[RequireComponent(typeof(Camera))]
public class DarknessFogRenderer : MonoBehaviour
{
    public static DarknessFogRenderer Instance { get; private set; }

    [Header("渲染模式")]
    [SerializeField] private RenderMode renderMode = RenderMode.OverlaySprite;

    [Header("暗色遮罩")]
    [SerializeField] private Color darknessColor = new Color(0, 0, 0, 0.85f);
    [SerializeField] private Material fogMaterial;
    [SerializeField] private int fogSortingOrder = 100;

    [Header("光源孔设置")]
    [SerializeField] private Sprite lightHoleSprite;       // 圆形渐变Sprite (白色到透明)
    [SerializeField] private float lightEdgeSoftness = 0.3f;
    [SerializeField] private int maxLightHoles = 20;

    [Header("动态效果")]
    [SerializeField] private float fogTransitionSpeed = 2f;
    [SerializeField] private bool enableFlicker = true;
    [SerializeField] private float flickerIntensity = 0.02f;
    [SerializeField] private float flickerSpeed = 5f;

    [Header("Nox视野")]
    [SerializeField] private float noxDarkVisionRadius = 8f;
    [SerializeField] private Color noxVisionTint = new Color(0.15f, 0.05f, 0.25f, 0.5f);

    public enum RenderMode
    {
        OverlaySprite,   // Sprite遮罩（移动端友好）
        ShaderBased      // Shader渲染（效果更好, 消耗更高）
    }

    // 运行时
    private SpriteRenderer fogOverlay;
    private SpriteMask[] lightMasks;
    private Transform fogTransform;
    private Camera cam;
    private float currentDarkness;
    private float targetDarkness;

    // 光源孔池
    private SpriteRenderer[] lightHoles;
    private int activeLightHoles;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (renderMode == RenderMode.OverlaySprite)
            SetupSpriteMode();

        CreateLightHolePool();
    }

    void LateUpdate()
    {
        if (DarknessSystem.Instance == null) return;

        // 平滑过渡暗度
        targetDarkness = DarknessSystem.Instance.GetGlobalDarkness();
        currentDarkness = Mathf.MoveTowards(currentDarkness, targetDarkness,
            fogTransitionSpeed * Time.deltaTime);

        // 闪烁效果
        float flicker = 0f;
        if (enableFlicker)
            flicker = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) * flickerIntensity;

        // 更新遮罩透明度
        UpdateFogOpacity(currentDarkness + flicker);

        // 更新光源孔
        UpdateLightHoles();

        // 保持遮罩跟随摄像机
        if (fogTransform != null)
        {
            fogTransform.position = new Vector3(
                cam.transform.position.x,
                cam.transform.position.y,
                fogTransform.position.z);

            // 根据摄像机正交大小缩放遮罩
            float orthoSize = cam.orthographicSize;
            float aspect = cam.aspect;
            fogTransform.localScale = new Vector3(
                orthoSize * aspect * 2.5f,
                orthoSize * 2.5f,
                1f);
        }
    }

    // ==================== 设置 ====================

    private void SetupSpriteMode()
    {
        // 创建全屏遮罩Sprite
        var fogGo = new GameObject("DarknessFog");
        fogGo.transform.SetParent(transform);
        fogTransform = fogGo.transform;

        fogOverlay = fogGo.AddComponent<SpriteRenderer>();

        // 创建1x1白色sprite作为基础
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        fogOverlay.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        fogOverlay.color = darknessColor;
        fogOverlay.sortingOrder = fogSortingOrder;

        if (fogMaterial != null)
            fogOverlay.material = fogMaterial;

        // 置于摄像机前方
        fogTransform.localPosition = new Vector3(0, 0, 5f);
    }

    private void CreateLightHolePool()
    {
        lightHoles = new SpriteRenderer[maxLightHoles];

        for (int i = 0; i < maxLightHoles; i++)
        {
            var go = new GameObject($"LightHole_{i}");
            go.transform.SetParent(transform);

            var sr = go.AddComponent<SpriteRenderer>();

            if (lightHoleSprite != null)
            {
                sr.sprite = lightHoleSprite;
            }
            else
            {
                // 生成程序化的径向渐变
                sr.sprite = CreateRadialGradientSprite(64);
            }

            sr.color = Color.clear;
            sr.sortingOrder = fogSortingOrder + 1;
            sr.maskInteraction = SpriteMaskInteraction.None;

            // 使用减法混合模式 (需要合适的Material)
            // 默认用Sprite-Diffuse, 实际项目应配置AdditiveBlend或SpriteMask
            go.SetActive(false);
            lightHoles[i] = sr;
        }
    }

    // ==================== 更新 ====================

    private void UpdateFogOpacity(float darkness)
    {
        if (fogOverlay == null) return;

        Color c = darknessColor;
        c.a = darkness;
        fogOverlay.color = c;
    }

    private void UpdateLightHoles()
    {
        if (DarknessSystem.Instance == null) return;

        activeLightHoles = 0;

        // 从DarknessSystem获取所有活跃光源
        // 由于DarknessSystem.lightSources是private，我们通过公开的查询接口采样
        // 更好的方案: 让DarknessSystem暴露光源列表（这里用反射兼容）
        var field = typeof(DarknessSystem).GetField("lightSources",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field == null) return;

        var sources = field.GetValue(DarknessSystem.Instance) as
            System.Collections.Generic.List<DarknessSystem.LightSource>;

        if (sources == null) return;

        foreach (var source in sources)
        {
            if (activeLightHoles >= maxLightHoles) break;
            if (source.transform == null || !source.isActive) continue;

            // 检查是否在摄像机视野内
            Vector3 viewPos = cam.WorldToViewportPoint(source.transform.position);
            if (viewPos.x < -0.5f || viewPos.x > 1.5f || viewPos.y < -0.5f || viewPos.y > 1.5f)
                continue;

            var hole = lightHoles[activeLightHoles];
            hole.gameObject.SetActive(true);
            hole.transform.position = new Vector3(
                source.transform.position.x,
                source.transform.position.y,
                fogTransform != null ? fogTransform.position.z - 0.01f : 0f);

            // 缩放对应光源半径
            float diameter = source.radius * 2f * (1f + lightEdgeSoftness);
            hole.transform.localScale = Vector3.one * diameter;

            // 颜色 — 光源颜色的反色用于"剪掉"黑暗
            Color holeColor = source.color;
            holeColor.a = source.intensity * currentDarkness;
            hole.color = holeColor;

            activeLightHoles++;
        }

        // 隐藏多余的光孔
        for (int i = activeLightHoles; i < maxLightHoles; i++)
        {
            if (lightHoles[i].gameObject.activeSelf)
                lightHoles[i].gameObject.SetActive(false);
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 临时闪亮（技能释放、爆炸等）
    /// </summary>
    public void FlashLight(float duration = 0.3f, float intensity = 0.5f)
    {
        StartCoroutine(LightFlashRoutine(duration, intensity));
    }

    /// <summary>
    /// 切换Nox暗视野模式
    /// </summary>
    public void SetNoxVisionMode(bool active)
    {
        if (fogOverlay == null) return;

        if (active)
        {
            darknessColor = noxVisionTint;
        }
        else
        {
            darknessColor = new Color(0, 0, 0, 0.85f);
        }
    }

    /// <summary>
    /// 设置是否启用迷雾渲染
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        if (fogOverlay != null)
            fogOverlay.enabled = enabled;

        foreach (var hole in lightHoles)
        {
            if (hole != null)
                hole.gameObject.SetActive(false);
        }
    }

    private System.Collections.IEnumerator LightFlashRoutine(float duration, float intensity)
    {
        float originalAlpha = darknessColor.a;
        float targetAlpha = originalAlpha * (1f - intensity);

        float elapsed = 0f;
        float halfDuration = duration * 0.5f;

        // 亮起
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            float alpha = Mathf.Lerp(originalAlpha, targetAlpha, t);

            Color c = darknessColor;
            c.a = alpha;
            if (fogOverlay != null) fogOverlay.color = c;

            yield return null;
        }

        // 恢复
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            float alpha = Mathf.Lerp(targetAlpha, originalAlpha, t);

            Color c = darknessColor;
            c.a = alpha;
            if (fogOverlay != null) fogOverlay.color = c;

            yield return null;
        }
    }

    // ==================== 工具方法 ====================

    private Sprite CreateRadialGradientSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float normalized = dist / center;
                float alpha = Mathf.Clamp01(1f - normalized);
                alpha = alpha * alpha; // 二次衰减更柔和

                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
