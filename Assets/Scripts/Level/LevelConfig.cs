using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 关卡配置 ScriptableObject - 定义关卡内所有元素的位置和属性
/// 用于编辑器离线配置，运行时由 LevelBuilder 读取并生成
/// </summary>
[CreateAssetMenu(fileName = "LevelConfig", menuName = "DoubleForward/Level Config")]
public class LevelConfig : ScriptableObject
{
    [Header("基本信息")]
    public int chapter;
    public int level;
    public string levelName;
    public string levelNameKey; // 本地化键

    [Header("地图范围")]
    public Vector2 mapMin = new Vector2(-30, -10);
    public Vector2 mapMax = new Vector2(30, 15);
    public Vector2 cameraBoundsMin;
    public Vector2 cameraBoundsMax;

    [Header("玩家出生点")]
    public Vector2 luxSpawnPoint = new Vector2(-5, 0);
    public Vector2 noxSpawnPoint = new Vector2(-3, 0);

    [Header("目标条件")]
    public float parTime = 120f;
    public int totalCollectibles = 5;
    public bool requireBothPlayersAtGoal = true;

    [Header("谜题元素")]
    public List<PuzzlePlacement> puzzles = new List<PuzzlePlacement>();

    [Header("敌人配置")]
    public List<EnemyPlacement> enemies = new List<EnemyPlacement>();

    [Header("收集品")]
    public List<Vector2> collectiblePositions = new List<Vector2>();

    [Header("检查点")]
    public List<CheckpointPlacement> checkpoints = new List<CheckpointPlacement>();

    [Header("机关平台")]
    public List<PlatformPlacement> movingPlatforms = new List<PlatformPlacement>();

    [Header("传送门")]
    public List<PortalPair> portalPairs = new List<PortalPair>();

    [Header("环境区域")]
    public List<ZonePlacement> zones = new List<ZonePlacement>();

    [Header("关卡终点")]
    public Vector2 goalPosition = new Vector2(25, 0);

    [Header("音乐")]
    public AudioClip bgmClip;
    public AudioClip bossMusic;

    [Header("对话触发")]
    public List<DialoguePlacement> dialogueTriggers = new List<DialoguePlacement>();

    // ============ 数据结构 ============

    [System.Serializable]
    public class PuzzlePlacement
    {
        public PuzzleType type;
        public Vector2 position;
        public Vector2 size = Vector2.one;
        public bool requireBothPlayers;
        public string linkedEventId;   // 关联的门/机关ID
    }

    [System.Serializable]
    public class EnemyPlacement
    {
        public EnemyType type;
        public Vector2 position;
        public Vector2[] patrolPath;
        public float detectionRange = 8f;
        public bool respawns;
    }

    [System.Serializable]
    public class CheckpointPlacement
    {
        public Vector2 position;
        public int order;               // 检查点顺序
    }

    [System.Serializable]
    public class PlatformPlacement
    {
        public Vector2 startPosition;
        public Vector2[] waypoints;
        public float speed = 2f;
        public bool loop = true;
    }

    [System.Serializable]
    public class PortalPair
    {
        public Vector2 portalA;
        public Vector2 portalB;
        public Color portalColor = Color.cyan;
    }

    [System.Serializable]
    public class ZonePlacement
    {
        public ZoneType type;
        public Vector2 position;
        public Vector2 size = new Vector2(5, 5);
        public float intensity = 1f;
    }

    [System.Serializable]
    public class DialoguePlacement
    {
        public Vector2 triggerPosition;
        public Vector2 triggerSize = new Vector2(3, 3);
        public string[] dialogueKeys;      // 本地化文本键
        public bool onlyOnce = true;
        public bool requireBothPlayers;
    }

    public enum PuzzleType
    {
        PressurePlate,
        LightSensor,
        ShadowWall,
        GearMechanism,
        Switch
    }

    public enum EnemyType
    {
        ShadowSlime,
        ShadowArcher,
        ShadowGuard,
        ShadowFlyer
    }

    public enum ZoneType
    {
        LightZone,
        ShadowZone,
        WaterCurrent,
        GravityZone,
        Hazard,
        AmbientSound
    }
}
