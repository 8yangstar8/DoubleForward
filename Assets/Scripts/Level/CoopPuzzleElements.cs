using UnityEngine;
using System.Collections;

/// <summary>
/// 双人合作谜题元素合集 - 需要Lux和Nox协作解决的关卡机关
/// 核心光暗二元设计的关卡应用
/// </summary>

// ============ 光暗棱镜 ============
/// <summary>
/// 光暗融合棱镜 - Lux发光照射棱镜，Nox暗影激活棱镜
/// 双方同时作用时激活机关
/// </summary>
public class LightShadowPrism : MonoBehaviour
{
    [Header("检测")]
    [SerializeField] private float detectionRadius = 3f;
    [SerializeField] private float requiredDuration = 1.5f;

    [Header("目标")]
    [SerializeField] private GameObject[] activateTargets;
    [SerializeField] private UnityEngine.Events.UnityEvent onActivated;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer prismRenderer;
    [SerializeField] private Color inactiveColor = Color.gray;
    [SerializeField] private Color lightOnlyColor = new Color(1f, 0.95f, 0.8f);
    [SerializeField] private Color shadowOnlyColor = new Color(0.3f, 0.1f, 0.5f);
    [SerializeField] private Color bothColor = new Color(0.8f, 0.6f, 1f);
    [SerializeField] private GameObject activationVFX;

    private bool luxInRange;
    private bool noxInRange;
    private float bothTimer;
    private bool isActivated;

    void Update()
    {
        if (isActivated) return;

        CheckPlayersInRange();

        // 更新视觉
        if (prismRenderer != null)
        {
            if (luxInRange && noxInRange)
                prismRenderer.color = Color.Lerp(prismRenderer.color, bothColor, 5f * Time.deltaTime);
            else if (luxInRange)
                prismRenderer.color = Color.Lerp(prismRenderer.color, lightOnlyColor, 5f * Time.deltaTime);
            else if (noxInRange)
                prismRenderer.color = Color.Lerp(prismRenderer.color, shadowOnlyColor, 5f * Time.deltaTime);
            else
                prismRenderer.color = Color.Lerp(prismRenderer.color, inactiveColor, 5f * Time.deltaTime);
        }

        // 双方同时在范围内计时
        if (luxInRange && noxInRange)
        {
            bothTimer += Time.deltaTime;
            if (bothTimer >= requiredDuration)
                Activate();
        }
        else
        {
            bothTimer = 0f;
        }
    }

    private void CheckPlayersInRange()
    {
        luxInRange = false;
        noxInRange = false;

        var colliders = Physics2D.OverlapCircleAll(transform.position, detectionRadius);
        foreach (var col in colliders)
        {
            var player = col.GetComponent<PlayerController>();
            if (player == null) continue;

            if (player.Type == PlayerController.PlayerType.Lux)
                luxInRange = true;
            else if (player.Type == PlayerController.PlayerType.Nox)
                noxInRange = true;
        }
    }

    private void Activate()
    {
        isActivated = true;

        // 激活目标
        if (activateTargets != null)
        {
            foreach (var target in activateTargets)
            {
                if (target != null) target.SetActive(true);
            }
        }

        // 特效
        if (activationVFX != null)
            Instantiate(activationVFX, transform.position, Quaternion.identity);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("prism_activate");

        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Success();

        onActivated?.Invoke();

        EventBus.Publish(new PuzzleSolvedEvent
        {
            puzzleId = gameObject.name,
            puzzleType = "LightShadowPrism"
        });
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0.6f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}

// ============ 重量天平 ============
/// <summary>
/// 双人重量天平 - 两个平台互为跷跷板
/// 一方站上去，另一方升高，需要配合才能到达高处
/// </summary>
public class WeightBalance : MonoBehaviour
{
    [Header("平台")]
    [SerializeField] private Transform platformA;
    [SerializeField] private Transform platformB;
    [SerializeField] private float maxHeightDifference = 3f;
    [SerializeField] private float balanceSpeed = 2f;

    [Header("检测")]
    [SerializeField] private Collider2D zoneA;
    [SerializeField] private Collider2D zoneB;

    [Header("绳索")]
    [SerializeField] private LineRenderer rope;
    [SerializeField] private Transform pulley;

    private int playersOnA;
    private int playersOnB;
    private float currentBalance;  // -1 = A下降, 0 = 平衡, 1 = B下降
    private float baseHeightA;
    private float baseHeightB;

    void Start()
    {
        if (platformA != null) baseHeightA = platformA.localPosition.y;
        if (platformB != null) baseHeightB = platformB.localPosition.y;
    }

    void Update()
    {
        // 计算重量差
        float targetBalance = 0f;
        if (playersOnA > playersOnB)
            targetBalance = -1f;
        else if (playersOnB > playersOnA)
            targetBalance = 1f;

        // 平滑过渡
        currentBalance = Mathf.MoveTowards(currentBalance, targetBalance, balanceSpeed * Time.deltaTime);

        // 更新平台位置
        if (platformA != null)
        {
            Vector3 posA = platformA.localPosition;
            posA.y = baseHeightA - currentBalance * maxHeightDifference;
            platformA.localPosition = posA;
        }

        if (platformB != null)
        {
            Vector3 posB = platformB.localPosition;
            posB.y = baseHeightB + currentBalance * maxHeightDifference;
            platformB.localPosition = posB;
        }

        // 更新绳索
        UpdateRope();
    }

