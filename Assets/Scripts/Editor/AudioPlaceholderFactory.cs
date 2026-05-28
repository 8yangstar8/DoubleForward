using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 音频占位工厂 - 生成静默占位AudioClip(.wav)到Resources目录
/// 确保AudioManager通过字符串key加载时不会null引用
/// 菜单：DoubleForward / Generate Placeholder Audio
/// </summary>
public static class AudioPlaceholderFactory
{
    // ==================== SFX ====================
    private static readonly string[] SFX_Keys = {
        // 脚步声（8种地面 × 步行）
        "footstep_stone", "footstep_wood", "footstep_metal", "footstep_sand",
        "footstep_water", "footstep_grass", "footstep_ice", "footstep_snow",
        // 着地声（8种地面）
        "land_stone", "land_wood", "land_metal", "land_sand",
        "land_water", "land_grass", "land_ice", "land_snow",
        // 玩家动作
        "player_jump", "player_dash", "player_hurt", "player_death",
        "player_attack1", "player_attack2", "player_attack3",
        "player_revive", "player_interact",
        // 技能
        "skill_light_beam", "skill_light_shield", "skill_light_dash",
        "skill_shadow_strike", "skill_shadow_phase", "skill_shadow_trap",
        "skill_light_bridge", "skill_dual_blast",
        // 协作技能组合
        "combo_explosion", "combo_shield", "combo_beam", "combo_wave", "combo_heal",
        // 绳索
        "rope_grab", "rope_release",
        // 收集品
        "coin_collect", "gem_collect", "heal_collect", "secret_found",
        // 机关交互
        "mechanism_activate", "bounce_pad", "water_splash",
        "level_transition", "checkpoint_activate",
        // 谜题
        "pressure_plate_on", "pressure_plate_off",
        "light_sensor_activate", "gear_turn",
        "portal_open", "portal_enter",
        "lever_pull", "switch_toggle",
        // 敌人
        "enemy_hit", "enemy_death", "enemy_alert",
        "enemy_attack", "enemy_shoot",
        // Boss
        "boss_intro", "boss_roar", "boss_attack",
        "boss_phase_change", "boss_death",
        // UI
        "ui_click", "ui_hover", "ui_back",
        "ui_confirm", "ui_cancel",
        "ui_star_earn", "ui_level_complete",
        "ui_achievement_unlock", "ui_notification",
        // 环境
        "door_open", "door_close",
        "crate_break", "glass_shatter",
        "explosion_small", "explosion_large",
        // 陷阱
        "trap_spike", "trap_saw_loop", "trap_arrow_fire",
        "trap_crumble", "trap_falling",
    };

    // ==================== BGM ====================
    private static readonly string[] BGM_Keys = {
        // 菜单/过渡
        "bgm_main_menu", "bgm_loading", "bgm_credits",
        // 5章每章的关卡BGM
        "bgm_chapter1_level", "bgm_chapter1_boss",
        "bgm_chapter2_level", "bgm_chapter2_boss",
        "bgm_chapter3_level", "bgm_chapter3_boss",
        "bgm_chapter4_level", "bgm_chapter4_boss",
        "bgm_chapter5_level", "bgm_chapter5_boss",
        // Boss Rush
        "bgm_boss_rush",
        // 通用
        "bgm_victory", "bgm_gameover",
        "bgm_story_intro", "bgm_story_ending",
    };

    // ==================== Ambient ====================
    private static readonly string[] Ambient_Keys = {
        "ambient_forest", "ambient_factory", "ambient_abyss",
        "ambient_ruins", "ambient_void",
        "ambient_water_underwater", "ambient_wind",
        "ambient_cave_drip", "ambient_lava_bubble",
        "ambient_mechanical_hum",
    };

