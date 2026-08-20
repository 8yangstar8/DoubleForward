using UnityEngine;

/// <summary>
/// Boss 合作护盾 - 把第一章 Boss 战变成真正需要两人配合的战斗。
///
/// Boss 常态带护盾、免疫一切伤害。只有 Lux 用光束照亮它的弱点(一个光敏机关),
/// 护盾才会落下一小段时间,Nox 趁这个窗口输出。
/// 一个人打不动:只有 Lux 有光束,而近战输出主要靠 Nox。
/// </summary>
public class BossCoopShield : MonoBehaviour
{
    [SerializeField] private BossBase boss;
    [SerializeField] private LightSensor weakPoint;
    [SerializeField] private float exposedDuration = 5f;
    [SerializeField] private GameObject shieldVisual;

    private float exposedTimer;

    /// <summary>护盾是否已落下(可被伤害)</summary>
    public bool IsExposed => exposedTimer > 0f;

    /// <summary>弱点机关。关卡里可能不止一个 LightSensor,测试要认准这一个</summary>
    public LightSensor WeakPoint => weakPoint;

    void Awake()
    {
        if (boss == null) boss = GetComponent<BossBase>();
    }

    void Start()
    {
        if (boss != null) boss.SetShield(true);   // 开场带盾
        if (shieldVisual != null) shieldVisual.SetActive(true);
    }

    void Update()
    {
        if (boss == null) return;

        // 弱点被照到就刷新暴露时长(机关不锁存,光束移开后靠计时维持窗口)
        if (weakPoint != null && weakPoint.IsActivated)
            exposedTimer = exposedDuration;
        else if (exposedTimer > 0f)
            exposedTimer -= Time.deltaTime;

        bool shielded = exposedTimer <= 0f;
        boss.SetShield(shielded);

        // 护盾必须看得见。没有它,玩家看到的只是"打上去不掉血",
        // 既不知道为什么,也不知道该做什么 —— 机制等于不存在。
        if (shieldVisual != null && shieldVisual.activeSelf != shielded)
            shieldVisual.SetActive(shielded);
    }

    /// <summary>编辑器配置用</summary>
    public void Configure(BossBase target, LightSensor sensor, float duration, GameObject visual)
    {
        boss = target;
        weakPoint = sensor;
        exposedDuration = duration;
        shieldVisual = visual;
    }
}
