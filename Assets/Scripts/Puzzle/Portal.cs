using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Portal : MonoBehaviour
{
    [SerializeField] private Portal linkedPortal;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float teleportCooldown = 1f;
    [SerializeField] private bool requireBothPlayers;
    [SerializeField] private ParticleSystem portalEffect;

    private float cooldownTimer;
    private int playersInPortal;

    public event System.Action OnTeleport;

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Update()
    {
        if (cooldownTimer > 0)
            cooldownTimer -= Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        playersInPortal++;

        if (cooldownTimer > 0) return;
        if (linkedPortal == null) return;
        if (requireBothPlayers && playersInPortal < 2) return;

        TeleportPlayer(player);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null)
            playersInPortal = Mathf.Max(0, playersInPortal - 1);
    }

    private void TeleportPlayer(PlayerController player)
    {
        cooldownTimer = teleportCooldown;
        linkedPortal.cooldownTimer = teleportCooldown;

        Vector3 exitPos = linkedPortal.exitPoint != null
            ? linkedPortal.exitPoint.position
            : linkedPortal.transform.position + Vector3.up;

        player.Respawn(exitPos);

        if (portalEffect != null) portalEffect.Play();
        if (linkedPortal.portalEffect != null) linkedPortal.portalEffect.Play();

        OnTeleport?.Invoke();
    }

    public void TeleportBothPlayers()
    {
        if (linkedPortal == null) return;

        var manager = LevelManager.Instance;
        if (manager == null) return;

        if (manager.LuxPlayer != null) TeleportPlayer(manager.LuxPlayer);
        if (manager.NoxPlayer != null) TeleportPlayer(manager.NoxPlayer);
    }
}
