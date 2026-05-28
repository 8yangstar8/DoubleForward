using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 占位符精灵生成器 - 为所有游戏对象创建简易彩色占位精灵
/// 在Unity中运行后即可让所有预制体可视化运行
/// </summary>
public static class PlaceholderSpriteGenerator
{
    private const string SPRITE_DIR = "Assets/Art/Placeholders";

    [MenuItem("DoubleForward/Generate Placeholder Sprites", false, 50)]
    public static void GenerateAll()
    {
        EnsureDirectory(SPRITE_DIR);
        EnsureDirectory(SPRITE_DIR + "/Characters");
        EnsureDirectory(SPRITE_DIR + "/Enemies");
        EnsureDirectory(SPRITE_DIR + "/Bosses");
        EnsureDirectory(SPRITE_DIR + "/Puzzles");
        EnsureDirectory(SPRITE_DIR + "/Projectiles");
        EnsureDirectory(SPRITE_DIR + "/VFX");
        EnsureDirectory(SPRITE_DIR + "/UI");
        EnsureDirectory(SPRITE_DIR + "/Environment");

        // 角色
        CreateSprite("Characters/Lux", 32, 48, new Color(1f, 0.9f, 0.4f), DrawHumanoid);
        CreateSprite("Characters/Nox", 32, 48, new Color(0.35f, 0.15f, 0.6f), DrawHumanoid);

        // 敌人
        CreateSprite("Enemies/ShadowSlime", 32, 28, new Color(0.3f, 0.1f, 0.4f), DrawSlime);
        CreateSprite("Enemies/ShadowArcher", 32, 48, new Color(0.4f, 0.15f, 0.3f), DrawHumanoid);
        CreateSprite("Enemies/ShadowGuard", 36, 48, new Color(0.25f, 0.25f, 0.35f), DrawGuard);
        CreateSprite("Enemies/ShadowFlyer", 40, 32, new Color(0.5f, 0.2f, 0.5f), DrawFlyer);
        CreateSprite("Enemies/ShadowBrute", 40, 52, new Color(0.35f, 0.1f, 0.15f), DrawBrute);
        CreateSprite("Enemies/Projectile", 12, 12, new Color(0.8f, 0.2f, 0.8f), DrawCircle);

        // Boss
        CreateSprite("Bosses/ForestGuardian", 64, 80, new Color(0.2f, 0.5f, 0.2f), DrawBossTree);
        CreateSprite("Bosses/IceFlameTitan", 72, 72, new Color(0.3f, 0.5f, 0.9f), DrawBossSquare);
        CreateSprite("Bosses/SandstormDjinn", 56, 72, new Color(0.8f, 0.7f, 0.3f), DrawBossTriangle);
        CreateSprite("Bosses/AbyssalSerpent", 80, 48, new Color(0.15f, 0.1f, 0.3f), DrawBossSerpent);
        CreateSprite("Bosses/VoidEntity", 72, 72, new Color(0.5f, 0.1f, 0.5f), DrawBossVoid);
        CreateSprite("Bosses/BossCore", 20, 20, new Color(1f, 0.3f, 0.3f), DrawCircle);
        CreateSprite("Bosses/RootSwipe", 64, 16, new Color(0.3f, 0.4f, 0.2f), DrawRect);
        CreateSprite("Bosses/LeafProjectile", 12, 12, new Color(0.4f, 0.7f, 0.2f), DrawDiamond);
        CreateSprite("Bosses/VineTrap", 16, 48, new Color(0.2f, 0.6f, 0.15f), DrawRect);

        // 谜题元素
        CreateSprite("Puzzles/PressurePlate", 32, 8, new Color(0.6f, 0.6f, 0.6f), DrawRect);
        CreateSprite("Puzzles/LightSensor", 24, 24, new Color(1f, 1f, 0.5f), DrawDiamond);
        CreateSprite("Puzzles/ShadowWall", 16, 48, new Color(0.15f, 0.05f, 0.2f), DrawRect);
        CreateSprite("Puzzles/Portal", 24, 32, new Color(0.3f, 0.8f, 1f), DrawOval);
        CreateSprite("Puzzles/GearMechanism", 32, 32, new Color(0.5f, 0.5f, 0.5f), DrawGear);
        CreateSprite("Puzzles/Switch", 16, 24, new Color(0.8f, 0.5f, 0.2f), DrawRect);
        CreateSprite("Puzzles/Checkpoint", 16, 32, new Color(0.2f, 0.8f, 0.3f), DrawFlag);
        CreateSprite("Puzzles/Collectible", 16, 16, new Color(1f, 0.85f, 0.2f), DrawStar);
        CreateSprite("Puzzles/GoalFlag", 24, 48, new Color(0.9f, 0.1f, 0.1f), DrawFlag);

        // 环境
        CreateSprite("Environment/MovingPlatform", 64, 12, new Color(0.5f, 0.45f, 0.4f), DrawRect);
        CreateSprite("Environment/OneWayPlatform", 64, 8, new Color(0.6f, 0.55f, 0.3f), DrawRect);
        CreateSprite("Environment/BouncePad", 32, 12, new Color(0.3f, 0.9f, 0.5f), DrawRect);
        CreateSprite("Environment/Ladder", 16, 64, new Color(0.5f, 0.35f, 0.2f), DrawLadder);
        CreateSprite("Environment/Spike", 16, 16, new Color(0.7f, 0.7f, 0.7f), DrawTriangle);
        CreateSprite("Environment/SwingRope", 4, 64, new Color(0.5f, 0.4f, 0.25f), DrawRect);
        CreateSprite("Environment/PowerUp", 16, 16, new Color(0.3f, 1f, 0.6f), DrawStar);
        CreateSprite("Environment/RelicPickup", 20, 20, new Color(0.9f, 0.6f, 0.1f), DrawDiamond);

        // VFX
        CreateSprite("VFX/Particle", 8, 8, Color.white, DrawCircle);
        CreateSprite("VFX/GlowSoft", 32, 32, new Color(1f, 1f, 1f, 0.5f), DrawGlow);
        CreateSprite("VFX/Ring", 32, 32, Color.white, DrawRing);
        CreateSprite("VFX/Arrow", 16, 8, Color.white, DrawArrow);

        // UI 占位
        CreateSprite("UI/HealthIcon", 16, 16, new Color(1f, 0.3f, 0.3f), DrawHeart);
        CreateSprite("UI/StarFilled", 24, 24, new Color(1f, 0.85f, 0.2f), DrawStar);
        CreateSprite("UI/StarEmpty", 24, 24, new Color(0.4f, 0.4f, 0.4f), DrawStar);
        CreateSprite("UI/ButtonBG", 128, 48, new Color(0.25f, 0.25f, 0.3f), DrawRoundedRect);
        CreateSprite("UI/PanelBG", 256, 256, new Color(0.12f, 0.12f, 0.18f, 0.9f), DrawRoundedRect);
        CreateSprite("UI/SliderBG", 128, 12, new Color(0.2f, 0.2f, 0.25f), DrawRoundedRect);
        CreateSprite("UI/SliderFill", 128, 12, new Color(0.3f, 0.7f, 1f), DrawRoundedRect);
        CreateSprite("UI/IconLock", 24, 24, new Color(0.5f, 0.5f, 0.5f), DrawLock);
        CreateSprite("UI/AchievementDefault", 32, 32, new Color(0.6f, 0.6f, 0.6f), DrawCircle);

        AssetDatabase.Refresh();
        Debug.Log($"[PlaceholderSprites] All placeholder sprites generated in {SPRITE_DIR}");
    }

