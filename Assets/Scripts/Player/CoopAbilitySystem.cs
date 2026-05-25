using UnityEngine;
using System.Collections;

/// <summary>
/// 双人合作技能系统 - Lux和Nox的联合技能
/// 当两个角色满足特定条件时触发合体技能
/// </summary>
public class CoopAbilitySystem : MonoBehaviour
{
    public static CoopAbilitySystem Instance { get; private set; }

    [Header("合体技能设置")]
    [SerializeField] private float coopMeterMax = 100f;
    [SerializeField] private float meterGainPerHit = 5f;
    [SerializeField] private float meterGainPerPuzzle = 15f;
    [SerializeField] private float meterDecayRate = 2f;           // 每秒衰减
    [SerializeField] private float proximityBonus = 1.5f;         // 靠近时增益倍率
    [SerializeField] private float proximityRange = 5f;           // 靠近判定距离

    [Header("技能")]
    [SerializeField] private CoopAbility[] coopAbilities;

    [Header("视觉")]
    [SerializeField] private LineRenderer connectionBeam;          // 连线光束
    [SerializeField] private Color beamNormalColor = new Color(1, 1, 1, 0.3f);
    [SerializeField] private Color beamReadyColor = new Color(1, 0.8f, 0, 1f);
    [SerializeField] private float beamWidth = 0.1f;

    // 运行时
    private float currentMeter;
    private bool isCoopActive;
    private int selectedAbilityIndex;
    private PlayerController luxPlayer;
    private PlayerController noxPlayer;

    public float CoopMeterPercent => currentMeter / coopMeterMax;
    public bool IsCoopReady => currentMeter >= coopMeterMax;
    public bool IsCoopActive => isCoopActive;

    public event System.Action<float> OnMeterChanged;      // percent 0-1
    public event System.Action OnCoopReady;
    public event System.Action<string> OnCoopActivated;    // ability name
    public event System.Action OnCoopEnded;

    [System.Serializable]
    public class CoopAbility
    {
        public string abilityName;
        public string descriptionKey;                      // 本地化key
        public Sprite icon;
        public float duration = 5f;
        public float cooldownAfterUse = 30f;
        public CoopAbilityType type;
        public GameObject vfxPrefab;
        public AudioClip activationSound;
    }

    public enum CoopAbilityType
    {
        LightNova,          // 光之新星 - Lux和Nox释放光明冲击波
        ShadowMerge,        // 暗影融合 - 两人暂时融合为一个强力角色
        DualBarrier,        // 双重屏障 - 光影交织的护盾
        TimeFracture,       // 时间裂隙 - 大范围减速
        Convergence         // 融合光线 - 两人之间的连线造成伤害
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// 初始化合作系统
    /// </summary>
    public void Initialize(PlayerController lux, PlayerController nox)
    {
        luxPlayer = lux;
        noxPlayer = nox;
        currentMeter = 0;

        if (connectionBeam != null)
        {
            connectionBeam.positionCount = 2;
            connectionBeam.startWidth = beamWidth;
            connectionBeam.endWidth = beamWidth;
            connectionBeam.startColor = beamNormalColor;
            connectionBeam.endColor = beamNormalColor;
        }
    }

    void Update()
    {
        if (luxPlayer == null || noxPlayer == null) return;

        UpdateConnectionBeam();
        DecayMeter();
    }

    /// <summary>
    /// 增加合体能量（击中敌人时调用）
    /// </summary>
    public void AddMeter(float amount)
    {
        if (isCoopActive) return;

        // 检查距离加成
        if (luxPlayer != null && noxPlayer != null)
        {
            float dist = Vector2.Distance(
                luxPlayer.transform.position,
                noxPlayer.transform.position
            );
            if (dist <= proximityRange)
                amount *= proximityBonus;
        }

        float prevPercent = CoopMeterPercent;
        currentMeter = Mathf.Min(currentMeter + amount, coopMeterMax);

        OnMeterChanged?.Invoke(CoopMeterPercent);

        // 从未满到满
        if (prevPercent < 1f && CoopMeterPercent >= 1f)
        {
            OnCoopReady?.Invoke();

            if (SoundFeedback.Instance != null)
                SoundFeedback.Instance.Play("coop_ready");

            // 更新光束颜色
            if (connectionBeam != null)
            {
                connectionBeam.startColor = beamReadyColor;
                connectionBeam.endColor = beamReadyColor;
                connectionBeam.startWidth = beamWidth * 2f;
                connectionBeam.endWidth = beamWidth * 2f;
            }
        }
    }

