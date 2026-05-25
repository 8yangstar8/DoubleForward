using UnityEngine;

/// <summary>
/// 双人摄像机系统 - 智能跟踪两位玩家的位置
/// 自动调整缩放以保持两人都在画面内
/// 支持动态边界限制、平滑跟随、以及Boss战锁定
/// </summary>
public class DualPlayerCamera : MonoBehaviour
{
    public static DualPlayerCamera Instance { get; private set; }

    [Header("目标")]
    [SerializeField] private Transform player1;
    [SerializeField] private Transform player2;

    [Header("跟随")]
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private float lookAheadFactor = 0.5f;
    [SerializeField] private Vector3 offset = new Vector3(0, 1, -10);

    [Header("缩放")]
    [SerializeField] private float minOrthoSize = 5f;
    [SerializeField] private float maxOrthoSize = 12f;
    [SerializeField] private float zoomSpeed = 3f;
    [SerializeField] private float zoomPadding = 2f;
    [SerializeField] private float defaultOrthoSize = 7f;

    [Header("边界")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 boundsMin = new Vector2(-50, -20);
    [SerializeField] private Vector2 boundsMax = new Vector2(50, 20);

    [Header("分离警告")]
    [SerializeField] private float maxPlayerDistance = 20f;
    [SerializeField] private float warningDistance = 15f;

    // 运行时
    private Camera cam;
    private Vector3 velocity;
    private float targetOrthoSize;
    private bool isLocked;
    private Vector3 lockPosition;
    private float lockOrthoSize;
    private float previousPlayerDistance;

    // 事件
    public event System.Action OnPlayersToFar;
    public event System.Action OnPlayersReunited;

    private bool wasWarning;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        targetOrthoSize = defaultOrthoSize;
    }

    void Start()
    {
        FindPlayers();
    }

    void LateUpdate()
    {
        if (isLocked)
        {
            UpdateLockedCamera();
            return;
        }

        if (player1 == null && player2 == null) FindPlayers();
        if (player1 == null && player2 == null) return;

        UpdateCameraPosition();
        UpdateCameraZoom();
        ClampToBounds();
        CheckPlayerDistance();
    }

    // ==================== 摄像机更新 ====================

