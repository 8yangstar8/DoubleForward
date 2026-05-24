using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 章节剧情数据 - ScriptableObject定义每章的剧情内容
/// 包含开场动画脚本、过渡对话、结尾过场
/// </summary>
[CreateAssetMenu(fileName = "ChapterStory", menuName = "DoubleForward/Chapter Story Data")]
public class ChapterStoryData : ScriptableObject
{
    [Header("章节信息")]
    public int chapterIndex;             // 1-5
    public string chapterTitleKey;       // 本地化key: "chapter_1_title"
    public string chapterSubtitleKey;    // 本地化key: "chapter_1_subtitle"
    public Color themeColor = Color.white;
    public Sprite chapterArt;            // 章节封面图

    [Header("开场")]
    public OpeningSequence opening;

    [Header("关卡间过渡对话")]
    public List<LevelTransitionDialogue> transitions = new List<LevelTransitionDialogue>();

    [Header("章节结尾")]
    public EndingSequence ending;

    [Header("Boss战前对话")]
    public DialogueSystem.DialogueSequence bossIntroDialogue;

    [Header("Boss战后对话")]
    public DialogueSystem.DialogueSequence bossDefeatDialogue;

    // ============ 开场序列 ============

    [System.Serializable]
    public class OpeningSequence
    {
        public List<StorySlide> slides = new List<StorySlide>();
        public AudioClip openingBGM;
        public float slideDuration = 4f;         // 每张幻灯片默认时长
        public float fadeInDuration = 0.8f;
        public float fadeOutDuration = 0.5f;
        public bool allowSkip = true;
    }

    [System.Serializable]
    public class StorySlide
    {
        public Sprite illustration;               // 插画
        [TextArea(2, 5)]
        public string narrativeKey;               // 本地化key，旁白文字
        public AudioClip narrationClip;           // 语音旁白（可选）
        public float customDuration = -1f;        // -1表示用默认值
        public SlideTransition transition = SlideTransition.CrossFade;
        public CameraMovement cameraMove = CameraMovement.None;
    }

    public enum SlideTransition
    {
        CrossFade,          // 交叉淡入淡出
        SlideLeft,          // 向左滑入
        SlideRight,         // 向右滑入
        ZoomIn,             // 缩放进入
        Dissolve            // 溶解
    }

    public enum CameraMovement
    {
        None,
        PanLeft,            // 慢速左移
        PanRight,           // 慢速右移
        PanUp,              // 慢速上移
        ZoomInSlow,         // 缓慢放大
        ZoomOutSlow         // 缓慢缩小
    }

    // ============ 关卡过渡 ============

    [System.Serializable]
    public class LevelTransitionDialogue
    {
        public int afterLevel;                    // 哪关之后播放（1-based）
        public DialogueSystem.DialogueSequence dialogue;
        public bool showOnlyOnce = true;          // 只播放一次
    }

    // ============ 结尾序列 ============

    [System.Serializable]
    public class EndingSequence
    {
        public List<StorySlide> slides = new List<StorySlide>();
        public AudioClip endingBGM;
        public bool unlockNextChapter = true;
        public string unlockRewardKey;            // 解锁奖励描述key
    }
}

/// <summary>
/// 章节剧情管理器 - 控制剧情播放流程
/// </summary>
public class ChapterStoryManager : MonoBehaviour
{
    public static ChapterStoryManager Instance { get; private set; }

    [Header("剧情数据")]
    [SerializeField] private ChapterStoryData[] chapterStories; // 索引0-4对应第1-5章

    [Header("UI引用")]
    [SerializeField] private CanvasGroup storyCanvas;
    [SerializeField] private UnityEngine.UI.Image slideImage;
    [SerializeField] private TMPro.TextMeshProUGUI narrativeText;
    [SerializeField] private UnityEngine.UI.Button skipButton;
    [SerializeField] private CanvasGroup skipButtonCanvas;

    [Header("章节标题")]
    [SerializeField] private CanvasGroup titleCanvas;
    [SerializeField] private TMPro.TextMeshProUGUI chapterTitleText;
    [SerializeField] private TMPro.TextMeshProUGUI chapterSubtitleText;
    [SerializeField] private UnityEngine.UI.Image chapterArtImage;

