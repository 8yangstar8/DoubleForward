using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 通用对象池 - 减少GC压力，优化Android性能
/// 支持预热、自动扩展、最大容量限制
/// </summary>
public class ObjectPool : MonoBehaviour
{
    public static ObjectPool Instance { get; private set; }

    [System.Serializable]
    public class PoolConfig
    {
        public string poolName;
        public GameObject prefab;
        public int initialSize = 10;
        public int maxSize = 50;
        public bool autoExpand = true;
    }

    [SerializeField] private List<PoolConfig> poolConfigs = new List<PoolConfig>();

    private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
    private Dictionary<string, PoolConfig> configMap = new Dictionary<string, PoolConfig>();
    private Dictionary<string, Transform> poolContainers = new Dictionary<string, Transform>();
    private Dictionary<string, int> activeCount = new Dictionary<string, int>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializePools();
    }

    private void InitializePools()
    {
        foreach (var config in poolConfigs)
        {
            CreatePool(config);
        }
    }

    /// <summary>
    /// 创建对象池
    /// </summary>
    public void CreatePool(PoolConfig config)
    {
        if (config.prefab == null || pools.ContainsKey(config.poolName)) return;

        configMap[config.poolName] = config;
        pools[config.poolName] = new Queue<GameObject>();
        activeCount[config.poolName] = 0;

        // 创建容器
        var container = new GameObject($"Pool_{config.poolName}");
        container.transform.SetParent(transform);
        poolContainers[config.poolName] = container.transform;

        // 预热
        for (int i = 0; i < config.initialSize; i++)
        {
            var obj = Instantiate(config.prefab, container.transform);
            obj.SetActive(false);
            pools[config.poolName].Enqueue(obj);
        }
    }

    /// <summary>
    /// 运行时动态创建池
    /// </summary>
    public void CreatePool(string poolName, GameObject prefab, int initialSize = 5, int maxSize = 30)
    {
        CreatePool(new PoolConfig
        {
            poolName = poolName,
            prefab = prefab,
            initialSize = initialSize,
            maxSize = maxSize,
            autoExpand = true
        });
    }

    /// <summary>
    /// 从池中获取对象
    /// </summary>
    public GameObject Get(string poolName, Vector3 position, Quaternion rotation)
    {
        if (!pools.ContainsKey(poolName))
        {
            Debug.LogWarning($"[ObjectPool] 池不存在: {poolName}");
            return null;
        }

        var pool = pools[poolName];
        var config = configMap[poolName];
        GameObject obj = null;

        if (pool.Count > 0)
        {
            obj = pool.Dequeue();

            // 对象可能被意外销毁
            if (obj == null)
            {
                obj = Instantiate(config.prefab, poolContainers[poolName]);
            }
        }
        else if (config.autoExpand && activeCount[poolName] < config.maxSize)
        {
            obj = Instantiate(config.prefab, poolContainers[poolName]);
        }
        else
        {
            Debug.LogWarning($"[ObjectPool] 池已满: {poolName} ({config.maxSize})");
            return null;
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);
        activeCount[poolName]++;

        return obj;
    }

    /// <summary>
    /// 从池中获取对象（简化版）
    /// </summary>
    public GameObject Get(string poolName)
    {
        return Get(poolName, Vector3.zero, Quaternion.identity);
    }

    // ============ Prefab-based 接口（自动创建/查找池） ============

    private Dictionary<int, string> prefabToPoolName = new Dictionary<int, string>();

    /// <summary>
    /// 通过Prefab获取对象（自动创建池）
    /// </summary>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        string poolName = GetOrCreatePoolForPrefab(prefab);
        return Get(poolName, position, rotation);
    }

    /// <summary>
    /// 通过Prefab获取对象（默认位置）
    /// </summary>
    public GameObject Get(GameObject prefab)
    {
        return Get(prefab, Vector3.zero, Quaternion.identity);
    }

    /// <summary>
    /// 归还对象（自动识别池）
    /// </summary>
    public void Return(GameObject obj)
    {
        if (obj == null) return;

        // 尝试根据物体名称查找池
        string objName = obj.name.Replace("(Clone)", "").Trim();
        foreach (var kvp in configMap)
        {
            if (kvp.Value.prefab != null && kvp.Value.prefab.name == objName)
            {
                Return(kvp.Key, obj);
                return;
            }
        }

        // 找不到池则销毁
        Destroy(obj);
    }

    /// <summary>
    /// 延迟归还（自动识别池）
    /// </summary>
    public void ReturnDelayed(GameObject obj, float delay)
    {
        if (obj == null) return;

        string objName = obj.name.Replace("(Clone)", "").Trim();
        foreach (var kvp in configMap)
        {
            if (kvp.Value.prefab != null && kvp.Value.prefab.name == objName)
            {
                ReturnDelayed(kvp.Key, obj, delay);
                return;
            }
        }

        // 找不到池则延迟销毁
        Destroy(obj, delay);
    }

    private string GetOrCreatePoolForPrefab(GameObject prefab)
    {
        int prefabId = prefab.GetInstanceID();

        if (prefabToPoolName.TryGetValue(prefabId, out string existingName))
            return existingName;

        // 用prefab名称作为池名
        string poolName = $"auto_{prefab.name}";

        // 避免重名冲突
        if (pools.ContainsKey(poolName))
        {
            prefabToPoolName[prefabId] = poolName;
            return poolName;
        }

        CreatePool(poolName, prefab, 5, 30);
        prefabToPoolName[prefabId] = poolName;
        return poolName;
    }

    /// <summary>
    /// 归还对象到池
    /// </summary>
    public void Return(string poolName, GameObject obj)
    {
        if (obj == null) return;

        if (!pools.ContainsKey(poolName))
        {
            Destroy(obj);
            return;
        }

        obj.SetActive(false);

        if (poolContainers.ContainsKey(poolName))
            obj.transform.SetParent(poolContainers[poolName]);

        pools[poolName].Enqueue(obj);
        activeCount[poolName] = Mathf.Max(0, activeCount[poolName] - 1);
    }

    /// <summary>
    /// 延迟归还
    /// </summary>
    public void ReturnDelayed(string poolName, GameObject obj, float delay)
    {
        if (obj == null) return;
        StartCoroutine(ReturnAfterDelay(poolName, obj, delay));
    }

    private System.Collections.IEnumerator ReturnAfterDelay(string poolName, GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        Return(poolName, obj);
    }

    /// <summary>
    /// 清空指定池
    /// </summary>
    public void ClearPool(string poolName)
    {
        if (!pools.ContainsKey(poolName)) return;

        var pool = pools[poolName];
        while (pool.Count > 0)
        {
            var obj = pool.Dequeue();
            if (obj != null) Destroy(obj);
        }
        activeCount[poolName] = 0;
    }

    /// <summary>
    /// 获取池统计信息
    /// </summary>
    public string GetPoolStats()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kvp in pools)
        {
            int active = activeCount.ContainsKey(kvp.Key) ? activeCount[kvp.Key] : 0;
            sb.AppendLine($"{kvp.Key}: 空闲={kvp.Value.Count} 活跃={active}");
        }
        return sb.ToString();
    }
}
