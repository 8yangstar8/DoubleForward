using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 关卡数值修复 - 把场景里烤死的敌人血量改回可玩范围。
///
/// 背景: EnemyBase.maxHealth 默认是 100,而玩家近战 2 点、远程 1 点伤害,
/// 打死一只小怪要 50~100 下。敌人又是实心碰撞体,于是路上每只小怪都是一堵
/// 打不烂的墙 —— 实测走通测试在第一只怪前就卡死,关卡根本过不去。
/// 改默认值不够,场景里已经序列化的 100 会覆盖它。
///
/// 用法: -executeMethod LevelBalanceFixer.FixEnemyHealth
/// </summary>
public static class LevelBalanceFixer
{
    private const float TutorialEnemyHealth = 4f;   // 近战2下 / 远程4下

    [MenuItem("DoubleForward/Fix Enemy Balance", false, 16)]
    public static void FixEnemyHealth()
    {
        int scenes = 0, fixedCount = 0;

        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            bool dirty = false;

            foreach (var enemy in Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                var so = new SerializedObject(enemy);
                var prop = so.FindProperty("maxHealth");
                if (prop == null || prop.floatValue <= TutorialEnemyHealth * 2f) continue;

                prop.floatValue = TutorialEnemyHealth;
                so.ApplyModifiedProperties();
                dirty = true;
                fixedCount++;
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                scenes++;
            }
        }

        Debug.Log($"[Balance] {fixedCount} enemies across {scenes} scenes set to {TutorialEnemyHealth} HP");
    }
}