    /// <summary>
    /// 因谜题合作增加能量
    /// </summary>
    public void AddMeterForPuzzle()
    {
        AddMeter(meterGainPerPuzzle);
    }

    /// <summary>
    /// 因攻击增加能量
    /// </summary>
    public void AddMeterForHit()
    {
        AddMeter(meterGainPerHit);
    }

    /// <summary>
    /// 激活合体技能
    /// </summary>
    public void ActivateCoopAbility(int abilityIndex = -1)
    {
        if (!IsCoopReady || isCoopActive) return;
        if (luxPlayer == null || noxPlayer == null) return;

        int index = abilityIndex >= 0 ? abilityIndex : selectedAbilityIndex;
        if (index < 0 || index >= coopAbilities.Length) return;

        var ability = coopAbilities[index];
        StartCoroutine(ExecuteCoopAbility(ability));
    }

    /// <summary>
    /// 选择合体技能
    /// </summary>
    public void SelectAbility(int index)
    {
        if (index >= 0 && index < coopAbilities.Length)
            selectedAbilityIndex = index;
    }

    private IEnumerator ExecuteCoopAbility(CoopAbility ability)
    {
        isCoopActive = true;
        currentMeter = 0;
        OnMeterChanged?.Invoke(0);

        // 激活音效
        if (ability.activationSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(ability.activationSound);

        // 激活特效
        Vector3 midPoint = (luxPlayer.transform.position + noxPlayer.transform.position) / 2f;
        GameObject vfx = null;
        if (ability.vfxPrefab != null)
            vfx = Instantiate(ability.vfxPrefab, midPoint, Quaternion.identity);

        // 相机效果
        if (CameraEffects.Instance != null)
        {
            CameraEffects.Instance.SlowMotion(0.3f, 0.5f);
            CameraEffects.Instance.ChromaticPulse(0.8f, 0.5f);
        }

        if (VFXManager.Instance != null)
            VFXManager.Instance.ShakeHeavy();

        OnCoopActivated?.Invoke(ability.abilityName);

        // 发布合作技能事件（成就追踪）
        EventBus.Publish(new CoopAbilityUsedEvent
        {
            abilityName = ability.abilityName,
            player1Index = 0,
            player2Index = 1
        });

        // 执行技能效果
        switch (ability.type)
        {
            case CoopAbilityType.LightNova:
                yield return ExecuteLightNova(ability.duration);
                break;
            case CoopAbilityType.ShadowMerge:
                yield return ExecuteShadowMerge(ability.duration);
                break;
            case CoopAbilityType.DualBarrier:
                yield return ExecuteDualBarrier(ability.duration);
                break;
            case CoopAbilityType.TimeFracture:
                yield return ExecuteTimeFracture(ability.duration);
                break;
            case CoopAbilityType.Convergence:
                yield return ExecuteConvergence(ability.duration);
                break;
        }

        // 清理
        if (vfx != null) Destroy(vfx);

        // 恢复光束状态
        if (connectionBeam != null)
        {
            connectionBeam.startColor = beamNormalColor;
            connectionBeam.endColor = beamNormalColor;
            connectionBeam.startWidth = beamWidth;
            connectionBeam.endWidth = beamWidth;
        }

        isCoopActive = false;
        OnCoopEnded?.Invoke();
    }

    // ============ 技能实现 ============

    private IEnumerator ExecuteLightNova(float duration)
    {
        // 光之新星：以两人中点为中心释放光圈，对范围内敌人造成伤害
        Vector3 center = (luxPlayer.transform.position + noxPlayer.transform.position) / 2f;
        float radius = 8f;

        // 范围伤害
        var colliders = Physics2D.OverlapCircleAll(center, radius);
        foreach (var col in colliders)
        {
            var enemy = col.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(50);
            }
        }

        yield return new WaitForSeconds(duration);
    }

