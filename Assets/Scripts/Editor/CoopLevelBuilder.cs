using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 合作关卡构建器 - 在 Level_1_2 里搭出两道"能力互补门",互为前置:
///   ① 影墙: 只有Nox能影穿通过, Lux被挡在外面
///   ② 压力板: Nox踩住后影墙消失, 放Lux进来
///   ③ 光敏机关: 只有Lux的光束能激活(玩家踩不亮)
///   ④ 大门: 机关锁存激活后升起, 两人一同前往终点
/// 用法: -executeMethod CoopLevelBuilder.BuildLevel12
/// </summary>
public static class CoopLevelBuilder
{
    private const string ScenePath = "Assets/Scenes/Chapter1/Level_1_2.unity";
    private const string Prefix = "Coop_";
    private const string SpriteDir = "Assets/Resources/Art/";

    [MenuItem("DoubleForward/Build Co-op Level 1-2", false, 10)]
    public static void BuildLevel12()
    {
        EditorSceneManager.OpenScene(ScenePath);

        var ground = GameObject.Find("Ground");
        var luxSpawn = GameObject.Find("LuxSpawnPoint");
        var goal = Object.FindAnyObjectByType<LevelGoalTrigger>();
        if (ground == null || luxSpawn == null || goal == null)
        {
            Debug.LogError("[CoopLevel] Ground / LuxSpawnPoint / LevelGoal missing, aborting");
            return;
        }

        ClearPreviousBuild();
        RemoveBrokenLegacyPuzzle();

        float startX = luxSpawn.transform.position.x;
        float standY = luxSpawn.transform.position.y;                       // 玩家站立时的中心高度
        float groundTopY = GroundTop(ground);

        float wallX = startX + 6f;
        float plateX = startX + 10f;
        float sensorX = startX + 16f;
        float doorX = startX + 20f;
        float goalX = startX + 25f;

        EnsureGroundSpans(ground, startX - 3f, goalX + 3f);
        ClearCorridor(startX, goalX, groundTopY);
        // 谜题关不要巡逻敌人: 它们会堵在通往终点的路上,把设计好的解谜流程搅乱
        ClearEnemies(startX, goalX + 2f);

        var parent = GameObject.Find("--- PUZZLES ---");
        Transform p = parent != null ? parent.transform : null;

        // ① 影墙 - 只有影穿中的Nox能过
        var wall = CreateBlock("Coop_ShadowWall", new Vector3(wallX, groundTopY + 2f, 0f),
            Vector3.one, Color.white, "ShadowWallTile", p, false);
        wall.AddComponent<ShadowWall>();
        // 必须在编辑期就写好层: 场景加载时碰撞体就以该层注册,等ShadowWall.Start()
        // 运行时再改层,层过滤对已注册的碰撞体不会生效(LevelBuilderWindow同样这么做)
        int shadowWallLayer = LayerMask.NameToLayer("ShadowWall");
        if (shadowWallLayer >= 0) wall.layer = shadowWallLayer;

        // ② 压力板 - Nox踩住后影墙消失
        var plateGO = CreateBlock("Coop_Plate", new Vector3(plateX, groundTopY + 0.15f, 0f),
            Vector3.one, Color.white, "PressurePlateArt", p, false);
        var plate = plateGO.AddComponent<PressurePlate>();

        // ③ 光敏机关 - 放在玩家站立高度,Lux走过来平射即可命中
        var sensorGO = CreateBlock("Coop_GateSensor", new Vector3(sensorX, standY, 0f),
            Vector3.one, Color.white, "LightSensorArt", p, true);
        var sensor = sensorGO.AddComponent<LightSensor>();
        // 锁存: 光束只持续3秒,不锁存的话门会立刻落回去
        var sensorSO = new SerializedObject(sensor);
        sensorSO.FindProperty("stayActivated").boolValue = true;
        sensorSO.FindProperty("sensorRenderer").objectReferenceValue =
            sensorGO.GetComponent<SpriteRenderer>();
        sensorSO.ApplyModifiedProperties();

        // ④ 大门 - 机关激活后升起
        var door = CreateBlock("Coop_GateDoor", new Vector3(doorX, groundTopY + 2f, 0f),
            Vector3.one, Color.white, "GateDoorArt", p, false);
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0) door.layer = groundLayer; // 门要挡住玩家

        // 接线
        var wallLink = new GameObject("Coop_Link_Wall");
        if (p != null) wallLink.transform.SetParent(p);
        wallLink.AddComponent<PuzzleLink>().ConfigureDisable(plate, wall);

        var doorLink = new GameObject("Coop_Link_Door");
        if (p != null) doorLink.transform.SetParent(p);
        doorLink.AddComponent<PuzzleLink>().Configure(sensor, door, Vector3.up * 4.5f);

