using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Android 权限管理器 - 处理运行时权限请求
/// 网络、存储、振动等权限的统一管理
/// </summary>
public class AndroidPermissionManager : MonoBehaviour
{
    public static AndroidPermissionManager Instance { get; private set; }

    // Android权限常量
    public static class Permissions
    {
        public const string Internet = "android.permission.INTERNET";
        public const string AccessNetworkState = "android.permission.ACCESS_NETWORK_STATE";
        public const string AccessWifiState = "android.permission.ACCESS_WIFI_STATE";
        public const string ChangeWifiMulticast = "android.permission.CHANGE_WIFI_MULTICAST_STATE";
        public const string Vibrate = "android.permission.VIBRATE";
        public const string WriteExternalStorage = "android.permission.WRITE_EXTERNAL_STORAGE";
        public const string ReadExternalStorage = "android.permission.READ_EXTERNAL_STORAGE";
        public const string WakeLock = "android.permission.WAKE_LOCK";
    }

    public event System.Action<string, bool> OnPermissionResult;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 检查权限是否已授予
    /// </summary>
    public bool HasPermission(string permission)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission);
#else
        return true;
#endif
    }

    /// <summary>
    /// 请求单个权限
    /// </summary>
    public void RequestPermission(string permission, System.Action<bool> callback = null)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (HasPermission(permission))
        {
            callback?.Invoke(true);
            return;
        }

        var callbacks = new UnityEngine.Android.PermissionCallbacks();
        callbacks.PermissionGranted += (perm) =>
        {
            callback?.Invoke(true);
            OnPermissionResult?.Invoke(perm, true);
        };
        callbacks.PermissionDenied += (perm) =>
        {
            callback?.Invoke(false);
            OnPermissionResult?.Invoke(perm, false);
        };
        callbacks.PermissionDeniedAndDontAskAgain += (perm) =>
        {
            callback?.Invoke(false);
            OnPermissionResult?.Invoke(perm, false);
        };

        UnityEngine.Android.Permission.RequestUserPermission(permission, callbacks);
#else
        callback?.Invoke(true);
#endif
    }

    /// <summary>
    /// 请求多个权限
    /// </summary>
    public void RequestPermissions(string[] permissions, System.Action<Dictionary<string, bool>> callback = null)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var results = new Dictionary<string, bool>();
        int pending = permissions.Length;

        foreach (var perm in permissions)
        {
            RequestPermission(perm, (granted) =>
            {
                results[perm] = granted;
                pending--;
                if (pending <= 0)
                    callback?.Invoke(results);
            });
        }
#else
        var results = new Dictionary<string, bool>();
        foreach (var perm in permissions)
            results[perm] = true;
        callback?.Invoke(results);
#endif
    }

    /// <summary>
    /// 请求网络相关权限（用于联机模式）
    /// </summary>
    public void RequestNetworkPermissions(System.Action<bool> callback = null)
    {
        var perms = new string[]
        {
            Permissions.Internet,
            Permissions.AccessNetworkState,
            Permissions.AccessWifiState,
            Permissions.ChangeWifiMulticast
        };

        RequestPermissions(perms, (results) =>
        {
            bool allGranted = true;
            foreach (var r in results)
            {
                if (!r.Value) { allGranted = false; break; }
            }
            callback?.Invoke(allGranted);
        });
    }

    /// <summary>
    /// 请求存储权限（用于存档）
    /// </summary>
    public void RequestStoragePermissions(System.Action<bool> callback = null)
    {
        var perms = new string[]
        {
            Permissions.ReadExternalStorage,
            Permissions.WriteExternalStorage
        };

        RequestPermissions(perms, (results) =>
        {
            bool allGranted = true;
            foreach (var r in results)
            {
                if (!r.Value) { allGranted = false; break; }
            }
            callback?.Invoke(allGranted);
        });
    }

    /// <summary>
    /// 获取网络连接状态
    /// </summary>
    public bool IsNetworkAvailable()
    {
        return Application.internetReachability != NetworkReachability.NotReachable;
    }

    /// <summary>
    /// 获取WiFi连接状态
    /// </summary>
    public bool IsWifiConnected()
    {
        return Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork;
    }

    /// <summary>
    /// 触发设备振动（短振动）
    /// </summary>
    public void VibrateDevice()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (HasPermission(Permissions.Vibrate))
            Handheld.Vibrate();
#endif
    }

    /// <summary>
    /// 防止屏幕自动关闭（游戏中保持屏幕常亮）
    /// </summary>
    public void SetScreenAlwaysOn(bool alwaysOn)
    {
        Screen.sleepTimeout = alwaysOn ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
    }
}
