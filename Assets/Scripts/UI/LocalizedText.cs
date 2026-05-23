using UnityEngine;
using TMPro;

/// <summary>
/// 挂载到TextMeshPro组件上，自动根据语言更新文本内容
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string localizationKey;
    [SerializeField] private string defaultValue;

    private TextMeshProUGUI textComponent;

    void Awake()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        UpdateText();
        if (LocalizationSystem.Instance != null)
            LocalizationSystem.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    void OnDisable()
    {
        if (LocalizationSystem.Instance != null)
            LocalizationSystem.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(LocalizationSystem.Language lang)
    {
        UpdateText();
    }

    public void SetKey(string key, string fallback = null)
    {
        localizationKey = key;
        defaultValue = fallback;
        UpdateText();
    }

    private void UpdateText()
    {
        if (textComponent == null || LocalizationSystem.Instance == null) return;
        if (string.IsNullOrEmpty(localizationKey)) return;

        textComponent.text = LocalizationSystem.Instance.Get(localizationKey, defaultValue);
    }
}
