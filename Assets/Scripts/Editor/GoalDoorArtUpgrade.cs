using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 终点和门的美术 - 把两块最显眼的占位色块换掉。
///
/// 终点原本是一根半透明的黄色柱子,门是一块褐色长条。两个都是玩家必须一眼
/// 认出来的东西,却是全场最像"没做完"的部分。
///
/// 终点: 保留原来的光柱(它就是触发区,调低透明度当光晕),旁边立一面
/// Kenney 的三帧飘动旗。旗子是独立物件而不是光柱的子物件 —— 光柱靠
/// transform.localScale 当世界尺寸(约 1x3),挂子物件就得反向补偿缩放,
/// 徒增一层容易算错的换算。
///
/// 门: 从"一张1x1贴图被拉成整扇门"改成木板平铺。门只靠 PuzzleLink 位移开合,
/// 不读 localScale,所以把尺寸从 localScale 转到 sr.size + 碰撞体是安全的。
///
/// 用法: -executeMethod GoalDoorArtUpgrade.UpgradeAll
/// </summary>
public static class GoalDoorArtUpgrade
{
    private const string KenneyDir = "Assets/Art/External/Kenney16x16/";
    private const string FlagName = "GoalFlag";

    [MenuItem("DoubleForward/Upgrade Goal and Door Art", false, 21)]
    public static void UpgradeAll()
    {
        for (int i = 0; i < 3; i++) Import($"goal_flag_{i}");
        Import("door_planks");

        int flags = 0, doors = 0, scenes = 0;
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            flags += PlaceGoalFlag();
            doors += RetileDoors();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            scenes++;
        }
        Debug.Log($"[GoalArt] {flags} goal flags, {doors} doors retiled across {scenes} scenes");
    }

    private static int PlaceGoalFlag()
    {
        var goal = GameObject.Find("LevelGoal");
        if (goal == null) return 0;

        var frames = new Sprite[3];
        for (int i = 0; i < 3; i++)
        {
            frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{KenneyDir}goal_flag_{i}.png");
            if (frames[i] == null) { Debug.LogError("[GoalArt] flag frames missing"); return 0; }
        }

        // 原来那根柱子是 1x1 的 UnitGoal 贴图被 localScale 撑成 4x8,
        // 就是一大块糊在天上的半透明黄斑,调低透明度也只是变淡的黄斑。
        // 触发区(碰撞体)保留不动,视觉交给旗子。
        var goalSr = goal.GetComponent<SpriteRenderer>();
        if (goalSr != null) goalSr.enabled = false;

        var old = GameObject.Find(FlagName);
        if (old != null) Object.DestroyImmediate(old);

        var flag = new GameObject(FlagName);
        var sr = flag.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingOrder = 6;                 // 压在光柱(5)之上
        flag.AddComponent<SpriteFrameAnimator>().Configure(frames, 6f);

        // 旗杆底部落在地面上,而不是跟着光柱的中心走
        float baseY = goal.transform.position.y;
        var goalCol = goal.GetComponent<Collider2D>();
        if (goalCol != null) baseY = goalCol.bounds.min.y;
        var ground = GameObject.Find("Ground");
        var groundCol = ground != null ? ground.GetComponent<Collider2D>() : null;
        if (groundCol != null) baseY = groundCol.bounds.max.y;

        flag.transform.position = new Vector3(
            goal.transform.position.x, baseY + sr.sprite.bounds.extents.y, 0f);
        return 1;
    }

    private static int RetileDoors()
    {
        var planks = AssetDatabase.LoadAssetAtPath<Sprite>(KenneyDir + "door_planks.png");
        if (planks == null) { Debug.LogError("[GoalArt] door_planks missing"); return 0; }

        int n = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (go == null || !go.name.StartsWith("PuzzleDoor")) continue;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            var col = go.GetComponent<BoxCollider2D>();
            var scale = go.transform.localScale;
            Vector2 worldSize = col != null
                ? new Vector2(col.size.x * scale.x, col.size.y * scale.y)
                : new Vector2(scale.x, scale.y);

            go.transform.localScale = Vector3.one;
            sr.sprite = planks;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = worldSize;
            sr.color = Color.white;

            if (col != null) { col.size = worldSize; col.offset = Vector2.zero; }
            n++;
        }
        return n;
    }

    private static void Import(string name)
    {
        string path = KenneyDir + name + ".png";
        AssetDatabase.ImportAsset(path);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Debug.LogWarning($"[GoalArt] missing {path}"); return; }

        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 16;
        imp.filterMode = FilterMode.Point;
        imp.alphaIsTransparency = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;

        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }
}
