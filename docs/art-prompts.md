# Double Forward 美术委托提示词

给外部图像模型（GPT / Midjourney / SD 等）用的提示词。当前仓库里的美术全部是
**程序化生成的临时占位**（`GameArtGenerator` / `CharacterAnimationGenerator` /
`BackgroundArtGenerator`），本文档的提示词用于产出正式资源来替换它们。

**用法**：把「通用风格约束」拼在每条具体提示词前面一起发给模型；拿到图后按每节
末尾的「落盘」放到对应路径，再跑对应的导入命令。

---

## 0. 通用风格约束（每条提示词都要带）

```
Pixel art, 2D side-scrolling platformer game asset.
Limited palette, hard pixel edges, no anti-aliasing, no dithering gradients.
Transparent background (PNG with alpha), no drop shadow, no ground plane,
no text, no watermark, no border frame.
Single flat orthographic view, lit from the upper-left.
Art direction: "light vs shadow" — two complementary characters,
warm gold light against cool violet darkness. Mysterious, calm, not gory.
```

**配色基准**（和现有工程内资源一致，请沿用）：

| 用途 | 颜色 |
|---|---|
| Lux 主色 / 高光 | `#FFD14D` / `#FFF7BF` |
| Nox 主色 / 高光 | `#5C2994` / `#B873F2` |
| 影墙砖 / 缝隙 | `#33174D` / `#1A0A29` |
| 天空 顶 / 地平线 | `#141A38` / `#4D5785` |
| 树林剪影 | `#173328` |

---

## 1. 角色精灵表（优先级最高）

工程里角色目前是 32×48 的程序化小人，**这是最该替换的部分**。

### Lux（光）

```
[通用风格约束]
A sprite sheet of a slender humanoid figure made of warm golden light.
Hooded cloak with softly glowing edges, face in shadow with two calm
glowing eyes. Silhouette must stay readable at 32x48 pixels.

Sheet layout: a single horizontal strip, 8 frames, each frame exactly
32x48 pixels, uniform cell size, frames evenly spaced, no gaps, no labels.
Frame order: idle-1, idle-2, run-1, run-2, run-3, run-4, jump, fall.
Run cycle must read as a clear walk/run gait with alternating legs and
counter-swinging arms. Jump = knees tucked, arms up. Fall = legs apart,
arms out.
```

### Nox（影）

同上，把角色描述换成：

```
A slender humanoid figure made of living shadow, deep violet body with a
bright violet rim-light along the left edge, wisps of dark smoke trailing
from the shoulders, two pale glowing eyes.
```

**落盘**：切成单帧 PNG，命名严格如下，放到 `Assets/Art/Characters/`：

```
Lux_Idle_0.png  Lux_Idle_1.png
Lux_Run_0.png   Lux_Run_1.png   Lux_Run_2.png   Lux_Run_3.png
Lux_Jump_0.png  Lux_Fall_0.png
（Nox_ 同名规则）
```

