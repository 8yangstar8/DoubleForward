using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.IO;

/// <summary>
/// 场景UI关联工厂 - 将SceneFactory创建的UI对象关联到对应脚本的SerializeField
/// 修复MainMenu/Level/Pause等场景中UI组件引用为空的问题
/// </summary>
public static class SceneUIWiringFactory
{
    [MenuItem("DoubleForward/Wire All Scene UI References", false, 60)]
    public static void WireAll()
    {
        int totalWired = 0;

        EditorUtility.DisplayProgressBar("Wiring", "Player prefabs...", 0.05f);
        totalWired += WirePlayerPrefabs();

        EditorUtility.DisplayProgressBar("Wiring", "Enemy prefabs...", 0.08f);
        totalWired += WireEnemyPrefabs();

        EditorUtility.DisplayProgressBar("Wiring", "Manager prefabs...", 0.09f);
        totalWired += WireManagerPrefabs();

        EditorUtility.DisplayProgressBar("Wiring", "MainMenu...", 0.1f);
        totalWired += WireMainMenuScene();

        EditorUtility.DisplayProgressBar("Wiring", "Level scenes...", 0.3f);
        for (int ch = 1; ch <= 5; ch++)
        {
            for (int lv = 1; lv <= 4; lv++)
            {
                float p = 0.3f + (float)((ch - 1) * 4 + lv) / 20f * 0.6f;
                EditorUtility.DisplayProgressBar("Wiring", $"Level_{ch}_{lv}...", p);
                totalWired += WireLevelScene(ch, lv);
            }
        }

        EditorUtility.ClearProgressBar();
        Debug.Log($"[UIWiring] Done! Wired {totalWired} references across all scenes and prefabs.");
    }

    /// <summary>
    /// CLI入口
    /// </summary>
    public static void WireAllFromCommandLine()
    {
        WireAll();
    }

    // ==================== Player Prefabs ====================

    private static int WirePlayerPrefabs()
    {
        int wired = 0;
        string[] players = { "Lux", "Nox" };

        foreach (var name in players)
        {
            string path = $"Assets/Prefabs/Player/{name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            // 打开prefab编辑模式
            var root = PrefabUtility.LoadPrefabContents(path);
            var controller = root.GetComponent<PlayerController>();
            if (controller == null) { PrefabUtility.UnloadPrefabContents(root); continue; }

            var so = new SerializedObject(controller);

            // groundCheck
            var groundCheckT = root.transform.Find("GroundCheck");
            if (groundCheckT != null)
            {
                var prop = so.FindProperty("groundCheck");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = groundCheckT;
                    wired++;
                }
            }

            // wallCheckPoint
            var wallCheckT = root.transform.Find("WallCheck");
            if (wallCheckT != null)
            {
                var prop = so.FindProperty("wallCheckPoint");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = wallCheckT;
                    wired++;
                }
            }

            // groundLayer → "Ground" layer
            var layerProp = so.FindProperty("groundLayer");
            if (layerProp != null)
            {
                int groundLayerIdx = LayerMask.NameToLayer("Ground");
                if (groundLayerIdx >= 0)
                {
                    int mask = 1 << groundLayerIdx;
                    if (layerProp.intValue == 0)
                    {
                        layerProp.intValue = mask;
                        wired++;
                    }
                }
            }

            // playerIndex
            var indexProp = so.FindProperty("playerIndex");
            if (indexProp != null)
            {
                int expected = name == "Lux" ? 0 : 1;
                if (indexProp.intValue != expected)
                {
                    indexProp.intValue = expected;
                    wired++;
                }
            }

            // playerType
            var typeProp = so.FindProperty("playerType");
            if (typeProp != null)
            {
                int expected = name == "Lux" ? 0 : 1; // Lux=0, Nox=1
                if (typeProp.enumValueIndex != expected)
                {
                    typeProp.enumValueIndex = expected;
                    wired++;
                }
            }

