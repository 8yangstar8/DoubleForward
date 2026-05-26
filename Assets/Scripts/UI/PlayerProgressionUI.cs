using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 玩家成长UI - 显示经验值、等级、天赋点分配
/// 包含：HUD经验条、升级演出、天赋分配面板
/// </summary>
public class PlayerProgressionUI : MonoBehaviour
{
    [Header("HUD - 经验条")]
    [SerializeField] private GameObject expHudPanel;
    [SerializeField] private Slider expBar;
    [SerializeField] private Image expBarFill;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI expText;

    [Header("经验获取浮动文字")]
    [SerializeField] private GameObject expPopupPrefab;
    [SerializeField] private Transform expPopupParent;

    [Header("升级演出")]
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private CanvasGroup levelUpCanvasGroup;
    [SerializeField] private TextMeshProUGUI levelUpTitleText;
    [SerializeField] private TextMeshProUGUI newLevelText;
    [SerializeField] private TextMeshProUGUI talentPointsText;
    [SerializeField] private Image levelUpGlow;

    [Header("天赋面板")]
    [SerializeField] private GameObject talentPanel;
    [SerializeField] private CanvasGroup talentCanvasGroup;
    [SerializeField] private TextMeshProUGUI luxPointsText;
    [SerializeField] private TextMeshProUGUI noxPointsText;
    [SerializeField] private TextMeshProUGUI currentLevelInfoText;
    [SerializeField] private Slider detailExpBar;
    [SerializeField] private TextMeshProUGUI detailExpText;

    [Header("天赋按钮（Lux）")]
    [SerializeField] private Button luxHealthButton;
    [SerializeField] private Button luxAttackButton;
    [SerializeField] private Button luxSpeedButton;
    [SerializeField] private Button luxLightRadiusButton;

    [Header("天赋按钮（Nox）")]
    [SerializeField] private Button noxHealthButton;
    [SerializeField] private Button noxAttackButton;
    [SerializeField] private Button noxSpeedButton;
    [SerializeField] private Button noxShadowButton;

    [Header("天赋导航")]
    [SerializeField] private Button openTalentButton;
    [SerializeField] private Button closeTalentButton;

    [Header("动画")]
    [SerializeField] private float levelUpDisplayDuration = 3f;
    [SerializeField] private float expPopupDuration = 1.5f;

    [Header("颜色")]
    [SerializeField] private Color expBarColor = new Color(0.3f, 0.8f, 1f);
    [SerializeField] private Color maxLevelColor = new Color(1f, 0.85f, 0.2f);

    // 缓存
    private Coroutine levelUpCoroutine;
    private float displayedExp;

    void Start()
    {
        // 订阅事件
        if (PlayerProgressionSystem.Instance != null)
        {
            PlayerProgressionSystem.Instance.OnLevelUp += OnLevelUp;
            PlayerProgressionSystem.Instance.OnExpGained += OnExpGained;
            PlayerProgressionSystem.Instance.OnTalentPointsChanged += OnTalentPointsChanged;
        }

        // 按钮绑定
        if (openTalentButton != null) openTalentButton.onClick.AddListener(ShowTalentPanel);
        if (closeTalentButton != null) closeTalentButton.onClick.AddListener(HideTalentPanel);

        // Lux天赋
        BindTalentButton(luxHealthButton, 0, "health");
        BindTalentButton(luxAttackButton, 0, "attack");
        BindTalentButton(luxSpeedButton, 0, "speed");
        BindTalentButton(luxLightRadiusButton, 0, "light_radius");

        // Nox天赋
        BindTalentButton(noxHealthButton, 1, "health");
        BindTalentButton(noxAttackButton, 1, "attack");
        BindTalentButton(noxSpeedButton, 1, "speed");
        BindTalentButton(noxShadowButton, 1, "shadow_phase");

        // 初始化
        SetPanelActive(levelUpPanel, false);
        SetPanelActive(talentPanel, false);

        UpdateHUD();
    }

    void OnDestroy()
    {
        if (PlayerProgressionSystem.Instance != null)
        {
            PlayerProgressionSystem.Instance.OnLevelUp -= OnLevelUp;
            PlayerProgressionSystem.Instance.OnExpGained -= OnExpGained;
            PlayerProgressionSystem.Instance.OnTalentPointsChanged -= OnTalentPointsChanged;
        }
    }

