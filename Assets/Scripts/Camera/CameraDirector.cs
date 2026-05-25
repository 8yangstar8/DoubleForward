using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 摄像机导演系统 - 控制过场、Boss入场、关卡转场等的摄像机演出
/// 提供电影化镜头表现：推拉摇移、聚焦目标、分屏切换、慢动作配合
/// 与CameraController协同工作，临时接管摄像机控制权
/// </summary>
public class CameraDirector : MonoBehaviour
{
    public static CameraDirector Instance { get; private set; }

    [Header("引用")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private CameraController cameraController;

    [Header("默认参数")]
    [SerializeField] private float defaultTransitionSpeed = 3f;
    [SerializeField] private float defaultHoldDuration = 2f;
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // 状态
    private bool isDirecting;
    private Coroutine activeSequence;
    private float originalOrthoSize;
    private Vector3 originalPosition;
    private bool wasControllerEnabled;

    public bool IsDirecting => isDirecting;

    public event System.Action OnDirectingStart;
    public event System.Action OnDirectingEnd;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (cameraController == null)
            cameraController = FindAnyObjectByType<CameraController>();
    }

    // ==================== 预设演出 ====================

    /// <summary>
    /// Boss入场演出：摄像机推向Boss → 停留 → 拉回
    /// </summary>
    public void PlayBossIntro(Transform bossTransform, string bossName, float duration = 3f)
    {
        if (isDirecting) return;

        var sequence = new List<CameraAction>
        {
            // 推向Boss
            new CameraAction
            {
                type = ActionType.MoveTo,
                targetPosition = bossTransform.position + Vector3.back * 10f,
                targetOrthoSize = 4f,
                duration = duration * 0.35f
            },
            // 停留
            new CameraAction
            {
                type = ActionType.Hold,
                duration = duration * 0.3f
            },
            // 拉回玩家
            new CameraAction
            {
                type = ActionType.ReturnToPlayers,
                duration = duration * 0.35f
            }
        };

        PlaySequence(sequence);
    }

    /// <summary>
    /// 关卡开始全景：从高处俯瞰 → 推向玩家起点
    /// </summary>
    public void PlayLevelIntro(Vector3 overviewPos, float overviewSize, Vector3 playerStartPos)
    {
        if (isDirecting) return;

        var sequence = new List<CameraAction>
        {
            // 全景
            new CameraAction
            {
                type = ActionType.MoveTo,
                targetPosition = overviewPos + Vector3.back * 10f,
                targetOrthoSize = overviewSize,
                duration = 0.01f // 瞬间到达
            },
            new CameraAction
            {
                type = ActionType.Hold,
                duration = 1.5f
            },
            // 推向玩家
            new CameraAction
            {
                type = ActionType.MoveTo,
                targetPosition = playerStartPos + Vector3.back * 10f,
                targetOrthoSize = 5f,
                duration = 2f
            },
            new CameraAction
            {
                type = ActionType.ReturnToPlayers,
                duration = 0.5f
            }
        };

        PlaySequence(sequence);
    }

    /// <summary>
    /// 聚焦目标：推到目标 → 停留 → 返回
    /// </summary>
    public void FocusOn(Vector3 target, float zoomSize = 3f, float holdTime = 1.5f, float transitionTime = 0.8f)
    {
        if (isDirecting) return;

        var sequence = new List<CameraAction>
        {
            new CameraAction
            {
                type = ActionType.MoveTo,
                targetPosition = target + Vector3.back * 10f,
                targetOrthoSize = zoomSize,
                duration = transitionTime
            },
            new CameraAction
            {
                type = ActionType.Hold,
                duration = holdTime
            },
            new CameraAction
            {
                type = ActionType.ReturnToPlayers,
                duration = transitionTime
            }
        };

        PlaySequence(sequence);
    }

    /// <summary>
    /// 平移展示路径（展示机关效果、门打开等）
    /// </summary>
    public void PanTo(Vector3 target, float panDuration = 1f, float holdDuration = 1f)
    {
        if (isDirecting) return;

        var sequence = new List<CameraAction>
        {
            new CameraAction
            {
                type = ActionType.MoveTo,
                targetPosition = target + Vector3.back * 10f,
                targetOrthoSize = -1f, // 保持当前大小
                duration = panDuration
            },
            new CameraAction
            {
                type = ActionType.Hold,
                duration = holdDuration
            },
            new CameraAction
            {
                type = ActionType.ReturnToPlayers,
                duration = panDuration * 0.8f
            }
        };

        PlaySequence(sequence);
    }

