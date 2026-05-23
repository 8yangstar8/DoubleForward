using UnityEngine;
using UnityEngine.Events;

public class GearMechanism : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private bool startActive;
    [SerializeField] private GearMechanism linkedGear;
    [SerializeField] private bool reverseLinked = true;
    [SerializeField] private float gearRatio = 1f;

    public UnityEvent OnActivated;
    public UnityEvent OnDeactivated;

    public bool IsActive { get; private set; }

    void Start()
    {
        IsActive = startActive;
    }

    void Update()
    {
        if (!IsActive) return;

        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);

        if (linkedGear != null && !linkedGear.IsActive)
        {
            float linkedSpeed = rotationSpeed * gearRatio * (reverseLinked ? -1f : 1f);
            linkedGear.transform.Rotate(0, 0, linkedSpeed * Time.deltaTime);
        }
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        OnActivated?.Invoke();
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        OnDeactivated?.Invoke();
    }

    public void Toggle()
    {
        if (IsActive) Deactivate();
        else Activate();
    }
}
