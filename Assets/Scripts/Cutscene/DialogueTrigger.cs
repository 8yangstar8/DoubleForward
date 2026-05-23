using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class DialogueTrigger : MonoBehaviour
{
    [SerializeField] private DialogueSystem.DialogueSequence dialogue;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool requireBothPlayers;

    private bool triggered;
    private int playersInZone;

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered && triggerOnce) return;
        if (other.GetComponent<PlayerController>() == null) return;

        playersInZone++;

        if (!requireBothPlayers || playersInZone >= 2)
        {
            triggered = true;
            DialogueSystem.Instance?.StartDialogue(dialogue);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null)
            playersInZone = Mathf.Max(0, playersInZone - 1);
    }
}
