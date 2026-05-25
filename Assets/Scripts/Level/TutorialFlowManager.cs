using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 教程流程管理器 - 控制新手引导的完整流程
/// 按关卡/章节安排教程节点，确保玩家逐步学习所有机制
/// 与InputGuideUI、TutorialOverlayUI、DialogueSystem协作
/// </summary>
public class TutorialFlowManager : MonoBehaviour
{
    public static TutorialFlowManager Instance { get; private set; }

    [Header("教程配置")]
    [SerializeField] private bool enableTutorials = true;
    [SerializeField] private bool forceShowInDebug = false;

    [Header("教程步骤定义")]
    [SerializeField] private List<TutorialStep> tutorialSteps = new List<TutorialStep>();

    [System.Serializable]
    public class TutorialStep
    {
        public string stepId;                        // 唯一标识
        public int requiredChapter = 1;              // 所需章节
        public int requiredLevel = 1;                // 所需关卡
        public TutorialTiming timing = TutorialTiming.LevelStart;

        [Header("内容")]
        public string titleKey;                      // 本地化标题key
        public string descriptionKey;                // 本地化描述key
        public InputGuideUI.ActionType actionType;   // 对应的操作
        public Sprite customIcon;                    // 自定义图标

        [Header("行为")]
        public bool pauseGameplay = true;            // 暂停游戏
        public bool requireAction = true;            // 需要玩家执行操作才继续
        public float autoAdvanceDelay = 5f;          // 自动推进延迟（requireAction=false时）
        public bool highlightUI;                     // 高亮UI元素
        public string highlightTarget;               // 高亮目标名称

        [Header("可选对话")]
        public DialogueSystem.DialogueSequence dialogue; // 可选的对话序列
    }

    public enum TutorialTiming
    {
        LevelStart,          // 关卡开始时
        FirstMove,           // 首次移动后
        FirstJump,           // 首次跳跃后
        FirstEnemy,          // 遇到第一个敌人时
        FirstPuzzle,         // 遇到第一个谜题时
        FirstAbility,        // 首次使用技能时
        FirstCoop,           // 首次合作时
        FirstBoss,           // 首次Boss战时
        Custom               // 由TutorialTrigger手动触发
    }

    // 运行时
    private HashSet<string> completedSteps = new HashSet<string>();
    private TutorialStep activeStep;
    private bool isShowingTutorial;
    private Coroutine activeCoroutine;

    // 事件
    public event System.Action<string> OnStepStarted;
    public event System.Action<string> OnStepCompleted;
    public event System.Action OnAllTutorialsComplete;

