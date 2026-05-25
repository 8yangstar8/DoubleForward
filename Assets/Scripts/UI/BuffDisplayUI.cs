using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 增益显示UI - 显示玩家当前活跃的增益/减益图标
/// 图标上显示剩余时间，快到期时闪烁
/// 支持双人分别显示
/// </summary>
public class BuffDisplayUI : MonoBehaviour
{
    [Header("容器")]
    [SerializeField] private RectTransform player1BuffContainer;
    [SerializeField] private RectTransform player2BuffContainer;
    [SerializeField] private GameObject buffIconPrefab;

    [Header("样式")]
    [SerializeField] private Color buffColor = new Color(0.3f, 0.8f, 1f);
    [SerializeField] private Color debuffColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private float expiringThreshold = 3f;      // 快到期闪烁阈值
    [SerializeField] private float flashSpeed = 5f;

    [Header("默认图标")]
    [SerializeField] private Sprite speedIcon;
    [SerializeField] private Sprite damageIcon;
    [SerializeField] private Sprite shieldIcon;
    [SerializeField] private Sprite invincibleIcon;
    [SerializeField] private Sprite magnetIcon;
    [SerializeField] private Sprite regenIcon;
    [SerializeField] private Sprite slowIcon;
    [SerializeField] private Sprite poisonIcon;
    [SerializeField] private Sprite frozenIcon;
    [SerializeField] private Sprite fireIcon;
    [SerializeField] private Sprite jumpIcon;
    [SerializeField] private Sprite defaultBuffIcon;

    // 运行时
    private PlayerBuffSystem[] playerBuffSystems = new PlayerBuffSystem[2];
    private Dictionary<string, BuffIconEntry>[] iconMaps;

    private class BuffIconEntry
    {
        public GameObject gameObject;
        public Image iconImage;
        public Image timerFill;
        public TextMeshProUGUI stackText;
        public CanvasGroup canvasGroup;
    }

    void Start()
    {
        iconMaps = new Dictionary<string, BuffIconEntry>[2];
        iconMaps[0] = new Dictionary<string, BuffIconEntry>();
        iconMaps[1] = new Dictionary<string, BuffIconEntry>();

        FindBuffSystems();
    }

    void Update()
    {
        for (int i = 0; i < 2; i++)
        {
            if (playerBuffSystems[i] == null) continue;
            UpdatePlayerBuffs(i);
        }
    }

    // ==================== 更新逻辑 ====================

    private void UpdatePlayerBuffs(int playerIndex)
    {
        var system = playerBuffSystems[playerIndex];
        var map = iconMaps[playerIndex];
        var container = playerIndex == 0 ? player1BuffContainer : player2BuffContainer;

        if (system == null || map == null || container == null) return;

        // 标记现有的为"待清理"
        var toRemove = new List<string>(map.Keys);

        foreach (var buff in system.Buffs)
        {
            string key = $"{buff.type}_{buff.source}";
            toRemove.Remove(key);

            if (!map.ContainsKey(key))
            {
                // 创建新图标
                CreateBuffIcon(playerIndex, key, buff, container);
            }

            // 更新图标
            UpdateBuffIcon(map[key], buff);
        }

        // 移除过期的图标
        foreach (var key in toRemove)
        {
            if (map.ContainsKey(key))
            {
                if (map[key].gameObject != null)
                    Destroy(map[key].gameObject);
                map.Remove(key);
            }
        }
    }

    private void CreateBuffIcon(int playerIndex, string key,
        PlayerBuffSystem.ActiveBuff buff, RectTransform container)
    {
        if (buffIconPrefab == null) return;

        var go = Instantiate(buffIconPrefab, container);
        var images = go.GetComponentsInChildren<Image>();
        var texts = go.GetComponentsInChildren<TextMeshProUGUI>();

        var entry = new BuffIconEntry
        {
            gameObject = go,
            iconImage = images.Length > 0 ? images[0] : null,
            timerFill = images.Length > 1 ? images[1] : null,
            stackText = texts.Length > 0 ? texts[0] : null,
            canvasGroup = go.GetComponent<CanvasGroup>()
        };

        if (entry.canvasGroup == null)
            entry.canvasGroup = go.AddComponent<CanvasGroup>();

        // 设置图标
        if (entry.iconImage != null)
        {
            Sprite icon = buff.icon != null ? buff.icon : GetDefaultIcon(buff.type);
            if (icon != null) entry.iconImage.sprite = icon;
            entry.iconImage.color = buff.IsDebuff ? debuffColor : buffColor;
        }

        iconMaps[playerIndex][key] = entry;
    }

    private void UpdateBuffIcon(BuffIconEntry entry, PlayerBuffSystem.ActiveBuff buff)
    {
        if (entry == null) return;

        // 计时器填充
        if (entry.timerFill != null)
            entry.timerFill.fillAmount = buff.NormalizedTime;

        // 层数文本
        if (entry.stackText != null)
        {
            if (buff.stacks > 1)
            {
                entry.stackText.gameObject.SetActive(true);
                entry.stackText.text = $"x{buff.stacks}";
            }
            else
            {
                entry.stackText.gameObject.SetActive(false);
            }
        }

        // 快到期闪烁
        if (entry.canvasGroup != null && buff.remaining <= expiringThreshold)
        {
            float alpha = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * flashSpeed));
            entry.canvasGroup.alpha = alpha;
        }
        else if (entry.canvasGroup != null)
        {
            entry.canvasGroup.alpha = 1f;
        }
    }

    // ==================== 辅助 ====================

    private Sprite GetDefaultIcon(PlayerBuffSystem.BuffType type)
    {
        return type switch
        {
            PlayerBuffSystem.BuffType.SpeedBoost => speedIcon,
            PlayerBuffSystem.BuffType.SpeedDown => slowIcon,
            PlayerBuffSystem.BuffType.JumpBoost => jumpIcon,
            PlayerBuffSystem.BuffType.DamageBoost => damageIcon,
            PlayerBuffSystem.BuffType.DamageDown => damageIcon,
            PlayerBuffSystem.BuffType.Shield => shieldIcon,
            PlayerBuffSystem.BuffType.Invincibility => invincibleIcon,
            PlayerBuffSystem.BuffType.Magnetism => magnetIcon,
            PlayerBuffSystem.BuffType.Regeneration => regenIcon,
            PlayerBuffSystem.BuffType.Poisoned => poisonIcon,
            PlayerBuffSystem.BuffType.Frozen => frozenIcon,
            PlayerBuffSystem.BuffType.Burning => fireIcon,
            _ => defaultBuffIcon
        };
    }

    private void FindBuffSystems()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            int idx = p.PlayerIndex;
            if (idx >= 0 && idx < 2)
            {
                playerBuffSystems[idx] = p.GetComponent<PlayerBuffSystem>();

                // 订阅变化事件
                if (playerBuffSystems[idx] != null)
                {
                    // 事件驱动可在这里增加动画效果
                }
            }
        }
    }
}
