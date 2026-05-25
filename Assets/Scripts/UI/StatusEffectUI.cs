using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 状态效果UI - 在HUD上显示玩家当前的增减益图标
/// 每个效果显示为带冷却遮罩的小图标
/// 支持双人分别显示
/// </summary>
public class StatusEffectUI : MonoBehaviour
{
    [Header("P1 (Lux) 效果槽")]
    [SerializeField] private RectTransform p1EffectContainer;
    [SerializeField] private int p1MaxSlots = 5;

    [Header("P2 (Nox) 效果槽")]
    [SerializeField] private RectTransform p2EffectContainer;
    [SerializeField] private int p2MaxSlots = 5;

    [Header("效果图标模板")]
    [SerializeField] private GameObject effectSlotPrefab;

    [Header("图标映射")]
    [SerializeField] private EffectIconEntry[] effectIcons;

    [System.Serializable]
    public class EffectIconEntry
    {
        public PlayerStatusEffect.EffectType type;
        public Sprite icon;
        public Color borderColor = Color.white;
    }

    // 运行时
    private PlayerStatusEffect[] playerEffects = new PlayerStatusEffect[2];
    private List<EffectSlot>[] slots = new List<EffectSlot>[2];

    private class EffectSlot
    {
        public GameObject gameObject;
        public Image iconImage;
        public Image cooldownFill;
        public Image borderImage;
        public TextMeshProUGUI stackText;
        public string currentEffectId;
    }

    void Start()
    {
        slots[0] = new List<EffectSlot>();
        slots[1] = new List<EffectSlot>();

        CreateSlots(0, p1EffectContainer, p1MaxSlots);
        CreateSlots(1, p2EffectContainer, p2MaxSlots);

        FindPlayerEffects();
    }

    void Update()
    {
        if (playerEffects[0] == null && playerEffects[1] == null)
            FindPlayerEffects();

        UpdatePlayerSlots(0);
        UpdatePlayerSlots(1);
    }

    // ==================== 创建UI ====================

    private void CreateSlots(int playerIndex, RectTransform container, int maxSlots)
    {
        if (container == null || effectSlotPrefab == null) return;

        for (int i = 0; i < maxSlots; i++)
        {
            var go = Instantiate(effectSlotPrefab, container);
            go.SetActive(false);

            var slot = new EffectSlot
            {
                gameObject = go,
                iconImage = go.transform.Find("Icon")?.GetComponent<Image>(),
                cooldownFill = go.transform.Find("CooldownFill")?.GetComponent<Image>(),
                borderImage = go.transform.Find("Border")?.GetComponent<Image>(),
                stackText = go.GetComponentInChildren<TextMeshProUGUI>()
            };

            slots[playerIndex].Add(slot);
        }
    }

    // ==================== 更新 ====================

    private void UpdatePlayerSlots(int playerIndex)
    {
        if (playerEffects[playerIndex] == null) return;

        var effects = playerEffects[playerIndex].GetActiveEffects();
        var playerSlots = slots[playerIndex];

        for (int i = 0; i < playerSlots.Count; i++)
        {
            if (i < effects.Count)
            {
                var effect = effects[i];
                var slot = playerSlots[i];

                slot.gameObject.SetActive(true);
                slot.currentEffectId = effect.id;

                // 图标
                if (slot.iconImage != null)
                {
                    slot.iconImage.sprite = GetIcon(effect.type);
                    slot.iconImage.color = effect.tintColor != default ? effect.tintColor : Color.white;
                }

                // 冷却遮罩
                if (slot.cooldownFill != null && !effect.isPermanent)
                {
                    float progress = effect.remainingTime / effect.duration;
                    slot.cooldownFill.fillAmount = 1f - progress;
                }

                // 边框颜色
                if (slot.borderImage != null)
                {
                    slot.borderImage.color = GetBorderColor(effect.type);

                    // 快过期时闪烁
                    if (!effect.isPermanent && effect.remainingTime < 3f)
                    {
                        float pulse = (Mathf.Sin(Time.time * 5f) + 1f) * 0.5f;
                        Color c = slot.borderImage.color;
                        c.a = Mathf.Lerp(0.3f, 1f, pulse);
                        slot.borderImage.color = c;
                    }
                }

                // 叠加层数
                if (slot.stackText != null)
                {
                    if (effect.stackCount > 1)
                    {
                        slot.stackText.text = $"x{effect.stackCount}";
                        slot.stackText.gameObject.SetActive(true);
                    }
                    else
                    {
                        slot.stackText.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                playerSlots[i].gameObject.SetActive(false);
                playerSlots[i].currentEffectId = null;
            }
        }
    }

    // ==================== 辅助 ====================

    private void FindPlayerEffects()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            var statusEffect = p.GetComponent<PlayerStatusEffect>();
            if (statusEffect != null && p.PlayerIndex >= 0 && p.PlayerIndex < 2)
            {
                playerEffects[p.PlayerIndex] = statusEffect;
            }
        }
    }

    private Sprite GetIcon(PlayerStatusEffect.EffectType type)
    {
        if (effectIcons == null) return null;
        foreach (var entry in effectIcons)
        {
            if (entry.type == type)
                return entry.icon;
        }
        return null;
    }

    private Color GetBorderColor(PlayerStatusEffect.EffectType type)
    {
        if (effectIcons != null)
        {
            foreach (var entry in effectIcons)
            {
                if (entry.type == type)
                    return entry.borderColor;
            }
        }

        // 默认：正面=绿色，负面=红色
        switch (type)
        {
            case PlayerStatusEffect.EffectType.SpeedBoost:
            case PlayerStatusEffect.EffectType.DamageBoost:
            case PlayerStatusEffect.EffectType.Shield:
            case PlayerStatusEffect.EffectType.Regeneration:
            case PlayerStatusEffect.EffectType.LightAura:
            case PlayerStatusEffect.EffectType.DamageReduction:
                return new Color(0.3f, 1f, 0.4f);

            case PlayerStatusEffect.EffectType.ShadowCloak:
                return new Color(0.5f, 0.2f, 0.8f);

            default:
                return new Color(1f, 0.3f, 0.2f);
        }
    }
}
