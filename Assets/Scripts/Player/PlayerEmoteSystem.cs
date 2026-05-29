using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 玩家表情系统 - 双人合作无语音交流的替代方案
/// 快捷表情轮盘：来这里、跳！、等等、点赞、需要帮助
/// 表情气泡在角色头上显示
/// </summary>
public class PlayerEmoteSystem : MonoBehaviour
{
    [Header("表情配置")]
    [SerializeField] private EmoteData[] emotes;

    [Header("气泡UI")]
    [SerializeField] private GameObject emoteBubblePrefab;
    [SerializeField] private Vector3 bubbleOffset = new Vector3(0, 1.8f, 0);
    [SerializeField] private float bubbleDisplayTime = 2.5f;
    [SerializeField] private float bubbleScaleInTime = 0.15f;

    [Header("轮盘UI")]
    [SerializeField] private GameObject emoteWheelPrefab;
    [SerializeField] private float wheelRadius = 80f;

    [Header("音效")]
    [SerializeField] private AudioClip emotePopSound;

    // 运行时
    private PlayerController controller;
    private int playerIndex;
    private GameObject currentBubble;
    private Coroutine bubbleCoroutine;
    private GameObject activeWheel;
    private bool wheelOpen;
    private float wheelOpenTime;
    private int selectedEmoteIndex = -1;

    // 冷却
    private float emoteCooldown = 1f;
    private float cooldownTimer;

    [System.Serializable]
    public class EmoteData
    {
        public string emoteId;
        public Sprite icon;
        public string localizedKey;
        public string fallbackText;        // "来这里！"、"跳！"、"等等" 等
        public Color bubbleColor = Color.white;
        public bool triggerPartnerHighlight; // 是否高亮队友位置
    }

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        if (controller != null)
            playerIndex = controller.PlayerIndex;

