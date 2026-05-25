using UnityEngine;

/// <summary>
/// 可交互物体适配器合集 - 让环境物件实现IInteractable接口
/// IInteractable定义在Core/EventBus.cs中
/// </summary>

// ============ 拉杆交互适配器 ============
/// <summary>
/// 让LeverSwitch实现IInteractable，挂在LeverSwitch同一个GameObject上
/// </summary>
public class LeverInteractable : MonoBehaviour, IInteractable
{
    private LeverSwitch lever;

    void Awake()
    {
        lever = GetComponent<LeverSwitch>();
    }

    public bool CanInteract(GameObject player)
    {
        return lever != null;
    }

    public void OnInteract(GameObject player)
    {
        lever?.Interact();
    }

    public string GetInteractPrompt()
    {
        if (lever == null) return "";
        return lever.IsOn ? "interact_lever_off" : "interact_lever_on";
    }
}

// ============ 告示牌/NPC对话交互 ============
public class SignInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string localizationKey = "sign_default";
    [SerializeField] private DialogueSystem.DialogueSequence dialogue;
    [SerializeField] private bool showOnce = false;

    private bool hasShown;

    public bool CanInteract(GameObject player)
    {
        if (showOnce && hasShown) return false;
        return true;
    }

    public void OnInteract(GameObject player)
    {
        hasShown = true;

        if (dialogue != null && DialogueSystem.Instance != null)
        {
            DialogueSystem.Instance.StartDialogue(dialogue);
        }
        else
        {
            // 简单文本提示
            string text = LocalizationSystem.Instance != null
                ? LocalizationSystem.Instance.GetText(localizationKey)
                : localizationKey;

            // 通过EventBus发布，避免直接依赖UI
            Debug.Log($"[Sign] {text}");
        }
    }

    public string GetInteractPrompt() => "interact_read";
}

// ============ 宝箱交互 ============
public class ChestInteractable : MonoBehaviour, IInteractable
{
    [Header("宝箱")]
    [SerializeField] private GameObject[] rewardPrefabs;
    [SerializeField] private int coinReward = 50;
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private GameObject openVFX;
    [SerializeField] private string chestId;   // 用于存档记录

    private bool isOpened;

    void Start()
    {
        // 检查存档 - 是否已打开
        if (!string.IsNullOrEmpty(chestId))
        {
            isOpened = PlayerPrefs.GetInt($"Chest_{chestId}", 0) == 1;
            if (isOpened && chestAnimator != null)
                chestAnimator.SetBool("IsOpen", true);
        }
    }

    public bool CanInteract(GameObject player)
    {
        return !isOpened;
    }

    public void OnInteract(GameObject player)
    {
        if (isOpened) return;
        isOpened = true;

        // 动画
        if (chestAnimator != null)
            chestAnimator.SetTrigger("Open");

        // 特效
        if (openVFX != null)
            Instantiate(openVFX, transform.position + Vector3.up * 0.5f, Quaternion.identity);

        // 奖励金币
        if (coinReward > 0 && CurrencyManager.Instance != null)
            CurrencyManager.Instance.AddCoins(coinReward);

        // 生成奖励物品
        if (rewardPrefabs != null)
        {
            foreach (var prefab in rewardPrefabs)
            {
                if (prefab == null) continue;
                var spawned = Instantiate(prefab,
                    transform.position + Vector3.up * 1f, Quaternion.identity);

                var rb = spawned.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Vector2 dir = new Vector2(
                        Random.Range(-1f, 1f), Random.Range(1f, 2f)).normalized;
                    rb.AddForce(dir * 4f, ForceMode2D.Impulse);
                }
            }
        }

        // 音效 & 振动
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("chest_open");
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Medium();

        // 存档
        if (!string.IsNullOrEmpty(chestId))
        {
            PlayerPrefs.SetInt($"Chest_{chestId}", 1);
            PlayerPrefs.Save();
        }

        // 发布收集事件
        EventBus.Publish(new CollectiblePickedEvent
        {
            collected = 1,
            total = 1,
            position = transform.position
        });
    }

    public string GetInteractPrompt() => "interact_open_chest";
}
