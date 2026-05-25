using UnityEngine;
using System.Collections;

/// <summary>
/// 环境交互物件合集 - 可破坏物、弹簧、梯子、绳索、开关门等
/// 丰富关卡玩法的基础交互元素
/// </summary>

// ============ 可破坏物 ============
/// <summary>
/// 可破坏的墙壁/箱子/障碍物
/// </summary>
public class Breakable : MonoBehaviour, IDamageable
{
    public bool IsAlive => currentHP > 0;
    [Header("破坏设置")]
    [SerializeField] private int hitPoints = 3;
    [SerializeField] private bool requireSpecificCharacter = false;
    [SerializeField] private bool requireShadowPower = false;      // 仅暗影能力可破坏
    [SerializeField] private bool requireLightPower = false;       // 仅光明能力可破坏

    [Header("掉落")]
    [SerializeField] private GameObject[] dropItems;                // 破坏后掉落物品
    [SerializeField] private float dropForce = 3f;

    [Header("视效")]
    [SerializeField] private GameObject breakVFX;
    [SerializeField] private Sprite[] damageSprites;                // 不同损坏程度的贴图
    [SerializeField] private float shakeIntensity = 0.1f;

    private int currentHP;
    private SpriteRenderer spriteRenderer;
    private Vector3 originalPosition;

    void Start()
    {
        currentHP = hitPoints;
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalPosition = transform.position;
    }

    /// <summary>
    /// 受到攻击
    /// </summary>
    public void TakeHit(int damage = 1, bool isShadowAttack = false, bool isLightAttack = false)
    {
        // 检查特殊破坏条件
        if (requireShadowPower && !isShadowAttack) return;
        if (requireLightPower && !isLightAttack) return;

        currentHP -= damage;

        // 更新损坏贴图
        if (spriteRenderer != null && damageSprites != null && damageSprites.Length > 0)
        {
            int spriteIndex = Mathf.Clamp(
                hitPoints - currentHP - 1, 0, damageSprites.Length - 1);
            spriteRenderer.sprite = damageSprites[spriteIndex];
        }

        // 抖动效果
        StartCoroutine(ShakeEffect());

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("breakable_hit");

        if (currentHP <= 0)
            Break();
    }

    private void Break()
    {
        // 破坏特效
        if (breakVFX != null)
            Instantiate(breakVFX, transform.position, Quaternion.identity);

        // 掉落物品
        if (dropItems != null)
        {
            foreach (var item in dropItems)
            {
                if (item == null) continue;
                var spawned = Instantiate(item, transform.position, Quaternion.identity);
                var rb = spawned.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Vector2 randomDir = new Vector2(
                        Random.Range(-1f, 1f),
                        Random.Range(0.5f, 1.5f)
                    ).normalized;
                    rb.AddForce(randomDir * dropForce, ForceMode2D.Impulse);
                }
            }
        }

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("breakable_break");

        // 振动
        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeLight();

        Destroy(gameObject);
    }

    private IEnumerator ShakeEffect()
    {
        float duration = 0.15f;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offsetX = Random.Range(-shakeIntensity, shakeIntensity);
            float offsetY = Random.Range(-shakeIntensity, shakeIntensity);
            transform.position = originalPosition + new Vector3(offsetX, offsetY, 0);
            yield return null;
        }
        transform.position = originalPosition;
    }

    /// <summary>
    /// IDamageable接口实现 — 通用伤害入口（PlayerCombat使用）
    /// </summary>
    public void TakeDamage(int damage)
    {
        TakeHit(damage);
    }

    /// <summary>
    /// 带攻击类型的伤害（Projectile使用）
    /// </summary>
    public void TakeDamage(int damage, string attackType)
    {
        bool isShadow = attackType == "shadow";
        bool isLight = attackType == "light";
        TakeHit(damage, isShadow, isLight);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 玩家冲刺时可以撞碎
        var player = collision.collider.GetComponent<PlayerController>();
        if (player != null && player.IsDashing)
        {
            TakeHit(2);
        }
    }
}

// ============ 弹簧 ============
/// <summary>
/// 弹跳平台 - 踩上去会弹飞
/// </summary>
public class SpringPad : MonoBehaviour
{
    [Header("弹跳设置")]
    [SerializeField] private float bounceForce = 15f;
    [SerializeField] private Vector2 bounceDirection = Vector2.up;
    [SerializeField] private bool overrideVelocity = true;          // 是否覆盖当前速度

    [Header("动画")]
    [SerializeField] private Animator animator;
    [SerializeField] private string bounceAnimTrigger = "Bounce";

