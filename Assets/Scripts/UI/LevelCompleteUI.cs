using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class LevelCompleteUI : MonoBehaviour
{
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
            else if (GameFlowManager.Instance != null)
                levelNameText.text = $"关卡 {GameFlowManager.Instance.CurrentChapter}-{GameFlowManager.Instance.CurrentLevel} 完成！";
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

        // 优先用GameFlowManager的当前进度（LevelData SO可能未赋值）
        int curChapter = 1, curLevel = 1;
        if (GameFlowManager.Instance != null)
        {
            curChapter = GameFlowManager.Instance.CurrentChapter;
            curLevel = GameFlowManager.Instance.CurrentLevel;
        }
        else if (LevelManager.Instance?.CurrentLevel != null)
        {
            curChapter = LevelManager.Instance.CurrentLevel.chapter;
            curLevel = LevelManager.Instance.CurrentLevel.levelIndex;
        }

        int nextLevel = curLevel + 1;
        int nextChapter = curChapter;

        int[] levelsPerChapter = { 4, 4, 4, 4, 4 };
        int maxInChapter = nextChapter <= levelsPerChapter.Length ? levelsPerChapter[nextChapter - 1] : 4;

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
        int ch = GameFlowManager.Instance?.CurrentChapter ?? 1;
        int lv = GameFlowManager.Instance?.CurrentLevel ?? 1;
        GameManager.Instance?.LoadLevel(ch, lv);
    }

    private void OnMenu()
    {
        Time.timeScale = 1f;
        GameManager.Instance?.ReturnToMainMenu();
    }
}
