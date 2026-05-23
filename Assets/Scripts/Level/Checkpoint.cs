using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private bool isActivated;
    [SerializeField] private SpriteRenderer flagRenderer;
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color inactiveColor = Color.gray;

    public bool IsActivated => isActivated;
    public event System.Action<Checkpoint> OnCheckpointActivated;

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        UpdateVisual();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isActivated) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        isActivated = true;
        UpdateVisual();

        var health = other.GetComponent<PlayerHealth>();
        if (health != null)
            health.SetCheckpoint(transform.position);

        OnCheckpointActivated?.Invoke(this);
    }

    private void UpdateVisual()
    {
        if (flagRenderer != null)
            flagRenderer.color = isActivated ? activeColor : inactiveColor;
    }

    public void ResetCheckpoint()
    {
        isActivated = false;
        UpdateVisual();
    }
}
