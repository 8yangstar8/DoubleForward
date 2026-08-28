using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 给远程敌人接上弹药和发射点。
///
/// 场景里的 ShadowArcher 的 projectilePrefab 和 firePoint 全是空的。
/// 它会侦测、会追击、会进入攻击状态、出手计数照加 —— 就是永远射不出东西,
/// 而且一条报错都没有: PerformAttack 第一行就是
///     if (currentTarget == null || projectilePrefab == null || firePoint == null) return;
///
/// 这就是集成测试里 "Enemy attacks player" 长期偶发失败的真正原因:
/// 测试每次抓到的敌人不固定,抓到近战史莱姆就过,抓到这只射手就挂 ——
/// 表现为"时过时不过",查了很久才落到这里。
///
/// 弹药预制体本来就有(Assets/Prefabs/Enemies/EnemyProjectile.prefab),
/// 又是典型的"资产齐了,没接线"。
///
/// 用法: -executeMethod EnemyWiringFixer.FixAll
/// </summary>
public static class EnemyWiringFixer
{
    private const string ProjectilePath = "Assets/Prefabs/Enemies/EnemyProjectile.prefab";
    private const string FirePointName = "FirePoint";

    [MenuItem("DoubleForward/Fix Enemy Wiring", false, 28)]
    public static void FixAll()
    {
        var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
        if (projectile == null)
        {
            Debug.LogError($"[EnemyWiring] missing {ProjectilePath}");
            return;
        }

        int wired = 0, scenes = 0;
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            string sceneName = Path.GetFileNameWithoutExtension(entry.path);
            bool dirty = false;

            foreach (var archer in Object.FindObjectsByType<ShadowArcher>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(archer);
                var projProp = so.FindProperty("projectilePrefab");
                var fireProp = so.FindProperty("firePoint");
                if (projProp == null || fireProp == null) continue;

                bool changed = false;
                if (projProp.objectReferenceValue == null)
                {
                    projProp.objectReferenceValue = projectile;
                    changed = true;
                }

                if (fireProp.objectReferenceValue == null)
                {
                    var t = archer.transform.Find(FirePointName);
                    if (t == null)
                    {
                        t = new GameObject(FirePointName).transform;
                        t.SetParent(archer.transform);
                        // 略微前上方出膛,免得弹丸一出生就卡在自己碰撞体里
                        t.localPosition = new Vector3(0.6f, 0.3f, 0f);
                    }
                    fireProp.objectReferenceValue = t;
                    changed = true;
                }

                if (!changed) continue;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(archer);
                wired++; dirty = true;
                Debug.Log($"[EnemyWiring] {sceneName}: wired '{archer.name}'");
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                scenes++;
            }
        }

        Debug.Log($"[EnemyWiring] wired {wired} ranged enemies across {scenes} scenes");
    }
}
