using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 网络大厅UI增强版 - 房间创建/加入/匹配
/// 支持房间代码、玩家准备状态、角色选择、聊天
/// </summary>
public class NetworkLobbyUI : MonoBehaviour
{
    [Header("面板切换")]
    [SerializeField] private GameObject mainPanel;         // 主页（创建/加入选择）
    [SerializeField] private GameObject createPanel;       // 创建房间面板
    [SerializeField] private GameObject joinPanel;         // 加入房间面板
    [SerializeField] private GameObject roomPanel;         // 房间内面板
    [SerializeField] private GameObject matchmakingPanel;  // 匹配中面板

    [Header("主页")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button quickMatchButton;
    [SerializeField] private Button backToMenuButton;

    [Header("创建房间")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Toggle privateToggle;
    [SerializeField] private TMP_Dropdown chapterDropdown;
    [SerializeField] private Button confirmCreateButton;
    [SerializeField] private Button cancelCreateButton;

    [Header("加入房间")]
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private Button joinByCodeButton;
    [SerializeField] private Transform roomListContainer;
    [SerializeField] private GameObject roomListItemPrefab;
    [SerializeField] private Button refreshListButton;
    [SerializeField] private Button cancelJoinButton;
    [SerializeField] private TextMeshProUGUI roomCountText;

    [Header("房间内")]
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private TextMeshProUGUI roomCodeText;
    [SerializeField] private Button copyCodeButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveRoomButton;

    [Header("玩家信息")]
    [SerializeField] private Transform player1Slot;
    [SerializeField] private Transform player2Slot;
    [SerializeField] private TextMeshProUGUI player1Name;
    [SerializeField] private TextMeshProUGUI player2Name;
    [SerializeField] private Image player1ReadyIcon;
    [SerializeField] private Image player2ReadyIcon;
    [SerializeField] private Image player1CharacterIcon;
    [SerializeField] private Image player2CharacterIcon;
    [SerializeField] private Button swapCharacterButton;

    [Header("角色选择")]
    [SerializeField] private Sprite luxPortrait;
    [SerializeField] private Sprite noxPortrait;
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.gray;

    [Header("聊天（简易）")]
    [SerializeField] private ScrollRect chatScroll;
    [SerializeField] private TextMeshProUGUI chatContent;
    [SerializeField] private TMP_InputField chatInput;
    [SerializeField] private Button sendChatButton;
    [SerializeField] private string[] quickMessages;

    [Header("匹配")]
    [SerializeField] private TextMeshProUGUI matchStatusText;
    [SerializeField] private TextMeshProUGUI matchTimerText;
    [SerializeField] private Button cancelMatchButton;
    [SerializeField] private Image matchSpinner;

    // 状态
    private bool isHost;
    private bool isReady;
    private bool selectedLux = true;
    private float matchTimer;
    private bool isMatching;
    private string currentRoomCode;
    private List<GameObject> roomListItems = new List<GameObject>();

    void Awake()
    {
        // 主页
        if (createRoomButton != null) createRoomButton.onClick.AddListener(ShowCreatePanel);
        if (joinRoomButton != null) joinRoomButton.onClick.AddListener(ShowJoinPanel);
        if (quickMatchButton != null) quickMatchButton.onClick.AddListener(StartQuickMatch);
        if (backToMenuButton != null) backToMenuButton.onClick.AddListener(BackToMenu);

        // 创建
        if (confirmCreateButton != null) confirmCreateButton.onClick.AddListener(ConfirmCreateRoom);
        if (cancelCreateButton != null) cancelCreateButton.onClick.AddListener(ShowMainPanel);

        // 加入
        if (joinByCodeButton != null) joinByCodeButton.onClick.AddListener(JoinByCode);
        if (refreshListButton != null) refreshListButton.onClick.AddListener(RefreshRoomList);
        if (cancelJoinButton != null) cancelJoinButton.onClick.AddListener(ShowMainPanel);

        // 房间内
        if (copyCodeButton != null) copyCodeButton.onClick.AddListener(CopyRoomCode);
        if (readyButton != null) readyButton.onClick.AddListener(ToggleReady);
        if (startGameButton != null) startGameButton.onClick.AddListener(StartGame);
        if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(LeaveRoom);
        if (swapCharacterButton != null) swapCharacterButton.onClick.AddListener(SwapCharacter);

        // 聊天
        if (sendChatButton != null) sendChatButton.onClick.AddListener(SendChat);

        // 匹配
        if (cancelMatchButton != null) cancelMatchButton.onClick.AddListener(CancelMatch);

        ShowMainPanel();
    }

    void Update()
    {
        if (isMatching)
        {
            matchTimer += Time.unscaledDeltaTime;
            UpdateMatchTimer();
            RotateSpinner();
        }
    }

    // ==================== 面板切换 ====================

    private void ShowMainPanel()
    {
        SetActivePanel(mainPanel);
    }

    private void ShowCreatePanel()
    {
        SetActivePanel(createPanel);
        PopulateChapterDropdown();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    private void ShowJoinPanel()
    {
        SetActivePanel(joinPanel);
        RefreshRoomList();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    private void ShowRoomPanel()
    {
        SetActivePanel(roomPanel);
        UpdateRoomUI();
    }

    private void ShowMatchmakingPanel()
    {
        SetActivePanel(matchmakingPanel);
    }

    private void SetActivePanel(GameObject active)
    {
        if (mainPanel != null) mainPanel.SetActive(mainPanel == active);
        if (createPanel != null) createPanel.SetActive(createPanel == active);
        if (joinPanel != null) joinPanel.SetActive(joinPanel == active);
        if (roomPanel != null) roomPanel.SetActive(roomPanel == active);
        if (matchmakingPanel != null) matchmakingPanel.SetActive(matchmakingPanel == active);
    }

    // ==================== 创建房间 ====================

    private void PopulateChapterDropdown()
    {
        if (chapterDropdown == null) return;
        chapterDropdown.ClearOptions();

        var options = new List<string>();
        for (int i = 1; i <= 5; i++)
        {
            string name = $"Chapter {i}";
            if (LocalizationSystem.Instance != null)
                name = LocalizationSystem.Instance.Get($"chapter_{i}_title", name);
            options.Add(name);
        }
        chapterDropdown.AddOptions(options);
    }

    private void ConfirmCreateRoom()
    {
        isHost = true;
        currentRoomCode = GenerateRoomCode();

        if (roomNameText != null)
        {
            string name = roomNameInput != null ? roomNameInput.text : "";
            if (string.IsNullOrEmpty(name)) name = $"Room {currentRoomCode}";
            roomNameText.text = name;
        }

        if (roomCodeText != null)
            roomCodeText.text = currentRoomCode;

        // 通知网络系统
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.CreateRoom(currentRoomCode);

        isReady = false;
        selectedLux = true;

        ShowRoomPanel();
        AddChatMessage("System", "Room created. Waiting for player...");

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_confirm");
    }

    // ==================== 加入房间 ====================

    private void JoinByCode()
    {
        if (roomCodeInput == null) return;
        string code = roomCodeInput.text.Trim().ToUpper();

        if (code.Length != 4)
        {
            AddChatMessage("System", "Invalid room code.");
            return;
        }

        isHost = false;
        currentRoomCode = code;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.JoinRoom(code);

        selectedLux = false; // 加入者默认Nox
        isReady = false;

        ShowRoomPanel();
        AddChatMessage("System", $"Joined room {code}.");

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_confirm");
    }

    private void RefreshRoomList()
    {
        // 清除旧列表
        foreach (var item in roomListItems)
        {
            if (item != null) Destroy(item);
        }
        roomListItems.Clear();

        if (RoomDiscovery.Instance == null || roomListItemPrefab == null || roomListContainer == null)
            return;

        var rooms = RoomDiscovery.Instance.GetAvailableRooms();

        if (roomCountText != null)
            roomCountText.text = $"{rooms.Count} rooms";

        foreach (var room in rooms)
        {
            var itemObj = Instantiate(roomListItemPrefab, roomListContainer);
            roomListItems.Add(itemObj);

            var texts = itemObj.GetComponentsInChildren<TextMeshProUGUI>();
            var button = itemObj.GetComponentInChildren<Button>();

            if (texts.Length > 0) texts[0].text = room.hostName;
            if (texts.Length > 1) texts[1].text = room.ipAddress;
            if (texts.Length > 2) texts[2].text = room.roomCode;

            if (button != null)
            {
                string code = room.roomCode;
                button.onClick.AddListener(() =>
                {
                    if (roomCodeInput != null)
                        roomCodeInput.text = code;
                    JoinByCode();
                });
            }
        }

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    // ==================== 房间内操作 ====================

    private void UpdateRoomUI()
    {
        // 准备按钮
        if (readyButtonText != null)
        {
            string text = isReady ? "Cancel" : "Ready";
            if (LocalizationSystem.Instance != null)
                text = LocalizationSystem.Instance.Get(isReady ? "lobby_cancel_ready" : "lobby_ready", text);
            readyButtonText.text = text;
        }

        // 仅主机可开始
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(isHost);

        // 角色图标
        UpdateCharacterIcons();

        // 准备状态图标
        if (player1ReadyIcon != null)
            player1ReadyIcon.color = isHost && isReady ? readyColor : notReadyColor;
    }

    private void ToggleReady()
    {
        isReady = !isReady;
        UpdateRoomUI();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(isReady ? "ui_confirm" : "ui_click");
    }

    private void SwapCharacter()
    {
        selectedLux = !selectedLux;
        UpdateCharacterIcons();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    private void UpdateCharacterIcons()
    {
        Sprite myIcon = selectedLux ? luxPortrait : noxPortrait;
        Sprite otherIcon = selectedLux ? noxPortrait : luxPortrait;

        if (isHost)
        {
            if (player1CharacterIcon != null && myIcon != null) player1CharacterIcon.sprite = myIcon;
            if (player2CharacterIcon != null && otherIcon != null) player2CharacterIcon.sprite = otherIcon;
        }
        else
        {
            if (player2CharacterIcon != null && myIcon != null) player2CharacterIcon.sprite = myIcon;
            if (player1CharacterIcon != null && otherIcon != null) player1CharacterIcon.sprite = otherIcon;
        }
    }

    private void StartGame()
    {
        if (!isHost) return;

        AddChatMessage("System", "Starting game...");

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.StartGame();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_confirm");
    }

    private void LeaveRoom()
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.LeaveRoom();

        ShowMainPanel();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    private void CopyRoomCode()
    {
        if (!string.IsNullOrEmpty(currentRoomCode))
        {
            GUIUtility.systemCopyBuffer = currentRoomCode;
            AddChatMessage("System", "Room code copied!");

            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play("ui_confirm");
        }
    }

    // ==================== 快速匹配 ====================

    private void StartQuickMatch()
    {
        isMatching = true;
        matchTimer = 0;

        ShowMatchmakingPanel();

        if (matchStatusText != null)
        {
            string text = "Searching for players...";
            if (LocalizationSystem.Instance != null)
                text = LocalizationSystem.Instance.Get("lobby_searching", text);
            matchStatusText.text = text;
        }

        // 通知网络系统
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.StartQuickMatch();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    private void CancelMatch()
    {
        isMatching = false;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.CancelQuickMatch();

        ShowMainPanel();
    }

    private void UpdateMatchTimer()
    {
        if (matchTimerText == null) return;
        int seconds = Mathf.FloorToInt(matchTimer);
        matchTimerText.text = $"{seconds / 60:D2}:{seconds % 60:D2}";
    }

    private void RotateSpinner()
    {
        if (matchSpinner != null)
            matchSpinner.transform.Rotate(0, 0, -200f * Time.unscaledDeltaTime);
    }

    // ==================== 聊天 ====================

    private void SendChat()
    {
        if (chatInput == null || string.IsNullOrEmpty(chatInput.text)) return;

        string message = chatInput.text;
        chatInput.text = "";

        string name = isHost ? "Host" : "Guest";
        AddChatMessage(name, message);

        // 通过网络发送
        // NetworkGameManager.Instance?.SendChatRpc(message);
    }

    private void AddChatMessage(string sender, string message)
    {
        if (chatContent == null) return;

        string formatted = $"<b>{sender}:</b> {message}\n";
        chatContent.text += formatted;

        // 滚动到底部
        if (chatScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScroll.verticalNormalizedPosition = 0f;
        }
    }

    // ==================== 工具 ====================

    private void BackToMenu()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene("MainMenu");
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] code = new char[4];
        for (int i = 0; i < 4; i++)
            code[i] = chars[Random.Range(0, chars.Length)];
        return new string(code);
    }

    /// <summary>
    /// 外部通知：玩家加入房间
    /// </summary>
    public void OnPlayerJoined(string playerName)
    {
        if (player2Name != null)
            player2Name.text = playerName;

        AddChatMessage("System", $"{playerName} joined the room.");

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("player_join");
    }

    /// <summary>
    /// 外部通知：玩家离开房间
    /// </summary>
    public void OnPlayerLeft(string playerName)
    {
        if (player2Name != null)
            player2Name.text = "Waiting...";

        AddChatMessage("System", $"{playerName} left the room.");
    }

    /// <summary>
    /// 外部通知：匹配成功
    /// </summary>
    public void OnMatchFound(string roomCode)
    {
        isMatching = false;
        currentRoomCode = roomCode;
        isHost = false;
        selectedLux = false;
        isReady = false;

        ShowRoomPanel();
        AddChatMessage("System", "Match found!");

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_confirm");
    }
}

// RoomInfo定义在RoomDiscovery.cs中
