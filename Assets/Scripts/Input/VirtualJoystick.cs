using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform knob;
    [SerializeField] private float maxRadius = 80f;
    [SerializeField] private bool isDynamic = false;

    public Vector2 Direction { get; private set; }

    private Vector2 center;
    private Canvas canvas;
    private Camera uiCamera;

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            uiCamera = canvas.worldCamera;

        center = background.position;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isDynamic)
        {
            background.position = eventData.position;
            center = eventData.position;
        }
        else
        {
            center = background.position;
        }
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 offset = eventData.position - center;
        offset = Vector2.ClampMagnitude(offset, maxRadius);
        knob.position = center + offset;
        Direction = offset / maxRadius;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        knob.position = center;
        Direction = Vector2.zero;

        if (isDynamic)
        {
            background.position = center;
        }
    }

    public void ResetJoystick()
    {
        Direction = Vector2.zero;
        knob.localPosition = Vector2.zero;
    }
}