    // ==================== HUD更新 ====================

    private void UpdateHUD()
    {
        if (PlayerProgressionSystem.Instance == null) return;

        int level = PlayerProgressionSystem.Instance.CurrentLevel;
        float progress = PlayerProgressionSystem.Instance.ExpProgress;
        bool maxLevel = PlayerProgressionSystem.Instance.IsMaxLevel;

        // 等级数字
        if (levelText != null)
        {
            levelText.text = $"Lv.{level}";
            levelText.color = maxLevel ? maxLevelColor : Color.white;
        }

        // 经验条
        if (expBar != null)
            expBar.value = maxLevel ? 1f : progress;

        if (expBarFill != null)
            expBarFill.color = maxLevel ? maxLevelColor : expBarColor;

        // 经验文字
        if (expText != null)
        {
            if (maxLevel)
                expText.text = "MAX";
            else
            {
                float current = PlayerProgressionSystem.Instance.CurrentExp;
                float needed = PlayerProgressionSystem.Instance.ExpForNextLevel;
                expText.text = $"{current:F0}/{needed:F0}";
            }
        }
    }

    // ==================== 事件处理 ====================

    private void OnLevelUp(int newLevel)
    {
        UpdateHUD();

        if (levelUpCoroutine != null) StopCoroutine(levelUpCoroutine);
        levelUpCoroutine = StartCoroutine(LevelUpAnimation(newLevel));
    }

    private void OnExpGained(float amount, string source)
    {
        UpdateHUD();

        // 浮动经验文字
        if (expPopupPrefab != null && expPopupParent != null)
        {
            SpawnExpPopup(amount, source);
        }

        // 经验条平滑动画
        if (expBar != null)
        {
            StartCoroutine(SmoothExpBar());
        }
    }

    private void OnTalentPointsChanged(int luxPoints, int noxPoints)
    {
        UpdateTalentDisplay();
    }

    // ==================== 升级演出 ====================

    private IEnumerator LevelUpAnimation(int newLevel)
    {
        SetPanelActive(levelUpPanel, true);

        if (levelUpTitleText != null)
            levelUpTitleText.text = GetLocalizedText("level_up", "LEVEL UP!");

        if (newLevelText != null)
        {
            newLevelText.text = $"Lv.{newLevel}";
            newLevelText.color = maxLevelColor;
        }

        // 显示获得的天赋点
        if (talentPointsText != null && PlayerProgressionSystem.Instance != null)
        {
            int luxPts = PlayerProgressionSystem.Instance.LuxTalentPoints;
            int noxPts = PlayerProgressionSystem.Instance.NoxTalentPoints;

            if (luxPts > 0 || noxPts > 0)
            {
                talentPointsText.text = GetLocalizedText("talent_points_earned",
                    $"+{luxPts} Lux / +{noxPts} Nox Talent Points");
                talentPointsText.gameObject.SetActive(true);
            }
            else
            {
                talentPointsText.gameObject.SetActive(false);
            }
        }

        // 发光
        if (levelUpGlow != null)
            levelUpGlow.color = maxLevelColor;

        // 淡入
        yield return FadeIn(levelUpCanvasGroup, 0.3f);

        // 缩放弹入
        if (newLevelText != null)
        {
            newLevelText.transform.localScale = Vector3.one * 3f;
            float timer = 0;
            while (timer < 0.4f)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / 0.4f;
                float scale = Mathf.Lerp(3f, 1f, t * t);
                newLevelText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            newLevelText.transform.localScale = Vector3.one;
        }

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();

        // 保持显示
        yield return new WaitForSecondsRealtime(levelUpDisplayDuration);

        // 淡出
        yield return FadeOut(levelUpCanvasGroup, 0.5f);
        SetPanelActive(levelUpPanel, false);
    }

    // ==================== 天赋面板 ====================

    public void ShowTalentPanel()
    {
        SetPanelActive(talentPanel, true);
        UpdateTalentDisplay();
        StartCoroutine(FadeIn(talentCanvasGroup, 0.3f));
    }

    public void HideTalentPanel()
    {
        StartCoroutine(HideTalentPanelCoroutine());
    }

    private IEnumerator HideTalentPanelCoroutine()
    {
        yield return FadeOut(talentCanvasGroup, 0.2f);
        SetPanelActive(talentPanel, false);
    }

