using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Boss连战模式UI - 管理Boss Rush的所有界面元素
/// 包含：开始界面、Boss信息板、休息倒计时、结算界面
/// </summary>
public class BossRushUI : MonoBehaviour
{
    [Header("主面板")]
    [SerializeField] private GameObject bossRushPanel;
    [SerializeField] private CanvasGroup mainCanvasGroup;

    [Header("开始界面")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private TextMeshProUGUI startTitleText;
    [SerializeField] private TextMeshProUGUI bestTimeText;
    [SerializeField] private TextMeshProUGUI bestDeathsText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton;

    [Header("Boss信息板")]
    [SerializeField] private GameObject bossInfoPanel;
    [SerializeField] private TextMeshProUGUI bossNameText;
    [SerializeField] private TextMeshProUGUI bossNumberText;
    [SerializeField] private Image bossPortrait;
    [SerializeField] private Slider bossHealthBar;
    [SerializeField] private TextMeshProUGUI bossHealthText;
    [SerializeField] private CanvasGroup bossInfoCanvasGroup;

    [Header("HUD")]
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI deathCountText;
    [SerializeField] private TextMeshProUGUI bossProgressText;
    [SerializeField] private Image[] bossIcons;
    [SerializeField] private Sprite bossIconDefault;
    [SerializeField] private Sprite bossIconDefeated;
    [SerializeField] private Sprite bossIconCurrent;

    [Header("休息面板")]
    [SerializeField] private GameObject restPanel;
    [SerializeField] private TextMeshProUGUI restTimerText;
    [SerializeField] private TextMeshProUGUI nextBossNameText;
    [SerializeField] private Image nextBossPortrait;
    [SerializeField] private Slider restTimerBar;
    [SerializeField] private TextMeshProUGUI restHealText;

    [Header("结算面板")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private CanvasGroup resultCanvasGroup;
    [SerializeField] private TextMeshProUGUI resultTitleText;
    [SerializeField] private TextMeshProUGUI resultTotalTimeText;
    [SerializeField] private TextMeshProUGUI resultDeathsText;
    [SerializeField] private TextMeshProUGUI resultNoHitText;
    [SerializeField] private TextMeshProUGUI resultRankText;
    [SerializeField] private TextMeshProUGUI resultNewRecordText;
    [SerializeField] private Button resultRetryButton;
    [SerializeField] private Button resultMenuButton;

    [Header("Boss详情列表（结算用）")]
    [SerializeField] private Transform bossResultListParent;
    [SerializeField] private GameObject bossResultItemPrefab;

    [Header("动画设置")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float bossRevealDuration = 1.5f;
    [SerializeField] private float resultRowDelay = 0.2f;
    [SerializeField] private float numberCountDuration = 0.8f;

    // 缓存
    private float maxRestTime;
    private Coroutine currentAnimation;

    void Awake()
    {
        HideAll();
    }

    void Start()
    {
        // 按钮绑定
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
        if (resultRetryButton != null) resultRetryButton.onClick.AddListener(OnRetryClicked);
        if (resultMenuButton != null) resultMenuButton.onClick.AddListener(OnMenuClicked);

        // 订阅BossRush事件
        if (BossRushMode.Instance != null)
        {
            BossRushMode.Instance.OnBossRushStarted += OnBossRushStarted;
            BossRushMode.Instance.OnBossEncounterStarted += OnBossEncounterStarted;
            BossRushMode.Instance.OnBossDefeated += OnBossDefeated;
            BossRushMode.Instance.OnRestPhase += OnRestPhase;
            BossRushMode.Instance.OnBossRushComplete += OnBossRushComplete;
        }
    }

    void Update()
    {
        UpdateHUD();
    }

    void OnDestroy()
    {
        if (BossRushMode.Instance != null)
        {
            BossRushMode.Instance.OnBossRushStarted -= OnBossRushStarted;
            BossRushMode.Instance.OnBossEncounterStarted -= OnBossEncounterStarted;
            BossRushMode.Instance.OnBossDefeated -= OnBossDefeated;
            BossRushMode.Instance.OnRestPhase -= OnRestPhase;
            BossRushMode.Instance.OnBossRushComplete -= OnBossRushComplete;
        }
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 显示Boss连战开始界面
    /// </summary>
    public void ShowStartScreen()
    {
        HideAll();
        if (bossRushPanel != null) bossRushPanel.SetActive(true);
        if (startPanel != null) startPanel.SetActive(true);

        // 更新最佳记录
        if (BossRushMode.Instance != null)
        {
            float bestTime = BossRushMode.Instance.GetBestTime();
            int bestDeaths = BossRushMode.Instance.GetBestDeaths();

            if (bestTimeText != null)
            {
                bestTimeText.text = bestTime < float.MaxValue
                    ? FormatTime(bestTime)
                    : GetLocalizedText("no_record", "---");
            }

            if (bestDeathsText != null)
            {
                bestDeathsText.text = bestDeaths < int.MaxValue
                    ? bestDeaths.ToString()
                    : GetLocalizedText("no_record", "---");
            }

            // 解锁检查
            bool unlocked = BossRushMode.Instance.IsUnlocked();
            if (startButton != null) startButton.interactable = unlocked;

            if (startTitleText != null)
            {
                startTitleText.text = GetLocalizedText("boss_rush_title", "BOSS RUSH");
            }
        }

        if (currentAnimation != null) StopCoroutine(currentAnimation);
        currentAnimation = StartCoroutine(FadeIn(mainCanvasGroup));
    }

    /// <summary>
    /// 隐藏所有面板
    /// </summary>
    public void HideAll()
    {
        SetPanelActive(startPanel, false);
        SetPanelActive(bossInfoPanel, false);
        SetPanelActive(hudPanel, false);
        SetPanelActive(restPanel, false);
        SetPanelActive(resultPanel, false);
    }

    // ==================== 事件处理 ====================

    private void OnBossRushStarted()
    {
        HideAll();
        if (bossRushPanel != null) bossRushPanel.SetActive(true);
        if (hudPanel != null) hudPanel.SetActive(true);

        UpdateBossIcons(-1);
    }

    private void OnBossEncounterStarted(int bossIndex)
    {
        SetPanelActive(restPanel, false);

        if (BossRushMode.Instance == null) return;
        var entry = BossRushMode.Instance.CurrentBossEntry;
        if (entry == null) return;

        // 显示Boss信息板
        if (currentAnimation != null) StopCoroutine(currentAnimation);
        currentAnimation = StartCoroutine(ShowBossIntro(entry, bossIndex));

        UpdateBossIcons(bossIndex);
    }

    private void OnBossDefeated(int bossIndex, BossRushMode.BossResult result)
    {
        // 闪烁Boss信息板然后隐藏
        if (currentAnimation != null) StopCoroutine(currentAnimation);
        currentAnimation = StartCoroutine(BossDefeatedAnimation(bossIndex));
    }

    private void OnRestPhase(float restTimeRemaining)
    {
        if (restPanel != null && !restPanel.activeSelf)
        {
            restPanel.SetActive(true);

            // 显示下一个Boss信息
            if (BossRushMode.Instance != null)
            {
                int nextIndex = BossRushMode.Instance.CurrentBossIndex;
                if (nextIndex < BossRushMode.Instance.TotalBosses)
                {
                    // 通过反射或直接访问获取entry信息
                    if (nextBossNameText != null)
                    {
                        nextBossNameText.text = GetLocalizedText(
                            $"boss_{nextIndex + 1}_name",
                            $"Boss {nextIndex + 1}");
                    }
                }
            }

            if (restHealText != null)
            {
                restHealText.text = GetLocalizedText("rest_heal", "Recovering HP...");
            }

            maxRestTime = restTimeRemaining;
        }

        // 更新休息倒计时
        if (restTimerText != null)
        {
            restTimerText.text = $"{Mathf.CeilToInt(restTimeRemaining)}s";
        }

        if (restTimerBar != null && maxRestTime > 0)
        {
            restTimerBar.value = restTimeRemaining / maxRestTime;
        }
    }

    private void OnBossRushComplete(BossRushMode.BossRushSummary summary)
    {
        HideAll();
        if (bossRushPanel != null) bossRushPanel.SetActive(true);

        if (currentAnimation != null) StopCoroutine(currentAnimation);
        currentAnimation = StartCoroutine(ShowResultScreen(summary));
    }

    // ==================== HUD更新 ====================

    private void UpdateHUD()
    {
        if (BossRushMode.Instance == null) return;
        if (!BossRushMode.Instance.IsActive) return;

        // 计时器
        if (timerText != null)
        {
            timerText.text = FormatTime(BossRushMode.Instance.TotalTime);
        }

        // 死亡数
        if (deathCountText != null)
        {
            deathCountText.text = BossRushMode.Instance.TotalDeaths.ToString();
        }

        // Boss进度
        if (bossProgressText != null)
        {
            bossProgressText.text = $"{BossRushMode.Instance.CurrentBossIndex + 1}/{BossRushMode.Instance.TotalBosses}";
        }
    }

    // ==================== 动画 ====================

    private IEnumerator ShowBossIntro(BossRushMode.BossRushEntry entry, int bossIndex)
    {
        if (bossInfoPanel != null) bossInfoPanel.SetActive(true);

        // Boss编号
        if (bossNumberText != null)
        {
            bossNumberText.text = $"BOSS {bossIndex + 1}/{BossRushMode.Instance?.TotalBosses ?? 5}";
        }

        // Boss名称（从空白开始逐字显示）
        if (bossNameText != null)
        {
            string fullName = GetLocalizedText(entry.bossNameKey, $"Boss {bossIndex + 1}");
            bossNameText.text = "";

            for (int i = 0; i <= fullName.Length; i++)
            {
                bossNameText.text = fullName.Substring(0, i);
                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        // Boss头像
        if (bossPortrait != null && entry.bossPortrait != null)
        {
            bossPortrait.sprite = entry.bossPortrait;
            bossPortrait.color = Color.white;
        }

        // Boss血条初始化
        if (bossHealthBar != null)
        {
            bossHealthBar.value = 1f;
        }
        if (bossHealthText != null)
        {
            float scaledHealth = entry.baseHealth * (BossRushMode.Instance?.GetCurrentHealthScale() ?? 1f);
            bossHealthText.text = $"{scaledHealth:F0}";
        }

        // 淡入效果
        yield return FadeIn(bossInfoCanvasGroup);

        yield return new WaitForSecondsRealtime(bossRevealDuration);

        // 战斗开始时淡出信息板（但保留HUD上的名称）
        yield return FadeOut(bossInfoCanvasGroup);
        if (bossInfoPanel != null) bossInfoPanel.SetActive(false);
    }

    private IEnumerator BossDefeatedAnimation(int bossIndex)
    {
        // 闪烁效果
        if (bossInfoCanvasGroup != null)
        {
            if (bossInfoPanel != null) bossInfoPanel.SetActive(true);
            for (int i = 0; i < 4; i++)
            {
                bossInfoCanvasGroup.alpha = i % 2 == 0 ? 1f : 0.3f;
                yield return new WaitForSecondsRealtime(0.15f);
            }
            bossInfoCanvasGroup.alpha = 0;
            if (bossInfoPanel != null) bossInfoPanel.SetActive(false);
        }

        // 更新Boss图标为已击败
        UpdateBossIcons(bossIndex + 1);
    }

    private IEnumerator ShowResultScreen(BossRushMode.BossRushSummary summary)
    {
        if (resultPanel != null) resultPanel.SetActive(true);

        // 淡入
        yield return FadeIn(resultCanvasGroup);

        // 标题
        if (resultTitleText != null)
        {
            resultTitleText.text = GetLocalizedText("boss_rush_complete", "BOSS RUSH COMPLETE!");
        }

        yield return new WaitForSecondsRealtime(0.5f);

        // 总用时
        if (resultTotalTimeText != null)
        {
            yield return AnimateTime(resultTotalTimeText, summary.totalTime);
        }
        yield return new WaitForSecondsRealtime(resultRowDelay);

        // 死亡数
        if (resultDeathsText != null)
        {
            yield return AnimateNumber(resultDeathsText, 0, summary.totalDeaths, "{0}");
        }
        yield return new WaitForSecondsRealtime(resultRowDelay);

        // 无伤Boss数
        if (resultNoHitText != null)
        {
            yield return AnimateNumber(resultNoHitText, 0, summary.noHitBossCount,
                "{0}/" + summary.totalBossesDefeated);
        }
        yield return new WaitForSecondsRealtime(resultRowDelay);

        // Boss详情列表
        if (bossResultListParent != null && bossResultItemPrefab != null)
        {
            // 清除旧条目
            foreach (Transform child in bossResultListParent)
                Destroy(child.gameObject);

            foreach (var result in summary.bossResults)
            {
                var item = Instantiate(bossResultItemPrefab, bossResultListParent);
                SetupBossResultItem(item, result);
                yield return new WaitForSecondsRealtime(resultRowDelay);
            }
        }

        yield return new WaitForSecondsRealtime(0.5f);

        // 新记录提示
        if (resultNewRecordText != null)
        {
            if (summary.isNewBestTime || summary.isNewBestDeaths)
            {
                string recordText = "";
                if (summary.isNewBestTime)
                    recordText += GetLocalizedText("new_best_time", "NEW BEST TIME!") + "\n";
                if (summary.isNewBestDeaths)
                    recordText += GetLocalizedText("new_best_deaths", "NEW LOWEST DEATHS!");

                resultNewRecordText.text = recordText;
                resultNewRecordText.gameObject.SetActive(true);

                // 闪烁动画
                StartCoroutine(FlashText(resultNewRecordText));
            }
            else
            {
                resultNewRecordText.gameObject.SetActive(false);
            }
        }

        // 评级显示
        yield return new WaitForSecondsRealtime(0.5f);
        if (resultRankText != null)
        {
            resultRankText.text = summary.rank;
            resultRankText.color = GetRankColor(summary.rank);

            // 缩放弹入
            resultRankText.transform.localScale = Vector3.one * 4f;
            float timer = 0;
            while (timer < 0.5f)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / 0.5f;
                float scale = Mathf.Lerp(4f, 1f, t * t);
                resultRankText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            resultRankText.transform.localScale = Vector3.one;

            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play("rank_reveal");
        }
    }

    // ==================== UI组件设置 ====================

    private void SetupBossResultItem(GameObject item, BossRushMode.BossResult result)
    {
        var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length >= 3)
        {
            // Boss名称
            texts[0].text = GetLocalizedText($"boss_{result.bossId}", result.bossId);

            // 用时
            texts[1].text = FormatTime(result.timeToDefeat);

            // 无伤标记
            if (result.noHit)
            {
                texts[2].text = GetLocalizedText("no_hit", "NO HIT!");
                texts[2].color = new Color(1f, 0.85f, 0.2f); // 金色
            }
            else
            {
                texts[2].text = "";
            }
        }
    }

    private void UpdateBossIcons(int currentIndex)
    {
        if (bossIcons == null) return;

        for (int i = 0; i < bossIcons.Length; i++)
        {
            if (bossIcons[i] == null) continue;

            if (i < currentIndex)
                bossIcons[i].sprite = bossIconDefeated;
            else if (i == currentIndex)
                bossIcons[i].sprite = bossIconCurrent;
            else
                bossIcons[i].sprite = bossIconDefault;
        }
    }

    // ==================== 按钮回调 ====================

    private void OnStartClicked()
    {
        if (BossRushMode.Instance != null)
        {
            BossRushMode.Instance.StartBossRush();
        }
    }

    private void OnBackClicked()
    {
        HideAll();
        if (bossRushPanel != null) bossRushPanel.SetActive(false);
    }

    private void OnRetryClicked()
    {
        HideAll();
        if (BossRushMode.Instance != null)
        {
            BossRushMode.Instance.StartBossRush();
        }
    }

    private void OnMenuClicked()
    {
        HideAll();
        if (bossRushPanel != null) bossRushPanel.SetActive(false);

        // 返回主菜单
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.GoToMainMenu();
    }

    // ==================== 动画辅助 ====================

    private IEnumerator FadeIn(CanvasGroup cg)
    {
        if (cg == null) yield break;

        cg.alpha = 0;
        float timer = 0;
        while (timer < fadeInDuration)
        {
            timer += Time.unscaledDeltaTime;
            cg.alpha = timer / fadeInDuration;
            yield return null;
        }
        cg.alpha = 1;
    }

    private IEnumerator FadeOut(CanvasGroup cg)
    {
        if (cg == null) yield break;

        float timer = 0;
        while (timer < fadeInDuration)
        {
            timer += Time.unscaledDeltaTime;
            cg.alpha = 1f - timer / fadeInDuration;
            yield return null;
        }
        cg.alpha = 0;
    }

    private IEnumerator AnimateNumber(TextMeshProUGUI text, int from, int to, string format)
    {
        if (text == null) yield break;

        float elapsed = 0;
        while (elapsed < numberCountDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / numberCountDuration;
            t = t * t;
            int current = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
            text.text = string.Format(format, current);
            yield return null;
        }
        text.text = string.Format(format, to);
    }

    private IEnumerator AnimateTime(TextMeshProUGUI text, float targetTime)
    {
        if (text == null) yield break;

        float elapsed = 0;
        while (elapsed < numberCountDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / numberCountDuration;
            t = t * t;
            float current = Mathf.Lerp(0, targetTime, t);
            text.text = FormatTime(current);
            yield return null;
        }
        text.text = FormatTime(targetTime);
    }

    private IEnumerator FlashText(TextMeshProUGUI text)
    {
        if (text == null) yield break;

        Color original = text.color;
        Color flash = new Color(1f, 0.85f, 0.2f); // 金色

        for (int i = 0; i < 6; i++)
        {
            text.color = i % 2 == 0 ? flash : original;
            yield return new WaitForSecondsRealtime(0.3f);
        }
        text.color = flash;
    }

    // ==================== 工具方法 ====================

    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        int ms = Mathf.FloorToInt((seconds * 100f) % 100f);
        return $"{minutes:D2}:{secs:D2}.{ms:D2}";
    }

    private Color GetRankColor(string rank)
    {
        return rank switch
        {
            "S" => new Color(1f, 0.85f, 0.2f),
            "A" => new Color(0.4f, 0.9f, 0.4f),
            "B" => new Color(0.4f, 0.6f, 1f),
            "C" => Color.white,
            _ => new Color(0.7f, 0.7f, 0.7f)
        };
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
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
