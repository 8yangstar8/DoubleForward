using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// 给各章 Boss 贴图。
///
/// 五章的 Boss 全都是 m_Sprite: 0 —— 有血量、会追击、能被打死,画面上什么都没有,
/// 也不报任何错。整套测试没有一条能发现它,因为大家查的都是"组件在不在""血量掉没掉",
/// 没人查"看不看得见"。补断言 TestKeyObjectsAreVisible 之后一次抓出四只。
///
/// 贴图是同一只愤怒史莱姆按章节改色 —— 它正是玩家一路打过来的杂兵的巨大化版本,
/// 一眼就读得懂"这是那玩意儿的头目"。
///
/// 贴图必须放在 Boss 自己的 SpriteRenderer 上,不能像压力板那样挪到子物件:
/// EnemyBase.HurtFlash 用的是 Awake 里 GetComponent 到的那个 renderer,
/// 挪走了受击闪烁就静默失效。
///
/// 用法: -executeMethod BossArtUpgrade.DressAll
/// </summary>
public static class BossArtUpgrade
{
    private const string ArtDir = "Assets/Art/External/Kenney16x16/";

    [MenuItem("DoubleForward/Dress Bosses", false, 25)]
    public static void DressAll()
    {
        int dressed = 0;
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            var bosses = Object.FindObjectsByType<BossBase>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bosses.Length == 0) continue;

            int chapter = ChapterOf(entry.path);
            foreach (var boss in bosses)
                if (Dress(boss.gameObject, chapter)) dressed++;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        Debug.Log($"[BossArt] dressed {dressed} bosses");
    }

    /// <summary>给单个 Boss 贴图。BuildLevel14 也调这里,免得两处各写一遍。</summary>
    public static bool Dress(GameObject boss, int chapter)
    {
        string path = $"{ArtDir}boss_ch{Mathf.Clamp(chapter, 1, 5)}.png";
        Import(path);

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) { Debug.LogError($"[BossArt] missing {path}"); return false; }

        var sr = boss.GetComponent<SpriteRenderer>();
        if (sr == null) sr = boss.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.white;
        sr.drawMode = SpriteDrawMode.Simple;
        sr.sortingOrder = 10;
        return true;
    }

    private static int ChapterOf(string scenePath)
    {
        var m = Regex.Match(scenePath, @"/Chapter(\d+)/");
        return m.Success ? int.Parse(m.Groups[1].Value) : 1;
    }

    private static void Import(string path)
    {
        AssetDatabase.ImportAsset(path);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;

        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 16;      // 48x64 像素 = 3x4 世界单位,正好等于碰撞体
        imp.filterMode = FilterMode.Point;
        imp.alphaIsTransparency = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
    }
}