    private const string PREFS_KEY = "tutorial_completed_steps";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        LoadCompletedSteps();
        InitializeDefaultSteps();
    }

    void OnEnable()
    {
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Subscribe<EnemyHitEvent>(OnFirstCombat);
        EventBus.Subscribe<PuzzleSolvedEvent>(OnFirstPuzzle);
        EventBus.Subscribe<AbilityUsedEvent>(OnFirstAbility);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Unsubscribe<EnemyHitEvent>(OnFirstCombat);
        EventBus.Unsubscribe<PuzzleSolvedEvent>(OnFirstPuzzle);
        EventBus.Unsubscribe<AbilityUsedEvent>(OnFirstAbility);
    }

    // ==================== 初始化默认教程 ====================

    private void InitializeDefaultSteps()
    {
        if (tutorialSteps.Count > 0) return; // 已在Inspector中配置

        // Ch.1 Lv.1: 基础操作
        tutorialSteps.Add(new TutorialStep
        {
            stepId = "move_basic",
            requiredChapter = 1, requiredLevel = 1,
            timing = TutorialTiming.LevelStart,
            titleKey = "tutorial_move_title",
            descriptionKey = "tutorial_move_desc",
            actionType = InputGuideUI.ActionType.Move,
            pauseGameplay = true,
            requireAction = true
        });

        tutorialSteps.Add(new TutorialStep
        {
            stepId = "jump_basic",
            requiredChapter = 1, requiredLevel = 1,
            timing = TutorialTiming.FirstMove,
            titleKey = "tutorial_jump_title",
            descriptionKey = "tutorial_jump_desc",
            actionType = InputGuideUI.ActionType.Jump,
            pauseGameplay = true,
            requireAction = true
        });

        // Ch.1 Lv.2: 战斗
        tutorialSteps.Add(new TutorialStep
        {
            stepId = "attack_basic",
            requiredChapter = 1, requiredLevel = 2,
            timing = TutorialTiming.FirstEnemy,
            titleKey = "tutorial_attack_title",
            descriptionKey = "tutorial_attack_desc",
            actionType = InputGuideUI.ActionType.Attack,
            pauseGameplay = true,
            requireAction = true
        });

        // Ch.1 Lv.2: 交互
        tutorialSteps.Add(new TutorialStep
        {
            stepId = "interact_basic",
            requiredChapter = 1, requiredLevel = 2,
            timing = TutorialTiming.Custom,
            titleKey = "tutorial_interact_title",
            descriptionKey = "tutorial_interact_desc",
            actionType = InputGuideUI.ActionType.Interact,
            pauseGameplay = false,
            requireAction = false,
            autoAdvanceDelay = 4f
        });

        // Ch.1 Lv.3: 技能 (Lux光束)
        tutorialSteps.Add(new TutorialStep
        {
            stepId = "skill_lux",
            requiredChapter = 1, requiredLevel = 3,
            timing = TutorialTiming.LevelStart,
            titleKey = "tutorial_skill_lux_title",
            descriptionKey = "tutorial_skill_lux_desc",
            actionType = InputGuideUI.ActionType.Skill1,
            pauseGameplay = true,
            requireAction = true
        });

        tutorialSteps.Add(new TutorialStep
        {
            stepId = "skill_nox",
            requiredChapter = 1, requiredLevel = 3,
            timing = TutorialTiming.FirstAbility,
            titleKey = "tutorial_skill_nox_title",
            descriptionKey = "tutorial_skill_nox_desc",
            actionType = InputGuideUI.ActionType.Skill2,
            pauseGameplay = true,
            requireAction = true
        });

        // Ch.1 Lv.3: 冲刺
        tutorialSteps.Add(new TutorialStep
        {
            stepId = "dash_basic",
            requiredChapter = 1, requiredLevel = 3,
            timing = TutorialTiming.Custom,
            titleKey = "tutorial_dash_title",
            descriptionKey = "tutorial_dash_desc",
            actionType = InputGuideUI.ActionType.Dash,
            pauseGameplay = false,
            requireAction = false,
            autoAdvanceDelay = 4f
        });

        // Ch.1 Lv.4: Boss战（首个Boss）
        tutorialSteps.Add(new TutorialStep
        {
            stepId = "boss_intro",
            requiredChapter = 1, requiredLevel = 4,
            timing = TutorialTiming.FirstBoss,
            titleKey = "tutorial_boss_title",
            descriptionKey = "tutorial_boss_desc",
            actionType = InputGuideUI.ActionType.Custom,
            pauseGameplay = true,
            requireAction = false,
            autoAdvanceDelay = 5f
        });

        // Ch.2 Lv.1: 合作机制
        tutorialSteps.Add(new TutorialStep
        {
            stepId = "coop_basic",
            requiredChapter = 2, requiredLevel = 1,
            timing = TutorialTiming.LevelStart,
            titleKey = "tutorial_coop_title",
            descriptionKey = "tutorial_coop_desc",
            actionType = InputGuideUI.ActionType.Custom,
            pauseGameplay = true,
            requireAction = false,
            autoAdvanceDelay = 5f
        });
    }

    // ==================== 事件处理 ====================

    private void OnLevelStart(LevelStartEvent e)
    {
        if (!enableTutorials) return;

        // 查找此关卡的LevelStart教程
        StartCoroutine(CheckLevelStartTutorials(e.chapter, e.level));
    }

    private IEnumerator CheckLevelStartTutorials(int chapter, int level)
    {
        yield return new WaitForSeconds(0.5f); // 等待关卡初始化

        foreach (var step in tutorialSteps)
        {
            if (step.timing != TutorialTiming.LevelStart) continue;
            if (step.requiredChapter != chapter || step.requiredLevel != level) continue;
            if (IsStepCompleted(step.stepId) && !forceShowInDebug) continue;

            yield return ShowTutorialStep(step);
        }
    }

    private void OnFirstCombat(EnemyHitEvent e)
    {
        TriggerTimedSteps(TutorialTiming.FirstEnemy);
    }

    private void OnFirstPuzzle(PuzzleSolvedEvent e)
    {
        TriggerTimedSteps(TutorialTiming.FirstPuzzle);
    }

    private void OnFirstAbility(AbilityUsedEvent e)
    {
        TriggerTimedSteps(TutorialTiming.FirstAbility);
    }

    /// <summary>
    /// 外部手动触发教程步骤（由TutorialTrigger调用）
    /// </summary>
    public void TriggerStep(string stepId)
    {
        if (!enableTutorials) return;
        if (isShowingTutorial) return;
        if (IsStepCompleted(stepId) && !forceShowInDebug) return;

        var step = tutorialSteps.Find(s => s.stepId == stepId);
        if (step != null)
            activeCoroutine = StartCoroutine(ShowTutorialStep(step));
    }

    private void TriggerTimedSteps(TutorialTiming timing)
    {
        if (!enableTutorials || isShowingTutorial) return;

        foreach (var step in tutorialSteps)
        {
            if (step.timing != timing) continue;
            if (IsStepCompleted(step.stepId) && !forceShowInDebug) continue;

            activeCoroutine = StartCoroutine(ShowTutorialStep(step));
            return; // 一次只显示一个
        }
    }

    // ==================== 教程显示 ====================

    private IEnumerator ShowTutorialStep(TutorialStep step)
    {
        isShowingTutorial = true;
        activeStep = step;
        OnStepStarted?.Invoke(step.stepId);

        // 暂停
        if (step.pauseGameplay)
            Time.timeScale = 0f;

        // 播放对话（如果有）
        if (step.dialogue != null && DialogueSystem.Instance != null)
        {
            bool dialogueDone = false;
            DialogueSystem.Instance.OnDialogueEnd += () => dialogueDone = true;
            DialogueSystem.Instance.StartDialogue(step.dialogue);

            while (!dialogueDone)
                yield return null;
        }

        // 显示教程覆盖
        string title = step.titleKey;
        string desc = step.descriptionKey;

        if (LocalizationSystem.Instance != null)
        {
            title = LocalizationSystem.Instance.Get(step.titleKey, step.titleKey);
            desc = LocalizationSystem.Instance.Get(step.descriptionKey, step.descriptionKey);
        }

        if (TutorialOverlayUI.Instance != null)
        {
            TutorialOverlayUI.Instance.ShowTutorial(title, desc, step.highlightTarget);
        }

        // 显示输入指引
        if (InputGuideUI.Instance != null && step.actionType != InputGuideUI.ActionType.Custom)
        {
            InputGuideUI.Instance.ShowGuide(
                step.stepId,
                step.actionType,
                step.descriptionKey,
                step.autoAdvanceDelay,
                false // 教程步骤不标记为showOnce（由本系统管理）
            );
        }

        // 等待完成
        if (step.requireAction)
        {
            // 等待玩家执行对应操作
            yield return WaitForAction(step.actionType);
        }
        else
        {
            // 自动推进
            float elapsed = 0;
            while (elapsed < step.autoAdvanceDelay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // 完成
        CompleteTutorialStep(step);
    }

    private IEnumerator WaitForAction(InputGuideUI.ActionType action)
    {
        bool actionDetected = false;

        while (!actionDetected)
        {
            // 检测对应输入
            switch (action)
            {
                case InputGuideUI.ActionType.Move:
                    if (InputManager.Instance != null)
                        actionDetected = Mathf.Abs(InputManager.Instance.GetMoveInput(0).x) > 0.1f;
                    break;

                case InputGuideUI.ActionType.Jump:
                    if (InputManager.Instance != null)
                        actionDetected = InputManager.Instance.GetJumpDown(0);
                    break;

                case InputGuideUI.ActionType.Attack:
                    if (InputManager.Instance != null)
                        actionDetected = InputManager.Instance.GetAttackDown(0);
                    break;

                case InputGuideUI.ActionType.Skill1:
                case InputGuideUI.ActionType.Skill2:
                    if (InputManager.Instance != null)
                        actionDetected = InputManager.Instance.GetSkill1Down(0) ||
                                         InputManager.Instance.GetSkill2Down(0);
                    break;

                case InputGuideUI.ActionType.Interact:
                    if (InputManager.Instance != null)
                        actionDetected = InputManager.Instance.GetInteractDown(0);
                    break;

                case InputGuideUI.ActionType.Dash:
                    if (InputManager.Instance != null)
                        actionDetected = InputManager.Instance.GetDashDown(0);
                    break;

                default:
                    actionDetected = true;
                    break;
            }

            yield return null;
        }
    }

    private void CompleteTutorialStep(TutorialStep step)
    {
        // 恢复
        if (step.pauseGameplay)
            Time.timeScale = 1f;

        // 隐藏UI
        if (TutorialOverlayUI.Instance != null)
            TutorialOverlayUI.Instance.HideTutorial();

        // 标记完成
        completedSteps.Add(step.stepId);
        SaveCompletedSteps();

        isShowingTutorial = false;
        activeStep = null;

        OnStepCompleted?.Invoke(step.stepId);

        // 检查是否所有教程完成
        bool allDone = true;
        foreach (var s in tutorialSteps)
        {
            if (!completedSteps.Contains(s.stepId))
            {
                allDone = false;
                break;
            }
        }

        if (allDone)
            OnAllTutorialsComplete?.Invoke();
    }

    // ==================== 查询接口 ====================

    public bool IsStepCompleted(string stepId)
    {
        return completedSteps.Contains(stepId);
    }

    public bool IsTutorialActive => isShowingTutorial;

    /// <summary>
    /// 跳过当前教程步骤
    /// </summary>
    public void SkipCurrentStep()
    {
        if (activeStep == null) return;
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        CompleteTutorialStep(activeStep);
    }

    /// <summary>
    /// 重置所有教程进度（新存档时调用）
    /// </summary>
    public void ResetAllProgress()
    {
        completedSteps.Clear();
        PlayerPrefs.DeleteKey(PREFS_KEY);
        PlayerPrefs.Save();
    }

    // ==================== 持久化 ====================

    private void SaveCompletedSteps()
    {
        string data = string.Join(",", completedSteps);
        PlayerPrefs.SetString(PREFS_KEY, data);
        PlayerPrefs.Save();
    }

    private void LoadCompletedSteps()
    {
        string data = PlayerPrefs.GetString(PREFS_KEY, "");
        if (!string.IsNullOrEmpty(data))
        {
            foreach (string id in data.Split(','))
            {
                if (!string.IsNullOrEmpty(id))
                    completedSteps.Add(id);
            }
        }
    }
}
