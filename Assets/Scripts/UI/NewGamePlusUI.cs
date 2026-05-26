using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 新游戏+UI - 显示NG+信息和启动界面
/// 包含：当前NG+等级、难度信息、解锁内容列表、确认对话
/// </summary>
public class NewGamePlusUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject ngPlusPanel;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("状态显示")]
    [SerializeField] private TextMeshProUGUI currentLevelText;
    [SerializeField] private TextMeshProUGUI nextLevelText;

    [Header("难度信息")]
    [SerializeField] private TextMeshProUGUI enemyHPText;
    [SerializeField] private TextMeshProUGUI enemyDMGText;
    [SerializeField] private TextMeshProUGUI enemySpeedText;
    [SerializeField] private TextMeshProUGUI expBonusText;
    [SerializeField] private TextMeshProUGUI coinBonusText;
    [SerializeField] private TextMeshProUGUI scoreBonusText;

    [Header("解锁内容")]
    [SerializeField] private Transform contentListParent;
    [SerializeField] private GameObject contentItemPrefab;

    [Header("确认对话")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private TextMeshProUGUI confirmText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    [Header("按钮")]
    [SerializeField] private Button startNGButton;
    [SerializeField] private Button backButton;

    [Header("颜色")]
    [SerializeField] private Color ngLevel1Color = new Color(0.4f, 0.8f, 0.4f);
    [SerializeField] private Color ngLevel2Color = new Color(0.3f, 0.6f, 1f);
    [SerializeField] private Color ngLevel3Color = new Color(1f, 0.4f, 0.3f);

    // 缓存
    private List<GameObject> spawnedItems = new List<GameObject>();

    void Start()
    {
        if (startNGButton != null) startNGButton.onClick.AddListener(OnStartClicked);
        if (backButton != null) backButton.onClick.AddListener(Hide);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);

        if (ngPlusPanel != null) ngPlusPanel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 显示NG+界面
    /// </summary>
    public void Show()
    {
        if (ngPlusPanel != null) ngPlusPanel.SetActive(true);

        RefreshDisplay();
        StartCoroutine(FadeIn());
    }

    /// <summary>
    /// 隐藏界面
    /// </summary>
    public void Hide()
    {
        StartCoroutine(FadeOutAndHide());
    }

    // ==================== 显示更新 ====================

    private void RefreshDisplay()
    {
        if (NewGamePlusManager.Instance == null) return;

        int current = NewGamePlusManager.Instance.CurrentNGLevel;
        int next = current + 1;
        bool canStart = NewGamePlusManager.Instance.CanStartNewGamePlus;

        // 当前等级
        if (currentLevelText != null)
        {
            if (current == 0)
                currentLevelText.text = GetLocalizedText("ng_current_normal", "Normal Mode");
            else
            {
                currentLevelText.text = $"NG+{current}";
                currentLevelText.color = GetNGColor(current);
            }
        }

        // 下一等级
        if (nextLevelText != null)
        {
            if (canStart)
            {
                nextLevelText.text = $"→ NG+{next}";
                nextLevelText.color = GetNGColor(next);
            }
            else
            {
                nextLevelText.text = GetLocalizedText("ng_max_reached", "MAX LEVEL");
                nextLevelText.color = new Color(1f, 0.85f, 0.2f);
            }
        }

        // 难度预览（显示下一级的数据）
        UpdateDifficultyPreview(next);

        // 解锁内容
        UpdateContentList(next);

        // 开始按钮
        if (startNGButton != null)
            startNGButton.interactable = canStart;
    }

    private void UpdateDifficultyPreview(int ngLevel)
    {
        if (NewGamePlusManager.Instance == null) return;

        // 使用下一级的乘数计算
        float hpMult = 1f + ngLevel * 0.5f;
        float dmgMult = 1f + ngLevel * 0.3f;
        float spdMult = 1f + ngLevel * 0.1f;
        float expMult = 1f + ngLevel * 0.25f;
        float coinMult = 1f + ngLevel * 0.3f;
        float scoreMult = 1f + ngLevel * 0.5f;

        if (enemyHPText != null)
            enemyHPText.text = $"x{hpMult:F1}";

        if (enemyDMGText != null)
            enemyDMGText.text = $"x{dmgMult:F1}";

        if (enemySpeedText != null)
            enemySpeedText.text = $"x{spdMult:F1}";

        if (expBonusText != null)
            expBonusText.text = $"+{(ngLevel * 25):F0}%";

        if (coinBonusText != null)
            coinBonusText.text = $"+{(ngLevel * 30):F0}%";

        if (scoreBonusText != null)
            scoreBonusText.text = $"+{(ngLevel * 50):F0}%";
    }

    private void UpdateContentList(int ngLevel)
    {
        ClearItems();

        if (NewGamePlusManager.Instance == null) return;
        if (contentItemPrefab == null || contentListParent == null) return;

        // 获取将在这个级别解锁的内容
        var allContent = NewGamePlusManager.Instance.GetUnlockedContent();
        // 这里简化处理，显示targetNG的内容
        // 由于GetUnlockedContent返回所有已解锁的，我们需要不同的方式

        // 使用IsContentUnlocked检查每个可能的内容
        string[] knownIds = {
            "boss_variant_1", "hidden_level_abyss", "relic_ng_eclipse",
            "boss_variant_all", "true_ending", "skin_golden",
            "chaos_mode", "skin_cosmic"
        };

        foreach (var id in knownIds)
        {
            bool currentlyUnlocked = NewGamePlusManager.Instance.IsContentUnlocked(id);
            bool willUnlock = !currentlyUnlocked; // 简化：在更高NG+中会解锁

            SpawnContentItem(id, currentlyUnlocked);
        }
    }

    private void SpawnContentItem(string contentId, bool unlocked)
    {
        if (contentItemPrefab == null || contentListParent == null) return;

        var item = Instantiate(contentItemPrefab, contentListParent);
        spawnedItems.Add(item);

        var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length > 0)
        {
            string displayName = GetLocalizedText($"ng_content_{contentId}", FormatContentId(contentId));
            texts[0].text = unlocked ? displayName : "???";
            texts[0].color = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f);
        }

        // 解锁图标
        var images = item.GetComponentsInChildren<Image>();
        if (images.Length > 1)
        {
            images[1].color = unlocked
                ? new Color(0.4f, 0.9f, 0.4f)   // 绿色勾
                : new Color(0.5f, 0.5f, 0.5f);   // 灰色锁
        }
    }

    // ==================== 确认对话 ====================

    private void OnStartClicked()
    {
        if (NewGamePlusManager.Instance == null) return;
        if (!NewGamePlusManager.Instance.CanStartNewGamePlus) return;

        // 显示确认
        if (confirmPanel != null) confirmPanel.SetActive(true);

        int nextLevel = NewGamePlusManager.Instance.CurrentNGLevel + 1;
        if (confirmText != null)
        {
            confirmText.text = GetLocalizedText("ng_confirm",
                $"Start NG+{nextLevel}?\n\nLevel progress will be reset.\nRelics, skills, and level will be kept.");
        }

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    private void OnConfirmYes()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);

        if (NewGamePlusManager.Instance != null)
        {
            NewGamePlusManager.Instance.StartNewGamePlus();

            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.PlayConfirm();

            // 刷新显示
            RefreshDisplay();

            // 开始游戏
            GameManager.Instance?.LoadLevel(1, 1);

            Hide();
        }
    }

    private void OnConfirmNo()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    // ==================== 辅助 ====================

    private Color GetNGColor(int level)
    {
        return level switch
        {
            1 => ngLevel1Color,
            2 => ngLevel2Color,
            3 => ngLevel3Color,
            _ => Color.white
        };
    }

    private string FormatContentId(string id)
    {
        // boss_variant_1 → Boss Variant 1
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(id.Replace('_', ' '));
    }

    private void ClearItems()
    {
        foreach (var item in spawnedItems)
        {
            if (item != null) Destroy(item);
        }
        spawnedItems.Clear();
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null) yield break;
        canvasGroup.alpha = 0;
        float t = 0;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = t / 0.3f;
            yield return null;
        }
        canvasGroup.alpha = 1;
    }

    private IEnumerator FadeOutAndHide()
    {
        if (canvasGroup != null)
        {
            float t = 0;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - t / 0.3f;
                yield return null;
            }
        }

        ClearItems();
        if (ngPlusPanel != null) ngPlusPanel.SetActive(false);
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
