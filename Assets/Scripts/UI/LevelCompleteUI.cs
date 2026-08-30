using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class LevelCompleteUI : MonoBehaviour
{
    /// <summary>
    /// 解析"当前是第几章第几关"。三个来源按可靠性排序:
    ///   1. LevelManager 持有的 LevelData
    ///   2. GameFlowManager 的进度(走主菜单进关卡时才有)
    ///   3. 活动场景名 Level_章_关
    ///
    /// 第3条不是兜底摆设: 在编辑器里直接对某个关卡场景按 Play(开发时最常用的
    /// 玩法)时,前两者从没被赋值过,读出来是 0 —— 面板会显示"关卡 0-0 完成",
    /// 点"下一关"则以 levelsPerChapter[0 - 1] 直接抛 IndexOutOfRangeException。
    /// </summary>
    private static void ResolveCurrentLevel(out int chapter, out int level)
    {
        chapter = 0; level = 0;

        var data = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;
        if (data != null) { chapter = data.chapter; level = data.levelIndex; }

        if ((chapter <= 0 || level <= 0) && GameFlowManager.Instance != null)
        {
            chapter = GameFlowManager.Instance.CurrentChapter;
            level = GameFlowManager.Instance.CurrentLevel;
        }

        if (chapter <= 0 || level <= 0)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                SceneManager.GetActiveScene().name, @"Level_(\d+)_(\d+)");
            if (m.Success)
            {
                chapter = int.Parse(m.Groups[1].Value);
                level = int.Parse(m.Groups[2].Value);
            }
        }

        if (chapter <= 0) chapter = 1;
        if (level <= 0) level = 1;
    }

    [SerializeField] private GameObject completePanel;
    [SerializeField] private TextMeshProUGUI levelNameText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI collectibleText;
    [SerializeField] private Image[] stars;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private CanvasGroup canvasGroup;

    [SerializeField] private AudioClip completeSound;
    [SerializeField] private AudioClip starSound;

    void Start()
    {
        if (completePanel != null) completePanel.SetActive(false);

        nextLevelButton?.onClick.AddListener(OnNextLevel);
        replayButton?.onClick.AddListener(OnReplay);
        menuButton?.onClick.AddListener(OnMenu);

        var lm = LevelManager.Instance;
        if (lm != null)
            lm.OnLevelComplete += ShowCompleteScreen;
    }

    private void ShowCompleteScreen()
    {
        if (completePanel != null) completePanel.SetActive(true);

        var lm = LevelManager.Instance;
        if (lm == null) return;

        if (levelNameText != null)
        {
            if (lm.CurrentLevel != null)
                levelNameText.text = lm.CurrentLevel.DisplayName;
            else
            {
                ResolveCurrentLevel(out int ch, out int lv);
                levelNameText.text = $"关卡 {ch}-{lv} 完成！";
            }
        }

        float time = lm.GetLevelTime();
        if (timeText != null)
            timeText.text = $"{(int)(time / 60):00}:{(int)(time % 60):00}";

        if (collectibleText != null)
            collectibleText.text = $"{lm.GetCollectiblesGathered()} / {lm.GetTotalCollectibles()}";

        if (completeSound != null)
            AudioManager.Instance?.PlaySFX(completeSound);

        StartCoroutine(AnimateStars(lm));
    }

    private IEnumerator AnimateStars(LevelManager lm)
    {
        int starCount = CalculateStars(lm);

        if (stars == null) yield break;

        foreach (var star in stars)
        {
            if (star != null)
            {
                star.color = Color.gray;
                star.transform.localScale = Vector3.zero;
            }
        }

        yield return new WaitForSecondsRealtime(0.5f);

        for (int i = 0; i < starCount && i < stars.Length; i++)
        {
            if (stars[i] == null) continue;

            stars[i].color = Color.yellow;

            float t = 0;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                float scale = Mathf.Lerp(0, 1.2f, t / 0.3f);
                stars[i].transform.localScale = Vector3.one * scale;
                yield return null;
            }
            stars[i].transform.localScale = Vector3.one;

            if (starSound != null)
                AudioManager.Instance?.PlaySFX(starSound);

            yield return new WaitForSecondsRealtime(0.3f);
        }
    }

    private int CalculateStars(LevelManager lm)
    {
        int starCount = 1; // 通关至少一星

        // 收集全部收集品：+1 星
        if (lm.GetCollectiblesGathered() >= lm.GetTotalCollectibles())
            starCount++;

        // 在标准时间内完成：+1 星
        if (lm.CurrentLevel != null && lm.GetLevelTime() <= lm.CurrentLevel.parTime)
            starCount++;

        return starCount;
    }

    private void OnNextLevel()
    {
        Time.timeScale = 1f;

        ResolveCurrentLevel(out int curChapter, out int curLevel);

        int nextLevel = curLevel + 1;
        int nextChapter = curChapter;

        int[] levelsPerChapter = { 4, 4, 4, 4, 4 };
        // 索引必须夹住。原来只判了上界,章节号为0时就是 levelsPerChapter[-1]
        int maxInChapter = levelsPerChapter[Mathf.Clamp(nextChapter, 1, levelsPerChapter.Length) - 1];

        if (nextLevel > maxInChapter)
        {
            nextChapter++;
            nextLevel = 1;
        }

        if (nextChapter <= 5)
            GameManager.Instance?.LoadLevel(nextChapter, nextLevel);
        else
            GameManager.Instance?.ReturnToMainMenu();
    }

    private void OnReplay()
    {
        Time.timeScale = 1f;
        ResolveCurrentLevel(out int ch, out int lv);
        GameManager.Instance?.LoadLevel(ch, lv);
    }

    private void OnMenu()
    {
        Time.timeScale = 1f;
        GameManager.Instance?.ReturnToMainMenu();
    }
}
