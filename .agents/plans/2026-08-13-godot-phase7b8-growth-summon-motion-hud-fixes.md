# Phase 7B–8D 定向修复：成长候选、召唤攻击、连续移动、施法朝向与紧凑状态条

## Summary

在 `migration/godot`、基线 `fe92efd5` 上修复：Unity parity 的确定性技能三选一、Skeleton Warrior 近战、Godot Playable Slice 的 Fire Demon 魔法攻击、恒速移动、格子目标施法朝向和精确 `60x18` HP/MP Overlay。播放期间输入锁的人工验收后移；本轮保持合并人工验收 pending。

## Current State

- Unity `PureRunProgression.SkillChoiceCount` 为 3，`BuildSkillChoices` 混合新技能与下一等级升级；当前 Godot UI 直接列出全部合法候选。
- Unity 冻结 Unit DTO：Skeleton Warrior 引用 `MeleeAttack_Graph_Ability`；Fire Demon `_abilityConfigs` 为 0。用户已批准 Godot 为 Fire Demon 绑定现有 Magic Attack，并明确记录差异。
- Godot Move 当前逐格使用 Sine Tween；Unity 根节点移动使用固定速度 `MoveTowards`。
- Summon `SkillUsedEvent` 目标是施法者自身，实际目标格位于同一 Transition 的 `UnitSummonedEvent.Cell`。
- 当前 Meter 测试只断言常量，未验证实例化后的实际 Control bounds。

## Implementation

1. 新增确定性三选一 offer 服务，按 Run seed、角色 ID 和升级等级稳定混合新技能/升级；同一 progression reload 后一致，完成事务只能消费 offer 内技能。UI 明示 Learn 与 Upgrade。
2. 统一动态召唤物技能映射：Skeleton Warrior→`skill.basic.melee`；Fire Demon→`skill.basic.magic`（Godot divergence）；其他召唤物不推断。
3. 以 committed battle events 为表现事实：召唤朝向使用 `UnitSummonedEvent.Cell`；普通目标与自施法维持既有合同。
4. Actor 根节点沿累计屏幕路径线性移动，格子边界不缓停；倍速只改变时间，不改变状态。
5. 用专用自绘 `GodotCompactUnitMeter` 固定 `60x18` bounds，跟随 Actor、即时对账 HP/MP，并在退出/reload 清理。

## Verification

- Core/Application：三选一稳定性、重复 Lv1 排除、Lv2 upgrade、offer 外选择拒绝、两类召唤物攻击。
- GdUnit：连续移动、召唤朝向、实际 Meter bounds/fill、overlay 清理与 reload。
- 完整运行 `Tools/migration/Verify-GodotMigration.ps1`、OKF impact/sync、diff/whitespace/敏感信息门禁。
- 人工复验仅覆盖三选一、两类召唤攻击、三格恒速移动、尸体召唤朝向、Meter 和 0.5x/4x；输入锁后移。

## Boundaries and Closing

- Catalog 125、Save V4、Unity DTO、技能数值和玩法事件顺序不变。
- 不迁移新 VFX、Audio、Lv3 或其他召唤物能力。
- 自动门禁后允许本地 scoped checkpoint，不 push；状态保持 `Generated/UnityOwned + manual_inventory_progression_and_presentation_qa_pending`。
- 最终人工验收后将长期结论并入权威设计/OKF，删除本 active plan，由 Git 历史保留。
