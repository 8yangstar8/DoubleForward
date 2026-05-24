using UnityEngine;
using System.Collections;

/// <summary>
/// 关卡过渡区域 - 玩家到达时触发场景切换
/// 支持：即时切换、淡入淡出、滑动过渡、连接门
/// 用于章节内关卡间的无缝衔接
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LevelTransitionZone : MonoBehaviour
{
    public enum TransitionType
    {
        FadeToBlack,    // 渐黑过渡
        SlideLeft,      // 向左滑入
        SlideRight,     // 向右滑入
        SlideUp,        // 向上滑入
        CircleWipe,     // 圆形遮罩
        Instant         // 直接切换
    }

    [Header("目标")]
    [SerializeField] private string targetSceneName;
    [SerializeField] private int targetChapter;
    [SerializeField] private int targetLevel;

    [Header("过渡效果")]
    [SerializeField] private TransitionType transitionType = TransitionType.FadeToBlack;
    [SerializeField] private float transitionDuration = 1f;
    [SerializeField] private Color fadeColor = Color.black;

    [Header("触发条件")]
    [SerializeField] private bool requireBothPlayers = false;
    [SerializeField] private bool requireAllCollectibles = false;
    [SerializeField] private bool autoTrigger = true;
    [SerializeField] private float triggerDelay = 0.5f;

    [Header("重生点设置")]
    [SerializeField] private Vector3 spawnPositionInNewScene = Vector3.zero;

    [Header("视觉")]
    [SerializeField] private GameObject portalEffectPrefab;
    [SerializeField] private string transitionSoundKey = "level_transition";

    private bool isTriggered;
    private int playersInZone;

    void Awake()
    {
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (isTriggered) return;

        playersInZone++;

        if (autoTrigger && CanTransition())
        {
            TriggerTransition();
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playersInZone = Mathf.Max(0, playersInZone - 1);
    }

    /// <summary>
    /// 手动触发过渡（由UI按钮等调用）
    /// </summary>
    public void TriggerTransition()
    {
        if (isTriggered) return;
        if (!CanTransition()) return;

        isTriggered = true;
        StartCoroutine(DoTransition());
    }

    private bool CanTransition()
    {
        // 双人都需在区域内
        if (requireBothPlayers && playersInZone < 2)
            return false;

        // 需要收集所有收集品
        if (requireAllCollectibles)
        {
            var tracker = FindAnyObjectByType<LevelProgressTracker>();
            if (tracker != null && !tracker.AllCollected)
                return false;
        }

        return true;
    }

    private IEnumerator DoTransition()
    {
        // 播放音效
        AudioManager.Instance?.PlaySFX(transitionSoundKey);

        // 播放特效
        if (portalEffectPrefab != null)
        {
            var effect = Instantiate(portalEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }

        // 触发延迟
        yield return new WaitForSeconds(triggerDelay);

        // 发布关卡完成事件
        EventBus.Publish(new LevelCompleteEvent
        {
            chapter = targetChapter,
            level = targetLevel - 1, // 当前关卡
            stars = 0, // 由ScoreManager计算
            time = 0,
            collectibles = 0
        });

        // 存储重生位置供下一个场景使用
        if (spawnPositionInNewScene != Vector3.zero)
        {
            PlayerPrefs.SetFloat("SpawnX", spawnPositionInNewScene.x);
            PlayerPrefs.SetFloat("SpawnY", spawnPositionInNewScene.y);
            PlayerPrefs.Save();
        }

        // 使用SceneTransition（如果可用）或SceneLoader
        string sceneName = !string.IsNullOrEmpty(targetSceneName) ?
            targetSceneName :
            $"Chapter{targetChapter}_Level{targetLevel}";

        // 通过SceneLoader加载
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(sceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Vector3 center = transform.position + (Vector3)col.offset;
        Vector3 size = Vector3.Scale(col.size, transform.lossyScale);
        Gizmos.DrawCube(center, size);

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireCube(center, size);

        // 画箭头指向目标方向
        Gizmos.color = Color.green;
        Vector3 arrowEnd = center + Vector3.right * 2f;
        Gizmos.DrawLine(center, arrowEnd);
        Gizmos.DrawLine(arrowEnd, arrowEnd + new Vector3(-0.3f, 0.3f, 0));
        Gizmos.DrawLine(arrowEnd, arrowEnd + new Vector3(-0.3f, -0.3f, 0));

#if UNITY_EDITOR
        // 显示目标场景名
        string label = !string.IsNullOrEmpty(targetSceneName) ?
            targetSceneName : $"Ch{targetChapter}-Lv{targetLevel}";
        UnityEditor.Handles.Label(center + Vector3.up * 1f, label);
#endif
    }
}
