using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 对话选择系统 - 在对话中提供分支选项
/// 与DialogueSystem协同工作，在特定节点暂停并显示选择
/// 选择结果影响羁绊值、剧情走向、隐藏奖励
/// </summary>
public class DialogueChoiceSystem : MonoBehaviour
{
    public static DialogueChoiceSystem Instance { get; private set; }

    [Header("UI引用")]
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private CanvasGroup choiceCanvasGroup;
    [SerializeField] private Transform choiceButtonParent;
    [SerializeField] private GameObject choiceButtonPrefab;

    [Header("设置")]
    [SerializeField] private float choiceRevealInterval = 0.15f;
    [SerializeField] private float choiceFadeInDuration = 0.3f;
    [SerializeField] private float choiceTimeLimit = 0f;           // 0=无限时间
    [SerializeField] private int defaultChoiceIndex = 0;           // 超时时默认选择

    [Header("选择时间条")]
    [SerializeField] private Slider timeBar;
    [SerializeField] private Image timeBarFill;
    [SerializeField] private Color timeBarNormal = Color.white;
    [SerializeField] private Color timeBarUrgent = Color.red;

    // 运行时
    private bool isChoiceActive;
    private List<GameObject> spawnedButtons = new List<GameObject>();
    private System.Action<int> currentCallback;
    private DialogueChoice currentChoice;
    private float choiceTimer;
    private Coroutine timerCoroutine;

    public bool IsChoiceActive => isChoiceActive;

    // 事件
    public event System.Action<string, int> OnChoiceMade; // choiceId, selectedIndex

    [System.Serializable]
    public class DialogueChoice
    {
        public string choiceId;
        public string promptKey;             // 提示文本key
        public string promptFallback;        // 默认提示文本
        public List<ChoiceOption> options = new List<ChoiceOption>();
        public float timeLimit;              // 0=使用系统默认
        public int defaultOption;            // 超时默认选项
    }

    [System.Serializable]
    public class ChoiceOption
    {
        public string textKey;               // 选项文本key
        public string fallbackText;          // 默认选项文本
        public string speaker;               // 说这句话的角色（Lux/Nox）
        public float bondPointsChange;       // 选择后羁绊值变化
        public string resultDialogueId;      // 选择后触发的对话ID
        public string flagToSet;             // 选择后设置的标记（用于后续判断）
        public ChoiceCondition condition;     // 显示条件
    }

    [System.Serializable]
    public class ChoiceCondition
    {
        public ConditionType type = ConditionType.None;
        public string flagKey;               // 需要的标记
        public int minBondLevel;             // 最低羁绊等级

        public enum ConditionType
        {
            None,                // 无条件
            RequireFlag,         // 需要特定标记
            RequireBondLevel,    // 需要羁绊等级
            RequireNGPlus        // 需要NG+
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (choicePanel != null) choicePanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 显示对话选择
    /// </summary>
    public void ShowChoice(DialogueChoice choice, System.Action<int> callback)
    {
        if (isChoiceActive) return;
        if (choice == null || choice.options.Count == 0) return;

        currentChoice = choice;
        currentCallback = callback;
        isChoiceActive = true;

        StartCoroutine(ShowChoiceSequence(choice));
    }

    /// <summary>
    /// 通过选项数据快速显示选择
    /// </summary>
    public void ShowQuickChoice(string id, string prompt,
        string[] options, System.Action<int> callback,
        float timeLimit = 0)
    {
        var choice = new DialogueChoice
        {
            choiceId = id,
            promptFallback = prompt,
            timeLimit = timeLimit
        };

        foreach (var text in options)
        {
            choice.options.Add(new ChoiceOption
            {
                fallbackText = text
            });
        }

        ShowChoice(choice, callback);
    }

    /// <summary>
    /// 检查对话标记是否已设置
    /// </summary>
    public bool HasFlag(string flag)
    {
        return PlayerPrefs.GetInt($"dialogue_flag_{flag}", 0) == 1;
    }

    /// <summary>
    /// 设置对话标记
    /// </summary>
    public void SetFlag(string flag)
    {
        PlayerPrefs.SetInt($"dialogue_flag_{flag}", 1);
    }

    /// <summary>
    /// 获取之前的选择记录
    /// </summary>
    public int GetPreviousChoice(string choiceId)
    {
        return PlayerPrefs.GetInt($"choice_{choiceId}", -1);
    }

    // ==================== 显示流程 ====================

    private IEnumerator ShowChoiceSequence(DialogueChoice choice)
    {
        // 清除旧按钮
        ClearButtons();

        if (choicePanel != null) choicePanel.SetActive(true);

        // 淡入
        if (choiceCanvasGroup != null)
        {
            choiceCanvasGroup.alpha = 0;
            float timer = 0;
            while (timer < choiceFadeInDuration)
            {
                timer += Time.unscaledDeltaTime;
                choiceCanvasGroup.alpha = timer / choiceFadeInDuration;
                yield return null;
            }
            choiceCanvasGroup.alpha = 1;
        }

        // 获取可用选项
        var availableOptions = GetAvailableOptions(choice);

        // 逐个显示选项按钮
        for (int i = 0; i < availableOptions.Count; i++)
        {
            var option = availableOptions[i];
            SpawnChoiceButton(option, i);
            yield return new WaitForSecondsRealtime(choiceRevealInterval);
        }

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();

        // 计时器
        float limit = choice.timeLimit > 0 ? choice.timeLimit : choiceTimeLimit;
        if (limit > 0)
        {
            timerCoroutine = StartCoroutine(ChoiceTimerCoroutine(limit, choice.defaultOption));
        }
        else
        {
            if (timeBar != null) timeBar.gameObject.SetActive(false);
        }
    }

