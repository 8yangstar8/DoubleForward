using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 敌人血条UI - 在敌人头顶显示浮动血条
/// 使用World Space Canvas，自动跟随敌人位置
/// 受伤时显示，满血或死亡后隐藏
/// </summary>
public class EnemyHealthBarUI : MonoBehaviour
{
    [Header("组件")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Image delayFillImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("位置")]
    [SerializeField] private Vector3 offset = new Vector3(0, 1.2f, 0);
    [SerializeField] private bool faceCamera = true;

    [Header("颜色")]
    [SerializeField] private Color fullColor = new Color(0.2f, 0.9f, 0.2f);
    [SerializeField] private Color midColor = new Color(0.9f, 0.8f, 0.1f);
    [SerializeField] private Color lowColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private Color delayColor = new Color(1f, 1f, 1f, 0.5f);

    [Header("行为")]
    [SerializeField] private float showDuration = 3f;        // 受伤后显示时间
    [SerializeField] private float fadeSpeed = 3f;
    [SerializeField] private float delayBarSpeed = 2f;
    [SerializeField] private float delayBarWait = 0.4f;
    [SerializeField] private bool hideWhenFull = true;

    private EnemyBase enemy;
    private Transform enemyTransform;
    private float currentFill = 1f;
    private float delayFill = 1f;
    private float delayTimer;
    private float showTimer;
    private float targetAlpha;
    private Camera mainCam;

    void Start()
    {
        enemy = GetComponentInParent<EnemyBase>();
        if (enemy == null)
        {
            // 作为子物体挂载
            enemy = transform.parent?.GetComponent<EnemyBase>();
        }

        if (enemy != null)
        {
            enemyTransform = enemy.transform;
            enemy.OnDamaged += OnEnemyDamaged;
            enemy.OnDeath += OnEnemyDeath;
        }

        mainCam = Camera.main;

        if (delayFillImage != null)
            delayFillImage.color = delayColor;

        // 初始隐藏
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        targetAlpha = 0f;
    }

    void OnDestroy()
    {
        if (enemy != null)
        {
            enemy.OnDamaged -= OnEnemyDamaged;
            enemy.OnDeath -= OnEnemyDeath;
        }
    }

    void LateUpdate()
    {
        if (enemy == null || enemy.IsDead) return;

        // 跟随位置
        if (enemyTransform != null)
            transform.position = enemyTransform.position + offset;

        // 面向摄像机
        if (faceCamera && mainCam != null)
            transform.forward = mainCam.transform.forward;

        // 更新血量
        float healthPercent = enemy.HealthPercent;
        currentFill = Mathf.MoveTowards(currentFill, healthPercent, 5f * Time.deltaTime);

        if (fillImage != null)
        {
            fillImage.fillAmount = currentFill;
            fillImage.color = GetHealthColor(currentFill);
        }

        // 延迟条
        if (delayFillImage != null)
        {
            if (delayFill > currentFill)
            {
                delayTimer += Time.deltaTime;
                if (delayTimer >= delayBarWait)
                    delayFill = Mathf.MoveTowards(delayFill, currentFill, delayBarSpeed * Time.deltaTime);
            }
            else
            {
                delayFill = currentFill;
                delayTimer = 0f;
            }
            delayFillImage.fillAmount = delayFill;
        }

        // 显示/隐藏
        if (showTimer > 0)
        {
            showTimer -= Time.deltaTime;
            if (showTimer <= 0 && hideWhenFull && healthPercent >= 1f)
                targetAlpha = 0f;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha,
                fadeSpeed * Time.deltaTime);
        }
    }

    private Color GetHealthColor(float percent)
    {
        if (percent > 0.6f)
            return Color.Lerp(midColor, fullColor, (percent - 0.6f) / 0.4f);
        else if (percent > 0.3f)
            return Color.Lerp(lowColor, midColor, (percent - 0.3f) / 0.3f);
        else
            return lowColor;
    }

    private void OnEnemyDamaged(float damage)
    {
        targetAlpha = 1f;
        showTimer = showDuration;
        delayTimer = 0f;
    }

    private void OnEnemyDeath()
    {
        targetAlpha = 0f;
        showTimer = 0f;
    }
}
