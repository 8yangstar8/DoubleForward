# 双人合作连携 Play 测试设计

## 目标

为现有 `AbilityComboSystem` 增加真实运行时的 Play 集成测试，验证 Lux 与 Nox 的技能组合可以在正确条件下触发，并在无效条件下保持不触发。

## 范围

本次只覆盖 `AbilityComboSystem` 的双人连携机制。已有的 `CoopAbilitySystem` 合体大招、UI、美术、关卡和输入映射不在本次变更范围内。

## 设计

测试扩展既有的 `Assets/Scripts/Editor/AutoPlayIntegrationTest.cs`，沿用其场景启动与双玩家初始化流程。测试通过真实 `AbilityUsedEvent` 驱动 Lux 与 Nox 的技能记录，监听 `AbilityComboSystem.OnComboTriggered`，并通过公开状态验证结果。

覆盖四项行为：

1. Lux 的 `light_beam` 与 Nox 的 `shadow_zone` 在连携窗口内、且合作能量充足时，触发 `light_dark_explosion`，消耗对应能量并增加触发计数。
2. 合作能量不足时，相同技能组合不得触发。
3. 两次技能使用超过连携窗口时不得触发。
4. 连携刚触发后的冷却期内，重复组合不得再次触发。

## 实现边界

- 不新增技能类型、默认组合或输入操作。
- 测试先行；只有在失败明确指出运行时缺口时，才对生产代码做最小修复。
- 不修改已存在的未提交 Unity/TMP 配置文件。

## 验收标准

- 新增的四项合作连携 Play 测试先失败、后通过。
- 静态验证和 Play 集成测试完整回归通过。
- `AbilityComboSystem` 默认组合仍保持现有行为。
