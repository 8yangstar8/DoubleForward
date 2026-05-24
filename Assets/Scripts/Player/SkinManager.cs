using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 皮肤管理器 - 管理角色皮肤切换
/// 支持Lux和Nox的多套皮肤，通过商店购买解锁
/// 每个皮肤包含：Sprite集、特效颜色、技能特效变体
/// </summary>
public class SkinManager : MonoBehaviour
{
    public static SkinManager Instance { get; private set; }

    [Header("皮肤数据")]
    [SerializeField] private SkinData[] luxSkins;
    [SerializeField] private SkinData[] noxSkins;

    [System.Serializable]
    public class SkinData
    {
        public string skinId;
        public string displayNameKey;
        public Sprite portrait;                   // 立绘
        public RuntimeAnimatorController animator; // 动画控制器
        public Color primaryColor = Color.white;  // 主色调
        public Color secondaryColor = Color.white;
        public Color trailColor = Color.white;    // 拖尾颜色
        public Color skillColor = Color.white;    // 技能特效颜色
        public bool isDefault;
        public Sprite[] idleFrames;               // 可选：自定义帧动画
    }

    // 当前装备的皮肤
    private const string LUX_SKIN_KEY = "equipped_skin_lux";
    private const string NOX_SKIN_KEY = "equipped_skin_nox";
    private const string SKIN_OWNED_PREFIX = "skin_owned_";

    public string CurrentLuxSkin { get; private set; }
    public string CurrentNoxSkin { get; private set; }

    public event System.Action<string, PlayerController.PlayerType> OnSkinChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadEquippedSkins();
    }

    void OnEnable()
    {
        EventBus.Subscribe<ShopPurchaseEvent>(OnShopPurchase);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<ShopPurchaseEvent>(OnShopPurchase);
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 获取当前装备的皮肤数据
    /// </summary>
    public SkinData GetEquippedSkin(PlayerController.PlayerType playerType)
    {
        string skinId = playerType == PlayerController.PlayerType.Lux ? CurrentLuxSkin : CurrentNoxSkin;
        SkinData[] skins = playerType == PlayerController.PlayerType.Lux ? luxSkins : noxSkins;

        if (skins == null) return null;

        foreach (var skin in skins)
        {
            if (skin.skinId == skinId) return skin;
        }

        // 返回默认皮肤
        foreach (var skin in skins)
        {
            if (skin.isDefault) return skin;
        }

        return skins.Length > 0 ? skins[0] : null;
    }

    /// <summary>
    /// 装备皮肤
    /// </summary>
    public bool EquipSkin(string skinId, PlayerController.PlayerType playerType)
    {
        if (!IsSkinOwned(skinId)) return false;

        if (playerType == PlayerController.PlayerType.Lux)
        {
            CurrentLuxSkin = skinId;
            PlayerPrefs.SetString(LUX_SKIN_KEY, skinId);
        }
        else
        {
            CurrentNoxSkin = skinId;
            PlayerPrefs.SetString(NOX_SKIN_KEY, skinId);
        }

        PlayerPrefs.Save();
        OnSkinChanged?.Invoke(skinId, playerType);

        // 立即应用到场景中的玩家
        ApplySkinToActivePlayer(playerType);

        return true;
    }

    /// <summary>
    /// 检查皮肤是否已拥有
    /// </summary>
    public bool IsSkinOwned(string skinId)
    {
        // 默认皮肤始终拥有
        if (IsDefaultSkin(skinId)) return true;
        return PlayerPrefs.GetInt(SKIN_OWNED_PREFIX + skinId, 0) == 1;
    }

    /// <summary>
    /// 解锁皮肤
    /// </summary>
    public void UnlockSkin(string skinId)
    {
        PlayerPrefs.SetInt(SKIN_OWNED_PREFIX + skinId, 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 获取所有皮肤列表（用于UI展示）
    /// </summary>
    public List<SkinInfo> GetSkinList(PlayerController.PlayerType playerType)
    {
        var list = new List<SkinInfo>();
        SkinData[] skins = playerType == PlayerController.PlayerType.Lux ? luxSkins : noxSkins;
        string equippedId = playerType == PlayerController.PlayerType.Lux ? CurrentLuxSkin : CurrentNoxSkin;

        if (skins == null) return list;

        foreach (var skin in skins)
        {
            list.Add(new SkinInfo
            {
                skinId = skin.skinId,
                displayName = GetSkinDisplayName(skin),
                portrait = skin.portrait,
                primaryColor = skin.primaryColor,
                isOwned = IsSkinOwned(skin.skinId),
                isEquipped = skin.skinId == equippedId,
                isDefault = skin.isDefault
            });
        }

        return list;
    }

    /// <summary>
    /// 应用皮肤到指定的PlayerController
    /// </summary>
    public void ApplySkin(PlayerController player)
    {
        if (player == null) return;

        SkinData skin = GetEquippedSkin(player.Type);
        if (skin == null) return;

        // 替换AnimatorController
        var animator = player.GetComponent<Animator>();
        if (animator != null && skin.animator != null)
            animator.runtimeAnimatorController = skin.animator;

        // 设置颜色
        var sr = player.GetComponent<SpriteRenderer>();
        if (sr == null) sr = player.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            sr.color = skin.primaryColor;

        // 设置拖尾颜色
        var trail = player.GetComponentInChildren<TrailRenderer>();
        if (trail != null)
        {
            trail.startColor = skin.trailColor;
            trail.endColor = new Color(skin.trailColor.r, skin.trailColor.g, skin.trailColor.b, 0);
        }

        // 设置粒子颜色
        var particles = player.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            var main = ps.main;
            main.startColor = skin.skillColor;
        }
    }

    // ==================== 内部方法 ====================

    private void LoadEquippedSkins()
    {
        CurrentLuxSkin = PlayerPrefs.GetString(LUX_SKIN_KEY, GetDefaultSkinId(luxSkins));
        CurrentNoxSkin = PlayerPrefs.GetString(NOX_SKIN_KEY, GetDefaultSkinId(noxSkins));
    }

    private string GetDefaultSkinId(SkinData[] skins)
    {
        if (skins == null || skins.Length == 0) return "";
        foreach (var skin in skins)
        {
            if (skin.isDefault) return skin.skinId;
        }
        return skins[0].skinId;
    }

    private bool IsDefaultSkin(string skinId)
    {
        if (luxSkins != null)
            foreach (var skin in luxSkins)
                if (skin.skinId == skinId && skin.isDefault) return true;

        if (noxSkins != null)
            foreach (var skin in noxSkins)
                if (skin.skinId == skinId && skin.isDefault) return true;

        return false;
    }

    private void ApplySkinToActivePlayer(PlayerController.PlayerType playerType)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.Type == playerType)
            {
                ApplySkin(player);
                break;
            }
        }
    }

    private string GetSkinDisplayName(SkinData skin)
    {
        if (LocalizationSystem.Instance != null && !string.IsNullOrEmpty(skin.displayNameKey))
            return LocalizationSystem.Instance.Get(skin.displayNameKey, skin.skinId);
        return skin.skinId;
    }

    private void OnShopPurchase(ShopPurchaseEvent e)
    {
        if (e.category == "skin")
        {
            UnlockSkin(e.itemId);
        }
    }
}

/// <summary>
/// 皮肤信息（用于UI显示）
/// </summary>
public class SkinInfo
{
    public string skinId;
    public string displayName;
    public Sprite portrait;
    public Color primaryColor;
    public bool isOwned;
    public bool isEquipped;
    public bool isDefault;
}
