using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LobbyUI : MonoBehaviour
{
    [Header("Mode Selection")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button backButton;

    [Header("Host Panel")]
    [SerializeField] private GameObject hostPanel;
    [SerializeField] private TextMeshProUGUI roomCodeText;
    [SerializeField] private TextMeshProUGUI ipAddressText;
    [SerializeField] private TextMeshProUGUI waitingText;

    [Header("Join Panel")]
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private Button connectButton;
    [SerializeField] private Transform roomListContainer;
    [SerializeField] private GameObject roomItemPrefab;

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI statusText;

    private RoomDiscovery roomDiscovery;

    void Start()
    {
        roomDiscovery = FindFirstObjectByType<RoomDiscovery>();

        hostButton?.onClick.AddListener(OnHost);
        joinButton?.onClick.AddListener(OnJoin);
        backButton?.onClick.AddListener(OnBack);
        connectButton?.onClick.AddListener(OnConnect);

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnStateChanged += OnConnectionStateChanged;
            LobbyManager.Instance.OnError += OnConnectionError;
        }

        ShowModeSelection();
    }

    private void OnHost()
    {
        hostPanel?.SetActive(true);
        joinPanel?.SetActive(false);

        LobbyManager.Instance?.HostGame();

        if (roomCodeText != null)
            roomCodeText.text = LobbyManager.Instance?.RoomCode ?? "------";
        if (ipAddressText != null)
            ipAddressText.text = LobbyManager.Instance?.GetLocalIPAddress() ?? "Unknown";

        roomDiscovery?.StartBroadcasting(LobbyManager.Instance?.RoomCode ?? "");
    }

    private void OnJoin()
    {
        hostPanel?.SetActive(false);
        joinPanel?.SetActive(true);

        roomDiscovery?.StartListening();
        if (roomDiscovery != null)
            roomDiscovery.OnRoomsUpdated += UpdateRoomList;
    }

    private void OnConnect()
    {
        string ip = ipInputField?.text;
        if (string.IsNullOrEmpty(ip)) return;

        LobbyManager.Instance?.JoinGame(ip);
        SetStatus("Connecting...");
    }

    private void OnBack()
    {
        LobbyManager.Instance?.Disconnect();
        roomDiscovery?.StopAll();
        GameManager.Instance?.ReturnToMainMenu();
    }

    private void UpdateRoomList(List<RoomDiscovery.RoomInfo> rooms)
    {
        if (roomListContainer == null || roomItemPrefab == null) return;

        foreach (Transform child in roomListContainer)
            Destroy(child.gameObject);

        foreach (var room in rooms)
        {
            var item = Instantiate(roomItemPrefab, roomListContainer);
            var text = item.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = $"{room.hostName} [{room.roomCode}]";

            var button = item.GetComponent<Button>();
            string ip = room.ipAddress;
            button?.onClick.AddListener(() =>
            {
                if (ipInputField != null)
                    ipInputField.text = ip;
                OnConnect();
            });
        }
    }

    private void OnConnectionStateChanged(LobbyManager.ConnectionState state)
    {
        switch (state)
        {
            case LobbyManager.ConnectionState.Connected:
                SetStatus("Connected! Starting game...");
                break;
            case LobbyManager.ConnectionState.Hosting:
                SetStatus("Waiting for player 2...");
                break;
            case LobbyManager.ConnectionState.Disconnected:
                SetStatus("Disconnected");
                break;
        }
    }

    private void OnConnectionError(string error)
    {
        SetStatus($"Error: {error}");
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }

    private void ShowModeSelection()
    {
        hostPanel?.SetActive(false);
        joinPanel?.SetActive(false);
    }

    void OnDestroy()
    {
        if (roomDiscovery != null)
            roomDiscovery.OnRoomsUpdated -= UpdateRoomList;
    }
}
