using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 清理脚本引用损坏的组件,并把因此变成"哑巴"的敌人救回来。
///
/// 背景: 几乎每一关都有 2-3 个 Enemy_* 挂着 m_Script 没有 guid 的 MonoBehaviour
/// —— 也就是 Unity 里显示 "Missing (Mono Script)" 的那种。后果是:
///   · 没有 AI: 不巡逻、不追击、不攻击
///   · 没有 IDamageable: 打上去没有任何反应
///   · 碰撞体和刚体还在: 于是它是一堵推不动也打不烂的墙
///   · 按组件类型查找的修复脚本(如 LevelBalanceFixer)统统漏掉它
/// 实测 Level_2_3 的 Lux 就被这样一只堵死在 x=25.6,55秒纹丝不动。
///
/// 处理: 先移除损坏组件,再给没有 EnemyBase 的 Enemy_* 补一只正常的近战敌人
/// (和同场景里健康的敌人同参数)。删掉它们更省事,但那样关卡会空掉,
/// 而这些位置本来就是设计好要放敌人的。
///
/// 用法: -executeMethod MissingScriptCleanup.CleanAll
/// </summary>
public static class MissingScriptCleanup
{
    private const int EnemyHealth = 4;      // 与 LevelBalanceFixer 一致: 玩家两三下能打死
    private const float EnemyDamage = 20f;  // = 玩家1滴血

    [MenuItem("DoubleForward/Clean Missing Scripts", false, 26)]
    public static void CleanAll()
    {
        int removed = 0, revived = 0, scenes = 0;

        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            string sceneName = Path.GetFileNameWithoutExtension(entry.path);
            bool dirty = false;

            foreach (var go in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go == null) continue;
                int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (n <= 0) continue;

                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                removed += n;
                dirty = true;
                Debug.Log($"[MissingScript] {sceneName}: removed {n} broken component(s) from '{go.name}'");
            }

            // 补回敌人。清理之后 Enemy_* 上可能一个 EnemyBase 都不剩了
            foreach (var go in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go == null || !go.name.StartsWith("Enemy_")) continue;
                if (go.GetComponent<EnemyBase>() != null) continue;

                var slime = go.AddComponent<ShadowSlime>();
                var so = new SerializedObject(slime);
                SetFloat(so, "maxHealth", EnemyHealth);
                SetFloat(so, "damage", EnemyDamage);
                so.ApplyModifiedPropertiesWithoutUndo();

                revived++;
                dirty = true;
                Debug.Log($"[MissingScript] {sceneName}: '{go.name}' had no EnemyBase left, gave it a ShadowSlime");
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                scenes++;
            }
        }

        Debug.Log($"[MissingScript] removed {removed} broken components, " +
                  $"revived {revived} enemies across {scenes} scenes");
    }

    private static void SetFloat(SerializedObject so, string field, float value)
    {
        var p = so.FindProperty(field);
        if (p == null) return;
        if (p.propertyType == SerializedPropertyType.Integer) p.intValue = Mathf.RoundToInt(value);
        else p.floatValue = value;
    }
}
