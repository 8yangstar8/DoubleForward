using UnityEngine;
using UnityEngine.EventSystems;

public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool IsPressed { get; private set; }
    public bool WasPressedThisFrame { get; private set; }
    public bool WasReleasedThisFrame { get; private set; }

    private bool pressedLastFrame;

    public event System.Action OnButtonDown;
    public event System.Action OnButtonUp;

    void LateUpdate()
    {
        WasPressedThisFrame = IsPressed && !pressedLastFrame;
        WasReleasedThisFrame = !IsPressed && pressedLastFrame;
        pressedLastFrame = IsPressed;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsPressed = true;
        OnButtonDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsPressed = false;
        OnButtonUp?.Invoke();
    }

    void OnDisable()
    {
        IsPressed = false;
        WasPressedThisFrame = false;
        WasReleasedThisFrame = false;
    }
}