    /// <summary>
    /// 震动 + 缩放冲击波效果（Boss相变、大爆炸等）
    /// </summary>
    public void ImpactZoom(Vector3 center, float zoomInSize = 3f, float zoomOutSize = 7f, float duration = 0.6f)
    {
        if (isDirecting) return;

        var sequence = new List<CameraAction>
        {
            // 快速缩放到中心
            new CameraAction
            {
                type = ActionType.MoveTo,
                targetPosition = center + Vector3.back * 10f,
                targetOrthoSize = zoomInSize,
                duration = duration * 0.3f
            },
            // 冲击波般快速拉远
            new CameraAction
            {
                type = ActionType.MoveTo,
                targetPosition = center + Vector3.back * 10f,
                targetOrthoSize = zoomOutSize,
                duration = duration * 0.3f
            },
            // 恢复
            new CameraAction
            {
                type = ActionType.ReturnToPlayers,
                duration = duration * 0.4f
            }
        };

        PlaySequence(sequence);
    }

    /// <summary>
    /// 立即停止演出并恢复控制
    /// </summary>
    public void CancelDirecting()
    {
        if (!isDirecting) return;

        if (activeSequence != null)
            StopCoroutine(activeSequence);

        RestoreControl();
    }

    // ==================== 序列执行 ====================

    public enum ActionType
    {
        MoveTo,          // 移动到目标位置
        Hold,            // 保持当前位置
        ReturnToPlayers, // 返回玩家跟随模式
        Shake,           // 震动
        SlowMotion       // 慢动作
    }

    [System.Serializable]
    public class CameraAction
    {
        public ActionType type;
        public Vector3 targetPosition;
        public float targetOrthoSize = -1f; // -1 = 保持当前
        public float duration = 1f;
        public float shakeIntensity = 0.2f;
        public float slowMotionScale = 0.3f;
    }

    public void PlaySequence(List<CameraAction> actions)
    {
        if (isDirecting) return;
        activeSequence = StartCoroutine(ExecuteSequence(actions));
    }

    private IEnumerator ExecuteSequence(List<CameraAction> actions)
    {
        TakeControl();

        foreach (var action in actions)
        {
            switch (action.type)
            {
                case ActionType.MoveTo:
                    yield return ExecuteMoveTo(action);
                    break;

                case ActionType.Hold:
                    yield return new WaitForSecondsRealtime(action.duration);
                    break;

                case ActionType.ReturnToPlayers:
                    yield return ExecuteReturnToPlayers(action.duration);
                    break;

                case ActionType.Shake:
                    if (CameraEffects.Instance != null)
                        CameraEffects.Instance.Shake(action.shakeIntensity, action.duration);
                    yield return new WaitForSecondsRealtime(action.duration);
                    break;

                case ActionType.SlowMotion:
                    if (CameraEffects.Instance != null)
                        CameraEffects.Instance.SlowMotion(action.slowMotionScale, action.duration);
                    yield return new WaitForSecondsRealtime(action.duration);
                    break;
            }
        }

        RestoreControl();
    }

    private IEnumerator ExecuteMoveTo(CameraAction action)
    {
        if (mainCamera == null) yield break;

        Vector3 startPos = mainCamera.transform.position;
        float startSize = mainCamera.orthographicSize;
        float targetSize = action.targetOrthoSize > 0 ? action.targetOrthoSize : startSize;

        float elapsed = 0;
        while (elapsed < action.duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = easeCurve.Evaluate(Mathf.Clamp01(elapsed / action.duration));

            mainCamera.transform.position = Vector3.Lerp(startPos, action.targetPosition, t);
            mainCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, t);

            yield return null;
        }

        mainCamera.transform.position = action.targetPosition;
        mainCamera.orthographicSize = targetSize;
    }

    private IEnumerator ExecuteReturnToPlayers(float duration)
    {
        if (mainCamera == null) yield break;

        Vector3 startPos = mainCamera.transform.position;
        float startSize = mainCamera.orthographicSize;

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = easeCurve.Evaluate(Mathf.Clamp01(elapsed / duration));

            mainCamera.transform.position = Vector3.Lerp(startPos, originalPosition, t);
            mainCamera.orthographicSize = Mathf.Lerp(startSize, originalOrthoSize, t);

            yield return null;
        }
    }

    // ==================== 控制权管理 ====================

    private void TakeControl()
    {
        isDirecting = true;

        if (mainCamera != null)
        {
            originalPosition = mainCamera.transform.position;
            originalOrthoSize = mainCamera.orthographicSize;
        }

        // 禁用常规摄像机控制
        if (cameraController != null)
        {
            wasControllerEnabled = cameraController.enabled;
            cameraController.enabled = false;
        }

        OnDirectingStart?.Invoke();
    }

    private void RestoreControl()
    {
        isDirecting = false;
        activeSequence = null;

        // 恢复摄像机控制
        if (cameraController != null)
            cameraController.enabled = wasControllerEnabled;

        OnDirectingEnd?.Invoke();
    }
}
