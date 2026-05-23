using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class ShadowWall : MonoBehaviour
{
    [SerializeField] private SpriteRenderer wallRenderer;
    [SerializeField] private Color wallColor = new Color(0.2f, 0.1f, 0.3f, 0.8f);

    void Start()
    {
        gameObject.layer = LayerMask.NameToLayer("ShadowWall");

        if (wallRenderer != null)
            wallRenderer.color = wallColor;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        var player = collision.collider.GetComponent<PlayerController>();
        if (player != null && player.Type == PlayerController.PlayerType.Nox && player.IsDashing)
        {
            Physics2D.IgnoreCollision(GetComponent<BoxCollider2D>(), collision.collider, true);
            Invoke(nameof(ResetCollision), 0.5f);
        }
    }

    private void ResetCollision()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        var wallCollider = GetComponent<BoxCollider2D>();
        foreach (var player in players)
        {
            if (player.Type == PlayerController.PlayerType.Nox)
            {
                var playerCollider = player.GetComponent<Collider2D>();
                if (playerCollider != null && !wallCollider.bounds.Intersects(playerCollider.bounds))
                {
                    Physics2D.IgnoreCollision(wallCollider, playerCollider, false);
                }
            }
        }
    }
}
