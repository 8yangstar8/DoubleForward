using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 隐藏区域系统 - 管理关卡中的秘密房间和隐藏收集品
/// 需要特殊条件（合作技能、特定角色能力）才能进入
/// 发现隐藏区域给予额外奖励和成就
/// </summary>
public class SecretAreaSystem : MonoBehaviour
{
    public static SecretAreaSystem Instance { get; private set; }

    [Header("追踪")]
    [SerializeField] private int discoveredSecretsThisLevel;
    [SerializeField] private int totalSecretsThisLevel;

    // 持久化记录
    private HashSet<string> discoveredSecrets = new HashSet<string>();
    private const string SECRETS_KEY = "discovered_secrets";

    public int DiscoveredThisLevel => discoveredSecretsThisLevel;
    public int TotalThisLevel => totalSecretsThisLevel;

    public event System.Action<string, int> OnSecretFound;    // secretId, totalFound
    public event System.Action OnAllSecretsFound;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        LoadDiscoveredSecrets();
    }

    void Start()
    {
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        if (Instance == this) Instance = null;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 注册关卡中的隐藏区域
    /// </summary>
    public void RegisterSecret(string secretId)
    {
        totalSecretsThisLevel++;
        if (discoveredSecrets.Contains(secretId))
            discoveredSecretsThisLevel++;
    }

    /// <summary>
    /// 发现隐藏区域
    /// </summary>
    public void DiscoverSecret(string secretId)
    {
        if (discoveredSecrets.Contains(secretId)) return;

        discoveredSecrets.Add(secretId);
        discoveredSecretsThisLevel++;
        SaveDiscoveredSecrets();

        // 奖励
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.AddCurrency(50);

        if (ComboSystem.Instance != null)
            ComboSystem.Instance.PerfectAction("secret_found", 300);

        // 成就
        if (AchievementSystem.Instance != null)
        {
            AchievementSystem.Instance.UpdateProgress("secret_hunter", 1);
            if (discoveredSecrets.Count >= 10)
                AchievementSystem.Instance.Unlock("secret_master");
        }

        // 通知
        EventBus.Publish(new HintRequestEvent
        {
            textKey = "secret_found",
            fallbackText = "发现隐藏区域！",
            duration = 3f
        });

        // 音效/特效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("secret_found");

        if (VFXManager.Instance != null)
            VFXManager.Instance.Play("secret_sparkle", Vector3.zero);

        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Success();

        OnSecretFound?.Invoke(secretId, discoveredSecretsThisLevel);

        // 全部找到
        if (discoveredSecretsThisLevel >= totalSecretsThisLevel && totalSecretsThisLevel > 0)
        {
            OnAllSecretsFound?.Invoke();
            if (AchievementSystem.Instance != null)
                AchievementSystem.Instance.Unlock("level_all_secrets");
        }
    }

    /// <summary>
    /// 检查某隐藏区域是否已发现
    /// </summary>
    public bool IsDiscovered(string secretId)
    {
        return discoveredSecrets.Contains(secretId);
    }

    /// <summary>
    /// 获取总发现数
    /// </summary>
    public int GetTotalDiscovered()
    {
        return discoveredSecrets.Count;
    }

    // ==================== 事件 ====================

    private void OnLevelStart(LevelStartEvent e)
    {
        discoveredSecretsThisLevel = 0;
        totalSecretsThisLevel = 0;
    }

    // ==================== 持久化 ====================

    private void SaveDiscoveredSecrets()
    {
        string data = string.Join(",", discoveredSecrets);
        PlayerPrefs.SetString(SECRETS_KEY, data);
    }

    private void LoadDiscoveredSecrets()
    {
        string data = PlayerPrefs.GetString(SECRETS_KEY, "");
        if (!string.IsNullOrEmpty(data))
        {
            foreach (var id in data.Split(','))
            {
                if (!string.IsNullOrEmpty(id))
                    discoveredSecrets.Add(id);
            }
        }
    }
}

/// <summary>
/// 隐藏区域触发器 - 放置在关卡中标记秘密位置
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class SecretArea : MonoBehaviour
{
    [SerializeField] private string secretId;
    [SerializeField] private SecretType type = SecretType.HiddenRoom;
    [SerializeField] private bool requireBothPlayers;
    [SerializeField] private GameObject revealVFX;
    [SerializeField] private GameObject[] hiddenObjects;          // 发现后显示的物体
    [SerializeField] private SpriteRenderer concealSprite;         // 遮盖物

    private bool discovered;

    public enum SecretType
    {
        HiddenRoom,         // 隐藏房间（穿墙/破坏进入）
        HiddenPath,         // 隐藏通道
        HiddenCollectible,  // 隐藏收集品
        SecretBoss,         // 隐藏Boss
        EasterEgg            // 彩蛋
    }

    void Start()
    {
        // 注册
        if (SecretAreaSystem.Instance != null)
            SecretAreaSystem.Instance.RegisterSecret(secretId);

        // 检查是否已发现
        if (SecretAreaSystem.Instance != null && SecretAreaSystem.Instance.IsDiscovered(secretId))
        {
            discovered = true;
            RevealSecret(false);
        }

        var collider = GetComponent<BoxCollider2D>();
        if (collider != null) collider.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (discovered) return;
        if (other.GetComponent<PlayerController>() == null) return;

        if (requireBothPlayers)
        {
            // 检查两个玩家是否都在区域内
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            int insideCount = 0;
            var collider = GetComponent<BoxCollider2D>();

            foreach (var p in players)
            {
                if (collider.bounds.Contains(p.transform.position))
                    insideCount++;
            }

            if (insideCount < 2) return;
        }

        Discover();
    }

    private void Discover()
    {
        discovered = true;

        if (SecretAreaSystem.Instance != null)
            SecretAreaSystem.Instance.DiscoverSecret(secretId);

        RevealSecret(true);
    }

    private void RevealSecret(bool playEffects)
    {
        // 显示隐藏物体
        if (hiddenObjects != null)
        {
            foreach (var obj in hiddenObjects)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        // 隐藏遮盖物
        if (concealSprite != null)
            concealSprite.enabled = false;

        if (playEffects)
        {
            // 特效
            if (revealVFX != null)
            {
                var vfx = Instantiate(revealVFX, transform.position, Quaternion.identity);
                Destroy(vfx, 3f);
            }

            // 相机聚焦
            if (CameraEffects.Instance != null)
                CameraEffects.Instance.ChromaticPulse(0.3f, 0.3f);
        }
    }
}
