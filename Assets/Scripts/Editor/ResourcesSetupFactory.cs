using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Resources资源初始化工厂 - 创建代码中通过Resources.Load引用的必需资源
/// 包括：默认粒子材质、默认URP Sprite材质等
/// 菜单：DoubleForward / Setup Resources Assets
/// </summary>
public static class ResourcesSetupFactory
{
    [MenuItem("DoubleForward/Setup Resources Assets", false, 6)]
    public static void SetupAll()
    {
        int created = 0;

        EditorUtility.DisplayProgressBar("Setting up Resources...", "Creating materials...", 0.3f);
        created += CreateDefaultParticleMaterial();
        created += CreateDefaultSpriteMaterial();

        EditorUtility.DisplayProgressBar("Setting up Resources...", "Creating physics materials...", 0.6f);
        created += CreatePhysicsMaterials();

        EditorUtility.DisplayProgressBar("Setting up Resources...", "Verifying directories...", 0.9f);
        EnsureDirectoryStructure();

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();

        Debug.Log($"[ResourcesSetupFactory] Created {created} resource assets.");
        EditorUtility.DisplayDialog("Resources设置完成",
            $"已创建/验证 {created} 个资源文件\n\n" +
            "• DefaultParticle材质\n" +
            "• DefaultSprite材质\n" +
            "• Physics2D材质（冰面、弹跳等）\n" +
            "• 必需目录结构",
            "OK");
    }

    // ==================== 默认粒子材质 ====================

    private static int CreateDefaultParticleMaterial()
    {
        string dir = "Assets/Resources/Materials";
        string path = $"{dir}/DefaultParticle.mat";

        if (File.Exists(path)) return 0;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // 使用URP粒子着色器
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        var mat = new Material(shader);
        mat.name = "DefaultParticle";

        // 设置为Additive混合 - 大多数粒子特效使用此模式
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_Blend", 1);   // Additive
        mat.renderQueue = 3000;       // Transparent queue
        mat.SetColor("_BaseColor", Color.white);

        // 启用Alpha混合关键字
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");

        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"[Resources] Created DefaultParticle material: {path}");
        return 1;
    }

    private static int CreateDefaultSpriteMaterial()
    {
        string dir = "Assets/Resources/Materials";
        string path = $"{dir}/DefaultSprite.mat";

        if (File.Exists(path)) return 0;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        var mat = new Material(shader);
        mat.name = "DefaultSprite";

        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"[Resources] Created DefaultSprite material: {path}");
        return 1;
    }

    // ==================== Physics2D材质 ====================

    private static int CreatePhysicsMaterials()
    {
        string dir = "Assets/Resources/PhysicsMaterials";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        int created = 0;

        // 冰面 - 低摩擦
        created += CreatePhysMat2D(dir, "Ice", 0.02f, 0.0f);
        // 橡胶/弹跳 - 高弹性
        created += CreatePhysMat2D(dir, "Bouncy", 0.3f, 0.95f);
        // 普通地面
        created += CreatePhysMat2D(dir, "Default", 0.4f, 0.0f);
        // 沙地 - 高摩擦
        created += CreatePhysMat2D(dir, "Sand", 0.8f, 0.0f);
        // 金属 - 中等摩擦低弹性
        created += CreatePhysMat2D(dir, "Metal", 0.3f, 0.1f);
        // 单向平台 - 无摩擦
        created += CreatePhysMat2D(dir, "OneWay", 0.0f, 0.0f);
        // 墙面（用于墙跳检测）
        created += CreatePhysMat2D(dir, "WallSlide", 0.15f, 0.0f);

        return created;
    }

    private static int CreatePhysMat2D(string dir, string name, float friction, float bounciness)
    {
        string path = $"{dir}/{name}.physicsMaterial2D";
        if (File.Exists(path)) return 0;

        var mat = new PhysicsMaterial2D(name);
        mat.friction = friction;
        mat.bounciness = bounciness;

        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"[Resources] Created PhysicsMaterial2D: {name} (friction={friction}, bounce={bounciness})");
        return 1;
    }

    // ==================== 目录结构 ====================

    private static void EnsureDirectoryStructure()
    {
        string[] requiredDirs = {
            // Resources子目录
            "Assets/Resources/Audio/SFX",
            "Assets/Resources/Audio/BGM",
            "Assets/Resources/Audio/Ambient",
            "Assets/Resources/Materials",
            "Assets/Resources/PhysicsMaterials",
            "Assets/Resources/Localization",
            // 游戏资产目录
            "Assets/Art/Sprites/Characters",
            "Assets/Art/Sprites/Enemies",
            "Assets/Art/Sprites/Bosses",
            "Assets/Art/Sprites/Environment",
            "Assets/Art/Sprites/Puzzles",
            "Assets/Art/Sprites/UI",
            "Assets/Art/Sprites/VFX",
            "Assets/Art/Animations",
            "Assets/Art/Tilesets",
            // 预制体目录
            "Assets/Prefabs/Player",
            "Assets/Prefabs/Enemies",
            "Assets/Prefabs/Bosses",
            "Assets/Prefabs/Puzzles",
            "Assets/Prefabs/Environment",
            "Assets/Prefabs/VFX",
            "Assets/Prefabs/UI",
            "Assets/Prefabs/Managers",
            // 场景目录
            "Assets/Scenes/Chapter1",
            "Assets/Scenes/Chapter2",
            "Assets/Scenes/Chapter3",
            "Assets/Scenes/Chapter4",
            "Assets/Scenes/Chapter5",
            // ScriptableObject目录
            "Assets/ScriptableObjects/LevelData",
            "Assets/ScriptableObjects/LevelConfig",
            "Assets/ScriptableObjects/AbilityData",
            "Assets/ScriptableObjects/ChapterStory",
            // URP设置
            "Assets/Settings",
        };

        foreach (var dir in requiredDirs)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                Debug.Log($"[Resources] Created directory: {dir}");
            }
        }
    }

    /// <summary>
    /// 检查Resources是否已配置
    /// </summary>
    public static bool HasResourcesSetup()
    {
        return File.Exists("Assets/Resources/Materials/DefaultParticle.mat");
    }
}
