using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 敌人波次管理器 - 管理战斗竞技场中的波次式敌人生成
/// 支持多波次配置、波间休息、Boss波、奖励掉落
/// 常用于锁定房间战斗、Boss前哨战、生存挑战
/// </summary>
public class EnemyWaveManager : MonoBehaviour
{
    [Header("波次配置")]
    [SerializeField] private WaveData[] waves;
    [SerializeField] private float timeBetweenWaves = 3f;
    [SerializeField] private bool autoStart = false;

    [Header("生成点")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnRadius = 0.5f;

    [Header("战斗区域")]
    [SerializeField] private Collider2D arenaCollider;   // 可选: 锁定区域
    [SerializeField] private GameObject[] arenaDoors;     // 战斗开始关闭, 结束打开
    [SerializeField] private bool lockPlayersInArena = true;

    [Header("奖励")]
    [SerializeField] private GameObject waveCompleteRewardPrefab;
    [SerializeField] private GameObject allWavesCompleteRewardPrefab;
    [SerializeField] private int coinsPerWave = 20;
    [SerializeField] private int bonusCoinsAllClear = 100;

    [Header("视觉")]
    [SerializeField] private float spawnWarningDuration = 1f;
    [SerializeField] private GameObject spawnWarningVFX;

    // 状态
    public int CurrentWave { get; private set; }
    public int TotalWaves => waves != null ? waves.Length : 0;
    public bool IsActive { get; private set; }
    public bool IsComplete { get; private set; }
    public int EnemiesRemaining { get; private set; }

    private List<EnemyBase> activeEnemies = new List<EnemyBase>();
    private Coroutine waveRoutine;

    // 事件
    public event System.Action<int> OnWaveStarted;        // waveIndex
    public event System.Action<int> OnWaveCompleted;      // waveIndex
    public event System.Action OnAllWavesCompleted;
    public event System.Action<int> OnEnemyCountChanged;  // remaining

    [System.Serializable]
    public class WaveData
    {
        public string waveName = "Wave";
        public WaveEntry[] entries;
        public float spawnDelay = 0.5f;          // 每只敌人间隔
        public float preWaveDelay = 1f;          // 波次开始前等待
        [Tooltip("达成条件: 消灭所有敌人后才进入下一波")]
        public bool requireAllDefeated = true;
        [Tooltip("限时波次: 0=无限时")]
        public float timeLimit = 0f;
    }

    [System.Serializable]
    public class WaveEntry
    {
        public GameObject enemyPrefab;
        public int count = 1;
        public int preferredSpawnPoint = -1;  // -1=随机
    }

    void Start()
    {
        if (autoStart)
            StartWaves();
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 开始波次战斗
    /// </summary>
    public void StartWaves()
    {
        if (IsActive || waves == null || waves.Length == 0) return;

        IsActive = true;
        IsComplete = false;
        CurrentWave = 0;

        // 关闭竞技场门
        SetArenaDoors(false);

        waveRoutine = StartCoroutine(RunWaves());
    }

    /// <summary>
    /// 强制停止当前波次
    /// </summary>
    public void StopWaves()
    {
        if (waveRoutine != null)
            StopCoroutine(waveRoutine);

        // 清除残余敌人
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null)
                Destroy(enemy.gameObject);
        }
        activeEnemies.Clear();
        EnemiesRemaining = 0;

        IsActive = false;
        SetArenaDoors(true);
    }

    /// <summary>
    /// 跳到指定波次（调试用）
    /// </summary>
    public void SkipToWave(int waveIndex)
    {
        if (!IsActive) return;
        StopWaves();
        CurrentWave = Mathf.Clamp(waveIndex, 0, waves.Length - 1);
        IsActive = true;
        SetArenaDoors(false);
        waveRoutine = StartCoroutine(RunWaves());
    }

    // ==================== 波次流程 ====================

    private IEnumerator RunWaves()
    {
        for (int i = CurrentWave; i < waves.Length; i++)
        {
            CurrentWave = i;
            var wave = waves[i];

            // 波前等待
            yield return new WaitForSeconds(wave.preWaveDelay);

            // 通知UI
            OnWaveStarted?.Invoke(i);

            // 通过EventBus发布波次信息（UI层订阅显示）
            EventBus.Publish(new WaveStartedEvent
            {
                waveIndex = i,
                totalWaves = waves.Length,
                waveName = wave.waveName
            });

            // 生成敌人
            yield return StartCoroutine(SpawnWaveEnemies(wave));

            // 等待所有敌人被消灭
            if (wave.requireAllDefeated)
            {
                float timer = 0f;
                while (EnemiesRemaining > 0)
                {
                    // 清理已销毁的引用
                    CleanupDeadEnemies();

                    // 限时检查
                    if (wave.timeLimit > 0)
                    {
                        timer += Time.deltaTime;
                        if (timer >= wave.timeLimit)
                        {
                            // 时间到, 清除剩余敌人
                            foreach (var enemy in activeEnemies)
                            {
                                if (enemy != null)
                                    enemy.TakeDamage(9999f);
                            }
                            break;
                        }
                    }

                    yield return null;
                }
            }

            // 波次完成
            OnWaveCompleted?.Invoke(i);

            // 波次奖励
            if (coinsPerWave > 0 && CurrencyManager.Instance != null)
                CurrencyManager.Instance.AddCoins(coinsPerWave);

            if (waveCompleteRewardPrefab != null && spawnPoints.Length > 0)
            {
                Vector3 rewardPos = spawnPoints[0].position + Vector3.up;
                Instantiate(waveCompleteRewardPrefab, rewardPos, Quaternion.identity);
            }

            // 振动反馈
            if (HapticFeedback.Instance != null)
                HapticFeedback.Instance.Medium();

            // 波间等待（最后一波不等）
            if (i < waves.Length - 1)
                yield return new WaitForSeconds(timeBetweenWaves);
        }

        // 全部波次完成
        AllWavesComplete();
    }

