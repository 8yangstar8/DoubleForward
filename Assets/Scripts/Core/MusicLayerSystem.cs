using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 音乐层控制系统 - 根据游戏状态动态混合音乐层
/// 战斗层、探索层、谜题层、Boss层动态叠加
/// 与AudioManager配合，提供更细腻的音乐体验
/// </summary>
public class MusicLayerSystem : MonoBehaviour
{
    public static MusicLayerSystem Instance { get; private set; }

    [Header("音乐层")]
    [SerializeField] private MusicLayer[] layers;

    [Header("过渡")]
    [SerializeField] private float crossfadeDuration = 1.5f;
    [SerializeField] private float layerFadeDuration = 0.8f;

    [Header("动态混合")]
    [SerializeField] private float combatFadeInThreshold = 2f;    // 附近敌人距离
    [SerializeField] private float combatFadeOutDelay = 3f;       // 战斗结束后延迟淡出

    // 运行时
    private Dictionary<string, AudioSource> audioSources;
    private Dictionary<string, float> targetVolumes;
    private float combatTimer;
    private bool inCombat;
    private bool inBossFight;
    private string currentTrackSet;

    [System.Serializable]
    public class MusicLayer
    {
        public string layerId;           // "exploration", "combat", "puzzle", "boss", "tension"
        public AudioClip clip;
        [Range(0, 1)] public float maxVolume = 0.8f;
        public bool looping = true;
        public bool syncToBeat = true;   // 与主旋律同步
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        audioSources = new Dictionary<string, AudioSource>();
        targetVolumes = new Dictionary<string, float>();

