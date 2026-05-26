using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 合作评分板UI - 关卡结束时显示双人合作评分详情
/// 分别展示Lux和Nox的贡献，以及合作加成
/// 包含动画演出效果
/// </summary>
public class CoopScoreBoardUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject scoreBoardPanel;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("标题")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI rankText;

    [Header("Lux得分")]
    [SerializeField] private TextMeshProUGUI luxKillsText;
    [SerializeField] private TextMeshProUGUI luxComboText;
    [SerializeField] private TextMeshProUGUI luxAbilitiesText;
    [SerializeField] private TextMeshProUGUI luxCollectiblesText;
    [SerializeField] private TextMeshProUGUI luxSubtotalText;
    [SerializeField] private Image luxScoreBar;

    [Header("Nox得分")]
    [SerializeField] private TextMeshProUGUI noxKillsText;
    [SerializeField] private TextMeshProUGUI noxComboText;
    [SerializeField] private TextMeshProUGUI noxAbilitiesText;
    [SerializeField] private TextMeshProUGUI noxCollectiblesText;
    [SerializeField] private TextMeshProUGUI noxSubtotalText;
    [SerializeField] private Image noxScoreBar;

    [Header("合作评分")]
    [SerializeField] private TextMeshProUGUI syncActionsText;
    [SerializeField] private TextMeshProUGUI coopAbilitiesText;
    [SerializeField] private TextMeshProUGUI revivesText;
    [SerializeField] private TextMeshProUGUI proximityBonusText;
    [SerializeField] private TextMeshProUGUI coopSubtotalText;

    [Header("总分")]
    [SerializeField] private TextMeshProUGUI totalScoreText;
    [SerializeField] private TextMeshProUGUI modifierBonusText;

    [Header("星级")]
    [SerializeField] private Image[] starImages;
    [SerializeField] private Sprite starEmpty;
    [SerializeField] private Sprite starFilled;

    [Header("按钮")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;

    [Header("动画")]
    [SerializeField] private float rowRevealInterval = 0.15f;
    [SerializeField] private float numberCountDuration = 0.8f;
    [SerializeField] private float starRevealDelay = 0.5f;

    // 回调
    private System.Action onNext;
    private System.Action onRetry;
    private System.Action onMenu;

    void Start()
    {
        if (nextButton != null) nextButton.onClick.AddListener(() => onNext?.Invoke());
        if (retryButton != null) retryButton.onClick.AddListener(() => onRetry?.Invoke());
        if (menuButton != null) menuButton.onClick.AddListener(() => onMenu?.Invoke());

        if (scoreBoardPanel != null) scoreBoardPanel.SetActive(false);
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 设置按钮回调
    /// </summary>
    public void SetCallbacks(System.Action next, System.Action retry, System.Action menu)
    {
        onNext = next;
        onRetry = retry;
        onMenu = menu;
    }

    /// <summary>
    /// 显示合作评分板（带动画）
    /// </summary>
    public void ShowScoreBoard(CoopScoreData data)
    {
        if (scoreBoardPanel != null) scoreBoardPanel.SetActive(true);
        StartCoroutine(AnimateScoreBoard(data));
    }

    /// <summary>
    /// 隐藏评分板
    /// </summary>
    public void Hide()
    {
        if (scoreBoardPanel != null) scoreBoardPanel.SetActive(false);
    }

    // ==================== 动画 ====================

    private IEnumerator AnimateScoreBoard(CoopScoreData data)
    {
        // 淡入
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            float fadeTimer = 0;
            while (fadeTimer < 0.5f)
            {
                fadeTimer += Time.unscaledDeltaTime;
                canvasGroup.alpha = fadeTimer / 0.5f;
                yield return null;
            }
            canvasGroup.alpha = 1;
        }

        // 标题
        if (titleText != null)
        {
            string title = GetLocalizedText("score_title", "Level Complete!");
            titleText.text = title;
        }

        yield return new WaitForSecondsRealtime(0.3f);

        // Lux得分逐行显示
        yield return AnimateNumber(luxKillsText, 0, data.luxKills, "x{0}");
        yield return new WaitForSecondsRealtime(rowRevealInterval);
        yield return AnimateNumber(luxComboText, 0, data.luxBestCombo, "x{0}");
        yield return new WaitForSecondsRealtime(rowRevealInterval);
        yield return AnimateNumber(luxAbilitiesText, 0, data.luxAbilityUses, "x{0}");
        yield return new WaitForSecondsRealtime(rowRevealInterval);
        yield return AnimateNumber(luxCollectiblesText, 0, data.luxCollectibles, "x{0}");
        yield return new WaitForSecondsRealtime(rowRevealInterval);

        // Lux小计
        yield return AnimateNumber(luxSubtotalText, 0, data.luxSubtotal, "{0}");

        // Lux柱状图
        if (luxScoreBar != null)
        {
            float totalMax = Mathf.Max(data.luxSubtotal + data.noxSubtotal, 1);
            yield return AnimateBar(luxScoreBar, data.luxSubtotal / totalMax);
        }

        yield return new WaitForSecondsRealtime(0.3f);

        // Nox得分逐行显示
        yield return AnimateNumber(noxKillsText, 0, data.noxKills, "x{0}");
        yield return new WaitForSecondsRealtime(rowRevealInterval);
        yield return AnimateNumber(noxComboText, 0, data.noxBestCombo, "x{0}");
        yield return new WaitForSecondsRealtime(rowRevealInterval);
        yield return AnimateNumber(noxAbilitiesText, 0, data.noxAbilityUses, "x{0}");
        yield return new WaitForSecondsRealtime(rowRevealInterval);
        yield return AnimateNumber(noxCollectiblesText, 0, data.noxCollectibles, "x{0}");
        yield return new WaitForSecondsRealtime(rowRevealInterval);

        // Nox小计
        yield return AnimateNumber(noxSubtotalText, 0, data.noxSubtotal, "{0}");

        if (noxScoreBar != null)
        {
            float totalMax = Mathf.Max(data.luxSubtotal + data.noxSubtotal, 1);
            yield return AnimateBar(noxScoreBar, data.noxSubtotal / totalMax);
        }

        yield return new WaitForSecondsRealtime(0.3f);

        // 合作评分
        yield return AnimateNumber(syncActionsText, 0, data.syncActions, "x{0}");
        yield return new WaitForSecondsRealtime(rowRevealInterval);
        yield return AnimateNumber(coopAbilitiesText, 0, data.coopAbilitiesUsed, "x{0}");
        yield return new WaitForSecondsRealtime(rowRevealInterval);
        yield return AnimateNumber(revivesText, 0, data.reviveCount, "x{0}");
        yield return new WaitForSecondsRealtime(rowRevealInterval);
        yield return AnimateNumber(proximityBonusText, 0, data.proximityBonusScore, "+{0}");
        yield return new WaitForSecondsRealtime(rowRevealInterval);

        yield return AnimateNumber(coopSubtotalText, 0, data.coopSubtotal, "{0}");

        yield return new WaitForSecondsRealtime(0.5f);

        // 修改器加成
        if (modifierBonusText != null && data.modifierMultiplier != 1f)
        {
            modifierBonusText.text = $"x{data.modifierMultiplier:F2}";
            modifierBonusText.gameObject.SetActive(true);
        }
        else if (modifierBonusText != null)
        {
            modifierBonusText.gameObject.SetActive(false);
        }

        // 总分（大数字动画）
        yield return AnimateNumber(totalScoreText, 0, data.totalScore, "{0}");

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayConfirm();

        // 星级
        yield return new WaitForSecondsRealtime(starRevealDelay);

        for (int i = 0; i < (starImages?.Length ?? 0); i++)
        {
            if (starImages[i] == null) continue;

            bool earned = i < data.stars;
            starImages[i].sprite = earned ? starFilled : starEmpty;

            if (earned)
            {
                // 弹出动画
                starImages[i].transform.localScale = Vector3.zero;
                float timer = 0;
                while (timer < 0.3f)
                {
                    timer += Time.unscaledDeltaTime;
                    float t = timer / 0.3f;
                    float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
                    starImages[i].transform.localScale = Vector3.one * Mathf.Min(scale, 1.3f - t * 0.3f);
                    yield return null;
                }
                starImages[i].transform.localScale = Vector3.one;

                if (SoundFeedback.Instance != null)
                    SoundFeedback.Instance.Play("star_earn");

                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        // 评级
        yield return new WaitForSecondsRealtime(0.3f);
        if (rankText != null)
        {
            rankText.text = data.rank;
            rankText.color = GetRankColor(data.rank);

            // 缩放弹入
            rankText.transform.localScale = Vector3.one * 3f;
            float rankTimer = 0;
            while (rankTimer < 0.4f)
            {
                rankTimer += Time.unscaledDeltaTime;
                float t = rankTimer / 0.4f;
                float scale = Mathf.Lerp(3f, 1f, t * t);
                rankText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            rankText.transform.localScale = Vector3.one;

            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play("rank_reveal");
        }
    }

    private IEnumerator AnimateNumber(TextMeshProUGUI text, int from, int to, string format)
    {
        if (text == null) yield break;

        float elapsed = 0;
        while (elapsed < numberCountDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / numberCountDuration;
            t = t * t; // 加速曲线

            int current = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
            text.text = string.Format(format, current);
            yield return null;
        }

        text.text = string.Format(format, to);
    }

    private IEnumerator AnimateBar(Image bar, float targetFill)
    {
        if (bar == null) yield break;

        float elapsed = 0;
        float duration = 0.5f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            bar.fillAmount = Mathf.Lerp(0, targetFill, elapsed / duration);
            yield return null;
        }
        bar.fillAmount = targetFill;
    }

    // ==================== 辅助 ====================

    private Color GetRankColor(string rank)
    {
        return rank switch
        {
            "S" => new Color(1f, 0.85f, 0.2f),    // 金色
            "A" => new Color(0.4f, 0.9f, 0.4f),    // 绿色
            "B" => new Color(0.4f, 0.6f, 1f),      // 蓝色
            "C" => Color.white,
            _ => new Color(0.7f, 0.7f, 0.7f)       // 灰色
        };
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

/// <summary>
/// 合作评分数据 - 由LevelResultCalculator或LevelBootstrap构建
/// </summary>
[System.Serializable]
public class CoopScoreData
{
    // Lux统计
    public int luxKills;
    public int luxBestCombo;
    public int luxAbilityUses;
    public int luxCollectibles;
    public int luxSubtotal;

    // Nox统计
    public int noxKills;
    public int noxBestCombo;
    public int noxAbilityUses;
    public int noxCollectibles;
    public int noxSubtotal;

    // 合作统计
    public int syncActions;
    public int coopAbilitiesUsed;
    public int reviveCount;
    public int proximityBonusScore;
    public int coopSubtotal;

    // 总结
    public int totalScore;
    public float modifierMultiplier;
    public int stars;
    public string rank;
    public float levelTime;
}
