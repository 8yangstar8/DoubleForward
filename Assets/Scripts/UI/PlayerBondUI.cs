using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 玩家羁绊UI - 显示Lux和Nox的羁绊等级和进度
/// 包含：常驻HUD图标、升级演出、对话气泡、详情面板
/// </summary>
public class PlayerBondUI : MonoBehaviour
{
    [Header("HUD - 常驻显示")]
    [SerializeField] private GameObject hudBondIcon;
    [SerializeField] private Image bondLevelIcon;
    [SerializeField] private Slider bondProgressBar;
    [SerializeField] private Image bondProgressFill;
    [SerializeField] private TextMeshProUGUI bondLevelText;

    [Header("升级演出")]
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private CanvasGroup levelUpCanvasGroup;
    [SerializeField] private TextMeshProUGUI levelUpTitleText;
    [SerializeField] private TextMeshProUGUI levelUpNameText;
    [SerializeField] private Image levelUpGlow;
    [SerializeField] private Image[] bondHearts;
    [SerializeField] private Sprite heartEmpty;
    [SerializeField] private Sprite heartFilled;

    [Header("对话气泡")]
    [SerializeField] private GameObject dialogueBubblePanel;
    [SerializeField] private CanvasGroup dialogueBubbleCanvasGroup;
    [SerializeField] private TextMeshProUGUI luxDialogueText;
    [SerializeField] private TextMeshProUGUI noxDialogueText;
    [SerializeField] private Image luxPortrait;
    [SerializeField] private Image noxPortrait;

