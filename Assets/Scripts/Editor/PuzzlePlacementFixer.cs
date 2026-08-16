using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 机关归位 - 把被 Unity 魔法回调 Reset() 拽到世界原点的压力板放回关卡路径上,
/// 并把它联动的门一起挪过去。
///
/// 背景: PressurePlate 曾定义 public void Reset(),编辑器 AddComponent 时会自动
/// 调用它,而那时 originalPosition 还是 Vector3.zero,于是每个板都被瞬移到 (0,0)。
/// 那里正好在玩家出生点附近,开局就一直处于踩下状态,联动的门永远敞开。
/// 组件已改名为 ResetPlate(),这里修的是已经跑偏的存量场景数据。
///
/// 用法: -executeMethod PuzzlePlacementFixer.FixAllScenes
/// </summary>
public static class PuzzlePlacementFixer
{
    [MenuItem("DoubleForward/Fix Stranded Puzzle Placement", false, 11)]
    public static void FixAllScenes()
    {
        int scenesFixed = 0, platesMoved = 0, doorsMoved = 0;

        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);

            var ground = GameObject.Find("Ground");
            var luxSpawn = GameObject.Find("LuxSpawnPoint");
            if (ground == null || luxSpawn == null) continue;

            float groundTopY = GroundTop(ground);
            float plateX = float.NaN; // 确实有板跑偏时才去找空位

            bool dirty = false;
            foreach (var plate in Object.FindObjectsByType<PressurePlate>(FindObjectsSortMode.None))
            {
                var pos = plate.transform.position;
                if (new Vector2(pos.x, pos.y).magnitude > 1f) continue; // 没跑偏就不动

                if (float.IsNaN(plateX))
                {
                    var goal = Object.FindAnyObjectByType<LevelGoalTrigger>();
                    float searchFrom = goal != null
                        ? goal.transform.position.x - 8f          // 门守在通往终点的路上
                        : luxSpawn.transform.position.x + 12f;
                    plateX = FindClearSpot(searchFrom, luxSpawn.transform.position.x, groundTopY);
                }

                float plateHalfH = HalfHeight(plate.gameObject, 0.15f);
                plate.transform.position = new Vector3(plateX, groundTopY + plateHalfH, 0f);
                platesMoved++;
                dirty = true;

                // 联动的门跟着挪到板右侧,底边落在地面上
                var door = GameObject.Find("PuzzleDoor");
                if (door != null)
                {
                    float doorHalfH = HalfHeight(door, 1.5f);
                    door.transform.position = new Vector3(plateX + 3f, groundTopY + doorHalfH, 0f);
                    doorsMoved++;
                }
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                scenesFixed++;
                Debug.Log($"[PuzzleFix] {Path.GetFileNameWithoutExtension(entry.path)}: plate -> x={plateX:F1}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PuzzleFix] Done: {scenesFixed} scenes, {platesMoved} plates, {doorsMoved} doors repositioned");
    }

    /// <summary>
    /// 把各关自带的 PressurePlate_1 设为锁存(isToggle)。
    ///
    /// 这些板是瞬时的:踩住门才开,一离开门就落下。板在x=10、门在x=13,
    /// 单人玩家不可能同时站在板上又穿过门 —— 关卡实际过不去。
    /// (板被 Reset() 拽到原点的年代,它一直被出生点的玩家压着,门"恰好"常开,
    ///  所以这个问题被掩盖了;把板归位反而暴露出来。)
    /// 合作关卡里 Coop_Plate / Coop3_Plate 保持瞬时,那是设计的一部分。
    /// </summary>
    [MenuItem("DoubleForward/Make Legacy Plates Latching", false, 14)]
    public static void MakeLegacyPlatesLatching()
    {
        int changed = 0;
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            bool dirty = false;

            foreach (var plate in Object.FindObjectsByType<PressurePlate>(FindObjectsSortMode.None))
            {
                if (plate.name != "PressurePlate_1") continue;
                var so = new SerializedObject(plate);
                var prop = so.FindProperty("isToggle");
                if (prop == null || prop.boolValue) continue;
                prop.boolValue = true;
                so.ApplyModifiedProperties();
                dirty = true;
                changed++;
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Debug.Log($"[PuzzleFix] {Path.GetFileNameWithoutExtension(entry.path)}: plate latching");
            }
        }
        Debug.Log($"[PuzzleFix] {changed} legacy plates set to latching");
    }


    /// <summary>
    /// 地面上表面高度。必须读碰撞体范围而不是 localScale —— 地面改用平铺渲染后
    /// 尺寸记在碰撞体和 SpriteRenderer.size 上,localScale 恒为1,
    /// 旧的 position.y + localScale.y*0.5 会算出错误的高度,机关就会悬空。
    /// </summary>
    private static float GroundTop(GameObject ground)
    {
        var col = ground.GetComponent<Collider2D>();
        return col != null
            ? col.bounds.max.y
            : ground.transform.position.y + ground.transform.localScale.y * 0.5f;
    }

    /// <summary>
    /// 找一处板和门都放得下的空位。模板场景在地面上随机撒了平台和敌人,
    /// 固定偏移会把板埋进平台里(踩不到)、把门叠在敌人身上(挡住弹道)。
    /// </summary>
    private static float FindClearSpot(float preferredX, float spawnX, float groundTopY)
    {
        Physics2D.SyncTransforms();
        // 从关卡后段往前找,尽量靠近终点;不要退回到出生点附近的前段走廊
        float floorX = spawnX + 6f;
        for (float x = preferredX; x >= floorX; x -= 1f)
        {
            if (IsClear(x, groundTopY) && IsClear(x + 3f, groundTopY))
                return x;
        }
        Debug.LogWarning($"[PuzzleFix] No clear spot between x={floorX:F0} and x={preferredX:F0}, falling back");
        return preferredX;
    }

    /// <summary>该处玩家身体大小的范围内除了地面没有别的碰撞体</summary>
    private static bool IsClear(float x, float groundTopY)
    {
        // 只看贴地的通行高度: 头顶上方的平台可以从下面走过,不算障碍
        var hits = Physics2D.OverlapBoxAll(new Vector2(x, groundTopY + 0.75f), new Vector2(2.2f, 1.5f), 0f);
        foreach (var h in hits)
        {
            if (h == null || h.isTrigger) continue;   // 触发器(死亡区/收集品/检查点)不算障碍
            string n = h.gameObject.name;
            if (n == "Ground" || n == "PressurePlate_1" || n == "PuzzleDoor") continue;
            return false;
        }
        return true;
    }

    /// <summary>取对象碰撞体的半高,用来把它的底边对齐到地面</summary>
    private static float HalfHeight(GameObject go, float fallback)
    {
        var col = go.GetComponent<Collider2D>();
        return col != null ? col.bounds.extents.y : fallback;
    }
}
