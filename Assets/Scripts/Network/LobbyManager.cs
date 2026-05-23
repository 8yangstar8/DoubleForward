using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [SerializeField] private ushort defaultPort = 7777;

    public enum ConnectionState { Disconnected, Hosting, Connecting, Connected }
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public string RoomCode { get; private set; }

    public event System.Action<ConnectionState> OnStateChanged;
    public event System.Action<string> OnError;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void HostGame(string ipAddress = "0.0.0.0")
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ipAddress, defaultPort);

        if (NetworkManager.Singleton.StartHost())
        {
            State = ConnectionState.Hosting;
            RoomCode = GenerateRoomCode();
            OnStateChanged?.Invoke(State);
        }
        else
        {
            OnError?.Invoke("Failed to start host");
        }
    }

    public void JoinGame(string ipAddress)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ipAddress, defaultPort);

        State = ConnectionState.Connecting;
        OnStateChanged?.Invoke(State);

        NetworkManager.Singleton.OnClientConnectedCallback += OnConnectedToHost;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnectedFromHost;

        if (!NetworkManager.Singleton.StartClient())
        {
            State = ConnectionState.Disconnected;
            OnError?.Invoke("Failed to connect");
            OnStateChanged?.Invoke(State);
        }
    }

    private void OnConnectedToHost(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            State = ConnectionState.Connected;
            OnStateChanged?.Invoke(State);
        }
    }

    private void OnDisconnectedFromHost(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            State = ConnectionState.Disconnected;
            OnStateChanged?.Invoke(State);
        }
    }

    public void Disconnect()
    {
        NetworkManager.Singleton.Shutdown();
        State = ConnectionState.Disconnected;
        OnStateChanged?.Invoke(State);
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] code = new char[6];
        for (int i = 0; i < 6; i++)
            code[i] = chars[Random.Range(0, chars.Length)];
        return new string(code);
    }

    public string GetLocalIPAddress()
    {
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                return ip.ToString();
        }
        return "127.0.0.1";
    }
}
