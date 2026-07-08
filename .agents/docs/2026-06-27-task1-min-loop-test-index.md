# Task 1 最小闭环测试与实现总索引

## 状态

- 日期：2026-06-27
- 用途：集中记录 `Task 1` 最小闭环相关的 fixture、PlayMode 入口、关键代码收口点，以及当前通过状态
- 关联文档：[Task 1 最小前置事项开发与自动测试覆盖计划](./2026-06-27-task1-min-prerequisites-and-test-coverage-plan.md)
- 关联文档：[短期原型 Task 1：核心循环与失败标准](./2026-06-24-short-term-prototype-task1-design.md)

---

## 一、当前目标

当前这批测试与实现，不是在证明完整 roguelike 体验，而是在证明：

- `战斗 -> 结算写回 -> 补给或商店 -> 再选点`

这条最小功能链已经开始具备：

1. 可运行的代码骨架
2. 可回归的自动化 fixture
3. 可承接的 PlayMode 测试入口

---

## 二、当前闭环口径（已收紧）

### 最低状态集合

- `HP`
- `MP`
- `死亡`
- `留尸`
- `战后回收装备与消耗品`
- `Gold`
- `战后成长结果`

### 当前不要求

- `伤势系统`
- 地图根据队伍状态动态改节点可达性
- 地图动态改节点类型或内容
- 问号点 / 随机事件点前置实现
- 独立成长点前置实现

### 当前三类核心节点

1. `补给点`
   - 恢复继续推进资格
2. `战斗点`
   - 有风险的成长点
3. `商店点`
   - 资源转化节点

---

## 三、第一批已通过的最小测试链路

### 1. 战后写回

- fixture：`Tests/gameplay-specs/map/battle-result-writeback.gameplay-test.md`
- plan：`Tests/gameplay-specs/compiled/battle-result-writeback.plan.json`
- PlayMode 入口：
  - `Assets/Tactics/Tests/PlayMode/GameplayRuntimeMapPlanTests.cs`
  - `RuntimeRunner_ExecutesMapBattleResultWriteback()`
- 覆盖目标：
  - 战斗结束后，战斗层 `HP / MP / 死亡态` 写回冒险状态

### 2. 补给写回

- fixture：`Tests/gameplay-specs/map/restsite-repair-writeback.gameplay-test.md`
- plan：`Tests/gameplay-specs/compiled/restsite-repair-writeback.plan.json`
- PlayMode 入口：
  - `RuntimeRunner_ExecutesMapRestSiteRepairWriteback()`
- 覆盖目标：
  - RestSite 通过统一结果结构恢复 `HP / MP`
  - 并写回冒险状态

### 3. 商店 Gold 兑换

- fixture：`Tests/gameplay-specs/map/shop-gold-conversion.gameplay-test.md`
- plan：`Tests/gameplay-specs/compiled/shop-gold-conversion.plan.json`
- PlayMode 入口：
  - `RuntimeRunner_ExecutesMapShopGoldConversion()`
- 覆盖目标：
  - Gold 扣减
  - 装备进入库存

### 4. 同一局最小两轮闭环

- fixture：`Tests/gameplay-specs/map/task1-min-loop-2rounds.gameplay-test.md`
- plan：`Tests/gameplay-specs/compiled/task1-min-loop-2rounds.plan.json`
- PlayMode 入口：
  - `RuntimeRunner_ExecutesTask1MinLoop2Rounds()`
- 覆盖目标：
  - `战斗 -> 写回 -> 补给/商店 -> 再选点` 这条链在同一局里至少成立两轮

---

## 四、第二批边界测试链路

### 5. 战后死亡态写回

- fixture：`Tests/gameplay-specs/map/battle-death-writeback.gameplay-test.md`
- plan：`Tests/gameplay-specs/compiled/battle-death-writeback.plan.json`
- PlayMode 入口：
  - `RuntimeRunner_ExecutesMapBattleDeathWriteback()`
- 覆盖目标：
  - 战斗失败后，`HP / MP / IsDead` 正确写回冒险状态

### 6. 补给点不复活死亡角色

- fixture：`Tests/gameplay-specs/map/restsite-skips-dead.gameplay-test.md`
- plan：`Tests/gameplay-specs/compiled/restsite-skips-dead.plan.json`
- PlayMode 入口：
  - `RuntimeRunner_ExecutesMapRestSiteSkipsDead()`
- 覆盖目标：
  - `IsDead = true` 的角色不会被 RestSite 恢复

---

## 五、第三批扩展测试链路

### 7. 战后成长结果写回

- fixture：`Tests/gameplay-specs/map/battle-growth-writeback.gameplay-test.md`
- plan：`Tests/gameplay-specs/compiled/battle-growth-writeback.plan.json`
- PlayMode 入口：
  - `RuntimeRunner_ExecutesMapBattleGrowthWriteback()`
