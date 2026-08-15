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

    private float exposedTimer;

    /// <summary>护盾是否已落下(可被伤害)</summary>
    public bool IsExposed => exposedTimer > 0f;

    void Awake()
    {
        if (boss == null) boss = GetComponent<BossBase>();
    }

    void Start()
    {
        if (boss != null) boss.SetShield(true);   // 开场带盾
    }

    void Update()
    {
        if (boss == null) return;

        // 弱点被照到就刷新暴露时长(机关不锁存,光束移开后靠计时维持窗口)
        if (weakPoint != null && weakPoint.IsActivated)
            exposedTimer = exposedDuration;
        else if (exposedTimer > 0f)
            exposedTimer -= Time.deltaTime;

        boss.SetShield(exposedTimer <= 0f);
    }

    /// <summary>编辑器配置用</summary>
    public void Configure(BossBase target, LightSensor sensor, float duration)
    {
        boss = target;
        weakPoint = sensor;
        exposedDuration = duration;
    }
}
