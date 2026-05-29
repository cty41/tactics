# RoguelikeMap Phase 3 — 统一结算与整局收口计划

> **版本**: v1.0
> **日期**: 2026-05-27
> **状态**: 已完成
> **关联设计**: [roguelike-map-gameplay-design.md](../docs/roguelike-map-gameplay-design.md)
> **关联主计划**: [roguelike-map-gameplay-开发计划.md](./roguelike-map-gameplay-开发计划.md)

---

## Background

### 当前问题

到 Phase 3 时，RoguelikeMap 的主要缺口不再是单个节点玩法，而是战斗后的整局收口仍未统一：

- 普通战斗、Boss 战、失败态的结算路径还没有完全收束到同一条主链
- Roguelike 战斗返回时，仍容易在奖励/升级/技能选择结束前过早离开战斗场景
- Boss 结算仍带有明显的 Boss-only 语义，不适合作为整局 run-end 收口
- `RunSummary` 已有基础字段，但还不足以稳定承载统一的 run-end 展示

### 目标

完成 Roguelike 的统一战斗结算与整局收口：

- 所有 Roguelike 战斗先进入统一 `BattleSettlement`
- 普通战斗胜利与 Boss 胜利都在整条结算 UI 链结束后进入 Roguelike run-end 总结
- run-end 总结结束后统一返回 `Home`
- 为后续失败态总结保留数据结构和 UI 语义扩展位

### 预期收益

- Roguelike 的战斗后体验一致，不再分裂为普通战斗链和 Boss-only 链
- 地图推进、升级/技能选择、run-end 总结的顺序清晰可验证
- Phase 4 的回归与数据清理有稳定边界

---

## Scope

### In Scope

1. 统一 Roguelike 战斗结算入口
2. 收束普通战斗 / Boss 胜利 / 失败态的结算分流规则
3. 将 Boss-only 结算升级为 Roguelike run-end 总结
4. 扩展 `RunSummary` 与 Roguelike 结算上下文
5. 完成进入 run-end 总结 / 返回 Home 的最终触发时机收口

### Out of Scope

1. Treasure / Store / Mystery / RestSite 的玩法扩展
2. 事件编辑器能力建设
3. 多区域地图、元进度
4. 非 Roguelike 战斗系统的大规模重构

---

## Tasks

### Task 1: 统一 Roguelike 战斗结算入口

- **目标**: 让所有 Roguelike 战斗都先经过统一 `BattleSettlement` 主链
- **输入**: `RoguelikeBattleReturnHandler`, `BattleSettlementCoordinator`, `BattleSettlementFlow`
- **输出**: 统一的战斗结算入口与时序
- **验收标准**:
  - [x] 普通战斗胜利、Boss 胜利、失败态都先进入 `BattleSettlement`
  - [x] `BattleSettlementFlow` 成为所有战后 UI 的唯一编排层
  - [x] 回 `Home` / 回地图 的触发点固定在整条结算 UI 链结束后

### Task 2: 收束 Roguelike 结算分流规则

- **目标**: 明确结算完成后的去向，不再让分流逻辑散落在多个类里
- **输入**: 战斗节点类型、战斗结果、当前 run 状态
- **输出**: 可执行的 Roguelike 结算分流规则
- **验收标准**:
  - [x] 普通战斗胜利：结算完成后提交节点，并进入 run-end 总结，结束后返回 `Home`
  - [x] Boss 胜利：结算完成后进入 run-end 总结，结束后返回 `Home`
  - [x] 失败态：若采用统一结算，则同样在结算链末尾进入 run-end 失败总结并返回 `Home`
  - [x] 独立战斗测试不误开 RoguelikeMap，不误消费 Roguelike 收口逻辑

### Task 3: 升级 Boss-only 结算为 Roguelike run-end 总结

- **目标**: 把现有 `BossVictoryUIController` 升级成面向整局总结的控制器
- **输入**: `BossVictoryUIController`, `RunSummary`
- **输出**: Roguelike run-end 总结 UI
- **验收标准**:
  - [x] 不再新增并行的 `RunSummaryUIController`
  - [x] UI 至少支持 Victory，并在结构上兼容 Defeat
  - [x] 面板显示：金币、击败敌人数、访问节点数、完成事件数、装备/物品收获、Boss 状态
  - [x] 返回目标为 `Home`，不直接跳主菜单

### Task 4: 扩展 `RunSummary` 与结算上下文

- **目标**: 为整局收口提供稳定的数据真值
- **输入**: 节点奖励、战斗结果、事件结果、当前 run 统计
- **输出**: 扩展后的 `RunSummary` 与 Roguelike 结算上下文
- **验收标准**:
  - [x] `RunSummary` 持续累计金币、敌人击败数、访问节点数、事件完成数、装备/物品收获
  - [x] `RunSummary` 增加 `RunOutcome`
  - [x] Roguelike 结算上下文至少表达：当前节点类型、是否 Boss、是否 run-ending、结算后目标去向

### Task 5: 完成 Phase 3 回归验证

- **目标**: 验证统一结算与 run-end 收口没有破坏现有主流程
- **输入**: Roguelike 战斗流程、Boss 节点、升级/技能选择链
- **输出**: 可复现的回归验证结果
- **验收标准**:
  - [x] 普通战斗胜利，无升级时：奖励面板结束后进入 run-end 总结，再返回 `Home`
  - [x] 普通战斗胜利，有升级/技能选择时：必须等最后一个确认结束后进入 run-end 总结，再返回 `Home`
  - [x] Boss 胜利后：`BattleSettlement` -> run-end 总结 -> `Home`
  - [x] 地图节点推进、pending battle 清理、Loading 链不被破坏

---

## Risks & Open Questions

- 失败态是否在本阶段完整交付 UI，还是先只保证 Victory 收口；当前建议是 Victory 必做，Defeat 结构兼容
- `BattleSettlementCoordinator` 里保存时机是否还要进一步后移；若保留当前写法，需要在 Phase 4 回归里重点覆盖
- Boss 战难度是否依赖战斗系统后续新特性；若依赖未落地，Phase 3 只能先收口流程，不承诺最终数值平衡

---

## Assumptions

1. Phase 3 的主题是“统一结算与整局收口”，不再混入新节点玩法开发
2. `RestSite` 已固定为 rest-only
3. Roguelike 战后返回目标场景是 `Home`
4. Phase 3 优先交付 Victory 收口，并在结构上兼容 Defeat