    private IEnumerator ExecuteShadowMerge(float duration)
    {
        // 暗影融合：Nox暂时隐身，攻击力大幅提升
        // 设置增强状态
        var noxHealth = noxPlayer.GetComponent<PlayerHealth>();
        if (noxHealth != null)
            noxHealth.SetInvincible(true);

        // 视觉：Nox半透明
        var noxRenderer = noxPlayer.GetComponentInChildren<SpriteRenderer>();
        Color originalColor = Color.white;
        if (noxRenderer != null)
        {
            originalColor = noxRenderer.color;
            noxRenderer.color = new Color(0.5f, 0, 1f, 0.5f);
        }

        yield return new WaitForSeconds(duration);

        // 恢复
        if (noxHealth != null)
            noxHealth.SetInvincible(false);
        if (noxRenderer != null)
            noxRenderer.color = originalColor;
    }

    private IEnumerator ExecuteDualBarrier(float duration)
    {
        // 双重屏障：两人都获得护盾
        var luxHealth = luxPlayer.GetComponent<PlayerHealth>();
        var noxHealth = noxPlayer.GetComponent<PlayerHealth>();

        if (luxHealth != null) luxHealth.SetInvincible(true);
        if (noxHealth != null) noxHealth.SetInvincible(true);

        yield return new WaitForSeconds(duration);

        if (luxHealth != null) luxHealth.SetInvincible(false);
        if (noxHealth != null) noxHealth.SetInvincible(false);
    }

    private IEnumerator ExecuteTimeFracture(float duration)
    {
        // 时间裂隙：全局减速
        Time.timeScale = 0.2f;
        Time.fixedDeltaTime = 0.02f * 0.2f;

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    private IEnumerator ExecuteConvergence(float duration)
    {
        // 融合光线：两人之间的连线造成持续伤害
        float elapsed = 0;
        float tickInterval = 0.3f;
        float lastTick = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (elapsed - lastTick >= tickInterval)
            {
                lastTick = elapsed;
                DamageAlongBeam();
            }

            // 加粗光束
            if (connectionBeam != null)
            {
                connectionBeam.startWidth = beamWidth * 4f;
                connectionBeam.endWidth = beamWidth * 4f;
                connectionBeam.startColor = Color.yellow;
                connectionBeam.endColor = Color.cyan;
            }

            yield return null;
        }
    }

    private void DamageAlongBeam()
    {
        if (luxPlayer == null || noxPlayer == null) return;

        Vector2 start = luxPlayer.transform.position;
        Vector2 end = noxPlayer.transform.position;
        Vector2 dir = (end - start).normalized;
        float dist = Vector2.Distance(start, end);

        // Raycast沿连线检测敌人
        var hits = Physics2D.RaycastAll(start, dir, dist);
        foreach (var hit in hits)
        {
            var enemy = hit.collider.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(10);
            }
        }
    }

    // ============ 内部系统 ============

    private void DecayMeter()
    {
        if (isCoopActive || currentMeter <= 0) return;

        currentMeter = Mathf.Max(0, currentMeter - meterDecayRate * Time.deltaTime);
        OnMeterChanged?.Invoke(CoopMeterPercent);
    }

    private void UpdateConnectionBeam()
    {
        if (connectionBeam == null || luxPlayer == null || noxPlayer == null) return;

        connectionBeam.SetPosition(0, luxPlayer.transform.position);
        connectionBeam.SetPosition(1, noxPlayer.transform.position);

        // 距离过远时隐藏
        float dist = Vector2.Distance(luxPlayer.transform.position, noxPlayer.transform.position);
        connectionBeam.enabled = dist <= proximityRange * 2f;
    }

    /// <summary>
    /// 获取当前选择的技能信息
    /// </summary>
    public CoopAbility GetSelectedAbility()
    {
        if (selectedAbilityIndex >= 0 && selectedAbilityIndex < coopAbilities.Length)
            return coopAbilities[selectedAbilityIndex];
        return null;
    }

    /// <summary>
    /// 获取所有合体技能
    /// </summary>
    public CoopAbility[] GetAllAbilities()
    {
        return coopAbilities;
    }
}
