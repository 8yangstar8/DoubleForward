using UnityEngine;
using System.Collections;

/// <summary>
/// 移动端服务 - 应用评分提示、社交分享、应用内反馈
/// 非侵入式地引导玩家评价和分享
/// </summary>
public class MobileServices : MonoBehaviour
{
    public static MobileServices Instance { get; private set; }

    [Header("评分提示设置")]
    [SerializeField] private int levelsBeforeFirstPrompt = 5;     // 通过几关后首次提示
    [SerializeField] private int levelsBeforeRetryPrompt = 15;    // 拒绝后再过几关重试
    [SerializeField] private int maxPromptCount = 3;               // 最多提示次数
    [SerializeField] private float minPlayTimeBeforePrompt = 600f; // 至少玩10分钟

    [Header("分享设置")]
    [SerializeField] private string gameTitle = "双向前行 Double Forward";
    [SerializeField] private string shareHashtag = "#DoubleForward";
    [SerializeField] private string playStoreUrl = "https://play.google.com/store/apps/details?id=com.yourstudio.doubleforward";

    [Header("UI引用")]
    [SerializeField] private GameObject ratePanel;
    [SerializeField] private UnityEngine.UI.Button rateYesButton;
    [SerializeField] private UnityEngine.UI.Button rateLaterButton;
    [SerializeField] private UnityEngine.UI.Button rateNeverButton;
    [SerializeField] private TMPro.TextMeshProUGUI rateMessageText;

    [Header("分享UI")]
    [SerializeField] private GameObject sharePanel;
    [SerializeField] private UnityEngine.UI.Button shareButton;
    [SerializeField] private UnityEngine.UI.Button shareCloseButton;
    [SerializeField] private TMPro.TextMeshProUGUI sharePreviewText;

    // PlayerPrefs keys
    private const string PREFS_RATED = "app_rated";
    private const string PREFS_NEVER_RATE = "app_never_rate";
    private const string PREFS_PROMPT_COUNT = "rate_prompt_count";
    private const string PREFS_LAST_PROMPT_LEVEL = "rate_last_prompt_level";
    private const string PREFS_TOTAL_PLAY_TIME = "total_play_time_for_rate";
    private const string PREFS_SHARE_COUNT = "share_count";

    private bool hasRated;
    private bool neverAsk;
    private int promptCount;
    private int lastPromptLevelCount;
    private float accumulatedPlayTime;

    public event System.Action OnRateAccepted;
    public event System.Action OnShareCompleted;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadState();
        SetupUI();
    }

    void Update()
    {
        // 累计游戏时间
        if (GameFlowManager.Instance != null &&
            GameFlowManager.Instance.CurrentState == GameFlowManager.FlowState.Playing)
        {
            accumulatedPlayTime += Time.unscaledDeltaTime;
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) SaveState();
    }

    void OnApplicationQuit()
    {
        SaveState();
    }

    // ============ 评分系统 ============

    /// <summary>
    /// 检查是否应该显示评分提示（在关卡完成时调用）
    /// </summary>
    public bool ShouldShowRatePrompt(int totalLevelsCompleted)
    {
        if (hasRated || neverAsk) return false;
        if (promptCount >= maxPromptCount) return false;
        if (accumulatedPlayTime < minPlayTimeBeforePrompt) return false;

        // 首次提示
        if (promptCount == 0 && totalLevelsCompleted >= levelsBeforeFirstPrompt)
            return true;

        // 重试提示
        if (promptCount > 0 &&
            totalLevelsCompleted >= lastPromptLevelCount + levelsBeforeRetryPrompt)
            return true;

        return false;
    }

    /// <summary>
    /// 显示评分提示面板
    /// </summary>
    public void ShowRatePrompt(int totalLevelsCompleted)
    {
        if (ratePanel == null) return;

        promptCount++;
        lastPromptLevelCount = totalLevelsCompleted;
        SaveState();

        // 设置提示文字
        if (rateMessageText != null)
        {
            string msg = "rate_message";
            if (LocalizationSystem.Instance != null)
                msg = LocalizationSystem.Instance.Get("rate_message",
                    "Enjoying the game? Please rate us!");

            rateMessageText.text = msg;
        }

        ratePanel.SetActive(true);
    }

    /// <summary>
    /// 玩家同意评分
    /// </summary>
    public void OnRateYes()
    {
        hasRated = true;
        SaveState();

        if (ratePanel != null) ratePanel.SetActive(false);

        // 尝试使用Android原生评分API
        OpenStoreForRating();

        OnRateAccepted?.Invoke();
    }

    /// <summary>
    /// 稍后再说
    /// </summary>
    public void OnRateLater()
    {
        if (ratePanel != null) ratePanel.SetActive(false);
    }

    /// <summary>
    /// 不再询问
    /// </summary>
    public void OnRateNever()
    {
        neverAsk = true;
        SaveState();

        if (ratePanel != null) ratePanel.SetActive(false);
    }

    /// <summary>
    /// 打开应用商店评分页面
    /// </summary>
    private void OpenStoreForRating()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // 尝试使用 Google Play In-App Review API
        try
        {
            // 如果没有接入In-App Review，降级到打开商店页面
            Application.OpenURL(playStoreUrl);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MobileServices] Failed to open store: {e.Message}");
            Application.OpenURL(playStoreUrl);
        }