        // 地面贴上生成的草地/泥土材质
        var groundSr = ground.GetComponent<SpriteRenderer>();
        if (groundSr != null)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "GroundTile.png");
            if (tile != null) { groundSr.sprite = tile; groundSr.color = Color.white; }
        }

        // 终点挪到最后一道门之后
        goal.transform.position = new Vector3(goalX, goal.transform.position.y, 0f);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[CoopLevel] Level_1_2 built: wall={wallX:F1} plate={plateX:F1} " +
            $"sensor={sensorX:F1} door={doorX:F1} goal={goalX:F1}");
    }

    /// <summary>
    /// Level_1_3 - 用上1_2没用到的两个能力:
    ///   ① Nox影推箱子压住压板 → A门永久开启(箱子替人站着,队友不必一直踩着)
    ///   ② 高处光敏机关地面平射够不到 → Lux造光桥站上去才能打亮 → B门开
    /// </summary>
    [MenuItem("DoubleForward/Build Co-op Level 1-3", false, 12)]
    public static void BuildLevel13()
    {
        const string scene13 = "Assets/Scenes/Chapter1/Level_1_3.unity";
        EditorSceneManager.OpenScene(scene13);

        var ground = GameObject.Find("Ground");
        var luxSpawn = GameObject.Find("LuxSpawnPoint");
        var goal = Object.FindAnyObjectByType<LevelGoalTrigger>();
        if (ground == null || luxSpawn == null || goal == null)
        {
            Debug.LogError("[CoopLevel] 1-3: Ground / LuxSpawnPoint / LevelGoal missing");
            return;
        }

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name.StartsWith("Coop3_")) Object.DestroyImmediate(go);

        float startX = luxSpawn.transform.position.x;
        float standY = luxSpawn.transform.position.y;
        float groundTopY = GroundTop(ground);

        float plateX = startX + 9f;
        float crateX = plateX - 2.5f;   // 推一两下就到位,不要让玩家一路推4格
        float doorAX = startX + 12f;
        float sensorX = startX + 18f;
        float doorBX = startX + 21f;
        float goalX = startX + 26f;

        EnsureGroundSpans(ground, startX - 3f, goalX + 3f);
        ClearCorridor(startX, goalX, groundTopY);
        // 推箱走廊里不能有巡逻敌人: 它们会晃过来把箱子顶回去,谜题就没法完成了
        ClearEnemies(crateX - 2f, doorAX + 2f);

        var parent = GameObject.Find("--- PUZZLES ---");
        Transform p = parent != null ? parent.transform : null;

        // ① 可推箱子 - 只有Nox的影推能挪动它
        var crate = CreateBlock("Coop3_Crate", new Vector3(crateX, groundTopY + 0.5f, 0f),
            Vector3.one, Color.white, "CrateArt", p, false);
        crate.tag = "Pushable";
        var crateRb = crate.AddComponent<Rigidbody2D>();
        crateRb.gravityScale = 2.5f;
        crateRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        crateRb.mass = 1.2f;
        crateRb.drag = 2.5f;   // 阻尼大一些,推一下走一段就停,便于对准压板

        var plateGO = CreateBlock("Coop3_Plate", new Vector3(plateX, groundTopY + 0.15f, 0f),
            Vector3.one, Color.white, "PressurePlateArt", p, false);
        var plate = plateGO.AddComponent<PressurePlate>();

        var doorA = CreateBlock("Coop3_DoorA", new Vector3(doorAX, groundTopY + 2f, 0f),
            Vector3.one, Color.white, "GateDoorArt", p, false);
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0) doorA.layer = groundLayer;

        // ② 高处光敏机关 - 抬到地面平射够不到的高度
        // 高度必须"地面打不到、站上光桥打得到"。+3 时一座桥的站立高度约2.09,
        // 差0.56够不着(实测走通失败);+2.0 刚好在桥上够得到,地面平射仍打不着
        var sensorGO = CreateBlock("Coop3_HighSensor", new Vector3(sensorX, standY + 2f, 0f),
            Vector3.one, Color.white, "LightSensorArt", p, true);
        var sensor = sensorGO.AddComponent<LightSensor>();
        var so = new SerializedObject(sensor);
        so.FindProperty("stayActivated").boolValue = true;
        so.FindProperty("sensorRenderer").objectReferenceValue = sensorGO.GetComponent<SpriteRenderer>();
        so.ApplyModifiedProperties();

        var doorB = CreateBlock("Coop3_DoorB", new Vector3(doorBX, groundTopY + 2f, 0f),
            Vector3.one, Color.white, "GateDoorArt", p, false);
        if (groundLayer >= 0) doorB.layer = groundLayer;

        var linkA = new GameObject("Coop3_Link_DoorA");
        if (p != null) linkA.transform.SetParent(p);
        linkA.AddComponent<PuzzleLink>().Configure(plate, doorA, Vector3.up * 4.5f);

        var linkB = new GameObject("Coop3_Link_DoorB");
        if (p != null) linkB.transform.SetParent(p);
        linkB.AddComponent<PuzzleLink>().Configure(sensor, doorB, Vector3.up * 4.5f);

        goal.transform.position = new Vector3(goalX, goal.transform.position.y, 0f);

        var groundSr = ground.GetComponent<SpriteRenderer>();
        if (groundSr != null)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "GroundTile.png");
            if (tile != null) { groundSr.sprite = tile; groundSr.color = Color.white; }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[CoopLevel] Level_1_3 built: crate={crateX:F1} plate={plateX:F1} " +
            $"doorA={doorAX:F1} sensor={sensorX:F1}@standY+2 doorB={doorBX:F1} goal={goalX:F1}");
    }

    /// <summary>
    /// Level_1_4 (第一章Boss关) - 把Boss战改成需要两人配合:
    ///   Boss 常态带护盾免疫伤害 → Lux 光束照亮弱点 → 护盾落下5秒 → Nox 输出
    /// 一个人打不动: 只有 Lux 有光束,而近战输出主要靠 Nox。
    /// 用法: -executeMethod CoopLevelBuilder.BuildLevel14
    /// </summary>
    [MenuItem("DoubleForward/Build Co-op Boss 1-4", false, 13)]
    public static void BuildLevel14()
    {
        const string scene14 = "Assets/Scenes/Chapter1/Level_1_4.unity";
        EditorSceneManager.OpenScene(scene14);

        var boss = Object.FindAnyObjectByType<BossBase>();
        var luxSpawn = GameObject.Find("LuxSpawnPoint");
        if (boss == null || luxSpawn == null)
        {
            Debug.LogError("[CoopLevel] 1-4: Boss / LuxSpawnPoint missing");
            return;
        }

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name.StartsWith("Coop4_")) Object.DestroyImmediate(go);

        var parent = GameObject.Find("--- PUZZLES ---");
        Transform p = parent != null ? parent.transform : null;

        // 弱点放在Boss身前、玩家站立高度: Lux走到跟前平射就能照到
        float standY = luxSpawn.transform.position.y;
        var weakGO = CreateBlock("Coop4_WeakPoint",
            new Vector3(boss.transform.position.x - 2f, standY, 0f),
            Vector3.one, Color.white, "UnitSensor", p, true);
        var weak = weakGO.AddComponent<LightSensor>();
        var so = new SerializedObject(weak);
        // 不锁存: 光束移开后弱点熄灭,护盾窗口由 BossCoopShield 自己计时维持
        so.FindProperty("stayActivated").boolValue = false;
        so.FindProperty("sensorRenderer").objectReferenceValue = weakGO.GetComponent<SpriteRenderer>();
        so.ApplyModifiedProperties();

        BossArtUpgrade.Dress(boss.gameObject, 1);

        var shield = boss.gameObject.GetComponent<BossCoopShield>();
        if (shield == null) shield = boss.gameObject.AddComponent<BossCoopShield>();
        shield.Configure(boss, weak, 5f, BuildShieldVisual(boss.gameObject));
        EditorUtility.SetDirty(shield);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[CoopLevel] Level_1_4 boss shield wired: weakPoint at " +
            $"x={weakGO.transform.position.x:F1} y={standY:F1}, boss={boss.name}");
    }


    /// <summary>
    /// Boss 身上的护盾球。没有它,玩家看到的只是"打上去不掉血",既不知道原因
    /// 也不知道该做什么 —— 机制写了等于没写。带盾时显示,弱点被照亮后隐藏。
    /// </summary>
    private static GameObject BuildShieldVisual(GameObject boss)
    {
        const string spritePath = "Assets/Art/External/GenericPlatformer/boss_shield.png";
        const string childName = "ShieldVisual";

        AssetDatabase.ImportAsset(spritePath);
        var imp = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 32;
            imp.filterMode = FilterMode.Point;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null) { Debug.LogError("[CoopLevel] boss_shield.png missing"); return null; }

        var t = boss.transform.Find(childName);
        if (t == null)
        {
            t = new GameObject(childName).transform;
            t.SetParent(boss.transform);
        }

        var sr = t.GetComponent<SpriteRenderer>();
        if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.white;
        sr.sortingOrder = 20;                      // 盖在 Boss 之上

        // 罩住整个 Boss: 按碰撞体尺寸算,再放大一点留边;
        // 子物件要反向抵消父物件的缩放,否则 Boss 一被缩放护盾就变形
        var col = boss.GetComponent<Collider2D>();
        float span = col != null ? Mathf.Max(col.bounds.size.x, col.bounds.size.y) * 1.5f : 3f;
        float native = sprite.bounds.size.x;
        float k = native > 0f ? span / native : 1f;
        var ps = boss.transform.lossyScale;
        t.localScale = new Vector3(
            Mathf.Approximately(ps.x, 0f) ? k : k / ps.x,
            Mathf.Approximately(ps.y, 0f) ? k : k / ps.y,
            1f);
        t.position = col != null ? (Vector3)col.bounds.center : boss.transform.position;
        return t.gameObject;
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
    /// 移除模板遗留的坏掉的谜题: PressurePlate_1 被Unity的Reset()回调拽到了世界原点
    /// (玩家出生点上),开局就一直处于踩下状态,它的PuzzleDoor因此永远敞开、且正好
    /// 压在本关影墙的位置上。本关的谜题内容由下面的合作链路承担,这一对直接删掉。
    /// </summary>
    private static void RemoveBrokenLegacyPuzzle()
    {
        foreach (var name in new[] { "PressurePlate_1", "PuzzleDoor" })
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                Object.DestroyImmediate(go);
                Debug.Log($"[CoopLevel] Removed broken legacy object '{name}'");
            }
        }
    }

    /// <summary>
    /// 清理走廊 - 模板场景会在地面上随机撒平台,其中贴地的那些正好横在合作链路上,
    /// 会把影穿中的Nox顶住(它们在Ground层,而影穿只穿影墙不穿地面)。
    /// 只删和玩家身体等高的那些,头顶上方的平台保留。
    /// </summary>
    private static void ClearCorridor(float minX, float maxX, float floorY)
    {
        const float bodyHeight = 2.2f;
        int removed = 0;
        foreach (var col in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (col == null || !col.gameObject.name.StartsWith("Platform_")) continue;

            var b = col.bounds;
            bool inCorridor = b.max.x > minX && b.min.x < maxX
                && b.max.y > floorY && b.min.y < floorY + bodyHeight;
            if (inCorridor)
            {
                Debug.Log($"[CoopLevel] Removed corridor blocker '{col.gameObject.name}' at {b.center}");
                Object.DestroyImmediate(col.gameObject);
                removed++;
            }
        }
        if (removed > 0) Debug.Log($"[CoopLevel] Cleared {removed} blockers from the co-op corridor");
    }

    /// <summary>清掉指定x区间内的模板敌人(推箱谜题的走廊里不能有会动的东西)</summary>
    private static void ClearEnemies(float minX, float maxX)
    {
        int removed = 0;
        foreach (var e in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            if (e == null) continue;
            float x = e.transform.position.x;
            if (x < minX || x > maxX) continue;
            Debug.Log($"[CoopLevel] Removed enemy '{e.name}' at x={x:F1} from the puzzle lane");
            Object.DestroyImmediate(e.gameObject);
            removed++;
        }
        if (removed > 0) Debug.Log($"[CoopLevel] Cleared {removed} enemies from the puzzle lane");
    }

    /// <summary>重复运行时先清掉上一次生成的对象,保证幂等</summary>
    private static void ClearPreviousBuild()
    {
        int removed = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && go.name.StartsWith(Prefix))
            {
                Object.DestroyImmediate(go);
                removed++;
            }
        }
        if (removed > 0) Debug.Log($"[CoopLevel] Removed {removed} objects from previous build");
    }

    /// <summary>地面不够长就拉长,否则关卡后半段没有立足点</summary>
    private static void EnsureGroundSpans(GameObject ground, float minX, float maxX)
    {
        float halfW = ground.transform.localScale.x * 0.5f;
        float left = Mathf.Min(ground.transform.position.x - halfW, minX);
        float right = Mathf.Max(ground.transform.position.x + halfW, maxX);

        var scale = ground.transform.localScale;
        scale.x = right - left;
        ground.transform.localScale = scale;
        ground.transform.position = new Vector3((left + right) * 0.5f,
            ground.transform.position.y, ground.transform.position.z);
    }

    private static GameObject CreateBlock(string name, Vector3 pos, Vector3 scale, Color color,
        string spriteName, Transform parent, bool circleCollider)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = scale;
        if (parent != null) go.transform.SetParent(parent, true);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.color = color;
        sr.sortingOrder = 2;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + spriteName + ".png");
        if (sprite != null) sr.sprite = sprite;

        if (circleCollider) go.AddComponent<CircleCollider2D>();
        else go.AddComponent<BoxCollider2D>();
        return go;
    }
}
