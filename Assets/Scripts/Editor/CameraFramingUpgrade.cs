using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 镜头取景 - 把 DualPlayerCamera 的跟随偏移写进各关卡场景。
///
/// 原来偏移是 (0,1,-10),玩家基本在画面正中,于是地平线落在中间偏下,
/// 底下小半个屏幕全是地下泥土 —— 屏幕占比最大的区域没有任何信息。
/// 抬到 (0,2,-10) 后地面线压到约七成高度,空出来的是玩家真正要看的
/// 上方平台和跳跃落点。
///
/// 为什么必须写场景: offset 是 [SerializeField],改代码里的默认值只影响
/// 新建的组件,场景里已经序列化过的实例仍然读旧值 —— 改完代码跑截图
/// 画面纹丝不动,就是这个原因。
///
/// 用法: -executeMethod CameraFramingUpgrade.ApplyAll
/// </summary>
public static class CameraFramingUpgrade
{
    private static readonly Vector3 FollowOffset = new Vector3(0f, 2.5f, -10f);

    [MenuItem("DoubleForward/Apply Camera Framing", false, 22)]
    public static void ApplyAll()
    {
        int updated = 0, scenes = 0;
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || !entry.path.Contains("/Chapter")) continue;
            if (!File.Exists(entry.path)) continue;

            EditorSceneManager.OpenScene(entry.path);
            bool dirty = false;

            foreach (var camera in Object.FindObjectsByType<DualPlayerCamera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(camera);
                var prop = so.FindProperty("offset");
                if (prop == null || prop.vector3Value == FollowOffset) continue;
                prop.vector3Value = FollowOffset;
                so.ApplyModifiedPropertiesWithoutUndo();
                updated++; dirty = true;
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                scenes++;
            }
        }
        Debug.Log($"[CameraFraming] offset -> {FollowOffset} on {updated} cameras across {scenes} scenes");
    }
}
