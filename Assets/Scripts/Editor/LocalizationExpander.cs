using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// 本地化数据扩展器 - 扫描代码中引用的本地化key，补充缺失条目到所有语言JSON
/// 菜单：DoubleForward / Expand Localization Data
/// </summary>
public static class LocalizationExpander
{
    // 代码中引用但可能缺失的额外本地化条目 - 中文+英文对照
    private static readonly (string key, string zh, string en)[] ExtraEntries = {
        // UI 补充
        ("input_press_key", "请按下按钮...", "Press a button..."),
        ("achievement_hidden", "隐藏成就", "Hidden Achievement"),
        ("loading", "加载中...", "Loading..."),
        ("save_new", "新存档", "New Save"),
        ("confirm_delete", "确定删除存档 {0}？此操作不可撤销。", "Delete save {0}? This cannot be undone."),
        ("confirm_yes", "确定", "Confirm"),
        ("confirm_no", "取消", "Cancel"),

        // NG+ 系统
        ("ngplus_title", "New Game+", "New Game+"),
        ("ngplus_difficulty", "NG+ 难度倍率", "NG+ Difficulty Multiplier"),
        ("ngplus_cycle", "第{0}周目", "Cycle {0}"),
        ("ngplus_start", "开始新周目", "Start New Cycle"),
        ("ngplus_enemies_stronger", "敌人强化 ×{0}", "Enemies ×{0} stronger"),

        // 关卡完成扩展
        ("complete_stars", "星级评价", "Star Rating"),
        ("complete_rank_s", "S级", "Rank S"),
        ("complete_rank_a", "A级", "Rank A"),
        ("complete_rank_b", "B级", "Rank B"),
        ("complete_rank_c", "C级", "Rank C"),

        // Boss Rush
        ("bossrush_title", "Boss连战", "Boss Rush"),
        ("bossrush_best_time", "最佳时间", "Best Time"),
        ("bossrush_current", "当前波次", "Current Wave"),
        ("bossrush_cleared", "已全部击败！", "All Cleared!"),

        // 合作评分
        ("coop_score_title", "合作评分", "Co-op Score"),
        ("coop_sync_bonus", "同步奖励", "Sync Bonus"),
        ("coop_revive_count", "复活次数", "Revive Count"),

        // 挑战系统
        ("challenge_title", "挑战", "Challenges"),
        ("challenge_daily", "每日挑战", "Daily Challenge"),
        ("challenge_weekly", "每周挑战", "Weekly Challenge"),
        ("challenge_complete", "挑战完成！", "Challenge Complete!"),

        // 关卡修改器
        ("modifier_title", "关卡修改器", "Level Modifiers"),
        ("modifier_speed", "加速模式", "Speed Mode"),
        ("modifier_mirror", "镜像模式", "Mirror Mode"),
        ("modifier_onelife", "一命模式", "One Life Mode"),
        ("modifier_noskill", "无技能模式", "No Skills Mode"),

        // 故事/对话补充
        ("story_chapter_intro", "章节开始", "Chapter Start"),
        ("story_chapter_ending", "章节结束", "Chapter End"),

        // 系统消息
        ("msg_save_success", "存档保存成功", "Save successful"),
        ("msg_save_failed", "存档保存失败", "Save failed"),
        ("msg_load_success", "存档加载成功", "Load successful"),
        ("msg_load_failed", "存档加载失败", "Load failed"),
        ("msg_network_error", "网络连接错误", "Network error"),
        ("msg_sync_complete", "同步完成", "Sync complete"),

        // 关卡名称 (20关)
        ("level_1_1_name", "光影初现", "Light & Shadow"),
        ("level_1_2_name", "暗林小径", "Forest Trail"),
        ("level_1_3_name", "古树之心", "Heart of the Tree"),
        ("level_1_4_name", "森之守卫", "Forest Guardian"),
        ("level_2_1_name", "齿轮启动", "Gear Start"),
        ("level_2_2_name", "蒸汽通道", "Steam Passage"),
        ("level_2_3_name", "钢铁迷宫", "Iron Labyrinth"),
        ("level_2_4_name", "暴君之心", "Heart of the Tyrant"),
        ("level_3_1_name", "深渊之口", "Mouth of the Abyss"),
        ("level_3_2_name", "幽暗水域", "Dark Waters"),
        ("level_3_3_name", "沉船遗迹", "Sunken Ruins"),
        ("level_3_4_name", "渊蛇巢穴", "Serpent's Lair"),
        ("level_4_1_name", "遗忘神殿", "Forgotten Temple"),
        ("level_4_2_name", "影之回廊", "Shadow Corridor"),
        ("level_4_3_name", "光暗试炼", "Trial of Light & Dark"),
        ("level_4_4_name", "废墟哨兵", "Ruin Sentinel"),
        ("level_5_1_name", "虚空裂隙", "Void Rift"),
        ("level_5_2_name", "光暗交汇", "Convergence"),
        ("level_5_3_name", "终焉之路", "Path to the End"),
        ("level_5_4_name", "双向归一", "Two Become One"),

        // 关卡描述
        ("level_1_1_desc", "学习基本操作，体验光暗配合", "Learn basics, experience light and dark cooperation"),
        ("level_1_2_desc", "穿越暗林，解开第一个光暗谜题", "Cross the dark forest, solve first puzzles"),
        ("level_1_3_desc", "深入古树内部，发现隐藏的力量", "Explore the ancient tree, discover hidden power"),
        ("level_1_4_desc", "挑战森之守卫，证明你们的默契", "Challenge the Forest Guardian, prove your bond"),
        ("level_2_1_desc", "进入工厂，适应机械环境", "Enter the factory, adapt to machinery"),
        ("level_2_2_desc", "利用蒸汽和齿轮通过险境", "Use steam and gears to cross dangers"),
        ("level_2_3_desc", "在钢铁迷宫中找到出路", "Find your way through the iron maze"),
        ("level_2_4_desc", "对抗齿轮暴君", "Face the Gear Tyrant"),
        ("level_3_1_desc", "潜入深渊，面对未知", "Descend into the abyss, face the unknown"),
        ("level_3_2_desc", "在幽暗水域中求生", "Survive the dark waters"),
        ("level_3_3_desc", "探索沉船中的远古秘密", "Explore ancient secrets in the sunken ruins"),
        ("level_3_4_desc", "挑战渊蛇", "Challenge the Abyss Serpent"),
        ("level_4_1_desc", "进入远古遗迹", "Enter the ancient ruins"),
        ("level_4_2_desc", "在影之回廊中解开光暗机关", "Solve light-dark mechanisms in shadow corridors"),
        ("level_4_3_desc", "通过光暗试炼的考验", "Pass the trial of light and dark"),
        ("level_4_4_desc", "击败废墟哨兵", "Defeat the Ruin Sentinel"),
        ("level_5_1_desc", "踏入虚空世界", "Step into the void world"),
        ("level_5_2_desc", "光暗力量在此交汇", "Light and dark converge here"),
        ("level_5_3_desc", "最后的旅途", "The final journey"),
        ("level_5_4_desc", "一切的终点与起点", "Where it all ends and begins"),

        // 教程扩展
        ("tutorial_wall_jump", "靠近墙壁跳跃可以蹬墙跳", "Jump near walls to wall jump"),
        ("tutorial_climb", "抓住梯子上下攀爬", "Grab ladders to climb up and down"),
        ("tutorial_switch_char", "在单人模式下切换角色", "Switch character in single player mode"),
        ("tutorial_revive", "靠近倒地的队友长按交互复活", "Hold interact near downed partner to revive"),
        ("tutorial_checkpoint", "激活检查点作为复活地点", "Activate checkpoints as respawn points"),

        // 皮肤/外观
        ("skin_default", "默认", "Default"),
        ("skin_locked", "未解锁", "Locked"),
        ("skin_equip", "装备", "Equip"),
        ("skin_equipped", "已装备", "Equipped"),

        // 成就名称 - 部分常见成就
        ("ach_first_clear", "初次通关", "First Clear"),
        ("ach_all_stars", "满星大师", "Star Master"),
        ("ach_no_death", "不死之身", "Undying"),
        ("ach_speed_run", "闪电通关", "Speed Runner"),
        ("ach_all_collect", "收集达人", "Collector"),
        ("ach_coop_master", "默契搭档", "Perfect Partners"),
        ("ach_boss_rush", "Boss猎手", "Boss Hunter"),
        ("ach_secret_finder", "秘密发现者", "Secret Finder"),

        // 世界主题
        ("theme_light_shadow", "光影神殿", "Temple of Light & Shadow"),
        ("theme_ice_fire", "冰火交界", "Realm of Ice & Fire"),
        ("theme_sand_sea", "沙海迷城", "Desert Labyrinth"),
        ("theme_dark_abyss", "暗影深渊", "Dark Abyss"),
        ("theme_convergence", "双向归一", "Convergence"),

        // GameOver 扩展
        ("gameover_msg_1", "不要放弃！", "Don't give up!"),
        ("gameover_msg_2", "再试一次吧！", "Try again!"),
        ("gameover_msg_3", "两人配合是关键！", "Teamwork is key!"),
        ("gameover_hint_1", "试试不同的策略！", "Try a different approach!"),
        ("gameover_hint_2", "注意Boss的攻击模式", "Watch the boss's attack patterns"),
        ("gameover_hint_3", "利用环境中的机关", "Use the environment mechanisms"),
    };

