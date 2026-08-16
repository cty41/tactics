# Godot Phase 7B–8E：行动、速度、LOS 与事件处理者优化

## Summary

- Move 使用后由 Application availability 驱动按钮置灰并阻止重复 targeting。
- 保持 Unity MP 合同：Charisma 决定 MaxMP，Intelligence 决定回合结束恢复，Charisma 决定战后恢复。
- 通过非 Catalog 的 Godot playable speed profile 调整敌方速度：Ranged 6、Charger 6、Support 5、AOE 5、EliteCharger 7、ElitePoisonCaster 6。
- LOS 返回射线、阻挡格和阻挡类型；尸体、死亡单位与落地长矛永不阻挡，Preview/AI/Transition 共用查询。
- Mystery 进入时从存活正式队员中确定性随机并持久化一个处理者，所有固定选项均由该角色检定。

## Current State

- Core 已记录 `HasMovedThisTurn`，Godot Move 按钮尚未消费该状态。
- 玩家 Speed 均为 5，当前敌人 Speed 为 6–12，导致敌方普遍先手。
- LOS 拒绝只有 `line_of_sight_blocked`，无法观察实际 blocker。
- 当前 Godot Mystery 允许 option/character 组合；本计划有意改为事件级固定处理者。

## Implementation

1. 新增 `BattleUiMoveAvailability`，Application 在进入 Move targeting 前校验；Godot 按钮消费相同结果。
2. 增加 MP 派生与恢复回归测试，并在现有角色/Inventory 说明中明确属性职责；不改公式。
3. 新增 Adapter-owned `godot-playable-enemy-speed-v1`，仅在可玩 Run BattleState 构建时覆盖敌方速度，冻结 Unit Resource 与 Oracle 保持原值。
4. 将 LOS 查询升级为结构化结果，包含 ray、blocking cell/kind/unit；保留 Bone Spear 首敌命中语义和 Unity supercover 规则。
5. MapState 增加可选 Mystery adjudicator assignment；使用 `event-adjudicator:<node-id>:<event-id>` 稳定随机流，从存活正式队员中选择并在选项前持久化。效果目标仍由事件合同决定。

## Test Plan

- Move 成功后立即不可用，失败/取消不消耗，下个自身回合恢复。
- Mage MaxMP15/turn+6、Necromancer MaxMP18/turn+5，战后按 Charisma 恢复。
- Playable speed profile 精确匹配计划值，冻结内容不漂移，首轮不再由全体敌人垄断。
- 存活中间单位阻挡；尸体、死亡单位和 Dropped Spear 不阻挡；LOS 诊断精确报告 blocker；Preview/AI/Transition 一致。
- Mystery 同 seed/node/event 得到同一处理者；所有选项使用该角色；Reload 不重抽，死亡角色和召唤物不进入候选。
- Catalog 保持 131；完整 `Verify-GodotMigration.ps1`、双 renderer、OKF、UID 与 whitespace 全绿。

## Assumptions and Handoff

- Save 保持 V5；新增字段为可选兼容字段，旧档在首次进入事件时确定性补建。
- 不修改 Unity 冻结内容、玩家技能数值、AI 评分、正式 VFX/Audio 或程序集边界。
- 自动测试使用隔离存档，不覆盖用户主档或 backup。
- 完成后将长期结论并入权威设计、同步受影响 OKF、更新人工验收账本并删除本计划，由 Git 保存历史。
