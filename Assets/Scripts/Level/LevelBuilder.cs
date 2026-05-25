using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 关卡构建器 - 根据LevelConfig ScriptableObject在运行时生成关卡元素
/// 生成敌人、谜题、收集品、检查点、移动平台、传送门、环境区域等
/// 配合LevelConfig使用，实现数据驱动的关卡设计
/// </summary>
public class LevelBuilder : MonoBehaviour
{
    public static LevelBuilder Instance { get; private set; }

    [Header("关卡配置")]
    [SerializeField] private LevelConfig levelConfig;

    [Header("预制体引用")]
    [SerializeField] private PrefabLibrary prefabs;

    [System.Serializable]
    public class PrefabLibrary
    {
        [Header("谜题")]
        public GameObject pressurePlatePrefab;
        public GameObject lightSensorPrefab;
        public GameObject shadowWallPrefab;
        public GameObject gearMechanismPrefab;
        public GameObject switchPrefab;

        [Header("敌人")]
        public GameObject shadowSlimePrefab;
        public GameObject shadowArcherPrefab;
        public GameObject shadowGuardPrefab;
        public GameObject shadowFlyerPrefab;

        [Header("收集品/检查点")]
        public GameObject collectiblePrefab;
        public GameObject checkpointPrefab;

        [Header("机关")]
        public GameObject movingPlatformPrefab;
        public GameObject portalPrefab;

        [Header("区域")]
        public GameObject lightZonePrefab;
        public GameObject shadowZonePrefab;
        public GameObject waterCurrentPrefab;
        public GameObject gravityZonePrefab;
        public GameObject hazardZonePrefab;

        [Header("目标")]
        public GameObject goalTriggerPrefab;

        [Header("对话")]
        public GameObject dialogueTriggerPrefab;
    }

    // 生成的物体容器
    private Transform enemyContainer;
    private Transform puzzleContainer;
    private Transform collectibleContainer;
    private Transform platformContainer;
    private Transform zoneContainer;

    // 生成的引用追踪
    private List<GameObject> spawnedObjects = new List<GameObject>();

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 根据LevelConfig构建整个关卡
    /// </summary>
    public void BuildLevel(LevelConfig config = null)
    {
        if (config != null)
            levelConfig = config;

        if (levelConfig == null)
        {
            Debug.LogWarning("[LevelBuilder] 没有关卡配置!");
            return;
        }

        CreateContainers();

        Debug.Log($"[LevelBuilder] Building Ch.{levelConfig.chapter} Lv.{levelConfig.level}: {levelConfig.levelName}");

        // 按顺序构建各元素
        BuildCheckpoints();
        BuildCollectibles();
        BuildEnemies();
        BuildPuzzles();
        BuildMovingPlatforms();
        BuildPortals();
        BuildZones();
        BuildGoal();
        BuildDialogueTriggers();

        // 设置相机边界
        SetupCameraBounds();

        Debug.Log($"[LevelBuilder] 生成了 {spawnedObjects.Count} 个关卡元素");
    }

    private void CreateContainers()
    {
        enemyContainer = CreateContainer("--- Enemies ---");
        puzzleContainer = CreateContainer("--- Puzzles ---");
        collectibleContainer = CreateContainer("--- Collectibles ---");
        platformContainer = CreateContainer("--- Platforms ---");
        zoneContainer = CreateContainer("--- Zones ---");
    }

    private Transform CreateContainer(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        return go.transform;
    }

    // ==================== 检查点 ====================

    private void BuildCheckpoints()
    {
        if (levelConfig.checkpoints == null || prefabs.checkpointPrefab == null) return;

        foreach (var cp in levelConfig.checkpoints)
        {
            var go = Spawn(prefabs.checkpointPrefab, cp.position, puzzleContainer);

            var checkpoint = go.GetComponent<Checkpoint>();
            if (checkpoint != null)
                checkpoint.SetOrder(cp.order);
        }
    }

    // ==================== 收集品 ====================

    private void BuildCollectibles()
    {
        if (levelConfig.collectiblePositions == null || prefabs.collectiblePrefab == null) return;

        foreach (var pos in levelConfig.collectiblePositions)
        {
            Spawn(prefabs.collectiblePrefab, pos, collectibleContainer);
        }
    }

    // ==================== 敌人 ====================

    private void BuildEnemies()
    {
        if (levelConfig.enemies == null) return;

        foreach (var placement in levelConfig.enemies)
        {
            GameObject prefab = GetEnemyPrefab(placement.type);
            if (prefab == null) continue;

            var go = Spawn(prefab, placement.position, enemyContainer);

            // 设置巡逻路径
            var patrol = go.GetComponent<EnemyBase>();
            if (patrol != null && placement.patrolPath != null && placement.patrolPath.Length > 0)
            {
                // 巡逻路径由EnemyBase的子类自行处理
                // 这里设置检测范围
            }
        }
    }

    private GameObject GetEnemyPrefab(LevelConfig.EnemyType type)
    {
        return type switch
        {
            LevelConfig.EnemyType.ShadowSlime => prefabs.shadowSlimePrefab,
            LevelConfig.EnemyType.ShadowArcher => prefabs.shadowArcherPrefab,
            LevelConfig.EnemyType.ShadowGuard => prefabs.shadowGuardPrefab,
            LevelConfig.EnemyType.ShadowFlyer => prefabs.shadowFlyerPrefab,
            _ => null
        };
    }

    // ==================== 谜题 ====================

    private void BuildPuzzles()
    {
        if (levelConfig.puzzles == null) return;

        foreach (var placement in levelConfig.puzzles)
        {
            GameObject prefab = GetPuzzlePrefab(placement.type);
            if (prefab == null) continue;

            var go = Spawn(prefab, placement.position, puzzleContainer);
            go.transform.localScale = new Vector3(placement.size.x, placement.size.y, 1f);
        }
    }

