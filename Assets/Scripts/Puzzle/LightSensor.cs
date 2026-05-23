using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(CircleCollider2D))]
public class LightSensor : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sensorRenderer;
    [SerializeField] private Color activatedColor = Color.yellow;
    [SerializeField] private Color defaultColor = Color.gray;
    [SerializeField] private bool stayActivated;
    [SerializeField] private float activationDelay = 0.5f;

    public UnityEvent OnActivated;
    public UnityEvent OnDeactivated;

    public bool IsActivated { get; private set; }
    private bool isLit;
    private float litTimer;

    void Start()
    {
        var collider = GetComponent<CircleCollider2D>();
        collider.isTrigger = true;
        UpdateVisual();
    }

    void Update()
    {
        if (isLit && !IsActivated)
        {
            litTimer += Time.deltaTime;
            if (litTimer >= activationDelay)
            {
                IsActivated = true;
                OnActivated?.Invoke();
                UpdateVisual();
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("LightZone"))
        {
            isLit = true;
            litTimer = 0f;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("LightZone"))
        {
            isLit = false;
            litTimer = 0f;

            if (IsActivated && !stayActivated)
            {
                IsActivated = false;
                OnDeactivated?.Invoke();
                UpdateVisual();
            }
        }
    }

    private void UpdateVisual()
    {
        if (sensorRenderer != null)
            sensorRenderer.color = IsActivated ? activatedColor : defaultColor;
    }

    public void Reset()
    {
        IsActivated = false;
        isLit = false;
        litTimer = 0f;
        UpdateVisual();
    }
}
