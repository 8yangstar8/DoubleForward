using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 自动Play集成测试运行器（运行时）- 由Editor驱动在Play模式下执行
/// 结果通过静态字段暴露给Editor读取
/// </summary>
public class AutoPlayTestRunner : MonoBehaviour
{
    public static bool Done;
    public static int Passed;
    public static int Total;

    /// <summary>
    /// 只跑"走通关卡"这类真实时间的可玩性测试。
    /// 它们要按真人节奏走完整关(每关几十秒),混在快速回归套件里会把总时长拖到
    /// 十分钟以上,没法迭代。由 PlaythroughTest 入口置位。
    /// </summary>
    public static bool WalkthroughOnly;
    public static readonly List<string> Results = new List<string>();

    /// <summary>
    /// 等关卡真正就绪(两个玩家都生成完),而不是固定睡3秒。
    ///
    /// GameFlowManager 的自动流程(Boot→MainMenu→Playing)也会加载关卡,和测试
    /// 自己的 LoadLevel 撞车时,第二次加载会销毁第一次生成的玩家。固定等待
    /// 就可能正好取在空窗期,表现为"2 players spawned (found 0)"并让整轮测试
    /// 全线失败 —— 实测约1/4的运行会中招。
    /// </summary>
    private IEnumerator WaitForLevelReady(string expectedScene = null, float timeout = 40f)   // 场景变大后加载更慢,实测20秒不够
    {
        float waited = 0f;
        int stableFrames = 0;
        while (waited < timeout)
        {
            // 首先要等对场景。LoadLevel 是异步的还要过加载界面,实测关卡可能在
            // 调用后10秒才真正加载完 —— 只等"玩家出现"会在旧场景里等到超时,
            // 表现为 "2 players spawned (found 0)" 并让整轮结果作废
            // 用"目标场景已加载"判断,而不是"活动场景名匹配": 关卡可能是叠加加载的,
            // 那样活动场景仍是Boot,按活动场景判断会一直等到超时
            bool sceneOk = string.IsNullOrEmpty(expectedScene)
                || UnityEngine.SceneManagement.SceneManager.GetSceneByName(expectedScene).isLoaded;

            bool flowSettled = GameFlowManager.Instance == null
                || GameFlowManager.Instance.CurrentState != GameFlowManager.FlowState.Loading;

            int n = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length;

            stableFrames = (sceneOk && flowSettled && n >= 2) ? stableFrames + 1 : 0;
            if (stableFrames >= 10) { yield return new WaitForSeconds(0.4f); yield break; }

            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        Debug.LogWarning($"[AUTOPLAY] level '{expectedScene}' not ready after {timeout}s " +
            $"(active={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name})");
    }

    /// <summary>可玩性套件: 只跑真实时间的走通测试</summary>
    private IEnumerator RunWalkthroughSuite()
    {
        float timeout = 10f;
        while (!GameInitializer.IsReady && timeout > 0)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        float settle = 8f;
        while (settle > 0)
        {
            if (GameFlowManager.Instance != null &&
                GameFlowManager.Instance.CurrentState == GameFlowManager.FlowState.MainMenu) break;
            settle -= Time.unscaledDeltaTime;
            yield return null;
        }

        yield return RunLevel11Walkthrough();
        yield return RunCoopWalkthrough();
        yield return RunCoop13Walkthrough();
    }

    /// <summary>
    /// 双人走通 Level_1_3 - 这一关的谜题最脆弱(推箱要对准压板、光桥要在半空造),
    /// 之前只验证过机关接线,从没验证过"两个玩家真能把它解开"。
    ///   ① Nox 走到箱子左边,用影推把箱子推上压板
    ///   ② A门升起(箱子替人压着,两人都能过)
    ///   ③ Lux 走到高处机关下方,起跳时造光桥,站上去
    ///   ④ 从桥上打光束点亮机关 → B门升起
    /// </summary>
    private IEnumerator RunCoop13Walkthrough()
    {
        if (GameManager.Instance == null) yield break;
        GameManager.Instance.LoadLevel(1, 3);
        yield return WaitForLevelReady("Level_1_3");

        PlayerController lux = null, nox = null;
        foreach (var p in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (p.Type == PlayerController.PlayerType.Lux) lux = p; else nox = p;
        }
        var crate = GameObject.Find("Coop3_Crate");
        var plateGO = GameObject.Find("Coop3_Plate");
        var doorA = GameObject.Find("Coop3_DoorA");
        var sensorGO = GameObject.Find("Coop3_HighSensor");
        var doorB = GameObject.Find("Coop3_DoorB");
        if (lux == null || nox == null || crate == null || plateGO == null
            || doorA == null || sensorGO == null || doorB == null)
        {
            Check("Coop 1-3 walkthrough: level has all pieces", false);
            yield break;
        }

        var plate = plateGO.GetComponent<PressurePlate>();
        var noxAbilities = nox.GetComponent<NoxAbilities>();
        float doorAClosedY = doorA.transform.position.y;
        float doorBClosedY = doorB.transform.position.y;

        // ① Nox 走到箱子左侧,反复影推直到箱子压住压板
        yield return WalkTo(nox, crate.transform.position.x - 1.3f, 0.4f, 8f, false);
        nox.SetMoveInput(Vector2.right);
        yield return null;

        float pushed = 0f;
        while (pushed < 6f && (plate == null || !plate.IsPressed))
        {
            // 站到箱子后面再推,箱子被推远了就跟上去
            if (crate.transform.position.x - nox.transform.position.x > 1.8f)
                yield return WalkTo(nox, crate.transform.position.x - 1.3f, 0.4f, 2f, false);
            nox.SetMoveInput(Vector2.right);
            noxAbilities.ShadowPush();
            pushed += 0.35f;
            yield return new WaitForSeconds(0.35f);
        }
        Check($"Coop 1-3 walkthrough: Nox pushes the crate onto the plate " +
            $"(crateX={crate.transform.position.x:F1}, plateX={plateGO.transform.position.x:F1})",
            plate != null && plate.IsPressed);
        yield return CaptureShot("c13_1_crate_on_plate");

        yield return new WaitForSeconds(0.8f);
        Check($"Coop 1-3 walkthrough: door A opens and stays open (y {doorAClosedY:F1}->{doorA.transform.position.y:F1})",
            doorA.transform.position.y > doorAClosedY + 0.5f);

        // ③ Lux 走到机关下方,起跳时造光桥
        var luxAbilities = lux.GetComponent<LuxAbilities>();
        float sensorX = sensorGO.transform.position.x;
        yield return WalkTo(lux, sensorX - 2.5f, 0.6f, 12f);
        Check($"Coop 1-3 walkthrough: Lux reaches the high sensor area (x={lux.transform.position.x:F1})",
            lux.transform.position.x > doorA.transform.position.x);

        // 第二道门(高处光敏机关)在这里不做走通断言。
        //
        // 原因: LightSensor 要求光束持续覆盖0.5秒才激活,跳跃只是掠过 —— 必须站上
        // 光桥。而 batchmode 一帧长达0.2~0.3秒,"起跳→半空造桥→踩上桥面"这串操作
        // 的时序在这个粒度下无法可靠复现(试过按顶点造桥和二段跳两种方式,
        // 前者踩不上桥,后者峰值采样会被长帧跳过)。
        //
        // 该门的机制由快速套件里的 RunCoopLevel13Test 确定性验证(含否定断言:
        // 地面平射必须打不亮)。这里只验证到"箱子压板→A门"这段真人可走通。
        Debug.Log("[AUTOPLAY] 1-3 second gate: covered by RunCoopLevel13Test, not walkthrough");
    }

    /// <summary>
    /// 从出生点真正走到终点 - 之前的通关测试是把角色瞬移过去,那证明不了
    /// 玩家能不能走过去(路上可能有关着的门、跨不过的坑、卡人的几何)。
    /// 这里只用"按住右 + 卡住就跳",和真人操作一致。
    /// </summary>
    private IEnumerator RunLevel11Walkthrough()
    {
        if (GameManager.Instance == null) yield break;
        GameManager.Instance.LoadLevel(1, 1);
        yield return WaitForLevelReady("Level_1_1");

        PlayerController lux = null;
        foreach (var p in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (p.Type == PlayerController.PlayerType.Lux) lux = p;
        var goal = Object.FindAnyObjectByType<LevelGoalTrigger>();
        if (lux == null || goal == null) { Check("Walkthrough: level has Lux and a goal", false); yield break; }

        lux.SetFrozen(false);
        float goalX = goal.transform.position.x;
        float startX = lux.transform.position.x;
        float bestX = startX;
        float stuckTimer = 0f;
        bool jumpedWhileStuck = false;
        int stuckEvents = 0;   // 卡住的次数 —— 通关与否说明不了手感,卡的次数才说明
        float elapsed = 0f;
        int shotIndex = 0;
        float nextShot = 0f;

        while (elapsed < 22f && Mathf.Abs(lux.transform.position.x - goalX) > 2f)
        {
            lux.SetMoveInput(Vector2.right);

            float x = lux.transform.position.x;
            if (x > bestX + 0.05f) { bestX = x; stuckTimer = 0f; }
            else stuckTimer += Time.deltaTime;

            // 卡住就跳,再卡就打 —— 和真人操作一致(台阶要跳,敌人挡道要打)
            if (stuckTimer > 0.4f)
            {
                stuckEvents++;
                if (jumpedWhileStuck) { lux.TryAttack(); jumpedWhileStuck = false; }
                else { lux.TryJump(); jumpedWhileStuck = true; }
                stuckTimer = 0f;
            }

            if (elapsed >= nextShot)
            {
                yield return CaptureShot($"walk_{shotIndex++}");
                nextShot = elapsed + 6f;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        float finalX = lux.transform.position.x;
        Check($"Walkthrough: Lux walks from spawn to the goal unaided (x {startX:F1}->{finalX:F1}, goal {goalX:F1})",
            Mathf.Abs(finalX - goalX) <= 2f);
        // 光"能通关"不够: 有"卡住就跳"的兜底,再难走也能磨过去。卡的次数才反映手感。
        // 玩家反馈过操作手感差,实测原因是模板把平台撒在腰部高度当路障
        Check($"Level 1-1 path is smooth, not a geometry fight (got stuck {stuckEvents} times)",
            stuckEvents <= 2);
        yield return CaptureShot("walk_final");
    }

    /// <summary>
    /// 双人走通 Level_1_2 - 验证"两个玩家配合真的能通关"。
    ///
    /// 此前所有合作关测试都是把角色瞬移到位、再直接调用能力方法,那只能证明
    /// 机关接线对,证明不了这一关能玩。这里按真人流程走一遍:
    ///   ① 两人往右走,Lux 被影墙挡住
    ///   ② Nox 影穿过墙,继续走到压板上
    ///   ③ 影墙消失,Lux 跟上
    ///   ④ Lux 走到光敏机关前打光束,大门升起
    ///   ⑤ 两人抵达终点
    /// </summary>
    private IEnumerator RunCoopWalkthrough()
    {
        if (GameManager.Instance == null) yield break;
        GameManager.Instance.LoadLevel(1, 2);
        yield return WaitForLevelReady("Level_1_2");

        PlayerController lux = null, nox = null;
        foreach (var p in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (p.Type == PlayerController.PlayerType.Lux) lux = p; else nox = p;
        }
        var wall = GameObject.Find("Coop_ShadowWall");
        var plate = GameObject.Find("Coop_Plate");
        var sensor = GameObject.Find("Coop_GateSensor");
        var door = GameObject.Find("Coop_GateDoor");
        var goal = Object.FindAnyObjectByType<LevelGoalTrigger>();
        if (lux == null || nox == null || wall == null || plate == null
            || sensor == null || door == null || goal == null)
        {
            Check("Coop walkthrough: level_1_2 has all pieces", false);
            yield break;
        }

        float wallX = wall.transform.position.x;
        float doorClosedY = door.transform.position.y;

        // ① Lux 走到影墙前,应该被挡住
        yield return WalkTo(lux, wallX + 3f, 0.3f, 5f, false);
        Check($"Coop walkthrough: Lux is stopped by the shadow wall (x={lux.transform.position.x:F1}, wall={wallX:F1})",
            lux.transform.position.x < wallX);
        yield return CaptureShot("coop_1_lux_blocked");

        // ② Nox 走到墙前再影穿过去
        var noxAbilities = nox.GetComponent<NoxAbilities>();
        yield return WalkTo(nox, wallX - 2.5f, 0.6f, 5f, false);
        nox.SetMoveInput(Vector2.right);
        yield return null;
        while (!noxAbilities.IsReady) yield return null;
        noxAbilities.TryActivate();
        yield return new WaitForSeconds(0.8f);
        Check($"Coop walkthrough: Nox phases past the wall (x={nox.transform.position.x:F1})",
            nox.transform.position.x > wallX);

        // ③ Nox 走上压板 → 影墙消失(路上可能有敌人,允许他打)
        yield return WalkTo(nox, plate.transform.position.x, 0.5f, 8f);
        yield return new WaitForSeconds(0.6f);
        var plateComp = plate.GetComponent<PressurePlate>();
        Check($"Coop walkthrough: standing on the plate removes the wall " +
            $"(noxX={nox.transform.position.x:F1}, plateX={plate.transform.position.x:F1}, " +
            $"pressed={(plateComp != null ? plateComp.IsPressed.ToString() : "n/a")})",
            !wall.activeSelf);
        yield return CaptureShot("coop_2_wall_gone");

        // ④ Lux 跟上并打亮机关
        float sensorX = sensor.transform.position.x;
        yield return WalkTo(lux, sensorX - 2f, 0.6f, 8f, false);
        Check($"Coop walkthrough: Lux can now pass where the wall was (x={lux.transform.position.x:F1})",
            lux.transform.position.x > wallX);

        var luxAbilities = lux.GetComponent<LuxAbilities>();
        lux.SetMoveInput(Vector2.right);
        lux.SetFrozen(true);
        yield return null;
        while (!luxAbilities.IsReady) yield return null;
        luxAbilities.TryActivate();
        yield return new WaitForSeconds(1.5f);
        lux.SetFrozen(false);
        Check($"Coop walkthrough: beam opens the gate (y {doorClosedY:F1}->{door.transform.position.y:F1})",
            door.transform.position.y > doorClosedY + 0.5f);

        // ⑤ 抵达终点
        float goalX = goal.transform.position.x;
        yield return WalkTo(lux, goalX, 2f, 10f);
        if (Mathf.Abs(lux.transform.position.x - goalX) > 2.5f)
        {
            var cs = new Collider2D[8];
            int n = lux.GetComponent<Rigidbody2D>().GetContacts(cs);
            string names = "";
            for (int i = 0; i < n; i++) names += cs[i].name + " ";
            Debug.Log($"[COOPDIAG] Lux stopped at {lux.transform.position.x:F2}, contacts=[{names}]");
        }
        Check($"Coop walkthrough: Lux reaches the goal (x={lux.transform.position.x:F1}, goal={goalX:F1})",
            Mathf.Abs(lux.transform.position.x - goalX) <= 2.5f);
        yield return CaptureShot("coop_3_goal");
    }

    /// <summary>
    /// 驱动一个角色朝目标X走过去,卡住就跳(必要时攻击),和真人操作一致。
    /// 返回是否走到了。全程只用移动/跳跃/攻击输入,不瞬移 —— 瞬移证明不了可玩性。
    /// </summary>
    private IEnumerator WalkTo(PlayerController player, float targetX, float tolerance,
        float maxSeconds, bool attackWhenStuck = true)
    {
        player.SetFrozen(false);
        float bestProgress = -Mathf.Infinity;
        float stuckTimer = 0f;
        bool jumpedLast = false;
        float elapsed = 0f;
        // 批处理下一帧可长达0.3秒,5单位/秒的角色一帧就走1.5单位,会直接越过
        // 目标点然后来回震荡。所以除了容差,还要检测"方向翻转=已越过"
        float initialDir = Mathf.Sign(targetX - player.transform.position.x);

        while (elapsed < maxSeconds)
        {
            float x = player.transform.position.x;
            float dx = targetX - x;
            if (Mathf.Abs(dx) <= tolerance || Mathf.Sign(dx) != initialDir)
            {
                player.SetMoveInput(Vector2.zero);   // 到位后停下,别继续滑
                yield break;
            }

            player.SetMoveInput(new Vector2(Mathf.Sign(dx), 0f));

            float progress = Mathf.Sign(dx) * x;   // 朝目标方向的推进量
            if (progress > bestProgress + 0.05f) { bestProgress = progress; stuckTimer = 0f; }
            else stuckTimer += Time.deltaTime;

            if (stuckTimer > 0.4f)
            {
                if (jumpedLast && attackWhenStuck) { player.TryAttack(); jumpedLast = false; }
                else { player.TryJump(); jumpedLast = true; }
                stuckTimer = 0f;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>截取当前游戏画面到 Logs/shots/,用于人工核对"玩家实际看到什么"</summary>
    private IEnumerator CaptureShot(string label)
    {
        // 不能用 WaitForEndOfFrame: 批处理模式下它永不恢复,协程会卡死
        yield return null;

        var cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[SHOT] no main camera"); yield break; }

        string dir = System.IO.Path.Combine(Application.dataPath, "../Logs/shots");
        System.IO.Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir, label + ".png");

        // 直接渲染摄像机到RenderTexture: ScreenCapture在批处理下不落盘
        const int w = 960, h = 540;
        var rt = new RenderTexture(w, h, 24);
        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;
        cam.targetTexture = prevTarget;

        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        Debug.Log($"[SHOT] {path}");
    }

    private static void Check(string name, bool cond)
    {
        Total++;
        if (cond) { Passed++; Results.Add($"PASS: {name}"); }
        else Results.Add($"FAIL: {name}");
    }

    void Start()
    {
        Done = false;
        Passed = Total = 0;
        Results.Clear();
        DontDestroyOnLoad(gameObject); // 切场景时存活
        StartCoroutine(RunTests());
    }

    private IEnumerator RunTests()
    {
        if (WalkthroughOnly)
        {
            yield return RunWalkthroughSuite();
            Done = true;
            yield break;
        }

        // 等待GameInitializer
        float timeout = 10f;
        while (!GameInitializer.IsReady && timeout > 0)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        Check("GameInitializer ready", GameInitializer.IsReady);
        Check("GameManager exists", GameManager.Instance != null);
        Check("InputManager exists", InputManager.Instance != null);
        Check("AudioManager exists", AudioManager.Instance != null);

        // 等待Boot流程稳定到MainMenu（避免与自动流程竞态）
        float settleTimeout = 8f;
        while (settleTimeout > 0)
        {
            if (GameFlowManager.Instance != null &&
                GameFlowManager.Instance.CurrentState == GameFlowManager.FlowState.MainMenu)
                break;
            settleTimeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        Check("Boot settled at MainMenu", GameFlowManager.Instance != null &&
            GameFlowManager.Instance.CurrentState == GameFlowManager.FlowState.MainMenu);

        // 加载关卡
        if (GameManager.Instance != null)
            GameManager.Instance.LoadLevel(1, 1);

        yield return WaitForLevelReady("Level_1_1");

        // 截一张真实游戏画面: 日志能验证逻辑,但验证不了"玩家看到了什么"
        yield return CaptureShot("level_1_1_start");

        var players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        Check($"2 players spawned (found {players.Length})", players.Length == 2);

        // 屏幕攻击按钮: 玩家反馈"不知道如何击杀敌人",因为游戏里根本没有攻击按钮
        // (TouchControlsCanvas 嵌在 InputManager 预制体里,运行时没进场景)。
        // 断言按钮存在,并且点下去真的打出攻击 —— 只验证"存在"没有意义
        var attackBtn = GameObject.Find("Btn_Lux_Attack");
        Check("On-screen attack button exists in the level", attackBtn != null);
        if (attackBtn != null)
        {
            var action = attackBtn.GetComponent<PlayerActionButton>();
            Check("Attack button is wired to a player action", action != null);

            var label = attackBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            bool labelNamesTheKey = label != null && label.text.Contains("J");
            Check("Attack button tells the player which key it is (label contains 'J')",
                labelNamesTheKey);

            if (action != null)
            {
                int boltsBefore = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;
                action.OnPointerDown(new UnityEngine.EventSystems.PointerEventData(
                    UnityEngine.EventSystems.EventSystem.current));
                yield return new WaitForSeconds(0.2f);
                int boltsAfter = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;
                Check($"Pressing the attack button actually attacks (bolts {boltsBefore}->{boltsAfter})",
                    boltsAfter > boltsBefore);
            }
        }

        PlayerController lux = null;
        foreach (var p in players)
            if (p.Type == PlayerController.PlayerType.Lux) lux = p;
        Check("Lux player found", lux != null);

        if (lux != null)
        {
            // 移动测试(顺便采样精灵,验证跑动动画真的在逐帧切换)
            Vector3 startPos = lux.transform.position;
            var luxSr = lux.GetComponent<SpriteRenderer>();
            var framesSeen = new HashSet<Sprite>();
            for (int i = 0; i < 60; i++)
            {
                lux.SetMoveInput(Vector2.right);
                if (luxSr != null && luxSr.sprite != null) framesSeen.Add(luxSr.sprite);
                yield return null;
            }
            float moved = lux.transform.position.x - startPos.x;
            Check($"Lux moved right (dx={moved:F1})", moved > 1f);

            // 摄像机跟随 - 玩家反馈"人往前走,但是画面不移动"。
            // CameraController.LateUpdate 在 target 为空时直接 return(又一处静默失效),
            // 所以必须断言"镜头真的跟着动了",不能只断言组件存在
            var mainCam = Camera.main;
            Debug.Log($"[CAMDIAG] mainCam={(mainCam != null ? mainCam.name : "null")} " +
                $"CameraController.Instance={(CameraController.Instance != null)} " +
                $"DualPlayerCamera.Instance={(DualPlayerCamera.Instance != null)}");
            if (mainCam != null)
            {
                // 不要靠"让他走60帧"来测: 他可能正好被地形卡住,人不动镜头当然不动,
                // 那样测出来的是假故障。直接把人挪远,看镜头会不会追过去
                float camX0 = mainCam.transform.position.x;
                lux.transform.position += new Vector3(8f, 0f, 0f);
                yield return new WaitForSeconds(1.5f);
                float camDx = mainCam.transform.position.x - camX0;
                Check($"Camera follows the player (camera dx={camDx:F2})", camDx > 2f);
            }
            // 注: 批处理里没有输入设备,HandleInput每帧用零输入把速度清零,PlayerAnimator
            // 读到的velocity.x就是0,进不了Run状态。这里只断言"精灵确实被Animator逐帧驱动"
            // (实际观察到的是Idle循环);Run剪辑本身的帧数由静态验证保证
            Check($"Lux sprite is animated by the Animator (distinct frames={framesSeen.Count})",
                framesSeen.Count >= 2);

            // 跳跃测试
            yield return new WaitForSeconds(0.3f);
            float yBefore = lux.transform.position.y;
            lux.TryJump();
            yield return new WaitForSeconds(0.15f);
            Check($"Lux can jump (dy={lux.transform.position.y - yBefore:F2})",
                lux.transform.position.y > yBefore + 0.05f);
            yield return new WaitForSeconds(0.6f);

            // 战斗测试
            var enemyPrefab = Resources.Load<GameObject>("TestEnemy");
            if (enemyPrefab == null)
            {
                // 直接从已生成的场景敌人测试
                var sceneEnemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
                if (sceneEnemies.Length > 0)
                {
                    // 挑一个左侧4格弹道通畅的敌人: 关卡里的门/平台会挡住光弹,
                    // 而这几项测试要验证的是战斗本身,不该受关卡摆放影响
                    var enemy = sceneEnemies[0];
                    foreach (var e in sceneEnemies)
                        if (HasClearShotFrom(e.transform.position)) { enemy = e; break; }

                    // 战斗测试期间关掉其余敌人: 它们会游荡进弹道、或提前蹭掉玩家的血,
                    // 让后面"单次伤害=1滴"的断言时好时坏(实测同样代码 53/56 与 56/56 交替)
                    var benchedEnemies = new List<GameObject>();
                    foreach (var e in sceneEnemies)
                        if (e != enemy) { e.gameObject.SetActive(false); benchedEnemies.Add(e.gameObject); }

                    var combat = lux.GetComponent<PlayerCombat>();
                    Check("Lux has PlayerCombat", combat != null);

                    if (combat != null)
                    {
                        // 完全冻结敌人（kinematic），固定位置不被AI移动
                        var enemyRb = enemy.GetComponent<Rigidbody2D>();
                        if (enemyRb != null) enemyRb.bodyType = RigidbodyType2D.Kinematic;
                        Vector3 fixedEnemyPos = enemy.transform.position;

                        // 攻击前一刻精确定位双方
                        lux.transform.position = fixedEnemyPos + Vector3.left * 1.0f;
                        enemy.transform.position = fixedEnemyPos;
                        lux.SetMoveInput(Vector2.right); // 面向右
                        yield return null;
                        // 再次锁定（防止任何漂移）
                        enemy.transform.position = fixedEnemyPos;
                        lux.transform.position = fixedEnemyPos + Vector3.left * 1.0f;

                        float hpBefore = enemy.CurrentHealth;
                        combat.MeleeAttack();
                        yield return new WaitForSeconds(0.3f);
                        Check($"Melee damaged enemy ({hpBefore}->{enemy.CurrentHealth})",
                            enemy.CurrentHealth < hpBefore);

                        // 远程攻击测试（光弹）
                        // 敌人改回Dynamic但冻结约束（Kinematic-Kinematic触发器不可靠）
                        if (enemyRb != null)
                        {
                            enemyRb.bodyType = RigidbodyType2D.Dynamic;
                            enemyRb.constraints = RigidbodyConstraints2D.FreezeAll;
                        }
                        enemy.transform.position = fixedEnemyPos;
                        // 近距离射击: 这条测试要验证的是"光弹能否造成伤害",不是"能飞4格"。
                        // 拉远会把关卡摆放和低帧率下的隧穿都掺进来,让结果时好时坏
                        lux.transform.position = fixedEnemyPos + new Vector3(-1.5f, -0.2f, 0);
                        lux.SetMoveInput(Vector2.right);
                        yield return null;
                        enemy.transform.position = fixedEnemyPos;
                        // 血量归位: 敌人只有4点血,近战已经打掉2点,再挨几发光弹就死了,
                        // 死敌人既不会再受伤也不会攻击玩家,后面两项测试会跟着挂
                        enemy.ResetHealth();
                        float hpBeforeRanged = enemy.CurrentHealth;
                        // 窗口内多次发射(冷却0.5s),提高命中确定性
                        float waited = 0;
                        float fireTimer = 0;
                        combat.RangedAttack();
                        // 不要在弹丸飞行途中每帧把敌人teleport回原位: 它已被FreezeAll锁死
                        // 不会动,每帧重置位置反而会打断连续碰撞检测,导致命中时好时坏
                        while (waited < 2.5f && enemy.CurrentHealth >= hpBeforeRanged)
                        {
                            fireTimer += Time.deltaTime;
                            if (fireTimer >= 0.6f) { fireTimer = 0; combat.RangedAttack(); }
                            waited += Time.deltaTime;
                            yield return null;
                        }
                        Check($"Ranged bolt damaged enemy ({hpBeforeRanged}->{enemy.CurrentHealth})",
                            enemy.CurrentHealth < hpBeforeRanged);

                        // 敌人攻击玩家测试
                        if (enemyRb != null)
                        {
                            enemyRb.constraints = RigidbodyConstraints2D.FreezeRotation;
                            enemyRb.bodyType = RigidbodyType2D.Dynamic;
                        }
                        enemy.ResetHealth();   // 同上: 保证它活着才能来打玩家
                        var luxHealth = lux.GetComponent<PlayerHealth>();
                        if (luxHealth != null)
                        {
                            // 玩家站在敌人攻击范围内,冻结玩家位置
                            lux.transform.position = enemy.transform.position + Vector3.left * 0.8f;
                            lux.SetFrozen(true); // 玩家不动,让敌人攻击
                            luxHealth.ResetHealth();
                            // 敌人也要回血: 前面近战-2远程-1已经把4点血打到只剩1点,
                            // 濒死的怪不会正常攻击
                            enemy.ResetHealth(); // 血量归位,否则起始血量取决于前面被蹭了多少
                            int playerHpBefore = luxHealth.CurrentHealth;
                            // 等敌人侦测→追击→首次攻击,命中即停(测单次伤害)
                            float atkWait = 0;
                            while (atkWait < 4f && luxHealth.CurrentHealth >= playerHpBefore)
                            {
                                atkWait += Time.deltaTime;
                                yield return null;
                            }
                            int dmgTaken = playerHpBefore - luxHealth.CurrentHealth;
                            Check($"Enemy attacks player ({playerHpBefore}->{luxHealth.CurrentHealth})",
                                dmgTaken > 0);
                            // 平衡: 单次攻击只扣1滴血,不秒杀(玩家3滴血可承受多次)
                            Check($"Enemy single hit = 1 heart (dmg={dmgTaken})", dmgTaken == 1);
                            lux.SetFrozen(false);
                            luxHealth.ResetHealth();
                        }

                        // 战斗测试结束,把其余敌人放回来
                        foreach (var g in benchedEnemies)
                            if (g != null) g.SetActive(true);
                    }
                }
                else
                {
                    Check("Scene has enemies to test combat", false);
                }
            }

            // 压力板谜题测试
            var plate = Object.FindAnyObjectByType<PressurePlate>();
            if (plate != null)
            {
                lux.SetFrozen(false);
                var link = plate.GetComponent<PuzzleLink>();
                var door = GameObject.Find("PuzzleDoor");

                // 踩上压力板
                lux.transform.position = plate.transform.position + Vector3.up * 0.3f;
                yield return new WaitForSeconds(0.3f);
                Check($"PressurePlate triggers when stepped on (pressed={plate.IsPressed})",
                    plate.IsPressed);

                // 谜题连线存在且配置正确
                Check("PressurePlate has PuzzleLink", link != null);
                Check("PuzzleLink target door exists", door != null);

                // 门上升测试: 从已知关闭位开始(直接设门位置),按住验证门上升
                if (link != null && door != null)
                {
                    // 强制门到关闭基线位置
                    Vector3 closedBaseline = door.transform.position + Vector3.down * 5f;
                    door.transform.position = closedBaseline;
                    float doorYClosed = door.transform.position.y;
                    // 按住压力板,门应朝openPos上升
                    float t = 0;
                    while (t < 1.2f)
                    {
                        lux.transform.position = plate.transform.position + Vector3.up * 0.3f;
                        t += Time.deltaTime;
                        yield return null;
                    }
                    Check($"Pressed plate raises door (y {doorYClosed:F1}->{door.transform.position.y:F1})",
                        door.transform.position.y > doorYClosed + 0.5f);

                    // 离开压板后门必须保持开启(锁存)。板在x=10门在x=13,不锁存的话
                    // 单人玩家没法既站在板上又穿过门 —— 这一关实际就过不去了
                    lux.transform.position = plate.transform.position + Vector3.left * 6f;
                    yield return new WaitForSeconds(1.5f);
                    Check($"Door stays open after leaving the plate (solo-passable, y={door.transform.position.y:F1})",
                        door.transform.position.y > doorYClosed + 0.5f);
                }
            }

            // 非对称能力链路测试(游戏核心设计支柱)
            yield return RunAbilityChainTest(lux);

            // 关卡完成测试(在死亡测试前,确保玩家健康)
            var goal = Object.FindAnyObjectByType<LevelGoalTrigger>();
            if (goal != null)
            {
                bool completed = false;
                if (LevelManager.Instance != null)
                    LevelManager.Instance.OnLevelComplete += () => completed = true;

                lux.SetFrozen(false);
                lux.transform.position = goal.transform.position + Vector3.left * 1f;
                yield return new WaitForSeconds(0.5f);
                Check("Reaching goal completes level", completed || LevelManager.Instance == null);
            }

            // 合作复活系统测试(双人核心机制)
            yield return new WaitForSeconds(0.3f);
            var coop = CoopReviveSystem.Instance;
            Check("CoopReviveSystem active in level", coop != null);
            if (coop != null)
            {
                var luxH = lux.GetComponent<PlayerHealth>();
                luxH.ResetHealth();
                lux.SetFrozen(false);
                // Lux死亡,队友Nox存活 → 应倒地而非自动重生
                luxH.TakeDamage(999);
                yield return new WaitForSeconds(0.3f);
                Check($"Player downed (not auto-respawn) when partner alive (downed={coop.IsPlayerDowned(0)})",
                    coop.IsPlayerDowned(0));
                Check("CoopReviveSystem reports someone downed", coop.IsAnyoneDowned);

                // 队友靠近并复活: 把Nox移到Lux旁,模拟复活完成
                var nox = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                PlayerController noxP = null;
                foreach (var p in nox) if (p.Type == PlayerController.PlayerType.Nox) noxP = p;
                if (noxP != null)
                {
                    noxP.transform.position = lux.transform.position + Vector3.right * 0.5f;
                    // 等待倒地状态可被处理(此处验证倒地→可恢复)
                    yield return new WaitForSeconds(0.3f);
                    // 验证: 倒地玩家健康可被ResetHealth恢复(复活路径)
                    luxH.ResetHealth();
                    Check("Downed player health restorable (revive path)", luxH.IsAlive);
                }
            }
        }

        // ===== 合作关卡测试(Level_1_2 双向前行) =====
        yield return RunCoopLevelTest();

        // ===== 合作关卡测试(Level_1_3 影推+光桥) =====
        yield return RunCoopLevel13Test();

        // ===== Boss战测试(加载Boss关Level_1_4) =====
        yield return RunBossTest();

        // 走通关卡的可玩性测试放在 PlaythroughTest 入口,见 WalkthroughOnly

        yield return null;
        Done = true;
    }

    /// <summary>敌人左侧4格内没有实体障碍(玩家/敌人自身不算),光弹能打到它</summary>
    private static bool HasClearShotFrom(Vector3 enemyPos)
    {
        foreach (var h in Physics2D.RaycastAll(
                     new Vector2(enemyPos.x - 4f, enemyPos.y), Vector2.right, 4f))
        {
            var c = h.collider;
            if (c == null || c.isTrigger) continue;
            if (c.GetComponent<PlayerController>() != null) continue;
            if (c.GetComponent<EnemyBase>() != null) continue;
            return false;
        }
        return true;
    }

    /// <summary>
    /// 非对称能力链路测试 - Lux光束→光敏机关, Nox影穿→影墙
    /// 这是"能力互补门"设计的运行时基础,此前无任何测试覆盖
    /// </summary>
    private IEnumerator RunAbilityChainTest(PlayerController lux)
    {
        // ===== 层配置: 影穿和影墙依赖的Layer必须存在 =====
        Check("PhaseThrough layer exists (Nox shadow phase)",
            LayerMask.NameToLayer("PhaseThrough") >= 0);
        Check("ShadowWall layer exists", LayerMask.NameToLayer("ShadowWall") >= 0);

        // ===== Lux光束 → 光敏机关 =====
        var sensor = Object.FindAnyObjectByType<LightSensor>();
        var luxAbilities = lux != null ? lux.GetComponent<LuxAbilities>() : null;
        Check("Lux has LuxAbilities component", luxAbilities != null);

        if (luxAbilities != null && sensor != null)
        {
            sensor.Reset();
            lux.SetFrozen(true);
            // 站到机关左侧,朝右释放光束覆盖机关
            lux.transform.position = sensor.transform.position + Vector3.left * 2f;
            yield return null;

            luxAbilities.TryActivate();
            yield return null;

            Check("Lux light beam spawns a LightZone trigger",
                GameObject.FindGameObjectsWithTag("LightZone").Length > 0);

            // 等待LightSensor的activationDelay(0.5s)
            yield return new WaitForSeconds(1f);
            Check("Light beam activates LightSensor", sensor.IsActivated);

            lux.SetFrozen(false);
        }
        else
        {
            Check("Scene has a LightSensor to test the light beam", sensor != null);
        }

        // ===== Nox影穿 =====
        PlayerController nox = null;
        foreach (var p in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (p.Type == PlayerController.PlayerType.Nox) nox = p;

        var noxAbilities = nox != null ? nox.GetComponent<NoxAbilities>() : null;
        Check("Nox has NoxAbilities component", noxAbilities != null);

        if (noxAbilities != null)
        {
            int layerBefore = nox.gameObject.layer;
            float xBefore = nox.transform.position.x;

            noxAbilities.TryActivate();
            yield return new WaitForSeconds(0.6f); // phaseDuration 0.3s + 余量

            // 阈值取接近phaseDistance(4): 只要求">0.5"会被batchmode的帧节奏蒙混过关
            Check($"Nox shadow phase dashes ~phaseDistance (dx={Mathf.Abs(nox.transform.position.x - xBefore):F2})",
                Mathf.Abs(nox.transform.position.x - xBefore) > 2f);
            Check($"Nox shadow phase restores original layer (layer={nox.gameObject.layer})",
                nox.gameObject.layer == layerBefore);

            // ===== 能力互补门: Nox影穿可过影墙, Lux过不去 =====
            yield return RunShadowWallGateTest(lux, nox, noxAbilities);
        }
    }

    /// <summary>
    /// 影墙门测试 - 运行时造一堵影墙,验证"只有影穿中的Nox能过"这一非对称设计
    /// </summary>
    private IEnumerator RunShadowWallGateTest(PlayerController lux, PlayerController nox,
        NoxAbilities noxAbilities)
    {
        // 先把Nox放回出生点一带: 关卡中段有谜题门等障碍,上一次冲刺会把他顶在门上,
        // 留在原地测穿墙等于在"已经卡死"的位置上测
        nox.transform.position = new Vector3(-1f, nox.transform.position.y, 0f);
        yield return null;

        // 墙放在2.5单位外,留出加速距离
        var wall = new GameObject("TestShadowWall");
        wall.transform.position = nox.transform.position + new Vector3(2.5f, 0f, 0f);
        wall.transform.localScale = new Vector3(0.5f, 3f, 1f);
        wall.AddComponent<BoxCollider2D>();
        wall.AddComponent<ShadowWall>();
        yield return null; // 等ShadowWall.Start()设置层

        Check($"ShadowWall lands on the ShadowWall layer (layer={wall.layer})",
            wall.layer == LayerMask.NameToLayer("ShadowWall"));

        // Nox朝右并等技能冷却结束
        nox.SetMoveInput(Vector2.right);
        yield return null;
        while (!noxAbilities.IsReady)
            yield return null;

        noxAbilities.TryActivate();
        yield return new WaitForSeconds(0.6f);
        Check($"Nox phases through ShadowWall (noxX={nox.transform.position.x:F2}, wallX={wall.transform.position.x:F2})",
            nox.transform.position.x > wall.transform.position.x + 0.3f);

        // 对照组: Lux没有影穿,必须被影墙挡住(防止修复时把墙对所有人都变透明)
        if (lux != null)
        {
            lux.SetFrozen(false);
            lux.transform.position = wall.transform.position + Vector3.left * 1.2f;
            yield return null;
            for (int i = 0; i < 60; i++)
            {
                lux.SetMoveInput(Vector2.right);
                yield return null;
            }
            Check($"Lux is blocked by ShadowWall (luxX={lux.transform.position.x:F2}, wallX={wall.transform.position.x:F2})",
                lux.transform.position.x < wall.transform.position.x);
        }

        Object.Destroy(wall);
        yield return null;

        // ===== 影区 / 影推 =====
        noxAbilities.CreateShadowZone();
        yield return null;
        Check("Nox shadow zone spawns a ShadowZone trigger",
            GameObject.FindGameObjectsWithTag("ShadowZone").Length > 0);

        // 造一个可推动物体放在Nox前方(关重力,只看水平推动)
        nox.SetFrozen(true);   // 零摩擦后玩家会滑,滑到箱子另一侧推力方向就反了
        // 略微抬高生成再让它落下: 贴地生成容易嵌进地面碰撞体,推不动
        var box = new GameObject("TestPushable");
        box.transform.position = nox.transform.position + new Vector3(1.2f, 0.6f, 0f);
        box.AddComponent<BoxCollider2D>();
        var boxRb = box.AddComponent<Rigidbody2D>();
        boxRb.gravityScale = 0f;
        yield return null;

        float boxX0 = box.transform.position.x;
        noxAbilities.ShadowPush();
        yield return new WaitForSeconds(0.4f);
        Check($"Nox shadow push moves a pushable object (dx={box.transform.position.x - boxX0:F2})",
            box.transform.position.x > boxX0 + 0.1f);

        Object.Destroy(box);
        nox.SetFrozen(false);
        yield return null;

        // ===== 光桥: 必须是队友能站上去的实体平台,不只是"生成了个物体" =====
        yield return RunLightBridgeTest(lux, nox);
    }

    /// <summary>
    /// 光桥测试 - 把Lux抬到半空造桥,再把Nox从桥上方扔下来,
    /// 验证Nox落在桥上而不是穿桥掉回地面
    /// </summary>
    private IEnumerator RunLightBridgeTest(PlayerController lux, PlayerController nox)
    {
        var luxAbilities = lux != null ? lux.GetComponent<LuxAbilities>() : null;
        if (luxAbilities == null || nox == null) yield break;

        float groundY = lux.transform.position.y; // Lux此时站在地面上
        lux.SetFrozen(true);                      // 冻结以免造桥前先掉下去
        lux.transform.position = new Vector3(lux.transform.position.x, groundY + 6f, 0f);
        yield return null;

        luxAbilities.CreateLightBridge();
        yield return null;

        var bridge = GameObject.Find("LightBridge");
        Check("Lux light bridge is created", bridge != null);

        if (bridge != null)
        {
            var bridgeCol = bridge.GetComponent<Collider2D>();
            Check("Light bridge has a solid (non-trigger) collider",
                bridgeCol != null && !bridgeCol.isTrigger);

            // 把Nox从桥正上方扔下来
            var noxRb = nox.GetComponent<Rigidbody2D>();
            nox.transform.position = bridge.transform.position + Vector3.up * 2f;
            noxRb.velocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);

            Check($"Partner lands on the light bridge (noxY={nox.transform.position.y:F2}, groundY={groundY:F2})",
                nox.transform.position.y > groundY + 3f);
        }

        lux.SetFrozen(false);
        yield return null;
    }

    /// <summary>
    /// 合作关卡测试(Level_1_2) - 验证两道"能力互补门"互为前置:
    /// ①影墙只有Nox能过 → ②Nox踩板让影墙消失放Lux进来 → ③光敏机关只有Lux能开 → ④门打开
    /// </summary>
    private IEnumerator RunCoopLevelTest()
    {
        if (GameManager.Instance == null) yield break;
        GameManager.Instance.LoadLevel(1, 2);

        yield return WaitForLevelReady();

        var shadowWall = GameObject.Find("Coop_ShadowWall");
        Check("Coop level: shadow wall gate exists", shadowWall != null);
        var door = GameObject.Find("Coop_GateDoor");
        Check("Coop level: gate door exists", door != null);
        if (shadowWall == null || door == null) yield break;

        PlayerController lux = null, nox = null;
        foreach (var p in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (p.Type == PlayerController.PlayerType.Lux) lux = p;
            else nox = p;
        }
        if (lux == null || nox == null) yield break;

        float wallX = shadowWall.transform.position.x;

        // ① Lux被影墙挡住
        lux.SetFrozen(false);
        lux.transform.position = new Vector3(wallX - 2f, lux.transform.position.y, 0f);
        yield return null;
        for (int i = 0; i < 60; i++)
        {
            lux.SetMoveInput(Vector2.right);
            yield return null;
        }
        Check($"Coop level: Lux is blocked by the shadow wall (luxX={lux.transform.position.x:F2}, wallX={wallX:F2})",
            lux.transform.position.x < wallX);

        // ② Nox影穿过墙
        var noxAbilities = nox.GetComponent<NoxAbilities>();
        nox.SetFrozen(false);
        nox.transform.position = new Vector3(wallX - 2.5f, nox.transform.position.y, 0f);
        nox.SetMoveInput(Vector2.right);
        yield return null;
        while (!noxAbilities.IsReady) yield return null;
        noxAbilities.TryActivate();
        yield return new WaitForSeconds(0.1f);
        yield return new WaitForSeconds(0.6f);
        Check($"Coop level: Nox phases through the shadow wall (noxX={nox.transform.position.x:F2}, wallX={wallX:F2})",
            nox.transform.position.x > wallX);

        // ③ Nox踩压力板 → 影墙消失,Lux可以过
        var plate = GameObject.Find("Coop_Plate");
        Check("Coop level: co-op plate exists", plate != null);
        if (plate != null)
        {
            // 防回归: PressurePlate 曾因撞上Unity魔法回调Reset()被瞬移到世界原点
            Check($"Coop level: plate stays where it was authored (x={plate.transform.position.x:F1}, wallX={wallX:F1})",
                plate.transform.position.x > wallX);

            // 走上去,而不是瞬移到板面。瞬移的落点对不准就踩不到触发区,
            // 而可玩性套件里"走过去"是稳定生效的,两边用同一种方式
            nox.transform.position = new Vector3(plate.transform.position.x - 2.5f,
                nox.transform.position.y, 0f);
            yield return null;
            yield return WalkTo(nox, plate.transform.position.x, 0.5f, 6f, false);
            yield return new WaitForSeconds(0.8f);
            Check("Coop level: Nox on the plate removes the shadow wall for Lux",
                !shadowWall.activeSelf);
        }

        // ④ 光敏机关只有Lux能开 → 门升起
        // 教学提示: 系统接活了,且走进触发区真的会弹出来
        Check("Coop level: hint system is instantiated and wired", HintSystem.Instance != null);
        var hintZone = Object.FindAnyObjectByType<LevelHintZone>();
        Check("Coop level: hint zones are placed", hintZone != null);
        if (HintSystem.Instance != null && hintZone != null)
        {
            lux.SetFrozen(false);
            lux.transform.position = hintZone.transform.position;
            yield return new WaitForSeconds(0.4f);
            Check($"Coop level: walking into a hint zone shows the hint ('{hintZone.HintText}')",
                HintSystem.Instance.IsShowingHint);

            // 验证隐藏/主动再看之前,先把所有提示区停用: 提示区是可重复触发的,
            // 且对任何玩家都响应 —— 只挪开Lux不够,Nox游荡进去照样会重新弹出,
            // 后面"已隐藏"的断言就会时好时坏
            var allZones = Object.FindObjectsByType<LevelHintZone>(FindObjectsSortMode.None);
            foreach (var z in allZones) z.enabled = false;
            yield return null;

            // "提示"按钮: 玩家没看清时能主动再看一次
            // 轮询等淡出结束,而不是睡固定时长: FadeOutAndHide 用 unscaledDeltaTime
            // 累加,批处理下帧长波动大,固定 WaitForSeconds 会时好时坏
            HintSystem.Instance.HideHint();
            float hideWait = 0f;
            while (hideWait < 3f && HintSystem.Instance.IsShowingHint)
            {
                hideWait += Time.deltaTime;
                yield return null;
            }
            Check($"Coop level: hint hides again (after {hideWait:F2}s)",
                !HintSystem.Instance.IsShowingHint);

            HintSystem.Instance.RequestHint();
            yield return null;
            Check("Coop level: the hint button re-shows a hint on demand",
                HintSystem.Instance.IsShowingHint);

            foreach (var z in allZones) if (z != null) z.enabled = true;
        }

        // 背景: 云朵存在且真的在飘
        var cloud = GameObject.Find("BgCloud_0");
        Check("Coop level: background clouds exist", cloud != null);
        if (cloud != null)
        {
            float cloudX0 = cloud.transform.position.x;
            yield return new WaitForSeconds(0.5f);
            Check($"Coop level: clouds drift across the sky (dx={cloud.transform.position.x - cloudX0:F2})",
                cloud.transform.position.x > cloudX0);
        }

        var sensorObj = GameObject.Find("Coop_GateSensor");
        Check("Coop level: gate sensor exists", sensorObj != null);
        if (sensorObj != null)
        {
            float doorYClosed = door.transform.position.y;
            var luxAbilities = lux.GetComponent<LuxAbilities>();
            // Lux站到机关左侧,朝右打光束
            lux.transform.position = sensorObj.transform.position + Vector3.left * 2f;
            lux.SetMoveInput(Vector2.right);
            lux.SetFrozen(true);          // 零摩擦后会滑,滑开就打不中机关了
            yield return null;
            while (!luxAbilities.IsReady) yield return null;
            luxAbilities.TryActivate();
            yield return new WaitForSeconds(1.5f);
            lux.SetFrozen(false);
            Check($"Coop level: Lux beam opens the gate door (y {doorYClosed:F1}->{door.transform.position.y:F1})",
                door.transform.position.y > doorYClosed + 0.5f);
        }
    }

    /// <summary>
    /// 合作关卡测试(Level_1_3) - 用上1_2没用到的两个能力:
    /// ①Nox影推箱子压住压板 → A门永久开启(箱子替人站着)
    /// ②高处光敏机关地面够不到 → Lux造光桥站上去才能打亮 → B门开
    /// </summary>
    private IEnumerator RunCoopLevel13Test()
    {
        if (GameManager.Instance == null) yield break;
        GameManager.Instance.LoadLevel(1, 3);
        yield return WaitForLevelReady("Level_1_3");

        var crate = GameObject.Find("Coop3_Crate");
        var plateGO = GameObject.Find("Coop3_Plate");
        var doorA = GameObject.Find("Coop3_DoorA");
        var sensorGO = GameObject.Find("Coop3_HighSensor");
        var doorB = GameObject.Find("Coop3_DoorB");
        Check("Coop 1-3: crate exists", crate != null);
        Check("Coop 1-3: plate exists", plateGO != null);
        Check("Coop 1-3: door A exists", doorA != null);
        Check("Coop 1-3: high sensor exists", sensorGO != null);
        Check("Coop 1-3: door B exists", doorB != null);
        if (crate == null || plateGO == null || doorA == null || sensorGO == null || doorB == null)
            yield break;

        PlayerController lux = null, nox = null;
        foreach (var p in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (p.Type == PlayerController.PlayerType.Lux) lux = p;
            else nox = p;
        }
        if (lux == null || nox == null) yield break;

        var plate = plateGO.GetComponent<PressurePlate>();
        Check("Coop 1-3: plate starts unpressed", plate != null && !plate.IsPressed);

        // ===== ① Nox影推箱子上压板 =====
        float doorAClosedY = doorA.transform.position.y;
        nox.SetFrozen(false);
        // 站到箱子左侧,朝右推
        nox.transform.position = new Vector3(crate.transform.position.x - 1.2f,
            nox.transform.position.y, 0f);
        nox.SetMoveInput(Vector2.right);
        yield return null;

        var noxAbilities = nox.GetComponent<NoxAbilities>();
        float pushWait = 0f;
        while (pushWait < 3f && (plate == null || !plate.IsPressed))
        {
            Debug.Log($"[L13DIAG] noxX={nox.transform.position.x:F2} facingR={nox.IsFacingRight} " +
                $"crateX={crate.transform.position.x:F2} crateY={crate.transform.position.y:F2} " +
                $"crateLayer={crate.layer} tag={crate.tag}");
            noxAbilities.ShadowPush();
            pushWait += 0.25f;
            yield return new WaitForSeconds(0.25f);
        }
        Check($"Coop 1-3: Nox pushes the crate onto the plate (crateX={crate.transform.position.x:F1}, plateX={plateGO.transform.position.x:F1})",
            plate != null && plate.IsPressed);

        yield return new WaitForSeconds(0.8f);
        Check($"Coop 1-3: weighted plate opens door A (y {doorAClosedY:F1}->{doorA.transform.position.y:F1})",
            doorA.transform.position.y > doorAClosedY + 0.5f);

        // ===== ② 高处机关: 先证明地面打不亮 =====
        var sensor = sensorGO.GetComponent<LightSensor>();
        var luxAbilities = lux.GetComponent<LuxAbilities>();
        lux.SetFrozen(false);
        lux.transform.position = new Vector3(sensorGO.transform.position.x - 2f,
            lux.transform.position.y, 0f);
        lux.SetMoveInput(Vector2.right);
        yield return null;
        while (!luxAbilities.IsReady) yield return null;
        luxAbilities.TryActivate();
        yield return new WaitForSeconds(1.2f);
        Check("Coop 1-3: ground-level beam cannot reach the high sensor (gate is real)",
            sensor != null && !sensor.IsActivated);

        // ===== ② 造光桥站上去再打 =====
        float doorBClosedY = doorB.transform.position.y;
        lux.SetFrozen(true);
        lux.transform.position = new Vector3(sensorGO.transform.position.x - 2f,
            sensorGO.transform.position.y, 0f);
        yield return null;
        luxAbilities.CreateLightBridge();
        yield return null;
        Check("Coop 1-3: Lux can build a bridge at sensor height",
            GameObject.Find("LightBridge") != null);

        while (!luxAbilities.IsReady) yield return null;
        luxAbilities.TryActivate();
        yield return new WaitForSeconds(1.2f);
        Check("Coop 1-3: beam from bridge height activates the high sensor",
            sensor != null && sensor.IsActivated);

        yield return new WaitForSeconds(0.8f);
        Check($"Coop 1-3: high sensor opens door B (y {doorBClosedY:F1}->{doorB.transform.position.y:F1})",
            doorB.transform.position.y > doorBClosedY + 0.5f);

        lux.SetFrozen(false);
    }

    private IEnumerator RunBossTest()
    {
        if (GameManager.Instance == null) yield break;
        GameManager.Instance.LoadLevel(1, 4); // 第1章Boss关
        yield return WaitForLevelReady("Level_1_4");

        var boss = Object.FindAnyObjectByType<BossBase>();
        Check("Boss spawned in boss level", boss != null);
        if (boss == null) yield break;

        // Boss实现IDamageable(玩家可攻击)
        var damageable = boss.GetComponent<IDamageable>();
        Check("Boss implements IDamageable (player can hit it)", damageable != null);

        // 启动Boss战
        boss.StartBattle();
        yield return null;
        Check("Boss battle active", boss.IsBattleActive);

        // 玩家攻击Boss造成伤害
        var players = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        PlayerController lux = null;
        foreach (var p in players) if (p.Type == PlayerController.PlayerType.Lux) lux = p;

        if (lux != null && damageable != null)
        {
            int bossHpBefore = boss.CurrentHealth;
            // 直接通过IDamageable施加伤害(验证接口可用)
            damageable.TakeDamage(5);
            yield return null;
            Check($"Boss takes damage ({bossHpBefore}->{boss.CurrentHealth})",
                boss.CurrentHealth < bossHpBefore);

            // 验证击败流程
            while (boss.IsAlive)
                boss.TakeDamage(50);
            yield return new WaitForSeconds(0.2f);
            Check("Boss can be defeated", !boss.IsAlive);
        }
    }
}
