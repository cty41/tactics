# Task 1 最小前置事项开发与自动测试覆盖计划

## 文档状态

- 日期：2026-06-27
- 用途：把 `Task 1` 的最小前置事项，整理成可执行的开发计划，并说明如何逐步纳入 `gameplay-test-framework` 自动化覆盖
- 适用范围：`Task 1` 第一版 vertical slice
- 当前约束：
  - 不前置 `伤势系统`
  - 不要求地图根据队伍状态动态改写节点可达性、节点类型或节点内容
  - 核心节点只保留：
    - `战斗点`
    - `补给点`
    - `商店点`
  - `成长点` 仅作为后续低频特殊非战斗节点预留

---

## 一、当前闭环口径

当前 `Task 1` 的“闭环证明”不再优先用玩家主观体验来定义，而是先用一组**可实现、可验收的必要功能**来定义。

这里的 `再选择` 收紧为：

- 地图节点类型和内容一开始就固定随机生成
- 节点是否可达只由：
  1. 图连通性
  2. 节点自身状态（如 `visited / reachable / revealed`）
  决定
- 玩家在上一轮结算之后，带着最新的单局状态，在**固定可达节点集合**中重新决定先去哪个节点

因此，本轮最小闭环的重点不是：

- 地图是否会因为队伍状态动态改写拓扑

而是：

- **上一轮结算结果是否能稳定写回单局状态**
- **这些状态是否能在下一次节点比较时被系统继续持有和消费**

---

## 二、最低状态集合

`Task 1` 第一版只围绕以下最低状态集合实现闭环：

- `HP`
- `MP`
- `死亡`
- `留尸`
- `战后回收装备与消耗品`
- `Gold`
- `战后成长结果`

### 明确不前置的内容
- `伤势系统`
- 随机事件点
- 独立成长点
- 地图层动态改节点可达性/类型/内容
- 复杂掉落池
- 大规模测试矩阵

---

## 三、六项必要功能闭环

### 1. 结算结果必须能写回单局状态
- 战斗、补给、商店等节点的结果，必须能稳定写回当前 `run state`
- 至少覆盖：
  - `HP / MP / 死亡状态`
  - `Gold`
  - `装备 / 道具变化`
  - `成长结果`

### 2. 地图层必须能读取当前单局状态，并在重新选择节点时保留这些状态上下文
- 不要求地图层动态改节点可达性
- 但要求玩家返回地图后，系统已经持有并可继续消费本轮最新状态

### 3. 补给点 / 战斗点 / 商店点必须共享一套最小结果表达结构
- 三类核心节点不要求结果完全一致
- 但必须能统一表达、统一写回、统一被后续流程消费

### 4. 补给点、战斗点、商店点都必须有最小可用实现
- 补给点：能写回 `HP / MP / 死亡后回局口径` 的修复/承接结果
- 战斗点：能写回战后成长跃迁或战斗结果
- 商店点：能写回金币兑现成现实能力后的结果

### 5. 至少存在一条最小地图流程，能把功能链跑通
功能链只要求：
- `战斗结果 -> 状态写回 -> 后续修复或资源转化 -> 再选择`

不要求：
- 补给点和商店点必须平行
- 固定地图拓扑
- 节点内容随状态动态变化

### 6. 这条最小地图流程必须能在同一局 run 内连续成立至少 2-3 轮
这里的“2-3 轮”指的是：
- **同一局 run 内**
- 连续出现：
  - 战斗结果写回
  - 后续修复/转化
  - 再选点
- 不是完整 run 从头到尾跑 2-3 遍

---

## 四、按代码文件分组的实现清单

