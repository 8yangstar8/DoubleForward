using UnityEngine;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 多语言本地化系统 - 支持中文/英文/日文等多语言切换
/// 基于JSON语言包，运行时动态加载
/// </summary>
public class LocalizationSystem : MonoBehaviour
{
    public static LocalizationSystem Instance { get; private set; }

    public enum Language
    {
        ChineseSimplified,  // 简体中文
        ChineseTraditional, // 繁体中文
        English,            // 英语
        Japanese,           // 日语
        Korean              // 韩语
    }

    [SerializeField] private Language defaultLanguage = Language.ChineseSimplified;
    [SerializeField] private TextAsset[] languageFiles; // 按Language枚举顺序对应

    private Language currentLanguage;
    private Dictionary<string, string> localizedTexts = new Dictionary<string, string>();
    private Dictionary<Language, Dictionary<string, string>> cachedLanguages = new Dictionary<Language, Dictionary<string, string>>();

    private const string LANGUAGE_PREF_KEY = "game_language";

    public event System.Action<Language> OnLanguageChanged;

    public Language CurrentLanguage => currentLanguage;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSavedLanguage();
    }

    private void LoadSavedLanguage()
    {
        int savedLang = PlayerPrefs.GetInt(LANGUAGE_PREF_KEY, -1);
        if (savedLang >= 0 && savedLang < System.Enum.GetValues(typeof(Language)).Length)
            SetLanguage((Language)savedLang, false);
        else
            SetLanguage(DetectSystemLanguage(), false);
    }

    /// <summary>
    /// 根据系统语言自动检测
    /// </summary>
    private Language DetectSystemLanguage()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
                return Language.ChineseSimplified;
            case SystemLanguage.ChineseTraditional:
                return Language.ChineseTraditional;
            case SystemLanguage.English:
                return Language.English;
            case SystemLanguage.Japanese:
                return Language.Japanese;
            case SystemLanguage.Korean:
                return Language.Korean;
            default:
                return defaultLanguage;
        }
    }

    /// <summary>
    /// 切换语言
    /// </summary>
    public void SetLanguage(Language language, bool save = true)
    {
        currentLanguage = language;
        LoadLanguageData(language);

        if (save)
        {
            PlayerPrefs.SetInt(LANGUAGE_PREF_KEY, (int)language);
            PlayerPrefs.Save();
        }

        OnLanguageChanged?.Invoke(language);
    }

    private void LoadLanguageData(Language language)
    {
        // 尝试从缓存加载
        if (cachedLanguages.TryGetValue(language, out var cached))
        {
            localizedTexts = cached;
            return;
        }

        localizedTexts = new Dictionary<string, string>();

        int langIndex = (int)language;
        if (languageFiles == null || langIndex >= languageFiles.Length || languageFiles[langIndex] == null)
        {
            Debug.LogWarning($"[Localization] 语言文件未找到: {language}");
            return;
        }

        var jsonData = JsonUtility.FromJson<LocalizationData>(languageFiles[langIndex].text);
        if (jsonData != null && jsonData.entries != null)
        {
            foreach (var entry in jsonData.entries)
            {
                localizedTexts[entry.key] = entry.value;
            }
        }

        cachedLanguages[language] = new Dictionary<string, string>(localizedTexts);
    }

    /// <summary>
    /// 获取本地化文本
    /// </summary>
    /// <param name="key">文本键值</param>
    /// <param name="defaultValue">未找到时的默认值</param>
    /// <returns>本地化后的文本</returns>
    public string Get(string key, string defaultValue = null)
    {
        if (localizedTexts.TryGetValue(key, out string value))
            return value;

        if (defaultValue != null)
            return defaultValue;

        return $"[{key}]"; // 未找到时显示键名
    }

    /// <summary>
    /// Get的别名 - 便于语义化调用
    /// </summary>
    public string GetText(string key) => Get(key);

    /// <summary>
    /// 带参数的格式化本地化文本
    /// </summary>
    public string GetFormat(string key, params object[] args)
    {
        string template = Get(key);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }

    /// <summary>
    /// 检查键是否存在
    /// </summary>
    public bool HasKey(string key)
    {
        return localizedTexts.ContainsKey(key);
    }

    /// <summary>
    /// 获取语言显示名称
    /// </summary>
    public static string GetLanguageDisplayName(Language lang)
    {
        switch (lang)
        {
            case Language.ChineseSimplified: return "简体中文";
            case Language.ChineseTraditional: return "繁體中文";
            case Language.English: return "English";
            case Language.Japanese: return "日本語";
            case Language.Korean: return "한국어";
            default: return lang.ToString();
        }
    }

    /// <summary>
    /// 获取所有支持的语言
    /// </summary>
    public Language[] GetSupportedLanguages()
    {
        return (Language[])System.Enum.GetValues(typeof(Language));
    }

    [System.Serializable]
    private class LocalizationData
    {
        public List<LocalizationEntry> entries = new List<LocalizationEntry>();
    }

    [System.Serializable]
    private class LocalizationEntry
    {
        public string key;
        public string value;
    }
}
