using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// 关卡验证工具 - 编辑器窗口检查关卡是否配置正确
/// 验证生成点、检查点、目标触发器、收集品等
/// </summary>
public class LevelValidator : EditorWindow
{
    private Vector2 scrollPos;
    private List<ValidationResult> results = new List<ValidationResult>();
    private bool hasRun;

    public enum ResultType { Pass, Warning, Error }

    public class ValidationResult
    {
        public ResultType type;
        public string category;
        public string message;
        public Object context;
    }

    [MenuItem("DoubleForward/Level Validator", false, 200)]
    public static void ShowWindow()
    {
        var window = GetWindow<LevelValidator>("Level Validator");
        window.minSize = new Vector2(450, 400);
    }

    void OnGUI()
    {
        GUILayout.Label("Level Validator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Validates the current scene for common level setup issues.", MessageType.Info);
        EditorGUILayout.Space(5);

        if (GUILayout.Button("Validate Current Scene", GUILayout.Height(35)))
        {
            ValidateCurrentScene();
        }

        EditorGUILayout.Space(10);

        if (!hasRun)
        {
            EditorGUILayout.HelpBox("Click 'Validate' to check the current scene.", MessageType.None);
            return;
        }

        // 统计
        int errors = 0, warnings = 0, passes = 0;
        foreach (var r in results)
        {
            switch (r.type)
            {
                case ResultType.Error: errors++; break;
                case ResultType.Warning: warnings++; break;
                case ResultType.Pass: passes++; break;
            }
        }

        EditorGUILayout.LabelField($"Results: ✅ {passes}  ⚠️ {warnings}  ❌ {errors}");
        EditorGUILayout.Space(5);

        // 结果列表
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (var result in results)
        {
            string icon = result.type == ResultType.Pass ? "✅" :
                          result.type == ResultType.Warning ? "⚠️" : "❌";

            MessageType msgType = result.type == ResultType.Pass ? MessageType.Info :
                                  result.type == ResultType.Warning ? MessageType.Warning :
                                  MessageType.Error;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox($"{icon} [{result.category}] {result.message}", msgType);

            if (result.context != null)
            {
                if (GUILayout.Button("Select", GUILayout.Width(55), GUILayout.Height(38)))
                {
                    Selection.activeObject = result.context;
                    EditorGUIUtility.PingObject(result.context);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void ValidateCurrentScene()
    {
        results.Clear();
        hasRun = true;

        ValidatePlayerSpawns();
        ValidateCheckpoints();
        ValidateLevelGoal();
        ValidateCollectibles();
        ValidatePuzzles();
        ValidateEnemies();
        ValidateCameras();
        ValidateLighting();
        ValidateBoss();
        ValidatePerformance();

        Repaint();
    }

    private void ValidatePlayerSpawns()
    {
        // LevelBootstrap
        var bootstrap = Object.FindAnyObjectByType<LevelBootstrap>();
        if (bootstrap != null)
        {
            results.Add(new ValidationResult
            {
                type = ResultType.Pass,
                category = "Spawn",
                message = "LevelBootstrap found",
                context = bootstrap
            });
        }
        else
        {
            // 检查LevelManager
            var levelManager = Object.FindAnyObjectByType<LevelManager>();
            if (levelManager != null)
            {
                results.Add(new ValidationResult
                {
                    type = ResultType.Pass,
                    category = "Spawn",
                    message = "LevelManager found (legacy bootstrap)",
                    context = levelManager
                });
            }
            else
            {
                results.Add(new ValidationResult
                {
                    type = ResultType.Error,
                    category = "Spawn",
                    message = "No LevelBootstrap or LevelManager found! Players won't spawn."
                });
            }
        }
    }

    private void ValidateCheckpoints()
    {
        var checkpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);

        if (checkpoints.Length == 0)
        {
            results.Add(new ValidationResult
            {
                type = ResultType.Warning,
                category = "Checkpoint",
                message = "No checkpoints found. Players will respawn at start on death."
            });
        }
        else
        {
            results.Add(new ValidationResult
            {
                type = ResultType.Pass,
                category = "Checkpoint",
                message = $"{checkpoints.Length} checkpoint(s) found"
            });
        }
    }

    private void ValidateLevelGoal()
    {
        var goals = Object.FindObjectsByType<LevelGoalTrigger>(FindObjectsSortMode.None);

        if (goals.Length == 0)
        {
            results.Add(new ValidationResult
            {
                type = ResultType.Error,
                category = "Goal",
                message = "No LevelGoalTrigger found! Level cannot be completed."
            });
        }
        else if (goals.Length > 1)
        {
            results.Add(new ValidationResult
            {
                type = ResultType.Warning,
                category = "Goal",
                message = $"{goals.Length} LevelGoalTriggers found. Usually only 1 is needed."
            });
        }
        else
        {
            results.Add(new ValidationResult
            {
                type = ResultType.Pass,
                category = "Goal",
                message = "LevelGoalTrigger found",
                context = goals[0]
            });
        }
    }

    private void ValidateCollectibles()
    {
        var collectibles = Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None);
        results.Add(new ValidationResult
        {
            type = collectibles.Length > 0 ? ResultType.Pass : ResultType.Warning,
            category = "Collectibles",
            message = $"{collectibles.Length} collectible(s) found"
        });

        // 检查是否有重叠的收集品
        for (int i = 0; i < collectibles.Length; i++)
        {
            for (int j = i + 1; j < collectibles.Length; j++)
            {
                float dist = Vector3.Distance(
                    collectibles[i].transform.position,
                    collectibles[j].transform.position);

                if (dist < 0.5f)
                {
                    results.Add(new ValidationResult
                    {
                        type = ResultType.Warning,
                        category = "Collectibles",
                        message = $"Collectibles too close ({dist:F2}m apart)",
                        context = collectibles[i]
                    });
                }
            }
        }
    }

    private void ValidatePuzzles()
    {
        var plates = Object.FindObjectsByType<PressurePlate>(FindObjectsSortMode.None);
        var sensors = Object.FindObjectsByType<LightSensor>(FindObjectsSortMode.None);
        var walls = Object.FindObjectsByType<ShadowWall>(FindObjectsSortMode.None);
        var gears = Object.FindObjectsByType<GearMechanism>(FindObjectsSortMode.None);

        int totalPuzzles = plates.Length + sensors.Length + walls.Length + gears.Length;

        results.Add(new ValidationResult
        {
            type = totalPuzzles > 0 ? ResultType.Pass : ResultType.Warning,
            category = "Puzzles",
            message = $"{totalPuzzles} puzzle element(s): " +
                      $"{plates.Length} plates, {sensors.Length} sensors, " +
                      $"{walls.Length} walls, {gears.Length} gears"
        });
    }

    private void ValidateEnemies()
    {
        var enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        var spawners = Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);

        results.Add(new ValidationResult
        {
            type = ResultType.Pass,
            category = "Enemies",
            message = $"{enemies.Length} enemy(ies), {spawners.Length} spawner(s)"
        });
    }

    private void ValidateCameras()
    {
        var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);

        if (cameras.Length == 0)
        {
            results.Add(new ValidationResult
            {
                type = ResultType.Error,
                category = "Camera",
                message = "No camera found in scene!"
            });
        }
        else
        {
            var camController = Object.FindAnyObjectByType<CameraController>();
            results.Add(new ValidationResult
            {
                type = camController != null ? ResultType.Pass : ResultType.Warning,
                category = "Camera",
                message = camController != null ?
                    "CameraController found" :
                    "Camera exists but no CameraController. Camera won't follow players."
            });
        }
    }