    private GameObject GetPuzzlePrefab(LevelConfig.PuzzleType type)
    {
        return type switch
        {
            LevelConfig.PuzzleType.PressurePlate => prefabs.pressurePlatePrefab,
            LevelConfig.PuzzleType.LightSensor => prefabs.lightSensorPrefab,
            LevelConfig.PuzzleType.ShadowWall => prefabs.shadowWallPrefab,
            LevelConfig.PuzzleType.GearMechanism => prefabs.gearMechanismPrefab,
            LevelConfig.PuzzleType.Switch => prefabs.switchPrefab,
            _ => null
        };
    }

    // ==================== 移动平台 ====================

    private void BuildMovingPlatforms()
    {
        if (levelConfig.movingPlatforms == null || prefabs.movingPlatformPrefab == null) return;

        foreach (var placement in levelConfig.movingPlatforms)
        {
            var go = Spawn(prefabs.movingPlatformPrefab, placement.startPosition, platformContainer);

            var platform = go.GetComponent<MovingPlatform>();
            if (platform != null)
            {
                // 将waypoints转为Transform数组，或直接设置Vector2路径
                platform.SetPath(placement.waypoints, placement.speed, placement.loop);
            }
        }
    }

    // ==================== 传送门 ====================

    private void BuildPortals()
    {
        if (levelConfig.portalPairs == null || prefabs.portalPrefab == null) return;

        foreach (var pair in levelConfig.portalPairs)
        {
            var portalA = Spawn(prefabs.portalPrefab, pair.portalA, puzzleContainer);
            var portalB = Spawn(prefabs.portalPrefab, pair.portalB, puzzleContainer);

            // 互相关联（Portal使用序列化引用，运行时通过反射设置）
            var compA = portalA.GetComponent<Portal>();
            var compB = portalB.GetComponent<Portal>();

            if (compA != null && compB != null)
            {
                // 使用反射设置private serialized field
                var linkedField = typeof(Portal).GetField("linkedPortal",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (linkedField != null)
                {
                    linkedField.SetValue(compA, compB);
                    linkedField.SetValue(compB, compA);
                }
            }

            // 设置颜色
            SetPortalColor(portalA, pair.portalColor);
            SetPortalColor(portalB, pair.portalColor);
        }
    }

    private void SetPortalColor(GameObject portal, Color color)
    {
        var renderer = portal.GetComponent<SpriteRenderer>();
        if (renderer != null)
            renderer.color = color;

        var ps = portal.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            main.startColor = color;
        }
    }

    // ==================== 环境区域 ====================

    private void BuildZones()
    {
        if (levelConfig.zones == null) return;

        foreach (var placement in levelConfig.zones)
        {
            GameObject prefab = GetZonePrefab(placement.type);
            if (prefab == null) continue;

            var go = Spawn(prefab, placement.position, zoneContainer);
            go.transform.localScale = new Vector3(placement.size.x, placement.size.y, 1f);
        }
    }

    private GameObject GetZonePrefab(LevelConfig.ZoneType type)
    {
        return type switch
        {
            LevelConfig.ZoneType.LightZone => prefabs.lightZonePrefab,
            LevelConfig.ZoneType.ShadowZone => prefabs.shadowZonePrefab,
            LevelConfig.ZoneType.WaterCurrent => prefabs.waterCurrentPrefab,
            LevelConfig.ZoneType.GravityZone => prefabs.gravityZonePrefab,
            LevelConfig.ZoneType.Hazard => prefabs.hazardZonePrefab,
            _ => null
        };
    }

    // ==================== 终点 ====================

    private void BuildGoal()
    {
        if (prefabs.goalTriggerPrefab == null) return;

        Spawn(prefabs.goalTriggerPrefab, levelConfig.goalPosition, puzzleContainer);
    }

    // ==================== 对话触发 ====================

    private void BuildDialogueTriggers()
    {
        if (levelConfig.dialogueTriggers == null || prefabs.dialogueTriggerPrefab == null) return;

        foreach (var dialogue in levelConfig.dialogueTriggers)
        {
            var go = Spawn(prefabs.dialogueTriggerPrefab, dialogue.triggerPosition, puzzleContainer);

            var col = go.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                col.size = dialogue.triggerSize;
            }
        }
    }

    // ==================== 相机边界 ====================

    private void SetupCameraBounds()
    {
        if (levelConfig.cameraBoundsMin == Vector2.zero && levelConfig.cameraBoundsMax == Vector2.zero)
            return;

        // 通知CameraController设置边界
        var camController = FindFirstObjectByType<CameraController>();
        if (camController != null)
        {
            camController.SetBounds(levelConfig.cameraBoundsMin, levelConfig.cameraBoundsMax);
        }
        else
        {
            // 备用: DualPlayerCamera
            var dualCam = FindFirstObjectByType<DualPlayerCamera>();
            if (dualCam != null)
                dualCam.SetBounds(levelConfig.cameraBoundsMin, levelConfig.cameraBoundsMax);
        }
    }

    // ==================== 工具方法 ====================

    private GameObject Spawn(GameObject prefab, Vector2 position, Transform parent)
    {
        var go = Instantiate(prefab, new Vector3(position.x, position.y, 0), Quaternion.identity, parent);
        spawnedObjects.Add(go);
        return go;
    }

    /// <summary>
    /// 销毁所有生成的关卡元素（重新构建时调用）
    /// </summary>
    public void ClearLevel()
    {
        foreach (var obj in spawnedObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        spawnedObjects.Clear();
    }

    /// <summary>
    /// 获取生成的元素数量
    /// </summary>
    public int SpawnedCount => spawnedObjects.Count;
}
