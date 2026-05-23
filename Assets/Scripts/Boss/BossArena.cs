using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class BossArena : MonoBehaviour
{
    [SerializeField] private BossBase boss;
    [SerializeField] private BossHealthBar healthBar;
    [SerializeField] private GameObject arenaDoors; // 进入后关闭的门
    [SerializeField] private GameObject exitDoor;   // 击败后打开的出口
    [SerializeField] private AudioClip bossMusic;
    [SerializeField] private bool requireBothPlayers = true;

    private bool battleStarted;
    private int playersInArena;

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;

        if (exitDoor != null) exitDoor.SetActive(false);

        if (boss != null)
        {
            boss.OnBossDefeated += OnBossDefeated;
            boss.gameObject.SetActive(false);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (battleStarted) return;
        if (other.GetComponent<PlayerController>() == null) return;

        playersInArena++;

        if (!requireBothPlayers || playersInArena >= 2)
            StartBattle();
    }

    private void StartBattle()
    {
        if (battleStarted) return;
        battleStarted = true;

        // 关闭入口
        if (arenaDoors != null) arenaDoors.SetActive(true);

        // 激活 Boss
        if (boss != null)
        {
            boss.gameObject.SetActive(true);
            boss.StartBattle();
        }

        // 初始化血条
        if (healthBar != null && boss != null)
            healthBar.Initialize(boss, boss.name);

        // 切换音乐
        if (bossMusic != null)
            AudioManager.Instance?.PlayBGM(bossMusic);
    }

    private void OnBossDefeated()
    {
        // 打开出口
        if (exitDoor != null) exitDoor.SetActive(true);
        if (arenaDoors != null) arenaDoors.SetActive(false);
    }

    void OnDestroy()
    {
        if (boss != null)
            boss.OnBossDefeated -= OnBossDefeated;
    }
}