            // Player layer
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0 && root.layer != playerLayer)
            {
                root.layer = playerLayer;
                wired++;
            }

            // Player tag (DeathZone, AudioZone, etc. use CompareTag("Player"))
            try
            {
                if (root.tag != "Player")
                {
                    root.tag = "Player";
                    wired++;
                }
            }
            catch (UnityException)
            {
                Debug.LogWarning("[UIWiring] 'Player' tag not defined. Run Setup Layers & Tags first.");
            }

            so.ApplyModifiedProperties();

            // ===== PlayerCombat 引用关联 =====
            var combat = root.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                var cso = new SerializedObject(combat);
                var attackPoint = root.transform.Find("AttackPoint");
                int enemyLayer = LayerMask.NameToLayer("Enemy");

                if (attackPoint != null)
                {
                    var mp = cso.FindProperty("meleeAttackPoint");
                    if (mp != null && mp.objectReferenceValue == null)
                    { mp.objectReferenceValue = attackPoint; wired++; }

                    var fp = cso.FindProperty("firePoint");
                    if (fp != null && fp.objectReferenceValue == null)
                    { fp.objectReferenceValue = attackPoint; wired++; }
                }

                if (enemyLayer >= 0)
                {
                    var el = cso.FindProperty("enemyLayer");
                    if (el != null && el.intValue == 0)
                    { el.intValue = 1 << enemyLayer; wired++; }
                }

                // 玩家光弹预制体
                var projPrefab = GetOrCreatePlayerProjectile(name);
                if (projPrefab != null)
                {
                    var pp = cso.FindProperty("projectilePrefab");
                    if (pp != null && pp.objectReferenceValue == null)
                    { pp.objectReferenceValue = projPrefab; wired++; }
                }

                cso.ApplyModifiedProperties();
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[UIWiring] Player prefabs: wired {wired} references");
        return wired;
    }

    // ==================== 玩家光弹预制体 ====================

    private static GameObject GetOrCreatePlayerProjectile(string playerName)
    {
        string dir = "Assets/Prefabs/Player";
        string path = $"{dir}/{playerName}Bolt.prefab";

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        // 创建光弹
        var obj = new GameObject($"{playerName}Bolt");

        var sr = obj.AddComponent<UnityEngine.SpriteRenderer>();
        sr.color = playerName == "Lux" ? new Color(1f, 0.95f, 0.5f) : new Color(0.6f, 0.3f, 0.9f);
        sr.sortingOrder = 9;
        // 使用占位精灵
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/VFX/GlowSoft.png");
        if (sprite != null) sr.sprite = sprite;

        var col = obj.AddComponent<UnityEngine.CircleCollider2D>();
        col.radius = 0.25f;
        col.isTrigger = true;

        var rb = obj.AddComponent<UnityEngine.Rigidbody2D>();
        rb.gravityScale = 0;
        rb.bodyType = UnityEngine.RigidbodyType2D.Kinematic;

        var proj = obj.AddComponent<Projectile>();
        // hitLayers = Ground + Enemy
        var pso = new SerializedObject(proj);
        int groundLayer = LayerMask.NameToLayer("Ground");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int mask = 0;
        if (groundLayer >= 0) mask |= 1 << groundLayer;
        if (enemyLayer >= 0) mask |= 1 << enemyLayer;
        var hl = pso.FindProperty("hitLayers");
        if (hl != null) hl.intValue = mask;
        var lt = pso.FindProperty("lifetime");
        if (lt != null) lt.floatValue = 3f;
        pso.ApplyModifiedProperties();

        var saved = PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
        Debug.Log($"[UIWiring] Created player projectile: {path}");
        return saved;
    }

    // ==================== Enemy Prefabs ====================

    private static int WireEnemyPrefabs()
    {
        int wired = 0;
        string dir = "Assets/Prefabs/Enemies";
        if (!Directory.Exists(dir)) return 0;

        var files = Directory.GetFiles(dir, "*.prefab");
        foreach (var file in files)
        {
            string path = file.Replace('\\', '/');
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var enemy = prefab.GetComponent<EnemyBase>();
            if (enemy == null) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            var enemyComp = root.GetComponent<EnemyBase>();
            if (enemyComp == null) { PrefabUtility.UnloadPrefabContents(root); continue; }

            var so = new SerializedObject(enemyComp);

            // groundLayer
            var layerProp = so.FindProperty("groundLayer");
            if (layerProp != null && layerProp.intValue == 0)
            {
                int groundLayerIdx = LayerMask.NameToLayer("Ground");
                if (groundLayerIdx >= 0)
                {
                    layerProp.intValue = 1 << groundLayerIdx;
                    wired++;
                }
            }

            // patrol points
            var patrolProp = so.FindProperty("patrolPoints");
            if (patrolProp != null && patrolProp.arraySize == 0)
            {
                var p0 = root.transform.Find("Patrol_0");
                var p1 = root.transform.Find("Patrol_1");
                if (p0 != null && p1 != null)
                {
                    patrolProp.arraySize = 2;
                    patrolProp.GetArrayElementAtIndex(0).objectReferenceValue = p0;
                    patrolProp.GetArrayElementAtIndex(1).objectReferenceValue = p1;
                    wired += 2;
                }
            }

            so.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[UIWiring] Enemy prefabs: wired {wired} references");
        return wired;
    }

    // ==================== Manager Prefabs ====================

    private static int WireManagerPrefabs()
    {
        int wired = 0;
        string dir = "Assets/Prefabs/Managers";
        if (!Directory.Exists(dir)) return 0;

        // AudioManager: wire 3 AudioSource fields
        string audioPath = $"{dir}/AudioManager.prefab";
        if (File.Exists(audioPath))
        {
            var root = PrefabUtility.LoadPrefabContents(audioPath);
            var audioMgr = root.GetComponent<AudioManager>();
            if (audioMgr != null)
            {
                var sources = root.GetComponents<AudioSource>();
                if (sources.Length >= 3)
                {
                    var so = new SerializedObject(audioMgr);
                    var bgm = so.FindProperty("bgmSource");
                    var sfx = so.FindProperty("sfxSource");
                    var ambient = so.FindProperty("ambientSource");

                    if (bgm != null && bgm.objectReferenceValue == null)
                    { bgm.objectReferenceValue = sources[0]; wired++; }
                    if (sfx != null && sfx.objectReferenceValue == null)
                    { sfx.objectReferenceValue = sources[1]; wired++; }
                    if (ambient != null && ambient.objectReferenceValue == null)
                    { ambient.objectReferenceValue = sources[2]; wired++; }

                    so.ApplyModifiedProperties();
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, audioPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[UIWiring] Manager prefabs: wired {wired} references");
        return wired;
    }

    // ==================== MainMenu ====================

    private static int WireMainMenuScene()
    {
        string path = "Assets/Scenes/MainMenu.unity";
        if (!File.Exists(path)) return 0;

        EditorSceneManager.OpenScene(path);
        int wired = 0;

        var menuUI = Object.FindAnyObjectByType<MainMenuUI>();
        if (menuUI == null)
        {
            Debug.LogWarning("[UIWiring] MainMenuUI not found in MainMenu scene");
            return 0;
        }

        var so = new SerializedObject(menuUI);

        // 按钮关联
        wired += WireButton(so, "continueButton", "ContinueButton");
        wired += WireButton(so, "newGameButton", "NewGameButton");
        wired += WireButton(so, "localPlayButton", "LocalPlayButton");
        wired += WireButton(so, "onlinePlayButton", "OnlinePlayButton");
        wired += WireButton(so, "settingsButton", "SettingsButton");

        // 通关后按钮
        wired += WireButton(so, "newGamePlusButton", "NGPlusButton");
        wired += WireButton(so, "bossRushButton", "BossRushButton");
        wired += WireButton(so, "storyRecapButton", "StoryRecapButton");

        // 面板
        wired += WireGameObject(so, "mainPanel", "ButtonPanel");
        wired += WireGameObject(so, "postGameGroup", "PostGamePanel");

        // 文本
        wired += WireTMP(so, "versionText", "SubtitleText"); // 副标题可复用为版本号

        // CanvasGroup
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            var cg = canvas.GetComponent<CanvasGroup>();
            if (cg == null) cg = canvas.gameObject.AddComponent<CanvasGroup>();
            var cgProp = so.FindProperty("mainCanvasGroup");
            if (cgProp != null && cgProp.objectReferenceValue == null)
            {
                cgProp.objectReferenceValue = cg;
                wired++;
            }
        }

        so.ApplyModifiedProperties();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log($"[UIWiring] MainMenu: wired {wired} references");
        return wired;
    }

    // ==================== Level Scenes ====================

    private static int WireLevelScene(int chapter, int level)
    {
        string path = $"Assets/Scenes/Chapter{chapter}/Level_{chapter}_{level}.unity";
        if (!File.Exists(path)) return 0;

        EditorSceneManager.OpenScene(path);
        int wired = 0;

        // 设置Ground/Platform层
        wired += AssignGroundLayers();

        // 设置Player层
        wired += AssignPlayerLayers();

        // 设置Enemy层
        wired += AssignEnemyLayers();

        // HUDManager
        var hud = Object.FindAnyObjectByType<HUDManager>();
        if (hud != null)
        {
            var so = new SerializedObject(hud);
            wired += WireTMP(so, "levelNameText", "LevelName");
            wired += WireTMP(so, "timerText", "Timer");
            wired += WireTMP(so, "collectibleText", "Collectibles");
            so.ApplyModifiedProperties();
        }

        // PauseMenuUI
        var pause = Object.FindAnyObjectByType<PauseMenuUI>();
        if (pause != null)
        {
            var so = new SerializedObject(pause);
            wired += WireButton(so, "resumeButton", "ResumeButton");
            wired += WireButton(so, "settingsButton", "SettingsButton");
            wired += WireButton(so, "restartButton", "RestartButton");
            wired += WireButton(so, "mainMenuButton", "QuitButton");
            wired += WireGameObject(so, "pausePanel", "PausePanel");
            so.ApplyModifiedProperties();
        }

        // LevelCompleteUI — 如果不存在则创建
        var lcUI = Object.FindAnyObjectByType<LevelCompleteUI>();
        if (lcUI == null)
        {
            wired += CreateLevelCompleteCanvas();
        }

        // 压力板谜题连线 — 为每个压力板创建一扇门并连接
        wired += WirePuzzleLinks();

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        return wired;
    }

    private static int WirePuzzleLinks()
    {
        int wired = 0;
        var plates = Object.FindObjectsByType<PressurePlate>(FindObjectsSortMode.None);
        int groundLayer = LayerMask.NameToLayer("Ground");

        foreach (var plate in plates)
        {
            // 已连接则跳过
            if (plate.GetComponent<PuzzleLink>() != null) continue;

            // 在压力板右侧3米创建一扇门(障碍墙)
            var door = new GameObject("PuzzleDoor");
            door.transform.position = plate.transform.position + new Vector3(3f, 1.5f, 0);
            if (groundLayer >= 0) door.layer = groundLayer; // 门挡住玩家

            var sr = door.AddComponent<UnityEngine.SpriteRenderer>();
            sr.color = new Color(0.6f, 0.4f, 0.2f);
            sr.sortingOrder = 1;
            var doorSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tiles/Block.png");
            if (doorSprite != null) sr.sprite = doorSprite;
            door.transform.localScale = new Vector3(0.8f, 3f, 1f);

            var doorCol = door.AddComponent<UnityEngine.BoxCollider2D>();
            doorCol.size = Vector2.one;

            // 连接器: 踩下压力板时门上升打开
            var link = plate.gameObject.AddComponent<PuzzleLink>();
            link.Configure(plate, door, Vector3.up * 3.5f);
            wired++;
        }

        if (wired > 0) Debug.Log($"[UIWiring] Created {wired} puzzle door links");
        return wired;
    }

    // ==================== LevelComplete UI ====================

    private static int CreateLevelCompleteCanvas()
    {
        var canvasObj = new GameObject("LevelCompleteCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // 面板（默认隐藏）
        var panel = new GameObject("CompletePanel");
        panel.transform.SetParent(canvasObj.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.25f, 0.15f);
        panelRect.anchorMax = new Vector2(0.75f, 0.85f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelImg = panel.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        panel.SetActive(false);

        // 标题
        CreateTMPText(panel.transform, "TitleText", "关卡完成！",
            new Vector2(0, 200), 48, Color.yellow);

        // 时间
        CreateTMPText(panel.transform, "TimeText", "用时: 00:00",
            new Vector2(0, 100), 28, Color.white);

        // 收集品
        CreateTMPText(panel.transform, "CollectibleText", "收集: 0/0",
            new Vector2(0, 50), 28, Color.white);

        // 星级区域
        for (int i = 0; i < 3; i++)
        {
            var star = new GameObject($"Star_{i}");
            star.transform.SetParent(panel.transform, false);
            var starRect = star.AddComponent<RectTransform>();
            starRect.anchoredPosition = new Vector2(-60 + i * 60, 0);
            starRect.sizeDelta = new Vector2(50, 50);
            var starImg = star.AddComponent<UnityEngine.UI.Image>();
            starImg.color = Color.gray;
        }

        // 按钮
        CreateUIButton(panel.transform, "NextLevelButton", "下一关",
            new Vector2(0, -120), new Vector2(200, 50));
        CreateUIButton(panel.transform, "ReplayButton", "重新挑战",
            new Vector2(0, -180), new Vector2(200, 50));
        CreateUIButton(panel.transform, "MenuButton", "返回菜单",
            new Vector2(0, -240), new Vector2(200, 50));

        // 添加LevelCompleteUI组件并关联
        var lcUI = canvasObj.AddComponent<LevelCompleteUI>();
        var so = new SerializedObject(lcUI);

        var panelProp = so.FindProperty("completePanel");
        if (panelProp != null) panelProp.objectReferenceValue = panel;

        WireTMPOnSO(so, "levelNameText", panel.transform, "TitleText");
        WireTMPOnSO(so, "timeText", panel.transform, "TimeText");
        WireTMPOnSO(so, "collectibleText", panel.transform, "CollectibleText");
        WireButtonOnSO(so, "nextLevelButton", panel.transform, "NextLevelButton");
        WireButtonOnSO(so, "replayButton", panel.transform, "ReplayButton");
        WireButtonOnSO(so, "menuButton", panel.transform, "MenuButton");

        // 星级Image数组
        var starsProp = so.FindProperty("stars");
        if (starsProp != null)
        {
            starsProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var starObj = panel.transform.Find($"Star_{i}");
                if (starObj != null)
                    starsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                        starObj.GetComponent<UnityEngine.UI.Image>();
            }
        }

        so.ApplyModifiedProperties();
        Debug.Log("[UIWiring] Created LevelCompleteCanvas");
        return 10;
    }

    private static void CreateTMPText(Transform parent, string name, string text,
        Vector2 pos, float fontSize, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(400, 60);
        var tmp = obj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
    }

    private static void CreateUIButton(Transform parent, string name, string label,
        Vector2 pos, Vector2 size)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        var img = obj.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.25f, 0.25f, 0.35f, 0.9f);
        obj.AddComponent<UnityEngine.UI.Button>();

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 24;
        tmp.color = Color.white;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
    }

    private static void WireTMPOnSO(SerializedObject so, string propName,
        Transform parent, string childName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        var child = parent.Find(childName);
        if (child == null) return;
        var tmp = child.GetComponent<TMPro.TextMeshProUGUI>();
        if (tmp != null) prop.objectReferenceValue = tmp;
    }

    private static void WireButtonOnSO(SerializedObject so, string propName,
        Transform parent, string childName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        var child = parent.Find(childName);
        if (child == null) return;
        var btn = child.GetComponent<UnityEngine.UI.Button>();
        if (btn != null) prop.objectReferenceValue = btn;
    }

    // ==================== Layer 分配 ====================

    private static int AssignGroundLayers()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0) return 0;

        int count = 0;
        var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            string n = t.name.ToLower();
            bool isGround = n.Contains("ground") || n.Contains("platform") || n.Contains("floor");
            // 只处理有Collider2D的对象
            if (isGround && t.GetComponent<Collider2D>() != null && t.gameObject.layer != groundLayer)
            {
                t.gameObject.layer = groundLayer;
                count++;
            }
        }
        return count;
    }

    private static int AssignPlayerLayers()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer < 0) return 0;

        int count = 0;
        var controllers = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var pc in controllers)
        {
            if (pc.gameObject.layer != playerLayer)
            {
                pc.gameObject.layer = playerLayer;
                count++;
            }
        }
        return count;
    }

    private static int AssignEnemyLayers()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) return 0;

        int count = 0;
        var enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            if (e.gameObject.layer != enemyLayer)
            {
                e.gameObject.layer = enemyLayer;
                count++;
            }
        }
        return count;
    }

    // ==================== 辅助方法 ====================

    private static int WireButton(SerializedObject so, string propName, string goName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null || prop.objectReferenceValue != null) return 0;

        var go = FindInScene(goName);
        if (go == null) return 0;

        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();

        prop.objectReferenceValue = btn;
        return 1;
    }

    private static int WireGameObject(SerializedObject so, string propName, string goName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null || prop.objectReferenceValue != null) return 0;

        var go = FindInScene(goName);
        if (go == null) return 0;

        prop.objectReferenceValue = go;
        return 1;
    }

    private static int WireTMP(SerializedObject so, string propName, string goName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null || prop.objectReferenceValue != null) return 0;

        var go = FindInScene(goName);
        if (go == null) return 0;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) return 0;

        prop.objectReferenceValue = tmp;
        return 1;
    }

    private static GameObject FindInScene(string name)
    {
        // 全场景搜索同名对象
        var allObjects = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in allObjects)
        {
            if (t.name == name)
                return t.gameObject;
        }
        return null;
    }
}
