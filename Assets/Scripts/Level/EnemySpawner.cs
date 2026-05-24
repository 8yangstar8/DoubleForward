using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 敌人刷怪器 - 在指定区域生成和管理敌人
/// 支持波次刷怪、随机生成、Boss触发
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    public enum SpawnMode
    {
        OnStart,        // 关卡开始时生成
        OnTrigger,      // 玩家进入区域触发
        Wave,           // 波次模式
        Continuous      // 持续生成
    }

    [Header("生成模式")]
    [SerializeField] private SpawnMode mode = SpawnMode.OnStart;

    [Header("敌人预制体")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private Transform[] spawnPoints;

    [Header("生成设置")]
    [SerializeField] private int maxAliveEnemies = 5;
    [SerializeField] private int totalSpawnCount = 10;   // 0=无限
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private float initialDelay = 1f;

    [Header("波次模式")]
    [SerializeField] private List<WaveConfig> waves = new List<WaveConfig>();
    [SerializeField] private float waveCooldown = 5f;

    [Header("触发区域")]
    [SerializeField] private Vector2 triggerSize = new Vector2(10, 5);
    [SerializeField] private bool lockDoorsOnTrigger;
    [SerializeField] private GameObject[] doorsToLock;
    [SerializeField] private GameObject[] doorsToOpen; // 清空后开门

    [Header("通知")]
    [SerializeField] private string clearMessage = "";   // 清空时显示的消息

    [System.Serializable]
    public class WaveConfig
    {
        public string waveName;
        public int[] enemyIndices;       // enemyPrefabs中的索引
        public int[] spawnPointIndices;  // spawnPoints中的索引
        public float spawnDelay = 0.5f;  // 每个敌人间隔
    }

    private List<GameObject> aliveEnemies = new List<GameObject>();
    private int totalSpawned;
    private int currentWave;
    private bool isActive;
    private bool isTriggered;
    private bool isCleared;

    public int AliveCount => aliveEnemies.Count;
    public bool IsCleared => isCleared;
    public int CurrentWave => currentWave;

    public event System.Action OnAllCleared;
    public event System.Action<int> OnWaveStart; // wave index

    void Start()
    {
        if (mode == SpawnMode.OnStart)
        {
            StartCoroutine(SpawnSequence());
        }
    }

    void Update()
    {
        // 清理已死亡的敌人引用
        aliveEnemies.RemoveAll(e => e == null);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (mode != SpawnMode.OnTrigger || isTriggered) return;
        if (!other.CompareTag("Player")) return;

        isTriggered = true;

        // 锁门
        if (lockDoorsOnTrigger)
        {
            foreach (var door in doorsToLock)
            {
                if (door != null) door.SetActive(true);
            }
        }

        StartCoroutine(SpawnSequence());
    }

    private IEnumerator SpawnSequence()
    {
        isActive = true;
        yield return new WaitForSeconds(initialDelay);

        switch (mode)
        {
            case SpawnMode.OnStart:
            case SpawnMode.OnTrigger:
                yield return SpawnAll();
                break;
            case SpawnMode.Wave:
                yield return SpawnWaves();
                break;
            case SpawnMode.Continuous:
                yield return SpawnContinuous();
                break;
        }
    }

    private IEnumerator SpawnAll()
    {
        int count = totalSpawnCount > 0 ? totalSpawnCount : maxAliveEnemies;

        for (int i = 0; i < count; i++)
        {
            if (aliveEnemies.Count >= maxAliveEnemies)
                yield return new WaitUntil(() => aliveEnemies.Count < maxAliveEnemies);

            SpawnRandomEnemy();
            yield return new WaitForSeconds(spawnInterval);
        }

        // 等待所有敌人被消灭
        yield return new WaitUntil(() => aliveEnemies.Count == 0);
        OnCleared();
    }

    private IEnumerator SpawnWaves()
    {
        for (int w = 0; w < waves.Count; w++)
        {
            currentWave = w;
            OnWaveStart?.Invoke(w);

            var wave = waves[w];

            // 生成这一波的敌人
            for (int i = 0; i < wave.enemyIndices.Length; i++)
            {
                int enemyIdx = wave.enemyIndices[i];
                int spawnIdx = (wave.spawnPointIndices != null && i < wave.spawnPointIndices.Length) ?
                    wave.spawnPointIndices[i] : Random.Range(0, spawnPoints.Length);

                SpawnEnemy(enemyIdx, spawnIdx);
                yield return new WaitForSeconds(wave.spawnDelay);
            }

            // 等待这一波清空
            yield return new WaitUntil(() => aliveEnemies.Count == 0);

            // 波次间隔
            if (w < waves.Count - 1)
                yield return new WaitForSeconds(waveCooldown);
        }

        OnCleared();
    }

    private IEnumerator SpawnContinuous()
    {
        while (isActive)
        {
            if (totalSpawnCount > 0 && totalSpawned >= totalSpawnCount)
            {
                // 等待所有消灭
                yield return new WaitUntil(() => aliveEnemies.Count == 0);
                OnCleared();
                yield break;
            }

            if (aliveEnemies.Count < maxAliveEnemies)
            {
                SpawnRandomEnemy();
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnRandomEnemy()
    {
        int enemyIdx = Random.Range(0, enemyPrefabs.Length);
        int spawnIdx = Random.Range(0, spawnPoints.Length);
        SpawnEnemy(enemyIdx, spawnIdx);
    }

    private void SpawnEnemy(int enemyIndex, int spawnIndex)
    {
        if (enemyPrefabs == null || enemyIndex >= enemyPrefabs.Length) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        spawnIndex = Mathf.Clamp(spawnIndex, 0, spawnPoints.Length - 1);
        var prefab = enemyPrefabs[enemyIndex];
        var spawnPoint = spawnPoints[spawnIndex];

        if (prefab == null || spawnPoint == null) return;

        var enemy = Instantiate(prefab, spawnPoint.position, Quaternion.identity);
        aliveEnemies.Add(enemy);
        totalSpawned++;

        // 监听死亡
        var enemyBase = enemy.GetComponent<EnemyBase>();
        if (enemyBase != null)
        {
            enemyBase.OnDeath += () =>
            {
                aliveEnemies.Remove(enemy);

                // 统计
                if (GameStats.Instance != null)
                    GameStats.Instance.RecordEnemyKill();
            };
        }

        // 生成特效
        if (VFXManager.Instance != null)
            VFXManager.Instance.Play(VFXManager.Effects.PlayerRespawn, spawnPoint.position);
    }

    private void OnCleared()
    {
        isCleared = true;
        isActive = false;
        OnAllCleared?.Invoke();

        // 开门
        if (lockDoorsOnTrigger)
        {
            foreach (var door in doorsToLock)
            {
                if (door != null) door.SetActive(false);
            }
            foreach (var door in doorsToOpen)
            {
                if (door != null) door.SetActive(true);
            }
        }

        // 显示清空消息
        if (!string.IsNullOrEmpty(clearMessage) && HintSystem.Instance != null)
        {
            HintSystem.Instance.ShowCustomHint(clearMessage, 3f);
        }

        // 标记进度
        if (HintSystem.Instance != null)
            HintSystem.Instance.MarkProgress();
    }

    void OnDrawGizmosSelected()
    {
        // 绘制触发区域
        if (mode == SpawnMode.OnTrigger)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawCube(transform.position, triggerSize);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, triggerSize);
        }

        // 绘制生成点
        Gizmos.color = Color.yellow;
        if (spawnPoints != null)
        {
            foreach (var point in spawnPoints)
            {
                if (point != null)
                    Gizmos.DrawWireSphere(point.position, 0.5f);
            }
        }
    }
}
