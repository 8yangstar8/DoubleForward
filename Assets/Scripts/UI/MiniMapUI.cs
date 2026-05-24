using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 小地图系统 - 显示玩家位置、目标点、收集品等
/// 使用RenderTexture或UI图标模式
/// </summary>
public class MiniMapUI : MonoBehaviour
{
    public enum MiniMapMode
    {
        IconBased,      // 图标模式（轻量级，适合2D）
        CameraBased     // 相机渲染模式（需要额外相机）
    }

    [Header("模式")]
    [SerializeField] private MiniMapMode mode = MiniMapMode.IconBased;

    [Header("小地图设置")]
    [SerializeField] private RectTransform miniMapRect;
    [SerializeField] private RectTransform mapContent;
    [SerializeField] private RawImage cameraView;         // 相机模式用
    [SerializeField] private Image mapBackground;          // 图标模式用

    [Header("图标")]
    [SerializeField] private RectTransform player1Icon;
    [SerializeField] private RectTransform player2Icon;
    [SerializeField] private GameObject collectibleIconPrefab;
    [SerializeField] private GameObject checkpointIconPrefab;
    [SerializeField] private GameObject goalIconPrefab;
    [SerializeField] private Image player1DirectionArrow;
    [SerializeField] private Image player2DirectionArrow;

    [Header("范围")]
    [SerializeField] private Vector2 worldMin = new Vector2(-50, -20);
    [SerializeField] private Vector2 worldMax = new Vector2(50, 20);
    [SerializeField] private float zoomLevel = 1f;
    [SerializeField] private bool followPlayers = true;

    [Header("相机模式")]
    [SerializeField] private Camera miniMapCamera;
    [SerializeField] private float cameraHeight = 30f;
    [SerializeField] private float cameraSize = 15f;

    [Header("显示控制")]
    [SerializeField] private bool showCollectibles = true;
    [SerializeField] private bool showCheckpoints = true;
    [SerializeField] private bool showGoal = true;
    [SerializeField] private float iconPulseSpeed = 2f;

    private Transform player1Transform;
    private Transform player2Transform;
    private List<MiniMapIcon> trackedIcons = new List<MiniMapIcon>();
    private bool isVisible = true;

    private class MiniMapIcon
    {
        public Transform worldTarget;
        public RectTransform uiIcon;
        public MiniMapIconType iconType;
        public bool isActive = true;
    }

    public enum MiniMapIconType
    {
        Collectible,
        Checkpoint,
        Goal,
        Enemy,
        Custom
    }

    void Start()
    {
        if (mode == MiniMapMode.CameraBased)
            SetupCamera();
    }

    void LateUpdate()
    {
        if (!isVisible) return;

        UpdatePlayerIcons();

        if (mode == MiniMapMode.IconBased)
            UpdateTrackedIcons();

        if (mode == MiniMapMode.CameraBased)
            UpdateMiniMapCamera();
    }

    /// <summary>
    /// 设置玩家引用
    /// </summary>
    public void SetPlayers(Transform p1, Transform p2)
    {
        player1Transform = p1;
        player2Transform = p2;
    }

    /// <summary>
    /// 设置地图世界范围
    /// </summary>
    public void SetWorldBounds(Vector2 min, Vector2 max)
    {
        worldMin = min;
        worldMax = max;
    }

    /// <summary>
    /// 注册需要在小地图上追踪的对象
    /// </summary>
    public void RegisterIcon(Transform target, MiniMapIconType type)
    {
        if (mode != MiniMapMode.IconBased) return;

        GameObject prefab = null;
        switch (type)
        {
            case MiniMapIconType.Collectible:
                if (!showCollectibles) return;
                prefab = collectibleIconPrefab;
                break;
            case MiniMapIconType.Checkpoint:
                if (!showCheckpoints) return;
                prefab = checkpointIconPrefab;
                break;
            case MiniMapIconType.Goal:
                if (!showGoal) return;
                prefab = goalIconPrefab;
                break;
        }

        if (prefab == null || mapContent == null) return;

        var iconObj = Instantiate(prefab, mapContent);
        var icon = new MiniMapIcon
        {
            worldTarget = target,
            uiIcon = iconObj.GetComponent<RectTransform>(),
            iconType = type,
            isActive = true
        };

        trackedIcons.Add(icon);
    }

