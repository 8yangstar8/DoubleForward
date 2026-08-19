using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 压力板 / 光感应器 / 检查点的美术。
///
/// 这三个都还是"一张1x1贴图被 localScale 撑成物件大小"的占位色块,而且
/// PressurePlate / LightSensor / Checkpoint 都会把 SpriteRenderer **整体染色**
/// 来表示状态(默认色分别是 Color.red / Color.gray / 灰),像素图直接被糊成一坨。
///
/// 做法: 给每个物件挂一个尺寸正确的子物件当贴图,再把组件里那个
/// [SerializeField] 的 renderer 引用指到子物件上 —— 状态染色照旧生效,
/// 但染的是一张真正的图,而且默认色改成白色,原始配色能显出来。
///
/// 子物件用反向缩放抵消父物件的 localScale(父物件的缩放就是它的碰撞体尺寸,
/// 不能动),这样贴图按原始像素比例显示,不会被压扁。
///
/// 注意: 那几个颜色也是 [SerializeField],改代码默认值对已存场景无效,
/// 必须用 SerializedObject 写进场景。
///
/// 用法: -executeMethod PuzzleVisualUpgrade.UpgradeAll
/// </summary>
public static class PuzzleVisualUpgrade
{
    private const string ArtDir = "Assets/Art/External/Kenney16x16/";
    private const string VisualChild = "Visual";
    private const int VisualOrder = 3;          // 地面(0)之上,玩家之下

    [MenuItem("DoubleForward/Upgrade Puzzle Visuals", false, 23)]
    public static void UpgradeAll()
    {
        Import("plate_button");
        Import("sensor_crystal");
        for (int i = 0; i < 3; i++) Import($"checkpoint_flag_{i}");

        int plates = 0, sensors = 0, checkpoints = 0, scenes = 0;
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);

            var ground = GameObject.Find("Ground");
            var groundCol = ground != null ? ground.GetComponent<Collider2D>() : null;
            float groundTop = groundCol != null ? groundCol.bounds.max.y : float.NaN;

            foreach (var plate in Object.FindObjectsByType<PressurePlate>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var sr = AttachVisual(plate.gameObject, Load("plate_button"), float.NaN);
                if (sr == null) continue;
                var so = new SerializedObject(plate);
                SetRef(so, "plateRenderer", sr);
                SetColor(so, "defaultColor", Color.white);
                SetColor(so, "pressedColor", new Color(0.55f, 1f, 0.65f));
                so.ApplyModifiedPropertiesWithoutUndo();
                plates++;
            }

            foreach (var sensor in Object.FindObjectsByType<LightSensor>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var sr = AttachVisual(sensor.gameObject, Load("sensor_crystal"), float.NaN);
                if (sr == null) continue;
                var so = new SerializedObject(sensor);
                SetRef(so, "sensorRenderer", sr);
                SetColor(so, "defaultColor", new Color(0.62f, 0.64f, 0.72f));  // 暗着 = 没通电
                SetColor(so, "activatedColor", Color.white);
                so.ApplyModifiedPropertiesWithoutUndo();
                sensors++;
            }

            foreach (var checkpoint in Object.FindObjectsByType<Checkpoint>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var frames = new Sprite[3];
                for (int i = 0; i < 3; i++) frames[i] = Load($"checkpoint_flag_{i}");
                if (frames[0] == null) continue;

                var sr = AttachVisual(checkpoint.gameObject, frames[0], groundTop);
                if (sr == null) continue;
                var animator = sr.GetComponent<SpriteFrameAnimator>();
                if (animator == null) animator = sr.gameObject.AddComponent<SpriteFrameAnimator>();
                animator.Configure(frames, 5f);

                var so = new SerializedObject(checkpoint);
                SetRef(so, "flagRenderer", sr);
                SetColor(so, "activeColor", Color.white);
                // 灰着 = 没激活。别压太暗: 旗子挂在远山前面,一暗就和背景糊在一起
                SetColor(so, "inactiveColor", new Color(0.72f, 0.73f, 0.78f));
                so.ApplyModifiedPropertiesWithoutUndo();
                checkpoints++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            scenes++;
        }

        Debug.Log($"[PuzzleArt] {plates} plates, {sensors} sensors, {checkpoints} checkpoints " +
                  $"across {scenes} scenes");
    }

    /// <summary>
    /// 挂一个按原始像素比例显示的贴图子物件,并关掉父物件那张被拉伸的占位图。
    /// baseY 传地面高度时贴图底边落在地面上(检查点旗杆用),传 NaN 时落在
    /// 父物件碰撞体的底边(压力板/感应器用)。
    /// </summary>
    private static SpriteRenderer AttachVisual(GameObject host, Sprite sprite, float baseY)
    {
        if (sprite == null) { Debug.LogWarning($"[PuzzleArt] sprite missing for {host.name}"); return null; }

        var hostSr = host.GetComponent<SpriteRenderer>();
        if (hostSr != null) hostSr.enabled = false;

        var child = host.transform.Find(VisualChild);
        if (child == null)
        {
            child = new GameObject(VisualChild).transform;
            child.SetParent(host.transform);
        }

        // 不能用 ?? : 它走 C# 引用判空,绕过 Unity 重载的 ==,
        // GetComponent 返回的"伪空"对象会被当成有效值,下一行就抛 MissingComponent
        var sr = child.GetComponent<SpriteRenderer>();
        if (sr == null) sr = child.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.white;
        sr.sortingOrder = VisualOrder;

        // 反向缩放抵消父物件的缩放 —— 父物件的 localScale 就是它的碰撞体尺寸,动不得
        var ls = host.transform.lossyScale;
        child.localScale = new Vector3(
            Mathf.Approximately(ls.x, 0f) ? 1f : 1f / ls.x,
            Mathf.Approximately(ls.y, 0f) ? 1f : 1f / ls.y,
            1f);

        float bottom = baseY;
        if (float.IsNaN(bottom))
        {
            var col = host.GetComponent<Collider2D>();
            bottom = col != null ? col.bounds.min.y : host.transform.position.y;
        }
        child.position = new Vector3(
            host.transform.position.x, bottom + sprite.bounds.extents.y, 0f);
        return sr;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
    }

    private static void SetColor(SerializedObject so, string field, Color value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.colorValue = value;
    }

    private static Sprite Load(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + name + ".png");

    private static void Import(string name)
    {
        string path = ArtDir + name + ".png";
        AssetDatabase.ImportAsset(path);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Debug.LogWarning($"[PuzzleArt] missing {path}"); return; }

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
