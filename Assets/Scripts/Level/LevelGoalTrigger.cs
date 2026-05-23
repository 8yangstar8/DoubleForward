using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class LevelGoalTrigger : MonoBehaviour
{
    [SerializeField] private bool requireBothPlayers = true;

    private bool player1Arrived;
    private bool player2Arrived;

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (player.PlayerIndex == 0)
            player1Arrived = true;
        else
            player2Arrived = true;

        if (!requireBothPlayers || (player1Arrived && player2Arrived))
        {
            LevelManager.Instance?.CompleteLevel();
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (player.PlayerIndex == 0)
            player1Arrived = false;
        else
            player2Arrived = false;
    }
}
