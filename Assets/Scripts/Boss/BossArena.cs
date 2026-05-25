using UnityEngine;
using System.Collections;

/// <summary>
/// Boss战场管理器 - 管理Boss入场演出、战斗区域、胜利流程
/// 集成CameraDirector的电影化镜头、音乐切换、门锁机制
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BossArena : MonoBehaviour
{
    [Header("Boss")]
    [SerializeField] private BossBase boss;
    [SerializeField] private BossHealthBar healthBar;
    [SerializeField] private string bossDisplayName;

    [Header("区域")]
    [SerializeField] private GameObject arenaDoors;       // 进入后关闭的门
    [SerializeField] private GameObject exitDoor;         // 击败后打开的出口
    [SerializeField] private Transform bossSpawnPoint;    // Boss出场位置

    [Header("音频")]
    [SerializeField] private AudioClip bossMusic;
    [SerializeField] private AudioClip victoryMusic;
    [SerializeField] private AudioClip bossIntroSFX;

    [Header("入场演出")]
    [SerializeField] private bool playBossIntro = true;
    [SerializeField] private float introDuration = 3f;
    [SerializeField] private float introDelay = 0.5f;     // 进入后延迟多久开始演出

    [Header("胜利流程")]
    [SerializeField] private float victoryDelay = 1.5f;
    [SerializeField] private GameObject victoryVFXPrefab;
    [SerializeField] private bool openExitOnDefeat = true;

    [Header("设置")]
    [SerializeField] private bool requireBothPlayers = true;
    [SerializeField] private bool lockCameraDuringBattle = true;
    [SerializeField] private Bounds cameraBounds;         // 战斗中的摄像机边界

    private bool battleStarted;
    private bool battleEnded;
    private int playersInArena;

    public bool IsBattleActive => battleStarted && !battleEnded;

    public event System.Action OnBattleBegin;
    public event System.Action OnBattleVictory;

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
            StartCoroutine(BattleIntroSequence());
    }

    // ==================== 入场演出 ====================

    private IEnumerator BattleIntroSequence()
    {
        if (battleStarted) yield break;
        battleStarted = true;

        // 关闭入口
        if (arenaDoors != null) arenaDoors.SetActive(true);

        yield return new WaitForSeconds(introDelay);

        // 激活Boss（但暂不开始战斗）
        if (boss != null)
        {
            if (bossSpawnPoint != null)
                boss.transform.position = bossSpawnPoint.position;
            boss.gameObject.SetActive(true);
        }

        // 入场音效
        if (bossIntroSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(bossIntroSFX);

        // 摄像机演出
        if (playBossIntro && CameraDirector.Instance != null && boss != null)
        {
            string displayName = !string.IsNullOrEmpty(bossDisplayName)
                ? bossDisplayName : boss.name;
            CameraDirector.Instance.PlayBossIntro(boss.transform, displayName, introDuration);

            // 等待演出结束
            yield return new WaitForSeconds(introDuration);
        }

        // 切换Boss战音乐
        if (bossMusic != null)
            AudioManager.Instance?.PlayBGM(bossMusic);

        // 正式开始战斗
        if (boss != null)
            boss.StartBattle();

        // 初始化血条
        if (healthBar != null && boss != null)
        {
            string displayName = !string.IsNullOrEmpty(bossDisplayName)
                ? bossDisplayName : boss.name;
            healthBar.Initialize(boss, displayName);
        }

        // 锁定摄像机区域
        if (lockCameraDuringBattle)
        {
            var cam = FindAnyObjectByType<CameraController>();
            if (cam != null && cameraBounds.size.sqrMagnitude > 0)
                cam.SetBounds(cameraBounds.min, cameraBounds.max);
        }

        // 屏幕震动
        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeMedium();

        OnBattleBegin?.Invoke();
    }

    // ==================== 胜利流程 ====================

    private void OnBossDefeated()
    {
        battleEnded = true;
        StartCoroutine(VictorySequence());
    }

    private IEnumerator VictorySequence()
    {
        // 慢动作效果
        if (CameraEffects.Instance != null)
            CameraEffects.Instance.SlowMotion(0.3f, 1f);

        yield return new WaitForSecondsRealtime(victoryDelay);

        // 胜利特效
        if (victoryVFXPrefab != null && boss != null)
            Instantiate(victoryVFXPrefab, boss.transform.position, Quaternion.identity);

        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeHeavy();

        // 胜利音乐
        if (victoryMusic != null)
            AudioManager.Instance?.PlayBGM(victoryMusic);

        // 打开出口
        if (openExitOnDefeat)
        {
            if (exitDoor != null) exitDoor.SetActive(true);
            if (arenaDoors != null) arenaDoors.SetActive(false);
        }

        // 聚焦出口
        if (exitDoor != null && CameraDirector.Instance != null)
        {
            CameraDirector.Instance.FocusOn(
                exitDoor.transform.position, 4f, 1.5f, 1f);
        }

        // 触发合作能量增加
        CoopAbilitySystem.Instance?.AddMeter(30f);

        OnBattleVictory?.Invoke();

        // ComboSystem评分加分
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.PerfectAction("boss_defeat", 500);

        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Success();
    }

    void OnDestroy()
    {
        if (boss != null)
            boss.OnBossDefeated -= OnBossDefeated;
    }

    void OnDrawGizmosSelected()
    {
        if (cameraBounds.size.sqrMagnitude > 0)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireCube(cameraBounds.center, cameraBounds.size);
        }

        if (bossSpawnPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(bossSpawnPoint.position, 0.5f);
        }
    }
}
