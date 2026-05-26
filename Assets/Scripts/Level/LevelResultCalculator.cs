using UnityEngine;

/// <summary>
/// 关卡结果计算器 - 统一计算关卡完成后的评分与星级
/// 综合时间、收集品、连击、死亡、合作同步来评估表现
/// 被LevelBootstrap和LevelRewardProcessor调用
/// </summary>
public static class LevelResultCalculator
{
    /// <summary>
    /// 计算结果
    /// </summary>
    public static LevelResult Calculate(LevelResultInput input)
    {
        var result = new LevelResult();

        // ====== 基础分 ======
        result.baseScore = 1000;

        // ====== 时间评分 ======
        if (input.parTime > 0 && input.completionTime > 0)
        {
            float timeRatio = input.completionTime / input.parTime;
            if (timeRatio <= 0.5f)
                result.timeScore = 500;     // 极速通关
            else if (timeRatio <= 0.8f)
                result.timeScore = 400;
            else if (timeRatio <= 1.0f)
                result.timeScore = 300;
            else if (timeRatio <= 1.5f)
                result.timeScore = 150;
            else
                result.timeScore = 50;

            result.isSpeedRun = timeRatio <= 0.6f;
        }

        // ====== 收集品评分 ======
        if (input.totalCollectibles > 0)
        {
            float collectRatio = (float)input.collectedCount / input.totalCollectibles;
            result.collectibleScore = Mathf.RoundToInt(collectRatio * 300f);
            result.allCollected = input.collectedCount >= input.totalCollectibles;
        }

        // ====== 连击评分 ======
        if (input.maxCombo >= 30)
            result.comboScore = 400;
        else if (input.maxCombo >= 20)
            result.comboScore = 300;
        else if (input.maxCombo >= 10)
            result.comboScore = 200;
        else if (input.maxCombo >= 5)
            result.comboScore = 100;
        else
            result.comboScore = 0;

        // ====== 死亡惩罚 ======
        result.deathPenalty = Mathf.Min(input.deathCount * 150, 600);
        result.noDeath = input.deathCount == 0;

        // ====== 合作加成 ======
        result.coopBonus = input.syncActions * 30 + input.perfectSyncActions * 80;
        if (input.coopRevives > 0)
            result.coopBonus += input.coopRevives * 50;

        // ====== 总分 ======
        result.totalScore = Mathf.Max(0,
            result.baseScore +
            result.timeScore +
            result.collectibleScore +
            result.comboScore +
            result.coopBonus -
            result.deathPenalty
        );

        // ====== 星级计算 ======
        result.stars = CalculateStars(result, input);

        // ====== 评级 ======
        result.rank = CalculateRank(result.totalScore);

        // ====== 特殊成就检查 ======
        result.perfectClear = result.noDeath && result.allCollected && result.stars >= 3;

        return result;
    }

    private static int CalculateStars(LevelResult result, LevelResultInput input)
    {
        int stars = 0;

        // 1星：完成关卡
        stars = 1;

        // 2星：时间合格 + 收集至少50%
        float collectRatio = input.totalCollectibles > 0
            ? (float)input.collectedCount / input.totalCollectibles : 1f;
        float timeRatio = input.parTime > 0
            ? input.completionTime / input.parTime : 0.5f;

        if (timeRatio <= 1.2f && collectRatio >= 0.5f)
            stars = 2;

        // 3星：好时间 + 高收集 + 低死亡
        if (timeRatio <= 1.0f && collectRatio >= 0.8f && input.deathCount <= 1)
            stars = 3;

        return stars;
    }

    private static string CalculateRank(int totalScore)
    {
        if (totalScore >= 2500) return "S";
        if (totalScore >= 2000) return "A";
        if (totalScore >= 1500) return "B";
        if (totalScore >= 1000) return "C";
        return "D";
    }

    /// <summary>
    /// 计算输入参数
    /// </summary>
    public class LevelResultInput
    {
        public float completionTime;
        public float parTime;
        public int collectedCount;
        public int totalCollectibles;
        public int maxCombo;
        public int deathCount;
        public int syncActions;
        public int perfectSyncActions;
        public int coopRevives;
    }

    /// <summary>
    /// 计算结果
    /// </summary>
    public class LevelResult
    {
        public int baseScore;
        public int timeScore;
        public int collectibleScore;
        public int comboScore;
        public int coopBonus;
        public int deathPenalty;
        public int totalScore;
        public int stars;
        public string rank;

        // 特殊标记
        public bool isSpeedRun;
        public bool allCollected;
        public bool noDeath;
        public bool perfectClear;
    }
}
