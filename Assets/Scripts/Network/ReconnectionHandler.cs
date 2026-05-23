using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class ReconnectionHandler : MonoBehaviour
{
    [SerializeField] private float reconnectDelay = 2f;
    [SerializeField] private int maxRetries = 5;
    [SerializeField] private float retryInterval = 3f;

    private int currentRetries;
    private string lastServerAddress;
    private bool isReconnecting;

    public event System.Action OnReconnecting;
    public event System.Action OnReconnected;
    public event System.Action OnReconnectFailed;

    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnect;
        }
    }

    private void OnDisconnect(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;
        if (NetworkManager.Singleton.IsHost) return;

        StartCoroutine(AttemptReconnect());
    }

    private IEnumerator AttemptReconnect()
    {
        isReconnecting = true;
        currentRetries = 0;
        OnReconnecting?.Invoke();

        yield return new WaitForSeconds(reconnectDelay);

        while (currentRetries < maxRetries)
        {
            currentRetries++;
            Debug.Log($"Reconnect attempt {currentRetries}/{maxRetries}");

            NetworkManager.Singleton.Shutdown();
            yield return new WaitForSeconds(0.5f);

            if (NetworkManager.Singleton.StartClient())
            {
                yield return new WaitForSeconds(retryInterval);

                if (NetworkManager.Singleton.IsConnectedClient)
                {
                    isReconnecting = false;
                    OnReconnected?.Invoke();
                    yield break;
                }
            }

            yield return new WaitForSeconds(retryInterval);
        }

        isReconnecting = false;
        OnReconnectFailed?.Invoke();
    }

    public void CancelReconnect()
    {
        StopAllCoroutines();
        isReconnecting = false;
        NetworkManager.Singleton.Shutdown();
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnect;
    }
}
