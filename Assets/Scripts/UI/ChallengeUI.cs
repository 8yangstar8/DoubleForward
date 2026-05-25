using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 挑战模式UI - 每日挑战选择、挑战进行中HUD、结果展示
/// 与ChallengeMode系统配合
/// </summary>
public class ChallengeUI : MonoBehaviour
{
    [Header("挑战选择面板")]
    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private ChallengeSlot[] challengeSlots;
    [SerializeField] private Button backButton;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("挑战HUD")]
    [SerializeField] private GameObject challengeHUD;
    [SerializeField] private TextMeshProUGUI challengeNameText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Image timerFill;
    [SerializeField] private Button abandonButton;

    [Header("结果面板")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TextMeshProUGUI resultTitleText;
    [SerializeField] private TextMeshProUGUI resultDescText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Image resultIcon;
    [SerializeField] private Button resultCloseButton;

    [Header("颜色")]
    [SerializeField] private Color successColor = new Color(0.3f, 1f, 0.5f);
    [SerializeField] private Color failColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color completedColor = new Color(0.5f, 0.5f, 0.5f);

    [Header("动画")]
    [SerializeField] private float timerWarningThreshold = 10f;
    [SerializeField] private float pulseSpeed = 4f;

    [System.Serializable]
    public class ChallengeSlot
    {
        public Button button;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descText;
        public TextMeshProUGUI rewardText;
        public Image typeIcon;
        public Image completedOverlay;
        public TextMeshProUGUI statusText;
    }

    void Start()
    {
        // 绑定事件
        if (ChallengeMode.Instance != null)
        {
            ChallengeMode.Instance.OnChallengeStarted += OnChallengeStarted;
            ChallengeMode.Instance.OnChallengeCompleted += OnChallengeCompleted;
            ChallengeMode.Instance.OnChallengeTimerUpdate += OnTimerUpdate;
        }

        backButton?.onClick.AddListener(CloseSelection);
        abandonButton?.onClick.AddListener(AbandonChallenge);
        resultCloseButton?.onClick.AddListener(CloseResult);

        // 初始隐藏
        if (selectionPanel != null) selectionPanel.SetActive(false);
        if (challengeHUD != null) challengeHUD.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (ChallengeMode.Instance != null)
        {
            ChallengeMode.Instance.OnChallengeStarted -= OnChallengeStarted;
            ChallengeMode.Instance.OnChallengeCompleted -= OnChallengeCompleted;
            ChallengeMode.Instance.OnChallengeTimerUpdate -= OnTimerUpdate;
        }
    }

    void Update()
    {
        // 更新挑战进度显示
        if (challengeHUD != null && challengeHUD.activeSelf)
        {
            UpdateChallengeProgress();
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 显示每日挑战选择面板
    /// </summary>
    public void ShowDailyChallenges()
    {
        if (selectionPanel == null) return;

        selectionPanel.SetActive(true);
        RefreshDailySlots();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayClick();
    }

    // ==================== 选择面板 ====================

    private void RefreshDailySlots()
    {
        if (ChallengeMode.Instance == null) return;

        var dailies = ChallengeMode.Instance.DailyChallenges;

        for (int i = 0; i < challengeSlots.Length; i++)
        {
            var slot = challengeSlots[i];
            if (i >= dailies.Count)
            {
                if (slot.button != null) slot.button.gameObject.SetActive(false);
                continue;
            }

            var challenge = dailies[i];
            if (slot.button != null) slot.button.gameObject.SetActive(true);

            // 名称和描述
            string name = GetChallengeName(challenge.data.type);
            string desc = GetChallengeDescription(challenge.data);

            if (slot.nameText != null) slot.nameText.text = name;
            if (slot.descText != null) slot.descText.text = desc;
            if (slot.rewardText != null) slot.rewardText.text = $"+{challenge.data.coinReward}";

            // 完成状态
            bool completed = challenge.isCompleted;
            if (slot.completedOverlay != null)
                slot.completedOverlay.gameObject.SetActive(completed);
            if (slot.statusText != null)
            {
                slot.statusText.text = completed ? "已完成" : "未完成";
                slot.statusText.color = completed ? completedColor : Color.white;
            }

            // 按钮交互
            if (slot.button != null)
            {
                slot.button.interactable = !completed;
                int idx = i;
                slot.button.onClick.RemoveAllListeners();
                slot.button.onClick.AddListener(() => StartDaily(idx));
            }
        }
    }

    private void StartDaily(int index)
    {
        ChallengeMode.Instance?.StartDailyChallenge(index);

        if (selectionPanel != null)
            selectionPanel.SetActive(false);
    }

    private void CloseSelection()
    {
        if (selectionPanel != null)
            selectionPanel.SetActive(false);
    }

    // ==================== 挑战HUD ====================

    private void OnChallengeStarted(ChallengeMode.ChallengeInstance challenge)
    {
        if (challengeHUD == null) return;

        challengeHUD.SetActive(true);

        if (challengeNameText != null)
            challengeNameText.text = GetChallengeName(challenge.data.type);
    }

    private void OnTimerUpdate(float remaining)
    {
        if (timerText != null)
        {
            int mins = Mathf.FloorToInt(remaining / 60f);
            int secs = Mathf.FloorToInt(remaining % 60f);
            timerText.text = $"{mins:00}:{secs:00}";

            // 时间警告
            if (remaining <= timerWarningThreshold)
            {
                float pulse = Mathf.Abs(Mathf.Sin(Time.unscaledTime * pulseSpeed));
                timerText.color = Color.Lerp(warningColor, failColor, pulse);
                timerText.fontSize = Mathf.Lerp(24f, 28f, pulse);
            }
            else
            {
                timerText.color = Color.white;
            }
        }

        if (timerFill != null && ChallengeMode.Instance?.CurrentChallenge != null)
        {
            float total = ChallengeMode.Instance.CurrentChallenge.data.timeLimit;
            if (total > 0)
                timerFill.fillAmount = remaining / total;
        }
    }

    private void UpdateChallengeProgress()
    {
        var challenge = ChallengeMode.Instance?.CurrentChallenge;
        if (challenge == null || progressText == null) return;

        float progress = challenge.Progress;
        progressText.text = $"{Mathf.FloorToInt(progress * 100)}%";
    }

    private void AbandonChallenge()
    {
        ChallengeMode.Instance?.AbandonChallenge();
    }

    // ==================== 结果面板 ====================

    private void OnChallengeCompleted(ChallengeMode.ChallengeInstance challenge, bool success)
    {
        if (challengeHUD != null)
            challengeHUD.SetActive(false);

        if (resultPanel == null) return;

        resultPanel.SetActive(true);

        if (resultTitleText != null)
        {
            resultTitleText.text = success ? "挑战成功!" : "挑战失败";
            resultTitleText.color = success ? successColor : failColor;
        }

        if (resultDescText != null)
            resultDescText.text = GetChallengeName(challenge.data.type);

        if (rewardText != null)
        {
            if (success)
            {
                rewardText.text = $"+{challenge.data.coinReward} 金币";
                rewardText.color = successColor;
            }
            else
            {
                rewardText.text = "无奖励";
                rewardText.color = failColor;
            }
        }

        StartCoroutine(ResultAnimation(success));
    }

    private IEnumerator ResultAnimation(bool success)
    {
        if (resultPanel == null) yield break;

        // 缩放弹出
        var rt = resultPanel.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.zero;
            float elapsed = 0;
            float duration = 0.4f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                // Elastic ease out
                float scale = t < 1f
                    ? -Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - 0.075f) * (2f * Mathf.PI) / 0.3f) + 1f
                    : 1f;
                rt.localScale = Vector3.one * Mathf.Max(0, scale);
                yield return null;
            }

            rt.localScale = Vector3.one;
        }
    }

    private void CloseResult()
    {
        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    // ==================== 文本辅助 ====================

    private string GetChallengeName(ChallengeMode.ChallengeType type)
    {
        // 尝试本地化
        string key = $"challenge_{type.ToString().ToLower()}_name";
        if (LocalizationSystem.Instance != null)
        {
            string localized = LocalizationSystem.Instance.GetText(key);
            if (localized != key) return localized;
        }

        return type switch
        {
            ChallengeMode.ChallengeType.SpeedRun => "限时挑战",
            ChallengeMode.ChallengeType.NoDeath => "无死亡挑战",
            ChallengeMode.ChallengeType.HighCombo => "连击大师",
            ChallengeMode.ChallengeType.CollectAll => "全收集挑战",
            ChallengeMode.ChallengeType.PacifistRun => "和平主义者",
            ChallengeMode.ChallengeType.BossRush => "Boss连战",
            ChallengeMode.ChallengeType.CoopSync => "同步挑战",
            _ => type.ToString()
        };
    }

    private string GetChallengeDescription(ChallengeMode.ChallengeData data)
    {
        return data.type switch
        {
            ChallengeMode.ChallengeType.SpeedRun =>
                $"在 {data.timeLimit:F0} 秒内通关",
            ChallengeMode.ChallengeType.NoDeath =>
                "不死亡完成关卡",
            ChallengeMode.ChallengeType.HighCombo =>
                $"达到 {data.minCombo} 连击",
            ChallengeMode.ChallengeType.CollectAll =>
                "收集关卡内所有收集品",
            ChallengeMode.ChallengeType.PacifistRun =>
                "不击杀任何敌人通关",
            ChallengeMode.ChallengeType.BossRush =>
                $"连续击败 {data.targetCount} 个Boss",
            ChallengeMode.ChallengeType.CoopSync =>
                "双人同步完成动作",
            _ => data.descriptionKey
        };
    }
}