| 任务名 | 目标文件 | 要做什么 | 验收标准 | 风险点 | 优先级 |
|---|---|---|---|---|---|
| 1. 定义最小统一结果结构 | `Assets/Tactics/Scripts/RoguelikeMap/Interaction/RewardResult.cs`，必要时新增统一结果定义文件 | 明确 Task 1 最小结果字段：`HP/MP/死亡/留尸/战后回收装备与消耗品/Gold/战后成长结果`，区分“战斗结果”和“节点处理结果” | 有一个统一结构能完整表达三类核心节点与战斗写回需要的最小字段；字段命名和 owner 明确 | 现有 `RewardResult` 偏奖励展示，直接扩展可能语义污染 | P0 |
| 2. 收敛冒险态最小持久化字段 | `Assets/Tactics/Scripts/Common/Roster/CharacterDefinition.cs`，`Assets/Tactics/Scripts/Common/Roster/PlayerAdventureState.cs` | 明确哪些状态落在角色、哪些落在 run：至少补齐 MP、死亡/可用状态、成长落点；确认 Gold 只有一个持久化口径 | 角色态与 run 态字段边界清晰；不存在“结算需要但状态里无处可写”的字段 | 现有 `CurrentHp` 的旧存档兼容语义可能与死亡态冲突 | P0 |
| 3. 统一战斗结算写回入口 | `Assets/Tactics/Scripts/Roguelike/RoguelikeBattleReturnHandler.cs`，`Assets/Tactics/Scripts/Common/Battle/BattleSettlementCoordinator.cs` | 把战斗后的状态写回、奖励写回、地图推进前置到单一入口，避免 HP/Gold/成长分散保存 | 战斗胜负后只有一条主写回链；地图推进发生在写回完成之后 | 现有流程同时涉及 `BattleSettlementFlow`、`PlayerPrefs`、runtime state，容易双写 | P0 |
| 4. 合并金币链路为单一真源 | `Assets/Tactics/Scripts/RoguelikeMap/Economy/RunGoldManager.cs`，`Assets/Tactics/Scripts/Common/Battle/BattleSettlementCoordinator.cs`，`Assets/Tactics/Scripts/RoguelikeMap/Interaction/StoreNodeHandler.cs`，`Assets/Tactics/Scripts/RoguelikeMap/Interaction/TreasureNodeHandler.cs` | 明确 Gold 是“run 内真源”还是“冒险存档真源”；其余地方全部改成经统一接口读写 | 战斗奖励、宝藏加钱、商店消费、总结算显示的 Gold 一致，无双账本 | 现有 `RunGoldManager.CurrentGold` 与 `PlayerAdventureState.Gold` 已分裂 | P0 |
| 5. 补齐 MP/死亡/留尸/战后回收链 | `Assets/Tactics/Scripts/Roguelike/RoguelikeBattleReturnHandler.cs`，`Assets/Tactics/Scripts/Common/Units/Unit.cs`，`Assets/Tactics/Scripts/Common/Battle/BattleController.cs`，`Assets/Tactics/Scripts/Common/controllers/GridController.cs` | 明确战斗层到地图层的最小写回规则：MP 怎么写、角色死亡如何标记、敌方尸体是否只留战场态、战后装备与消耗品如何回收/结转 | 能说清并落地“战斗结束后每类状态去哪儿”；不存在 HP 写回了但 MP/死亡/回收缺失 | “留尸”对敌我是否同义、是否需要持久化到地图层，需要先定规则 | P0 |
| 6. 统一三类核心节点结果出口 | `Assets/Tactics/Scripts/RoguelikeMap/Interaction/NodeInteractionManager.cs`，`Assets/Tactics/Scripts/RoguelikeMap/Interaction/StoreNodeHandler.cs`，`Assets/Tactics/Scripts/RoguelikeMap/Interaction/RestSiteNodeHandler.cs`，`Assets/Tactics/Scripts/RoguelikeMap/Interaction/TreasureNodeHandler.cs` | 让战斗节点、补给/休息节点、商店节点最终都产出同一种“节点处理结果”，由统一入口应用到状态并决定后续 UI/回图 | 三类节点不再各自直改状态；节点处理器只负责“产出结果”或“请求应用” | 现有 Store/Rest/Treasure 都在直接改状态和 UI，重构时容易牵连展示逻辑 | P1 |
| 7. 固化“可达性不受队伍状态影响”约束 | `Assets/Tactics/Scripts/Common/MapReachabilityUtility.cs`，`Assets/Tactics/Scripts/Roguelike/RoguelikeMapRuntimeState.cs`，`Assets/Tactics/Scripts/UI/RoguelikeMapUIController.cs` | 把“节点可达仅由图连通性 + 节点自身状态决定”写成明确规则，检查是否有队伍状态介入可达判断 | 任一节点是否可选，与 HP/MP/死亡人数无直接判断耦合 | UI 层可能已有隐式过滤，需防止业务规则藏在显示逻辑里 | P1 |
| 8. 建最小闭环验收清单/测试骨架 | `Assets/Tactics/Scripts/Common/Testing/Gameplay/MapGameplayStepAdapter.cs`，`Assets/Tactics/Scripts/Common/Testing/Gameplay/BattleGameplayStepAdapter.cs`，相关测试目录 | 先定义最小闭环验收：`战斗 -> 结算写回 -> 补给或商店 -> 再选点`；按状态字段列断言，而不是按 UI 细节列断言 | 至少能覆盖 Gold、HP、MP、成长、节点推进、再选点这几类断言 | 现有测试更偏战斗/技能，地图-结算-节点跨域链路测试可能不足 | P1 |

---

