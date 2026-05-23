using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class AchievementNotificationUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image iconImage;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float slideDistance = 200f;
    [SerializeField] private float animDuration = 0.4f;
    [SerializeField] private float displayDuration = 3f;

    public void Show(string title, string description, Sprite icon)
    {
        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = description;
        if (iconImage != null && icon != null) iconImage.sprite = icon;

        StartCoroutine(AnimateNotification());
    }

    private IEnumerator AnimateNotification()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null || canvasGroup == null) yield break;

        Vector2 startPos = rt.anchoredPosition + Vector2.up * slideDistance;
        Vector2 endPos = rt.anchoredPosition;

        // 滑入
        rt.anchoredPosition = startPos;
        canvasGroup.alpha = 0;

        float t = 0;
        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = EaseOutBack(t / animDuration);
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, p);
            canvasGroup.alpha = Mathf.Lerp(0, 1, t / animDuration);
            yield return null;
        }

        rt.anchoredPosition = endPos;
        canvasGroup.alpha = 1;

        yield return new WaitForSecondsRealtime(displayDuration);

        // 滑出
        t = 0;
        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / animDuration;
            rt.anchoredPosition = Vector2.Lerp(endPos, startPos, p);
            canvasGroup.alpha = 1f - p;
            yield return null;
        }
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1;
        return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
    }
}