    [Header("详情面板")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private CanvasGroup detailCanvasGroup;
    [SerializeField] private TextMeshProUGUI detailLevelNameText;
    [SerializeField] private TextMeshProUGUI detailPointsText;
    [SerializeField] private TextMeshProUGUI detailProgressText;
    [SerializeField] private TextMeshProUGUI detailBonusText;
    [SerializeField] private Slider detailProgressBar;
    [SerializeField] private Button detailCloseButton;

    [Header("颜色")]
    [SerializeField] private Color[] bondLevelColors = {
        new Color(0.7f, 0.7f, 0.7f),    // 0: 灰色
        new Color(0.4f, 0.8f, 0.4f),    // 1: 绿色
        new Color(0.3f, 0.6f, 1f),      // 2: 蓝色
        new Color(0.8f, 0.4f, 1f),      // 3: 紫色
        new Color(1f, 0.7f, 0.2f),      // 4: 金色
        new Color(1f, 0.3f, 0.5f)       // 5: 粉红（灵魂共鸣）
    };

    [Header("动画")]
    [SerializeField] private float levelUpDisplayDuration = 4f;
    [SerializeField] private float dialogueBubbleDuration = 5f;
    [SerializeField] private float heartPopDelay = 0.3f;

    // 缓存
    private Coroutine levelUpCoroutine;
    private Coroutine dialogueCoroutine;

    void Start()
    {
        // 订阅事件
        if (PlayerBondSystem.Instance != null)
        {
            PlayerBondSystem.Instance.OnBondLevelUp += OnBondLevelUp;
            PlayerBondSystem.Instance.OnBondPointsGained += OnBondPointsGained;
            PlayerBondSystem.Instance.OnBondDialogueTrigger += OnBondDialogueTrigger;
        }

        if (detailCloseButton != null)
            detailCloseButton.onClick.AddListener(HideDetailPanel);

        // HUD点击打开详情
        if (hudBondIcon != null)
        {
            var btn = hudBondIcon.GetComponent<Button>();
            if (btn == null) btn = hudBondIcon.AddComponent<Button>();
            btn.onClick.AddListener(ShowDetailPanel);
        }

        // 初始化
        SetPanelActive(levelUpPanel, false);
        SetPanelActive(dialogueBubblePanel, false);
        SetPanelActive(detailPanel, false);

        UpdateHUD();
    }

    void OnDestroy()
    {
        if (PlayerBondSystem.Instance != null)
        {
            PlayerBondSystem.Instance.OnBondLevelUp -= OnBondLevelUp;
            PlayerBondSystem.Instance.OnBondPointsGained -= OnBondPointsGained;
            PlayerBondSystem.Instance.OnBondDialogueTrigger -= OnBondDialogueTrigger;
        }
    }

    // ==================== HUD更新 ====================

    private void UpdateHUD()
    {
        if (PlayerBondSystem.Instance == null) return;

        int level = PlayerBondSystem.Instance.CurrentBondLevel;
        float progress = PlayerBondSystem.Instance.BondProgress;

        if (bondLevelText != null)
            bondLevelText.text = level.ToString();

        if (bondProgressBar != null)
            bondProgressBar.value = progress;

        if (bondProgressFill != null)
            bondProgressFill.color = GetBondColor(level);

        if (bondLevelIcon != null)
            bondLevelIcon.color = GetBondColor(level);
    }

    // ==================== 事件处理 ====================

    private void OnBondLevelUp(int newLevel)
    {
        UpdateHUD();

        if (levelUpCoroutine != null) StopCoroutine(levelUpCoroutine);
        levelUpCoroutine = StartCoroutine(LevelUpAnimation(newLevel));
    }

    private void OnBondPointsGained(float points, string source)
    {
        UpdateHUD();

        // HUD图标小弹跳
        if (hudBondIcon != null)
            StartCoroutine(BounceIcon(hudBondIcon.transform));
    }

    private void OnBondDialogueTrigger(PlayerBondSystem.BondDialogue dialogue)
    {
        if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
        dialogueCoroutine = StartCoroutine(ShowBondDialogue(dialogue));
    }

    // ==================== 升级演出 ====================

    private IEnumerator LevelUpAnimation(int newLevel)
    {
        SetPanelActive(levelUpPanel, true);

        // 标题
        if (levelUpTitleText != null)
            levelUpTitleText.text = GetLocalizedText("bond_level_up", "Bond Level Up!");

        // 等级名称
        if (levelUpNameText != null)
        {
            string name = PlayerBondSystem.Instance?.GetBondLevelName(newLevel) ?? $"Level {newLevel}";
            levelUpNameText.text = name;
            levelUpNameText.color = GetBondColor(newLevel);
        }

        // 发光效果
        if (levelUpGlow != null)
            levelUpGlow.color = GetBondColor(newLevel);

        // 淡入
        yield return FadeIn(levelUpCanvasGroup, 0.3f);

        // 爱心动画
        for (int i = 0; i < (bondHearts?.Length ?? 0); i++)
        {
            if (bondHearts[i] == null) continue;

            bool filled = i < newLevel;
            bondHearts[i].sprite = filled ? heartFilled : heartEmpty;

            if (filled && i == newLevel - 1) // 新填充的心
            {
                bondHearts[i].transform.localScale = Vector3.zero;
                float timer = 0;
                while (timer < 0.4f)
                {
                    timer += Time.unscaledDeltaTime;
                    float t = timer / 0.4f;
                    float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.4f;
                    bondHearts[i].transform.localScale = Vector3.one * Mathf.Min(scale, 1.4f - t * 0.4f);
                    yield return null;
                }
                bondHearts[i].transform.localScale = Vector3.one;

                if (SoundFeedback.Instance != null)
                    SoundFeedback.Instance.Play("bond_heart");
            }

            yield return new WaitForSecondsRealtime(heartPopDelay);
        }

        // 持续显示
        yield return new WaitForSecondsRealtime(levelUpDisplayDuration);

        // 淡出
        yield return FadeOut(levelUpCanvasGroup, 0.5f);
        SetPanelActive(levelUpPanel, false);
    }

    // ==================== 对话气泡 ====================

    private IEnumerator ShowBondDialogue(PlayerBondSystem.BondDialogue dialogue)
    {
        SetPanelActive(dialogueBubblePanel, true);

        // 设置对话文本
        if (luxDialogueText != null)
        {
            string luxText = GetLocalizedText(dialogue.dialogueKey + "_lux", dialogue.fallbackTextLux);
            luxDialogueText.text = "";

            // 打字机效果
            foreach (char c in luxText)
            {
                luxDialogueText.text += c;
                yield return new WaitForSecondsRealtime(0.03f);
            }
        }

        yield return new WaitForSecondsRealtime(0.5f);

        if (noxDialogueText != null)
        {
            string noxText = GetLocalizedText(dialogue.dialogueKey + "_nox", dialogue.fallbackTextNox);
            noxDialogueText.text = "";

            foreach (char c in noxText)
            {
                noxDialogueText.text += c;
                yield return new WaitForSecondsRealtime(0.03f);
            }
        }

        // 淡入
        yield return FadeIn(dialogueBubbleCanvasGroup, 0.3f);

        // 显示持续时间
        yield return new WaitForSecondsRealtime(dialogueBubbleDuration);

        // 淡出
        yield return FadeOut(dialogueBubbleCanvasGroup, 0.5f);
        SetPanelActive(dialogueBubblePanel, false);
    }

    // ==================== 详情面板 ====================

    public void ShowDetailPanel()
    {
        if (PlayerBondSystem.Instance == null) return;

        SetPanelActive(detailPanel, true);

        int level = PlayerBondSystem.Instance.CurrentBondLevel;
        float progress = PlayerBondSystem.Instance.BondProgress;
        float points = PlayerBondSystem.Instance.CurrentBondPoints;
        float bonus = PlayerBondSystem.Instance.GetCoopAbilityBonus();

        if (detailLevelNameText != null)
        {
            detailLevelNameText.text = $"Lv.{level} - {PlayerBondSystem.Instance.BondLevelName}";
            detailLevelNameText.color = GetBondColor(level);
        }

        if (detailPointsText != null)
            detailPointsText.text = $"{points:F0} pts";

        if (detailProgressText != null)
            detailProgressText.text = $"{progress * 100:F0}%";

        if (detailProgressBar != null)
            detailProgressBar.value = progress;

        if (detailBonusText != null)
            detailBonusText.text = $"Coop Bonus: x{bonus:F2}";

        StartCoroutine(FadeIn(detailCanvasGroup, 0.2f));
    }

    public void HideDetailPanel()
    {
        StartCoroutine(HideDetailPanelCoroutine());
    }

    private IEnumerator HideDetailPanelCoroutine()
    {
        yield return FadeOut(detailCanvasGroup, 0.2f);
        SetPanelActive(detailPanel, false);
    }

    // ==================== 辅助方法 ====================

    private Color GetBondColor(int level)
    {
        if (level >= 0 && level < bondLevelColors.Length)
            return bondLevelColors[level];
        return Color.white;
    }

    private IEnumerator FadeIn(CanvasGroup cg, float duration)
    {
        if (cg == null) yield break;
        cg.alpha = 0;
        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = t / duration;
            yield return null;
        }
        cg.alpha = 1;
    }

    private IEnumerator FadeOut(CanvasGroup cg, float duration)
    {
        if (cg == null) yield break;
        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = 1f - t / duration;
            yield return null;
        }
        cg.alpha = 0;
    }

    private IEnumerator BounceIcon(Transform icon)
    {
        Vector3 orig = icon.localScale;
        float t = 0;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            float scale = 1f + Mathf.Sin(t / 0.3f * Mathf.PI) * 0.15f;
            icon.localScale = orig * scale;
            yield return null;
        }
        icon.localScale = orig;
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
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
