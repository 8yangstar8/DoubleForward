using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 视差滚动背景系统 - 多层背景按不同速率移动产生深度感
/// 支持无限滚动、垂直视差、自动检测层级
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [System.Serializable]
    public class ParallaxLayer
    {
        public Transform layerTransform;
        [Range(0f, 1f)] public float parallaxFactorX = 0.5f;  // 0=不动, 1=跟相机同步
        [Range(0f, 1f)] public float parallaxFactorY = 0.2f;
        public bool infiniteScrollX = true;                     // 水平无限滚动
        public bool infiniteScrollY = false;
        public float autoScrollSpeedX = 0f;                     // 自动滚动（如云朵）
        public float autoScrollSpeedY = 0f;
    }

    [Header("视差层")]
    [SerializeField] private List<ParallaxLayer> layers = new List<ParallaxLayer>();

    [Header("自动检测")]
    [SerializeField] private bool autoDetectLayers = true;
    [SerializeField] private float[] autoParallaxFactors = { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f };

    private Camera mainCamera;
    private Vector3 lastCameraPosition;
    private Dictionary<Transform, Vector3> layerStartPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, float> layerWidths = new Dictionary<Transform, float>();

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null) return;

        lastCameraPosition = mainCamera.transform.position;

        if (autoDetectLayers && layers.Count == 0)
            AutoDetectLayers();

        // 记录初始位置和宽度
        foreach (var layer in layers)
        {
            if (layer.layerTransform == null) continue;
            layerStartPositions[layer.layerTransform] = layer.layerTransform.position;

            // 计算层宽度（用于无限滚动）
            var sr = layer.layerTransform.GetComponent<SpriteRenderer>();
            if (sr != null)
                layerWidths[layer.layerTransform] = sr.bounds.size.x;
            else
                layerWidths[layer.layerTransform] = 0;
        }
    }

    void LateUpdate()
    {
        if (mainCamera == null) return;

        Vector3 cameraDelta = mainCamera.transform.position - lastCameraPosition;

        foreach (var layer in layers)
        {
            if (layer.layerTransform == null) continue;

            // 视差移动
            Vector3 pos = layer.layerTransform.position;
            pos.x += cameraDelta.x * (1f - layer.parallaxFactorX);  // 相反：因子小=动得少=看起来远
            pos.y += cameraDelta.y * (1f - layer.parallaxFactorY);

            // 自动滚动
            pos.x += layer.autoScrollSpeedX * Time.deltaTime;
            pos.y += layer.autoScrollSpeedY * Time.deltaTime;

            layer.layerTransform.position = pos;

            // 无限滚动：当相机远离起点超过一个层宽度时，重置位置
            if (layer.infiniteScrollX && layerWidths.ContainsKey(layer.layerTransform))
            {
                float width = layerWidths[layer.layerTransform];
                if (width > 0)
                {
                    Vector3 startPos = layerStartPositions[layer.layerTransform];
                    float camOffset = mainCamera.transform.position.x - startPos.x;
                    float parallaxOffset = camOffset * layer.parallaxFactorX;

                    // 计算需要的偏移来保持无缝
                    float textureOffset = camOffset * (1f - layer.parallaxFactorX);
                    if (Mathf.Abs(textureOffset) >= width)
                    {
                        float sign = textureOffset > 0 ? 1f : -1f;
                        layerStartPositions[layer.layerTransform] = startPos + Vector3.right * width * sign;
                    }
                }
            }
        }

        lastCameraPosition = mainCamera.transform.position;
    }

    /// <summary>
    /// 自动检测子物体作为视差层
    /// </summary>
    private void AutoDetectLayers()
    {
        layers.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            float factor = i < autoParallaxFactors.Length ?
                autoParallaxFactors[i] : 0.5f + i * 0.1f;

            layers.Add(new ParallaxLayer
            {
                layerTransform = child,
                parallaxFactorX = Mathf.Clamp01(factor),
                parallaxFactorY = factor * 0.3f,
                infiniteScrollX = true
            });
        }
    }

    /// <summary>
    /// 运行时添加视差层
    /// </summary>
    public void AddLayer(Transform layerTransform, float parallaxX, float parallaxY = 0.2f)
    {
        var layer = new ParallaxLayer
        {
            layerTransform = layerTransform,
            parallaxFactorX = parallaxX,
            parallaxFactorY = parallaxY,
            infiniteScrollX = true
        };
        layers.Add(layer);

        layerStartPositions[layerTransform] = layerTransform.position;
        var sr = layerTransform.GetComponent<SpriteRenderer>();
        layerWidths[layerTransform] = sr != null ? sr.bounds.size.x : 0;
    }

    /// <summary>
    /// 设置自动滚动速度（如风吹云动）
    /// </summary>
    public void SetAutoScroll(int layerIndex, float speedX, float speedY = 0)
    {
        if (layerIndex >= 0 && layerIndex < layers.Count)
        {
            layers[layerIndex].autoScrollSpeedX = speedX;
            layers[layerIndex].autoScrollSpeedY = speedY;
        }
    }
}
