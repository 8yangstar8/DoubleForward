using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider2D))]
public class GravityZone : MonoBehaviour
{
    [SerializeField] private Vector2 gravityDirection = Vector2.down;
    [SerializeField] private float gravityStrength = 9.81f;
    [SerializeField] private bool rotatePlayer = true;
    [SerializeField] private bool isActive = true;

    private List<Rigidbody2D> affectedBodies = new List<Rigidbody2D>();
    private Dictionary<Rigidbody2D, float> originalGravityScales = new Dictionary<Rigidbody2D, float>();

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void FixedUpdate()
    {
        if (!isActive) return;

        for (int i = affectedBodies.Count - 1; i >= 0; i--)
        {
            if (affectedBodies[i] == null)
            {
                affectedBodies.RemoveAt(i);
                continue;
            }

            var rb = affectedBodies[i];
            rb.AddForce(gravityDirection.normalized * gravityStrength * rb.mass);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var rb = other.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        if (!affectedBodies.Contains(rb))
        {
            affectedBodies.Add(rb);
            originalGravityScales[rb] = rb.gravityScale;
            rb.gravityScale = 0f;

            if (rotatePlayer)
            {
                float angle = Mathf.Atan2(gravityDirection.y, gravityDirection.x) * Mathf.Rad2Deg + 90f;
                other.transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var rb = other.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        if (affectedBodies.Remove(rb))
        {
            if (originalGravityScales.TryGetValue(rb, out float original))
            {
                rb.gravityScale = original;
                originalGravityScales.Remove(rb);
            }

            if (rotatePlayer)
                other.transform.rotation = Quaternion.identity;
        }
    }

    public void SetGravityDirection(Vector2 direction)
    {
        gravityDirection = direction;
        if (rotatePlayer)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
            foreach (var rb in affectedBodies)
            {
                if (rb != null)
                    rb.transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
    }

    public void Activate() => isActive = true;
    public void Deactivate() => isActive = false;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(transform.position, gravityDirection.normalized * 2f);
    }
}
