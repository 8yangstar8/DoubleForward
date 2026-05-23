using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0, 1, -10);

    [Header("Follow Settings")]
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float lookAheadDistance = 2f;
    [SerializeField] private float lookAheadSpeed = 3f;

    [Header("Bounds")]
    [SerializeField] private bool useBounds;
    [SerializeField] private Vector2 minBounds;
    [SerializeField] private Vector2 maxBounds;

    private Vector3 currentLookAhead;
    private PlayerController targetPlayer;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetPlayer = newTarget?.GetComponent<PlayerController>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 lookAheadTarget = Vector3.zero;
        if (targetPlayer != null)
        {
            float dir = targetPlayer.IsFacingRight ? 1f : -1f;
            lookAheadTarget = new Vector3(dir * lookAheadDistance, 0, 0);
        }
        currentLookAhead = Vector3.Lerp(currentLookAhead, lookAheadTarget, lookAheadSpeed * Time.deltaTime);

        Vector3 desiredPosition = target.position + offset + currentLookAhead;

        if (useBounds)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, minBounds.y, maxBounds.y);
        }

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }

    public void SetBounds(Vector2 min, Vector2 max)
    {
        useBounds = true;
        minBounds = min;
        maxBounds = max;
    }

    public void SnapToTarget()
    {
        if (target == null) return;
        transform.position = target.position + offset;
    }
}
