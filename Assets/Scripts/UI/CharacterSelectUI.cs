using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 角色选择界面 - 展示Lux和Nox的详细信息和皮肤预览
/// 双人本地模式时两个玩家分别选择角色
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    [Header("面板")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button backButton;

    [Header("Lux信息")]
    [SerializeField] private Image luxPortrait;
    [SerializeField] private Button luxSelectButton;
    [SerializeField] private TextMeshProUGUI luxNameText;
    [SerializeField] private TextMeshProUGUI luxDescText;
    [SerializeField] private TextMeshProUGUI luxAbility1Text;
    [SerializeField] private TextMeshProUGUI luxAbility2Text;
    [SerializeField] private Image luxHighlight;

    [Header("Nox信息")]
    [SerializeField] private Image noxPortrait;
    [SerializeField] private Button noxSelectButton;
    [SerializeField] private TextMeshProUGUI noxNameText;
    [SerializeField] private TextMeshProUGUI noxDescText;
    [SerializeField] private TextMeshProUGUI noxAbility1Text;
    [SerializeField] private TextMeshProUGUI noxAbility2Text;
    [SerializeField] private Image noxHighlight;

    [Header("属性对比")]
    [SerializeField] private Slider luxSpeedBar;
    [SerializeField] private Slider luxPowerBar;
    [SerializeField] private Slider luxDefenseBar;
    [SerializeField] private Slider noxSpeedBar;
    [SerializeField] private Slider noxPowerBar;
    [SerializeField] private Slider noxDefenseBar;

    [Header("皮肤预览")]
    [SerializeField] private Button luxSkinButton;
    [SerializeField] private Button noxSkinButton;
    [SerializeField] private TextMeshProUGUI luxSkinName;
    [SerializeField] private TextMeshProUGUI noxSkinName;

    [Header("玩家分配")]
    [SerializeField] private TextMeshProUGUI player1Label;
    [SerializeField] private TextMeshProUGUI player2Label;
    [SerializeField] private Image player1CharIcon;
    [SerializeField] private Image player2CharIcon;

    [Header("动画")]
    [SerializeField] private float portraitPunchScale = 1.15f;
    [SerializeField] private float selectAnimDuration = 0.3f;

    [Header("颜色")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f, 0.5f);
    [SerializeField] private Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);

    // 属性值（归一化0~1）
    private static readonly float[] LUX_STATS = { 0.7f, 0.5f, 0.6f };  // speed, power, defense
    private static readonly float[] NOX_STATS = { 0.8f, 0.7f, 0.4f };

    // 状态
    private int player1Selection; // 0=Lux, 1=Nox
    private int player2Selection; // 自动对调

    public event System.Action<int, int> OnCharactersConfirmed; // player1Type, player2Type

    void Awake()
    {
        if (luxSelectButton != null) luxSelectButton.onClick.AddListener(() => SelectCharacter(0));
        if (noxSelectButton != null) noxSelectButton.onClick.AddListener(() => SelectCharacter(1));
        if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmSelection);
        if (backButton != null) backButton.onClick.AddListener(Hide);
        if (luxSkinButton != null) luxSkinButton.onClick.AddListener(() => OpenSkinPreview(0));
        if (noxSkinButton != null) noxSkinButton.onClick.AddListener(() => OpenSkinPreview(1));

        if (panel != null) panel.SetActive(false);
    }

    // ==================== 公共接口 ====================

    public void Show()
    {
        if (panel != null) panel.SetActive(true);
        player1Selection = 0; // 默认P1=Lux
        player2Selection = 1; // P2=Nox

        PopulateCharacterInfo();
        UpdateSelection();
        AnimateStatsSliders();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    // ==================== 选择逻辑 ====================

    private void SelectCharacter(int charIndex)
    {
        player1Selection = charIndex;
        player2Selection = 1 - charIndex; // 另一个角色

        UpdateSelection();
        StartCoroutine(PunchPortrait(charIndex == 0 ? luxPortrait : noxPortrait));

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_select");

        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Light();
    }

    private void ConfirmSelection()
    {
        OnCharactersConfirmed?.Invoke(player1Selection, player2Selection);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("ui_confirm");

        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Medium();

        Hide();
    }

    // ==================== 显示更新 ====================

    private void PopulateCharacterInfo()
    {
        // Lux信息
        if (luxNameText != null)
        {
            luxNameText.text = "Lux";
            if (LocalizationSystem.Instance != null)
                luxNameText.text = LocalizationSystem.Instance.Get("char_lux_name", "Lux");
        }

        if (luxDescText != null)
        {
            string desc = "Master of light. Illuminates the path and attacks from range.";
            if (LocalizationSystem.Instance != null)
                desc = LocalizationSystem.Instance.Get("char_lux_desc", desc);
            luxDescText.text = desc;
        }

        if (luxAbility1Text != null)
        {
            string a1 = "Light Orb: Ranged projectile attack";
            if (LocalizationSystem.Instance != null)
                a1 = LocalizationSystem.Instance.Get("char_lux_ability1", a1);
            luxAbility1Text.text = a1;
        }

        if (luxAbility2Text != null)
        {
            string a2 = "Double Jump: Jump again mid-air";
            if (LocalizationSystem.Instance != null)
                a2 = LocalizationSystem.Instance.Get("char_lux_ability2", a2);
            luxAbility2Text.text = a2;
        }

        // Nox信息
        if (noxNameText != null)
        {
            noxNameText.text = "Nox";
            if (LocalizationSystem.Instance != null)
                noxNameText.text = LocalizationSystem.Instance.Get("char_nox_name", "Nox");
        }

        if (noxDescText != null)
        {
            string desc = "Shadow walker. Passes through barriers and strikes up close.";
            if (LocalizationSystem.Instance != null)
                desc = LocalizationSystem.Instance.Get("char_nox_desc", desc);
            noxDescText.text = desc;
        }

        if (noxAbility1Text != null)
        {
            string a1 = "Shadow Dash: Phase through obstacles";
            if (LocalizationSystem.Instance != null)
                a1 = LocalizationSystem.Instance.Get("char_nox_ability1", a1);
            noxAbility1Text.text = a1;
        }

        if (noxAbility2Text != null)
        {
            string a2 = "Shadow Cloak: Brief invisibility";
            if (LocalizationSystem.Instance != null)
                a2 = LocalizationSystem.Instance.Get("char_nox_ability2", a2);
            noxAbility2Text.text = a2;
        }

        // 属性条
        SetStatsSliders(luxSpeedBar, luxPowerBar, luxDefenseBar, LUX_STATS);
        SetStatsSliders(noxSpeedBar, noxPowerBar, noxDefenseBar, NOX_STATS);

        // 皮肤名
        UpdateSkinNames();
    }

    private void SetStatsSliders(Slider speed, Slider power, Slider defense, float[] values)
    {
        if (speed != null) speed.value = values[0];
        if (power != null) power.value = values[1];
        if (defense != null) defense.value = values[2];
    }

    private void UpdateSelection()
    {
        // 高亮
        bool luxSelected = player1Selection == 0;

        if (luxHighlight != null)
            luxHighlight.color = luxSelected ? selectedColor : unselectedColor;
        if (noxHighlight != null)
            noxHighlight.color = !luxSelected ? selectedColor : unselectedColor;

        // 玩家分配显示
        if (player1Label != null)
            player1Label.text = player1Selection == 0 ? "P1: Lux" : "P1: Nox";
        if (player2Label != null)
            player2Label.text = player2Selection == 0 ? "P2: Lux" : "P2: Nox";

        // 玩家图标
        if (player1CharIcon != null)
        {
            Sprite icon = player1Selection == 0 ? luxPortrait?.sprite : noxPortrait?.sprite;
            if (icon != null) player1CharIcon.sprite = icon;
        }
        if (player2CharIcon != null)
        {
            Sprite icon = player2Selection == 0 ? luxPortrait?.sprite : noxPortrait?.sprite;
            if (icon != null) player2CharIcon.sprite = icon;
        }
    }

    private void UpdateSkinNames()
    {
        if (SkinManager.Instance == null) return;

        if (luxSkinName != null)
        {
            var skin = SkinManager.Instance.GetEquippedSkin(PlayerController.PlayerType.Lux);
            luxSkinName.text = skin != null ? skin.skinId : "Default";
        }

        if (noxSkinName != null)
        {
            var skin = SkinManager.Instance.GetEquippedSkin(PlayerController.PlayerType.Nox);
            noxSkinName.text = skin != null ? skin.skinId : "Default";
        }
    }

    private void OpenSkinPreview(int charIndex)
    {
        // 跳转到皮肤选择（ShopUI的皮肤页签）
        var shop = FindAnyObjectByType<ShopUI>();
        if (shop != null)
            shop.Show();
    }

    // ==================== 动画 ====================

    private void AnimateStatsSliders()
    {
        StartCoroutine(AnimateSlider(luxSpeedBar, LUX_STATS[0]));
        StartCoroutine(AnimateSlider(luxPowerBar, LUX_STATS[1]));
        StartCoroutine(AnimateSlider(luxDefenseBar, LUX_STATS[2]));
        StartCoroutine(AnimateSlider(noxSpeedBar, NOX_STATS[0]));
        StartCoroutine(AnimateSlider(noxPowerBar, NOX_STATS[1]));
        StartCoroutine(AnimateSlider(noxDefenseBar, NOX_STATS[2]));
    }

    private IEnumerator AnimateSlider(Slider slider, float target)
    {
        if (slider == null) yield break;

        slider.value = 0;
        float t = 0;
        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            slider.value = Mathf.Lerp(0, target, t / 0.5f);
            yield return null;
        }
        slider.value = target;
    }

    private IEnumerator PunchPortrait(Image portrait)
    {
        if (portrait == null) yield break;

        var rt = portrait.rectTransform;
        Vector3 originalScale = Vector3.one;

        float t = 0;
        float half = selectAnimDuration * 0.5f;

        // 放大
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float p = t / half;
            rt.localScale = Vector3.Lerp(originalScale, originalScale * portraitPunchScale, p);
            yield return null;
        }

        // 缩回
        t = 0;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float p = t / half;
            rt.localScale = Vector3.Lerp(originalScale * portraitPunchScale, originalScale, p);
            yield return null;
        }

        rt.localScale = originalScale;
    }
}
