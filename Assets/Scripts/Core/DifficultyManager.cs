using UnityEngine;

/// <summary>
/// 动态难度调整系统 - 根据玩家表现自动微调游戏难度
/// 避免玩家过于沮丧或过于无聊
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    public enum DifficultyLevel
    {
        Easy,
        Normal,
        Hard,
        Adaptive  // 自适应
    }

    [Header("难度设置")]
    [SerializeField] private DifficultyLevel baseDifficulty = DifficultyLevel.Adaptive;

    [Header("自适应参数")]
    [SerializeField] private int deathsBeforeEasier = 5;      // 连续死亡N次降低难度
    [SerializeField] private int noDeathLevelsBeforeHarder = 3; // 连续N关零死亡提高难度
    [SerializeField] private float adjustmentRate = 0.1f;      // 每次调整幅度

    [Header("难度修正范围")]
    [SerializeField] private float minModifier = 0.5f;         // 最低难度倍率
    [SerializeField] private float maxModifier = 1.5f;         // 最高难度倍率

    // 运行时状态
    private float currentModifier = 1f;
    private int consecutiveDeaths;
    private int noDeathLevelStreak;
    private int currentLevelDeaths;

    private const string DIFFICULTY_KEY = "difficulty_level";
    private const string MODIFIER_KEY = "difficulty_modifier";

    public DifficultyLevel CurrentDifficulty => baseDifficulty;
    public float DifficultyModifier => currentModifier;

    // 难度影响的具体数值
    public float EnemyHealthMultiplier => GetEnemyHealthMultiplier();
    public float EnemyDamageMultiplier => GetEnemyDamageMultiplier();
    public float EnemySpeedMultiplier => GetEnemySpeedMultiplier();
    public float PlayerDamageReduction => GetPlayerDamageReduction();
    public int ExtraCheckpoints => GetExtraCheckpoints();
    public float HintDelayReduction => GetHintDelayReduction();

    public event System.Action<float> OnDifficultyChanged; // modifier

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
    }

    /// <summary>
    /// 设置基础难度
    /// </summary>
    public void SetDifficulty(DifficultyLevel level)
    {
        baseDifficulty = level;

        switch (level)
        {
            case DifficultyLevel.Easy:
                currentModifier = 0.6f;
                break;
            case DifficultyLevel.Normal:
                currentModifier = 1f;
                break;
            case DifficultyLevel.Hard:
                currentModifier = 1.4f;
                break;
            case DifficultyLevel.Adaptive:
                // 保持当前自适应值
                break;
        }

        SaveSettings();
        OnDifficultyChanged?.Invoke(currentModifier);
    }

    /// <summary>
    /// 记录玩家死亡（用于自适应调整）
    /// </summary>
    public void RecordPlayerDeath()
    {
        if (baseDifficulty != DifficultyLevel.Adaptive) return;

        consecutiveDeaths++;
        currentLevelDeaths++;

        if (consecutiveDeaths >= deathsBeforeEasier)
        {
            // 降低难度
            AdjustModifier(-adjustmentRate);
            consecutiveDeaths = 0;
            Debug.Log($"[Difficulty] 自适应降低难度: {currentModifier:F2}");
        }
    }

    /// <summary>
    /// 记录关卡完成（用于自适应调整）
    /// </summary>
    public void RecordLevelComplete()
    {
        if (baseDifficulty != DifficultyLevel.Adaptive) return;

        if (currentLevelDeaths == 0)
        {
            noDeathLevelStreak++;

            if (noDeathLevelStreak >= noDeathLevelsBeforeHarder)
            {
                // 提高难度
                AdjustModifier(adjustmentRate);
                noDeathLevelStreak = 0;
                Debug.Log($"[Difficulty] 自适应提高难度: {currentModifier:F2}");
            }
        }
        else
        {
            noDeathLevelStreak = 0;
        }

        currentLevelDeaths = 0;
        consecutiveDeaths = 0;
        SaveSettings();
    }

    private void AdjustModifier(float delta)
    {
        currentModifier = Mathf.Clamp(currentModifier + delta, minModifier, maxModifier);
        OnDifficultyChanged?.Invoke(currentModifier);
        SaveSettings();
    }

    // ============ 难度影响计算 ============

    private float GetEnemyHealthMultiplier()
    {
        // 难度越高，敌人血量越多
        return Mathf.Lerp(0.6f, 1.5f, (currentModifier - minModifier) / (maxModifier - minModifier));
    }

    private float GetEnemyDamageMultiplier()
    {
        return Mathf.Lerp(0.5f, 1.3f, (currentModifier - minModifier) / (maxModifier - minModifier));
    }

    private float GetEnemySpeedMultiplier()
    {
        return Mathf.Lerp(0.8f, 1.2f, (currentModifier - minModifier) / (maxModifier - minModifier));
    }

    private float GetPlayerDamageReduction()
    {
        // 低难度减伤更多
        if (currentModifier < 0.7f) return 0.3f;
        if (currentModifier < 0.9f) return 0.15f;
        return 0f;
    }

    private int GetExtraCheckpoints()
    {
        // 低难度额外检查点
        if (currentModifier < 0.7f) return 2;
        if (currentModifier < 0.9f) return 1;
        return 0;
    }

    private float GetHintDelayReduction()
    {
        // 低难度更快出现提示
        if (currentModifier < 0.7f) return 0.5f;  // 提示延迟减半
        if (currentModifier < 0.9f) return 0.3f;
        return 0f;
    }

    /// <summary>
    /// 获取玩家伤害倍率（用于PlayerCombat）
    /// 低难度玩家伤害略高，高难度略低
    /// </summary>
    public float GetPlayerDamageMultiplier()
    {
        return Mathf.Lerp(1.4f, 0.8f, (currentModifier - minModifier) / (maxModifier - minModifier));
    }

    /// <summary>
    /// 获取难度描述文本
    /// </summary>
    public string GetDifficultyText()
    {
        if (baseDifficulty != DifficultyLevel.Adaptive)
            return baseDifficulty.ToString();

        if (currentModifier < 0.7f) return "简单 (自适应)";
        if (currentModifier < 1.1f) return "普通 (自适应)";
        return "困难 (自适应)";
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetInt(DIFFICULTY_KEY, (int)baseDifficulty);
        PlayerPrefs.SetFloat(MODIFIER_KEY, currentModifier);
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        if (PlayerPrefs.HasKey(DIFFICULTY_KEY))
        {
            baseDifficulty = (DifficultyLevel)PlayerPrefs.GetInt(DIFFICULTY_KEY);
            currentModifier = PlayerPrefs.GetFloat(MODIFIER_KEY, 1f);
        }
    }
}