    void OnTriggerEnter2D(Collider2D other)
    {
        var rb = other.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        // 仅弹起角色
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (overrideVelocity)
            rb.linearVelocity = Vector2.zero;

        rb.AddForce(bounceDirection.normalized * bounceForce, ForceMode2D.Impulse);

        // 播放动画
        if (animator != null)
            animator.SetTrigger(bounceAnimTrigger);

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("spring_bounce");
    }
}

// ============ 梯子 ============
/// <summary>
/// 可攀爬的梯子
/// </summary>
public class Ladder : MonoBehaviour
{
    [Header("梯子设置")]
    [SerializeField] private float climbSpeed = 4f;
    [SerializeField] private bool disableGravityOnClimb = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null)
            player.EnterLadder(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null)
            player.ExitLadder();
    }

    public float ClimbSpeed => climbSpeed;
    public bool DisableGravity => disableGravityOnClimb;
}

// ============ 单向平台 ============
/// <summary>
/// 只能从下方穿过，从上方站立的平台
/// </summary>
[RequireComponent(typeof(PlatformEffector2D))]
public class OneWayPlatform : MonoBehaviour
{
    [SerializeField] private float dropDownTime = 0.3f;  // 按下+跳跃后平台消失时间

    private PlatformEffector2D effector;
    private Collider2D platformCollider;

    void Start()
    {
        effector = GetComponent<PlatformEffector2D>();
        platformCollider = GetComponent<Collider2D>();

        effector.useOneWay = true;
        effector.surfaceArc = 170f;
    }

    /// <summary>
    /// 玩家从平台上按下跳，暂时关闭碰撞
    /// </summary>
    public void DropDown(Collider2D playerCollider)
    {
        StartCoroutine(TemporaryDisable(playerCollider));
    }

    private IEnumerator TemporaryDisable(Collider2D playerCollider)
    {
        Physics2D.IgnoreCollision(platformCollider, playerCollider, true);
        yield return new WaitForSeconds(dropDownTime);
        Physics2D.IgnoreCollision(platformCollider, playerCollider, false);
    }
}

// ============ 拉杆/开关 ============
/// <summary>
/// 可拉动的杠杆开关
/// </summary>
public class LeverSwitch : MonoBehaviour
{
    [Header("开关设置")]
    [SerializeField] private bool isOn = false;
    [SerializeField] private bool isToggle = true;                  // 是否可反复切换
    [SerializeField] private bool requireBothPlayers = false;       // 是否需双人

    [Header("控制目标")]
    [SerializeField] private GameObject[] targetObjects;            // 激活/停用的目标
    [SerializeField] private Animator leverAnimator;

    [Header("视觉")]
    [SerializeField] private SpriteRenderer indicatorLight;
    [SerializeField] private Color onColor = Color.green;
    [SerializeField] private Color offColor = Color.red;

    private int playersInRange;

    public bool IsOn => isOn;
    public event System.Action<bool> OnSwitched;

    void Start()
    {
        UpdateVisuals();
        ApplyState();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        playersInRange++;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        playersInRange = Mathf.Max(0, playersInRange - 1);
    }

    /// <summary>
    /// 玩家按下交互键时调用
    /// </summary>
    public void Interact()
    {
        if (requireBothPlayers && playersInRange < 2) return;

        if (isToggle)
        {
            isOn = !isOn;
        }
        else
        {
            if (isOn) return; // 不可反复开启
            isOn = true;
        }

        UpdateVisuals();
        ApplyState();

        // 音效
        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play(isOn ? "switch_on" : "switch_off");

        OnSwitched?.Invoke(isOn);
    }

    private void UpdateVisuals()
    {
        if (indicatorLight != null)
            indicatorLight.color = isOn ? onColor : offColor;

        if (leverAnimator != null)
            leverAnimator.SetBool("IsOn", isOn);
    }

    private void ApplyState()
    {
        if (targetObjects == null) return;

        foreach (var obj in targetObjects)
        {
            if (obj != null)
                obj.SetActive(isOn);
        }
    }
}

// ============ 钥匙 & 锁门 ============
/// <summary>
/// 可收集的钥匙
/// </summary>
public class Key : MonoBehaviour
{
    public enum KeyColor { Gold, Silver, Red, Blue, Green }

    [SerializeField] private KeyColor keyColor = KeyColor.Gold;
    [SerializeField] private GameObject pickupVFX;

    public KeyColor Color => keyColor;

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 通知关卡管理器收集了钥匙
        var inventory = other.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            inventory.AddKey(keyColor);

