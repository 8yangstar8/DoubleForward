using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 制作人员名单 - 通关后的滚动致谢界面
/// 支持多段文本、角色展示、自动滚动
/// </summary>
public class CreditsUI : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private RectTransform scrollContent;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button skipButton;
    [SerializeField] private TextMeshProUGUI skipButtonText;

    [Header("滚动设置")]
    [SerializeField] private float scrollSpeed = 50f;
    [SerializeField] private float startDelay = 2f;
    [SerializeField] private float endDelay = 3f;
    [SerializeField] private float fadeInDuration = 1f;
    [SerializeField] private float fadeOutDuration = 1.5f;

    [Header("制作人员数据")]
    [SerializeField] private CreditSection[] sections;

    [Header("显示元素")]
    [SerializeField] private GameObject sectionHeaderPrefab;
    [SerializeField] private GameObject creditEntryPrefab;
    [SerializeField] private GameObject spacerPrefab;
    [SerializeField] private GameObject imagePrefab;
    [SerializeField] private GameObject thankYouPrefab;

    [System.Serializable]
    public class CreditSection
    {
        public string sectionTitle;
        public string sectionTitleKey;     // 本地化键
        public CreditEntry[] entries;
    }

    [System.Serializable]
    public class CreditEntry
    {
        public string role;                 // 职位
        public string roleLangKey;
        public string personName;           // 姓名
    }

    private bool isScrolling;
    private bool isSkipping;
    private float scrollStartY;

    void Start()
    {
        skipButton?.onClick.AddListener(SkipCredits);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);
    }

    /// <summary>
    /// 开始播放致谢名单
    /// </summary>
    public void Show()
    {
        if (creditsPanel == null) return;

        creditsPanel.SetActive(true);
        BuildCreditsContent();
        StartCoroutine(PlayCredits());
    }

    /// <summary>
    /// 跳过
    /// </summary>
    public void SkipCredits()
    {
        isSkipping = true;
    }

    private void BuildCreditsContent()
    {
        if (scrollContent == null) return;

        // 清空
        for (int i = scrollContent.childCount - 1; i >= 0; i--)
            Destroy(scrollContent.GetChild(i).gameObject);

        // 添加顶部间距
        AddSpacer(200f);

        // 游戏标题
        AddHeader(GetLocalized("game_title", "双向前行"));
        AddSpacer(60f);

        // 各部分
        if (sections != null)
        {
            foreach (var section in sections)
            {
                // 章节标题
                string title = GetLocalized(section.sectionTitleKey, section.sectionTitle);
                AddHeader(title);
                AddSpacer(20f);

                // 条目
                if (section.entries != null)
                {
                    foreach (var entry in section.entries)
                    {
                        string role = GetLocalized(entry.roleLangKey, entry.role);
                        AddCreditEntry(role, entry.personName);
                    }
                }

                AddSpacer(50f);
            }
        }

        // 默认内容（如果没有配置sections）
        if (sections == null || sections.Length == 0)
        {
            AddDefaultCredits();
        }

        // 感谢文字
        AddSpacer(100f);
        AddThankYou();
        AddSpacer(400f);

        // 强制重建布局
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
    }

    private void AddDefaultCredits()
    {
        AddHeader(GetLocalized("credits_design", "游戏设计"));
        AddSpacer(15f);
        AddCreditEntry(GetLocalized("credits_game_design", "游戏设计"), "DoubleForward Team");
        AddCreditEntry(GetLocalized("credits_level_design", "关卡设计"), "DoubleForward Team");
        AddSpacer(40f);

        AddHeader(GetLocalized("credits_programming", "程序开发"));
        AddSpacer(15f);
        AddCreditEntry(GetLocalized("credits_lead_dev", "主程序"), "DoubleForward Team");
        AddCreditEntry(GetLocalized("credits_gameplay", "游戏逻辑"), "DoubleForward Team");
        AddCreditEntry(GetLocalized("credits_network", "网络系统"), "DoubleForward Team");
        AddCreditEntry(GetLocalized("credits_ui", "UI系统"), "DoubleForward Team");
        AddSpacer(40f);

        AddHeader(GetLocalized("credits_art", "美术"));
        AddSpacer(15f);
        AddCreditEntry(GetLocalized("credits_character", "角色设计"), "DoubleForward Team");
        AddCreditEntry(GetLocalized("credits_environment", "场景美术"), "DoubleForward Team");
        AddCreditEntry(GetLocalized("credits_vfx", "视觉特效"), "DoubleForward Team");
        AddSpacer(40f);

        AddHeader(GetLocalized("credits_audio", "音频"));
        AddSpacer(15f);
        AddCreditEntry(GetLocalized("credits_music", "音乐"), "DoubleForward Team");
        AddCreditEntry(GetLocalized("credits_sfx", "音效"), "DoubleForward Team");
        AddSpacer(40f);

        AddHeader(GetLocalized("credits_tech", "技术支持"));
        AddSpacer(15f);
        AddCreditEntry(GetLocalized("credits_engine", "游戏引擎"), "Unity 2022.3 LTS");
        AddCreditEntry(GetLocalized("credits_ai_assist", "AI辅助开发"), "Claude (Anthropic)");
        AddSpacer(40f);

        AddHeader(GetLocalized("credits_special_thanks", "特别感谢"));
        AddSpacer(15f);
        AddCreditEntry("", GetLocalized("credits_thanks_players", "感谢所有玩家的支持！"));
    }

    private void AddHeader(string text)
    {
        if (sectionHeaderPrefab == null || scrollContent == null) return;
        var obj = Instantiate(sectionHeaderPrefab, scrollContent);
        var tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }

    private void AddCreditEntry(string role, string name)
    {
        if (creditEntryPrefab == null || scrollContent == null) return;
        var obj = Instantiate(creditEntryPrefab, scrollContent);

        var roleText = obj.transform.Find("RoleText")?.GetComponent<TextMeshProUGUI>();
        var nameText = obj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();

        if (roleText != null) roleText.text = role;
        if (nameText != null) nameText.text = name;

        // 如果只有一个TMP，合并显示
        if (roleText == null && nameText == null)
        {
            var singleText = obj.GetComponentInChildren<TextMeshProUGUI>();
            if (singleText != null)
                singleText.text = string.IsNullOrEmpty(role) ? name : $"{role}\n{name}";
        }
    }

    private void AddSpacer(float height)
    {
        if (scrollContent == null) return;

        if (spacerPrefab != null)
        {
            var obj = Instantiate(spacerPrefab, scrollContent);
            var le = obj.GetComponent<LayoutElement>();
            if (le != null) le.preferredHeight = height;
        }
        else
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(scrollContent);
            var le = spacer.GetComponent<LayoutElement>();
            le.preferredHeight = height;
        }
    }

    private void AddThankYou()
    {
        if (scrollContent == null) return;

        if (thankYouPrefab != null)
        {
            Instantiate(thankYouPrefab, scrollContent);
        }
        else
        {
            // 简单文本
            var obj = new GameObject("ThankYou", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(scrollContent);
            var tmp = obj.GetComponent<TextMeshProUGUI>();
            tmp.text = GetLocalized("credits_thank_you", "感谢游玩\nThank You For Playing");
            tmp.fontSize = 42;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.85f, 0.3f);

            var rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(800, 120);
        }
    }

    private IEnumerator PlayCredits()
    {
        // 淡入
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            float t = 0;
            while (t < fadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = t / fadeInDuration;
                yield return null;
            }
            canvasGroup.alpha = 1;
        }

        yield return new WaitForSecondsRealtime(startDelay);

        // 记录起始位置
        if (scrollContent != null)
        {
            scrollStartY = scrollContent.anchoredPosition.y;
            float contentHeight = scrollContent.rect.height;
            float viewHeight = ((RectTransform)scrollContent.parent).rect.height;
            float totalScroll = contentHeight - viewHeight;

            isScrolling = true;
            float scrolled = 0;

            while (scrolled < totalScroll && !isSkipping)
            {
                float delta = scrollSpeed * Time.unscaledDeltaTime;
                scrolled += delta;
                scrollContent.anchoredPosition = new Vector2(
                    scrollContent.anchoredPosition.x,
                    scrollStartY + scrolled
                );
                yield return null;
            }

            // 滚到底
            scrollContent.anchoredPosition = new Vector2(
                scrollContent.anchoredPosition.x,
                scrollStartY + totalScroll
            );

            isScrolling = false;
        }

        yield return new WaitForSecondsRealtime(endDelay);

        // 淡出
        if (canvasGroup != null)
        {
            float t = 0;
            while (t < fadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - t / fadeOutDuration;
                yield return null;
            }
            canvasGroup.alpha = 0;
        }

        // 返回主菜单
        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.GoToMainMenu();
    }

    private string GetLocalized(string key, string fallback)
    {
        if (LocalizationSystem.Instance != null && !string.IsNullOrEmpty(key))
            return LocalizationSystem.Instance.Get(key, fallback);
        return fallback;
    }
}
