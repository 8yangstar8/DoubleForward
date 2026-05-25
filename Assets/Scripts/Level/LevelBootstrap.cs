using UnityEngine;
using System.Collections;

/// <summary>
/// 关卡启动引导 - 每个关卡场景中放置一个
/// 负责关卡的完整初始化流程：检查系统→剧情→标题卡→教程→开始
/// </summary>
public class LevelBootstrap : MonoBehaviour
{
    [Header("关卡信息")]
    [SerializeField] private int chapter = 1;
    [SerializeField] private int level = 1;
    [SerializeField] private bool isFirstLevelInChapter = false;
    [SerializeField] private bool isBossLevel = false;

    [Header("玩家生成")]
    [SerializeField] private Transform luxSpawnPoint;
    [SerializeField] private Transform noxSpawnPoint;
    [SerializeField] private GameObject luxPrefab;
    [SerializeField] private GameObject noxPrefab;

    [Header("可选系统")]
    [SerializeField] private BossArena bossArena;
    [SerializeField] private bool enableMiniMap = true;
    [SerializeField] private bool enableComboTracking = true;

    [Header("BGM")]
    [SerializeField] private AudioClip levelBGM;
    [SerializeField] private AudioClip ambientSound;

    private PlayerController luxPlayer;
    private PlayerController noxPlayer;

    void Start()
    {
        StartCoroutine(BootSequence());
    }

