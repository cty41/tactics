# Roguelike地图玩法开发计划

> **版本**: v3.0
> **日期**: 2026-05-21
> **状态**: 进行中（Phase 3 已完成，Phase 4 进行中）
> **关联设计**: [roguelike-map-gameplay-design.md](../docs/roguelike-map-gameplay-design.md)
> **关联算法**: [ftl-style-map-generation-algorithm.md](../docs/ftl-style-map-generation-algorithm.md)
> **阶段计划**:
> - Phase 3: [roguelike-map-phase3-统一结算与整局收口计划.md](./roguelike-map-phase3-统一结算与整局收口计划.md) - 已完成
> - Phase 4: [roguelike-map-phase4-数据约定清理与回归计划.md](./roguelike-map-phase4-数据约定清理与回归计划.md) - 进行中

---

## Background

### 当前问题

项目已有基础Roguelike地图系统（7种节点类型、层式生成、UI显示、战斗桥接），但非战斗节点无实际玩法：

- Store/Treasure/Mystery/RestSite 点击后无事发生
- 地图探索缺乏风险-回报选择和路径规划策略
- 无资源管理、无事件系统
- 无事件编辑器工具，策划难以批量创建事件内容

### 目标

实现FTL风格自由探索Roguelike地图玩法，包括节点状态系统（有限视野迷雾）、BG3式属性判定事件系统、非战斗节点玩法、事件编辑器工具。

### 预期收益

- 玩家每次Run有独特的探索体验
- BG3式属性判定事件提供构筑策略深度
- 低金币经济（≤50/局）让每个决策都有分量
- 事件编辑器让策划可视化管理事件内容
- 单局时长控制在15-25分钟

---

## Scope

### In Scope

1. FTL风格地图生成（网格布局、距离约束连接、BFS连通性验证）
2. 节点状态系统（有限视野迷雾：未揭示/已揭示/可到达/已访问）
3. 事件系统（JSON配置 + BG3式属性判定 + 成功率显示）
4. 事件编辑器工具（独立计划 — 详见 [roguelike-event-editor-开发计划.md](roguelike-event-editor-开发计划.md)）
5. 非战斗节点玩法（商店/宝藏/休息站 — 低金币经济）
6. Boss胜利结算（单个最终Boss，击败后展示Run成果）
7. 资源系统（低金币经济：单局≤50金）

### Out of Scope

- 元进度系统（局外解锁/永久升级）
- 多人协作/对战
- 成就系统
- 音效和特效


---

## Tasks

### Phase 1: 基础框架 + 事件编辑器（Week 1-2）

#### Task 1: 地图生成器验证与优化

- **目标**: 验证现有FTL风格地图生成器，确保符合设计规范
- **输出**: 验证报告，必要时微调生成参数
- **现有文件**: `RoguelikeMapGenerator.cs`（已实现FTL风格网格布局）
- **验收标准**:
  - [ ] 生成5×4网格布局（20节点）
  - [ ] 前向/侧向/后向连接正确
  - [ ] BFS连通性验证通过
  - [ ] Boss节点固定在最右侧列
  - [ ] 每节点至少2个连接
  - [ ] 节点分布概率：MinorEnemy 30%, Elite 15%, Mystery 25%, Store 10%, Treasure 10%, Rest 10%

#### Task 2: 节点状态与视野系统

