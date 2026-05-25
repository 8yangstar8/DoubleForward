using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 玩家交互系统 - 处理与环境物件(拉杆、NPC、告示牌等)的交互
/// 检测范围内的可交互物体，显示提示，响应交互按键
/// IInteractable接口定义在Core/EventBus.cs中
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("交互设置")]
    [SerializeField] private float interactionRadius = 1.5f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private Transform interactionPoint;

    [Header("提示UI")]
    [SerializeField] private GameObject promptPrefab;     // 交互提示图标预制体
    [SerializeField] private Vector3 promptOffset = new Vector3(0, 1.5f, 0);

    private PlayerController controller;
    private IInteractable currentTarget;
    private GameObject promptInstance;
    private List<IInteractable> nearbyInteractables = new List<IInteractable>();

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        if (interactionPoint == null)
            interactionPoint = transform;
    }

    void Update()
    {
        ScanForInteractables();
        UpdatePrompt();

        // 按交互键
        if (InputManager.Instance != null &&
            InputManager.Instance.GetInteractPressed(controller.PlayerIndex))
        {
            TryInteract();
        }
    }

    private void ScanForInteractables()
    {
        nearbyInteractables.Clear();
        currentTarget = null;

        var colliders = Physics2D.OverlapCircleAll(
            interactionPoint.position, interactionRadius, interactableLayer);

        float closestDist = float.MaxValue;

        foreach (var col in colliders)
        {
            var interactable = col.GetComponent<IInteractable>();
            if (interactable == null) continue;
            if (!interactable.CanInteract(gameObject)) continue;

            nearbyInteractables.Add(interactable);

            float dist = Vector2.Distance(interactionPoint.position, col.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                currentTarget = interactable;
            }
        }
    }

    private void UpdatePrompt()
    {
        if (currentTarget != null)
        {
            if (promptInstance == null && promptPrefab != null)
            {
                promptInstance = Instantiate(promptPrefab, transform);
                promptInstance.transform.localPosition = promptOffset;
            }

            if (promptInstance != null)
                promptInstance.SetActive(true);
        }
        else
        {
            if (promptInstance != null)
                promptInstance.SetActive(false);
        }
    }

    public void TryInteract()
    {
        if (currentTarget == null) return;

        currentTarget.OnInteract(gameObject);

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("interact");
    }

    /// <summary>
    /// 获取当前可交互目标（供UI使用）
    /// </summary>
    public IInteractable GetCurrentTarget() => currentTarget;

    /// <summary>
    /// 获取当前可交互目标的提示文本
    /// </summary>
    public string GetPromptText()
    {
        return currentTarget?.GetInteractPrompt() ?? "";
    }

    void OnDrawGizmosSelected()
    {
        Transform point = interactionPoint != null ? interactionPoint : transform;
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.4f);
        Gizmos.DrawWireSphere(point.position, interactionRadius);
    }
}
