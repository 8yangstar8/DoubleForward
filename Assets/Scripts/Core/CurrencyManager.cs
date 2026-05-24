using UnityEngine;

/// <summary>
/// 货币系统 - 管理游戏内的金币和宝石
/// 金币：收集品获得，用于购买普通道具和皮肤
/// 宝石：通关奖励，用于购买高级皮肤和解锁内容
/// </summary>
public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    private const string COINS_KEY = "currency_coins";
    private const string GEMS_KEY = "currency_gems";

    [Header("货币显示")]
    [SerializeField] private int startingCoins = 0;
    [SerializeField] private int startingGems = 0;

    [Header("奖励配置")]
    [SerializeField] private int coinsPerCollectible = 10;
    [SerializeField] private int coinsPerEnemyKill = 5;
    [SerializeField] private int[] coinsPerStar = { 0, 20, 40, 80 };  // 0/1/2/3星
    [SerializeField] private int gemsPerLevelComplete = 1;
    [SerializeField] private int gemsPerBossDefeat = 5;
    [SerializeField] private int gemsPerChapterComplete = 10;

    public int Coins { get; private set; }
    public int Gems { get; private set; }

    public event System.Action<int, int> OnCoinsChanged;   // newValue, delta
    public event System.Action<int, int> OnGemsChanged;     // newValue, delta
    public event System.Action<string, int, bool> OnPurchaseResult; // itemId, cost, success

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadCurrency();
    }

    void OnEnable()
    {
        EventBus.Subscribe<CollectiblePickedEvent>(OnCollectiblePicked);
        EventBus.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Subscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<CollectiblePickedEvent>(OnCollectiblePicked);
        EventBus.Unsubscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        EventBus.Unsubscribe<LevelCompleteEvent>(OnLevelComplete);
        EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
    }

    // ==================== 货币操作 ====================

    /// <summary>
    /// 增加金币
    /// </summary>
    public void AddCoins(int amount, string source = "")
    {
        if (amount <= 0) return;
        Coins += amount;
        SaveCurrency();
        OnCoinsChanged?.Invoke(Coins, amount);

        if (AnalyticsTracker.Instance != null)
            AnalyticsTracker.Instance.TrackEvent("currency_earned",
                ("type", "coins"), ("amount", amount.ToString()), ("source", source));
    }

    /// <summary>
    /// 增加宝石
    /// </summary>
    public void AddGems(int amount, string source = "")
    {
        if (amount <= 0) return;
        Gems += amount;
        SaveCurrency();
        OnGemsChanged?.Invoke(Gems, amount);

        if (AnalyticsTracker.Instance != null)
            AnalyticsTracker.Instance.TrackEvent("currency_earned",
                ("type", "gems"), ("amount", amount.ToString()), ("source", source));
    }

    /// <summary>
    /// 消费金币
    /// </summary>
    public bool SpendCoins(int amount, string itemId = "")
    {
        if (amount <= 0 || Coins < amount)
        {
            OnPurchaseResult?.Invoke(itemId, amount, false);
            return false;
        }

        Coins -= amount;
        SaveCurrency();
        OnCoinsChanged?.Invoke(Coins, -amount);
        OnPurchaseResult?.Invoke(itemId, amount, true);

        if (AnalyticsTracker.Instance != null)
            AnalyticsTracker.Instance.TrackEvent("currency_spent",
                ("type", "coins"), ("amount", amount.ToString()), ("item", itemId));

        return true;
    }

    /// <summary>
    /// 消费宝石
    /// </summary>
    public bool SpendGems(int amount, string itemId = "")
    {
        if (amount <= 0 || Gems < amount)
        {
            OnPurchaseResult?.Invoke(itemId, amount, false);
            return false;
        }

        Gems -= amount;
        SaveCurrency();
        OnGemsChanged?.Invoke(Gems, -amount);
        OnPurchaseResult?.Invoke(itemId, amount, true);

        if (AnalyticsTracker.Instance != null)
            AnalyticsTracker.Instance.TrackEvent("currency_spent",
                ("type", "gems"), ("amount", amount.ToString()), ("item", itemId));

        return true;
    }

    /// <summary>
    /// 检查是否有足够金币
    /// </summary>
    public bool CanAffordCoins(int amount) => Coins >= amount;

    /// <summary>
    /// 检查是否有足够宝石
    /// </summary>
    public bool CanAffordGems(int amount) => Gems >= amount;

    // ==================== 事件处理 ====================

    private void OnCollectiblePicked(CollectiblePickedEvent e)
    {
        AddCoins(coinsPerCollectible, "collectible");
    }

    private void OnEnemyDefeated(EnemyDefeatedEvent e)
    {
        int reward = coinsPerEnemyKill + e.scoreValue / 10;
        AddCoins(reward, "enemy_kill");
    }

    private void OnLevelComplete(LevelCompleteEvent e)
    {
        // 星级金币奖励
        int starIdx = Mathf.Clamp(e.stars, 0, coinsPerStar.Length - 1);
        if (coinsPerStar[starIdx] > 0)
            AddCoins(coinsPerStar[starIdx], "level_complete");

        // 通关宝石奖励
        AddGems(gemsPerLevelComplete, "level_complete");

        // 检查是否完成章节
        CheckChapterCompletion(e.chapter);
    }

    private void OnBossDefeated(BossDefeatedEvent e)
    {
        AddGems(gemsPerBossDefeat, "boss_defeat");
    }

    private void CheckChapterCompletion(int chapter)
    {
        if (SaveSystem.Instance == null) return;

        int[] levelsPerChapter = { 3, 4, 4, 5, 4 };
        if (chapter < 1 || chapter > levelsPerChapter.Length) return;

        bool allComplete = true;
        for (int i = 1; i <= levelsPerChapter[chapter - 1]; i++)
        {
            if (!SaveSystem.Instance.IsLevelCompleted(chapter, i))
            {
                allComplete = false;
                break;
            }
        }

        if (allComplete)
        {
            AddGems(gemsPerChapterComplete, "chapter_complete");
        }
    }

    // ==================== 存储 ====================

    private void LoadCurrency()
    {
        Coins = PlayerPrefs.GetInt(COINS_KEY, startingCoins);
        Gems = PlayerPrefs.GetInt(GEMS_KEY, startingGems);
    }

    private void SaveCurrency()
    {
        PlayerPrefs.SetInt(COINS_KEY, Coins);
        PlayerPrefs.SetInt(GEMS_KEY, Gems);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 重置所有货币（测试用）
    /// </summary>
    public void ResetCurrency()
    {
        Coins = startingCoins;
        Gems = startingGems;
        SaveCurrency();
        OnCoinsChanged?.Invoke(Coins, 0);
        OnGemsChanged?.Invoke(Gems, 0);
    }
}
