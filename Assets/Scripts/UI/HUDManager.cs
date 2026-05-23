using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("Player 1 (Lux)")]
    [SerializeField] private Image[] healthIconsP1;
    [SerializeField] private Image skill1CooldownP1;
    [SerializeField] private TextMeshProUGUI playerNameP1;

    [Header("Player 2 (Nox)")]
    [SerializeField] private Image[] healthIconsP2;
    [SerializeField] private Image skill1CooldownP2;
    [SerializeField] private TextMeshProUGUI playerNameP2;

    [Header("Level Info")]
    [SerializeField] private TextMeshProUGUI levelNameText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI collectibleText;

    private PlayerHealth healthP1;
    private PlayerHealth healthP2;
    private PlayerAbilityBase abilityP1;
    private PlayerAbilityBase abilityP2;

    void Start()
    {
        var levelManager = LevelManager.Instance;
        if (levelManager != null)
        {
            levelManager.OnLevelStart += OnLevelStart;
            levelManager.OnCollectibleGathered += UpdateCollectibles;
        }
    }

    private void OnLevelStart()
    {
        var levelManager = LevelManager.Instance;
        if (levelManager == null) return;

        if (levelManager.LuxPlayer != null)
        {
            healthP1 = levelManager.LuxPlayer.GetComponent<PlayerHealth>();
            abilityP1 = levelManager.LuxPlayer.GetComponent<PlayerAbilityBase>();
            if (healthP1 != null) healthP1.OnHealthChanged += (hp) => UpdateHealthIcons(healthIconsP1, hp);
        }

        if (levelManager.NoxPlayer != null)
        {
            healthP2 = levelManager.NoxPlayer.GetComponent<PlayerHealth>();
            abilityP2 = levelManager.NoxPlayer.GetComponent<PlayerAbilityBase>();
            if (healthP2 != null) healthP2.OnHealthChanged += (hp) => UpdateHealthIcons(healthIconsP2, hp);
        }

        if (levelNameText != null && levelManager.CurrentLevel != null)
            levelNameText.text = levelManager.CurrentLevel.DisplayName;
    }

    void Update()
    {
        UpdateCooldownUI(skill1CooldownP1, abilityP1);
        UpdateCooldownUI(skill1CooldownP2, abilityP2);
        UpdateTimer();
    }

    private void UpdateHealthIcons(Image[] icons, int currentHealth)
    {
        if (icons == null) return;
        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] != null)
                icons[i].enabled = i < currentHealth;
        }
    }

    private void UpdateCooldownUI(Image cooldownImage, PlayerAbilityBase ability)
    {
        if (cooldownImage == null || ability == null) return;
        cooldownImage.fillAmount = ability.CooldownProgress;
    }

    private void UpdateTimer()
    {
        if (timerText == null) return;
        float time = LevelManager.Instance?.GetLevelTime() ?? 0f;
        int minutes = (int)(time / 60);
        int seconds = (int)(time % 60);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void UpdateCollectibles(int count)
    {
        if (collectibleText == null) return;
        int total = LevelManager.Instance?.GetTotalCollectibles() ?? 0;
        collectibleText.text = $"{count}/{total}";
    }
}
