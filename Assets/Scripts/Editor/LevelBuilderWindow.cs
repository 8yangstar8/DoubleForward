using UnityEngine;
using UnityEditor;

public class LevelBuilderWindow : EditorWindow
{
    private enum PuzzleTool
    {
        None,
        PressurePlate,
        LightSensor,
        ShadowWall,
        MovingPlatform,
        Portal,
        Checkpoint,
        Hazard
    }

    private PuzzleTool currentTool = PuzzleTool.None;
    private Vector2 scrollPos;
    private int selectedChapter = 1;
    private int selectedLevel = 1;
    private string levelName = "New Level";

    [MenuItem("DoubleForward/Level Builder")]
    public static void ShowWindow()
    {
        GetWindow<LevelBuilderWindow>("Level Builder");
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Double Forward - Level Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        DrawLevelInfoSection();
        EditorGUILayout.Space();
        DrawPuzzleToolsSection();
        EditorGUILayout.Space();
        DrawQuickActionsSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawLevelInfoSection()
    {
        GUILayout.Label("Level Info", EditorStyles.boldLabel);
        selectedChapter = EditorGUILayout.IntSlider("Chapter", selectedChapter, 1, 5);
        selectedLevel = EditorGUILayout.IntSlider("Level", selectedLevel, 1, 5);
        levelName = EditorGUILayout.TextField("Level Name", levelName);

        if (GUILayout.Button("Create Level Data Asset"))
        {
            CreateLevelDataAsset();
        }
    }

    private void DrawPuzzleToolsSection()
    {
        GUILayout.Label("Puzzle Components", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        DrawToolButton("Pressure Plate", PuzzleTool.PressurePlate);
        DrawToolButton("Light Sensor", PuzzleTool.LightSensor);
        DrawToolButton("Shadow Wall", PuzzleTool.ShadowWall);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawToolButton("Moving Platform", PuzzleTool.MovingPlatform);
        DrawToolButton("Portal", PuzzleTool.Portal);
        DrawToolButton("Checkpoint", PuzzleTool.Checkpoint);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (currentTool != PuzzleTool.None)
        {
            EditorGUILayout.HelpBox($"Selected: {currentTool}\nClick in Scene view to place.", MessageType.Info);

            if (GUILayout.Button("Place at Scene Center"))
            {
                PlacePuzzleComponent(currentTool, Vector3.zero);
            }
        }
    }

    private void DrawQuickActionsSection()
    {
        GUILayout.Label("Quick Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Spawn Player Start Points"))
        {
            SpawnStartPoints();
        }

        if (GUILayout.Button("Add Level Goal Trigger"))
        {
            CreateLevelGoal();
        }

        if (GUILayout.Button("Setup Camera Bounds"))
        {
            SetupCameraBounds();
        }
    }

    private void DrawToolButton(string label, PuzzleTool tool)
    {
        GUI.backgroundColor = currentTool == tool ? Color.cyan : Color.white;
        if (GUILayout.Button(label))
        {
            currentTool = currentTool == tool ? PuzzleTool.None : tool;
        }
        GUI.backgroundColor = Color.white;
    }

    private void PlacePuzzleComponent(PuzzleTool tool, Vector3 position)
    {
        GameObject obj = null;

        switch (tool)
        {
            case PuzzleTool.PressurePlate:
                obj = CreatePuzzleObject("PressurePlate", position);
                obj.AddComponent<BoxCollider2D>().isTrigger = true;
                obj.AddComponent<PressurePlate>();
                obj.transform.localScale = new Vector3(1.5f, 0.2f, 1);
                break;

            case PuzzleTool.LightSensor:
                obj = CreatePuzzleObject("LightSensor", position);
                obj.AddComponent<CircleCollider2D>().isTrigger = true;
                obj.AddComponent<LightSensor>();
                break;

            case PuzzleTool.ShadowWall:
                obj = CreatePuzzleObject("ShadowWall", position);
                obj.AddComponent<BoxCollider2D>();
                obj.AddComponent<ShadowWall>();
                obj.transform.localScale = new Vector3(0.5f, 3f, 1);
                obj.layer = LayerMask.NameToLayer("ShadowWall");
                break;

            case PuzzleTool.MovingPlatform:
                obj = CreatePuzzleObject("MovingPlatform", position);
                obj.AddComponent<BoxCollider2D>();
                obj.AddComponent<MovingPlatform>();
                obj.transform.localScale = new Vector3(3f, 0.5f, 1);
                break;

            case PuzzleTool.Portal:
                obj = CreatePuzzleObject("Portal", position);
                obj.AddComponent<BoxCollider2D>().isTrigger = true;
                obj.AddComponent<Portal>();
                break;

            case PuzzleTool.Checkpoint:
                obj = CreatePuzzleObject("Checkpoint", position);
                obj.AddComponent<BoxCollider2D>().isTrigger = true;
                obj.AddComponent<Checkpoint>();
                obj.tag = "Checkpoint";
                break;
        }

        if (obj != null)
        {
            Undo.RegisterCreatedObjectUndo(obj, $"Place {tool}");
            Selection.activeGameObject = obj;
        }
    }

    private GameObject CreatePuzzleObject(string name, Vector3 position)
    {
        var obj = new GameObject(name);
        obj.transform.position = position;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.color = GetPuzzleColor(name);
        sr.sortingLayerName = "PuzzleObjects";
        return obj;
    }

    private Color GetPuzzleColor(string name)
    {
        return name switch
        {
            "PressurePlate" => new Color(0.8f, 0.2f, 0.2f),
            "LightSensor" => new Color(1f, 0.9f, 0.3f),
            "ShadowWall" => new Color(0.2f, 0.1f, 0.3f, 0.8f),
            "MovingPlatform" => new Color(0.5f, 0.5f, 0.5f),
            "Portal" => new Color(0.3f, 0.6f, 1f),
            "Checkpoint" => new Color(0.3f, 1f, 0.3f),
            _ => Color.white
        };
    }

    private void CreateLevelDataAsset()
    {
        var data = ScriptableObject.CreateInstance<LevelData>();
        data.chapter = selectedChapter;
        data.levelIndex = selectedLevel;
        data.levelName = levelName;
        data.sceneName = $"Level_{selectedChapter}_{selectedLevel}";
        data.luxSpawnPoint = new Vector2(-3, 0);
        data.noxSpawnPoint = new Vector2(3, 0);

        string path = $"Assets/ScriptableObjects/LevelData/Chapter{selectedChapter}_Level{selectedLevel}.asset";
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = data;
    }

    private void SpawnStartPoints()
    {
        var luxSpawn = new GameObject("LuxSpawnPoint");
        luxSpawn.transform.position = new Vector3(-3, 0, 0);
        var luxIcon = luxSpawn.AddComponent<SpriteRenderer>();
        luxIcon.color = new Color(1, 0.9f, 0.5f, 0.5f);

        var noxSpawn = new GameObject("NoxSpawnPoint");
        noxSpawn.transform.position = new Vector3(3, 0, 0);
        var noxIcon = noxSpawn.AddComponent<SpriteRenderer>();
        noxIcon.color = new Color(0.3f, 0.1f, 0.5f, 0.5f);

        Undo.RegisterCreatedObjectUndo(luxSpawn, "Create Spawn Points");
        Undo.RegisterCreatedObjectUndo(noxSpawn, "Create Spawn Points");
    }

    private void CreateLevelGoal()
    {
        var goal = new GameObject("LevelGoal");
        goal.transform.position = new Vector3(20, 0, 0);
        var collider = goal.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(2, 4);
        var sr = goal.AddComponent<SpriteRenderer>();
        sr.color = new Color(1, 0.84f, 0, 0.5f);

        Undo.RegisterCreatedObjectUndo(goal, "Create Level Goal");
        Selection.activeGameObject = goal;
    }

    private void SetupCameraBounds()
    {
        var bounds = new GameObject("CameraBounds");
        var collider = bounds.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(40, 20);
        bounds.transform.position = new Vector3(15, 5, 0);

        Undo.RegisterCreatedObjectUndo(bounds, "Create Camera Bounds");
        Selection.activeGameObject = bounds;
    }
}