- 覆盖目标：
  - 战斗胜利后，经验值正确写回对应 `roster` 角色

### 8. 死亡后装备仍保留

- fixture：`Tests/gameplay-specs/map/battle-death-equipment-retained.gameplay-test.md`
- plan：`Tests/gameplay-specs/compiled/battle-death-equipment-retained.plan.json`
- PlayMode 入口：
  - `RuntimeRunner_ExecutesMapBattleDeathEquipmentRetained()`
- 覆盖目标：
  - 角色死亡后，`CharacterDefinition.Equipment` 上的已装备武器仍然保留

### 9. 死亡后消耗品仍保留

- fixture：`Tests/gameplay-specs/map/battle-death-consumable-retained.gameplay-test.md`
- plan：`Tests/gameplay-specs/compiled/battle-death-consumable-retained.plan.json`
- PlayMode 入口：
  - `RuntimeRunner_ExecutesMapBattleDeathConsumableRetained()`
- 覆盖目标：
  - 角色死亡后，`PlayerAdventureState.Inventory` 中的消耗品仍然保留

### 10. 商店非泛用价值（`staff_01 -> Mage`）

- fixture：`Tests/gameplay-specs/map/shop-staff-mage-intelligence.gameplay-test.md`
- plan：`Tests/gameplay-specs/compiled/shop-staff-mage-intelligence.plan.json`
- PlayMode 入口：
  - `RuntimeRunner_ExecutesMapShopStaffMageIntelligence()`
- 覆盖目标：
  - 商店购买 `staff_01`
  - 将其装备给 `Mage`
  - 断言 `Gold` 扣减、`Weapon == staff_01`、`Mage.TotalIntelligence == 12`

### 11. 商店非泛用价值镜像链（`bow_01 -> Hunter`）

- fixture：`Tests/gameplay-specs/map/shop-bow-hunter-agility.gameplay-test.md`
- plan：`Tests/gameplay-specs/compiled/shop-bow-hunter-agility.plan.json`
- PlayMode 入口：
  - `RuntimeRunner_ExecutesMapShopBowHunterAgility()`
- 覆盖目标：
  - 商店购买 `bow_01`
  - 将其装备给 `Hunter`
  - 断言 `Gold` 扣减、`Weapon == bow_01`、`Hunter.TotalAgility == 11`

---

## 六、当前通过状态

截至本轮收口：

### 已通过

1. `RuntimeRunner_ExecutesMapBattleResultWriteback`
2. `RuntimeRunner_ExecutesMapRestSiteRepairWriteback`
3. `RuntimeRunner_ExecutesMapShopGoldConversion`
4. `RuntimeRunner_ExecutesTask1MinLoop2Rounds`
5. `RuntimeRunner_ExecutesMapBattleDeathWriteback`
6. `RuntimeRunner_ExecutesMapRestSiteSkipsDead`
7. `RuntimeRunner_ExecutesMapBattleGrowthWriteback`
8. `RuntimeRunner_ExecutesMapBattleDeathEquipmentRetained`
9. `RuntimeRunner_ExecutesMapBattleDeathConsumableRetained`
10. `RuntimeRunner_ExecutesMapShopStaffMageIntelligence`
11. `RuntimeRunner_ExecutesMapShopBowHunterAgility`

这意味着当前最小闭环与边界条件，至少已经在自动化层证明了：

- 战后写回存在
- 补给恢复存在
- 商店 Gold 兑现存在
- 同局多步链路存在
- 永久死亡口径已成立
- 补给点不会复活死亡角色
- 战后成长结果可写回
- 死亡后已装备物仍保留
- 死亡后背包内消耗品仍保留
- 商店点的最小“非泛用价值”已被自动化证明：`staff_01` 对 `Mage` 的功能价值高于泛用消费语义
- 商店点的“非泛用价值”已具备镜像证明：`staff_01 -> Mage` 与 `bow_01 -> Hunter` 两条专用装备链均已通过

---

## 七、关键代码收口点

### 1. 状态与写回

- `Assets/Tactics/Scripts/Common/Roster/CharacterDefinition.cs`
  - 新增 `CurrentMp`
  - 新增 `IsDead`
  - 补 `MaxMp`

- `Assets/Tactics/Scripts/Common/Roster/PlayerAdventureStateStore.cs`
  - 在修复存档时初始化 `CurrentMp`
  - 保持 `IsDead` 角色不被误当成“未初始化”恢复

- `Assets/Tactics/Scripts/Roguelike/RoguelikeBattleReturnHandler.cs`
  - 战后统一写回 `HP / MP / IsDead`
  - 胜利/失败分支都能持久化状态
  - `ApplyPostBattleRegeneration(...)` 已按永久死亡口径修正：倒下角色不自动复活

### 2. Gold 统一链

- `Assets/Tactics/Scripts/RoguelikeMap/Economy/RunGoldManager.cs`
  - 新增 `SyncFromState(...)`
  - 新增 `SyncToState(...)`

