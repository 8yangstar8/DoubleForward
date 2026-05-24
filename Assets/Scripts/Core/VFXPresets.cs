using UnityEngine;

/// <summary>
/// VFX预设配置 - ScriptableObject集中管理所有粒子特效参数
/// 方便美术在Inspector中调整而无需修改代码
/// </summary>
[CreateAssetMenu(fileName = "VFXPresets", menuName = "DoubleForward/VFX Presets")]
public class VFXPresets : ScriptableObject
{
    [Header("玩家特效")]
    public VFXEntry playerJump;
    public VFXEntry playerLand;
    public VFXEntry playerDash;
    public VFXEntry playerDashTrail;
    public VFXEntry playerHit;
    public VFXEntry playerDeath;
    public VFXEntry playerRespawn;
    public VFXEntry playerHeal;

    [Header("Lux技能特效")]
    public VFXEntry luxLightBeam;
    public VFXEntry luxShield;
    public VFXEntry luxFlashBang;
    public VFXEntry luxHealAura;

    [Header("Nox技能特效")]
    public VFXEntry noxShadowDash;
    public VFXEntry noxShadowClone;
    public VFXEntry noxDarkSlash;
    public VFXEntry noxStealth;

    [Header("合体技效")]
    public VFXEntry coopLightNova;
    public VFXEntry coopShadowMerge;
    public VFXEntry coopDualBarrier;
    public VFXEntry coopTimeFracture;
    public VFXEntry coopConvergence;

    [Header("战斗特效")]
    public VFXEntry hitSpark;
    public VFXEntry criticalHit;
    public VFXEntry explosion;
    public VFXEntry enemyDeath;
    public VFXEntry bossPhaseChange;

    [Header("环境特效")]
    public VFXEntry checkpointActivate;
    public VFXEntry collectiblePickup;
    public VFXEntry doorOpen;
    public VFXEntry portalActive;
    public VFXEntry breakableDestroy;
    public VFXEntry waterSplash;
    public VFXEntry dustCloud;
    public VFXEntry sparkle;

    [Header("UI特效")]
    public VFXEntry starEarn;
    public VFXEntry levelComplete;
    public VFXEntry comboBreak;

    [System.Serializable]
    public class VFXEntry
    {
        public string name;
        public GameObject prefab;
        public float duration = 2f;
        public float scale = 1f;
        public Color tint = Color.white;
        public bool useObjectPool = true;
        public int poolInitialSize = 3;
        public AudioClip sound;               // 伴随音效
        [Range(0f, 1f)]
        public float soundVolume = 1f;
        public ScreenShakeType screenShake = ScreenShakeType.None;
    }

    public enum ScreenShakeType
    {
        None,
        Light,
        Medium,
        Heavy
    }

    /// <summary>
    /// 根据名称查找VFX条目
    /// </summary>
    public VFXEntry FindByName(string effectName)
    {
        // 使用反射遍历所有字段
        var fields = GetType().GetFields(System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        foreach (var field in fields)
        {
            if (field.FieldType == typeof(VFXEntry))
            {
                var entry = field.GetValue(this) as VFXEntry;
                if (entry != null && entry.name == effectName)
                    return entry;
            }
        }
        return null;
    }

    /// <summary>
    /// 播放VFX条目（便捷方法）
    /// </summary>
    public void Play(VFXEntry entry, Vector3 position, Quaternion rotation = default)
    {
        if (entry == null || entry.prefab == null) return;

        if (rotation == default) rotation = Quaternion.identity;

        GameObject instance;

        if (entry.useObjectPool && ObjectPool.Instance != null)
        {
            instance = ObjectPool.Instance.Get(entry.prefab);
            if (instance != null)
            {
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                instance.transform.localScale = Vector3.one * entry.scale;

                // 自动回收
                ObjectPool.Instance.ReturnDelayed(instance, entry.duration);
            }
        }
        else
        {
            instance = Instantiate(entry.prefab, position, rotation);
            instance.transform.localScale = Vector3.one * entry.scale;
            Destroy(instance, entry.duration);
        }

        // 着色
        if (entry.tint != Color.white && instance != null)
        {
            var ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.startColor = entry.tint;
            }
        }

        // 伴随音效
        if (entry.sound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(entry.sound, entry.soundVolume);

        // 屏幕震动
        if (entry.screenShake != ScreenShakeType.None && VFXManager.Instance != null)
        {
            switch (entry.screenShake)
            {
                case ScreenShakeType.Light: VFXManager.Instance.ShakeLight(); break;
                case ScreenShakeType.Medium: VFXManager.Instance.ShakeMedium(); break;
                case ScreenShakeType.Heavy: VFXManager.Instance.ShakeHeavy(); break;
            }
        }
    }
}
