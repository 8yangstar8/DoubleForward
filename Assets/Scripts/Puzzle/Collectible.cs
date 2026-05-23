using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class Collectible : MonoBehaviour
{
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.3f;
    [SerializeField] private float rotateSpeed = 90f;
    [SerializeField] private AudioClip collectSound;
    [SerializeField] private GameObject collectEffect;

    private Vector3 startPos;

    void Start()
    {
        GetComponent<CircleCollider2D>().isTrigger = true;
        gameObject.tag = "Collectible";
        startPos = transform.position;
    }

    void Update()
    {
        float yOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPos + Vector3.up * yOffset;
        transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        LevelManager.Instance?.CollectItem();

        if (collectSound != null)
            AudioManager.Instance?.PlaySFX(collectSound);

        if (collectEffect != null)
            Instantiate(collectEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