#else
        Debug.Log("[MobileServices] Would open store for rating (editor mode)");
#endif
    }

    // ============ 社交分享 ============

    /// <summary>
    /// 分享游戏成就
    /// </summary>
    public void ShareAchievement(string achievementName, int stars = 0)
    {
        string shareText = BuildShareText(ShareType.Achievement, achievementName, stars);
        ExecuteShare(shareText);
    }

    /// <summary>
    /// 分享关卡完成
    /// </summary>
    public void ShareLevelComplete(int chapter, int level, int stars, float time)
    {
        string shareText = BuildShareText(ShareType.LevelComplete,
            $"Ch.{chapter} Lv.{level}", stars, time);
        ExecuteShare(shareText);
    }

    /// <summary>
    /// 分享通关
    /// </summary>
    public void ShareGameComplete(float totalTime, float completionPercent)
    {
        string shareText = BuildShareText(ShareType.GameComplete,
            "", 0, totalTime, completionPercent);
        ExecuteShare(shareText);
    }

    /// <summary>
    /// 显示分享面板（由UI调用）
    /// </summary>
    public void ShowSharePanel(string previewText)
    {
        if (sharePanel == null) return;

        if (sharePreviewText != null)
            sharePreviewText.text = previewText;

        sharePanel.SetActive(true);
    }

    /// <summary>
    /// 关闭分享面板
    /// </summary>
    public void CloseSharePanel()
    {
        if (sharePanel != null) sharePanel.SetActive(false);
    }

    /// <summary>
    /// 通用分享（截图+文字）
    /// </summary>
    public void ShareWithScreenshot(string text)
    {
        StartCoroutine(CaptureAndShare(text));
    }

    private enum ShareType
    {
        Achievement,
        LevelComplete,
        GameComplete
    }

    private string BuildShareText(ShareType type, string detail, int stars = 0,
        float time = 0, float completionPercent = 0)
    {
        string text = "";

        switch (type)
        {
            case ShareType.Achievement:
                if (LocalizationSystem.Instance != null)
                    text = LocalizationSystem.Instance.Get("share_achievement",
                        "I just unlocked '{0}' in {1}!");
                else
                    text = "I just unlocked '{0}' in {1}!";

                text = string.Format(text, detail, gameTitle);
                break;

            case ShareType.LevelComplete:
                string starStr = new string('★', stars) + new string('☆', 3 - stars);
                if (LocalizationSystem.Instance != null)
                    text = LocalizationSystem.Instance.Get("share_level_complete",
                        "Completed {0} with {1} in {2}!");
                else
                    text = "Completed {0} with {1} in {2}!";

                text = string.Format(text, detail, starStr, gameTitle);

                if (time > 0)
                    text += $" ({time:F1}s)";
                break;

            case ShareType.GameComplete:
                if (LocalizationSystem.Instance != null)
                    text = LocalizationSystem.Instance.Get("share_game_complete",
                        "I completed {0}! {1}% completion.");
                else
                    text = "I completed {0}! {1}% completion.";

                text = string.Format(text, gameTitle, completionPercent.ToString("F0"));
                break;
        }

        text += $"\n{shareHashtag}\n{playStoreUrl}";
        return text;
    }

    private void ExecuteShare(string text)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
            using (AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent"))
            {
                intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intentObject.Call<AndroidJavaObject>("setType", "text/plain");
                intentObject.Call<AndroidJavaObject>("putExtra",
                    intentClass.GetStatic<string>("EXTRA_TEXT"), text);

                using (AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>(
                        "createChooser", intentObject, "Share");
                    activity.Call("startActivity", chooser);
                }
            }

            IncrementShareCount();
            OnShareCompleted?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MobileServices] Share failed: {e.Message}");
        }
#else
        Debug.Log($"[MobileServices] Share text: {text}");
        GUIUtility.systemCopyBuffer = text;
        IncrementShareCount();
        OnShareCompleted?.Invoke();
