# Roguelike地图玩法开发计划

> **版本**: v2.0
> **日期**: 2026-05-12
> **状态**: 待执行
> **关联设计**: [roguelike-map-gameplay-design.md](../design/roguelike-map-gameplay-design.md)

---

## Background

### 当前问题

项目已有基础Roguelike地图系统（7种节点类型、层式生成、UI显示、战斗桥接），但非战斗节点无实际玩法：

- Store/Treasure/Mystery/RestSite 点击后无事发生
- 地图探索缺乏风险-回报选择和路径规划策略
- 无资源管理、无事件系统
- 无事件编辑器工具，策划难以批量创建事件内容

### 目标

实现完整的暗黑破坏神风格Roguelike地图探索玩法，包括区域主题系统、节点状态系统、BG3式属性判定事件系统、非战斗节点玩法、事件编辑器工具。

### 预期收益

- 玩家每次Run有独特的探索体验
- BG3式属性判定事件提供构筑策略深度
- 低金币经济（≤50/局）让每个决策都有分量
- 事件编辑器让策划可视化管理事件内容
- 单局时长控制在30-45分钟

---

## Scope

### In Scope

1. 区域系统（3个暗黑风主题区域：黑暗森林/墓地/修道院）
2. 节点状态系统（逐步揭示机制：未揭示/已揭示/可到达/已访问）
3. 事件系统（JSON配置 + BG3式属性判定 + 成功率显示）
4. 事件编辑器工具（UI Toolkit Editor + Visual Scripting，WYSIWYG拖拽编辑，导出JSON）
5. 非战斗节点玩法（商店/宝藏/休息站 — 低金币经济）
6. Boss节点简化行为（仅"继续探索"/"返回避难所"2选项）
7. 资源系统（低金币经济：单局≤50金）

### Out of Scope

- 元进度系统（局外解锁/永久升级）
- 多人协作/对战
- 成就系统
- 音效和特效
- 区域主题战斗地形影响（已从v1.0移除）
- 复杂的区域过渡事件系统（已简化）

---

## Tasks

### Phase 1: 基础框架 + 事件编辑器（Week 1-2）

#### Task 1: 区域系统数据层（暗黑风主题）

- **目标**: 创建暗黑破坏神风格区域配置和区域管理器
- **输出**: `RoguelikeRegion` ScriptableObject, RegionConfig数据文件, `RegionManager`
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Regions/RoguelikeRegion.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Regions/RegionManager.cs`
  - `Assets/Tactics/Arts/ScriptableObjects/RegionConfigs/DarkForestRegionConfig.asset`
  - `Assets/Tactics/Arts/ScriptableObjects/RegionConfigs/BurialGroundsRegionConfig.asset`
  - `Assets/Tactics/Arts/ScriptableObjects/RegionConfigs/MonasteryRegionConfig.asset`
- **验收标准**:
  - [ ] `RoguelikeRegion` 包含 regionId/regionName/description/layerCount/nodeDistribution/eventPoolIds
  - [ ] 区域1"黑暗森林": 3层，MinorEnemy 35%, Elite 10%, Mystery 25%, Store 10%, Treasure 10%, Rest 10%
  - [ ] 区域2"墓地": 3层，MinorEnemy 30%, Elite 20%, Mystery 20%, Store 15%, Treasure 5%, Rest 10%
  - [ ] 区域3"修道院": 2层+Boss，MinorEnemy 25%, Elite 25%, Mystery 15%, Store 5%, Treasure 10%, Rest 10%, Boss 10%
  - [ ] `RegionManager` 可加载和切换区域配置

#### Task 2: 修改地图生成器支持区域

- **目标**: 让 `RoguelikeMapGenerator` 支持按区域生成地图
- **输出**: 修改后的 `RoguelikeMapGenerator`
- **修改文件**: `RoguelikeMapGenerator.cs`
- **验收标准**:
  - [ ] 地图按区域分层生成（黑暗森林3层 → 墓地3层 → 修道院2层+Boss）
  - [ ] 节点分布符合区域配置中的概率
  - [ ] Boss节点固定在每个区域的最后一层
  - [ ] 兼容现有地图生成逻辑（不影响非区域模式）

#### Task 3: 节点状态系统

- **目标**: 实现节点的逐步揭示和状态管理
- **输出**: 扩展的 `RoguelikeMapNode`, `MapRevealSystem`, 修改后的 `RoguelikeMapUIController`
- **修改文件**: `RoguelikeMapNode.cs`, `RoguelikeMapUIController.cs`
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/MapRevealSystem.cs`
- **验收标准**:
  - [ ] 节点支持4种状态：未揭示（灰色问号）、已揭示（真实图标）、可到达（高亮边框）、已访问（半透明不可点击）
  - [ ] 只能看到当前层节点和下一层节点类型
  - [ ] 到达新层时自动揭示下一层
  - [ ] UI正确显示4种节点状态和当前玩家位置标记