    [MenuItem("DoubleForward/Generate Placeholder Audio", false, 5)]
    public static void GenerateAll()
    {
        int total = 0;

        EditorUtility.DisplayProgressBar("Generating Audio...", "Creating SFX...", 0.1f);
        total += GenerateCategory("Assets/Resources/Audio/SFX", SFX_Keys, 0.2f);  // 短音效 0.2秒

        EditorUtility.DisplayProgressBar("Generating Audio...", "Creating BGM...", 0.5f);
        total += GenerateCategory("Assets/Resources/Audio/BGM", BGM_Keys, 4.0f);   // BGM 4秒循环段

        EditorUtility.DisplayProgressBar("Generating Audio...", "Creating Ambient...", 0.8f);
        total += GenerateCategory("Assets/Resources/Audio/Ambient", Ambient_Keys, 3.0f); // 环境音 3秒

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        Debug.Log($"[AudioPlaceholderFactory] Generated {total} placeholder audio clips.");
        EditorUtility.DisplayDialog("音频占位生成完成",
            $"已生成 {total} 个占位音频文件\n\n" +
            $"• SFX: {SFX_Keys.Length} 个\n" +
            $"• BGM: {BGM_Keys.Length} 个\n" +
            $"• Ambient: {Ambient_Keys.Length} 个\n\n" +
            "所有文件为静默WAV占位，后续替换为真实音效即可。",
            "OK");
    }

    private static int GenerateCategory(string directory, string[] keys, float durationSec)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        int created = 0;
        foreach (var key in keys)
        {
            string path = $"{directory}/{key}.wav";
            if (File.Exists(path)) continue;

            CreateSilentWav(path, durationSec);
            created++;
        }
        return created;
    }

    /// <summary>
    /// 生成静默WAV文件（PCM 16bit, 22050Hz, Mono）
    /// 保持文件极小但Unity能正常导入
    /// </summary>
    private static void CreateSilentWav(string path, float durationSec)
    {
        int sampleRate = 22050;
        int channels = 1;
        int bitsPerSample = 16;
        int numSamples = Mathf.CeilToInt(sampleRate * durationSec);
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        int blockAlign = channels * bitsPerSample / 8;
        int dataSize = numSamples * blockAlign;

        using (var stream = new FileStream(path, FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            // RIFF header
            writer.Write(new char[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataSize); // ChunkSize
            writer.Write(new char[] { 'W', 'A', 'V', 'E' });

            // fmt sub-chunk
            writer.Write(new char[] { 'f', 'm', 't', ' ' });
            writer.Write(16);            // SubChunk1Size (PCM=16)
            writer.Write((short)1);      // AudioFormat (PCM=1)
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write((short)bitsPerSample);

            // data sub-chunk
            writer.Write(new char[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);

            // 写入静默数据（全零 = 静默）
            // 为了不生成太大的文件，短音效完全静默
            // BGM/Ambient生成极低振幅的噪音以便在编辑器中可视化波形
            if (durationSec > 1.0f)
            {
                // 极低幅度的简单正弦波（几乎听不到但波形可见）
                for (int i = 0; i < numSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    // 220Hz正弦波，幅度极低（约-60dB）
                    float sample = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.001f;
                    short pcmSample = (short)(sample * short.MaxValue);
                    writer.Write(pcmSample);
                }
            }
            else
            {
                // 短音效：纯静默
                byte[] silence = new byte[dataSize];
                writer.Write(silence);
            }
        }
    }

    /// <summary>
    /// 检查占位音频是否已生成
    /// </summary>
    public static bool HasPlaceholderAudio()
    {
        // 检查几个关键文件是否存在
        return File.Exists("Assets/Resources/Audio/SFX/ui_click.wav")
            && File.Exists("Assets/Resources/Audio/BGM/bgm_main_menu.wav")
            && File.Exists("Assets/Resources/Audio/Ambient/ambient_forest.wav");
    }

    /// <summary>
    /// 获取所有音频key分类统计
    /// </summary>
    public static (int sfx, int bgm, int ambient) GetAudioKeyCounts()
    {
        return (SFX_Keys.Length, BGM_Keys.Length, Ambient_Keys.Length);
    }
}
