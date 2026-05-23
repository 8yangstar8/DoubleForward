using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

public class ProjectBootstrapper : EditorWindow
{
    [MenuItem("DoubleForward/Project Setup Wizard")]
    public static void ShowWindow()
    {
        GetWindow<ProjectBootstrapper>("Project Setup");
    }

    private bool scenesCreated;
    private bool prefabsCreated;
    private bool levelDataCreated;
    private Vector2 scroll;

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("Double Forward - Project Setup Wizard", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This wizard creates all necessary scenes, prefabs, and data assets.\n" +
            "Run each step in order. Green = completed.", MessageType.Info);

        EditorGUILayout.Space(10);

        // Step 1: Scenes
        GUI.backgroundColor = scenesCreated ? Color.green : Color.white;
        GUILayout.Label("Step 1: Create Scenes", EditorStyles.boldLabel);
        if (GUILayout.Button(scenesCreated ? "Scenes Created ✓" : "Create All Scenes", GUILayout.Height(35)))
        {
            CreateAllScenes();
            scenesCreated = true;
        }

        EditorGUILayout.Space(5);

        // Step 2: Prefabs
        GUI.backgroundColor = prefabsCreated ? Color.green : Color.white;
        GUILayout.Label("Step 2: Create Player Prefabs", EditorStyles.boldLabel);
        if (GUILayout.Button(prefabsCreated ? "Prefabs Created ✓" : "Create Player Prefabs", GUILayout.Height(35)))
        {
            CreatePlayerPrefabs();
            prefabsCreated = true;
        }

        EditorGUILayout.Space(5);

        // Step 3: Level Data
        GUI.backgroundColor = levelDataCreated ? Color.green : Color.white;
        GUILayout.Label("Step 3: Create Level Data Assets", EditorStyles.boldLabel);
        if (GUILayout.Button(levelDataCreated ? "Level Data Created ✓" : "Create All Level Data", GUILayout.Height(35)))
        {
            CreateAllLevelData();
            levelDataCreated = true;
        }

        EditorGUILayout.Space(10);

