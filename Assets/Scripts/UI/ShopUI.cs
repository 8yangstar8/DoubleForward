using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 商店界面 - 使用金币/宝石购买皮肤、道具、解锁内容
/// 支持分类标签页、购买确认、已拥有标记
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Button closeButton;

    [Header("货币显示")]
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI gemsText;

    [Header("分类标签")]
    [SerializeField] private Button skinsTab;
    [SerializeField] private Button itemsTab;
    [SerializeField] private Button specialTab;
    [SerializeField] private Image skinsTabBg;
    [SerializeField] private Image itemsTabBg;
    [SerializeField] private Image specialTabBg;
    [SerializeField] private Color activeTabColor = Color.white;
    [SerializeField] private Color inactiveTabColor = new Color(0.6f, 0.6f, 0.6f);

    [Header("商品列表")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private GameObject shopItemPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Header("购买确认")]
    [SerializeField] private GameObject confirmDialog;
    [SerializeField] private TextMeshProUGUI confirmItemName;
    [SerializeField] private TextMeshProUGUI confirmPrice;
    [SerializeField] private Image confirmItemIcon;
    [SerializeField] private Button confirmYes;
    [SerializeField] private Button confirmNo;

    [Header("商品数据")]
    [SerializeField] private ShopItemData[] shopItems;

    [System.Serializable]
    public class ShopItemData
    {
        public string itemId;
        public string nameKey;         // 本地化key
        public string descriptionKey;
        public Sprite icon;
        public ShopCategory category;
        public CurrencyType currencyType;
        public int price;
        public bool isLimited;         // 限购
        public int requiredLevel;      // 解锁条件
    }

    public enum ShopCategory { Skins, Items, Special }
    public enum CurrencyType { Coins, Gems }

    private ShopCategory currentCategory = ShopCategory.Skins;
    private List<GameObject> spawnedItems = new List<GameObject>();
    private ShopItemData pendingPurchase;

    // 已购买记录
    private const string PURCHASED_PREFIX = "shop_purchased_";

    void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (skinsTab != null) skinsTab.onClick.AddListener(() => SelectCategory(ShopCategory.Skins));
        if (itemsTab != null) itemsTab.onClick.AddListener(() => SelectCategory(ShopCategory.Items));
        if (specialTab != null) specialTab.onClick.AddListener(() => SelectCategory(ShopCategory.Special));
        if (confirmYes != null) confirmYes.onClick.AddListener(ConfirmPurchase);
        if (confirmNo != null) confirmNo.onClick.AddListener(CancelPurchase);

        if (shopPanel != null) shopPanel.SetActive(false);
        if (confirmDialog != null) confirmDialog.SetActive(false);
    }

    void OnEnable()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged += OnCurrencyChanged;
            CurrencyManager.Instance.OnGemsChanged += OnCurrencyChanged;
        }
    }

    void OnDisable()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged -= OnCurrencyChanged;
            CurrencyManager.Instance.OnGemsChanged -= OnCurrencyChanged;
        }
    }

    // ==================== 公共接口 ====================

    public void Show()
    {
        if (shopPanel != null) shopPanel.SetActive(true);
        UpdateCurrencyDisplay();
        SelectCategory(ShopCategory.Skins);
    }

    public void Hide()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
        CancelPurchase();
    }

    // ==================== 分类切换 ====================

    private void SelectCategory(ShopCategory category)
    {
        currentCategory = category;

        // 更新标签视觉
        UpdateTabVisuals();

        // 刷新商品列表
        RefreshItemList();

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    private void UpdateTabVisuals()
    {
        if (skinsTabBg != null)
            skinsTabBg.color = currentCategory == ShopCategory.Skins ? activeTabColor : inactiveTabColor;
        if (itemsTabBg != null)
            itemsTabBg.color = currentCategory == ShopCategory.Items ? activeTabColor : inactiveTabColor;
        if (specialTabBg != null)
            specialTabBg.color = currentCategory == ShopCategory.Special ? activeTabColor : inactiveTabColor;
    }

    // ==================== 商品列表 ====================

    private void RefreshItemList()
    {
        // 清除旧商品
        foreach (var obj in spawnedItems)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedItems.Clear();

        if (shopItemPrefab == null || itemContainer == null || shopItems == null) return;

        foreach (var item in shopItems)
        {
            if (item.category != currentCategory) continue;

            var itemObj = Instantiate(shopItemPrefab, itemContainer);
            spawnedItems.Add(itemObj);

            SetupShopItemUI(itemObj, item);
        }

        // 滚动到顶部
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private void SetupShopItemUI(GameObject itemObj, ShopItemData data)
    {
        var texts = itemObj.GetComponentsInChildren<TextMeshProUGUI>();
        var images = itemObj.GetComponentsInChildren<Image>();
        var button = itemObj.GetComponentInChildren<Button>();

        bool isOwned = IsItemOwned(data.itemId);
        bool canAfford = CanAffordItem(data);
        bool isLocked = data.requiredLevel > GetPlayerLevel();

        // 设置名称
        if (texts.Length > 0)
        {
            string name = data.nameKey;
            if (LocalizationSystem.Instance != null)
                name = LocalizationSystem.Instance.Get(data.nameKey, data.nameKey);
            texts[0].text = name;
        }

        // 设置价格
        if (texts.Length > 1)
        {
            if (isOwned)
            {
                string ownedText = "Owned";
                if (LocalizationSystem.Instance != null)
                    ownedText = LocalizationSystem.Instance.Get("shop_owned", "Owned");
                texts[1].text = ownedText;
                texts[1].color = Color.green;
            }
            else if (isLocked)
            {
                string lockText = $"Lv.{data.requiredLevel}";
                texts[1].text = lockText;
                texts[1].color = Color.gray;
            }
            else
            {
                string currencyIcon = data.currencyType == CurrencyType.Coins ? "C" : "G";
                texts[1].text = $"{currencyIcon} {data.price}";
                texts[1].color = canAfford ? Color.white : Color.red;
            }
        }

        // 设置图标
        if (images.Length > 1 && data.icon != null)
        {
            images[1].sprite = data.icon;
            images[1].color = isLocked ? new Color(0.3f, 0.3f, 0.3f) : Color.white;
        }

        // 设置按钮
        if (button != null)
        {
            button.interactable = !isOwned && !isLocked && canAfford;
            var capturedData = data;
            button.onClick.AddListener(() => OnItemClicked(capturedData));
        }
    }

    // ==================== 购买流程 ====================

    private void OnItemClicked(ShopItemData item)
    {
        pendingPurchase = item;

        // 显示确认对话框
        if (confirmDialog != null) confirmDialog.SetActive(true);

        if (confirmItemName != null)
        {
            string name = item.nameKey;
            if (LocalizationSystem.Instance != null)
                name = LocalizationSystem.Instance.Get(item.nameKey, item.nameKey);
            confirmItemName.text = name;
        }

        if (confirmPrice != null)
        {
            string currencyName = item.currencyType == CurrencyType.Coins ? "Coins" : "Gems";
            if (LocalizationSystem.Instance != null)
                currencyName = LocalizationSystem.Instance.Get(
                    item.currencyType == CurrencyType.Coins ? "currency_coins" : "currency_gems",
                    currencyName);
            confirmPrice.text = $"{item.price} {currencyName}";
        }

        if (confirmItemIcon != null && item.icon != null)
            confirmItemIcon.sprite = item.icon;

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_click");
    }

    private void ConfirmPurchase()
    {
        if (pendingPurchase == null) return;

        bool success = false;

        if (pendingPurchase.currencyType == CurrencyType.Coins)
            success = CurrencyManager.Instance != null &&
                      CurrencyManager.Instance.SpendCoins(pendingPurchase.price, pendingPurchase.itemId);
        else
            success = CurrencyManager.Instance != null &&
                      CurrencyManager.Instance.SpendGems(pendingPurchase.price, pendingPurchase.itemId);

        if (success)
        {
            // 记录购买
            PlayerPrefs.SetInt(PURCHASED_PREFIX + pendingPurchase.itemId, 1);
            PlayerPrefs.Save();

            // 应用购买效果
            ApplyPurchaseEffect(pendingPurchase);

            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play("purchase_success");

            // 成就检查
            if (AchievementSystem.Instance != null)
                AchievementSystem.Instance.Unlock("first_purchase");
        }
        else
        {
            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play("purchase_fail");
        }

        if (confirmDialog != null) confirmDialog.SetActive(false);
        pendingPurchase = null;

        // 刷新列表
        RefreshItemList();
        UpdateCurrencyDisplay();
    }

    private void CancelPurchase()
    {
        pendingPurchase = null;
        if (confirmDialog != null) confirmDialog.SetActive(false);
    }

    private void ApplyPurchaseEffect(ShopItemData item)
    {
        // 根据商品类型应用效果
        switch (item.category)
        {
            case ShopCategory.Skins:
                // 换皮肤 - 通知SkinManager
                EventBus.Publish(new ShopPurchaseEvent
                {
                    itemId = item.itemId,
                    category = "skin"
                });
                break;

            case ShopCategory.Items:
                // 消耗品 - 添加到背包
                EventBus.Publish(new ShopPurchaseEvent
                {
                    itemId = item.itemId,
                    category = "item"
                });
                break;

            case ShopCategory.Special:
                // 特殊解锁
                EventBus.Publish(new ShopPurchaseEvent
                {
                    itemId = item.itemId,
                    category = "special"
                });
                break;
        }
    }

    // ==================== 辅助方法 ====================

    private bool IsItemOwned(string itemId)
    {
        return PlayerPrefs.GetInt(PURCHASED_PREFIX + itemId, 0) == 1;
    }

    private bool CanAffordItem(ShopItemData item)
    {
        if (CurrencyManager.Instance == null) return false;
        return item.currencyType == CurrencyType.Coins ?
            CurrencyManager.Instance.CanAffordCoins(item.price) :
            CurrencyManager.Instance.CanAffordGems(item.price);
    }

    private int GetPlayerLevel()
    {
        if (SaveSystem.Instance == null) return 1;
        return SaveSystem.Instance.Data.lastChapter;
    }

    private void UpdateCurrencyDisplay()
    {
        if (CurrencyManager.Instance == null) return;

        if (coinsText != null)
            coinsText.text = FormatNumber(CurrencyManager.Instance.Coins);
        if (gemsText != null)
            gemsText.text = FormatNumber(CurrencyManager.Instance.Gems);
    }

    private void OnCurrencyChanged(int newValue, int delta)
    {
        UpdateCurrencyDisplay();
    }

    private string FormatNumber(int num)
    {
        if (num >= 1000000) return $"{num / 1000000f:F1}M";
        if (num >= 1000) return $"{num / 1000f:F1}K";
        return num.ToString();
    }
}

// ShopPurchaseEvent定义在Core/EventBus.cs中
