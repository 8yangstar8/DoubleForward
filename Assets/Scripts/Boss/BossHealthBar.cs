using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class BossHealthBar : MonoBehaviour
{
    [SerializeField] private GameObject healthBarPanel;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image healthFillDelay;
    [SerializeField] private TextMeshProUGUI bossNameText;
    [SerializeField] private TextMeshProUGUI phaseText;
    [SerializeField] private float delaySpeed = 2f;
    [SerializeField] private float showAnimDuration = 0.5f;

    private BossBase boss;
    private float targetFill;
    private float delayFill;

    public void Initialize(BossBase bossRef, string name)
    {
        boss = bossRef;

        if (bossNameText != null) bossNameText.text = name;

        boss.OnHealthChanged += UpdateHealthBar;
        boss.OnPhaseChanged += UpdatePhase;
        boss.OnBossDefeated += OnDefeated;
        boss.OnBattleStart += Show;

        if (healthBarPanel != null) healthBarPanel.SetActive(false);
        targetFill = 1f;
        delayFill = 1f;
    }

    private void Show()
    {
        if (healthBarPanel != null)
        {
            healthBarPanel.SetActive(true);
            StartCoroutine(ShowAnimation());
        }
    }

    private IEnumerator ShowAnimation()
    {
        var rt = healthBarPanel.GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector3 startPos = rt.anchoredPosition + Vector2.up * 100;
        Vector3 endPos = rt.anchoredPosition;
        rt.anchoredPosition = startPos;

        float t = 0;
        while (t < showAnimDuration)
        {
            t += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t / showAnimDuration);
            yield return null;
        }
        rt.anchoredPosition = endPos;
    }

    private void UpdateHealthBar(int current, int max)
    {
        targetFill = (float)current / max;
        if (healthFill != null)
            healthFill.fillAmount = targetFill;

        // 颜色渐变：绿 → 黄 → 红
        if (healthFill != null)
        {
            if (targetFill > 0.5f)
                healthFill.color = Color.Lerp(Color.yellow, Color.green, (targetFill - 0.5f) * 2f);
            else
                healthFill.color = Color.Lerp(Color.red, Color.yellow, targetFill * 2f);
        }
    }

    private void UpdatePhase(int phase)
    {
        if (phaseText != null)
        {
            string[] phaseNames = { "Phase I", "Phase II", "Phase III" };
            phaseText.text = phase < phaseNames.Length ? phaseNames[phase] : $"Phase {phase + 1}";
        }

        StartCoroutine(PhaseFlash());
    }

    private IEnumerator PhaseFlash()
    {
        if (healthFill == null) yield break;
        for (int i = 0; i < 3; i++)
        {
            healthFill.color = Color.white;
            yield return new WaitForSeconds(0.15f);
            UpdateHealthBar(boss.CurrentHealth, 20);
            yield return new WaitForSeconds(0.15f);
        }
    }

    void Update()
    {
        if (healthFillDelay == null) return;
        delayFill = Mathf.Lerp(delayFill, targetFill, delaySpeed * Time.deltaTime);
        healthFillDelay.fillAmount = delayFill;
    }

    private void OnDefeated()
    {
        StartCoroutine(HideAnimation());
    }

    private IEnumerator HideAnimation()
    {
        yield return new WaitForSeconds(1f);
        if (healthBarPanel != null)
            healthBarPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (boss != null)
        {
            boss.OnHealthChanged -= UpdateHealthBar;
            boss.OnPhaseChanged -= UpdatePhase;
            boss.OnBossDefeated -= OnDefeated;
            boss.OnBattleStart -= Show;
        }
    }
}