导入设置：Sprite / **Pixels Per Unit = 16** / Filter = Point / 关闭压缩。
放好后跑一次，动画剪辑会自动重建：

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.45f1/Editor/Unity.exe" -batchmode -nographics -projectPath "F:\AI\project\Double forward" -executeMethod CharacterAnimationGenerator.GenerateAll -quit -logFile "F:\AI\project\Double forward\Logs\charanim.log"
```

> ⚠️ 该命令会**重新生成程序化帧并覆盖**同名文件。若要保留外部美术，
> 先把 `CharacterAnimationGenerator.Frame()` 里的写文件那几行注释掉，
> 只保留 `WriteClip` 的部分。

---

## 2. 背景三层视差

三张图分别对应 `ParallaxLayer_0/1/2`，**必须能左右无缝平铺**。

### 远景天空（`BgSky.png`，128×96，PPU 8）

```
[通用风格约束]
A seamless tileable night sky background for a 2D platformer.
Vertical gradient from deep indigo at the top to dusty blue-violet at the
horizon. A soft warm golden glow low on the right side, like a distant sun
that never rises. A few faint stars. No ground, no objects, no clouds.
Must tile seamlessly left-to-right.
```

### 中景远山（`BgHills.png`，192×64，PPU 8）

```
[通用风格约束]
A seamless tileable silhouette of distant rolling hills and mountain
ridges, two overlapping layers, near-flat dark blue-violet silhouettes
(#2B3557 front, #1F2742 back), no texture detail, no trees.
Transparent above the ridge line. Must tile seamlessly left-to-right.
```

### 近景树林（`BgTrees.png`，192×80，PPU 8）

```
[通用风格约束]
A seamless tileable row of coniferous forest silhouettes, very dark
green-teal (#173328), varied tree heights, thin trunks visible at the
bottom, transparent sky above. Must tile seamlessly left-to-right.
```

### 云（`BgCloud.png`，64×24，PPU 12）

```
[通用风格约束]
A single soft wispy cloud, pale blue-white, semi-transparent, soft edges,
isolated on a fully transparent background. No sky, no other elements.
```

**落盘**：`Assets/Resources/Art/`，然后跑：

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.45f1/Editor/Unity.exe" -batchmode -nographics -projectPath "F:\AI\project\Double forward" -executeMethod BackgroundArtGenerator.GenerateAndApply -quit -logFile "F:\AI\project\Double forward\Logs\bgart.log"
```

> 同样注意：该命令会覆盖同名 PNG。要保留外部美术就只调用其中的
> `ApplyToLevel12()`。

---

## 3. 机关（尺寸必须严格遵守）

这几张的**世界尺寸由像素尺寸÷PPU决定**，改了尺寸会让碰撞体大小跟着变、
影响关卡可玩性（影墙矮于4单位，Lux 二段跳就能翻过去）。

| 文件 | 像素 | PPU | 世界尺寸 | 提示词要点 |
|---|---|---|---|---|
| `ShadowWallTile.png` | 24×160 | 40 | 0.6×4 | 深紫砖墙，砖缝清晰，左侧受光，边缘一圈亮紫描边，**竖向可平铺** |
| `PressurePlateArt.png` | 64×12 | 40 | 1.6×0.3 | 嵌在地里的金属压板，上缘高光，两端稍暗 |
| `LightSensorArt.png` | 36×36 | 40 | 0.9×0.9 | 菱形水晶，双切面，未激活偏灰、激活偏金（只画未激活态，激活由代码染色） |
| `GateDoorArt.png` | 32×160 | 40 | 0.8×4 | 石门/木门，横向分格，中央一列铆钉，左侧受光 |
| `GroundTile.png` | 32×32 | 16 | 2×2 | 顶部草皮、下部泥土颗粒，**四方向可平铺** |

示例（影墙）：

```
[通用风格约束]
A vertically tileable dark violet brick wall segment, 24x160 pixels.
Clear mortar lines between bricks, offset brick rows, lit from the left,
a thin bright violet outline around the whole slab. It should read as
"solid but ghostly" — a wall only a shadow creature can pass through.
```

**落盘**：`Assets/Resources/Art/`，然后重建关卡：

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.45f1/Editor/Unity.exe" -batchmode -nographics -projectPath "F:\AI\project\Double forward" -executeMethod CoopLevelBuilder.BuildLevel12 -quit -logFile "F:\AI\project\Double forward\Logs\build.log"
```

---

## 4. 能力特效

这三张会被代码**按玩法参数拉伸**，所以画成 1×1 世界单位（64×64 @ PPU 64），
内容要能承受横向拉伸（避免细密的横向花纹）。

| 文件 | 提示词要点 | 轴心 |
|---|---|---|
| `LightBeam.png` | 水平光束，中央近白亮芯，上下向外琥珀色衰减，右端渐隐 | **Left-Center**（重要，否则朝向翻转会错位） |
| `LightBridge.png` | 发光的实心平台板：受光顶面、琥珀板身、背光底面。**必须看起来能站人**，不能是雾 | Center |
| `ShadowZone.png` | 径向暗紫色雾团，中心浓、边缘柔和透明 | Center |

**落盘**：`Assets/Resources/Art/`。运行时由 `Resources.Load` 直接读取，
不需要跑任何命令，替换文件即生效。

---

## 5. 验收

替换任何资源后都要跑这两条，确认没有破坏玩法：

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.45f1/Editor/Unity.exe" -batchmode -nographics -projectPath "F:\AI\project\Double forward" -executeMethod HeadlessRuntimeTest.RunFromCommandLine -quit -logFile "F:\AI\project\Double forward\Logs\static.log"
```

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.45f1/Editor/Unity.exe" -batchmode -projectPath "F:\AI\project\Double forward" -executeMethod AutoPlayIntegrationTest.RunFromCommandLine -logFile "F:\AI\project\Double forward\Logs\play.log"
```

当前基线：**静态 102/102、Play 56/56**。静态验证里已有断言会盯住尺寸相关的
回归（机关摆放、动画帧数），换图后如果这两个数字掉了，多半是尺寸或 PPU 没对上。
