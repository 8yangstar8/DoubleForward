using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections;

/// <summary>
/// 云存档管理器 - 支持存档上传/下载/同步
/// 使用简单HTTP API与自定义后端或Firebase通信
/// 在本地存档的基础上提供云端备份
/// </summary>
public class CloudSaveManager : MonoBehaviour
{
    public static CloudSaveManager Instance { get; private set; }

    [Header("服务器配置")]
    [SerializeField] private string serverUrl = "https://your-server.com/api/save";
    [SerializeField] private float autoSyncInterval = 300f;  // 5分钟自动同步
    [SerializeField] private bool enableCloudSave = false;    // 默认关闭，需要配置服务器后开启

    [Header("状态")]
    [SerializeField] private bool isSyncing;

    public enum SyncState { Idle, Uploading, Downloading, Error, Success }
    public SyncState CurrentState { get; private set; } = SyncState.Idle;
    public string LastError { get; private set; }
    public DateTime LastSyncTime { get; private set; }

    private const string LAST_SYNC_KEY = "cloud_last_sync";
    private const string CLOUD_ENABLED_KEY = "cloud_save_enabled";

    public event Action<SyncState> OnSyncStateChanged;
    public event Action<bool, string> OnSyncComplete; // success, message

    private float syncTimer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        enableCloudSave = PlayerPrefs.GetInt(CLOUD_ENABLED_KEY, 0) == 1;
        string lastSync = PlayerPrefs.GetString(LAST_SYNC_KEY, "");
        if (DateTime.TryParse(lastSync, out DateTime parsed))
            LastSyncTime = parsed;
    }

    void Update()
    {
        if (!enableCloudSave || isSyncing) return;

        syncTimer += Time.unscaledDeltaTime;
        if (syncTimer >= autoSyncInterval)
        {
            syncTimer = 0;
            UploadSave();
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 启用/禁用云存档
    /// </summary>
    public void SetCloudSaveEnabled(bool enabled)
    {
        enableCloudSave = enabled;
        PlayerPrefs.SetInt(CLOUD_ENABLED_KEY, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public bool IsCloudSaveEnabled() => enableCloudSave;

    /// <summary>
    /// 上传当前存档到云端
    /// </summary>
    public void UploadSave()
    {
        if (isSyncing || !enableCloudSave) return;
        StartCoroutine(UploadCoroutine());
    }

    /// <summary>
    /// 从云端下载存档
    /// </summary>
    public void DownloadSave()
    {
        if (isSyncing || !enableCloudSave) return;
        StartCoroutine(DownloadCoroutine());
    }

    /// <summary>
    /// 手动同步（先上传再下载对比）
    /// </summary>
    public void SyncSave()
    {
        if (isSyncing || !enableCloudSave) return;
        StartCoroutine(SyncCoroutine());
    }

    /// <summary>
    /// 获取上次同步时间的显示文本
    /// </summary>
    public string GetLastSyncDisplay()
    {
        if (LastSyncTime == DateTime.MinValue)
        {
            string never = "Never synced";
            if (LocalizationSystem.Instance != null)
                never = LocalizationSystem.Instance.Get("cloud_never_synced", never);
            return never;
        }

        TimeSpan ago = DateTime.UtcNow - LastSyncTime;

        if (ago.TotalMinutes < 1) return "Just now";
        if (ago.TotalMinutes < 60) return $"{(int)ago.TotalMinutes}m ago";
        if (ago.TotalHours < 24) return $"{(int)ago.TotalHours}h ago";
        return LastSyncTime.ToString("MM/dd HH:mm");
    }

    // ==================== 网络操作 ====================

    private IEnumerator UploadCoroutine()
    {
        isSyncing = true;
        SetState(SyncState.Uploading);

        // 读取本地存档
        if (SaveSystem.Instance == null)
        {
            HandleError("SaveSystem not available");
            yield break;
        }

        SaveSystem.Instance.Save(); // 确保最新

        string savePath = Path.Combine(Application.persistentDataPath, "Saves",
            $"save_slot_{SaveSystem.Instance.ActiveSlot}.json");

        if (!File.Exists(savePath))
        {
            HandleError("No save data to upload");
            yield break;
        }

        string saveJson = File.ReadAllText(savePath);
        string deviceId = SystemInfo.deviceUniqueIdentifier;

        // 构建上传数据
        var uploadData = new CloudSavePayload
        {
            deviceId = deviceId,
            slotIndex = SaveSystem.Instance.ActiveSlot,
            saveData = saveJson,
            timestamp = DateTime.UtcNow.ToString("o"),
            gameVersion = Application.version
        };

        string json = JsonUtility.ToJson(uploadData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        using (var request = new UnityWebRequest($"{serverUrl}/upload", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Device-Id", deviceId);
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                RecordSyncTime();
                SetState(SyncState.Success);
                OnSyncComplete?.Invoke(true, "Upload successful");
            }
            else
            {
                HandleError($"Upload failed: {request.error}");
            }
        }

        isSyncing = false;
    }

    private IEnumerator DownloadCoroutine()
    {
        isSyncing = true;
        SetState(SyncState.Downloading);

        string deviceId = SystemInfo.deviceUniqueIdentifier;
        int slot = SaveSystem.Instance != null ? SaveSystem.Instance.ActiveSlot : 0;

        string url = $"{serverUrl}/download?deviceId={deviceId}&slot={slot}";

        using (var request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("X-Device-Id", deviceId);
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseJson = request.downloadHandler.text;

                try
                {
                    var payload = JsonUtility.FromJson<CloudSavePayload>(responseJson);

                    if (!string.IsNullOrEmpty(payload.saveData))
                    {
                        // 写入本地
                        string savePath = Path.Combine(Application.persistentDataPath, "Saves",
                            $"save_slot_{slot}.json");

                        // 备份当前本地存档
                        if (File.Exists(savePath))
                        {
                            string backupPath = savePath + ".cloud_backup";
                            File.Copy(savePath, backupPath, true);
                        }

                        File.WriteAllText(savePath, payload.saveData);

                        // 重新加载
                        if (SaveSystem.Instance != null)
                            SaveSystem.Instance.Load(slot);

                        RecordSyncTime();
                        SetState(SyncState.Success);
                        OnSyncComplete?.Invoke(true, "Download successful");
                    }
                    else
                    {
                        HandleError("No cloud save data found");
                    }
                }
                catch (Exception e)
                {
                    HandleError($"Parse error: {e.Message}");
                }
            }
            else
            {
                HandleError($"Download failed: {request.error}");
            }
        }

        isSyncing = false;
    }

    private IEnumerator SyncCoroutine()
    {
        // 先尝试上传
        yield return UploadCoroutine();

        // 短暂延迟
        yield return new WaitForSecondsRealtime(0.5f);

        // 查询云端版本时间戳对比
        // 如果云端更新则下载
        // 简化实现：上传成功即同步完成
    }

    // ==================== 工具方法 ====================

    private void SetState(SyncState state)
    {
        CurrentState = state;
        OnSyncStateChanged?.Invoke(state);
    }

    private void HandleError(string error)
    {
        LastError = error;
        SetState(SyncState.Error);
        OnSyncComplete?.Invoke(false, error);
        isSyncing = false;
        Debug.LogWarning($"[CloudSave] {error}");
    }

    private void RecordSyncTime()
    {
        LastSyncTime = DateTime.UtcNow;
        PlayerPrefs.SetString(LAST_SYNC_KEY, LastSyncTime.ToString("o"));
        PlayerPrefs.Save();
    }

    [Serializable]
    private class CloudSavePayload
    {
        public string deviceId;
        public int slotIndex;
        public string saveData;
        public string timestamp;
        public string gameVersion;
    }
}
