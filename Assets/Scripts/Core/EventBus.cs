using System;
using System.Collections.Generic;

/// <summary>
/// 全局事件总线 - 解耦各系统间的通信
/// 支持类型安全的事件发布/订阅，自动清理
/// </summary>
public static class EventBus
{
    private static Dictionary<Type, List<Delegate>> eventHandlers = new Dictionary<Type, List<Delegate>>();

    /// <summary>
    /// 订阅事件
    /// </summary>
    public static void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
    {
        var type = typeof(T);
        if (!eventHandlers.ContainsKey(type))
            eventHandlers[type] = new List<Delegate>();

        eventHandlers[type].Add(handler);
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    public static void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
    {
        var type = typeof(T);
        if (eventHandlers.ContainsKey(type))
            eventHandlers[type].Remove(handler);
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    public static void Publish<T>(T eventData) where T : struct, IGameEvent
    {
        var type = typeof(T);
        if (!eventHandlers.ContainsKey(type)) return;

        // 复制列表避免迭代中修改
        var handlers = new List<Delegate>(eventHandlers[type]);
        foreach (var handler in handlers)
        {
            (handler as Action<T>)?.Invoke(eventData);
        }
    }

    /// <summary>
    /// 清除所有订阅（场景切换时调用）
    /// </summary>
    public static void ClearAll()
    {
        eventHandlers.Clear();
    }

    /// <summary>
    /// 清除指定事件的所有订阅
    /// </summary>
    public static void Clear<T>() where T : struct, IGameEvent
    {
        var type = typeof(T);
        if (eventHandlers.ContainsKey(type))
            eventHandlers[type].Clear();
    }
}

/// <summary>
/// 事件接口标记
/// </summary>
public interface IGameEvent { }

// ============ 游戏事件定义 ============

// 玩家事件
public struct PlayerDamagedEvent : IGameEvent
{
    public float damage;
    public float remainingHealth;
    public int playerIndex;
    public UnityEngine.Vector2 hitDirection;
}

public struct PlayerDeathEvent : IGameEvent
{
    public int playerIndex;
    public UnityEngine.Vector3 deathPosition;
}

public struct PlayerRespawnEvent : IGameEvent
{
    public int playerIndex;
    public UnityEngine.Vector3 spawnPosition;
}

public struct PlayerHealEvent : IGameEvent
{
    public float amount;
    public int playerIndex;
}

// 技能事件
public struct AbilityUsedEvent : IGameEvent
{
    public string abilityName;
    public int playerIndex;
    public UnityEngine.Vector3 position;
}

public struct AbilityCooldownReadyEvent : IGameEvent
{
    public string abilityName;
    public int playerIndex;
}

// 关卡事件
public struct LevelStartEvent : IGameEvent
{
    public int chapter;
    public int level;
}

public struct LevelCompleteEvent : IGameEvent
{
    public int chapter;
    public int level;
    public int stars;
    public float time;
    public int collectibles;
}

public struct CheckpointReachedEvent : IGameEvent
{
    public UnityEngine.Vector3 position;
    public int checkpointIndex;
}

public struct CollectiblePickedEvent : IGameEvent
{
    public int collected;
    public int total;
    public UnityEngine.Vector3 position;
}

// 敌人事件
public struct EnemyDefeatedEvent : IGameEvent
{
    public string enemyType;
    public UnityEngine.Vector3 position;
    public int scoreValue;
}

public struct BossPhaseChangedEvent : IGameEvent
{
    public int newPhase;
    public float bossHealthPercent;
}

public struct BossDefeatedEvent : IGameEvent
{
    public string bossName;
    public int chapter;
}

// 谜题事件
public struct PuzzleSolvedEvent : IGameEvent
{
    public string puzzleId;
    public string puzzleType;
}

public struct DoorOpenedEvent : IGameEvent
{
    public string doorId;
    public UnityEngine.Vector3 position;
}

// UI事件
public struct ComboChangedEvent : IGameEvent
{
    public int comboCount;
    public float multiplier;
}

public struct ScoreChangedEvent : IGameEvent
{
    public int totalScore;
    public int delta;
}

// 系统事件
public struct GamePausedEvent : IGameEvent
{
    public bool isPaused;
}

public struct LanguageChangedEvent : IGameEvent
{
    public int languageIndex;
}

public struct DifficultyChangedEvent : IGameEvent
{
    public float modifier;
}

public struct NetworkPlayerJoinedEvent : IGameEvent
{
    public ulong clientId;
    public int playerIndex;
}

public struct NetworkPlayerLeftEvent : IGameEvent
{
    public ulong clientId;
}

// 战斗事件
public struct EnemyHitEvent : IGameEvent
{
    public int playerIndex;
    public int damage;
    public UnityEngine.Vector3 position;
}

// 商店事件
public struct ShopPurchaseEvent : IGameEvent
{
    public string itemId;
    public string category;
}

// 拍照模式事件
public struct PhotoFilterChangedEvent : IGameEvent
{
    public UnityEngine.Color tintColor;
    public float vignette;
    public float brightness;
    public float saturation;
}

// 波次战斗事件
public struct WaveStartedEvent : IGameEvent
{
    public int waveIndex;
    public int totalWaves;
    public string waveName;
}

public struct WaveCompletedEvent : IGameEvent
{
    public int waveIndex;
    public int totalWaves;
    public bool allCleared;
}

// 提示事件（跨程序集通知UI显示提示）
public struct HintRequestEvent : IGameEvent
{
    public string textKey;
    public string fallbackText;
    public float duration;
}

// 水域事件（定义在Core中供跨程序集使用）
public struct WaterEnteredEvent : IGameEvent
{
    public UnityEngine.Vector3 position;
    public int playerIndex;
}

public struct WaterExitedEvent : IGameEvent
{
    public UnityEngine.Vector3 position;
    public int playerIndex;
}

// 陷阱/道具事件
public struct TrapTriggeredEvent : IGameEvent
{
    public string trapType;
    public UnityEngine.Vector3 position;
    public int playerIndex;
}

public struct PowerUpCollectedEvent : IGameEvent
{
    public string powerUpType;
    public float duration;
    public int playerIndex;
}

public struct TeleportEvent : IGameEvent
{
    public UnityEngine.Vector3 fromPosition;
    public UnityEngine.Vector3 toPosition;
    public int playerIndex;
}

public struct BlockToggleEvent : IGameEvent
{
    public bool groupAActive;
}

/// <summary>
/// 通用可伤害接口（放在Core中供所有程序集使用）
/// </summary>
public interface IDamageable
{
    void TakeDamage(int damage);
    bool IsAlive { get; }
}

/// <summary>
/// 可交互物体接口（放在Core中供所有程序集使用）
/// PlayerInteraction通过此接口检测和执行交互
/// 使用GameObject参数避免Core→Player循环依赖
/// </summary>
public interface IInteractable
{
    /// <summary>是否可以交互</summary>
    bool CanInteract(UnityEngine.GameObject player);

    /// <summary>执行交互</summary>
    void OnInteract(UnityEngine.GameObject player);

    /// <summary>获取交互提示文本key</summary>
    string GetInteractPrompt();
}