        // 创建AudioSource
        if (layers != null)
        {
            foreach (var layer in layers)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.clip = layer.clip;
                source.loop = layer.looping;
                source.volume = 0f;
                source.playOnAwake = false;
                source.spatialBlend = 0f; // 2D音效

                audioSources[layer.layerId] = source;
                targetVolumes[layer.layerId] = 0f;
            }
        }
    }

    void Start()
    {
        // 订阅事件
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
        EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Subscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Subscribe<GamePausedEvent>(OnGamePaused);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Unsubscribe<PuzzleSolvedEvent>(OnPuzzleSolved);
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Unsubscribe<GamePausedEvent>(OnGamePaused);
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // 平滑音量过渡
        foreach (var kvp in audioSources)
        {
            string layerId = kvp.Key;
            var source = kvp.Value;
            float target = targetVolumes.ContainsKey(layerId) ? targetVolumes[layerId] : 0f;

            source.volume = Mathf.MoveTowards(source.volume, target,
                Time.unscaledDeltaTime / layerFadeDuration);
        }

        // 动态战斗检测
        UpdateCombatDetection();
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 设置音乐层音量（0~1）
    /// </summary>
    public void SetLayerVolume(string layerId, float volume)
    {
        if (!targetVolumes.ContainsKey(layerId)) return;

        var layer = GetLayer(layerId);
        float maxVol = layer != null ? layer.maxVolume : 1f;
        targetVolumes[layerId] = Mathf.Clamp01(volume) * maxVol;

        // 确保在播放
        if (volume > 0 && audioSources.ContainsKey(layerId))
        {
            if (!audioSources[layerId].isPlaying)
                audioSources[layerId].Play();
        }
    }

    /// <summary>
    /// 开始播放所有层（同步启动）
    /// </summary>
    public void StartAllLayers()
    {
        double startTime = AudioSettings.dspTime + 0.1;

        foreach (var kvp in audioSources)
        {
            kvp.Value.PlayScheduled(startTime);
        }
    }

    /// <summary>
    /// 停止所有层
    /// </summary>
    public void StopAllLayers(bool fade = true)
    {
        foreach (var key in new List<string>(targetVolumes.Keys))
        {
            targetVolumes[key] = 0f;
        }

        if (!fade)
        {
            foreach (var source in audioSources.Values)
            {
                source.volume = 0f;
                source.Stop();
            }
        }
    }

    /// <summary>
    /// 切换到探索模式（默认）
    /// </summary>
    public void SetExplorationMode()
    {
        SetLayerVolume("exploration", 1f);
        SetLayerVolume("combat", 0f);
        SetLayerVolume("tension", 0f);
        inCombat = false;
        inBossFight = false;
    }

    /// <summary>
    /// 切换到战斗模式
    /// </summary>
    public void SetCombatMode()
    {
        SetLayerVolume("exploration", 0.3f);
        SetLayerVolume("combat", 1f);
        SetLayerVolume("tension", 0.5f);
        inCombat = true;
    }

    /// <summary>
    /// 切换到Boss模式
    /// </summary>
    public void SetBossMode()
    {
        SetLayerVolume("exploration", 0f);
        SetLayerVolume("combat", 0f);
        SetLayerVolume("boss", 1f);
        SetLayerVolume("tension", 1f);
        inBossFight = true;
    }

    /// <summary>
    /// 切换到谜题模式
    /// </summary>
    public void SetPuzzleMode()
    {
        SetLayerVolume("exploration", 0.5f);
        SetLayerVolume("puzzle", 1f);
        SetLayerVolume("combat", 0f);
    }

    /// <summary>
    /// 加载新的音乐套组
    /// </summary>
    public void LoadTrackSet(string setName, MusicLayer[] newLayers)
    {
        if (setName == currentTrackSet) return;

        // 停止当前
        StopAllLayers(true);

        // 替换clips
        if (newLayers != null)
        {
            foreach (var newLayer in newLayers)
            {
                if (audioSources.ContainsKey(newLayer.layerId))
                {
                    audioSources[newLayer.layerId].clip = newLayer.clip;
                }
            }
        }

        currentTrackSet = setName;

        // 启动新套组
        StartCoroutine(DelayedStart(crossfadeDuration));
    }

    // ==================== 事件处理 ====================

    private void OnLevelStart(LevelStartEvent e)
    {
        StartAllLayers();
        SetExplorationMode();
    }

    private void OnBossDefeated(BossDefeatedEvent e)
    {
        inBossFight = false;
        SetExplorationMode();
    }

    private void OnEnemyDefeated(EnemyDefeatedEvent e)
    {
        // 战斗计时器重置
        combatTimer = combatFadeOutDelay;
    }

    private void OnPuzzleSolved(PuzzleSolvedEvent e)
    {
        // 谜题解开后切回探索
        if (!inCombat && !inBossFight)
            SetExplorationMode();
    }

    private void OnLevelComplete(LevelCompleteEvent e)
    {
        StopAllLayers(true);
    }

    private void OnGamePaused(GamePausedEvent e)
    {
        // 暂停时降低音量
        foreach (var source in audioSources.Values)
        {
            source.volume *= e.isPaused ? 0.3f : 1f;
        }
    }

    // ==================== 动态战斗检测 ====================

    private void UpdateCombatDetection()
    {
        if (inBossFight) return;

        // 检查附近是否有活跃敌人
        bool enemiesNearby = false;
        var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        if (players.Length > 0 && enemies.Length > 0)
        {
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;

                foreach (var player in players)
                {
                    float dist = Vector2.Distance(
                        player.transform.position, enemy.transform.position);
                    if (dist <= combatFadeInThreshold)
                    {
                        enemiesNearby = true;
                        break;
                    }
                }
                if (enemiesNearby) break;
            }
        }

        if (enemiesNearby)
        {
            if (!inCombat)
                SetCombatMode();
            combatTimer = combatFadeOutDelay;
        }
        else if (inCombat)
        {
            combatTimer -= Time.deltaTime;
            if (combatTimer <= 0)
                SetExplorationMode();
        }
    }

    // ==================== 辅助 ====================

    private MusicLayer GetLayer(string layerId)
    {
        if (layers == null) return null;
        foreach (var layer in layers)
        {
            if (layer.layerId == layerId) return layer;
        }
        return null;
    }

    private IEnumerator DelayedStart(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        StartAllLayers();
        SetExplorationMode();
    }
}
