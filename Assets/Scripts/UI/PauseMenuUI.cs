using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 增强暂停菜单 - 集成设置、成就、统计快捷入口
/// 支持手柄导航和本地化文本
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private CanvasGroup pauseCanvasGroup;

    [Header("按钮")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button achievementsButton;
    [SerializeField] private Button statsButton;
    [SerializeField] private Button mainMenuButton;

    [Header("关联面板")]
    [SerializeField] private SettingsUI settingsUI;
    [SerializeField] private AchievementPanelUI achievementPanelUI;
    [SerializeField] private StatsUI statsUI;

    [Header("关卡信息")]
    [SerializeField] private TextMeshProUGUI levelNameText;
    [SerializeField] private TextMeshProUGUI playTimeText;
    [SerializeField] private TextMeshProUGUI collectiblesText;

    [Header("动画")]
    [SerializeField] private float fadeInSpeed = 5f;

    private bool isPaused;

    void Start()
    {
        resumeButton?.onClick.AddListener(OnResume);
        restartButton?.onClick.AddListener(OnRestart);
        settingsButton?.onClick.AddListener(OnSettings);
        achievementsButton?.onClick.AddListener(OnAchievements);
        statsButton?.onClick.AddListener(OnStats);
        mainMenuButton?.onClick.AddListener(OnMainMenu);

        // 点击音效
        var buttons = new Button[] { resumeButton, restartButton, settingsButton,
            achievementsButton, statsButton, mainMenuButton };
        foreach (var btn in buttons)
        {
            if (btn != null)
            {
                btn.onClick.AddListener(() =>
                {
                    if (SoundFeedback.Instance != null)
                        SoundFeedback.Instance.PlayUIClick();
                });
            }
        }

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    void Update()
    {
        // ESC 或手柄 Start 键暂停/恢复
        bool pausePressed = Input.GetKeyDown(KeyCode.Escape);

        #if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Gamepad.current != null)
            pausePressed |= UnityEngine.InputSystem.Gamepad.current.startButton.wasPressedThisFrame;
        #endif

        if (pausePressed)
        {
            if (!isPaused && CanPause())
                ShowPause();
            else if (isPaused)
                OnResume();
        }
    }

    private bool CanPause()
    {
        if (GameFlowManager.Instance != null)
        {
            var state = GameFlowManager.Instance.CurrentState;
            return state == GameFlowManager.FlowState.Playing ||
                   state == GameFlowManager.FlowState.BossBattle;
        }

        return GameManager.Instance?.CurrentState == GameManager.GameState.Playing;
    }

    /// <summary>
    /// 显示暂停菜单
    /// </summary>
    public void ShowPause()
    {
        isPaused = true;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (pauseCanvasGroup != null)
            pauseCanvasGroup.alpha = 1;

        // 更新关卡信息
        UpdateLevelInfo();

        // 暂停游戏
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.PauseGame();
        else
            GameManager.Instance?.PauseGame();

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayUIClick();
    }

    private void UpdateLevelInfo()
    {
        if (GameFlowManager.Instance != null)
        {
            int ch = GameFlowManager.Instance.CurrentChapter;
            int lv = GameFlowManager.Instance.CurrentLevel;

            if (levelNameText != null)
            {
                string chapterKey = $"chapter_{ch}";
                if (LocalizationSystem.Instance != null)
                    levelNameText.text = $"{LocalizationSystem.Instance.Get(chapterKey)} - {lv}";
                else
                    levelNameText.text = $"第{ch}章 - 第{lv}关";
            }
        }

        if (playTimeText != null && LevelManager.Instance != null)
        {
            float time = LevelManager.Instance.ElapsedTime;
            int minutes = Mathf.FloorToInt(time / 60);
            int seconds = Mathf.FloorToInt(time % 60);
            playTimeText.text = $"{minutes}:{seconds:D2}";
        }

        if (collectiblesText != null && LevelManager.Instance != null)
        {
            collectiblesText.text = $"{LevelManager.Instance.CollectedCount}/{LevelManager.Instance.TotalCollectibles}";
        }
    }

    private void OnResume()
    {
        isPaused = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.ResumeGame();
        else
            GameManager.Instance?.ResumeGame();
    }

    private void OnRestart()
    {
        isPaused = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ResumeGame();
            GameFlowManager.Instance.RetryLevel();
        }
        else
        {
            GameManager.Instance?.ResumeGame();
            LevelManager.Instance?.RestartLevel();
        }
    }

    private void OnSettings()
    {
        if (settingsUI != null)
            settingsUI.Show();
    }

    private void OnAchievements()
    {
        if (achievementPanelUI != null)
            achievementPanelUI.Show();
    }

    private void OnStats()
    {
        if (statsUI != null)
            statsUI.Show();
    }

    private void OnMainMenu()
    {
        isPaused = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        Time.timeScale = 1;

        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.GoToMainMenu();
        else
            GameManager.Instance?.ReturnToMainMenu();
    }
}
