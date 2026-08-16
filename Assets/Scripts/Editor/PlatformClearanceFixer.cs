using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 平台净空修正 - 把横在行走路线上的悬空平台抬高到玩家能从下面走过。
///
/// 背景: 模板场景把平台撒在 y=-0.5,而地面上表面在 -1.5,于是它们变成一块块
/// 离地0.75、正好卡在玩家腰部高度的板。玩家往前走就撞在它的左侧面上
/// (实测 Lux 反复卡在 x=2.6,正是 Platform_0 左边缘减去半身宽的位置)。
/// 自动走通测试因为有"卡住就跳"的兜底所以能过,但真人玩会明显觉得卡。
///
/// 平台游戏里的平台应该是"跳得上去的台"或"走得过去的门洞",
/// 不该是齐腰高的路障。这里把与行走通道相交的平台抬到头顶以上。
///
/// 用法: -executeMethod PlatformClearanceFixer.FixAll
/// </summary>
public static class PlatformClearanceFixer
{
    private const float BodyHeight = 2.2f;      // 玩家通行需要的净空
    private const float LiftClearance = 2.6f;   // 抬高后平台底面距地面的高度

    [MenuItem("DoubleForward/Fix Platform Clearance", false, 19)]
    public static void FixAll()
    {
        int scenes = 0, lifted = 0;

        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);

            var ground = GameObject.Find("Ground");
            var groundCol = ground != null ? ground.GetComponent<Collider2D>() : null;
            if (groundCol == null) continue;
            float groundTop = groundCol.bounds.max.y;

            bool dirty = false;
            foreach (var col in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            {
                if (col == null || !col.gameObject.name.StartsWith("Platform_")) continue;

                var b = col.bounds;
                bool blocksWalking = b.min.y < groundTop + BodyHeight && b.max.y > groundTop;
                if (!blocksWalking) continue;

                float halfH = b.extents.y;
                var tf = col.transform;
                tf.position = new Vector3(tf.position.x, groundTop + LiftClearance + halfH, tf.position.z);
                lifted++;
                dirty = true;
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                scenes++;
            }
        }

        Debug.Log($"[Clearance] lifted {lifted} path-blocking platforms across {scenes} scenes");
    }
}
