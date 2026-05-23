using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class WaterCurrent : MonoBehaviour
{
    [SerializeField] private Vector2 flowDirection = Vector2.right;
    [SerializeField] private float flowForce = 3f;
    [SerializeField] private float buoyancyForce = 5f;
    [SerializeField] private float dragInWater = 3f;
    [SerializeField] private float swimSpeed = 3f;

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        var rb = other.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        rb.AddForce(flowDirection.normalized * flowForce);
        rb.AddForce(Vector2.up * buoyancyForce);
        rb.linearDamping = dragInWater;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var rb = other.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearDamping = 0f;
    }

    public void SetFlowDirection(Vector2 direction)
    {
        flowDirection = direction;
    }

    public void SetFlowForce(float force)
    {
        flowForce = force;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Gizmos.DrawWireCube(transform.position + (Vector3)col.offset, col.size);
        }
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, flowDirection.normalized * 2f);
    }
}