## 五、推荐开发顺序

### Phase 1：先收状态与结果结构
1. 任务 1：定义最小统一结果结构
2. 任务 2：收敛冒险态最小持久化字段
3. 任务 4：合并金币链路为单一真源

### Phase 2：打通主写回链
4. 任务 3：统一战斗结算写回入口
5. 任务 5：补齐 MP/死亡/留尸/战后回收链
6. 任务 6：统一三类核心节点结果出口

### Phase 3：闭环成立与验证
7. 任务 7：固化“可达性不受队伍状态影响”约束
8. 任务 8：建立最小闭环验收清单/测试骨架

---

## 六、最小前置集（如果只做最少）

如果当前只想先做最小可行闭环，建议优先完成：

1. 任务 2：收敛最小持久化字段
2. 任务 4：收口金币链
3. 任务 3：统一战斗结算写回入口
4. 任务 6：统一三类核心节点结果出口
5. 任务 8：最小闭环验收清单/测试骨架

完成这 5 项后，才更有资格说 `Task 1` 已具备“可证明闭环”的最低实现基础。

---

## 七、结合 gameplay-test-framework 的自动测试覆盖计划

### 结论
`gameplay-test-framework` **目前不足以直接覆盖 Task 1 的每个最小前置事项**。

它当前更擅长覆盖：
1. `Map -> Battle -> Node complete` 这类骨架级流程
2. 战斗内 HP/MP/Buff/位置/死亡等切片
3. 地图节点 visited/reachable/currentNode 一类状态

它当前明显不足以直接覆盖：
1. `PlayerAdventureState` / run state 写回断言
2. `Gold` 统一资源链断言
3. 商店点“资源兑现成现实能力”的节点语义
4. 同一局 2-3 轮最小闭环的完整自动化验收

### 覆盖策略

#### A. 现有框架可直接覆盖
1. 战斗点最小切片
2. 地图节点推进骨架
3. 尸体/死亡战场态

#### B. 只能部分覆盖，需要补断言/adapter
1. 结算结果写回单局状态
2. 商店 Gold 消费与兑现
3. 补给点恢复 `HP / MP`
4. 同一局 2-3 轮连续成立

#### C. 当前不应先覆盖，先做实现再接测试
1. 复杂节点推荐/警示逻辑
2. 稀有成长点 / 问号点 / 非战斗成长节点
3. 完整地图内容多样性

### 推荐测试承接能力补齐顺序

#### Phase T0：先补测试承接能力
1. 增加 run state 断言能力
   - `runGoldEquals`
   - `partyHpEquals`
   - `partyMpEquals`
   - `inventoryContains`
   - `equipmentAssigned`

2. 增加商店/补给领域动作
   - `buyShopItem`
   - `applyRestSite`
   - `assertShopPurchaseResult`

3. 增加最小闭环跨轮断言
   - `nodeSequenceContinues`
   - `runStatePersistsBetweenNodes`
   - `currentNodeProgressEquals`

#### Phase T1：为六项前置事项建立测试目标

| 前置事项 | 现有框架支持度 | 推荐测试形式 | 备注 |
|---|---|---|---|
| 1. 结算结果写回单局状态 | 部分 | 新增 run state 断言 | 当前无 `PlayerAdventureState` 直接断言 |
| 2. 地图层读取状态并保留上下文 | 部分 | Map fixture + 上下文断言 | 不测动态改可达性，只测状态在下一次选择前仍存在 |
| 3. 三类节点共享最小结果结构 | 低 | 单元/集成双层验证 | 更适合代码结构断言 + 轻量 fixture |
| 4. 三类节点最小可用实现 | 高/部分 | battle/map/shop/rest fixtures | 战斗/地图骨架已有，商店/补给需补动作 |
| 5. 最小地图流程跑通 | 部分 | 基于 `map-battle-node` 扩展新 fixture | 当前最适合从 map fixture 扩展 |
| 6. 同一局连续 2-3 轮成立 | 低 | 新建多轮 fixture | 当前缺跨轮断言能力 |

#### Phase T2：建议先落的 4 条最小 fixture
1. `battle-result-writeback.gameplay-test.md`
2. `restsite-repair-writeback.gameplay-test.md`
3. `shop-gold-conversion.gameplay-test.md`
4. `task1-min-loop-2rounds.gameplay-test.md`

### 一句话收口
`gameplay-test-framework` 现在还不能直接证明 Task 1 的每个最小前置事项，但已经足够作为“测试驱动实现计划”的骨架。最合理的做法是：

1. 先补 run state / 商店 / 补给 / 跨轮断言能力
2. 再让六项前置事项逐步进入自动化覆盖