    /// <summary>
    /// 移除追踪的图标
    /// </summary>
    public void UnregisterIcon(Transform target)
    {
        for (int i = trackedIcons.Count - 1; i >= 0; i--)
        {
            if (trackedIcons[i].worldTarget == target)
            {
                if (trackedIcons[i].uiIcon != null)
                    Destroy(trackedIcons[i].uiIcon.gameObject);
                trackedIcons.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 切换小地图显示
    /// </summary>
    public void ToggleVisibility()
    {
        isVisible = !isVisible;
        if (miniMapRect != null)
            miniMapRect.gameObject.SetActive(isVisible);
    }

    public void SetZoom(float zoom)
    {
        zoomLevel = Mathf.Clamp(zoom, 0.5f, 3f);
        if (miniMapCamera != null)
            miniMapCamera.orthographicSize = cameraSize / zoomLevel;
    }

    private void UpdatePlayerIcons()
    {
        if (mode == MiniMapMode.IconBased)
        {
            UpdateIconPosition(player1Icon, player1Transform);
            UpdateIconPosition(player2Icon, player2Transform);
        }

        // 方向指示（当玩家超出小地图范围时指示方向）
        UpdateDirectionArrow(player1DirectionArrow, player1Transform);
        UpdateDirectionArrow(player2DirectionArrow, player2Transform);
    }

    private void UpdateIconPosition(RectTransform icon, Transform worldTarget)
    {
        if (icon == null || worldTarget == null || mapContent == null) return;

        Vector2 mapSize = mapContent.rect.size;
        Vector2 worldSize = worldMax - worldMin;

        // 世界坐标 → 小地图坐标
        Vector2 worldPos = new Vector2(worldTarget.position.x, worldTarget.position.y);
        Vector2 normalized = new Vector2(
            (worldPos.x - worldMin.x) / worldSize.x,
            (worldPos.y - worldMin.y) / worldSize.y
        );

        // 居中并缩放
        Vector2 mapPos = new Vector2(
            (normalized.x - 0.5f) * mapSize.x * zoomLevel,
            (normalized.y - 0.5f) * mapSize.y * zoomLevel
        );

        if (followPlayers && player1Transform != null)
        {
            // 以双人中点为中心
            Vector2 center = player1Transform.position;
            if (player2Transform != null)
                center = (center + (Vector2)player2Transform.position) / 2f;

            Vector2 centerNorm = new Vector2(
                (center.x - worldMin.x) / worldSize.x,
                (center.y - worldMin.y) / worldSize.y
            );
            Vector2 centerMap = new Vector2(
                (centerNorm.x - 0.5f) * mapSize.x * zoomLevel,
                (centerNorm.y - 0.5f) * mapSize.y * zoomLevel
            );
            mapPos -= centerMap;
        }

        icon.anchoredPosition = mapPos;
    }

    private void UpdateTrackedIcons()
    {
        for (int i = trackedIcons.Count - 1; i >= 0; i--)
        {
            var tracked = trackedIcons[i];

            // 清理已销毁的目标
            if (tracked.worldTarget == null)
            {
                if (tracked.uiIcon != null) Destroy(tracked.uiIcon.gameObject);
                trackedIcons.RemoveAt(i);
                continue;
            }

            UpdateIconPosition(tracked.uiIcon, tracked.worldTarget);

            // 目标点脉冲动画
            if (tracked.iconType == MiniMapIconType.Goal && tracked.uiIcon != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * iconPulseSpeed) * 0.2f;
                tracked.uiIcon.localScale = Vector3.one * pulse;
            }
        }
    }

    private void UpdateDirectionArrow(Image arrow, Transform target)
    {
        if (arrow == null || target == null || miniMapRect == null) return;

        // 如果目标在小地图范围外，显示方向箭头
        Vector2 mapSize = miniMapRect.rect.size;
        Vector2 worldSize = worldMax - worldMin;
        Vector2 worldPos = new Vector2(target.position.x, target.position.y);
        Vector2 normalized = new Vector2(
            (worldPos.x - worldMin.x) / worldSize.x,
            (worldPos.y - worldMin.y) / worldSize.y
        );

        bool outsideMap = normalized.x < 0 || normalized.x > 1 || normalized.y < 0 || normalized.y > 1;
        arrow.gameObject.SetActive(outsideMap);

        if (outsideMap)
        {
            Vector2 dir = (normalized - Vector2.one * 0.5f).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            arrow.rectTransform.rotation = Quaternion.Euler(0, 0, angle - 90);
        }
    }

    private void SetupCamera()
    {
        if (miniMapCamera == null) return;

        miniMapCamera.orthographic = true;
        miniMapCamera.orthographicSize = cameraSize;
        miniMapCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
    }

    private void UpdateMiniMapCamera()
    {
        if (miniMapCamera == null) return;

        // 相机跟随双人中点
        Vector3 targetPos = Vector3.zero;
        int count = 0;

        if (player1Transform != null) { targetPos += player1Transform.position; count++; }
        if (player2Transform != null) { targetPos += player2Transform.position; count++; }

        if (count > 0)
        {
            targetPos /= count;
            miniMapCamera.transform.position = new Vector3(targetPos.x, cameraHeight, targetPos.z);
        }
    }

    /// <summary>
    /// 清除所有追踪图标
    /// </summary>
    public void ClearAllIcons()
    {
        foreach (var icon in trackedIcons)
        {
            if (icon.uiIcon != null)
                Destroy(icon.uiIcon.gameObject);
        }
        trackedIcons.Clear();
    }
}