            if (pickupVFX != null)
                Instantiate(pickupVFX, transform.position, Quaternion.identity);

            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play("key_pickup");

            Destroy(gameObject);
        }
    }
}

/// <summary>
/// 需要钥匙才能打开的门
/// </summary>
public class LockedDoor : MonoBehaviour
{
    [SerializeField] private Key.KeyColor requiredKey = Key.KeyColor.Gold;
    [SerializeField] private int keysRequired = 1;
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private Collider2D doorCollider;
    [SerializeField] private GameObject openVFX;

    private bool isOpen = false;

    public bool IsOpen => isOpen;
    public event System.Action OnDoorOpened;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isOpen) return;

        var inventory = other.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        if (inventory.HasKey(requiredKey, keysRequired))
        {
            inventory.UseKeys(requiredKey, keysRequired);
            OpenDoor();
        }
    }

    public void OpenDoor()
    {
        if (isOpen) return;
        isOpen = true;

        if (doorAnimator != null)
            doorAnimator.SetTrigger("Open");

        if (doorCollider != null)
            doorCollider.enabled = false;

        if (openVFX != null)
            Instantiate(openVFX, transform.position, Quaternion.identity);

        if (SoundFeedback.Instance != null)
            SoundFeedback.Instance.Play("door_open");

        // 发布事件
        EventBus.Publish(new DoorOpenedEvent
        {
            doorId = gameObject.name,
            position = transform.position
        });

        OnDoorOpened?.Invoke();
    }
}

// ============ 玩家物品栏 ============
/// <summary>
/// 玩家简单物品栏 - 管理钥匙等可收集道具
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    private System.Collections.Generic.Dictionary<Key.KeyColor, int> keys =
        new System.Collections.Generic.Dictionary<Key.KeyColor, int>();

    public event System.Action<Key.KeyColor, int> OnKeyCountChanged;

    public void AddKey(Key.KeyColor color)
    {
        if (!keys.ContainsKey(color))
            keys[color] = 0;
        keys[color]++;
        OnKeyCountChanged?.Invoke(color, keys[color]);
    }

    public bool HasKey(Key.KeyColor color, int count = 1)
    {
        return keys.ContainsKey(color) && keys[color] >= count;
    }

    public void UseKeys(Key.KeyColor color, int count = 1)
    {
        if (HasKey(color, count))
        {
            keys[color] -= count;
            OnKeyCountChanged?.Invoke(color, keys[color]);
        }
    }

    public int GetKeyCount(Key.KeyColor color)
    {
        return keys.ContainsKey(color) ? keys[color] : 0;
    }

    public void ClearAll()
    {
        keys.Clear();
    }
}

// ============ 触发式对话区域 ============
/// <summary>
/// 进入区域时触发对话/提示
/// </summary>
public class DialogueZone : MonoBehaviour
{
    [SerializeField] private DialogueSystem.DialogueSequence dialogue;
    [SerializeField] private bool playOnce = true;
    [SerializeField] private bool pauseGameplay = false;

    private bool hasTriggered = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered && playOnce) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (DialogueSystem.Instance != null && dialogue != null)
        {
            dialogue.pauseGameplay = pauseGameplay;
            DialogueSystem.Instance.StartDialogue(dialogue);
            hasTriggered = true;
        }
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
    }
}

// ============ 传送带 ============
/// <summary>
/// 传送带 - 站在上面会被推动
/// </summary>
public class ConveyorBelt : MonoBehaviour
{
    [Header("传送带设置")]
    [SerializeField] private float speed = 3f;
    [SerializeField] private Vector2 direction = Vector2.right;
    [SerializeField] private bool affectEnemies = true;

    [Header("视觉")]
    [SerializeField] private float uvScrollSpeed = 1f;

    private Material mat;

    void Start()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            mat = sr.material;
        }
    }

    void Update()
    {
        // 滚动贴图
        if (mat != null)
        {
            Vector2 offset = mat.mainTextureOffset;
            offset.x += uvScrollSpeed * Time.deltaTime * Mathf.Sign(direction.x);
            mat.mainTextureOffset = offset;
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        var rb = other.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        bool isPlayer = other.GetComponent<PlayerController>() != null;
        bool isEnemy = other.GetComponent<EnemyBase>() != null;

        if (isPlayer || (affectEnemies && isEnemy))
        {
            rb.AddForce(direction.normalized * speed, ForceMode2D.Force);
        }
    }

    /// <summary>
    /// 反转传送带方向
    /// </summary>
    public void ReverseDirection()
    {
        direction = -direction;
        uvScrollSpeed = -uvScrollSpeed;
    }
}
