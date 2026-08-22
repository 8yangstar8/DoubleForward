using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 把地面延伸到能覆盖终点。
///
/// 模板把终点放在 x=45/50/55/60,地面却一律是"60宽、中心x=20",也就是只到 x=50。
/// 20关里有12关的终点落在地面之外 —— 而 LevelBootstrap 还会在地面边缘立边界墙,
/// 于是玩家连走都走不过去。**全部四个 Boss 关都在其中**。
///
/// 这类问题一直没人发现,是因为过去的通关测试都是把角色瞬移到终点再判定,
/// 瞬移当然不会被边界墙挡住。加了"真的从出生点走过去"的走通测试才暴露出来。
///
/// 只改地面宽度和中心,上表面高度不动 —— 所有机关和平台的落点都按上表面算,
/// 动了高度会让全场悬空。
///
/// 用法: -executeMethod LevelSpanFixer.FixAll
/// </summary>
public static class LevelSpanFixer
{
    private const float RightMargin = 6f;    // 终点之后还要留出落脚地
    private const float LeftMargin = 5f;

    [MenuItem("DoubleForward/Fix Level Span", false, 27)]
    public static void FixAll()
    {
        int fixedCount = 0;

        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            string sceneName = Path.GetFileNameWithoutExtension(entry.path);

            var ground = GameObject.Find("Ground");
            var goal = Object.FindAnyObjectByType<LevelGoalTrigger>();
            if (ground == null || goal == null) continue;

            var col = ground.GetComponent<BoxCollider2D>();
            var sr = ground.GetComponent<SpriteRenderer>();
            if (col == null) continue;

            var b = col.bounds;
            float goalX = goal.transform.position.x;

            float spawnX = b.min.x;
            var luxSpawn = GameObject.Find("LuxSpawnPoint");
            if (luxSpawn != null) spawnX = Mathf.Min(spawnX, luxSpawn.transform.position.x);

            float left = Mathf.Min(b.min.x, spawnX - LeftMargin);
            float right = Mathf.Max(b.max.x, goalX + RightMargin);
            float newWidth = right - left;
            if (newWidth <= b.size.x + 0.01f) continue;   // 已经够宽

            // 上表面高度必须保持不变: 平台净空、机关落点、植被全按它算
            float top = b.max.y;
            float height = col.size.y;
            float centerX = (left + right) * 0.5f;

            col.size = new Vector2(newWidth, height);
            col.offset = Vector2.zero;
            if (sr != null) sr.size = new Vector2(newWidth, sr.size.y);
            ground.transform.localScale = Vector3.one;
            ground.transform.position = new Vector3(centerX, top - height * 0.5f, ground.transform.position.z);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            fixedCount++;
            Debug.Log($"[Span] {sceneName}: ground {b.size.x:F0} -> {newWidth:F0} wide " +
                      $"([{left:F0},{right:F0}]), goal at {goalX:F0}");
        }

        Debug.Log($"[Span] widened ground in {fixedCount} scenes");
    }
}
