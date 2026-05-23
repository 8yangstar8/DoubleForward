using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MovingPlatform))]
public class MovingPlatformEditor : Editor
{
    void OnSceneGUI()
    {
        var platform = (MovingPlatform)target;
        var waypointsField = serializedObject.FindProperty("waypoints");
        var useLocal = serializedObject.FindProperty("useLocalPositions");

        if (waypointsField == null) return;

        Handles.color = Color.cyan;
        for (int i = 0; i < waypointsField.arraySize; i++)
        {
            var wp = waypointsField.GetArrayElementAtIndex(i);
            Vector3 worldPos = useLocal.boolValue
                ? platform.transform.position + wp.vector3Value
                : wp.vector3Value;

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(worldPos, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(platform, "Move Waypoint");
                wp.vector3Value = useLocal.boolValue
                    ? newPos - platform.transform.position
                    : newPos;
                serializedObject.ApplyModifiedProperties();
            }

            Handles.Label(worldPos + Vector3.up * 0.5f, $"WP {i}");

            if (i < waypointsField.arraySize - 1)
            {
                var nextWp = waypointsField.GetArrayElementAtIndex(i + 1);
                Vector3 nextPos = useLocal.boolValue
                    ? platform.transform.position + nextWp.vector3Value
                    : nextWp.vector3Value;
                Handles.DrawDottedLine(worldPos, nextPos, 5f);
            }
        }
    }
}

[CustomEditor(typeof(Portal))]
public class PortalEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var portal = (Portal)target;
        var linked = serializedObject.FindProperty("linkedPortal");

        if (linked.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("No linked portal! Drag another Portal here to create a pair.", MessageType.Warning);

            if (GUILayout.Button("Create Linked Portal"))
            {
                var newPortal = new GameObject("Portal_Linked");
                newPortal.transform.position = portal.transform.position + Vector3.right * 5;
                var comp = newPortal.AddComponent<Portal>();
                newPortal.AddComponent<BoxCollider2D>().isTrigger = true;
                var sr = newPortal.AddComponent<SpriteRenderer>();
                sr.color = new Color(0.3f, 0.6f, 1f);

                linked.objectReferenceValue = comp;
                var linkedSerial = new SerializedObject(comp);
                linkedSerial.FindProperty("linkedPortal").objectReferenceValue = portal;
                linkedSerial.ApplyModifiedProperties();
                serializedObject.ApplyModifiedProperties();

                Undo.RegisterCreatedObjectUndo(newPortal, "Create Linked Portal");
            }
        }
    }
}