    // ==================== 绘制函数 ====================

    private delegate void DrawFunc(Texture2D tex, Color baseColor);

    private static void DrawHumanoid(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        // 头
        FillCircle(tex, w / 2, h - w / 4 - 2, w / 4, c);
        // 身体
        FillRect(tex, w / 4, h / 4, w / 2, h / 2, c * 0.85f);
        // 腿
        FillRect(tex, w / 4, 0, w / 5, h / 4, c * 0.7f);
        FillRect(tex, w - w / 4 - w / 5, 0, w / 5, h / 4, c * 0.7f);
        // 眼睛
        FillRect(tex, w / 2 - 3, h - w / 4 - 1, 2, 2, Color.white);
        FillRect(tex, w / 2 + 1, h - w / 4 - 1, 2, 2, Color.white);
        tex.Apply();
    }

    private static void DrawSlime(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - w / 2f) / (w / 2f);
                float dy = (y - 0f) / (float)h;
                float shape = dx * dx + (1f - dy) * (1f - dy) * 0.7f;
                if (shape < 1f && y > 0)
                    tex.SetPixel(x, y, Color.Lerp(c, c * 1.3f, dy));
            }
        // 眼睛
        FillRect(tex, w / 3 - 1, h * 2 / 3, 3, 3, Color.white);
        FillRect(tex, w * 2 / 3 - 2, h * 2 / 3, 3, 3, Color.white);
        tex.Apply();
    }

    private static void DrawGuard(Texture2D tex, Color c)
    {
        DrawHumanoid(tex, c);
        // 盾牌
        FillRect(tex, 1, tex.height / 4, tex.width / 4, tex.height / 2, new Color(0.6f, 0.6f, 0.7f));
        tex.Apply();
    }

    private static void DrawFlyer(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        // 身体
        FillCircle(tex, w / 2, h / 2, h / 3, c);
        // 翅膀
        FillTriangle(tex, 0, h / 2, w / 4, h - 2, w / 3, h / 2, c * 0.8f);
        FillTriangle(tex, w, h / 2, w * 3 / 4, h - 2, w * 2 / 3, h / 2, c * 0.8f);
        // 眼睛
        FillRect(tex, w / 2 - 3, h / 2 + 1, 2, 2, Color.red);
        FillRect(tex, w / 2 + 1, h / 2 + 1, 2, 2, Color.red);
        tex.Apply();
    }

    private static void DrawBrute(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        // 大身体
        FillRect(tex, w / 6, h / 6, w * 2 / 3, h * 3 / 5, c);
        // 头
        FillCircle(tex, w / 2, h - w / 4, w / 4, c * 1.1f);
        // 腿（粗）
        FillRect(tex, w / 5, 0, w / 4, h / 5, c * 0.7f);
        FillRect(tex, w - w / 5 - w / 4, 0, w / 4, h / 5, c * 0.7f);
        // 眼
        FillRect(tex, w / 2 - 4, h - w / 4, 3, 3, new Color(1f, 0.3f, 0.1f));
        FillRect(tex, w / 2 + 1, h - w / 4, 3, 3, new Color(1f, 0.3f, 0.1f));
        tex.Apply();
    }

    private static void DrawBossTree(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        // 树干
        FillRect(tex, w / 3, 0, w / 3, h * 2 / 3, new Color(0.35f, 0.25f, 0.1f));
        // 树冠
        FillCircle(tex, w / 2, h * 3 / 4, w / 3, c);
        FillCircle(tex, w / 3, h * 2 / 3, w / 4, c * 0.9f);
        FillCircle(tex, w * 2 / 3, h * 2 / 3, w / 4, c * 0.9f);
        // 红眼
        FillCircle(tex, w / 2 - 6, h * 3 / 4 + 2, 4, new Color(1f, 0.2f, 0.1f));
        FillCircle(tex, w / 2 + 6, h * 3 / 4 + 2, 4, new Color(1f, 0.2f, 0.1f));
        tex.Apply();
    }

    private static void DrawBossSquare(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        // 冰（左半）
        FillRect(tex, 2, 2, w / 2 - 4, h - 4, new Color(0.4f, 0.7f, 1f));
        // 火（右半）
        FillRect(tex, w / 2 + 2, 2, w / 2 - 4, h - 4, new Color(1f, 0.4f, 0.15f));
        // 眼
        FillCircle(tex, w / 4, h * 2 / 3, 5, Color.white);
        FillCircle(tex, w * 3 / 4, h * 2 / 3, 5, Color.white);
        tex.Apply();
    }

    private static void DrawBossTriangle(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        FillTriangle(tex, w / 2, h - 2, 2, 2, w - 2, 2, c);
        FillCircle(tex, w / 2, h / 2, 6, new Color(1f, 0.8f, 0.2f));
        tex.Apply();
    }

    private static void DrawBossSerpent(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        for (int x = 4; x < w - 4; x++)
        {
            float wave = Mathf.Sin(x * 0.15f) * (h / 4f);
            int cy = h / 2 + (int)wave;
            FillCircle(tex, x, cy, 8, Color.Lerp(c, c * 1.5f, (float)x / w));
        }
        // 头
        FillCircle(tex, w - 10, h / 2, 12, c * 1.3f);
        FillRect(tex, w - 14, h / 2 + 3, 3, 3, new Color(1f, 0.2f, 0.5f));
        tex.Apply();
    }

    private static void DrawBossVoid(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        // 旋涡效果
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x - w / 2f, dy = y - h / 2f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float maxR = Mathf.Min(w, h) / 2f - 2;
                if (dist < maxR)
                {
                    float t = dist / maxR;
                    Color col = Color.Lerp(new Color(0.8f, 0.2f, 1f), c, t);
                    col.a = 1f - t * 0.3f;
                    tex.SetPixel(x, y, col);
                }
            }
        // 中心眼睛
        FillCircle(tex, w / 2, h / 2, 8, new Color(1f, 1f, 1f, 0.9f));
        FillCircle(tex, w / 2, h / 2, 4, new Color(0.1f, 0f, 0.2f));
        tex.Apply();
    }

    // ==================== 基础形状 ====================

    private static void DrawCircle(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        FillCircle(tex, tex.width / 2, tex.height / 2, Mathf.Min(tex.width, tex.height) / 2 - 1, c);
        tex.Apply();
    }

    private static void DrawRect(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        FillRect(tex, 1, 1, tex.width - 2, tex.height - 2, c);
        tex.Apply();
    }

    private static void DrawOval(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - w / 2f) / (w / 2f);
                float dy = (y - h / 2f) / (h / 2f);
                if (dx * dx + dy * dy < 1f)
                    tex.SetPixel(x, y, c);
            }
        tex.Apply();
    }

    private static void DrawDiamond(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x - w / 2f) / (w / 2f);
                float dy = Mathf.Abs(y - h / 2f) / (h / 2f);
                if (dx + dy < 1f)
                    tex.SetPixel(x, y, c);
            }
        tex.Apply();
    }

    private static void DrawTriangle(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        FillTriangle(tex, tex.width / 2, tex.height - 2, 2, 2, tex.width - 2, 2, c);
        tex.Apply();
    }

    private static void DrawStar(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        FillDiamond(tex, w / 2, h / 2, w / 2 - 1, h / 2 - 1, c);
        FillRect(tex, 1, h / 3, w - 2, h / 3, c);
        tex.Apply();
    }

    private static void DrawGear(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        FillCircle(tex, w / 2, h / 2, w / 3, c);
        // 齿牙（4个方向）
        FillRect(tex, w / 2 - 3, 0, 6, h, c * 0.8f);
        FillRect(tex, 0, h / 2 - 3, w, 6, c * 0.8f);
        // 中心孔
        FillCircle(tex, w / 2, h / 2, w / 8, new Color(0, 0, 0, 0));
        tex.Apply();
    }

    private static void DrawFlag(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        // 杆
        FillRect(tex, 1, 0, 3, h, new Color(0.5f, 0.4f, 0.3f));
        // 旗
        FillRect(tex, 4, h / 2, w - 5, h / 2 - 1, c);
        tex.Apply();
    }

    private static void DrawHeart(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        FillCircle(tex, w / 3, h * 2 / 3, w / 4, c);
        FillCircle(tex, w * 2 / 3, h * 2 / 3, w / 4, c);
        FillTriangle(tex, w / 2, 2, 2, h / 2, w - 2, h / 2, c);
        tex.Apply();
    }

    private static void DrawGlow(Texture2D tex, Color c)
    {
        int w = tex.width, h = tex.height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - w / 2f) / (w / 2f);
                float dy = (y - h / 2f) / (h / 2f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - dist);
                alpha *= alpha;
                tex.SetPixel(x, y, new Color(c.r, c.g, c.b, alpha * c.a));
            }
        tex.Apply();
    }

    private static void DrawRing(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - w / 2f) / (w / 2f);
                float dy = (y - h / 2f) / (h / 2f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > 0.6f && dist < 0.95f)
                    tex.SetPixel(x, y, c);
            }
        tex.Apply();
    }

    private static void DrawArrow(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        FillRect(tex, 0, h / 3, w * 2 / 3, h / 3, c);
        FillTriangle(tex, w - 1, h / 2, w / 2, h - 1, w / 2, 0, c);
        tex.Apply();
    }

    private static void DrawRoundedRect(Texture2D tex, Color c)
    {
        int w = tex.width, h = tex.height;
        int r = Mathf.Min(4, Mathf.Min(w, h) / 4);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool inside = true;
                // 检查四角
                if (x < r && y < r) inside = ((x - r) * (x - r) + (y - r) * (y - r)) <= r * r;
                else if (x >= w - r && y < r) inside = ((x - (w - r - 1)) * (x - (w - r - 1)) + (y - r) * (y - r)) <= r * r;
                else if (x < r && y >= h - r) inside = ((x - r) * (x - r) + (y - (h - r - 1)) * (y - (h - r - 1))) <= r * r;
                else if (x >= w - r && y >= h - r) inside = ((x - (w - r - 1)) * (x - (w - r - 1)) + (y - (h - r - 1)) * (y - (h - r - 1))) <= r * r;

                tex.SetPixel(x, y, inside ? c : Color.clear);
            }
        tex.Apply();
    }

    private static void DrawLadder(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        // 两根竖杆
        FillRect(tex, 1, 0, 3, h, c);
        FillRect(tex, w - 4, 0, 3, h, c);
        // 横档
        int step = h / 6;
        for (int i = 1; i < 6; i++)
            FillRect(tex, 2, i * step - 1, w - 4, 2, c * 0.8f);
        tex.Apply();
    }

    private static void DrawLock(Texture2D tex, Color c)
    {
        ClearTexture(tex);
        int w = tex.width, h = tex.height;
        // 锁体
        FillRect(tex, w / 5, 1, w * 3 / 5, h / 2, c);
        // 锁环
        for (int a = 0; a < 360; a++)
        {
            float rad = a * Mathf.Deg2Rad;
            int rx = (int)(Mathf.Cos(rad) * w / 4);
            int ry = (int)(Mathf.Sin(rad) * h / 4);
            int px = w / 2 + rx, py = h / 2 + Mathf.Abs(ry);
            if (py >= 0 && py < h && px >= 0 && px < w)
                tex.SetPixel(px, py, c * 0.8f);
        }
        tex.Apply();
    }

    // ==================== 图元绘制 ====================

    private static void ClearTexture(Texture2D tex)
    {
        Color[] clear = new Color[tex.width * tex.height];
        tex.SetPixels(clear);
    }

    private static void FillCircle(Texture2D tex, int cx, int cy, int radius, Color c)
    {
        for (int y = cy - radius; y <= cy + radius; y++)
            for (int x = cx - radius; x <= cx + radius; x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                    SafeSetPixel(tex, x, y, c);
    }

    private static void FillRect(Texture2D tex, int x0, int y0, int w, int h, Color c)
    {
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                SafeSetPixel(tex, x, y, c);
    }

    private static void FillTriangle(Texture2D tex, int x0, int y0, int x1, int y1, int x2, int y2, Color c)
    {
        int minX = Mathf.Min(x0, Mathf.Min(x1, x2));
        int maxX = Mathf.Max(x0, Mathf.Max(x1, x2));
        int minY = Mathf.Min(y0, Mathf.Min(y1, y2));
        int maxY = Mathf.Max(y0, Mathf.Max(y1, y2));

        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                if (PointInTriangle(x, y, x0, y0, x1, y1, x2, y2))
                    SafeSetPixel(tex, x, y, c);
    }

    private static void FillDiamond(Texture2D tex, int cx, int cy, int rx, int ry, Color c)
    {
        for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = Mathf.Abs(x - cx) / (float)rx;
                float dy = Mathf.Abs(y - cy) / (float)ry;
                if (dx + dy <= 1f)
                    SafeSetPixel(tex, x, y, c);
            }
    }

    private static bool PointInTriangle(int px, int py, int x0, int y0, int x1, int y1, int x2, int y2)
    {
        float d1 = Sign(px, py, x0, y0, x1, y1);
        float d2 = Sign(px, py, x1, y1, x2, y2);
        float d3 = Sign(px, py, x2, y2, x0, y0);
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }

    private static float Sign(int x1, int y1, int x2, int y2, int x3, int y3)
    {
        return (x1 - x3) * (y2 - y3) - (x2 - x3) * (y1 - y3);
    }

    private static void SafeSetPixel(Texture2D tex, int x, int y, Color c)
    {
        if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
            tex.SetPixel(x, y, c);
    }

    // ==================== 文件操作 ====================

    private static void CreateSprite(string relativePath, int width, int height, Color baseColor, DrawFunc drawFunc)
    {
        string fullPath = $"{SPRITE_DIR}/{relativePath}.png";
        string dir = Path.GetDirectoryName(fullPath);
        EnsureDirectory(dir);

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        drawFunc(tex, baseColor);

        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(fullPath, bytes);
        Object.DestroyImmediate(tex);

        // 导入设置
        AssetDatabase.ImportAsset(fullPath);
        var importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    /// <summary>
    /// 加载已生成的占位精灵
    /// </summary>
    public static Sprite LoadPlaceholder(string relativePath)
    {
        string path = $"{SPRITE_DIR}/{relativePath}.png";
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// 检查是否已生成占位精灵
    /// </summary>
    public static bool HasPlaceholders()
    {
        return File.Exists($"{SPRITE_DIR}/Characters/Lux.png");
    }
}
