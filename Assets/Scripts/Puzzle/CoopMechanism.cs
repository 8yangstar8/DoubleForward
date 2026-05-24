using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 合作机制 - 需要两个玩家同时参与才能解锁的谜题元素
/// 类型：同步踩踏、光影融合、协力推动、通道保持
/// </summary>
public class CoopMechanism : MonoBehaviour
{
    public enum CoopType
    {
        DualSwitch,       // 两人同时站在开关上
        LightShadowFuse,  // 一人站在光区、一人站在暗区
        PushTogether,     // 两人同时推一个物体
        HoldOpen,         // 一人保持门开，另一人通过
        SyncActivate,     // 两人在时间窗口内分别激活开关
        Bridge,           // 一人变成另一人的桥/平台
    }

    [Header("合作类型")]
    [SerializeField] private CoopType coopType = CoopType.DualSwitch;

    [Header("区域设置")]
    [SerializeField] private BoxCollider2D zoneA;
    [SerializeField] private BoxCollider2D zoneB;
    [SerializeField] private float syncTimeWindow = 2f; // SyncActivate的时间窗口

    [Header("效果")]
    [SerializeField] private GameObject targetObject; // 要激活的目标
    [SerializeField] private Animator mechanismAnimator;
    [SerializeField] private string activateAnimTrigger = "Activate";
    [SerializeField] private string deactivateAnimTrigger = "Deactivate";
    [SerializeField] private string activateSoundKey = "mechanism_activate";

    [Header("视觉指示")]
    [SerializeField] private SpriteRenderer indicatorA;
    [SerializeField] private SpriteRenderer indicatorB;
    [SerializeField] private Color inactiveColor = new Color(0.3f, 0.3f, 0.3f);
    [SerializeField] private Color activeColor = new Color(0f, 1f, 0.5f);
    [SerializeField] private Color readyColor = new Color(1f, 1f, 0f);

    // 运行时状态
    private bool playerInZoneA;
    private bool playerInZoneB;
    private bool isActivated;
    private float syncTimerA;
    private float syncTimerB;
    private PlayerController playerA;
    private PlayerController playerB;

    void Start()
    {
        if (zoneA != null) zoneA.isTrigger = true;
        if (zoneB != null) zoneB.isTrigger = true;

        UpdateIndicators();

        // 初始状态
        if (targetObject != null && coopType != CoopType.HoldOpen)
            targetObject.SetActive(false);
    }

    void Update()
    {
        switch (coopType)
        {
            case CoopType.DualSwitch:
                CheckDualSwitch();
                break;
            case CoopType.LightShadowFuse:
                CheckLightShadowFuse();
                break;
            case CoopType.SyncActivate:
                UpdateSyncTimers();
                break;
            case CoopType.HoldOpen:
                CheckHoldOpen();
                break;
        }

        UpdateIndicators();
    }

    // ============ 区域检测（由子物体的Trigger转发） ============

    /// <summary>
    /// 由ZoneTriggerRelay调用
    /// </summary>
    public void OnPlayerEnterZone(int zoneIndex, PlayerController player)
    {
        if (zoneIndex == 0)
        {
            playerInZoneA = true;
            playerA = player;

            if (coopType == CoopType.SyncActivate)
                syncTimerA = syncTimeWindow;
        }
        else
        {
            playerInZoneB = true;
            playerB = player;

            if (coopType == CoopType.SyncActivate)
                syncTimerB = syncTimeWindow;
        }
    }

    public void OnPlayerExitZone(int zoneIndex, PlayerController player)
    {
        if (zoneIndex == 0)
        {
            playerInZoneA = false;
            playerA = null;
        }
        else
        {
            playerInZoneB = false;
            playerB = null;
        }
    }

    // ============ 机制逻辑 ============

    private void CheckDualSwitch()
    {
        bool shouldActivate = playerInZoneA && playerInZoneB;

        if (shouldActivate && !isActivated)
            Activate();
        else if (!shouldActivate && isActivated)
            Deactivate();
    }