    private void UpdateRope()
    {
        if (rope == null || platformA == null || platformB == null) return;

        if (pulley != null)
        {
            rope.positionCount = 4;
            rope.SetPosition(0, platformA.position + Vector3.up * 0.5f);
            rope.SetPosition(1, pulley.position + Vector3.left * 0.3f);
            rope.SetPosition(2, pulley.position + Vector3.right * 0.3f);
            rope.SetPosition(3, platformB.position + Vector3.up * 0.5f);
        }
        else
        {
            rope.positionCount = 2;
            rope.SetPosition(0, platformA.position + Vector3.up * 0.5f);
            rope.SetPosition(1, platformB.position + Vector3.up * 0.5f);
        }
    }

    // 由子触发器调用
    public void OnPlayerEnterA() => playersOnA++;
    public void OnPlayerExitA() => playersOnA = Mathf.Max(0, playersOnA - 1);
    public void OnPlayerEnterB() => playersOnB++;
    public void OnPlayerExitB() => playersOnB = Mathf.Max(0, playersOnB - 1);
}

/// <summary>
/// WeightBalance的触发器子物体
/// </summary>
public class WeightBalanceZone : MonoBehaviour
{
    [SerializeField] private WeightBalance balance;
    [SerializeField] private bool isZoneA = true;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        if (balance == null) return;

        if (isZoneA) balance.OnPlayerEnterA();
        else balance.OnPlayerEnterB();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        if (balance == null) return;

        if (isZoneA) balance.OnPlayerExitA();
        else balance.OnPlayerExitB();
    }
}

// ============ 光桥 ============
/// <summary>
/// Lux站在一端发光，生成可行走的光桥让Nox通过
/// </summary>
public class LightBridge : MonoBehaviour
{
    [Header("桥梁")]
    [SerializeField] private Transform bridgeStart;
    [SerializeField] private Transform bridgeEnd;
    [SerializeField] private SpriteRenderer bridgeRenderer;
    [SerializeField] private Collider2D bridgeCollider;

    [Header("激活条件")]
    [SerializeField] private float activationRadius = 2f;
    [SerializeField] private bool requireLuxOnly = true;

    [Header("视觉")]
    [SerializeField] private Color activeColor = new Color(1f, 0.95f, 0.7f, 0.8f);
    [SerializeField] private Color inactiveColor = new Color(1f, 0.95f, 0.7f, 0.1f);
    [SerializeField] private float fadeSpeed = 4f;
    [SerializeField] private float pulseSpeed = 2f;

    private bool isActive;
    private float currentAlpha;

    void Start()
    {
        if (bridgeCollider != null) bridgeCollider.enabled = false;
        currentAlpha = 0f;
        UpdateVisual();
    }

    void Update()
    {
        bool wasActive = isActive;
        isActive = CheckActivation();

        if (bridgeCollider != null)
            bridgeCollider.enabled = isActive;

        // 渐变效果
        float targetAlpha = isActive ? activeColor.a : inactiveColor.a;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        UpdateVisual();

        // 激活/取消音效
        if (isActive && !wasActive && SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("light_bridge_on");
        else if (!isActive && wasActive && SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("light_bridge_off");
    }

    private bool CheckActivation()
    {
        if (bridgeStart == null) return false;

        var colliders = Physics2D.OverlapCircleAll(bridgeStart.position, activationRadius);
        foreach (var col in colliders)
        {
            var player = col.GetComponent<PlayerController>();
            if (player == null) continue;

            if (requireLuxOnly)
            {
                if (player.Type == PlayerController.PlayerType.Lux)
                    return true;
            }
            else
            {
                return true;
            }
        }
        return false;
    }

    private void UpdateVisual()
    {
        if (bridgeRenderer == null) return;

        Color c = activeColor;
        c.a = currentAlpha;

        // 脉冲效果
        if (isActive)
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.1f;
            c.a += pulse;
        }

        bridgeRenderer.color = c;
    }

    void OnDrawGizmos()
    {
        if (bridgeStart != null && bridgeEnd != null)
        {
            Gizmos.color = new Color(1f, 0.95f, 0.7f, 0.5f);
            Gizmos.DrawLine(bridgeStart.position, bridgeEnd.position);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(bridgeStart.position, activationRadius);
        }
    }
}

// ============ 暗影通道 ============
/// <summary>
/// 只有Nox能通过的暗影门/墙壁
/// Lux需要绕路或寻找其他方式
/// </summary>
public class ShadowPassage : MonoBehaviour
{
    [Header("设置")]
    [SerializeField] private Collider2D passageCollider;
    [SerializeField] private SpriteRenderer passageRenderer;
    [SerializeField] private Color blockedColor = new Color(0.2f, 0f, 0.4f, 0.8f);
    [SerializeField] private Color passableColor = new Color(0.2f, 0f, 0.4f, 0.2f);

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (player.Type == PlayerController.PlayerType.Nox)
        {
            // Nox可以通过 — 临时关闭碰撞
            if (passageCollider != null)
                Physics2D.IgnoreCollision(passageCollider, other, true);

            if (passageRenderer != null)
                passageRenderer.color = passableColor;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (player.Type == PlayerController.PlayerType.Nox)
        {
            if (passageCollider != null)
                Physics2D.IgnoreCollision(passageCollider, other, false);

            if (passageRenderer != null)
                passageRenderer.color = blockedColor;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        var player = collision.collider.GetComponent<PlayerController>();
        if (player != null && player.Type == PlayerController.PlayerType.Lux)
        {
            // Lux无法通过 — 显示提示
            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play("passage_blocked");
        }
    }
}