    private void UpdateTalentDisplay()
    {
        if (PlayerProgressionSystem.Instance == null) return;

        if (luxPointsText != null)
            luxPointsText.text = $"Lux: {PlayerProgressionSystem.Instance.LuxTalentPoints}";

        if (noxPointsText != null)
            noxPointsText.text = $"Nox: {PlayerProgressionSystem.Instance.NoxTalentPoints}";

        if (currentLevelInfoText != null)
            currentLevelInfoText.text = $"Lv.{PlayerProgressionSystem.Instance.CurrentLevel}";

        if (detailExpBar != null)
            detailExpBar.value = PlayerProgressionSystem.Instance.ExpProgress;

        if (detailExpText != null)
        {
            float current = PlayerProgressionSystem.Instance.CurrentExp;
            float needed = PlayerProgressionSystem.Instance.ExpForNextLevel;
            detailExpText.text = PlayerProgressionSystem.Instance.IsMaxLevel
                ? "MAX" : $"{current:F0} / {needed:F0}";
        }

        // 更新按钮状态
        UpdateTalentButtons();
    }

    private void UpdateTalentButtons()
    {
        if (PlayerProgressionSystem.Instance == null) return;

        bool luxHasPoints = PlayerProgressionSystem.Instance.LuxTalentPoints > 0;
        bool noxHasPoints = PlayerProgressionSystem.Instance.NoxTalentPoints > 0;

        SetButtonInteractable(luxHealthButton, luxHasPoints);
        SetButtonInteractable(luxAttackButton, luxHasPoints);
        SetButtonInteractable(luxSpeedButton, luxHasPoints);
        SetButtonInteractable(luxLightRadiusButton, luxHasPoints);

        SetButtonInteractable(noxHealthButton, noxHasPoints);
        SetButtonInteractable(noxAttackButton, noxHasPoints);
        SetButtonInteractable(noxSpeedButton, noxHasPoints);
        SetButtonInteractable(noxShadowButton, noxHasPoints);
    }

    private void BindTalentButton(Button button, int playerIndex, string talentId)
    {
        if (button == null) return;

        button.onClick.AddListener(() =>
        {
            if (PlayerProgressionSystem.Instance != null)
            {
                var playerType = playerIndex == 0
                    ? PlayerController.PlayerType.Lux
                    : PlayerController.PlayerType.Nox;

                bool success = PlayerProgressionSystem.Instance.SpendTalentPoint(playerType);
                if (success)
                {
                    UpdateTalentDisplay();

                    if (SoundFeedback.Instance != null)
                        SoundFeedback.Instance.PlayConfirm();
                }
            }
        });
    }

    // ==================== 浮动经验文字 ====================

    private void SpawnExpPopup(float amount, string source)
    {
        var popup = Instantiate(expPopupPrefab, expPopupParent);

        var text = popup.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = $"+{amount:F0} EXP";
            text.color = expBarColor;
        }

        StartCoroutine(AnimateExpPopup(popup));
    }

    private IEnumerator AnimateExpPopup(GameObject popup)
    {
        if (popup == null) yield break;

        Vector3 startPos = popup.transform.localPosition;
        float timer = 0;

        var cg = popup.GetComponent<CanvasGroup>();
        if (cg == null) cg = popup.AddComponent<CanvasGroup>();

        while (timer < expPopupDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = timer / expPopupDuration;

            // 上浮
            popup.transform.localPosition = startPos + Vector3.up * (t * 50f);

            // 淡出
            cg.alpha = 1f - t;

            yield return null;
        }

        Destroy(popup);
    }

    private IEnumerator SmoothExpBar()
    {
        if (expBar == null || PlayerProgressionSystem.Instance == null) yield break;

        float target = PlayerProgressionSystem.Instance.IsMaxLevel ? 1f
            : PlayerProgressionSystem.Instance.ExpProgress;

        float current = expBar.value;
        float timer = 0;
        float duration = 0.5f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            expBar.value = Mathf.Lerp(current, target, timer / duration);
            yield return null;
        }

        expBar.value = target;
    }

    // ==================== 辅助 ====================

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

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null) button.interactable = interactable;
    }

    private string GetLocalizedText(string key, string fallback)
    {
        if (LocalizationSystem.Instance != null)
        {
            string localized = LocalizationSystem.Instance.GetText(key);
            if (localized != key) return localized;
        }
        return fallback;
    }
}
