using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 主菜单UI - 游戏入口界面
/// 包含：继续游戏、新游戏、模式选择、NG+、Boss连战、故事回顾、设置
/// 通关后解锁额外选项
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("主按钮")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button localPlayButton;
    [SerializeField] private Button lanPlayButton;
    [SerializeField] private Button onlinePlayButton;
    [SerializeField] private Button settingsButton;

    [Header("通关后解锁按钮")]
    [SerializeField] private Button newGamePlusButton;
    [SerializeField] private Button bossRushButton;
    [SerializeField] private Button storyRecapButton;
    [SerializeField] private Button challengeModeButton;

    [Header("面板")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject modeSelectPanel;
    [SerializeField] private GameObject newGamePlusPanel;

    [Header("显示")]
    [SerializeField] private TextMeshProUGUI versionText;
    [SerializeField] private TextMeshProUGUI saveInfoText;
    [SerializeField] private TextMeshProUGUI ngPlusLabelText;
    [SerializeField] private GameObject postGameGroup;        // 通关后内容的父容器

    [Header("动画")]
    [SerializeField] private CanvasGroup mainCanvasGroup;
    [SerializeField] private float fadeInDuration = 0.5f;

    void Start()
    {
        // 核心按钮
        continueButton?.onClick.AddListener(OnContinue);
        newGameButton?.onClick.AddListener(OnNewGame);
        localPlayButton?.onClick.AddListener(OnLocalPlay);
        lanPlayButton?.onClick.AddListener(OnLanPlay);
        onlinePlayButton?.onClick.AddListener(OnOnlinePlay);
        settingsButton?.onClick.AddListener(OnSettings);

        // 通关后按钮
        newGamePlusButton?.onClick.AddListener(OnNewGamePlus);
        bossRushButton?.onClick.AddListener(OnBossRush);
        storyRecapButton?.onClick.AddListener(OnStoryRecap);
        challengeModeButton?.onClick.AddListener(OnChallengeMode);

        RefreshUI();
        StartCoroutine(FadeInMenu());
    }

    /// <summary>
    /// 刷新主菜单状态
    /// </summary>
    public void RefreshUI()
    {
        // 继续按钮（需要存档）
        bool hasSave = SaveSystem.Instance != null && SaveSystem.Instance.Data.lastChapter > 0;
        if (continueButton != null)
            continueButton.interactable = hasSave;

        // 存档信息
        if (saveInfoText != null && hasSave)
        {
            var data = SaveSystem.Instance.Data;
            saveInfoText.text = $"Ch.{data.lastChapter}-{data.lastLevel} ★{data.totalStars}";
        }
        else if (saveInfoText != null)
        {
            saveInfoText.text = "";
        }

        // NG+显示
        bool hasCompletedGame = NewGamePlusManager.Instance?.HasCompletedGame() ?? false;

        if (postGameGroup != null)
            postGameGroup.SetActive(hasCompletedGame);

        // NG+按钮
        if (newGamePlusButton != null)
        {
            bool canNG = NewGamePlusManager.Instance?.CanStartNewGamePlus ?? false;
            newGamePlusButton.interactable = canNG;
            newGamePlusButton.gameObject.SetActive(hasCompletedGame);
        }

        // NG+标签
        if (ngPlusLabelText != null && NewGamePlusManager.Instance != null)
        {
            string ngName = NewGamePlusManager.Instance.NGDisplayName;
            ngPlusLabelText.text = string.IsNullOrEmpty(ngName) ? "" : ngName;
            ngPlusLabelText.gameObject.SetActive(!string.IsNullOrEmpty(ngName));
        }

        // Boss连战按钮
        if (bossRushButton != null)
        {
            bool bossRushUnlocked = BossRushMode.Instance?.IsUnlocked() ?? false;
            bossRushButton.interactable = bossRushUnlocked;
            bossRushButton.gameObject.SetActive(hasCompletedGame);
        }

        // 故事回顾按钮
        if (storyRecapButton != null)
        {
            int storyCount = StoryRecapSystem.Instance?.UnlockedCount ?? 0;
            storyRecapButton.interactable = storyCount > 0;
            storyRecapButton.gameObject.SetActive(storyCount > 0 || hasCompletedGame);
        }

        // 挑战模式
        if (challengeModeButton != null)
        {
            challengeModeButton.gameObject.SetActive(hasCompletedGame);
        }

        // 版本号
        if (versionText != null)
            versionText.text = $"v{Application.version}";
    }

    // ==================== 按钮回调 ====================

    private void OnContinue()
    {
        var save = SaveSystem.Instance?.Data;
        if (save != null)
        {
            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.PlayConfirm();

            GameManager.Instance?.LoadLevel(save.lastChapter, save.lastLevel);
        }
    }

    private void OnNewGame()
    {
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();

        // 选择模式
        if (modeSelectPanel != null)
        {
            mainPanel?.SetActive(false);
            modeSelectPanel.SetActive(true);
        }
        else
        {
            // 直接开始本地模式
            OnLocalPlay();
        }
    }

    private void OnLocalPlay()
    {
        InputManager.Instance?.SetPlayMode(InputManager.PlayMode.LocalSplitScreen);
        SplitScreenManager.Instance?.SetMode(SplitScreenManager.ScreenMode.SplitHorizontal);
        GameManager.Instance?.LoadLevel(1, 1);
    }

    private void OnLanPlay()
    {
        SceneLoader.Instance?.LoadScene("Lobby");
    }

    private void OnOnlinePlay()
    {
        SceneLoader.Instance?.LoadScene("Lobby");
    }

    private void OnNewGamePlus()
    {
        if (NewGamePlusManager.Instance != null && NewGamePlusManager.Instance.CanStartNewGamePlus)
        {
            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.PlayConfirm();

            NewGamePlusManager.Instance.StartNewGamePlus();

            // 刷新UI并开始新游戏
            RefreshUI();
            GameManager.Instance?.LoadLevel(1, 1);
        }
    }

    private void OnBossRush()
    {
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();

        // 找到BossRushUI并显示
        var bossRushUI = FindAnyObjectByType<BossRushUI>();
        if (bossRushUI != null)
        {
            mainPanel?.SetActive(false);
            bossRushUI.ShowStartScreen();
        }
    }

    private void OnStoryRecap()
    {
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();

        // 通过EventBus通知故事回顾UI打开
        EventBus.Publish(new HintRequestEvent
        {
            textKey = "story_recap_open",
            fallbackText = "Story Gallery",
            duration = 2f
        });

        mainPanel?.SetActive(false);
    }

    private void OnChallengeMode()
    {
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();

        if (ChallengeMode.Instance != null)
        {
            mainPanel?.SetActive(false);
            // ChallengeUI处理显示
        }
    }

    private void OnSettings()
    {
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");

        mainPanel?.SetActive(false);
        // SettingsUI处理打开
    }

    public void ShowMainPanel()
    {
        mainPanel?.SetActive(true);
        modeSelectPanel?.SetActive(false);
        RefreshUI();
    }

    // ==================== 动画 ====================

    private IEnumerator FadeInMenu()
    {
        if (mainCanvasGroup == null) yield break;

        mainCanvasGroup.alpha = 0;
        float t = 0;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            mainCanvasGroup.alpha = t / fadeInDuration;
            yield return null;
        }
        mainCanvasGroup.alpha = 1;
    }
}
