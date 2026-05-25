using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 浮动伤害数字系统 - 显示伤害/治疗/暴击等数字弹出效果
/// 使用对象池管理，带动画上浮、缩放、淡出
/// </summary>
public class FloatingDamageText : MonoBehaviour
{
    public static FloatingDamageText Instance { get; private set; }

    [Header("预制体")]
    [SerializeField] private GameObject damageTextPrefab;

    [Header("动画参数")]
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float floatDistance = 1.5f;
    [SerializeField] private float fadeDuration = 0.8f;
    [SerializeField] private float scaleInDuration = 0.15f;
    [SerializeField] private float maxScale = 1.3f;

    [Header("颜色")]
    [SerializeField] private Color normalDamageColor = new Color(1f, 0.9f, 0.9f);
    [SerializeField] private Color criticalDamageColor = new Color(1f, 0.3f, 0.1f);
    [SerializeField] private Color healColor = new Color(0.3f, 1f, 0.3f);
    [SerializeField] private Color shieldColor = new Color(0.3f, 0.7f, 1f);
    [SerializeField] private Color comboColor = new Color(1f, 0.85f, 0.2f);

    [Header("偏移随机")]
    [SerializeField] private float horizontalSpread = 0.5f;
    [SerializeField] private float verticalOffset = 0.8f;