        InitializeDefaultEmotes();
    }

    void Update()
    {
        if (controller == null) return;

        cooldownTimer -= Time.deltaTime;

        // 检查表情输入
        HandleEmoteInput();

        // 更新气泡位置
        UpdateBubblePosition();
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 显示表情（外部调用）
    /// </summary>
    public void ShowEmote(int emoteIndex)
    {
        if (emoteIndex < 0 || emotes == null || emoteIndex >= emotes.Length) return;
        if (cooldownTimer > 0) return;

        cooldownTimer = emoteCooldown;
        var emote = emotes[emoteIndex];

        // 显示气泡
        if (bubbleCoroutine != null) StopCoroutine(bubbleCoroutine);
        bubbleCoroutine = StartCoroutine(ShowBubbleRoutine(emote));

        // 音效
        if (emotePopSound != null && SoundFeedback.Instance != null)
            SoundFeedback.Instance.PlayOneShot(emotePopSound);

        // 触觉
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Light();

        // 高亮队友
        if (emote.triggerPartnerHighlight)
            HighlightPartner();
    }

    /// <summary>
    /// 通过ID显示表情
    /// </summary>
    public void ShowEmoteById(string emoteId)
    {
        if (emotes == null) return;
        for (int i = 0; i < emotes.Length; i++)
        {
            if (emotes[i].emoteId == emoteId)
            {
                ShowEmote(i);
                return;
            }
        }
    }

    /// <summary>
    /// 打开表情轮盘
    /// </summary>
    public void OpenEmoteWheel()
    {
        if (wheelOpen) return;
        wheelOpen = true;
        wheelOpenTime = Time.unscaledTime;
        selectedEmoteIndex = -1;

        // 创建轮盘UI（世界空间Canvas）
        if (emoteWheelPrefab != null)
        {
            activeWheel = Instantiate(emoteWheelPrefab, transform.position + bubbleOffset, Quaternion.identity);
            SetupWheelIcons();
        }

        // 减速时间
        if (TimeManager.Instance != null)
            TimeManager.Instance.SetTimeScale(0.3f);
    }

    /// <summary>
    /// 关闭表情轮盘并执行选择
    /// </summary>
    public void CloseEmoteWheel()
    {
        if (!wheelOpen) return;
        wheelOpen = false;

        if (activeWheel != null)
            Destroy(activeWheel);

        // 恢复时间
        if (TimeManager.Instance != null)
            TimeManager.Instance.SetTimeScale(1f);

        // 执行选择
        if (selectedEmoteIndex >= 0)
            ShowEmote(selectedEmoteIndex);
    }

    // ==================== 输入处理 ====================

    private void HandleEmoteInput()
    {
        if (InputManager.Instance == null) return;

        // D-Pad上 = 打开/关闭表情轮盘
        // 或者键盘快捷键 1-5
        if (wheelOpen)
        {
            // 摇杆方向选择表情
            Vector2 input = InputManager.Instance.GetMoveInput(playerIndex);
            if (input.magnitude > 0.5f)
            {
                float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;

                int emoteCount = emotes != null ? emotes.Length : 0;
                if (emoteCount > 0)
                {
                    float sectorSize = 360f / emoteCount;
                    selectedEmoteIndex = Mathf.FloorToInt(angle / sectorSize) % emoteCount;
                    HighlightWheelSlot(selectedEmoteIndex);
                }
            }
        }

        // 键盘快捷键（开发/调试）
        #if UNITY_EDITOR
        if (playerIndex == 0)
        {
            for (int i = 0; i < 5; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    ShowEmote(i);
            }
        }
        #endif
    }

    // ==================== 气泡显示 ====================

    private IEnumerator ShowBubbleRoutine(EmoteData emote)
    {
        // 清除旧气泡
        if (currentBubble != null)
            Destroy(currentBubble);

        // 创建新气泡
        if (emoteBubblePrefab != null)
        {
            currentBubble = Instantiate(emoteBubblePrefab,
                transform.position + bubbleOffset, Quaternion.identity, transform);

            // 设置图标
            var bubbleImage = currentBubble.GetComponentInChildren<Image>();
            if (bubbleImage != null && emote.icon != null)
            {
                bubbleImage.sprite = emote.icon;
                bubbleImage.color = emote.bubbleColor;
            }

            // 设置文字
            var text = currentBubble.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (text != null)
            {
                string displayText = emote.fallbackText;
                if (LocalizationSystem.Instance != null)
                {
                    string localized = LocalizationSystem.Instance.GetText(emote.localizedKey);
                    if (localized != emote.localizedKey) displayText = localized;
                }
                text.text = displayText;
            }
        }

        // 缩放入场动画
        if (currentBubble != null)
        {
            currentBubble.transform.localScale = Vector3.zero;
            float elapsed = 0;
            while (elapsed < bubbleScaleInTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / bubbleScaleInTime;
                float scale = EaseOutBack(t);
                currentBubble.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            currentBubble.transform.localScale = Vector3.one;
        }

        // 等待显示时间
        yield return new WaitForSeconds(bubbleDisplayTime);

        // 缩放退场
        if (currentBubble != null)
        {
            float elapsed = 0;
            float fadeTime = 0.2f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float t = 1f - (elapsed / fadeTime);
                if (currentBubble != null)
                    currentBubble.transform.localScale = Vector3.one * t;
                yield return null;
            }

            if (currentBubble != null)
                Destroy(currentBubble);
        }
    }

    private void UpdateBubblePosition()
    {
        if (currentBubble != null)
        {
            currentBubble.transform.position = transform.position + bubbleOffset;
        }
    }

    // ==================== 轮盘 ====================

    private void SetupWheelIcons()
    {
        if (activeWheel == null || emotes == null) return;

        int count = emotes.Length;
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * wheelRadius * 0.01f,
                Mathf.Sin(angle) * wheelRadius * 0.01f,
                0
            );

            // 创建图标
            if (emotes[i].icon != null)
            {
                var iconObj = new GameObject($"Emote_{i}");
                iconObj.transform.SetParent(activeWheel.transform);
                iconObj.transform.localPosition = offset;

                var sr = iconObj.AddComponent<SpriteRenderer>();
                sr.sprite = emotes[i].icon;
                sr.sortingOrder = 100;
                sr.color = new Color(1, 1, 1, 0.8f);

                iconObj.transform.localScale = Vector3.one * 0.3f;
            }
        }
    }

    private void HighlightWheelSlot(int index)
    {
        if (activeWheel == null) return;

        for (int i = 0; i < activeWheel.transform.childCount; i++)
        {
            var child = activeWheel.transform.GetChild(i);
            var sr = child.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = (i == index)
                    ? Color.white
                    : new Color(1, 1, 1, 0.5f);
                child.localScale = (i == index)
                    ? Vector3.one * 0.45f
                    : Vector3.one * 0.3f;
            }
        }
    }

    private void HighlightPartner()
    {
        int partnerIndex = playerIndex == 0 ? 1 : 0;
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (var p in players)
        {
            if (p.PlayerIndex == partnerIndex)
            {
                // 在队友位置闪烁
                if (VFXManager.Instance != null)
                    VFXManager.Instance.Play("partner_ping", p.transform.position);

                // 小地图标记（通过EventBus避免跨程序集依赖）
                // MinimapSystem在UI层自行监听VFX事件

                break;
            }
        }
    }

    // ==================== 辅助 ====================

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);
    }

    private void InitializeDefaultEmotes()
    {
        if (emotes != null && emotes.Length > 0) return;

        emotes = new EmoteData[]
        {
            new EmoteData
            {
                emoteId = "come_here", localizedKey = "emote_come",
                fallbackText = "来这里！", bubbleColor = Color.yellow,
                triggerPartnerHighlight = true
            },
            new EmoteData
            {
                emoteId = "jump", localizedKey = "emote_jump",
                fallbackText = "跳！", bubbleColor = Color.cyan
            },
            new EmoteData
            {
                emoteId = "wait", localizedKey = "emote_wait",
                fallbackText = "等一下", bubbleColor = new Color(1f, 0.6f, 0.2f)
            },
            new EmoteData
            {
                emoteId = "thumbs_up", localizedKey = "emote_good",
                fallbackText = "不错！", bubbleColor = Color.green
            },
            new EmoteData
            {
                emoteId = "help", localizedKey = "emote_help",
                fallbackText = "救我！", bubbleColor = Color.red,
                triggerPartnerHighlight = true
            }
        };
    }
}
