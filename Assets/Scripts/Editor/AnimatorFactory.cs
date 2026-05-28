using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// 动画控制器工厂 - 为所有角色和敌人创建Animator Controller
/// 包含完整的状态机配置和转换条件
/// </summary>
public static class AnimatorFactory
{
    private const string ANIM_DIR = "Assets/Animations";

    [MenuItem("DoubleForward/Create Animator Controllers", false, 52)]
    public static void CreateAll()
    {
        EnsureDirectory($"{ANIM_DIR}/Controllers");
        EnsureDirectory($"{ANIM_DIR}/Clips");

        CreatePlayerAnimator("Lux");
        CreatePlayerAnimator("Nox");
        CreateEnemyAnimator("ShadowSlime");
        CreateEnemyAnimator("ShadowArcher");
        CreateEnemyAnimator("ShadowGuard");
        CreateEnemyAnimator("ShadowFlyer");
        CreateBossAnimator("ForestGuardian", 3);
        CreateBossAnimator("IceFlameTitan", 3);
        CreateBossAnimator("SandstormDjinn", 3);
        CreateBossAnimator("AbyssalSerpent", 3);
        CreateBossAnimator("VoidEntity", 4);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AnimatorFactory] All Animator Controllers created.");
    }

    // ==================== 玩家 ====================

    private static void CreatePlayerAnimator(string name)
    {
        string path = $"{ANIM_DIR}/Controllers/{name}Controller.controller";
        if (File.Exists(path)) return;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        // 参数
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("VelocityY", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsJumping", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsFalling", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsDashing", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsWallSliding", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsClimbing", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Revive", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Ability", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("AbilityIndex", AnimatorControllerParameterType.Int);
        controller.AddParameter("Interact", AnimatorControllerParameterType.Trigger);

        var rootStateMachine = controller.layers[0].stateMachine;

        // 状态
        var idleClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Idle.anim", name, 1f);
        var runClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Run.anim", name, 0.5f);
        var jumpClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Jump.anim", name, 0.4f);
        var fallClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Fall.anim", name, 0.6f);
        var dashClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Dash.anim", name, 0.2f);
        var wallSlideClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_WallSlide.anim", name, 0.5f);
        var climbClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Climb.anim", name, 0.6f);
        var attack1Clip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Attack1.anim", name, 0.3f);
        var attack2Clip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Attack2.anim", name, 0.35f);
        var attack3Clip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Attack3.anim", name, 0.4f);
        var hurtClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Hurt.anim", name, 0.3f);
        var dieClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Die.anim", name, 0.8f);
        var reviveClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Revive.anim", name, 0.6f);
        var ability1Clip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Ability1.anim", name, 0.5f);
        var ability2Clip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Ability2.anim", name, 0.5f);
        var interactClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Interact.anim", name, 0.4f);

        var idle = rootStateMachine.AddState("Idle", new Vector3(300, 0, 0));
        idle.motion = idleClip;
        rootStateMachine.defaultState = idle;

        var run = rootStateMachine.AddState("Run", new Vector3(300, 80, 0));
        run.motion = runClip;

        var jump = rootStateMachine.AddState("Jump", new Vector3(550, -60, 0));
        jump.motion = jumpClip;

        var fall = rootStateMachine.AddState("Fall", new Vector3(550, 60, 0));
        fall.motion = fallClip;

        var dash = rootStateMachine.AddState("Dash", new Vector3(550, 140, 0));
        dash.motion = dashClip;

        var wallSlide = rootStateMachine.AddState("WallSlide", new Vector3(550, 220, 0));
        wallSlide.motion = wallSlideClip;

        var climb = rootStateMachine.AddState("Climb", new Vector3(550, 300, 0));
        climb.motion = climbClip;

        var attack1 = rootStateMachine.AddState("Attack1", new Vector3(100, -100, 0));
        attack1.motion = attack1Clip;

        var attack2 = rootStateMachine.AddState("Attack2", new Vector3(100, -180, 0));
        attack2.motion = attack2Clip;

        var attack3 = rootStateMachine.AddState("Attack3", new Vector3(100, -260, 0));
        attack3.motion = attack3Clip;

        var hurt = rootStateMachine.AddState("Hurt", new Vector3(-100, -100, 0));
        hurt.motion = hurtClip;

        var die = rootStateMachine.AddState("Die", new Vector3(-100, -180, 0));
        die.motion = dieClip;

        var revive = rootStateMachine.AddState("Revive", new Vector3(-100, -260, 0));
        revive.motion = reviveClip;

        var ability1 = rootStateMachine.AddState("Ability1", new Vector3(100, 200, 0));
        ability1.motion = ability1Clip;

        var ability2 = rootStateMachine.AddState("Ability2", new Vector3(100, 280, 0));
        ability2.motion = ability2Clip;

        var interact = rootStateMachine.AddState("Interact", new Vector3(100, 360, 0));
        interact.motion = interactClip;

        // 核心转换
        // Idle ↔ Run
        AddTransition(idle, run, "Speed", 0.1f, AnimatorConditionMode.Greater);
        AddTransition(run, idle, "Speed", 0.1f, AnimatorConditionMode.Less);

        // Idle/Run → Jump
        AddBoolTransition(idle, jump, "IsJumping", true);
        AddBoolTransition(run, jump, "IsJumping", true);

        // Jump → Fall
        AddBoolTransition(jump, fall, "IsFalling", true);

        // Fall → Idle（着地）
        var fallToIdle = fall.AddTransition(idle);
        fallToIdle.hasExitTime = false;
        fallToIdle.duration = 0.05f;
        fallToIdle.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");

        // Dash
        AddBoolTransition(idle, dash, "IsDashing", true);
        AddBoolTransition(run, dash, "IsDashing", true);
        AddBoolTransition(dash, idle, "IsDashing", false);

        // WallSlide
        AddBoolTransition(fall, wallSlide, "IsWallSliding", true);
        AddBoolTransition(wallSlide, fall, "IsWallSliding", false);
        AddBoolTransition(wallSlide, jump, "IsJumping", true);

        // Climb
        AddBoolTransition(idle, climb, "IsClimbing", true);
        AddBoolTransition(climb, idle, "IsClimbing", false);

        // 攻击
        AddTriggerTransition(idle, attack1, "Attack");
        AddTriggerTransition(run, attack1, "Attack");
        AddExitTimeTransition(attack1, idle, 0.9f);
        AddExitTimeTransition(attack2, idle, 0.9f);
        AddExitTimeTransition(attack3, idle, 0.9f);

        // 受伤
        AddTriggerToAnyState(rootStateMachine, hurt, "Hurt");
        AddExitTimeTransition(hurt, idle, 0.9f);

        // 死亡
        AddTriggerToAnyState(rootStateMachine, die, "Die");

        // 复活
        AddTriggerTransition(die, revive, "Revive");
        AddExitTimeTransition(revive, idle, 0.9f);

        // 技能
        AddTriggerTransition(idle, ability1, "Ability");
        AddTriggerTransition(run, ability1, "Ability");
        AddExitTimeTransition(ability1, idle, 0.9f);
        AddExitTimeTransition(ability2, idle, 0.9f);

        // 交互
        AddTriggerTransition(idle, interact, "Interact");
        AddExitTimeTransition(interact, idle, 0.9f);

        EditorUtility.SetDirty(controller);
        Debug.Log($"[AnimatorFactory] Created {name} player animator");
    }

    // ==================== 敌人 ====================

    private static void CreateEnemyAnimator(string name)
    {
        string path = $"{ANIM_DIR}/Controllers/{name}Controller.controller";
        if (File.Exists(path)) return;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsChasing", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;

        var idleClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Idle.anim", name, 1f);
        var walkClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Walk.anim", name, 0.6f);
        var chaseClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Chase.anim", name, 0.4f);
        var attackClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Attack.anim", name, 0.4f);
        var hurtClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Hurt.anim", name, 0.3f);
        var dieClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Die.anim", name, 0.8f);

        var idle = sm.AddState("Idle", new Vector3(300, 0, 0));
        idle.motion = idleClip;
        sm.defaultState = idle;

        var walk = sm.AddState("Walk", new Vector3(300, 80, 0));
        walk.motion = walkClip;

        var chase = sm.AddState("Chase", new Vector3(500, 40, 0));
        chase.motion = chaseClip;

        var attack = sm.AddState("Attack", new Vector3(500, 140, 0));
        attack.motion = attackClip;

        var hurt = sm.AddState("Hurt", new Vector3(100, -80, 0));
        hurt.motion = hurtClip;

        var die = sm.AddState("Die", new Vector3(100, -160, 0));
        die.motion = dieClip;

        AddTransition(idle, walk, "Speed", 0.1f, AnimatorConditionMode.Greater);
        AddTransition(walk, idle, "Speed", 0.1f, AnimatorConditionMode.Less);
        AddBoolTransition(walk, chase, "IsChasing", true);
        AddBoolTransition(chase, walk, "IsChasing", false);

        AddTriggerTransition(idle, attack, "Attack");
        AddTriggerTransition(chase, attack, "Attack");
        AddExitTimeTransition(attack, idle, 0.9f);

        AddTriggerToAnyState(sm, hurt, "Hurt");
        AddExitTimeTransition(hurt, idle, 0.9f);

        AddTriggerToAnyState(sm, die, "Die");

        EditorUtility.SetDirty(controller);
        Debug.Log($"[AnimatorFactory] Created {name} enemy animator");
    }

    // ==================== Boss ====================

    private static void CreateBossAnimator(string name, int phaseCount)
    {
        string path = $"{ANIM_DIR}/Controllers/{name}Controller.controller";
        if (File.Exists(path)) return;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.AddParameter("Phase", AnimatorControllerParameterType.Int);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("PhaseTransition", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Enrage", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);

        var sm = controller.layers[0].stateMachine;

        var idleClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Idle.anim", name, 1.5f);
        var moveClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Move.anim", name, 0.8f);
        var attack1Clip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Attack1.anim", name, 0.6f);
        var attack2Clip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Attack2.anim", name, 0.7f);
        var attack3Clip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Attack3.anim", name, 0.8f);
        var hurtClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Hurt.anim", name, 0.3f);
        var phaseClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_PhaseChange.anim", name, 1f);
        var dieClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Die.anim", name, 2f);
        var enrageClip = CreatePlaceholderClip($"{ANIM_DIR}/Clips/{name}_Enrage.anim", name, 1f);

        var idle = sm.AddState("Idle", new Vector3(300, 0, 0));
        idle.motion = idleClip;
        sm.defaultState = idle;

        var move = sm.AddState("Move", new Vector3(300, 100, 0));
        move.motion = moveClip;

        var atk1 = sm.AddState("Attack1", new Vector3(550, -40, 0));
        atk1.motion = attack1Clip;

        var atk2 = sm.AddState("Attack2", new Vector3(550, 40, 0));
        atk2.motion = attack2Clip;

        var atk3 = sm.AddState("Attack3", new Vector3(550, 120, 0));
        atk3.motion = attack3Clip;

        var hurt = sm.AddState("Hurt", new Vector3(50, -80, 0));
        hurt.motion = hurtClip;

        var phaseChange = sm.AddState("PhaseChange", new Vector3(50, 100, 0));
        phaseChange.motion = phaseClip;

        var die = sm.AddState("Die", new Vector3(50, 200, 0));
        die.motion = dieClip;

        var enrage = sm.AddState("Enrage", new Vector3(300, 200, 0));
        enrage.motion = enrageClip;

        AddBoolTransition(idle, move, "IsMoving", true);
        AddBoolTransition(move, idle, "IsMoving", false);

        AddTriggerTransition(idle, atk1, "Attack");
        AddTriggerTransition(move, atk1, "Attack");
        AddExitTimeTransition(atk1, idle, 0.9f);
        AddExitTimeTransition(atk2, idle, 0.9f);
        AddExitTimeTransition(atk3, idle, 0.9f);

        AddTriggerToAnyState(sm, hurt, "Hurt");
        AddExitTimeTransition(hurt, idle, 0.9f);

        AddTriggerToAnyState(sm, phaseChange, "PhaseTransition");
        AddExitTimeTransition(phaseChange, idle, 0.95f);

        AddBoolTransition(idle, enrage, "Enrage", true);
        AddBoolTransition(enrage, idle, "Enrage", false);

        AddTriggerToAnyState(sm, die, "Die");

        EditorUtility.SetDirty(controller);
        Debug.Log($"[AnimatorFactory] Created {name} boss animator ({phaseCount} phases)");
    }

    // ==================== 动画片段生成 ====================

    private static AnimationClip CreatePlaceholderClip(string path, string objName, float duration)
    {
        if (File.Exists(path))
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

        string dir = Path.GetDirectoryName(path);
        EnsureDirectory(dir);

        var clip = new AnimationClip();
        clip.name = Path.GetFileNameWithoutExtension(path);

        // 简单的缩放脉动动画（占位）
        var curveX = new AnimationCurve();
        var curveY = new AnimationCurve();

        string clipName = clip.name.ToLower();

        if (clipName.Contains("idle"))
        {
            // 轻微呼吸动画
            curveX.AddKey(0f, 1f);
            curveX.AddKey(duration / 2f, 1.02f);
            curveX.AddKey(duration, 1f);
            curveY.AddKey(0f, 1f);
            curveY.AddKey(duration / 2f, 1.03f);
            curveY.AddKey(duration, 1f);
        }
        else if (clipName.Contains("run") || clipName.Contains("walk") || clipName.Contains("chase"))
        {
            // 轻微上下弹跳
            curveY.AddKey(0f, 1f);
            curveY.AddKey(duration / 4f, 1.05f);
            curveY.AddKey(duration / 2f, 1f);
            curveY.AddKey(duration * 3f / 4f, 1.05f);
            curveY.AddKey(duration, 1f);
            curveX.AddKey(0f, 1f);
            curveX.AddKey(duration, 1f);
        }
        else if (clipName.Contains("attack"))
        {
            // 攻击前倾+回弹
            curveX.AddKey(0f, 1f);
            curveX.AddKey(duration * 0.3f, 1.15f);
            curveX.AddKey(duration * 0.6f, 0.95f);
            curveX.AddKey(duration, 1f);
            curveY.AddKey(0f, 1f);
            curveY.AddKey(duration, 1f);
        }
        else if (clipName.Contains("hurt"))
        {
            // 受击震颤
            curveX.AddKey(0f, 1f);
            curveX.AddKey(duration * 0.15f, 0.9f);
            curveX.AddKey(duration * 0.3f, 1.1f);
            curveX.AddKey(duration * 0.5f, 0.95f);
            curveX.AddKey(duration, 1f);
            curveY.AddKey(0f, 1f);
            curveY.AddKey(duration, 1f);
        }
        else if (clipName.Contains("die"))
        {
            // 倒下
            curveX.AddKey(0f, 1f);
            curveX.AddKey(duration * 0.5f, 1.1f);
            curveX.AddKey(duration, 1.3f);
            curveY.AddKey(0f, 1f);
            curveY.AddKey(duration * 0.5f, 0.8f);
            curveY.AddKey(duration, 0.2f);
        }
        else
        {
            // 默认：轻微脉动
            curveX.AddKey(0f, 1f);
            curveX.AddKey(duration / 2f, 1.05f);
            curveX.AddKey(duration, 1f);
            curveY.AddKey(0f, 1f);
            curveY.AddKey(duration / 2f, 1.05f);
            curveY.AddKey(duration, 1f);
        }

        clip.SetCurve("", typeof(Transform), "localScale.x", curveX);
        clip.SetCurve("", typeof(Transform), "localScale.y", curveY);

        // 设置循环
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = clipName.Contains("idle") || clipName.Contains("run") ||
                           clipName.Contains("walk") || clipName.Contains("chase") ||
                           clipName.Contains("climb") || clipName.Contains("slide") ||
                           clipName.Contains("fall") || clipName.Contains("move") ||
                           clipName.Contains("enrage");
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // ==================== 辅助：转换构建 ====================

    private static void AddTransition(AnimatorState from, AnimatorState to,
        string param, float threshold, AnimatorConditionMode mode)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.1f;
        t.AddCondition(mode, threshold, param);
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to,
        string param, bool value)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.1f;
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
    }

    private static void AddTriggerTransition(AnimatorState from, AnimatorState to, string triggerParam)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.AddCondition(AnimatorConditionMode.If, 0, triggerParam);
    }

    private static void AddExitTimeTransition(AnimatorState from, AnimatorState to, float exitTime)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.duration = 0.1f;
    }

    private static void AddTriggerToAnyState(AnimatorStateMachine sm, AnimatorState target, string triggerParam)
    {
        var t = sm.AddAnyStateTransition(target);
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.AddCondition(AnimatorConditionMode.If, 0, triggerParam);
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