    private IEnumerator SpawnWaveEnemies(WaveData wave)
    {
        foreach (var entry in wave.entries)
        {
            if (entry.enemyPrefab == null) continue;

            for (int i = 0; i < entry.count; i++)
            {
                // 选择生成点
                Vector3 spawnPos = GetSpawnPosition(entry.preferredSpawnPoint);

                // 生成预警特效
                if (spawnWarningVFX != null)
                {
                    var warning = Instantiate(spawnWarningVFX, spawnPos, Quaternion.identity);
                    Destroy(warning, spawnWarningDuration);
                    yield return new WaitForSeconds(spawnWarningDuration * 0.5f);
                }

                // 生成敌人
                var enemyGo = Instantiate(entry.enemyPrefab, spawnPos, Quaternion.identity);
                var enemy = enemyGo.GetComponent<EnemyBase>();

                if (enemy != null)
                {
                    activeEnemies.Add(enemy);
                    enemy.OnDeath += () => OnEnemyDefeated(enemy);
                }

                EnemiesRemaining++;
                OnEnemyCountChanged?.Invoke(EnemiesRemaining);

                yield return new WaitForSeconds(wave.spawnDelay);
            }
        }
    }

    private Vector3 GetSpawnPosition(int preferred)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return transform.position;

        Transform point;
        if (preferred >= 0 && preferred < spawnPoints.Length)
            point = spawnPoints[preferred];
        else
            point = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // 添加随机偏移
        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        return point.position + new Vector3(offset.x, offset.y, 0);
    }

    private void OnEnemyDefeated(EnemyBase enemy)
    {
        activeEnemies.Remove(enemy);
        EnemiesRemaining = Mathf.Max(0, EnemiesRemaining - 1);
        OnEnemyCountChanged?.Invoke(EnemiesRemaining);

        // 发布击败事件
        EventBus.Publish(new EnemyDefeatedEvent
        {
            enemyType = enemy.GetType().Name,
            position = enemy.transform.position,
            scoreValue = 100
        });
    }

    private void CleanupDeadEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null || activeEnemies[i].IsDead)
            {
                activeEnemies.RemoveAt(i);
                EnemiesRemaining = Mathf.Max(0, EnemiesRemaining - 1);
            }
        }
        OnEnemyCountChanged?.Invoke(EnemiesRemaining);
    }

    private void AllWavesComplete()
    {
        IsActive = false;
        IsComplete = true;

        // 打开竞技场门
        SetArenaDoors(true);

        // 总通关奖励
        if (bonusCoinsAllClear > 0 && CurrencyManager.Instance != null)
            CurrencyManager.Instance.AddCoins(bonusCoinsAllClear);

        if (allWavesCompleteRewardPrefab != null)
        {
            Vector3 pos = transform.position + Vector3.up;
            Instantiate(allWavesCompleteRewardPrefab, pos, Quaternion.identity);
        }

        // 成就和音效
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Success();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("wave_all_clear");

        // 发布谜题完成事件（战斗竞技场也是一种谜题）
        EventBus.Publish(new PuzzleSolvedEvent
        {
            puzzleId = gameObject.name,
            puzzleType = "WaveBattle"
        });

        OnAllWavesCompleted?.Invoke();
    }

    private void SetArenaDoors(bool open)
    {
        if (arenaDoors == null) return;
        foreach (var door in arenaDoors)
        {
            if (door != null)
                door.SetActive(!open); // 门开=物体关闭
        }
    }

    // ==================== Gizmo ====================

    void OnDrawGizmosSelected()
    {
        // 生成点
        if (spawnPoints != null)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null) continue;
                Gizmos.DrawWireSphere(spawnPoints[i].position, spawnRadius);
                Gizmos.DrawIcon(spawnPoints[i].position, "d_console.warnicon", true);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    spawnPoints[i].position + Vector3.up * 0.5f, $"Spawn {i}");
#endif
            }
        }

        // 竞技场区域
        if (arenaCollider != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawCube(arenaCollider.bounds.center, arenaCollider.bounds.size);
        }
    }
}