#endif
    }

    private IEnumerator CaptureAndShare(string text)
    {
        // 等待帧结束再截图
        yield return new WaitForEndOfFrame();

        string fileName = $"doubleforward_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        string filePath = System.IO.Path.Combine(Application.temporaryCachePath, fileName);

        // 截图
        Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenshot.Apply();

        byte[] bytes = screenshot.EncodeToPNG();
        System.IO.File.WriteAllBytes(filePath, bytes);
        Object.Destroy(screenshot);

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // 通过FileProvider分享图片+文字
            using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
            using (AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent"))
            {
                intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intentObject.Call<AndroidJavaObject>("setType", "image/png");

                // 将文件路径转为URI
                using (AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject fileObj = new AndroidJavaObject("java.io.File", filePath))
                using (AndroidJavaClass fileProvider = new AndroidJavaClass("androidx.core.content.FileProvider"))
                {
                    string authority = Application.identifier + ".fileprovider";
                    AndroidJavaObject uri = fileProvider.CallStatic<AndroidJavaObject>(
                        "getUriForFile", activity, authority, fileObj);

                    intentObject.Call<AndroidJavaObject>("putExtra",
                        intentClass.GetStatic<string>("EXTRA_STREAM"), uri);
                    intentObject.Call<AndroidJavaObject>("putExtra",
                        intentClass.GetStatic<string>("EXTRA_TEXT"), text);
                    intentObject.Call<AndroidJavaObject>("addFlags", 1); // FLAG_GRANT_READ_URI_PERMISSION

                    AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>(
                        "createChooser", intentObject, "Share");
                    activity.Call("startActivity", chooser);
                }
            }

            IncrementShareCount();
            OnShareCompleted?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MobileServices] Screenshot share failed: {e.Message}");
            // 降级为文字分享
            ExecuteShare(text);
        }
#else
        Debug.Log($"[MobileServices] Screenshot saved to: {filePath}");
        ExecuteShare(text);
#endif
    }

    // ============ 应用内反馈 ============

    /// <summary>
    /// 发送反馈邮件
    /// </summary>
    public void SendFeedbackEmail()
    {
        string subject = System.Uri.EscapeDataString($"{gameTitle} Feedback");
        string body = System.Uri.EscapeDataString(GetDeviceInfo());
        string email = "feedback@yourstudio.com";

        Application.OpenURL($"mailto:{email}?subject={subject}&body={body}");
    }

    private string GetDeviceInfo()
    {
        return $"\n\n---\nDevice: {SystemInfo.deviceModel}\n" +
               $"OS: {SystemInfo.operatingSystem}\n" +
               $"GPU: {SystemInfo.graphicsDeviceName}\n" +
               $"RAM: {SystemInfo.systemMemorySize}MB\n" +
               $"Version: {Application.version}\n" +
               $"Language: {Application.systemLanguage}";
    }

    // ============ 统计 ============

    private void IncrementShareCount()
    {
        int count = PlayerPrefs.GetInt(PREFS_SHARE_COUNT, 0) + 1;
        PlayerPrefs.SetInt(PREFS_SHARE_COUNT, count);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 获取分享次数
    /// </summary>
    public int GetShareCount()
    {
        return PlayerPrefs.GetInt(PREFS_SHARE_COUNT, 0);
    }

    // ============ 持久化 ============

    private void SetupUI()
    {
        if (rateYesButton != null) rateYesButton.onClick.AddListener(OnRateYes);
        if (rateLaterButton != null) rateLaterButton.onClick.AddListener(OnRateLater);
        if (rateNeverButton != null) rateNeverButton.onClick.AddListener(OnRateNever);

        if (shareButton != null)
            shareButton.onClick.AddListener(() => ShareWithScreenshot(BuildShareText(
                ShareType.LevelComplete, "latest", 3)));

        if (shareCloseButton != null)
            shareCloseButton.onClick.AddListener(CloseSharePanel);

        if (ratePanel != null) ratePanel.SetActive(false);
        if (sharePanel != null) sharePanel.SetActive(false);
    }

    private void SaveState()
    {
        PlayerPrefs.SetInt(PREFS_RATED, hasRated ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_NEVER_RATE, neverAsk ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PROMPT_COUNT, promptCount);
        PlayerPrefs.SetInt(PREFS_LAST_PROMPT_LEVEL, lastPromptLevelCount);
        PlayerPrefs.SetFloat(PREFS_TOTAL_PLAY_TIME, accumulatedPlayTime);
        PlayerPrefs.Save();
    }

    private void LoadState()
    {
        hasRated = PlayerPrefs.GetInt(PREFS_RATED, 0) == 1;
        neverAsk = PlayerPrefs.GetInt(PREFS_NEVER_RATE, 0) == 1;
        promptCount = PlayerPrefs.GetInt(PREFS_PROMPT_COUNT, 0);
        lastPromptLevelCount = PlayerPrefs.GetInt(PREFS_LAST_PROMPT_LEVEL, 0);
        accumulatedPlayTime = PlayerPrefs.GetFloat(PREFS_TOTAL_PLAY_TIME, 0f);
    }
}
