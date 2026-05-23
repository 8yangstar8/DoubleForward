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
        TutorialSystem.Instance?.ShowStep(tutorialStepId);

        if (triggerOnce)
            gameObject.SetActive(false);
    }
}
