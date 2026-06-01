using UnityEngine;

/// <summary>
/// 谜题连接器 - 将触发源(压力板/光感器)连接到目标机关(门/平台)
/// 提供运行时的"踩下开门"等基础谜题逻辑,无需UnityEvent手动连线
/// </summary>
public class PuzzleLink : MonoBehaviour
{
    public enum SourceType { PressurePlate, LightSensor }
    public enum TargetAction { OpenDoor, MovePlatform, DisableObject }

    [Header("触发源")]
    [SerializeField] private SourceType sourceType = SourceType.PressurePlate;
    [SerializeField] private PressurePlate pressurePlate;
    [SerializeField] private LightSensor lightSensor;

    [Header("目标")]
    [SerializeField] private TargetAction action = TargetAction.OpenDoor;
    [SerializeField] private GameObject targetObject;       // 门/障碍
    [SerializeField] private Vector3 openOffset = Vector3.up * 4f; // 门打开时的位移
    [SerializeField] private float moveSpeed = 5f;

    private Vector3 closedPos;
    private Vector3 openPos;
    private bool initialized;

    void Start()
    {
        if (targetObject != null)
        {
            closedPos = targetObject.transform.position;
            openPos = closedPos + openOffset;
            initialized = true;
        }
    }

    void Update()
    {
        if (!initialized) return;

        bool active = IsSourceActive();

        if (action == TargetAction.DisableObject)
        {
            // 激活时隐藏目标(如移除障碍墙)
            if (targetObject.activeSelf == active)
                targetObject.SetActive(!active);
            return;
        }

        // 门/平台: 激活时移到开启位置
        Vector3 target = active ? openPos : closedPos;
        targetObject.transform.position = Vector3.MoveTowards(
            targetObject.transform.position, target, moveSpeed * Time.deltaTime);
    }

    private bool IsSourceActive()
    {
        switch (sourceType)
        {
            case SourceType.PressurePlate:
                return pressurePlate != null && pressurePlate.IsPressed;
            case SourceType.LightSensor:
                return lightSensor != null && lightSensor.IsActivated;
            default:
                return false;
        }
    }

    /// <summary>编辑器配置用</summary>
    public void Configure(PressurePlate plate, GameObject target, Vector3 offset)
    {
        sourceType = SourceType.PressurePlate;
        pressurePlate = plate;
        targetObject = target;
        openOffset = offset;
    }
}
