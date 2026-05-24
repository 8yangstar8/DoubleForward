using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 网络谜题状态同步 - 确保所有客户端看到一致的谜题状态
/// 压力板、光感应器、开关等互动元素的网络同步
/// </summary>
public class NetworkPuzzleSync : NetworkBehaviour
{
    private NetworkVariable<bool> isActivated = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<float> progress = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] private UnityEngine.Events.UnityEvent onActivatedRemote;
    [SerializeField] private UnityEngine.Events.UnityEvent onDeactivatedRemote;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        isActivated.OnValueChanged += OnActivationChanged;
        progress.OnValueChanged += OnProgressChanged;
    }

    /// <summary>
    /// 本地触发激活 → 通知Server
    /// </summary>
    public void SetActivated(bool active)
    {
        if (IsServer)
        {
            isActivated.Value = active;
        }
        else
        {
            RequestActivateRpc(active);
        }
    }

    /// <summary>
    /// 设置进度值 (0~1)
    /// </summary>
    public void SetProgress(float value)
    {
        if (IsServer)
        {
            progress.Value = Mathf.Clamp01(value);
        }
        else
        {
            RequestProgressRpc(value);
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestActivateRpc(bool active)
    {
        isActivated.Value = active;
    }

    [Rpc(SendTo.Server)]
    private void RequestProgressRpc(float value)
    {
        progress.Value = Mathf.Clamp01(value);
    }

    private void OnActivationChanged(bool prev, bool current)
    {
        if (current)
            onActivatedRemote?.Invoke();
        else
            onDeactivatedRemote?.Invoke();
    }

    private void OnProgressChanged(float prev, float current)
    {
        // 子类或监听者可通过GetProgress获取
    }

    public bool GetActivated() => isActivated.Value;
    public float GetProgress() => progress.Value;

    /// <summary>
    /// 同步机关链接触发
    /// </summary>
    [Rpc(SendTo.Everyone)]
    public void SyncLinkedEventRpc(string eventId, bool active)
    {
        // 通知所有关联的机关
        var linkedObjects = GameObject.FindGameObjectsWithTag("PuzzleInteractable");
        foreach (var obj in linkedObjects)
        {
            var linked = obj.GetComponent<ILinkedPuzzle>();
            if (linked != null)
                linked.OnLinkedEvent(eventId, active);
        }
    }
}

/// <summary>
/// 关联谜题接口 - 接收其他谜题发出的事件
/// </summary>
public interface ILinkedPuzzle
{
    void OnLinkedEvent(string eventId, bool active);
}
