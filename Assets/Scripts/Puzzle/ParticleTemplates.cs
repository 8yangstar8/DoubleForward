using UnityEngine;

/// <summary>
/// 粒子系统模板 - 通过代码创建常用粒子效果
/// 用于没有美术资源时的占位特效，或运行时动态生成
/// 所有模板生成后可通过ObjectPool回收
/// </summary>
public static class ParticleTemplates
{
    // ==================== 玩家相关 ====================

    /// <summary>
    /// 跳跃烟尘（着地时脚下的灰尘）
    /// </summary>
    public static ParticleSystem CreateJumpDust(Transform parent = null)
    {
        var ps = CreateBase("JumpDust", parent);
        var main = ps.main;
        main.duration = 0.3f;
        main.startLifetime = 0.4f;
        main.startSpeed = 2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startColor = new Color(0.8f, 0.75f, 0.65f, 0.6f);
        main.maxParticles = 10;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.3f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;

        return ps;
    }

    /// <summary>
    /// 冲刺拖尾（Nox冲刺时的暗影残影）
    /// </summary>
    public static ParticleSystem CreateDashTrail(Transform parent = null)
    {
        var ps = CreateBase("DashTrail", parent);
        var main = ps.main;
        main.duration = 0.5f;
        main.startLifetime = 0.3f;
        main.startSpeed = 0.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startColor = new Color(0.3f, 0.1f, 0.5f, 0.7f);
        main.maxParticles = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 30;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

        return ps;
    }

    /// <summary>
    /// 治疗粒子（绿色上升光点）
    /// </summary>
    public static ParticleSystem CreateHealEffect(Transform parent = null)
    {
        var ps = CreateBase("HealEffect", parent);
        var main = ps.main;
        main.duration = 1f;
        main.startLifetime = 0.8f;
        main.startSpeed = 1.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = new Color(0.3f, 1f, 0.4f, 0.8f);
        main.maxParticles = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.5f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.5f;

        return ps;
    }

    // ==================== 战斗相关 ====================

    /// <summary>
    /// 命中火花（攻击命中时的粒子）
    /// </summary>
    public static ParticleSystem CreateHitSpark(Transform parent = null)
    {
        var ps = CreateBase("HitSpark", parent);
        var main = ps.main;
        main.duration = 0.2f;
        main.startLifetime = 0.2f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
        main.startColor = new Color(1f, 0.9f, 0.3f, 1f);
        main.maxParticles = 15;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 2f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        return ps;
    }

    /// <summary>
    /// 爆炸效果（敌人死亡、可破坏物碎裂）
    /// </summary>
    public static ParticleSystem CreateExplosion(Transform parent = null, Color? color = null)
    {
        Color c = color ?? new Color(1f, 0.5f, 0.1f, 1f);

        var ps = CreateBase("Explosion", parent);
        var main = ps.main;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startColor = c;
        main.maxParticles = 30;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.5f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

        return ps;
    }

    /// <summary>
    /// 光弹拖尾（Lux远程攻击的尾迹）
    /// </summary>
    public static ParticleSystem CreateProjectileTrail(Transform parent = null)
    {
        var ps = CreateBase("ProjectileTrail", parent);
        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = 0.3f;
        main.startSpeed = 0.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = new Color(1f, 0.95f, 0.7f, 0.8f);
        main.maxParticles = 30;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 30;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

        return ps;
    }

    // ==================== 环境相关 ====================

    /// <summary>
    /// 火焰粒子（火把、岩浆）
    /// </summary>
    public static ParticleSystem CreateFire(Transform parent = null)
    {
        var ps = CreateBase("Fire", parent);
        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startColor = new Color(1f, 0.6f, 0.1f, 0.8f);
        main.maxParticles = 30;
        main.gravityModifier = -0.5f;

        var emission = ps.emission;
        emission.rateOverTime = 20;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.1f;

        return ps;
    }

    /// <summary>
    /// 水滴/水花（瀑布、水面）
    /// </summary>
    public static ParticleSystem CreateWaterSplash(Transform parent = null)
    {
        var ps = CreateBase("WaterSplash", parent);
        var main = ps.main;
        main.duration = 0.5f;
        main.startLifetime = 0.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
        main.startColor = new Color(0.5f, 0.8f, 1f, 0.7f);
        main.maxParticles = 20;
        main.gravityModifier = 2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.2f;

        return ps;
    }

    /// <summary>
    /// 环境光点（森林萤火虫、虚空星尘）
    /// </summary>
    public static ParticleSystem CreateAmbientParticles(Transform parent = null,
        Color? color = null, float area = 5f)
    {
        Color c = color ?? new Color(1f, 1f, 0.7f, 0.5f);

        var ps = CreateBase("AmbientParticles", parent);
        var main = ps.main;
        main.duration = 10f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startColor = c;
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 5;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(area, area, 0);

        return ps;
    }

    // ==================== UI相关 ====================

    /// <summary>
    /// 星星爆发（评分界面星星出现时）
    /// </summary>
    public static ParticleSystem CreateStarBurst(Transform parent = null)
    {
        var ps = CreateBase("StarBurst", parent);
        var main = ps.main;
        main.duration = 0.5f;
        main.startLifetime = 0.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = new Color(1f, 0.9f, 0.2f, 1f);
        main.maxParticles = 15;
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        return ps;
    }

    /// <summary>
    /// 收集品拾取效果
    /// </summary>
    public static ParticleSystem CreateCollectEffect(Transform parent = null, Color? color = null)
    {
        Color c = color ?? new Color(1f, 0.85f, 0f, 1f);

        var ps = CreateBase("CollectEffect", parent);
        var main = ps.main;
        main.duration = 0.4f;
        main.startLifetime = 0.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = c;
        main.maxParticles = 10;
        main.gravityModifier = -0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

        return ps;
    }

    // ==================== 工具方法 ====================

    private static ParticleSystem CreateBase(string name, Transform parent)
    {
        var go = new GameObject($"PS_{name}");
        if (parent != null)
            go.transform.SetParent(parent, false);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;

        // 默认Renderer设置
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        // 尝试使用默认粒子材质
        renderer.material = GetDefaultParticleMaterial();

        return ps;
    }

    private static Material GetDefaultParticleMaterial()
    {
        // 尝试加载URP默认粒子材质
        var mat = Resources.Load<Material>("Materials/DefaultParticle");
        if (mat != null) return mat;

        // 回退到内置Shader
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            mat = new Material(shader);
            return mat;
        }

        return null;
    }
}