    private void CheckLightShadowFuse()
    {
        if (playerA == null || playerB == null) return;

        // 检查角色类型：一个是Lux一个是Nox
        bool luxInA = playerA.Type == PlayerController.PlayerType.Lux;
        bool noxInB = playerB.Type == PlayerController.PlayerType.Nox;
        bool noxInA = playerA.Type == PlayerController.PlayerType.Nox;
        bool luxInB = playerB.Type == PlayerController.PlayerType.Lux;

        bool shouldActivate = (luxInA && noxInB) || (noxInA && luxInB);

        if (shouldActivate && playerInZoneA && playerInZoneB && !isActivated)
            Activate();
        else if (!shouldActivate && isActivated)
            Deactivate();
    }

    private void UpdateSyncTimers()
    {
        if (syncTimerA > 0) syncTimerA -= Time.deltaTime;
        if (syncTimerB > 0) syncTimerB -= Time.deltaTime;

        // 两个计时器都还有时间 = 同步成功
        if (syncTimerA > 0 && syncTimerB > 0 && !isActivated)
        {
            Activate();
        }
    }

    private void CheckHoldOpen()
    {
        if (playerInZoneA && !isActivated)
            Activate();
        else if (!playerInZoneA && isActivated)
            Deactivate();
    }

    private void Activate()
    {
        isActivated = true;

        if (targetObject != null)
            targetObject.SetActive(true);

        if (mechanismAnimator != null)
            mechanismAnimator.SetTrigger(activateAnimTrigger);

        AudioManager.Instance?.PlaySFX(activateSoundKey);

        EventBus.Publish(new PuzzleSolvedEvent
        {
            puzzleId = gameObject.name,
            puzzleType = coopType.ToString()
        });

        Debug.Log($"[CoopMechanism] {coopType} 已激活");
    }

    private void Deactivate()
    {
        isActivated = false;

        // HoldOpen类型需要关闭目标
        if (coopType == CoopType.HoldOpen && targetObject != null)
            targetObject.SetActive(false);

        if (mechanismAnimator != null && !string.IsNullOrEmpty(deactivateAnimTrigger))
            mechanismAnimator.SetTrigger(deactivateAnimTrigger);
    }

    private void UpdateIndicators()
    {
        if (indicatorA != null)
        {
            if (isActivated)
                indicatorA.color = activeColor;
            else if (playerInZoneA)
                indicatorA.color = readyColor;
            else
                indicatorA.color = inactiveColor;
        }

        if (indicatorB != null)
        {
            if (isActivated)
                indicatorB.color = activeColor;
            else if (playerInZoneB)
                indicatorB.color = readyColor;
            else
                indicatorB.color = inactiveColor;
        }
    }

    void OnDrawGizmos()
    {
        // Zone A
        if (zoneA != null)
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
            Vector3 center = zoneA.transform.position + (Vector3)zoneA.offset;
            Vector3 size = Vector3.Scale(zoneA.size, zoneA.transform.lossyScale);
            Gizmos.DrawCube(center, size);
            Gizmos.DrawWireCube(center, size);
        }

        // Zone B
        if (zoneB != null)
        {
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f);
            Vector3 center = zoneB.transform.position + (Vector3)zoneB.offset;
            Vector3 size = Vector3.Scale(zoneB.size, zoneB.transform.lossyScale);
            Gizmos.DrawCube(center, size);
            Gizmos.DrawWireCube(center, size);
        }

        // 连线到目标
        if (targetObject != null)
        {
            Gizmos.color = isActivated ? Color.green : Color.gray;
            Gizmos.DrawLine(transform.position, targetObject.transform.position);
        }
    }
}

/// <summary>
/// 挂在CoopMechanism的子物体（ZoneA/ZoneB）上，转发触发事件
/// </summary>
public class CoopZoneTrigger : MonoBehaviour
{
    [SerializeField] private CoopMechanism parentMechanism;
    [SerializeField] private int zoneIndex; // 0=A, 1=B

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        var player = other.GetComponent<PlayerController>();
        if (player != null && parentMechanism != null)
            parentMechanism.OnPlayerEnterZone(zoneIndex, player);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        var player = other.GetComponent<PlayerController>();
        if (player != null && parentMechanism != null)
            parentMechanism.OnPlayerExitZone(zoneIndex, player);
    }
}
