# RoguelikeMap Phase 4 — 数据约定清理与回归计划

> **版本**: v1.0
> **日期**: 2026-05-27
> **状态**: 进行中
> **关联设计**: [roguelike-map-gameplay-design.md](../design/roguelike-map-gameplay-design.md)
> **关联主计划**: [roguelike-map-gameplay-开发计划.md](./roguelike-map-gameplay-开发计划.md)
> **前置阶段**: [roguelike-map-phase3-统一结算与整局收口计划.md](./roguelike-map-phase3-统一结算与整局收口计划.md)

---

## Background

### 当前问题

到 Phase 4，RoguelikeMap 的主要风险不再是功能缺失，而是设计、文档和运行时代码的约定仍有漂移：

- 事件资源路径和实际加载方式不完全一致
- 节点配置接口在文档、编辑器、运行时之间还不够统一
- 奖励与效果展示仍有分散实现，后续维护成本高
- 统一结算、地图恢复、节点玩法都已经跨多个系统，需要专门的回归阶段

### 目标

用一整个阶段专门完成：

- 数据约定清理
- 文档与代码同步
- RoguelikeMap 主流程回归
- 非 Roguelike 战斗回归

### 预期收益

- 后续新增内容不会继续建立在漂移接口上
- 主要主流程具备可重复验证的稳定性
- 规划和实现边界清晰，避免把回归工作无限拖后

---

## Scope

### In Scope

1. 统一事件资源契约与加载方式
2. 统一节点配置接口风格
3. 收束奖励/效果结果模型
4. RoguelikeMap 主流程回归
5. 非 Roguelike 战斗回归
6. 设计文档与主计划同步

### Out of Scope

1. 新玩法功能
2. 新 UI 功能
3. 多区域扩展
4. 元进度系统

---

## Tasks

### Task 1: 统一事件资源契约

- **目标**: 消除事件文件路径与运行时加载方式的漂移
- **输入**: `EventManager`, `RoguelikeMapConfig`, 设计文档与主计划中的事件资源约定
- **输出**: 唯一、明确的事件资源接入规范
- **验收标准**:
  - [x] 文档不再把 `Assets/Tactics/Resources/Events/*.json` 写成唯一契约
  - [x] 当前运行时消费路径与文档描述一致
  - [x] 节点 `eventId`、区域随机事件池、`eventFiles` 接入方式的职责边界明确

### Task 2: 统一节点配置接口

- **目标**: 让 Treasure / Store / 未来扩展节点的配置消费方式保持一致
- **输入**: `treasureConfig`, `storeConfig`, 编辑器侧配置入口
- **输出**: 一致的节点配置接口约定
- **验收标准**:
  - [x] 文档、运行时、编辑器对节点配置字段的命名和语义一致
  - [x] 旧的占位字段或过时注释被清理
  - [x] 后续新增节点配置不需要重新定义一套完全不同的风格

### Task 3: 收束奖励/效果结果模型

- **目标**: 降低节点效果和结算效果的分散实现
- **输入**: Mystery / Treasure / Store / Battle / run-end 的结果展示与状态写回逻辑
- **输出**: 更统一的奖励/效果结果约定
- **验收标准**:
  - [x] 不再依赖大量各节点独立拼接字符串做最终展示
  - [x] 金币、装备、Buff、HP/MP、统计类结果的写回和展示边界清晰
  - [x] `RunSummary` 和节点奖励模型之间没有明显重复统计或遗漏

### Task 4: RoguelikeMap 主流程回归

- **目标**: 对地图主流程做一轮完整回归
- **输入**: 地图生成、节点推进、战斗返回、run-end 收口
- **输出**: 可执行的主流程回归结果
- **验收标准**:
  - [ ] 新开一局可稳定从起点推进到 Boss
  - [ ] 非战斗节点结果不会因重新选中或切场景而回滚
  - [ ] 战斗返回后节点进度、当前位置、可达节点保持正确
  - [ ] Boss 胜利后统一结算与 run-end 收口正常

### Task 5: 非 Roguelike 战斗回归

- **目标**: 确认 Roguelike 改动没有破坏独立战斗流程
- **输入**: 独立战斗测试场景、`battle win` 结算链
- **输出**: 非 Roguelike 战斗回归结果
- **验收标准**:
  - [ ] 独立战斗测试不会误开 RoguelikeMap
  - [ ] 独立战斗测试不会误进入 Roguelike run-end 总结
  - [ ] 统一 `BattleSettlement` 的升级/技能选择链继续可用

### Task 6: 文档同步清理

- **目标**: 把设计文档、主计划、分阶段计划中的旧约束同步清理掉
- **输入**: `.agents/docs/design/roguelike-map-gameplay-design.md`, 主计划, Phase 3/4 子计划
- **输出**: 一致的文档真相源
- **验收标准**:
  - [x] 设计文档不再保留 `RestSite` 的训练/冥想旧描述
  - [x] Phase 3/4 与主计划引用一致
  - [x] 事件资源路径、run-end 收口目标、返回场景目标的表述一致

---

## Risks & Open Questions

- 若前置阶段仍存在未收束的 runtime state 生命周期问题，Phase 4 回归会暴露大量连锁问题，需要优先回滚到 Phase 3 修复
- 奖励/效果结果模型若收束过度，可能把已有轻量节点处理复杂化；建议只做最小统一，不做抽象过度

---

## Assumptions

1. Phase 4 是“清理与回归阶段”，不是新增功能阶段
2. 前置的 Phase 3 已至少完成 Victory 收口
3. 文档漂移的修复优先级与代码回归同级，不作为可选收尾项