    private List<ChoiceOption> GetAvailableOptions(DialogueChoice choice)
    {
        var result = new List<ChoiceOption>();

        foreach (var option in choice.options)
        {
            if (option.condition == null || option.condition.type == ChoiceCondition.ConditionType.None)
            {
                result.Add(option);
                continue;
            }

            bool conditionMet = option.condition.type switch
            {
                ChoiceCondition.ConditionType.RequireFlag =>
                    HasFlag(option.condition.flagKey),
                ChoiceCondition.ConditionType.RequireBondLevel =>
                    (PlayerBondSystem.Instance?.CurrentBondLevel ?? 0) >= option.condition.minBondLevel,
                ChoiceCondition.ConditionType.RequireNGPlus =>
                    NewGamePlusManager.Instance?.IsNewGamePlus ?? false,
                _ => true
            };

            if (conditionMet)
                result.Add(option);
        }

        return result;
    }

    private void SpawnChoiceButton(ChoiceOption option, int index)
    {
        if (choiceButtonPrefab == null || choiceButtonParent == null) return;

        var buttonObj = Instantiate(choiceButtonPrefab, choiceButtonParent);
        spawnedButtons.Add(buttonObj);

        // 设置文本
        var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            string optionText = GetLocalizedText(option.textKey, option.fallbackText);

            // 如果有说话人标记
            if (!string.IsNullOrEmpty(option.speaker))
            {
                optionText = $"[{option.speaker}] {optionText}";
            }

            text.text = optionText;
        }

        // 绑定点击事件
        var button = buttonObj.GetComponent<Button>();
        int capturedIndex = index;
        if (button != null)
        {
            button.onClick.AddListener(() => OnChoiceSelected(capturedIndex));
        }

        // 初始缩放动画
        buttonObj.transform.localScale = Vector3.zero;
        StartCoroutine(ScaleIn(buttonObj.transform));
    }

    private IEnumerator ScaleIn(Transform target)
    {
        float timer = 0;
        float duration = 0.2f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = timer / duration;
            float scale = Mathf.Lerp(0, 1, t * (2f - t)); // easeOut
            target.localScale = Vector3.one * scale;
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    // ==================== 选择处理 ====================

    private void OnChoiceSelected(int index)
    {
        if (!isChoiceActive) return;

        // 停止计时器
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();

        // 处理选择结果
        ProcessChoice(index);

        // 隐藏面板
        StartCoroutine(HideChoicePanel());
    }

    private void ProcessChoice(int index)
    {
        if (currentChoice == null) return;

        var availableOptions = GetAvailableOptions(currentChoice);
        if (index < 0 || index >= availableOptions.Count) return;

        var selected = availableOptions[index];

        // 保存选择记录
        PlayerPrefs.SetInt($"choice_{currentChoice.choiceId}", index);

        // 羁绊值变化
        if (selected.bondPointsChange != 0 && PlayerBondSystem.Instance != null)
        {
            if (selected.bondPointsChange > 0)
                PlayerBondSystem.Instance.AddBondPoints(selected.bondPointsChange, "dialogue_choice");
            else
                PlayerBondSystem.Instance.RemoveBondPoints(-selected.bondPointsChange, "dialogue_choice");
        }

        // 设置标记
        if (!string.IsNullOrEmpty(selected.flagToSet))
            SetFlag(selected.flagToSet);

        // 触发后续对话
        if (!string.IsNullOrEmpty(selected.resultDialogueId) && DialogueSystem.Instance != null)
        {
            // 后续对话由调用方处理（通过回调）
        }

        // 通知
        OnChoiceMade?.Invoke(currentChoice.choiceId, index);
        currentCallback?.Invoke(index);

        Debug.Log($"[DialogueChoice] Choice '{currentChoice.choiceId}' selected option {index}: {selected.fallbackText}");
    }

    private IEnumerator HideChoicePanel()
    {
        isChoiceActive = false;

        // 淡出
        if (choiceCanvasGroup != null)
        {
            float timer = 0;
            while (timer < choiceFadeInDuration)
            {
                timer += Time.unscaledDeltaTime;
                choiceCanvasGroup.alpha = 1f - timer / choiceFadeInDuration;
                yield return null;
            }
            choiceCanvasGroup.alpha = 0;
        }

        ClearButtons();
        if (choicePanel != null) choicePanel.SetActive(false);

        currentChoice = null;
        currentCallback = null;
    }

    // ==================== 计时器 ====================

    private IEnumerator ChoiceTimerCoroutine(float limit, int defaultIndex)
    {
        if (timeBar != null)
        {
            timeBar.gameObject.SetActive(true);
            timeBar.maxValue = limit;
        }

        choiceTimer = limit;

        while (choiceTimer > 0)
        {
            choiceTimer -= Time.unscaledDeltaTime;

            if (timeBar != null)
            {
                timeBar.value = choiceTimer;
            }

            // 紧急颜色
            if (timeBarFill != null)
            {
                float urgencyThreshold = limit * 0.3f;
                timeBarFill.color = choiceTimer <= urgencyThreshold ? timeBarUrgent : timeBarNormal;
            }

            yield return null;
        }

        // 超时，自动选择默认
        OnChoiceSelected(defaultIndex);
    }

    // ==================== 辅助 ====================

    private void ClearButtons()
    {
        foreach (var btn in spawnedButtons)
        {
            if (btn != null) Destroy(btn);
        }
        spawnedButtons.Clear();
    }

    private string GetLocalizedText(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback;

        if (LocalizationSystem.Instance != null)
        {
            string localized = LocalizationSystem.Instance.GetText(key);
            if (localized != key) return localized;
        }
        return fallback;
    }
}
