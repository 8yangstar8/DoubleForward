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

        // ====== 0. 关卡边界墙 + 可见终点(运行时创建,避免修改场景文件) ======
        SetupLevelBoundaries();
        MakeGoalVisible();

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

        // 合作复活系统(双人核心机制): 若不存在则创建
        if (CoopReviveSystem.Instance == null && luxPlayer != null && noxPlayer != null)
        {
            var coopReviveObj = new GameObject("CoopReviveSystem");
            coopReviveObj.AddComponent<CoopReviveSystem>();
        }

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

        // ====== 6.5. 新系统集成 ======

        // 发布关卡开始事件（供PlayerBondSystem等订阅）
        EventBus.Publish(new LevelStartEvent { chapter = chapter, level = level });

        // NG+难度应用
        if (NewGamePlusManager.Instance != null && NewGamePlusManager.Instance.IsNewGamePlus)
        {
            Debug.Log($"[LevelBoot] NG+{NewGamePlusManager.Instance.CurrentNGLevel} active");
        }

        // 关卡修改器应用
        if (LevelModifierSystem.Instance != null)
        {
            var activeModifiers = LevelModifierSystem.Instance.ActiveModifiers;
            if (activeModifiers.Count > 0)
                Debug.Log($"[LevelBoot] {activeModifiers.Count} level modifiers active");
        }

        // 故事解锁
        if (StoryRecapSystem.Instance != null)
        {
            if (isFirstLevelInChapter)
                StoryRecapSystem.Instance.UnlockStory($"ch{chapter}_intro");
        }

        // Boss战前羁绊对话
        if (isBossLevel && PlayerBondSystem.Instance != null)
            PlayerBondSystem.Instance.TryTriggerBondDialogue(
                PlayerBondSystem.BondDialogue.BondDialogueTrigger.BossEncounter);

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

    // ====== 运行时关卡边界墙 ======
    private void SetupLevelBoundaries()
    {
        var ground = GameObject.Find("Ground");
        if (ground == null) return;

        // 用碰撞体范围而不是 localScale: 地面改用平铺渲染后尺寸记在
        // SpriteRenderer.size 和碰撞体上,localScale 恒为1,按缩放算会把墙立到地面中间
        var groundCol = ground.GetComponent<Collider2D>();
        float gx = ground.transform.position.x;
        float halfW = groundCol != null
            ? groundCol.bounds.extents.x
            : ground.transform.localScale.x * 0.5f;
        float gy = ground.transform.position.y;
        int groundLayer = LayerMask.NameToLayer("Ground");

        CreateBoundaryWall("LevelBoundary_Left", new Vector3(gx - halfW - 0.5f, gy + 6f, 0), groundLayer);
        CreateBoundaryWall("LevelBoundary_Right", new Vector3(gx + halfW + 0.5f, gy + 6f, 0), groundLayer);
    }

    private void CreateBoundaryWall(string wallName, Vector3 pos, int layer)
    {
        var wall = new GameObject(wallName);
        wall.transform.position = pos;
        wall.transform.localScale = new Vector3(1f, 24f, 1f);
        if (layer >= 0) wall.layer = layer;
        var col = wall.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
    }

    // ====== 运行时终点可见化 ======
    private void MakeGoalVisible()
    {
        var goal = FindAnyObjectByType<LevelGoalTrigger>();
        if (goal == null) return;
        // 放大的是触发区,不是贴图 —— 碰撞体(1x1)跟着 transform 缩放,
        // 不放大的话终点判定只有一格宽,人从旁边擦过去都算没到。
        // 这行原本写在方法末尾、混在"加占位贴图"的分支里,方法名又叫
        // MakeGoalVisible,于是任何一条提前 return 都会把触发区一起跳掉。
        goal.transform.localScale = new Vector3(1.5f, 4f, 1f);

        if (goal.GetComponent<SpriteRenderer>() != null) return; // 已可见

        // 场景里立了终点旗就不用这根占位光柱。它是拿玩家精灵拉成 1.5x4 顶上的,
        // 画面上就是一大块糊着的半透明黄斑,比没有还难看。
        if (GameObject.Find("GoalFlag") != null) return;

        var sr = goal.gameObject.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 0.9f, 0.3f, 0.6f); // 金色光柱
        sr.sortingOrder = 5;
        // 复用玩家精灵作为占位可见标记(已在内存,避免新建纹理)
        if (luxPrefab != null)
        {
            var luxSr = luxPrefab.GetComponent<SpriteRenderer>();
            if (luxSr != null && luxSr.sprite != null)
                sr.sprite = luxSr.sprite;
        }
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
            // 设置初始复活点为出生位置
            luxObj.GetComponent<PlayerHealth>()?.SetCheckpoint(luxPos);
        }

        if (noxPrefab != null)
        {
            var noxObj = Instantiate(noxPrefab, noxPos, Quaternion.identity);
            noxObj.name = "Nox";
            noxPlayer = noxObj.GetComponent<PlayerController>();
            noxObj.GetComponent<PlayerHealth>()?.SetCheckpoint(noxPos);
        }

        // 注册到LevelManager供其他系统引用
        if (LevelManager.Instance != null)
            LevelManager.Instance.RegisterPlayers(luxPlayer, noxPlayer);

        // 设置相机跟随。
        //
        // 必须取"正在渲染的那台摄像机"上的组件,不能用单例: Instance 是静态字段,
        // 跨场景保留,切关时新旧摄像机会短暂共存(DualPlayerCamera.Awake 里还会
        // Destroy 掉后来的那个整只 GameObject)。用单例就可能把玩家引用设到一台
        // 不负责渲染的摄像机上 —— 表现为"人往前走,画面不动"。
        if (luxPlayer != null)
        {
            var mainCam = Camera.main;
            var dual = mainCam != null ? mainCam.GetComponent<DualPlayerCamera>() : null;
            var follow = mainCam != null ? mainCam.GetComponent<CameraController>() : null;

            if (dual == null) dual = DualPlayerCamera.Instance;
            if (follow == null) follow = CameraController.Instance;

            if (dual != null)
            {
                dual.SetPlayers(luxPlayer.transform,
                    noxPlayer != null ? noxPlayer.transform : null);
            }
            else if (follow != null)
            {
                follow.SetTarget(luxPlayer.transform);
            }

            Debug.Log($"[LevelBoot] camera follow wired: dual={(dual != null)} " +
                $"follow={(follow != null)} onMainCam={(mainCam != null && dual != null && dual.gameObject == mainCam.gameObject)}");
        }
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

        // 故事解锁（Boss击败、章节结局）
        if (StoryRecapSystem.Instance != null)
        {
            if (isBossLevel)
            {
                StoryRecapSystem.Instance.UnlockStory($"ch{chapter}_boss_intro");
                StoryRecapSystem.Instance.UnlockStory($"ch{chapter}_boss_defeat");
            }
            // 章节最后一关
            if (level == 4 || isBossLevel)
            {
                StoryRecapSystem.Instance.UnlockStory($"ch{chapter}_outro");
            }
        }

        // 检查评分提示
        if (MobileServices.Instance != null && SaveSystem.Instance != null)
        {
            int totalLevels = SaveSystem.Instance.Data.levelsCompletedCount;
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