- `Assets/Tactics/Scripts/Common/Battle/BattleSettlementCoordinator.cs`
  - Gold 奖励走 `RunGoldManager` 同步链

- `Assets/Tactics/Scripts/Common/Battle/BattleRewardSystem.cs`
  - `ApplyRewards()` 里的 Gold 写法已收口

- `Assets/Tactics/Scripts/RoguelikeMap/Interaction/StoreNodeHandler.cs`
  - 商店消费后会写回 `PlayerAdventureState.Gold`

- `Assets/Tactics/Scripts/RoguelikeMap/Interaction/TreasureNodeHandler.cs`
- `Assets/Tactics/Scripts/RoguelikeMap/Events/EventResult.cs`
  - 加 Gold 路径开始统一走同步链

### 3. 统一结果层

- `Assets/Tactics/Scripts/RoguelikeMap/Interaction/RewardResult.cs`
  - 新增 `GoldCost`
  - 新增 `HealPercent`
  - 新增 `ManaHealPercent`
  - 新增 `ApplyMpChangeToParty(...)`
  - 新增 `ApplyToState(PlayerAdventureState)`

- `Assets/Tactics/Scripts/RoguelikeMap/Interaction/NodeInteractionManager.cs`
  - 新增 `ApplyRewardResult(...)`
  - 作为补给/商店/宝藏节点统一状态出口

### 4. 非战斗节点统一出口

- `Assets/Tactics/Scripts/RoguelikeMap/Interaction/RestSiteNodeHandler.cs`
  - 通过 `RewardResult` 恢复 `HP / MP`

- `Assets/Tactics/Scripts/RoguelikeMap/Interaction/StoreNodeHandler.cs`
  - 通过 `RewardResult` 执行 `GoldCost + EquipmentIds`

- `Assets/Tactics/Scripts/RoguelikeMap/Interaction/TreasureNodeHandler.cs`
  - 通过 `RewardResult` 统一应用奖励

### 5. 测试承接能力

- `Assets/Tactics/Scripts/Common/Testing/Gameplay/MapGameplayStepAdapter.cs`
  - 新增动作：
    - `setAdventureGold`
    - `setRosterCharacterState`
    - `addInventoryItem`
    - `applyRestSiteResult`
    - `buyShopEquipment`
  - 新增断言：
    - `runGoldEquals`
    - `rosterCharacterHpEquals`
    - `rosterCharacterMpEquals`
    - `rosterCharacterDeadEquals`
    - `rosterCharacterExperienceEquals`
    - `rosterCharacterEquipmentEquals`
    - `rosterCharacterTotalAttributeEquals`
    - `inventoryContains`
  - `loadRoguelikeMap(...)` 增加最小内存测试图 fallback

- `Assets/Tactics/Scripts/Common/Testing/Gameplay/BattleGameplayStepAdapter.cs`
  - `setUnitState(...)` 已支持 `characterId`，会自动挂 `RosterCharacterLink`
  - `endBattleWithResult` 已支持显式构造胜负方

- `Tools/gameplay-test-spec/src/validator.ts`
- `Tools/gameplay-test-spec/src/compiler.ts`
- `Tools/gameplay-test-spec/dist/src/validator.js`
- `Tools/gameplay-test-spec/dist/src/compiler.js`
  - 已补齐新动作/断言 kind 的编译与校验支持

---

## 八、当前仍可继续扩展的方向

### 1. 第三批测试链路

更适合继续补的是：

- 商店点“非泛用型价值”可继续扩成更多职业/装备镜像链
- 战后结果统一语义导出（如 `BattleSettlementCoordinator.CurrentRewardResult`）
- 更明确的“节点结果写回后进入地图层比较上下文”链路

### 2. 统一结果协议继续收口

当前虽然已经明显统一，但仍然可以继续推进：

- 让战斗点也更完整地复用 `RewardResult`
- 让节点 handler 尽量只“产出结果”，不直接散写状态

### 3. 文档与实现同步

当前最小闭环代码已经比早期计划更具体，后续如有重要口径变化，应同步回：

- `2026-06-24-short-term-prototype-task1-design.md`
- `2026-06-27-task1-min-prerequisites-and-test-coverage-plan.md`

---

## 九、使用建议

后续如果要继续推进 `Task 1`，推荐按下面顺序读：

1. 先看本索引文档，了解当前已通过的闭环测试范围
2. 再看 `2026-06-27-task1-min-prerequisites-and-test-coverage-plan.md`，确认下一步是补实现还是补测试
3. 最后再进对应 fixture / PlayMode 测试 / 代码收口点做增量修改

一句话收口：

> 当前 `Task 1` 的最小闭环，已经从“设计口径”推进到了“代码骨架 + 自动化链路 + 边界条件”三层同时存在的状态。