    private void ValidateLighting()
    {
        var lights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);

        if (lights.Length == 0)
        {
            results.Add(new ValidationResult
            {
                type = ResultType.Warning,
                category = "Lighting",
                message = "No 2D lights found. Scene may appear dark."
            });
        }
        else
        {
            results.Add(new ValidationResult
            {
                type = ResultType.Pass,
                category = "Lighting",
                message = $"{lights.Length} 2D light(s) found"
            });
        }
    }

    private void ValidateBoss()
    {
        var bosses = Object.FindObjectsByType<BossBase>(FindObjectsSortMode.None);
        var arenas = Object.FindObjectsByType<BossArena>(FindObjectsSortMode.None);

        if (bosses.Length > 0)
        {
            results.Add(new ValidationResult
            {
                type = arenas.Length > 0 ? ResultType.Pass : ResultType.Warning,
                category = "Boss",
                message = arenas.Length > 0 ?
                    $"Boss + Arena found" :
                    $"Boss found but no BossArena. Boss won't trigger properly.",
                context = bosses[0]
            });
        }
    }

    private void ValidatePerformance()
    {
        // 检查场景中的物体数量
        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int count = allObjects.Length;

        if (count > 2000)
        {
            results.Add(new ValidationResult
            {
                type = ResultType.Warning,
                category = "Performance",
                message = $"{count} GameObjects in scene. Consider optimizing for mobile."
            });
        }
        else
        {
            results.Add(new ValidationResult
            {
                type = ResultType.Pass,
                category = "Performance",
                message = $"{count} GameObjects (OK for mobile)"
            });
        }

        // 检查Sprite排序
        var renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        results.Add(new ValidationResult
        {
            type = ResultType.Pass,
            category = "Performance",
            message = $"{renderers.Length} SpriteRenderers"
        });
    }
}
