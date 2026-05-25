using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class TutorialTrigger : MonoBehaviour
{
    [SerializeField] private string tutorialStepId;
    [SerializeField] private bool triggerOnce = true;

    private bool triggered;

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered && triggerOnce) return;
        if (other.GetComponent<PlayerController>() == null) return;

        triggered = true;

        // 优先使用TutorialFlowManager（结构化教程流程）
        if (TutorialFlowManager.Instance != null)
            TutorialFlowManager.Instance.TriggerStep(tutorialStepId);

        // 备用：旧版TutorialSystem
        TutorialSystem.Instance?.ShowStep(tutorialStepId);

        if (triggerOnce)
            gameObject.SetActive(false);
    }
}