    private IEnumerator BootSequence()
    {
        // 等待GameInitializer完成
        while (!GameInitializer.IsReady)
            yield return null;

        Debug.Log($"[LevelBoot] Starting Ch.{chapter} Lv.{level}");

        // ====== 1. 生成玩家 ======
        SpawnPlayers();
        yield return null;

        // ====== 2. 注册到复活系统 ======
        if (RespawnSystem.Instance != null)
        {
            if (luxPlayer != null)
            {
                var luxHealth = luxPlayer.GetComponent<PlayerHealth>();
                if (luxHealth != null)
                    RespawnSystem.Instance.RegisterPlayer(0, luxHealth, luxPlayer,
                        luxSpawnPoint != null ? luxSpawnPoint.position : Vector3.zero);
            }

            if (noxPlayer != null)
            {
                var noxHealth = noxPlayer.GetComponent<PlayerHealth>();
                if (noxHealth != null)
                    RespawnSystem.Instance.RegisterPlayer(1, noxHealth, noxPlayer,
                        noxSpawnPoint != null ? noxSpawnPoint.position : Vector3.zero);
            }
        }

        // ====== 3. 初始化合作系统 ======
        if (CoopAbilitySystem.Instance != null)
            CoopAbilitySystem.Instance.Initialize(luxPlayer, noxPlayer);

        // ====== 4. 播放音乐 ======
        if (levelBGM != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(levelBGM);
        if (ambientSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayAmbient(ambientSound);

        // ====== 5. 章节开场（首关时） ======
        if (isFirstLevelInChapter && ChapterStoryManager.Instance != null)
        {
            if (!ChapterStoryManager.Instance.HasSeenOpening(chapter))
            {
                bool storyDone = false;
                ChapterStoryManager.Instance.PlayChapterOpening(chapter, () => storyDone = true);

                while (!storyDone)
                    yield return null;

                ChapterStoryManager.Instance.MarkOpeningSeen(chapter);
            }
            else
            {
                // 只播放标题卡
                bool titleDone = false;
                ChapterStoryManager.Instance.PlayChapterTitleCard(chapter, () => titleDone = true);

                while (!titleDone)
                    yield return null;
            }
        }

        // ====== 6. 启动追踪 ======
        if (enableComboTracking && ComboSystem.Instance != null)
            ComboSystem.Instance.StartTracking();

        if (AnalyticsTracker.Instance != null)
            AnalyticsTracker.Instance.TrackLevelStart(chapter, level);

        // ====== 7. 通知流程管理器 ======
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.OnLevelReady();

        // ====== 8. Boss关特殊处理 ======
        if (isBossLevel)
        {
            // Boss战前对话
            if (ChapterStoryManager.Instance != null)
            {
                bool bossIntroDone = false;
                ChapterStoryManager.Instance.PlayBossIntro(chapter, () => bossIntroDone = true);

                while (!bossIntroDone)
                    yield return null;
            }

            if (GameFlowManager.Instance != null)
                GameFlowManager.Instance.EnterBossBattle();

            if (CameraEffects.Instance != null)
                CameraEffects.Instance.SetBossAtmosphere(true);

            if (AudioMixerSetup.Instance != null)
                AudioMixerSetup.Instance.TransitionToBoss();
        }

        Debug.Log($"[LevelBoot] Ch.{chapter} Lv.{level} fully initialized");
    }

    private void SpawnPlayers()
    {
        Vector3 luxPos = luxSpawnPoint != null ? luxSpawnPoint.position : new Vector3(-2, 0, 0);
        Vector3 noxPos = noxSpawnPoint != null ? noxSpawnPoint.position : new Vector3(2, 0, 0);

        if (luxPrefab != null)
        {
            var luxObj = Instantiate(luxPrefab, luxPos, Quaternion.identity);
            luxObj.name = "Lux";
            luxPlayer = luxObj.GetComponent<PlayerController>();
        }

        if (noxPrefab != null)
        {
            var noxObj = Instantiate(noxPrefab, noxPos, Quaternion.identity);
            noxObj.name = "Nox";
            noxPlayer = noxObj.GetComponent<PlayerController>();
        }

        // 注册到LevelManager供其他系统引用
        if (LevelManager.Instance != null)
            LevelManager.Instance.RegisterPlayers(luxPlayer, noxPlayer);
    }

    /// <summary>
    /// 关卡完成（由LevelGoalTrigger调用）
    /// </summary>
    public void OnLevelCompleted(float time, int collectibles, int totalCollectibles)
    {
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.StopTracking();

        // 计算结果
        ComboSystem.LevelResult result = null;
        if (ComboSystem.Instance != null)
            result = ComboSystem.Instance.CalculateLevelResult(120f, totalCollectibles, collectibles);

        int stars = result?.stars ?? 1;

        // 发布完成事件
        EventBus.Publish(new LevelCompleteEvent
        {
            chapter = chapter,
            level = level,
            stars = stars,
            time = time,
            collectibles = collectibles
        });

        // Boss击败对话
        if (isBossLevel && ChapterStoryManager.Instance != null)
        {
            ChapterStoryManager.Instance.PlayBossDefeat(chapter, () =>
            {
                // Boss击败事件
                EventBus.Publish(new BossDefeatedEvent
                {
                    bossName = $"Chapter{chapter}Boss",
                    chapter = chapter
                });
            });
        }

        // 显示奖励弹窗
        if (RewardPopupUI.Instance != null)
        {
            var completeData = new RewardPopupUI.LevelCompleteData
            {
                stars = stars,
                time = time,
                collectibles = collectibles,
                totalCollectibles = totalCollectibles,
                maxCombo = result?.maxCombo ?? 0,
                score = result?.totalScore ?? 0,
                isLastLevel = (chapter == 5 && level == 4)
            };

            RewardPopupUI.Instance.SetCallbacks(
                onNext: () => GameFlowManager.Instance?.NextLevel(),
                onReplay: () => GameFlowManager.Instance?.RetryLevel(),
                onMenu: () => GameFlowManager.Instance?.GoToMainMenu()
            );

            RewardPopupUI.Instance.ShowLevelComplete(completeData);
        }

        // 流程管理器
        GameFlowManager.Instance?.CompleteLevelFlow(time, collectibles, totalCollectibles);

        // 关卡过渡对话
        if (ChapterStoryManager.Instance != null)
        {
            ChapterStoryManager.Instance.PlayLevelTransition(chapter, level, null);
        }

        // 难度调整
        if (DifficultyManager.Instance != null)
            DifficultyManager.Instance.RecordLevelComplete();

        // 检查评分提示
        if (MobileServices.Instance != null && SaveSystem.Instance != null)
        {
            int totalLevels = SaveSystem.Instance.Data.levelsCompleted;
            if (MobileServices.Instance.ShouldShowRatePrompt(totalLevels))
            {
                // 延迟显示，不要打断奖励弹窗
                StartCoroutine(DelayedRatePrompt(totalLevels));
            }
        }
    }

    private IEnumerator DelayedRatePrompt(int totalLevels)
    {
        yield return new WaitForSeconds(5f);
        MobileServices.Instance?.ShowRatePrompt(totalLevels);
    }
}