    [MenuItem("DoubleForward/Expand Localization Data", false, 7)]
    public static void ExpandAll()
    {
        int addedZh = 0, addedEn = 0;

        EditorUtility.DisplayProgressBar("Expanding Localization...", "Reading existing data...", 0.2f);

        // 读取现有数据
        var existingZh = LoadExistingKeys("Assets/Resources/Localization/lang_zh_cn.json");
        var existingEn = LoadExistingKeys("Assets/Resources/Localization/lang_en.json");

        EditorUtility.DisplayProgressBar("Expanding Localization...", "Adding missing entries...", 0.5f);

        // 找出需要添加的条目
        var newZhEntries = new List<(string key, string value)>();
        var newEnEntries = new List<(string key, string value)>();

        foreach (var (key, zh, en) in ExtraEntries)
        {
            if (!existingZh.Contains(key))
            {
                newZhEntries.Add((key, zh));
                addedZh++;
            }
            if (!existingEn.Contains(key))
            {
                newEnEntries.Add((key, en));
                addedEn++;
            }
        }

        // 追加到JSON文件
        if (newZhEntries.Count > 0)
            AppendEntriesToJson("Assets/Resources/Localization/lang_zh_cn.json", newZhEntries);
        if (newEnEntries.Count > 0)
            AppendEntriesToJson("Assets/Resources/Localization/lang_en.json", newEnEntries);

        // 同步到其他语言文件（使用英文作为占位）
        SyncToOtherLanguages(newEnEntries);

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        Debug.Log($"[LocalizationExpander] Added {addedZh} zh-CN entries, {addedEn} en entries.");
        EditorUtility.DisplayDialog("本地化扩展完成",
            $"已添加缺失的本地化条目：\n\n" +
            $"• 简体中文: +{addedZh} 条\n" +
            $"• English: +{addedEn} 条\n" +
            $"• 繁体中文/日文/韩文: 使用英文占位\n\n" +
            $"总条目数已从约{existingZh.Count}扩展至{existingZh.Count + addedZh}条。",
            "OK");
    }