        // Full setup
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("Run Full Setup (All Steps)", GUILayout.Height(45)))
        {
            CreateAllScenes();
            CreatePlayerPrefabs();
            CreateAllLevelData();
            scenesCreated = prefabsCreated = levelDataCreated = true;
            EditorUtility.DisplayDialog("Setup Complete",
                "All scenes, prefabs, and level data have been created.\n\n" +
                "Open Assets/Scenes/MainMenu.unity to get started!", "OK");
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndScrollView();
    }

    private void CreateAllScenes()
    {
        CreateScene("Assets/Scenes/MainMenu.unity", SetupMainMenuScene);
        CreateScene("Assets/Scenes/Lobby.unity", SetupLobbyScene);

        string[][] levels = new string[][]
        {
            new[] { "Level_1_1", "Level_1_2", "Level_1_3" },
            new[] { "Level_2_1", "Level_2_2", "Level_2_3", "Level_2_4" },
            new[] { "Level_3_1", "Level_3_2", "Level_3_3", "Level_3_4" },
            new[] { "Level_4_1", "Level_4_2", "Level_4_3", "Level_4_4", "Level_4_5" },
            new[] { "Level_5_1", "Level_5_2", "Level_5_3", "Level_5_4" },
        };

        for (int ch = 0; ch < levels.Length; ch++)
        {
            for (int lv = 0; lv < levels[ch].Length; lv++)
            {
                string path = $"Assets/Scenes/Chapter{ch + 1}/{levels[ch][lv]}.unity";
                int chapter = ch + 1;
                int level = lv + 1;
                CreateScene(path, () => SetupLevelScene(chapter, level));
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("[Setup] All scenes created.");
    }

    private void CreateScene(string path, System.Action setupAction)
    {
        string dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        setupAction?.Invoke();
        EditorSceneManager.SaveScene(scene, path);
    }

    private void SetupMainMenuScene()
    {
        // GameManager
        var gmObj = new GameObject("GameManager");
        gmObj.AddComponent<GameManager>();
        gmObj.AddComponent<SceneLoader>();
        gmObj.AddComponent<SaveSystem>();

        // AudioManager
        var audioObj = new GameObject("AudioManager");
        var am = audioObj.AddComponent<AudioManager>();
        audioObj.AddComponent<AudioSource>(); // BGM
        audioObj.AddComponent<AudioSource>(); // SFX
        audioObj.AddComponent<AudioSource>(); // Ambient

        // Canvas
        var canvasObj = new GameObject("Canvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // EventSystem
        var eventObj = new GameObject("EventSystem");
        eventObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    private void SetupLobbyScene()
    {
        var canvasObj = new GameObject("Canvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var eventObj = new GameObject("EventSystem");
        eventObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        var lobbyObj = new GameObject("LobbyManager");
        lobbyObj.AddComponent<LobbyManager>();
        lobbyObj.AddComponent<RoomDiscovery>();
    }

    private void SetupLevelScene(int chapter, int level)
    {
        // Level Manager
        var lmObj = new GameObject("LevelManager");
        lmObj.AddComponent<LevelManager>();

        // Input
        var inputObj = new GameObject("InputManager");
        inputObj.AddComponent<InputManager>();

        // Ground
        var groundObj = new GameObject("Ground");
        var groundSR = groundObj.AddComponent<SpriteRenderer>();
        groundSR.color = new Color(0.3f, 0.25f, 0.2f);
        groundSR.sortingLayerName = "Tilemap";
        var groundCol = groundObj.AddComponent<BoxCollider2D>();
        groundCol.size = new Vector2(50, 2);
        groundObj.transform.position = new Vector3(15, -2, 0);
        groundObj.layer = LayerMask.NameToLayer("Ground");

        // Camera
        var cam = Camera.main;
        if (cam != null)
        {
            cam.gameObject.AddComponent<CameraController>();
            cam.orthographicSize = 6;
        }

        // Canvas for HUD
        var canvasObj = new GameObject("HUDCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var eventObj = new GameObject("EventSystem");
        eventObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    private void CreatePlayerPrefabs()
    {
        CreatePlayerPrefab("Lux", new Color(1f, 0.9f, 0.5f), true);
        CreatePlayerPrefab("Nox", new Color(0.3f, 0.15f, 0.5f), false);
        AssetDatabase.Refresh();
        Debug.Log("[Setup] Player prefabs created.");
    }

    private void CreatePlayerPrefab(string name, Color color, bool isLux)
    {
        var playerObj = new GameObject(name);

        // Sprite
        var sr = playerObj.AddComponent<SpriteRenderer>();
        sr.color = color;
        sr.sortingLayerName = "Players";

        // Physics
        var rb = playerObj.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 2.5f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var collider = playerObj.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.8f, 1.2f);

        // GroundCheck child
        var groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.SetParent(playerObj.transform);
        groundCheck.transform.localPosition = new Vector3(0, -0.65f, 0);

        // Scripts
        var controller = playerObj.AddComponent<PlayerController>();
        playerObj.AddComponent<PlayerHealth>();

        if (isLux)
            playerObj.AddComponent<LuxAbilities>();
        else
            playerObj.AddComponent<NoxAbilities>();

        // Save prefab
        string dir = "Assets/Prefabs/Player";
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string path = $"{dir}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(playerObj, path);
        DestroyImmediate(playerObj);
    }

    private void CreateAllLevelData()
    {
        string dir = "Assets/ScriptableObjects/LevelData";
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        CreateLevelAsset(1, 1, "Awakening", dir, new Vector2(-3, 0), new Vector2(3, 0));
        CreateLevelAsset(1, 2, "First Steps Together", dir, new Vector2(-4, 0), new Vector2(4, 0));
        CreateLevelAsset(1, 3, "Light and Shadow", dir, new Vector2(-3, 1), new Vector2(3, 1));

        CreateLevelAsset(2, 1, "The Clockwork Gate", dir, new Vector2(-3, 0), new Vector2(3, 0));
        CreateLevelAsset(2, 2, "Refraction Hall", dir, new Vector2(-5, 0), new Vector2(5, 0));
        CreateLevelAsset(2, 3, "Gear Assembly", dir, new Vector2(-3, 2), new Vector2(3, 2));
        CreateLevelAsset(2, 4, "The Mirror Engine", dir, new Vector2(-4, 0), new Vector2(4, 0));

        CreateLevelAsset(3, 1, "Tide Pools", dir, new Vector2(-3, 3), new Vector2(3, 3));
        CreateLevelAsset(3, 2, "The Dark Current", dir, new Vector2(-4, 0), new Vector2(4, 0));
        CreateLevelAsset(3, 3, "Luminescent Cave", dir, new Vector2(-3, 1), new Vector2(3, 1));
        CreateLevelAsset(3, 4, "Abyssal Gate", dir, new Vector2(-5, 0), new Vector2(5, 0));

        CreateLevelAsset(4, 1, "Skyfall Ruins", dir, new Vector2(-3, 5), new Vector2(3, 5));
        CreateLevelAsset(4, 2, "Inverted Tower", dir, new Vector2(-3, 0), new Vector2(3, 0));
        CreateLevelAsset(4, 3, "Gravity Wells", dir, new Vector2(-4, 3), new Vector2(4, 3));
        CreateLevelAsset(4, 4, "The Floating Archive", dir, new Vector2(-3, 2), new Vector2(3, 2));
        CreateLevelAsset(4, 5, "Collapse", dir, new Vector2(-5, 5), new Vector2(5, 5));

        CreateLevelAsset(5, 1, "The Void Threshold", dir, new Vector2(-3, 0), new Vector2(3, 0));
        CreateLevelAsset(5, 2, "Fracture World", dir, new Vector2(-4, 0), new Vector2(4, 0));
        CreateLevelAsset(5, 3, "Memory Corridor", dir, new Vector2(-3, 1), new Vector2(3, 1));
        CreateLevelAsset(5, 4, "Reunion", dir, new Vector2(-2, 0), new Vector2(2, 0));

        AssetDatabase.Refresh();
        Debug.Log("[Setup] All 20 level data assets created.");
    }

    private void CreateLevelAsset(int chapter, int level, string name, string dir,
        Vector2 luxSpawn, Vector2 noxSpawn)
    {
        var data = ScriptableObject.CreateInstance<LevelData>();
        data.chapter = chapter;
        data.levelIndex = level;
        data.levelName = name;
        data.sceneName = $"Level_{chapter}_{level}";
        data.luxSpawnPoint = luxSpawn;
        data.noxSpawnPoint = noxSpawn;
        data.parTime = 300f;
        data.collectibleCount = 3;

        string path = $"{dir}/Ch{chapter}_Lv{level}_{name.Replace(" ", "")}.asset";
        AssetDatabase.CreateAsset(data, path);
    }
}