    [Header("打字机")]
    [SerializeField] private float typewriterSpeed = 0.04f;

    public bool IsPlaying { get; private set; }

    private bool skipRequested;
    private const string SEEN_PREFIX = "story_seen_";

    public event System.Action OnStoryComplete;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (skipButton != null)
            skipButton.onClick.AddListener(RequestSkip);

        HideAll();
    }

    /// <summary>
    /// 获取章节剧情数据
    /// </summary>
    public ChapterStoryData GetChapterStory(int chapter)
    {
        int index = chapter - 1;
        if (index >= 0 && index < chapterStories.Length)
            return chapterStories[index];
        return null;
    }

    /// <summary>
    /// 播放章节开场
    /// </summary>
    public void PlayChapterOpening(int chapter, System.Action onComplete = null)
    {
        var story = GetChapterStory(chapter);
        if (story == null || story.opening == null || story.opening.slides.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(PlayOpeningSequence(story, onComplete));
    }

    /// <summary>
    /// 播放章节标题卡
    /// </summary>
    public void PlayChapterTitleCard(int chapter, System.Action onComplete = null)
    {
        var story = GetChapterStory(chapter);
        if (story == null)
        {
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(ShowTitleCard(story, onComplete));
    }

    /// <summary>
    /// 播放关卡过渡对话
    /// </summary>
    public void PlayLevelTransition(int chapter, int completedLevel, System.Action onComplete = null)
    {
        var story = GetChapterStory(chapter);
        if (story == null)
        {
            onComplete?.Invoke();
            return;
        }

        // 查找对应的过渡对话
        LevelTransitionDialogue transition = null;
        foreach (var t in story.transitions)
        {
            if (t.afterLevel == completedLevel)
            {
                transition = t;
                break;
            }
        }

        if (transition == null || transition.dialogue == null)
        {
            onComplete?.Invoke();
            return;
        }

        // 检查是否只播放一次
        string seenKey = SEEN_PREFIX + $"trans_{chapter}_{completedLevel}";
        if (transition.showOnlyOnce && PlayerPrefs.GetInt(seenKey, 0) == 1)
        {
            onComplete?.Invoke();
            return;
        }

        // 标记已看
        PlayerPrefs.SetInt(seenKey, 1);
        PlayerPrefs.Save();

        // 播放对话
        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.OnDialogueEnd += OnTransitionDialogueEnd;
            DialogueSystem.Instance.StartDialogue(transition.dialogue);

            void OnTransitionDialogueEnd()
            {
                DialogueSystem.Instance.OnDialogueEnd -= OnTransitionDialogueEnd;
                onComplete?.Invoke();
            }
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 播放章节结尾
    /// </summary>
    public void PlayChapterEnding(int chapter, System.Action onComplete = null)
    {
        var story = GetChapterStory(chapter);
        if (story == null || story.ending == null || story.ending.slides.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(PlayEndingSequence(story, onComplete));
    }

    /// <summary>
    /// 播放Boss战前对话
    /// </summary>
    public void PlayBossIntro(int chapter, System.Action onComplete = null)
    {
        var story = GetChapterStory(chapter);
        if (story == null || story.bossIntroDialogue == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.OnDialogueEnd += OnBossIntroEnd;
            DialogueSystem.Instance.StartDialogue(story.bossIntroDialogue);

            void OnBossIntroEnd()
            {
                DialogueSystem.Instance.OnDialogueEnd -= OnBossIntroEnd;
                onComplete?.Invoke();
            }
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 播放Boss击败对话
    /// </summary>
    public void PlayBossDefeat(int chapter, System.Action onComplete = null)
    {
        var story = GetChapterStory(chapter);
        if (story == null || story.bossDefeatDialogue == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.OnDialogueEnd += OnBossDefeatEnd;
            DialogueSystem.Instance.StartDialogue(story.bossDefeatDialogue);

            void OnBossDefeatEnd()
            {
                DialogueSystem.Instance.OnDialogueEnd -= OnBossDefeatEnd;
                onComplete?.Invoke();
            }
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    // ============ 内部实现 ============

    private System.Collections.IEnumerator PlayOpeningSequence(ChapterStoryData story, System.Action onComplete)
    {
        IsPlaying = true;
        skipRequested = false;

        var opening = story.opening;

        // 播放BGM
        if (opening.openingBGM != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(opening.openingBGM);

        // 显示故事画布
        if (storyCanvas != null)
        {
            storyCanvas.gameObject.SetActive(true);
            yield return FadeCanvasGroup(storyCanvas, 0f, 1f, opening.fadeInDuration);
        }

        // 显示跳过按钮
        if (opening.allowSkip && skipButtonCanvas != null)
        {
            skipButtonCanvas.gameObject.SetActive(true);
            skipButtonCanvas.alpha = 1f;
        }

        // 播放每张幻灯片
        foreach (var slide in opening.slides)
        {
            if (skipRequested) break;

            yield return PlaySlide(slide, opening.slideDuration);
        }

        // 淡出
        if (storyCanvas != null)
            yield return FadeCanvasGroup(storyCanvas, 1f, 0f, opening.fadeOutDuration);

        // 显示章节标题卡
        yield return ShowTitleCard(story, null);

        HideAll();
        IsPlaying = false;
        OnStoryComplete?.Invoke();
        onComplete?.Invoke();
    }

    private System.Collections.IEnumerator PlayEndingSequence(ChapterStoryData story, System.Action onComplete)
    {
        IsPlaying = true;
        skipRequested = false;

        var ending = story.ending;

        // 播放BGM
        if (ending.endingBGM != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(ending.endingBGM);

        // 显示画布
        if (storyCanvas != null)
        {
            storyCanvas.gameObject.SetActive(true);
            yield return FadeCanvasGroup(storyCanvas, 0f, 1f, 0.8f);
        }

        if (skipButtonCanvas != null)
        {
            skipButtonCanvas.gameObject.SetActive(true);
            skipButtonCanvas.alpha = 1f;
        }

        // 播放幻灯片
        foreach (var slide in ending.slides)
        {
            if (skipRequested) break;
            yield return PlaySlide(slide, 4f);
        }

        // 淡出
        if (storyCanvas != null)
            yield return FadeCanvasGroup(storyCanvas, 1f, 0f, 0.5f);

        HideAll();
        IsPlaying = false;
        OnStoryComplete?.Invoke();
        onComplete?.Invoke();
    }

    private System.Collections.IEnumerator ShowTitleCard(ChapterStoryData story, System.Action onComplete)
    {
        if (titleCanvas == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        // 设置标题内容
        string title = story.chapterTitleKey;
        string subtitle = story.chapterSubtitleKey;

        if (LocalizationSystem.Instance != null)
        {
            title = LocalizationSystem.Instance.Get(story.chapterTitleKey, $"Chapter {story.chapterIndex}");
            subtitle = LocalizationSystem.Instance.Get(story.chapterSubtitleKey, "");
        }

        if (chapterTitleText != null)
        {
            chapterTitleText.text = title;
            chapterTitleText.color = story.themeColor;
        }

        if (chapterSubtitleText != null)
            chapterSubtitleText.text = subtitle;

        if (chapterArtImage != null && story.chapterArt != null)
        {
            chapterArtImage.sprite = story.chapterArt;
            chapterArtImage.gameObject.SetActive(true);
        }

        // 淡入
        titleCanvas.gameObject.SetActive(true);
        yield return FadeCanvasGroup(titleCanvas, 0f, 1f, 0.6f);

        // 停留
        yield return new WaitForSecondsRealtime(2.5f);

        // 淡出
        yield return FadeCanvasGroup(titleCanvas, 1f, 0f, 0.4f);
        titleCanvas.gameObject.SetActive(false);

        onComplete?.Invoke();
    }

    private System.Collections.IEnumerator PlaySlide(ChapterStoryData.StorySlide slide, float defaultDuration)
    {
        float duration = slide.customDuration > 0 ? slide.customDuration : defaultDuration;

        // 设置插图
        if (slideImage != null && slide.illustration != null)
        {
            slideImage.sprite = slide.illustration;
            slideImage.gameObject.SetActive(true);
        }

        // 播放语音
        if (slide.narrationClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(slide.narrationClip);

        // 打字机显示旁白
        string narrative = slide.narrativeKey;
        if (LocalizationSystem.Instance != null)
            narrative = LocalizationSystem.Instance.Get(slide.narrativeKey, slide.narrativeKey);

        if (narrativeText != null)
        {
            yield return TypewriterNarrative(narrative);
        }

        // 相机运动模拟（通过移动图片实现）
        if (slide.cameraMove != ChapterStoryData.CameraMovement.None && slideImage != null)
        {
            StartCoroutine(AnimateSlide(slideImage.rectTransform, slide.cameraMove, duration));
        }

        // 等待剩余时长
        float waitTime = duration - (narrative.Length * typewriterSpeed);
        if (waitTime > 0)
        {
            float elapsed = 0;
            while (elapsed < waitTime && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // 清除文字
        if (narrativeText != null)
            narrativeText.text = "";
    }

    private System.Collections.IEnumerator TypewriterNarrative(string text)
    {
        if (narrativeText == null) yield break;

        narrativeText.text = "";
        foreach (char c in text)
        {
            if (skipRequested)
            {
                narrativeText.text = text;
                yield break;
            }

            narrativeText.text += c;
            if (c != ' ')
                yield return new WaitForSecondsRealtime(typewriterSpeed);
        }
    }

    private System.Collections.IEnumerator AnimateSlide(RectTransform rt, ChapterStoryData.CameraMovement move, float duration)
    {
        Vector2 startPos = rt.anchoredPosition;
        Vector3 startScale = rt.localScale;
        float moveAmount = 30f;
        float zoomAmount = 0.1f;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            switch (move)
            {
                case ChapterStoryData.CameraMovement.PanLeft:
                    rt.anchoredPosition = startPos + Vector2.left * moveAmount * t;
                    break;
                case ChapterStoryData.CameraMovement.PanRight:
                    rt.anchoredPosition = startPos + Vector2.right * moveAmount * t;
                    break;
                case ChapterStoryData.CameraMovement.PanUp:
                    rt.anchoredPosition = startPos + Vector2.up * moveAmount * t;
                    break;
                case ChapterStoryData.CameraMovement.ZoomInSlow:
                    rt.localScale = startScale * (1f + zoomAmount * t);
                    break;
                case ChapterStoryData.CameraMovement.ZoomOutSlow:
                    rt.localScale = startScale * (1f - zoomAmount * 0.5f * t);
                    break;
            }

            yield return null;
        }

        // 复位
        rt.anchoredPosition = startPos;
        rt.localScale = startScale;
    }

    private System.Collections.IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    private void RequestSkip()
    {
        skipRequested = true;
    }

    private void HideAll()
    {
        if (storyCanvas != null) storyCanvas.gameObject.SetActive(false);
        if (titleCanvas != null) titleCanvas.gameObject.SetActive(false);
        if (skipButtonCanvas != null) skipButtonCanvas.gameObject.SetActive(false);
    }

    /// <summary>
    /// 检查某章节开场是否已看过
    /// </summary>
    public bool HasSeenOpening(int chapter)
    {
        return PlayerPrefs.GetInt(SEEN_PREFIX + $"opening_{chapter}", 0) == 1;
    }

    /// <summary>
    /// 标记开场已看
    /// </summary>
    public void MarkOpeningSeen(int chapter)
    {
        PlayerPrefs.SetInt(SEEN_PREFIX + $"opening_{chapter}", 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 重置所有剧情观看记录（用于新存档）
    /// </summary>
    public void ResetAllSeen()
    {
        for (int ch = 1; ch <= 5; ch++)
        {
            PlayerPrefs.DeleteKey(SEEN_PREFIX + $"opening_{ch}");
            for (int lv = 1; lv <= 5; lv++)
            {
                PlayerPrefs.DeleteKey(SEEN_PREFIX + $"trans_{ch}_{lv}");
            }
        }
        PlayerPrefs.Save();
    }
}
