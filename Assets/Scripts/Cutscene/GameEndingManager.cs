using UnityEngine;
using System.Collections;

/// <summary>
/// 游戏结局管理器 - 处理通关后的结局演出
/// 根据完成度（星数、隐藏区域、合作评分）决定结局类型
/// 普通结局、好结局、完美结局三种
/// </summary>
public class GameEndingManager : MonoBehaviour
{
    public static GameEndingManager Instance { get; private set; }

    [Header("结局配置")]
    [SerializeField] private EndingConfig normalEnding;
    [SerializeField] private EndingConfig goodEnding;
    [SerializeField] private EndingConfig perfectEnding;

    [Header("结局条件")]
    [SerializeField] private float goodEndingCompletion = 70f;     // 好结局需要的完成度%
    [SerializeField] private int goodEndingStars = 40;             // 好结局需要的星数
    [SerializeField] private float perfectEndingCompletion = 95f;  // 完美结局需要的完成度%
    [SerializeField] private int perfectEndingStars = 55;          // 完美结局需要的星数
    [SerializeField] private int perfectEndingSecrets = 15;        // 完美结局需要的隐藏数
    [SerializeField] private int perfectEndingBondLevel = 4;     // 完美结局需要的羁绊等级

    [Header("制作人员")]
    [SerializeField] private float creditsDelay = 3f;

    [System.Serializable]
    public class EndingConfig
    {
        public string endingId;
        public string storyKey;              // 本地化key
        public string fallbackDialogue;      // 默认文本
        public AudioClip endingMusic;
        public Sprite endingIllustration;
        public float illustrationDuration = 5f;
        public string achievementId;         // 解锁的成就
    }

    public enum EndingType
    {
        Normal,
        Good,
        Perfect
    }

    public event System.Action<EndingType> OnEndingPlayed;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 判断并播放结局
    /// </summary>
    public void PlayEnding()
    {
        EndingType type = DetermineEnding();
        StartCoroutine(EndingSequence(type));
    }

    /// <summary>
    /// 获取当前达成的结局类型（不播放）
    /// </summary>
    public EndingType DetermineEnding()
    {
        float completion = WorldProgressionManager.Instance?.GetOverallCompletion() ?? 0;
        int totalStars = WorldProgressionManager.Instance?.GetTotalStarsEarned() ?? 0;
        int secrets = SecretAreaSystem.Instance?.GetTotalDiscovered() ?? 0;
        int bondLevel = PlayerBondSystem.Instance?.CurrentBondLevel ?? 0;

        if (completion >= perfectEndingCompletion &&
            totalStars >= perfectEndingStars &&
            secrets >= perfectEndingSecrets &&
            bondLevel >= perfectEndingBondLevel)
        {
            return EndingType.Perfect;
        }

        if (completion >= goodEndingCompletion && totalStars >= goodEndingStars)
        {
            return EndingType.Good;
        }

        return EndingType.Normal;
    }

    /// <summary>
    /// 检查是否已通关（最后一章Boss击败）
    /// </summary>
    public bool HasCompletedGame()
    {
        return WorldProgressionManager.Instance?.IsBossDefeated(5) ?? false;
    }

    // ==================== 结局序列 ====================

    private IEnumerator EndingSequence(EndingType type)
    {
        var config = GetConfig(type);
        if (config == null) yield break;

        Debug.Log($"[Ending] Playing {type} ending");

        // 1. 淡出游戏画面（通过AudioManager淡出BGM作为过渡）
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopBGM();

        // 2. 播放结局音乐
        if (config.endingMusic != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(config.endingMusic);

        // 3. 显示结局插画
        yield return new WaitForSecondsRealtime(1f);

        // 淡入音乐
        yield return new WaitForSecondsRealtime(0.5f);

        // 4. 播放结局对话
        if (DialogueSystem.Instance != null)
        {
            string text = config.fallbackDialogue;
            if (LocalizationSystem.Instance != null)
            {
                string localized = LocalizationSystem.Instance.GetText(config.storyKey);
                if (localized != config.storyKey) text = localized;
            }

            // 构建对话行
            var lines = new System.Collections.Generic.List<DialogueLine>();
            string[] paragraphs = text.Split('\n');
            foreach (var para in paragraphs)
            {
                string trimmed = para.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    lines.Add(new DialogueLine
                    {
                        speakerName = "",
                        text = trimmed,
                        delay = 0.5f
                    });
                }
            }

            bool dialogueDone = false;
            DialogueSystem.Instance.OnDialogueEnd += () => dialogueDone = true;
            DialogueSystem.Instance.StartDialogue(lines, false);

            while (!dialogueDone)
                yield return null;
        }

        yield return new WaitForSecondsRealtime(config.illustrationDuration);

        // 5. 解锁成就
        if (!string.IsNullOrEmpty(config.achievementId) && AchievementSystem.Instance != null)
            AchievementSystem.Instance.Unlock(config.achievementId);

        // 6. 保存结局记录
        SaveEndingReached(type);

        // 7. 通知
        OnEndingPlayed?.Invoke(type);

        // 8. 制作人员名单
        yield return new WaitForSecondsRealtime(creditsDelay);

        // 通知游戏流程进入制作人员画面
        if (GameFlowManager.Instance != null)
        {
            // Credits状态由GameFlowManager管理，UI层订阅状态变化自动显示CreditsUI
            GameFlowManager.Instance.PlayCutscene();
        }
    }

    // ==================== 辅助 ====================

    private EndingConfig GetConfig(EndingType type)
    {
        return type switch
        {
            EndingType.Normal => normalEnding,
            EndingType.Good => goodEnding,
            EndingType.Perfect => perfectEnding,
            _ => normalEnding
        };
    }

    private void SaveEndingReached(EndingType type)
    {
        int best = PlayerPrefs.GetInt("best_ending", 0);
        int current = (int)type;
        if (current > best)
        {
            PlayerPrefs.SetInt("best_ending", current);
            PlayerPrefs.Save();
        }
    }

    private void InitializeDefaults()
    {
        if (normalEnding == null)
        {
            normalEnding = new EndingConfig
            {
                endingId = "normal",
                storyKey = "ending_normal",
                fallbackDialogue = "Lux和Nox终于击败了黄昏之王，但世界的光暗裂隙仍未完全修复...\n\n两人望向远方，知道还有更多秘密等待他们去发现。",
                achievementId = "game_complete"
            };
        }

        if (goodEnding == null)
        {
            goodEnding = new EndingConfig
            {
                endingId = "good",
                storyKey = "ending_good",
                fallbackDialogue = "在漫长的旅途中，Lux和Nox的羁绊越来越深。\n\n他们不仅击败了黄昏之王，还修复了大部分光暗裂隙。世界重新恢复了平衡。",
                achievementId = "good_ending"
            };
        }

        if (perfectEnding == null)
        {
            perfectEnding = new EndingConfig
            {
                endingId = "perfect",
                storyKey = "ending_perfect",
                fallbackDialogue = "光与暗，不再是对立的存在。\n\nLux和Nox携手走过了每一个角落，解开了每一个谜题，发现了每一个秘密。\n\n他们的旅途不仅修复了世界，更证明了——\n只要携手同行，任何前方都不再是终点。\n\n双向前行。",
                achievementId = "perfect_ending"
            };
        }
    }
}