    // 对象池
    private Queue<DamageTextInstance> pool = new Queue<DamageTextInstance>();
    private const int POOL_SIZE = 20;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        PrewarmPool();
    }

    void Start()
    {
        // 订阅事件
        EventBus.Subscribe<EnemyHitEvent>(OnEnemyHit);
        EventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Subscribe<PlayerHealEvent>(OnPlayerHealed);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<EnemyHitEvent>(OnEnemyHit);
        EventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Unsubscribe<PlayerHealEvent>(OnPlayerHealed);
        if (Instance == this) Instance = null;
    }

    // ==================== 公共接口 ====================

    /// <summary>
    /// 显示伤害数字
    /// </summary>
    public void ShowDamage(Vector3 worldPos, int amount, bool isCritical = false)
    {
        Color color = isCritical ? criticalDamageColor : normalDamageColor;
        float scale = isCritical ? maxScale * 1.3f : maxScale;
        string text = isCritical ? $"{amount}!" : amount.ToString();

        SpawnText(worldPos, text, color, scale);
    }

    /// <summary>
    /// 显示治疗数字
    /// </summary>
    public void ShowHeal(Vector3 worldPos, int amount)
    {
        SpawnText(worldPos, $"+{amount}", healColor, maxScale);
    }

    /// <summary>
    /// 显示护盾伤害（被格挡）
    /// </summary>
    public void ShowBlocked(Vector3 worldPos)
    {
        string text = LocalizationSystem.Instance != null
            ? LocalizationSystem.Instance.GetText("damage_blocked")
            : "Blocked";
        SpawnText(worldPos, text, shieldColor, maxScale * 0.9f);
    }

    /// <summary>
    /// 显示连击数字
    /// </summary>
    public void ShowCombo(Vector3 worldPos, int comboCount)
    {
        SpawnText(worldPos, $"x{comboCount}", comboColor, maxScale * (1f + comboCount * 0.05f));
    }

    /// <summary>
    /// 显示自定义文本
    /// </summary>
    public void ShowCustom(Vector3 worldPos, string text, Color color)
    {
        SpawnText(worldPos, text, color, maxScale);
    }

    // ==================== 事件处理 ====================

    private void OnEnemyHit(EnemyHitEvent e)
    {
        bool isCritical = e.damage >= 3; // 高伤判定暴击
        ShowDamage(e.position, e.damage, isCritical);
    }

    private void OnPlayerDamaged(PlayerDamagedEvent e)
    {
        // 寻找玩家位置
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.PlayerIndex == e.playerIndex)
            {
                ShowDamage(p.transform.position, Mathf.CeilToInt(e.damage));
                break;
            }
        }
    }

    private void OnPlayerHealed(PlayerHealEvent e)
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.PlayerIndex == e.playerIndex)
            {
                ShowHeal(p.transform.position, Mathf.CeilToInt(e.amount));
                break;
            }
        }
    }

    // ==================== 内部实现 ====================

    private void SpawnText(Vector3 worldPos, string text, Color color, float targetScale)
    {
        // 添加随机偏移避免重叠
        float offsetX = Random.Range(-horizontalSpread, horizontalSpread);
        worldPos += new Vector3(offsetX, verticalOffset, 0);

        var instance = GetFromPool();
        if (instance == null) return;

        instance.gameObject.SetActive(true);
        instance.transform.position = worldPos;

        if (instance.text != null)
        {
            instance.text.text = text;
            instance.text.color = color;
            instance.text.fontSize = 0.5f;
        }

        StartCoroutine(AnimateText(instance, color, targetScale));
    }

    private IEnumerator AnimateText(DamageTextInstance instance, Color color, float targetScale)
    {
        float elapsed = 0f;
        Vector3 startPos = instance.transform.position;
        Vector3 endPos = startPos + Vector3.up * floatDistance;

        // 弹入阶段
        float scaleElapsed = 0f;
        while (scaleElapsed < scaleInDuration)
        {
            scaleElapsed += Time.deltaTime;
            elapsed += Time.deltaTime;

            float scaleT = scaleElapsed / scaleInDuration;
            // 弹性曲线
            float scale = Mathf.Lerp(0f, targetScale, EaseOutBack(scaleT));
            instance.transform.localScale = Vector3.one * scale;

            // 同时上浮
            float moveT = elapsed / fadeDuration;
            instance.transform.position = Vector3.Lerp(startPos, endPos, moveT);

            yield return null;
        }

        // 上浮 + 淡出阶段
        instance.transform.localScale = Vector3.one * targetScale;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;

            // 上浮
            instance.transform.position = Vector3.Lerp(startPos, endPos, t);

            // 缩小
            float shrink = Mathf.Lerp(targetScale, targetScale * 0.5f, t);
            instance.transform.localScale = Vector3.one * shrink;

            // 淡出
            if (instance.text != null)
            {
                Color c = color;
                c.a = Mathf.Lerp(1f, 0f, t * t);
                instance.text.color = c;
            }

            yield return null;
        }

        ReturnToPool(instance);
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);
    }

    // ==================== 对象池 ====================

    private void PrewarmPool()
    {
        if (damageTextPrefab == null) return;

        for (int i = 0; i < POOL_SIZE; i++)
        {
            var go = Instantiate(damageTextPrefab, transform);
            go.SetActive(false);
            var instance = new DamageTextInstance
            {
                gameObject = go,
                transform = go.transform,
                text = go.GetComponent<TMPro.TextMeshPro>()
            };

            // 如果没有TextMeshPro，尝试TextMesh
            if (instance.text == null)
            {
                var tm = go.GetComponent<TextMesh>();
                if (tm != null)
                {
                    // 使用适配器
                    instance.legacyText = tm;
                }
            }

            pool.Enqueue(instance);
        }
    }

    private DamageTextInstance GetFromPool()
    {
        if (pool.Count > 0)
            return pool.Dequeue();

        // 池耗尽，创建新的
        if (damageTextPrefab == null) return null;

        var go = Instantiate(damageTextPrefab, transform);
        return new DamageTextInstance
        {
            gameObject = go,
            transform = go.transform,
            text = go.GetComponent<TMPro.TextMeshPro>(),
            legacyText = go.GetComponent<TextMesh>()
        };
    }

    private void ReturnToPool(DamageTextInstance instance)
    {
        instance.gameObject.SetActive(false);
        pool.Enqueue(instance);
    }

    private class DamageTextInstance
    {
        public GameObject gameObject;
        public Transform transform;
        public TMPro.TextMeshPro text;
        public TextMesh legacyText;
    }
}
