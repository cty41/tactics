# Roguelike地图玩法开发计划

> **版本**: v1.0
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
- 区域主题未对战斗产生影响

### 目标

实现完整的Roguelike地图探索玩法，包括区域主题系统、节点状态系统、事件系统、非战斗节点玩法、区域过渡系统。

### 预期收益

- 玩家每次Run有独特的探索体验
- 区域主题与战斗系统形成联动
- 事件选择提供构筑策略深度
- 单局时长控制在30-45分钟

---

## Scope

### In Scope

1. 区域系统（3个主题区域配置：森林外围/城堡外围/Boss领地）
2. 节点状态系统（逐步揭示机制：未揭示/已揭示/可到达/已访问）
3. 事件系统（JSON配置 + 条件判定 + 结果执行）
4. 非战斗节点玩法（商店/宝藏/休息站）
5. 区域过渡系统（Boss击败后选择进入下一区域的方式）
6. 资源系统（金币）

### Out of Scope

- 元进度系统（局外解锁/永久升级）
- 多人协作/对战
- 成就系统
- 音效和特效
- 完整的事件内容（先实现20个基础事件，后续扩展至40个）

---

## Tasks

### Phase 1: 基础框架（Week 1-2）

#### Task 1: 区域系统数据层

- **目标**: 创建区域配置和区域管理器
- **输出**: `RoguelikeRegion` ScriptableObject, RegionConfig数据文件, `RegionManager`
- **修改文件**: 无（纯新增）
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Regions/RoguelikeRegion.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Regions/RegionManager.cs`
  - `Assets/Tactics/Arts/ScriptableObjects/RegionConfigs/ForestRegionConfig.asset`
  - `Assets/Tactics/Arts/ScriptableObjects/RegionConfigs/CastleRegionConfig.asset`
  - `Assets/Tactics/Arts/ScriptableObjects/RegionConfigs/BossDomainConfig.asset`
- **验收标准**:
  - [ ] `RoguelikeRegion` 包含 regionId/regionName/description/layerCount/nodeDistribution/terrainModifiers
  - [ ] `RegionManager` 可加载和切换区域配置
  - [ ] 3个区域SO配置正确（节点分布概率、战斗地形影响、事件池ID列表）

#### Task 2: 修改地图生成器支持区域

- **目标**: 让 `RoguelikeMapGenerator` 支持按区域生成地图
- **输出**: 修改后的 `RoguelikeMapGenerator`
- **修改文件**: `RoguelikeMapGenerator.cs`
- **验收标准**:
  - [ ] 地图按区域分层生成（森林3层 → 城堡3层 → Boss领地2层）
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

#### Task 4: 非战斗节点基础框架

- **目标**: 实现Store/Treasure/RestSite的基础交互
- **输出**: `NodeInteractionManager`, Store/Treasure/RestSite的Handler, 基础UI
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Interaction/NodeInteractionManager.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Interaction/StoreNodeHandler.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Interaction/TreasureNodeHandler.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Interaction/RestSiteNodeHandler.cs`
- **验收标准**:
  - [ ] 点击Store节点弹出商店UI（基础占位界面）
  - [ ] 点击Treasure节点直接获得奖励（金币30-60，弹出提示）
  - [ ] 点击RestSite节点弹出选项UI（休息/训练/冥想，占位界面）
  - [ ] 每个非战斗节点只能交互一次，已访问后变半透明

---

### Phase 2: 事件系统（Week 2-3）

#### Task 5: 事件数据层

