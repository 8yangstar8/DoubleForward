using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider2D))]
public class PressurePlate : MonoBehaviour
{
    [SerializeField] private bool requireBothPlayers;
    [SerializeField] private bool isToggle;
    [SerializeField] private SpriteRenderer plateRenderer;
    [SerializeField] private Color pressedColor = Color.green;
    [SerializeField] private Color defaultColor = Color.red;
    [SerializeField] private float pressDepth = 0.1f;

    public UnityEvent OnPressed;
    public UnityEvent OnReleased;

    public bool IsPressed { get; private set; }
    private int playersOnPlate;
    private Vector3 originalPosition;

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        originalPosition = transform.position;
        UpdateVisual();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        playersOnPlate++;
        CheckState();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        playersOnPlate = Mathf.Max(0, playersOnPlate - 1);
        CheckState();
    }

    private void CheckState()
    {
        bool shouldBePressed = requireBothPlayers ? playersOnPlate >= 2 : playersOnPlate >= 1;

        if (shouldBePressed && !IsPressed)
        {
            IsPressed = true;
            OnPressed?.Invoke();
            UpdateVisual();
        }
        else if (!shouldBePressed && IsPressed && !isToggle)
        {
            IsPressed = false;
            OnReleased?.Invoke();
            UpdateVisual();
        }
    }

    private void UpdateVisual()
    {
        if (plateRenderer != null)
            plateRenderer.color = IsPressed ? pressedColor : defaultColor;

        transform.position = IsPressed
            ? originalPosition + Vector3.down * pressDepth
            : originalPosition;
    }

    public void Reset()
    {
        IsPressed = false;
        playersOnPlate = 0;
        transform.position = originalPosition;
        UpdateVisual();
    }
}
