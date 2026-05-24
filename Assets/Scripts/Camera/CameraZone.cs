using UnityEngine;

/// <summary>
/// 相机区域触发器 - 进入时改变相机行为
/// 支持：缩放、跟随偏移、固定视角、震动、边界覆盖
/// 放在关卡中作为Trigger区域使用
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class CameraZone : MonoBehaviour
{
    public enum ZoneType
    {
        ZoomChange,       // 改变缩放
        OffsetChange,     // 改变跟随偏移
        FixedPosition,    // 固定相机位置
        BoundsOverride,   // 覆盖相机边界
        Shake,            // 进入时震动
        SlowFollow,       // 慢速跟随（紧张氛围）
        WideView,         // 宽视角（大场景展示）
    }

    [Header("区域类型")]
    [SerializeField] private ZoneType zoneType = ZoneType.ZoomChange;

    [Header("缩放设置")]
    [SerializeField] private float targetOrthographicSize = 5f;
    [SerializeField] private float zoomTransitionSpeed = 2f;

    [Header("偏移设置")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0, 1, -10);

    [Header("固定位置")]
    [SerializeField] private Transform fixedCameraTarget;

    [Header("边界覆盖")]
    [SerializeField] private Vector2 boundsMin;
    [SerializeField] private Vector2 boundsMax;

    [Header("震动设置")]
    [SerializeField] private float shakeIntensity = 0.3f;
    [SerializeField] private float shakeDuration = 0.5f;

    [Header("慢速跟随")]
    [SerializeField] private float slowFollowSpeed = 1.5f;

    [Header("通用")]
    [SerializeField] private float transitionDuration = 0.5f;
    [SerializeField] private bool restoreOnExit = true;

    // 缓存原始值
    private float originalSize;
    private Vector3 originalOffset;
    private float originalFollowSpeed;
    private bool originalUseBounds;
    private Vector2 originalBoundsMin;
    private Vector2 originalBoundsMax;
    private bool isActive;
    private Camera mainCam;
    private CameraController camController;

    // 过渡状态
    private float transitionTimer;
    private float startSize;
    private Vector3 startOffset;
    private bool isTransitioning;
    private bool isEntering; // true=进入过渡, false=退出过渡

    void Awake()
    {
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    void Update()
    {
        if (!isTransitioning) return;

        transitionTimer += Time.deltaTime;
        float t = Mathf.Clamp01(transitionTimer / transitionDuration);
        t = SmoothStep(t);

        if (mainCam == null) return;

        switch (zoneType)
        {
            case ZoneType.ZoomChange:
            case ZoneType.WideView:
                float targetSize = isEntering ? targetOrthographicSize : originalSize;
                mainCam.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
                break;

            case ZoneType.OffsetChange:
                // 通过CameraController的公共接口处理
                break;

            case ZoneType.SlowFollow:
                // 在CameraController中设置速度
                break;
        }

        if (t >= 1f)
            isTransitioning = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (isActive) return;

        isActive = true;
        mainCam = Camera.main;
        if (mainCam == null) return;

        camController = mainCam.GetComponent<CameraController>();

        // 缓存原始值
        originalSize = mainCam.orthographicSize;
        if (camController != null)
        {
            // 通过反射或公开属性获取原始值不太好，使用默认值
            originalFollowSpeed = 5f;
        }

        ApplyZoneEffect();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (!isActive) return;
        if (!restoreOnExit) return;

        isActive = false;
        RestoreOriginal();
    }

    private void ApplyZoneEffect()
    {
        switch (zoneType)
        {
            case ZoneType.ZoomChange:
                StartTransition(true);
                break;

            case ZoneType.OffsetChange:
                StartTransition(true);
                break;

            case ZoneType.FixedPosition:
                if (fixedCameraTarget != null && camController != null)
                    camController.SetTarget(fixedCameraTarget);
                break;

            case ZoneType.BoundsOverride:
                if (camController != null)
                    camController.SetBounds(boundsMin, boundsMax);
                break;

            case ZoneType.Shake:
                StartCoroutine(DoShake());
                break;

            case ZoneType.SlowFollow:
                StartTransition(true);
                break;

            case ZoneType.WideView:
                StartTransition(true);
                break;
        }
    }

    private void RestoreOriginal()
    {
        switch (zoneType)
        {
            case ZoneType.ZoomChange:
            case ZoneType.WideView:
                startSize = mainCam.orthographicSize;
                StartTransition(false);
                break;

            case ZoneType.FixedPosition:
                // 需要外部重新设置target（由LevelManager负责）
                break;

            case ZoneType.BoundsOverride:
                if (camController != null)
                    camController.SetBounds(originalBoundsMin, originalBoundsMax);
                break;
        }
    }

    private void StartTransition(bool entering)
    {
        isTransitioning = true;
        isEntering = entering;
        transitionTimer = 0f;
        startSize = mainCam != null ? mainCam.orthographicSize : 5f;
    }

    private System.Collections.IEnumerator DoShake()
    {
        if (mainCam == null) yield break;

        Vector3 originalPos = mainCam.transform.position;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-shakeIntensity, shakeIntensity);
            float y = Random.Range(-shakeIntensity, shakeIntensity);
            mainCam.transform.position = originalPos + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCam.transform.position = originalPos;
    }

    private float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        switch (zoneType)
        {
            case ZoneType.ZoomChange:
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                break;
            case ZoneType.FixedPosition:
                Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
                break;
            case ZoneType.BoundsOverride:
                Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
                break;
            case ZoneType.Shake:
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
                break;
            case ZoneType.WideView:
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.2f);
                break;
            default:
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
                break;
        }

        Vector3 center = transform.position + (Vector3)col.offset;
        Gizmos.DrawCube(center, col.size);
        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.8f);
        Gizmos.DrawWireCube(center, col.size);

        // 画边界覆盖区域
        if (zoneType == ZoneType.BoundsOverride)
        {
            Gizmos.color = Color.green;
            Vector3 bCenter = new Vector3((boundsMin.x + boundsMax.x) / 2f, (boundsMin.y + boundsMax.y) / 2f, 0);
            Vector3 bSize = new Vector3(boundsMax.x - boundsMin.x, boundsMax.y - boundsMin.y, 0);
            Gizmos.DrawWireCube(bCenter, bSize);
        }

        // 画固定目标连线
        if (zoneType == ZoneType.FixedPosition && fixedCameraTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(center, fixedCameraTarget.position);
            Gizmos.DrawWireSphere(fixedCameraTarget.position, 0.5f);
        }
    }
}