    private static HashSet<string> LoadExistingKeys(string path)
    {
        var keys = new HashSet<string>();
        if (!File.Exists(path)) return keys;

        string json = File.ReadAllText(path);
        // 简单正则匹配所有key
        var matches = Regex.Matches(json, @"""key""\s*:\s*""([^""]+)""");
        foreach (Match m in matches)
        {
            keys.Add(m.Groups[1].Value);
        }
        return keys;
    }

    private static void AppendEntriesToJson(string path, List<(string key, string value)> entries)
    {
        if (!File.Exists(path)) return;

        string json = File.ReadAllText(path);

        // 找到最后一个 } 之前的 ] 位置
        int lastBracket = json.LastIndexOf(']');
        if (lastBracket < 0) return;

        // 构建新条目文本
        string newEntries = "";
        foreach (var (key, value) in entries)
        {
            // 转义JSON字符串中的特殊字符
            string escapedValue = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            newEntries += $",\n\n        {{\"key\": \"{key}\", \"value\": \"{escapedValue}\"}}";
        }

        // 在 ] 之前插入
        json = json.Insert(lastBracket, newEntries);
        File.WriteAllText(path, json);
    }

    private static void SyncToOtherLanguages(List<(string key, string value)> enEntries)
    {
        string[] otherLangs = {
            "Assets/Resources/Localization/lang_zh_tw.json",
            "Assets/Resources/Localization/lang_ja.json",
            "Assets/Resources/Localization/lang_ko.json",
        };

        foreach (var langPath in otherLangs)
        {
            if (!File.Exists(langPath)) continue;

            var existing = LoadExistingKeys(langPath);
            var missing = enEntries.Where(e => !existing.Contains(e.key)).ToList();

            if (missing.Count > 0)
            {
                AppendEntriesToJson(langPath, missing);
                Debug.Log($"[Localization] Added {missing.Count} placeholder entries to {Path.GetFileName(langPath)}");
            }
        }
    }

    /// <summary>
    /// 统计当前本地化key数量
    /// </summary>
    public static int GetCurrentKeyCount()
    {
        var keys = LoadExistingKeys("Assets/Resources/Localization/lang_zh_cn.json");
        return keys.Count;
    }
}
