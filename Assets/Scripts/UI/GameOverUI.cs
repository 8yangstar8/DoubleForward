using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 游戏结束界面 - 死亡后的重试/退出UI
/// 带有统计信息和激励文案
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private CanvasGroup panelCanvas;
    [SerializeField] private RectTransform contentContainer;

    [Header("信息")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI deathCountText;
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("按钮")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button checkpointButton;    // 从上次检查点开始
    [SerializeField] private Button easyModeButton;      // 降低难度重试
    [SerializeField] private Button quitButton;

    [Header("提示")]
    [SerializeField] private TextMeshProUGUI hintText;   // 死亡提示

    [Header("动画")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float delayBeforeButtons = 0.8f;
    [SerializeField] private CanvasGroup buttonsGroup;

    private string[] deathMessages;
    private string[] encourageMessages;

    void Awake()
    {
        if (panelCanvas != null) panelCanvas.gameObject.SetActive(false);

        SetupButtons();
        InitMessages();

        // 监听游戏状态
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.OnStateChanged += OnGameStateChanged;
    }

    void OnDestroy()
    {
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.OnStateChanged -= OnGameStateChanged;
    }

    private void SetupButtons()
    {
        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetry);
        if (checkpointButton != null)
            checkpointButton.onClick.AddListener(OnRetryFromCheckpoint);
        if (easyModeButton != null)
            easyModeButton.onClick.AddListener(OnEasyModeRetry);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuit);
    }

    private void InitMessages()
    {
        deathMessages = new string[]
        {
            "gameover_msg_1",
            "gameover_msg_2",
            "gameover_msg_3"
        };

        encourageMessages = new string[]
        {
            "gameover_encourage_1",
            "gameover_encourage_2",
            "gameover_encourage_3"
        };
    }

    private void OnGameStateChanged(GameFlowManager.FlowState oldState, GameFlowManager.FlowState newState)
    {
        if (newState == GameFlowManager.FlowState.GameOver)
        {
            Show();
        }
        else if (oldState == GameFlowManager.FlowState.GameOver)
        {
            Hide();
        }
    }

    /// <summary>
    /// 显示GameOver界面
    /// </summary>
    public void Show()
    {
        StartCoroutine(ShowSequence());
    }

    private IEnumerator ShowSequence()
    {
        if (panelCanvas == null) yield break;

        panelCanvas.gameObject.SetActive(true);
        panelCanvas.alpha = 0;

        if (buttonsGroup != null) buttonsGroup.alpha = 0;

        // 设置标题
        if (titleText != null)
        {
            string title = "gameover_title";
            if (LocalizationSystem.Instance != null)
                title = LocalizationSystem.Instance.Get("gameover_title", "Game Over");
            titleText.text = title;
        }

        // 随机死亡感言
        if (messageText != null)
        {
            string msgKey = deathMessages[Random.Range(0, deathMessages.Length)];
            string msg = msgKey;
            if (LocalizationSystem.Instance != null)
                msg = LocalizationSystem.Instance.Get(msgKey, "Don't give up!");
            messageText.text = msg;
        }

        // 死亡次数
        if (deathCountText != null && GameStats.Instance != null)
        {
            int deaths = GameStats.Instance.TotalDeaths;
            deathCountText.text = $"x{deaths}";
        }

        // 进度提示
        if (progressText != null && SaveSystem.Instance != null)
        {
            float percent = SaveSystem.Instance.Data.completionPercent;
            progressText.text = $"{percent:F0}%";
        }

        // 死亡提示/攻略
        if (hintText != null)
        {
            string hintKey = encourageMessages[Random.Range(0, encourageMessages.Length)];
            string hint = hintKey;
            if (LocalizationSystem.Instance != null)
                hint = LocalizationSystem.Instance.Get(hintKey, "Try a different approach!");
            hintText.text = hint;
        }

        // 简单模式按钮：只在多次死亡后显示
        if (easyModeButton != null && DifficultyManager.Instance != null)
        {
            bool showEasy = DifficultyManager.Instance.CurrentDifficulty != DifficultyManager.DifficultyLevel.Easy;
            easyModeButton.gameObject.SetActive(showEasy);
        }

        // 淡入
        float t = 0;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            panelCanvas.alpha = t / fadeInDuration;
            yield return null;
        }
        panelCanvas.alpha = 1f;

        // 等待后显示按钮
        yield return new WaitForSecondsRealtime(delayBeforeButtons);

        if (buttonsGroup != null)
        {
            t = 0;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                buttonsGroup.alpha = t / 0.3f;
                yield return null;
            }
            buttonsGroup.alpha = 1f;
        }
    }

    public void Hide()
    {
        if (panelCanvas != null)
            panelCanvas.gameObject.SetActive(false);
    }

    private void OnRetry()
    {
        Time.timeScale = 1;
        GameFlowManager.Instance?.RetryLevel();
    }

    private void OnRetryFromCheckpoint()
    {
        Time.timeScale = 1;

        // 从检查点恢复
        if (RespawnSystem.Instance != null)
        {
            RespawnSystem.Instance.ReviveAllPlayers();
            GameFlowManager.Instance?.StartPlaying();
        }
        else
        {
            GameFlowManager.Instance?.RetryLevel();
        }
    }

    private void OnEasyModeRetry()
    {
        Time.timeScale = 1;

        // 切换到简单模式
        if (DifficultyManager.Instance != null)
            DifficultyManager.Instance.SetDifficulty(DifficultyManager.DifficultyLevel.Easy);

        GameFlowManager.Instance?.RetryLevel();
    }

    private void OnQuit()
    {
        Time.timeScale = 1;
        GameFlowManager.Instance?.GoToMainMenu();
    }
}
