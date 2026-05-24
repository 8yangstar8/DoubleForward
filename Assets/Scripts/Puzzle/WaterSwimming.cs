using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 水域游泳系统 - 玩家进入水域后的完整行为控制
/// 管理浮力、游泳移动、氧气、水面检测、视觉效果
/// 可与WaterCurrent配合使用
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class WaterSwimming : MonoBehaviour
{
    [Header("水域设置")]
    [SerializeField] private float buoyancyForce = 8f;
    [SerializeField] private float waterDrag = 4f;
    [SerializeField] private float swimSpeed = 3.5f;
    [SerializeField] private float swimJumpForce = 6f;
    [SerializeField] private float surfaceY = 0f; // 水面Y坐标（相对本地）

    [Header("氧气系统")]
    [SerializeField] private bool useOxygen = true;
    [SerializeField] private float maxOxygen = 10f;
    [SerializeField] private float oxygenDrainRate = 1f;
    [SerializeField] private float oxygenRefillRate = 5f;
    [SerializeField] private float drowningDamageInterval = 1f;
    [SerializeField] private int drowningDamage = 1;

    [Header("视觉效果")]
    [SerializeField] private Color underwaterTint = new Color(0.2f, 0.4f, 0.8f, 0.3f);
    [SerializeField] private float bubbleInterval = 0.5f;
    [SerializeField] private GameObject splashEffectPrefab;
    [SerializeField] private GameObject bubbleEffectPrefab;

    [Header("音效")]
    [SerializeField] private string splashSoundKey = "water_splash";
    [SerializeField] private string underwaterAmbientKey = "underwater_ambient";
    [SerializeField] private string swimSoundKey = "swim_stroke";

    // 追踪水中的玩家
    private Dictionary<PlayerController, SwimmerState> swimmers = new Dictionary<PlayerController, SwimmerState>();

    private class SwimmerState
    {
        public Rigidbody2D rb;
        public PlayerHealth health;
        public float originalGravity;
        public float originalDrag;
        public float currentOxygen;
        public float drowningTimer;
        public float bubbleTimer;
        public bool isSubmerged; // 完全在水下
        public bool isSurfacing; // 在水面
    }

    void Awake()
    {
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;

        // 计算世界空间的水面Y坐标
        surfaceY = transform.position.y + GetComponent<BoxCollider2D>().offset.y
                  + GetComponent<BoxCollider2D>().size.y * 0.5f * transform.lossyScale.y;
    }

    void Update()
    {
        foreach (var kvp in swimmers)
        {
            UpdateSwimmer(kvp.Key, kvp.Value);
        }
    }

    void FixedUpdate()
    {
        foreach (var kvp in swimmers)
        {
            ApplyWaterPhysics(kvp.Key, kvp.Value);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;
        if (swimmers.ContainsKey(player)) return;

        var rb = other.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        var state = new SwimmerState
        {
            rb = rb,
            health = other.GetComponent<PlayerHealth>(),
            originalGravity = rb.gravityScale,
            originalDrag = rb.linearDamping,
            currentOxygen = maxOxygen,
            drowningTimer = 0f,
            bubbleTimer = 0f,
            isSubmerged = false,
            isSurfacing = false
        };

        swimmers[player] = state;

        // 修改物理
        rb.gravityScale = 0.3f;
        rb.linearDamping = waterDrag;

        // 入水溅射效果
        SpawnSplash(other.transform.position);

        // 发布入水事件
        EventBus.Publish(new WaterEnteredEvent
        {
            playerIndex = player.PlayerIndex,
            position = other.transform.position
        });

        Debug.Log($"[Water] Player {player.PlayerIndex} entered water");
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;
        if (!swimmers.ContainsKey(player)) return;

        var state = swimmers[player];

        // 恢复物理
        state.rb.gravityScale = state.originalGravity;
        state.rb.linearDamping = state.originalDrag;

        // 出水溅射效果
        SpawnSplash(other.transform.position);

        swimmers.Remove(player);

        // 发布出水事件
        EventBus.Publish(new WaterExitedEvent
        {
            playerIndex = player.PlayerIndex,
            position = other.transform.position
        });
    }

    private void UpdateSwimmer(PlayerController player, SwimmerState state)
    {
        if (player == null) return;

        float playerY = player.transform.position.y;
        bool wasSubmerged = state.isSubmerged;

        // 判断是否完全在水下
        state.isSubmerged = playerY < surfaceY - 0.5f;
        state.isSurfacing = playerY >= surfaceY - 0.5f && playerY <= surfaceY + 0.3f;

        // 水面检测变化
        if (state.isSubmerged && !wasSubmerged)
        {
            // 刚潜入水中
        }
        else if (!state.isSubmerged && wasSubmerged)
        {
            // 刚浮出水面
            state.currentOxygen = maxOxygen; // 出水面立即恢复
        }

        // 氧气系统
        if (useOxygen)
        {
            if (state.isSubmerged)
            {
                state.currentOxygen -= oxygenDrainRate * Time.deltaTime;
                state.currentOxygen = Mathf.Max(0, state.currentOxygen);

                // 溺水伤害
                if (state.currentOxygen <= 0)
                {
                    state.drowningTimer += Time.deltaTime;
                    if (state.drowningTimer >= drowningDamageInterval)
                    {
                        state.drowningTimer = 0f;
                        state.health?.TakeDamage(drowningDamage);
                    }
                }
            }
            else
            {
                // 水面恢复氧气
                state.currentOxygen += oxygenRefillRate * Time.deltaTime;
                state.currentOxygen = Mathf.Min(maxOxygen, state.currentOxygen);
                state.drowningTimer = 0f;
            }
        }

        // 气泡效果
        if (state.isSubmerged)
        {
            state.bubbleTimer += Time.deltaTime;
            if (state.bubbleTimer >= bubbleInterval)
            {
                state.bubbleTimer = 0f;
                SpawnBubble(player.transform.position + Vector3.up * 0.3f);
            }
        }
    }

    private void ApplyWaterPhysics(PlayerController player, SwimmerState state)
    {
        if (player == null || state.rb == null) return;

        float playerY = player.transform.position.y;

        // 浮力 - 越深越强
        float depth = surfaceY - playerY;
        if (depth > 0)
        {
            float buoyancy = Mathf.Clamp(depth * buoyancyForce, 0, buoyancyForce * 2f);
            state.rb.AddForce(Vector2.up * buoyancy);
        }

        // 在水面附近稳定
        if (state.isSurfacing)
        {
            float surfacePull = (surfaceY - 0.2f - playerY) * 5f;
            state.rb.AddForce(Vector2.up * surfacePull);

            // 减少垂直速度抖动
            if (Mathf.Abs(state.rb.linearVelocity.y) < 0.5f)
            {
                state.rb.linearVelocity = new Vector2(
                    state.rb.linearVelocity.x,
                    state.rb.linearVelocity.y * 0.9f
                );
            }
        }

        // 游泳移动输入
        if (InputManager.Instance != null)
        {
            Vector2 input = InputManager.Instance.GetMoveInput(player.PlayerIndex);

            // 水中可以上下移动
            if (state.isSubmerged)
            {
                Vector2 swimForce = input * swimSpeed;
                state.rb.AddForce(swimForce);
            }
            else
            {
                // 水面只能水平移动
                state.rb.AddForce(new Vector2(input.x * swimSpeed, 0));
            }

            // 水中跳跃 = 向上推进
            if (InputManager.Instance.GetJumpPressed(player.PlayerIndex))
            {
                if (state.isSurfacing)
                {
                    // 从水面跳出
                    state.rb.linearVelocity = new Vector2(state.rb.linearVelocity.x, swimJumpForce);
                }
                else if (state.isSubmerged)
                {
                    // 水中向上游
                    state.rb.AddForce(Vector2.up * swimJumpForce * 0.6f, ForceMode2D.Impulse);
                }
            }
        }

        // 限制水中最大速度
        float maxSpeed = swimSpeed * 1.5f;
        if (state.rb.linearVelocity.magnitude > maxSpeed)
        {
            state.rb.linearVelocity = state.rb.linearVelocity.normalized * maxSpeed;
        }
    }

    // ============ 效果 ============

    private void SpawnSplash(Vector3 position)
    {
        Vector3 surfacePos = new Vector3(position.x, surfaceY, position.z);

        if (splashEffectPrefab != null)
        {
            var splash = Instantiate(splashEffectPrefab, surfacePos, Quaternion.identity);
            Destroy(splash, 2f);
        }

        AudioManager.Instance?.PlaySFX(splashSoundKey);
    }

    private void SpawnBubble(Vector3 position)
    {
        if (bubbleEffectPrefab != null)
        {
            var bubble = Instantiate(bubbleEffectPrefab, position, Quaternion.identity);
            Destroy(bubble, 3f);
        }
    }

    // ============ 公共查询 ============

    /// <summary>
    /// 玩家是否在此水域中
    /// </summary>
    public bool IsPlayerInWater(PlayerController player)
    {
        return swimmers.ContainsKey(player);
    }

    /// <summary>
    /// 获取玩家当前氧气百分比
    /// </summary>
    public float GetOxygenPercent(PlayerController player)
    {
        if (swimmers.TryGetValue(player, out var state))
            return state.currentOxygen / maxOxygen;
        return 1f;
    }

    /// <summary>
    /// 获取玩家当前氧气值
    /// </summary>
    public float GetCurrentOxygen(PlayerController player)
    {
        if (swimmers.TryGetValue(player, out var state))
            return state.currentOxygen;
        return maxOxygen;
    }

    /// <summary>
    /// 玩家是否完全在水下
    /// </summary>
    public bool IsPlayerSubmerged(PlayerController player)
    {
        if (swimmers.TryGetValue(player, out var state))
            return state.isSubmerged;
        return false;
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        // 画水域
        Gizmos.color = new Color(0.1f, 0.3f, 0.9f, 0.25f);
        Vector3 center = transform.position + (Vector3)col.offset;
        Vector3 size = Vector3.Scale(col.size, transform.lossyScale);
        Gizmos.DrawCube(center, size);

        // 画水面线
        float worldSurfaceY = transform.position.y + col.offset.y + col.size.y * 0.5f * transform.lossyScale.y;
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        Vector3 left = new Vector3(center.x - size.x * 0.5f, worldSurfaceY, 0);
        Vector3 right = new Vector3(center.x + size.x * 0.5f, worldSurfaceY, 0);
        Gizmos.DrawLine(left, right);
    }
}
// WaterEnteredEvent / WaterExitedEvent 定义在 Core/EventBus.cs 中