    private void UpdateCameraPosition()
    {
        Vector3 targetPos;

        if (player1 != null && player2 != null)
        {
            // 两人中点
            Vector3 midpoint = (player1.position + player2.position) * 0.5f;

            // Look ahead：往两人移动方向稍微偏移
            Vector3 p1Vel = GetPlayerVelocity(player1);
            Vector3 p2Vel = GetPlayerVelocity(player2);
            Vector3 avgVel = (p1Vel + p2Vel) * 0.5f;

            targetPos = midpoint + avgVel * lookAheadFactor;
        }
        else
        {
            // 只有一个玩家
            Transform alive = player1 != null ? player1 : player2;
            targetPos = alive.position;
        }

        targetPos += offset;

        // 平滑跟随
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, 1f / followSpeed);
    }

    private void UpdateCameraZoom()
    {
        if (cam == null) return;

        if (player1 != null && player2 != null)
        {
            // 根据两人距离调整缩放
            float distance = Vector2.Distance(
                new Vector2(player1.position.x, player1.position.y),
                new Vector2(player2.position.x, player2.position.y));

            targetOrthoSize = Mathf.Clamp(
                distance * 0.5f + zoomPadding,
                minOrthoSize,
                maxOrthoSize);
        }
        else
        {
            targetOrthoSize = defaultOrthoSize;
        }

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrthoSize, zoomSpeed * Time.deltaTime);
    }

    private void ClampToBounds()
    {
        if (!useBounds || cam == null) return;

        float orthoHeight = cam.orthographicSize;
        float orthoWidth = orthoHeight * cam.aspect;

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, boundsMin.x + orthoWidth, boundsMax.x - orthoWidth);
        pos.y = Mathf.Clamp(pos.y, boundsMin.y + orthoHeight, boundsMax.y - orthoHeight);
        transform.position = pos;
    }

    private void UpdateLockedCamera()
    {
        transform.position = Vector3.SmoothDamp(transform.position,
            lockPosition + offset, ref velocity, 1f / followSpeed);

        if (cam != null)
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, lockOrthoSize, zoomSpeed * Time.deltaTime);
    }

    // ==================== 玩家距离检测 ====================

    private void CheckPlayerDistance()
    {
        if (player1 == null || player2 == null) return;

        float distance = Vector2.Distance(player1.position, player2.position);

        // 过远警告
        if (distance > warningDistance && !wasWarning)
        {
            wasWarning = true;
            OnPlayersToFar?.Invoke();

            // 通过EventBus通知HintSystem（避免Camera→UI的循环依赖）
            EventBus.Publish(new HintRequestEvent
            {
                textKey = "hint_players_far",
                fallbackText = "Players are too far apart!",
                duration = 3f
            });
        }
        else if (distance < warningDistance * 0.7f && wasWarning)
        {
            wasWarning = false;
            OnPlayersReunited?.Invoke();
        }

        // 超出最大距离可选惩罚
        if (distance > maxPlayerDistance)
        {
            // 边界弹力 — 可通知PlayerController限制移动
        }

        previousPlayerDistance = distance;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 锁定摄像机到指定位置（Boss战/过场/谜题房间）
    /// </summary>
    public void LockToPosition(Vector3 position, float orthoSize = -1f)
    {
        isLocked = true;
        lockPosition = position;
        lockOrthoSize = orthoSize > 0 ? orthoSize : defaultOrthoSize;
    }

    /// <summary>
    /// 解除锁定，恢复跟随
    /// </summary>
    public void Unlock()
    {
        isLocked = false;
    }

    /// <summary>
    /// 设置关卡边界
    /// </summary>
    public void SetBounds(Vector2 min, Vector2 max)
    {
        useBounds = true;
        boundsMin = min;
        boundsMax = max;
    }

    /// <summary>
    /// 设置目标玩家
    /// </summary>
    public void SetTargets(Transform p1, Transform p2)
    {
        player1 = p1;
        player2 = p2;
    }

    /// <summary>
    /// 瞬间跳转到目标位置（场景加载后）
    /// </summary>
    public void SnapToTargets()
    {
        if (player1 == null && player2 == null) FindPlayers();
        if (player1 == null && player2 == null) return;

        Vector3 targetPos;
        if (player1 != null && player2 != null)
            targetPos = (player1.position + player2.position) * 0.5f + offset;
        else
            targetPos = (player1 != null ? player1.position : player2.position) + offset;

        transform.position = targetPos;
        velocity = Vector3.zero;

        if (cam != null)
            cam.orthographicSize = defaultOrthoSize;
    }

    /// <summary>
    /// 获取当前两玩家距离
    /// </summary>
    public float GetPlayerDistance()
    {
        if (player1 == null || player2 == null) return 0f;
        return Vector2.Distance(player1.position, player2.position);
    }

    // ==================== 辅助 ====================

    private void FindPlayers()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Type == PlayerController.PlayerType.Lux)
                player1 = p.transform;
            else if (p.Type == PlayerController.PlayerType.Nox)
                player2 = p.transform;
        }
    }

    private Vector3 GetPlayerVelocity(Transform playerTransform)
    {
        if (playerTransform == null) return Vector3.zero;
        var rb = playerTransform.GetComponent<Rigidbody2D>();
        if (rb != null)
            return new Vector3(rb.linearVelocity.x, rb.linearVelocity.y, 0);
        return Vector3.zero;
    }

    void OnDrawGizmosSelected()
    {
        if (useBounds)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3(
                (boundsMin.x + boundsMax.x) * 0.5f,
                (boundsMin.y + boundsMax.y) * 0.5f, 0);
            Vector3 size = new Vector3(
                boundsMax.x - boundsMin.x,
                boundsMax.y - boundsMin.y, 0);
            Gizmos.DrawWireCube(center, size);
        }

        // 画警告距离
        if (player1 != null && player2 != null)
        {
            float dist = Vector2.Distance(player1.position, player2.position);
            Gizmos.color = dist > warningDistance ? Color.red : Color.green;
            Gizmos.DrawLine(player1.position, player2.position);
        }
    }
}