- **目标**: 创建事件配置系统和数据表
- **输出**: `RoguelikeEvent` JSON结构, `EventOption`/`EventCondition`/`EventResult` 数据结构, 20个基础事件JSON
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Events/RoguelikeEvent.cs`（数据模型）
  - `Assets/Tactics/Scripts/RoguelikeMap/Events/EventOption.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Events/EventCondition.cs`
  - `Assets/Tactics/Scripts/RoguelikeMap/Events/EventResult.cs`
  - `Assets/Tactics/Resources/Events/ForestEvents/*.json`（森林事件，至少5个）
  - `Assets/Tactics/Resources/Events/CastleEvents/*.json`（城堡事件，至少5个）
  - `Assets/Tactics/Resources/Events/BossEvents/*.json`（Boss领地事件，至少5个）
- **验收标准**:
  - [ ] JSON格式正确，包含 eventId/title/description/region/options
  - [ ] 支持条件判定（class/attribute/item类型）
  - [ ] 支持多种结果类型（battle/reward/buff/nothing/transition）
  - [ ] 每个区域至少5个事件，覆盖奖励/风险/条件/连锁类型

#### Task 6: 事件管理器

- **目标**: 实现事件的加载、触发和结算
- **输出**: `EventManager`, 事件触发逻辑, 条件判定逻辑, 结果执行逻辑
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Events/EventManager.cs`
- **验收标准**:
  - [ ] 随机选择符合当前区域的事件
  - [ ] 正确判定选项条件（职业、属性、物品等）
  - [ ] 正确执行选项结果（战斗、奖励、Buff、无）
  - [ ] 每个事件标记已访问，不重复触发

#### Task 7: 事件UI

- **目标**: 实现事件交互界面
- **输出**: `EventUIController`, 事件弹窗UXML/USS
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/UI/EventUIController.cs`
  - `Assets/Tactics/Arts/UI/EventPanel.uxml`
  - `Assets/Tactics/Arts/UI/EventPanel.uss`
- **验收标准**:
  - [ ] 显示事件标题、描述文本
  - [ ] 显示2-4个选项按钮
  - [ ] 有条件的选项显示条件要求（如"需要: Mage"）
  - [ ] 不满足条件的选项显示为灰色/禁用
  - [ ] 选择后展示结果（奖励/战斗提示/无）

---

### Phase 3: 区域过渡与集成（Week 3-4）

#### Task 8: 区域过渡系统

- **目标**: 实现区域间的过渡事件
- **输出**: `RegionTransitionManager`, 过渡事件数据, 选择影响逻辑
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Regions/RegionTransitionManager.cs`
  - `Assets/Tactics/Resources/Events/Transitions/ForestToCastle.json`
  - `Assets/Tactics/Resources/Events/Transitions/CastleToBoss.json`
- **验收标准**:
  - [ ] 击败Boss后触发过渡事件
  - [ ] 显示2-3个进入方式选项
  - [ ] 选择影响下一区域初始状态（如商店价格-10%、敌人+1等）
  - [ ] 正确加载下一区域的地图

#### Task 9: 商店系统完善

- **目标**: 实现完整的商店功能
- **输出**: `ShopManager`, 商品池配置, 购买逻辑, 完善商店UI
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Interaction/ShopManager.cs`
  - `Assets/Tactics/Arts/ScriptableObjects/ShopConfigs/DefaultShopConfig.asset`
- **修改文件**: `StoreNodeHandler.cs`, 商店UI相关
- **验收标准**:
  - [ ] 根据当前区域和进度生成4-6个商品
  - [ ] 商品类型覆盖：装备(50%)/消耗品(30%)/技能书(20%)
  - [ ] 购买时检查金币余额
  - [ ] 购买后物品进入背包，金币正确扣除
  - [ ] 访问后商店变已访问状态

#### Task 10: 休息站系统完善

- **目标**: 实现完整的休息站功能
- **输出**: `RestSiteManager`, 休息选项逻辑, 效果应用
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Interaction/RestSiteManager.cs`
- **修改文件**: `RestSiteNodeHandler.cs`
- **验收标准**:
  - [ ] 选项正确显示：休息/训练/冥想
  - [ ] 休息：恢复队伍50%最大HP
  - [ ] 训练：选择一个角色，提升1点随机属性
  - [ ] 冥想：选择一个角色，恢复全部MP
  - [ ] 访问后休息站变已访问状态

#### Task 11: 资源系统集成

- **目标**: 将金币系统集成到所有节点
- **输出**: `GoldManager`, 战斗奖励发放, 事件奖励发放, 宝藏奖励发放, UI金币显示
- **新增文件**:
  - `Assets/Tactics/Scripts/RoguelikeMap/Resources/GoldManager.cs`
- **修改文件**: `RoguelikeBattleReturnHandler.cs`, `RoguelikeMapUIController.cs`
- **验收标准**:
  - [ ] 战斗胜利根据敌人强度获得10-80金币
  - [ ] 事件奖励正确发放0-100金币
  - [ ] 商店购买正确扣除20-300金币
  - [ ] 顶部信息栏实时显示金币数量

---

### Phase 4: 内容填充与优化（Week 4-5）

#### Task 12: 事件内容扩展

- **目标**: 增加事件数量从20到40个
- **输出**: 森林15个/城堡15个/Boss领地10个事件JSON
- **验收标准**:
  - [ ] 每个事件2-4个有意义的选项
  - [ ] 覆盖纯奖励/风险回报/条件判定/连锁事件四种类型
  - [ ] 文本质量合格，符合区域主题
  - [ ] 数值平衡（奖励不过强不过弱）

#### Task 13: 数值平衡

- **目标**: 调整所有数值确保游戏平衡
- **输出**: 金币平衡表, 难度曲线, 奖励价值评估, 时长测试数据
- **验收标准**:
  - [ ] 单局时长30-45分钟（测试均值）
  - [ ] 单局总金币收入约500-800，不溢出也不紧缺
  - [ ] 敌人难度随区域平滑递增
  - [ ] 不同路径选择产生明显差异（金币差±200以上）

#### Task 14: 区域主题战斗影响

- **目标**: 让区域主题真正影响战斗
- **输出**: 森林/城堡/Boss领地地形生成逻辑, 区域敌人配置
- **修改文件**: 地形生成相关代码, 敌人配置/加载
- **验收标准**:
  - [ ] 森林战斗增加树木地形（掩护效果）
  - [ ] 城堡战斗增加城墙地形，精英概率增加
  - [ ] Boss领地增加黑暗地形，Boss有独特机制
  - [ ] `BattleContext.regionId` 正确传递并在战斗中生效

---

## 依赖关系

```
Task 1 (区域数据层)
  ├── Task 2 (地图生成器) ── Task 3 (节点状态) ── Task 4 (非战斗节点)
  │                                                    ├── Task 9 (商店)
  │                                                    ├── Task 10 (休息站)
  │                                                    └── Task 11 (资源系统)
  └── Task 14 (区域战斗影响)

Task 5 (事件数据层) ── Task 6 (事件管理器) ── Task 7 (事件UI)
  └── Task 12 (事件扩展)

Task 1 + Task 6 ── Task 8 (区域过渡)

All ── Task 13 (数值平衡)
```

## Risks & Open Questions

| 风险 | 等级 | 缓解措施 |
|------|------|----------|
| 事件内容量大：40个事件需要大量文本创作 | 高 | 先实现20个MVP事件，Task 12作为扩展阶段 |
| 与战斗系统耦合：区域影响战斗需要修改地形生成 | 中 | Task 14放在最后，确保依赖方接口稳定 |
| UI工作量大：商店/事件/休息站都需要独立UI | 中 | 使用UI Toolkit复用组件，先占位后细化 |
| 数值平衡困难：需要大量测试 | 中 | 提供调试工具，导出数值表人工评审 |
| 现有代码兼容：修改多个现有文件可能引入回归 | 低 | 扩展现有类而非重构核心逻辑，保持向后兼容 |

### Open Questions

1. **休息站的"训练"选项**：提升1点随机属性是否过于随机？是否需要改为玩家选择属性？
2. **商店刷新机制**：是否需要支持"刷新商品"功能（付费刷新）？
3. **事件连锁**：是否需要支持"事件A的选择影响后续事件B"的跨事件状态？
4. **Boss战难度**：Boss的多阶段机制是否依赖战斗系统的新特性？

---

## Assumptions

1. **允许修改现有文件**：`RoguelikeMapGenerator.cs`, `RoguelikeMapUIController.cs`, `RoguelikeMapNode.cs`, `RoguelikeBattleReturnHandler.cs`
2. **允许新增命名空间**：`Tactics.RoguelikeMap.Events`, `Tactics.RoguelikeMap.Regions`, `Tactics.RoguelikeMap.Interaction`
3. **技术栈**：Unity 2022+, UI Toolkit (UXML/USS), JSON配置
4. **事件配置**：使用JSON文件而非ScriptableObject（便于策划批量编辑）
5. **商店商品**：使用现有Item系统（ItemData/EquipmentData）
6. **战斗地形**：复用现有地形生成系统，仅新增地形类型配置
7. **队伍数据**：`CharacterDefinition` 包含HP/MP/属性，可在Roguelike运行中修改
8. **金币系统**：独立管理，不依赖已有的Inventory系统（如有）

---

## 整体验收标准

- [ ] 可以开始一局完整的Roguelike Run（从区域1到区域3通关）
- [ ] 可以探索3个主题区域，每区域有独特的节点分布和视觉风格
- [ ] 可以触发事件并做出有意义的2-4个选择
- [ ] 可以在商店浏览和购买物品（金币正确扣除和获得）
- [ ] 可以在休息站选择恢复/训练/冥想
- [ ] 可以击败区域Boss并触发过渡事件进入下一区域
- [ ] 单局时长30-45分钟（3次测试均值）
- [ ] 每次Run体验不同（节点分布、事件、商品随机）

---

## 关联文档

- **设计文档**: [roguelike-map-gameplay-design.md](../design/roguelike-map-gameplay-design.md)
- **项目文档组织规范**: [project-doc-organization](../../skills/project-doc-organization/SKILL.md)
- **前置计划**: [战斗结算与奖励计划.md](战斗结算与奖励计划.md)
- **前置计划**: [地形效果计划.md](地形效果计划.md)
