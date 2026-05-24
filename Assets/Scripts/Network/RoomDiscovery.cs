using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

public class RoomDiscovery : MonoBehaviour
{
    [SerializeField] private int broadcastPort = 47777;
    [SerializeField] private float broadcastInterval = 1f;
    [SerializeField] private float roomTimeout = 5f;

    [System.Serializable]
    public class RoomInfo
    {
        public string hostName;
        public string ipAddress;
        public string roomCode;
        public float lastSeen;
    }

    public List<RoomInfo> DiscoveredRooms { get; private set; } = new List<RoomInfo>();
    public event System.Action<List<RoomInfo>> OnRoomsUpdated;

    private UdpClient broadcaster;
    private UdpClient listener;
    private float broadcastTimer;
    private bool isBroadcasting;
    private bool isListening;

    public void StartBroadcasting(string roomCode)
    {
        try
        {
            broadcaster = new UdpClient();
            broadcaster.EnableBroadcast = true;
            isBroadcasting = true;

            var lobbyManager = LobbyManager.Instance;
            string ip = lobbyManager != null ? lobbyManager.GetLocalIPAddress() : "127.0.0.1";
            string message = $"DF|{SystemInfo.deviceName}|{ip}|{roomCode}";
            byte[] data = Encoding.UTF8.GetBytes(message);

            broadcastTimer = 0;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Broadcast error: {e.Message}");
        }
    }

    public void StartListening()
    {
        try
        {
            listener = new UdpClient(broadcastPort);
            listener.EnableBroadcast = true;
            isListening = true;
            listener.BeginReceive(OnReceive, null);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Listen error: {e.Message}");
        }
    }

    void Update()
    {
        if (isBroadcasting)
        {
            broadcastTimer -= Time.deltaTime;
            if (broadcastTimer <= 0)
            {
                broadcastTimer = broadcastInterval;
                SendBroadcast();
            }
        }

        CleanupStaleRooms();
    }

    private void SendBroadcast()
    {
        if (broadcaster == null) return;
        var lobbyManager = LobbyManager.Instance;
        string ip = lobbyManager != null ? lobbyManager.GetLocalIPAddress() : "127.0.0.1";
        string roomCode = lobbyManager?.RoomCode ?? "UNKNOWN";
        string message = $"DF|{SystemInfo.deviceName}|{ip}|{roomCode}";
        byte[] data = Encoding.UTF8.GetBytes(message);

        try
        {
            broadcaster.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, broadcastPort));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Send broadcast failed: {e.Message}");
        }
    }

    private void OnReceive(System.IAsyncResult result)
    {
        if (listener == null) return;

        try
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = listener.EndReceive(result, ref endPoint);
            string message = Encoding.UTF8.GetString(data);

            if (message.StartsWith("DF|"))
            {
                string[] parts = message.Split('|');
                if (parts.Length >= 4)
                {
                    var room = new RoomInfo
                    {
                        hostName = parts[1],
                        ipAddress = parts[2],
                        roomCode = parts[3],
                        lastSeen = Time.time
                    };

                    UpdateRoom(room);
                }
            }

            listener.BeginReceive(OnReceive, null);
        }
        catch (System.Exception) { }
    }

    private void UpdateRoom(RoomInfo newRoom)
    {
        var existing = DiscoveredRooms.Find(r => r.ipAddress == newRoom.ipAddress);
        if (existing != null)
        {
            existing.lastSeen = Time.time;
            existing.hostName = newRoom.hostName;
            existing.roomCode = newRoom.roomCode;
        }
        else
        {
            DiscoveredRooms.Add(newRoom);
        }

        OnRoomsUpdated?.Invoke(DiscoveredRooms);
    }

    private void CleanupStaleRooms()
    {
        bool changed = DiscoveredRooms.RemoveAll(r => Time.time - r.lastSeen > roomTimeout) > 0;
        if (changed) OnRoomsUpdated?.Invoke(DiscoveredRooms);
    }

    /// <summary>
    /// 获取可用房间列表（供UI使用）
    /// </summary>
    public List<RoomInfo> GetAvailableRooms()
    {
        return new List<RoomInfo>(DiscoveredRooms);
    }

    /// <summary>
    /// 房间是否存在指定Instance
    /// </summary>
    public static RoomDiscovery Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StopAll()
    {
        isBroadcasting = false;
        isListening = false;
        broadcaster?.Close();
        listener?.Close();
        broadcaster = null;
        listener = null;
    }

    void OnDestroy()
    {
        StopAll();
    }
}