- **目标**: 实现FTL风格有限视野迷雾系统
- **输出**: 扩展的 `RoguelikeMapNode`, `MapRevealSystem`, `NodeStateManager` 重写
- **修改文件**: `RoguelikeMapNode.cs`, `NodeStateManager.cs`, `RoguelikeMapUIController.cs`
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/MapRevealSystem.cs` — 视野范围计算
- **验收标准**:
  - [ ] 节点支持4种状态：未揭示（灰色问号）、已揭示（真实图标）、可到达（高亮边框）、已访问（半透明不可点击）
  - [ ] 视野范围基于距离值计算（从当前节点沿连接路径遍历）
  - [ ] 已访问节点永久可见
  - [ ] 当前节点的连接邻居可见（Reachable状态）
  - [ ] 视野范围外的未访问节点不可见（Unrevealed状态）
  - [ ] UI正确显示4种节点状态和当前玩家位置标记

#### Task 3: 节点状态与视野系统

> **已合并到 Task 2** — 节点状态管理和视野系统合并为一个任务。详见 Task 2。

#### Task 4: 事件编辑器（独立计划）

> 事件编辑器已分离为**独立开发计划**，不与本计划的任务共享依赖。
> 
> **设计文档**: [roguelike-event-editor-design.md](../docs/roguelike-event-editor-design.md)  
> **开发计划**: [roguelike-event-editor-开发计划.md](roguelike-event-editor-开发计划.md)
> 
> **开发策略**: 事件编辑器与主计划完全并行。事件系统的开发（Task 6-8）前期使用手工编写的JSON文件先行开发，
> 待编辑器完成后切换为编辑器导出。两边的数据接口通过 `Assets/Tactics/Resources/Events/*.json` 对接。

#### Task 5: 非战斗节点基础框架（低金币版）

- **目标**: 实现Store/Treasure/RestSite的基础交互，所有金币数值缩小10倍
- **输出**: `NodeInteractionManager`, Store/Treasure/RestSite的Handler, 基础UI
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Interaction/NodeInteractionManager.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Interaction/StoreNodeHandler.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Interaction/TreasureNodeHandler.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Interaction/RestSiteNodeHandler.cs`
- **修改文件**: Boss节点交互逻辑（仅"继续探索"/"返回避难所"2选项）
- **验收标准**:
  - [ ] 点击Store节点弹出商店UI（基础占位界面）
  - [ ] 点击Treasure节点直接获得奖励（金币2-5，弹出提示）
  - [ ] 点击RestSite节点弹出选项UI（休息/训练/冥想，占位界面）
  - [ ] 点击Boss节点进入战斗，胜利后进入胜利结算界面
  - [ ] 每个非战斗节点只能交互一次，已访问后变半透明

---

### Phase 2: 事件系统（Week 2-3）

#### Task 6: 事件数据层

- **目标**: 创建BG3式事件配置系统和数据表（通过事件编辑器创建）
- **输出**: `RoguelikeEvent` JSON结构, `EventOption`/`EventCondition`/`EventResult` 数据结构, 基础事件JSON（编辑器导出）
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Events/RoguelikeEvent.cs`（数据模型）
  - `Assets/Tactics/Scripts/RoguelikeMap/Events/EventOption.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Events/EventCondition.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Events/EventResult.cs`
  - `Assets/Tactics/Resources/Events/*.json`（统一事件池，通过编辑器创建至少10个）
- **事件JSON结构**:
  ```json
  {
    "eventId": "cursed_chest_001",
    "title": "被诅咒的宝箱",
    "description": "...",
    "options": [
      {
        "optionId": "smash",
        "text": "暴力撬开",
        "attribute": "Strength",
        "successRate": 50,
        "success": {"type": "gold", "amount": 5},
        "failure": {"type": "damage", "amount": 8, "target": "self"}
      }
    ]
  }
  ```
- **验收标准**:
  - [ ] JSON格式正确，包含 eventId/title/description/options
  - [ ] 每个选项支持 attribute/successRate/condition/success/failure 字段
  - [ ] 支持多种结果类型（gold/item/equip/buff/heal/damage/nothing/battle）
  - [ ] 至少10个事件，覆盖单属性/多属性/团队协作类型

#### Task 7: BG3式属性判定系统

- **目标**: 实现属性成功率计算和团队属性检查
- **输出**: `AttributeCheckSystem`, `EventManager`（含判定逻辑）
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Events/AttributeCheckSystem.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Events/EventManager.cs`
- **验收标准**:
  - [ ] 成功率计算公式: `基础成功率 + (属性值 - 10) × 5%` 正确实现
  - [ ] 属性值6→20%, 8→30%, 10→40%, 14→60%, 18→80%, 20→90%
  - [ ] 无属性选项自动成功（successRate=100）
  - [ ] 团队成员属性和职业正确读取

#### Task 8: 事件UI

- **目标**: 实现BG3式事件交互界面（显示成功率）
- **输出**: `EventUIController`, 事件弹窗UXML/USS
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/UI/EventUIController.cs`
  - `Assets/Tactics/Arts/UI/EventPanel.uxml`
  - `Assets/Tactics/Arts/UI/EventPanel.uss`
- **验收标准**:
  - [ ] 显示事件标题、沉浸式描述文本
  - [ ] 显示2-4个选项按钮，每个显示：动作描述 + 关联属性 + 成功率百分比
  - [ ] 成功率直观展示（如绿色65% / 黄色45% / 红色25%）
  - [ ] 选择后展示成功/失败结果（奖励/惩罚描述 + 动画过渡）
  - [ ] 无属性选项显示为"自动成功"（绿色100%）

#### Task 9: 商店系统（金币精简版）

- **目标**: 实现低金币经济下的完整商店功能
- **输出**: `ShopManager`, 商品池配置, 购买逻辑, 完善商店UI
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Interaction/ShopManager.cs`
  - `Assets/Tactics/Arts/ScriptableObjects/ShopConfigs/DefaultShopConfig.asset`
- **修改文件**: `StoreNodeHandler.cs`, 商店UI相关
- **验收标准**:
  - [ ] 商品数量: 2-3个（精简）
  - [ ] 消耗品价格: 3-5金币；普通装备: 8-12金币；稀有装备: 15金币
  - [ ] 商品类型覆盖：装备(50%)/消耗品(30%)/技能书(20%)
  - [ ] 购买时检查金币余额，购买后物品进入背包，金币正确扣除
  - [ ] 访问后商店变已访问状态

#### Task 10: 休息站系统

- **目标**: 实现完整的休息站功能
- **输出**: `RestSiteManager`, 休息选项逻辑, 效果应用
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Interaction/RestSiteManager.cs`
- **修改文件**: `RestSiteNodeHandler.cs`
- **验收标准**:
  - [ ] 选项正确显示：休息(恢复30%HP) / 训练(提升1点属性) / 冥想(恢复全部MP)
  - [ ] 训练：可选择提升的属性（力量/敏捷/体质/智力/魅力）
  - [ ] 访问后休息站变已访问状态

---

### Phase 3: 内容填充与 Run 收口（Week 3-4）

> Phase 3 已拆分为独立计划，详见：
>
> - [roguelike-map-phase3-统一结算与整局收口计划.md](./roguelike-map-phase3-统一结算与整局收口计划.md)
>
> 本阶段专注于：
> - 统一 Roguelike 战斗结算入口
> - Roguelike run-end 总结
> - `RunSummary` 与结算上下文收束
> - 事件内容填充与 Phase 3 级别的数值平衡

---

### Phase 4: 数据约定清理与回归（Week 4）

> Phase 4 已拆分为独立计划，详见：
>
> - [roguelike-map-phase4-数据约定清理与回归计划.md](./roguelike-map-phase4-数据约定清理与回归计划.md)
>
> 本阶段专注于：
> - 事件资源契约统一
> - 节点配置接口统一
> - 奖励/效果结果模型收束
> - RoguelikeMap 与非 Roguelike 战斗回归

---

## 依赖关系

```
Task 1 (地图生成器验证)
  ├── Task 2 (节点状态/视野系统) ── Task 3 (非战斗节点)
  │     │                              ├── Task 9 (商店)
  │     │                              ├── Task 10 (休息站)
  │     │                              └── Task 13 (Boss结算)
  │     └── Task 6 (事件数据层) ── Task 7 (属性判定) ── Task 8 (事件UI)
  │           └── Task 11 (事件扩展)
  └── Task 12 (数值平衡) ── 依赖所有功能完成

> **事件编辑器（Task 4/14）**: 已分离为独立计划，详见 [roguelike-event-editor-开发计划.md](roguelike-event-editor-开发计划.md)。
> 事件系统（Task 6-8）通过 JSON 文件接口与编辑器对接，两方可完全并行开发。
```

## Risks & Open Questions

| 风险 | 等级 | 缓解措施 |
|------|------|----------|
| 事件编辑器开发复杂度高：已分离到独立计划 | 高 | 详见 [roguelike-event-editor-开发计划.md](roguelike-event-editor-开发计划.md)；事件系统前期用手工JSON开发 |
| 低金币经济平衡困难：≤50金需精确数值设计 | 中 | Task 12提供调试工具，导出金币流数据人工评审 |
| UI工作量大：事件/商店/休息站/Boss结算都需要独立UI | 中 | 使用UI Toolkit复用组件，先占位后细化 |
| 现有代码兼容：修改多个现有文件可能引入回归 | 低 | 扩展现有类而非重构核心逻辑，保持向后兼容 |

### Open Questions

1. **商店刷新机制**：是否需要支持"付费刷新商品"功能？（当前设计：无，精简2-3个商品）
2. **事件连锁**：是否需要支持"事件A的选择影响后续事件B"的跨事件状态？（v2暂不实现）
3. **Boss战难度**：Boss的多阶段机制是否依赖战斗系统的新特性？
4. **Run-end Defeat**：Phase 3 是否只交付 Victory 收口，Defeat 作为后续扩展？（当前计划：Phase 3 先交付普通战斗胜利 + Boss 胜利的 run-end 收口，结构上兼容 Defeat）

---

## Assumptions

1. **允许修改现有文件**：`RoguelikeMapGenerator.cs`, `RoguelikeMapUIController.cs`, `RoguelikeMapNode.cs`, `RoguelikeBattleReturnHandler.cs`
2. **允许新增命名空间**：`Tactics.RoguelikeMap.Events`, `Tactics.RoguelikeMap.Interaction`, `Tactics.Editor.RoguelikeEventEditor`
3. **技术栈**：Unity 2022+, UI Toolkit (UXML/USS), JSON配置, Editor Window API
4. **事件配置**：使用JSON文件，通过事件编辑器导出（不再手工编写）
5. **商店商品**：使用现有Item系统（ItemData/EquipmentData）
6. **队伍数据**：`CharacterDefinition` 包含HP/MP和属性（力量/敏捷/体质/智力/魅力），可在Roguelike运行中修改
7. **金币系统**：独立管理，不依赖已有的Inventory系统（如有），单局≤50金
8. **无战斗地形影响**：v3延续此决策，不实现战斗地形影响

---

## 整体验收标准

- [ ] 可以开始一局完整的Roguelike Run（FTL风格自由探索地图）
- [ ] 可以探索FTL风格自由星图，体验非线性路径选择
- [ ] 可以触发事件并看到BG3式属性判定选项（显示成功率百分比）
- [ ] 可以使用编辑器导出的JSON事件文件（事件编辑器验收详见[独立计划](roguelike-event-editor-开发计划.md)）
- [ ] 可以在商店浏览和购买物品（金币正确扣除和获得，价格3-15金）
- [ ] 可以在休息站执行 `rest`
- [ ] 普通战斗胜利和 Boss 胜利后，都在统一战斗结算链结束后进入 Roguelike run-end 总结，展示 Run 成果
- [ ] 单局总金币获取≤50
- [ ] 单局时长15-25分钟（3次测试均值）
- [ ] 每次Run体验不同（节点分布、事件、商品随机）

---

## 关联文档

- **设计文档**: [roguelike-map-gameplay-design.md](../docs/roguelike-map-gameplay-design.md)
- **项目文档组织规范**: [project-doc-organization](../../skills/project-doc-organization/SKILL.md)
- **前置计划**: [战斗结算与奖励计划.md](战斗结算与奖励计划.md)
- **前置计划**: [地形效果计划.md](地形效果计划.md)