#### Task 4: 事件编辑器 ⭐（高优先级）

- **目标**: 开发WYSIWYG事件编辑器，让策划可视化创建和编辑Roguelike事件
- **输出**: `RoguelikeEventEditorWindow`, 节点图编辑系统, JSON导出功能
- **新增文件**:
  - `Assets/Tactics/Editor/RoguelikeEventEditor/RoguelikeEventEditorWindow.cs`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/EventGraphView.cs`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/Nodes/StartNode.cs`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/Nodes/OptionNode.cs`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/Nodes/CheckNode.cs`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/Nodes/ResultNode.cs`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/EventExporter.cs`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/EventEditorUXML.uxml`
  - `Assets/Tactics/Editor/RoguelikeEventEditor/EventEditorUSS.uss`
- **技术方案**: UI Toolkit Editor Window + 自定义节点图 + Visual Scripting风格
- **验收标准**:
  - [ ] 左侧事件列表面板（新建/删除/选择事件）
  - [ ] 中央节点图画布，支持Start/Option/Check/Result节点
  - [ ] 右侧属性面板，编辑选中节点的属性（文本、属性、成功率、奖励类型）
  - [ ] 节点间拖拽连接（Start→Option→Check→Success/Failure）
  - [ ] 实时预览面板，显示事件在实际游戏中的表现
  - [ ] JSON导出功能（导出至 `Assets/Tactics/Resources/Events/`）
  - [ ] 属性下拉选择（Strength/Dexterity/Constitution/Intelligence/Charisma）
  - [ ] 奖励类型下拉选择（gold/item/equip/buff/heal/damage/nothing/battle）
- **依赖**: Task 1（需知道区域ID列表）, Task 2（无需，但编辑器窗口独立）
- **为何高优先级**: 后续所有事件内容（Task 6/11）依赖此编辑器创建，手工编写JSON效率低且易错

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
  - [ ] 点击Boss节点进入战斗，胜利后仅显示2个选项：继续探索 / 返回避难所
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
  - `Assets/Tactics/Resources/Events/DarkForest/*.json`（黑暗森林事件，通过编辑器创建至少5个）
  - `Assets/Tactics/Resources/Events/BurialGrounds/*.json`（墓地事件，至少5个）
  - `Assets/Tactics/Resources/Events/Monastery/*.json`（修道院事件，至少5个）
- **事件JSON结构**:
  ```json
  {
    "eventId": "cursed_chest_001",
    "title": "被诅咒的宝箱",
    "description": "...",
    "region": "DarkForest",
    "options": [
      {
        "optionId": "smash",
        "text": "暴力撬开",
        "attribute": "Strength",
        "successRate": 50,
        "condition": null,
        "success": {"type": "gold", "amount": 5},
        "failure": {"type": "damage", "amount": 8, "target": "self"}
      }
    ]
  }
  ```
- **验收标准**:
  - [ ] JSON格式正确，包含 eventId/title/description/region/options
  - [ ] 每个选项支持 attribute/successRate/condition/success/failure 字段
  - [ ] 支持多种结果类型（gold/item/equip/buff/heal/damage/nothing/battle）
  - [ ] 每个区域至少5个事件，覆盖单属性/多属性/团队协作类型

#### Task 7: BG3式属性判定系统

- **目标**: 实现属性成功率计算和团队属性检查
- **输出**: `AttributeCheckSystem`, `EventManager`（含判定逻辑）
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Events/AttributeCheckSystem.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Events/EventManager.cs`
- **验收标准**:
  - [ ] 成功率计算公式: `基础成功率 + (属性值 - 10) × 5%` 正确实现
  - [ ] 属性值6→20%, 8→30%, 10→40%, 14→60%, 18→80%, 20→90%
  - [ ] 支持条件判定（如 `"condition": {"class": "Mage"}` 需队伍有法师）
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
  - [ ] 有条件限制的选项显示条件要求（如"需要: Mage"），不满足时灰色禁用
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

### Phase 3: 内容填充（Week 3-4）

#### Task 11: 事件内容扩展（通过编辑器创建）

- **目标**: 使用事件编辑器批量创建30+个事件
- **输出**: 黑暗森林10个/墓地12个/修道院8个事件JSON（编辑器导出）
- **验收标准**:
  - [ ] 所有事件通过事件编辑器创建和导出
  - [ ] 每个事件2-4个有意义的选项（至少含1个自动成功选项）
  - [ ] 覆盖单属性抉择/多属性分工/团队协作/条件限制四种类型
  - [ ] 文本符合暗黑破坏神风格（黑暗森林：堕落祭坛、求救村民；墓地：亡灵低语、圣物；修道院：恶魔契约、黑暗真相）
  - [ ] 数值平衡（奖励不过强不过弱）

#### Task 12: 数值平衡

- **目标**: 调整所有数值确保低金币经济下的游戏平衡
- **输出**: 金币平衡表, 难度曲线, 奖励价值评估, 时长测试数据
- **验收标准**:
  - [ ] **单局总金币≤50**（核心硬指标）
  - [ ] 金币收入分布合理：普通战斗1-3金/精英3-6金/宝藏2-5金/事件0-5金/Boss 5-10金
  - [ ] 商店商品价格3-15金，确保玩家能买1-3件
  - [ ] 属性判定成功率平衡：核心属性玩家应能达到60-80%成功率
  - [ ] 单局时长30-45分钟（测试均值）
  - [ ] Boss难度曲线平滑递增
  - [ ] 不同路径选择产生明显金币差异（±15金以上）

#### Task 13: Boss节点"返回避难所"结算

- **目标**: 实现Boss击败后的简化选项和Run结算
- **输出**: `BossVictoryUI`, `RunSummaryUI`, 返回避难所逻辑
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/UI/BossVictoryUIController.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/UI/RunSummaryUIController.cs`
  - `Assets/Tactics/Arts/UI/BossVictoryPanel.uxml`
  - `Assets/Tactics/Arts/UI/RunSummaryPanel.uxml`
- **验收标准**:
  - [ ] Boss击败后显示战利品（金币/装备图标）和2个大按钮："继续探索" / "返回避难所"
  - [ ] "继续探索"：进入下一区域的地图
  - [ ] "返回避难所"：显示Run结算界面（获得的金币/装备/经验汇总）
  - [ ] 返回避难所后回到主菜单，所有奖励结算完成
  - [ ] 最后一个Boss（修道院Boss）击败后直接进入胜利结算，无"继续探索"

---

### Phase 4: 优化（Week 4）

#### Task 14: 事件编辑器完善

- **目标**: 完善事件编辑器的高级功能
- **输出**: 编辑器增强功能
- **修改文件**: 编辑器相关文件
- **验收标准**:
  - [ ] 支持导入已有JSON事件进行编辑
  - [ ] 支持撤销/重做（Ctrl+Z/Ctrl+Y）
  - [ ] 支持批量导出（多选事件 → 一键导出所有）
  - [ ] 节点图缩放和平移
  - [ ] 自动布局功能（整理节点位置）
  - [ ] 事件模板功能（从模板快速创建新事件）

---

## 依赖关系

```
Task 1 (区域数据层)
  ├── Task 2 (地图生成器) ── Task 3 (节点状态) ── Task 5 (非战斗节点)
  │     │                                              ├── Task 9 (商店)
  │     │                                              ├── Task 10 (休息站)
  │     │                                              └── Task 13 (Boss结算)
  │     │
  │     └── Task 4 (事件编辑器) ⭐ 高优先级
  │           └── Task 6 (事件数据层) ── Task 7 (属性判定) ── Task 8 (事件UI)
  │                 └── Task 11 (事件扩展) ── Task 14 (编辑器完善)
  │
  └── Task 12 (数值平衡) ── 依赖所有功能完成
```

## Risks & Open Questions

| 风险 | 等级 | 缓解措施 |
|------|------|----------|
| 事件编辑器开发复杂度高：节点图+Visual Scripting需要较多Editor开发 | 高 | Task 4放在Phase 1最优先；先实现基础版（表单编辑），再迭代节点图 |
| 低金币经济平衡困难：≤50金需精确数值设计 | 中 | Task 12提供调试工具，导出金币流数据人工评审 |
| UI工作量大：事件/商店/休息站/Boss结算都需要独立UI | 中 | 使用UI Toolkit复用组件，先占位后细化 |
| 现有代码兼容：修改多个现有文件可能引入回归 | 低 | 扩展现有类而非重构核心逻辑，保持向后兼容 |

### Open Questions

1. **休息站的"训练"选项**：提升1点属性应由玩家选择具体属性还是随机？（当前设计：玩家选择）
2. **商店刷新机制**：是否需要支持"付费刷新商品"功能？（当前设计：无，精简2-3个商品）
3. **事件连锁**：是否需要支持"事件A的选择影响后续事件B"的跨事件状态？（v2暂不实现）
4. **Boss战难度**：Boss的多阶段机制是否依赖战斗系统的新特性？

---

## Assumptions

1. **允许修改现有文件**：`RoguelikeMapGenerator.cs`, `RoguelikeMapUIController.cs`, `RoguelikeMapNode.cs`, `RoguelikeBattleReturnHandler.cs`
2. **允许新增命名空间**：`Tactics.RoguelikeMap.Events`, `Tactics.RoguelikeMap.Regions`, `Tactics.RoguelikeMap.Interaction`, `Tactics.Editor.RoguelikeEventEditor`
3. **技术栈**：Unity 2022+, UI Toolkit (UXML/USS), JSON配置, Editor Window API
4. **事件配置**：使用JSON文件，通过事件编辑器导出（不再手工编写）
5. **商店商品**：使用现有Item系统（ItemData/EquipmentData）
6. **队伍数据**：`CharacterDefinition` 包含HP/MP和属性（力量/敏捷/体质/智力/魅力），可在Roguelike运行中修改
7. **金币系统**：独立管理，不依赖已有的Inventory系统（如有），单局≤50金
8. **区域主题无战斗地形影响**：v2移除该特性，简化开发范围

---

## 整体验收标准

- [ ] 可以开始一局完整的暗黑风Roguelike Run（黑暗森林 → 墓地 → 修道院通关）
- [ ] 可以探索3个主题区域，每区域有独特的节点分布和事件主题
- [ ] 可以触发事件并看到BG3式属性判定选项（显示成功率百分比）
- [ ] 可以使用事件编辑器创建、编辑、导出事件JSON
- [ ] 可以在商店浏览和购买物品（金币正确扣除和获得，价格3-15金）
- [ ] 可以在休息站选择休息/训练/冥想
- [ ] 击败Boss后仅显示"继续探索"/"返回避难所"2个选项
- [ ] 单局总金币获取≤50
- [ ] 单局时长30-45分钟（3次测试均值）
- [ ] 每次Run体验不同（节点分布、事件、商品随机）

---

## 关联文档

- **设计文档**: [roguelike-map-gameplay-design.md](../design/roguelike-map-gameplay-design.md)
- **项目文档组织规范**: [project-doc-organization](../../skills/project-doc-organization/SKILL.md)
- **前置计划**: [战斗结算与奖励计划.md](战斗结算与奖励计划.md)
- **前置计划**: [地形效果计划.md](地形效果计划.md)
