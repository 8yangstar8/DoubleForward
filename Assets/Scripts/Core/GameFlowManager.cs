using UnityEngine;
using System.Collections;

/// <summary>
/// 游戏流程管理器 - 管理从主菜单到通关的完整流程
/// 协调各系统之间的初始化与交互
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    public enum FlowState
    {
        Boot,           // 启动初始化
        MainMenu,       // 主菜单
        SaveSelect,     // 存档选择
        ModeSelect,     // 模式选择（本地/联机）
        Lobby,          // 联机大厅
        Loading,        // 加载关卡
        Cutscene,       // 过场动画
        Tutorial,       // 教程
        Playing,        // 游戏中
        BossBattle,     // Boss战
        Paused,         // 暂停
        LevelComplete,  // 关卡完成
        GameOver,       // 游戏结束
        Credits         // 制作人员
    }

    [Header("首次启动")]
    [SerializeField] private bool showTutorialOnFirstPlay = true;
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string lobbyScene = "Lobby";

    private FlowState currentState = FlowState.Boot;
    private FlowState previousState;
    private float stateEnterTime;

    // 当前游戏会话
    private int currentChapter;
    private int currentLevel;
    private bool isFirstTimePlayer;

    private const string FIRST_PLAY_KEY = "first_time_played";

    public FlowState CurrentState => currentState;
    public FlowState PreviousState => previousState;
    public float TimeInCurrentState => Time.time - stateEnterTime;
    public int CurrentChapter => currentChapter;
    public int CurrentLevel => currentLevel;

    public event System.Action<FlowState, FlowState> OnStateChanged; // old, new

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        isFirstTimePlayer = PlayerPrefs.GetInt(FIRST_PLAY_KEY, 1) == 1;
    }

    void Start()
    {
        // 启动初始化
        StartCoroutine(BootSequence());
    }

    private IEnumerator BootSequence()
    {
        SetState(FlowState.Boot);

        // 等待各系统初始化
        yield return null; // 等一帧让其他Awake执行完

        // 请求Android权限
        if (AndroidPermissionManager.Instance != null)
        {
            AndroidPermissionManager.Instance.SetScreenAlwaysOn(true);
        }

        // 自动检测性能并应用
        if (PerformanceManager.Instance != null)
        {
            // 首次启动自动检测
            if (!PlayerPrefs.HasKey("performance_level"))
            {
                var level = PerformanceManager.DetectDeviceLevel();
                PerformanceManager.Instance.SetPerformanceLevel(level);
            }
        }

        yield return new WaitForSeconds(0.5f);

        // 进入主菜单
        GoToMainMenu();
    }

    /// <summary>
    /// 进入主菜单
    /// </summary>
    public void GoToMainMenu()
    {
        SetState(FlowState.MainMenu);

        if (ComboSystem.Instance != null)
            ComboSystem.Instance.StopTracking();

        // 加载主菜单场景
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(mainMenuScene);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuScene);
    }

    /// <summary>
    /// 进入存档选择
    /// </summary>
    public void GoToSaveSelect()
    {
        SetState(FlowState.SaveSelect);
    }

    /// <summary>
    /// 选择存档后开始游戏
    /// </summary>
    public void StartFromSave(int slot)
    {
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.SetActiveSlot(slot);
            var data = SaveSystem.Instance.Data;
            currentChapter = data.lastChapter;
            currentLevel = data.lastLevel;
        }

        // 首次游戏标记
        if (isFirstTimePlayer)
        {
            isFirstTimePlayer = false;
            PlayerPrefs.SetInt(FIRST_PLAY_KEY, 0);
            PlayerPrefs.Save();
        }

        LoadLevel(currentChapter, currentLevel);
    }

    /// <summary>
    /// 选择模式
    /// </summary>
    public void SelectPlayMode(InputManager.PlayMode mode)
    {
        if (mode == InputManager.PlayMode.Network)
        {
            SetState(FlowState.Lobby);
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadScene(lobbyScene);
        }
        else
        {
            GoToSaveSelect();
        }
    }

    /// <summary>
    /// 加载关卡
    /// </summary>
    public void LoadLevel(int chapter, int level)
    {
        currentChapter = chapter;
        currentLevel = level;
        SetState(FlowState.Loading);

        string sceneName = $"Level_{chapter}_{level}";

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(sceneName, chapter - 1);
    }

    /// <summary>
    /// 关卡加载完成后调用
    /// </summary>
    public void OnLevelReady()
    {
        // 第一章第一关且首次游玩：显示教程
        if (showTutorialOnFirstPlay && currentChapter == 1 && currentLevel == 1 && isFirstTimePlayer)
        {
            SetState(FlowState.Tutorial);
        }
        else
        {
            StartPlaying();
        }
    }

    /// <summary>
    /// 开始游玩
    /// </summary>
    public void StartPlaying()
    {
        SetState(FlowState.Playing);

        if (ComboSystem.Instance != null)
            ComboSystem.Instance.StartTracking();

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameManager.GameState.Playing);
    }

    /// <summary>
    /// 进入Boss战
    /// </summary>
    public void EnterBossBattle()
    {
        SetState(FlowState.BossBattle);

        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeHeavy();
    }

    /// <summary>
    /// 暂停游戏
    /// </summary>
    public void PauseGame()
    {
        if (currentState != FlowState.Playing && currentState != FlowState.BossBattle) return;

        SetState(FlowState.Paused);
        Time.timeScale = 0;
    }

    /// <summary>
    /// 恢复游戏
    /// </summary>
    public void ResumeGame()
    {
        if (currentState != FlowState.Paused) return;

        Time.timeScale = 1;
        SetState(previousState);
    }

    /// <summary>
    /// 关卡完成
    /// </summary>
    public void CompleteLevelFlow(float time, int collectibles, int totalCollectibles)
    {
        SetState(FlowState.LevelComplete);
        Time.timeScale = 1;

        // 计算评分
        ComboSystem.LevelResult result = null;
        if (ComboSystem.Instance != null)
        {
            result = ComboSystem.Instance.CalculateLevelResult(120f, totalCollectibles, collectibles);
            ComboSystem.Instance.StopTracking();
        }

        int stars = result != null ? result.stars : 1;

        // 保存进度
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.MarkLevelComplete(currentChapter, currentLevel, stars, time, collectibles);
        }

        // 特效
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.LevelComplete, Vector3.zero);
    }

    /// <summary>
    /// 游戏结束
    /// </summary>
    public void TriggerGameOver()
    {
        SetState(FlowState.GameOver);
        Time.timeScale = 0;

        if (ComboSystem.Instance != null)
            ComboSystem.Instance.RecordDeath();
    }

    /// <summary>
    /// 重试当前关卡
    /// </summary>
    public void RetryLevel()
    {
        Time.timeScale = 1;
        LoadLevel(currentChapter, currentLevel);
    }

    /// <summary>
    /// 下一关
    /// </summary>
    public void NextLevel()
    {
        int[] levelsPerChapter = { 4, 4, 4, 4, 4 };
        int nextLevel = currentLevel + 1;
        int nextChapter = currentChapter;

        if (nextLevel > levelsPerChapter[currentChapter - 1])
        {
            nextChapter++;
            nextLevel = 1;
        }

        if (nextChapter > 5)
        {
            // 游戏通关
            SetState(FlowState.Credits);
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadScene("Credits");
            return;
        }

        LoadLevel(nextChapter, nextLevel);
    }

    /// <summary>
    /// 播放过场动画
    /// </summary>
    public void PlayCutscene()
    {
        SetState(FlowState.Cutscene);
    }

    /// <summary>
    /// 过场结束
    /// </summary>
    public void OnCutsceneEnd()
    {
        StartPlaying();
    }

    private void SetState(FlowState newState)
    {
        if (currentState == newState) return;

        previousState = currentState;
        currentState = newState;
        stateEnterTime = Time.time;

        OnStateChanged?.Invoke(previousState, newState);

        Debug.Log($"[GameFlow] {previousState} → {newState}");
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && currentState == FlowState.Playing)
        {
            // 后台自动存档
            if (SaveSystem.Instance != null)
                SaveSystem.Instance.Save();
        }
    }

    void OnApplicationQuit()
    {
        // 退出前自动存档
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.Save();
    }
}
