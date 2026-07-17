# Pure Run 三人小队 Vertical Slice 开发计划

## Background

- 当前问题：现有项目的 Roguelike 外围和战斗系统默认沿着“搜打撤 + 带出收益”方向演进，但最新设计已切换为 `纯 run 构筑`。如果继续沿用旧的奖励与结算思路，会把验证目标混成两套：一套验证带出收益，一套验证 run 内成型，最终无法判断到底什么好玩。
- 当前现状：项目已经具备可用的战斗主链、FTL 风格星图、非战斗节点基础、战斗结算与奖励框架、职业与技能基础。但这些能力仍偏向“跨节点保存收益”和“地图层持续推进”，尚未收敛为 `固定三人小队 + run 内成长清空 + 血量/资源磨损驱动` 的一体化原型。
- 目标：先做一个最小可玩 vertical slice，能让玩家用固定三人小队连续完成至少 3 场战斗/节点推进，并在过程中真实感受到职业分工、技能联动、装备分配和资源磨损压力。
- 预期收益：
  - 以最小成本验证 pure run 方向是否比搜打撤方向更容易做出“马上想再开一局”的体验。
  - 尽早识别核心风险：三人协同是否成立、装备掉落是否有分配价值、资源磨损是否只是补给税。
  - 为后续完整原型提供一条清晰主线，而不是继续在两套玩法身份之间摇摆。

## Scope

### In Scope

- 固定三人小队的 run 内成长切片：职业分工、技能联动、装备与消耗品分配。
- FTL 星图上的最小推进闭环：战斗节点、宝箱/商店/补给节点、节点后继续推进。
- 纯 run 语义下的奖励与结算改写：不带出、不保留跨局成长，只服务当前 run。
- 以血量/资源磨损为核心的推进压力。
- 一条可以连续玩 3 场战斗的最小体验链路。

### Out of Scope

- 完整职业阵容与完整职业平衡。
- 大型装备池、稀有度体系和完整掉落表。
- 复杂事件系统和大量文本内容。
- 长期局外成长、永久解锁和带出收益机制。
- 自动战斗、复杂 AI 重写、联网功能。
- 大规模项目结构重构；仅在服务本切片时做最小边界调整。

## File Structure

- `Assets/Tactics/Scripts/Flow/RoguelikeFlowCoordinator.cs` — 协调 pure run 的启动、结束和场景间流程收口。
- `Assets/Tactics/Scripts/Roguelike/RoguelikeBattleReturnHandler.cs` — 战斗结束后返回星图，并应用 pure run 语义下的节点结果与小队状态。
- `Assets/Tactics/Scripts/RoguelikeMap/RoguelikeSettlementContext.cs` — 承载当前 run 的结算上下文，收纳“本局继续推进”的必要状态。
- `Assets/Tactics/Scripts/RoguelikeMap/RunSummary.cs` — run 结束时汇总本局结果，用于原型复盘而非局外继承。
- `Assets/Tactics/Scripts/RoguelikeMap/Interaction/NodeInteractionManager.cs` — 将节点交互统一到 pure run 节奏，串联战斗、补给、商店与宝箱。
- `Assets/Tactics/Scripts/RoguelikeMap/Interaction/TreasureNodeHandler.cs` — 处理装备/消耗品掉落与拾取。
- `Assets/Tactics/Scripts/RoguelikeMap/Interaction/StoreNodeHandler.cs` — 处理商店购买与 run 内经济消耗。
- `Assets/Tactics/Scripts/RoguelikeMap/Interaction/RestSiteNodeHandler.cs` — 处理最小补给节点，提供资源恢复而非长期成长。
- `Assets/Tactics/Scripts/RoguelikeMap/Interaction/RoguelikeRewardHelper.cs` — 统一 pure run 奖励结构，确保结果立即影响当前小队。
- `Assets/Tactics/Scripts/RoguelikeMap/Economy/RunGoldManager.cs` — 管理 run 内金币与消耗，不向局外持久层泄漏主成长。
- `Assets/Tactics/Scripts/Common/Battle/BattleSettlementCoordinator.cs` — 保留战斗结算流程，但改成服务当前 run 的状态更新与复盘。
- `Assets/Tactics/Scripts/UI/BattleSettlementUIController.cs` — 展示本场战斗后的小队状态变化与即时奖励。
- `Assets/Tactics/Scripts/UI/RoguelikeMapUIController.cs` — 呈现星图推进、节点选择和当前小队磨损状态。
- `Assets/Tactics/Scripts/UI/InventoryUIController.cs` — 用于三人小队的装备分配与消耗品管理。
- `Assets/Tactics/Scripts/UI/SkillSelectionUIController.cs` — 提供最小技能成长入口，强化角色分工。
- `Assets/Tactics/Scripts/Common/Battle/BattleController.cs` — 维持三人小队入场、战斗结束和状态回传的运行时锚点。
- `Assets/Tactics/Scripts/Common/Units/Unit.cs` — 承载 HP、资源、装备效果等战斗运行时状态。

## Assumptions

- 允许修改运行时代码、共享基础库/API、工程化/工具链、项目结构边界；但仍以最小改动为准，不主动做无关重构。
- 首轮 vertical slice 允许继续复用现有职业与技能骨架，不要求一次性重做所有职业表达。
- 首轮验证的“能连续玩 3 场战斗”以单个短 run 中连续推进为准，不要求完整最终 Boss 闭环。

## Tasks

### Task 1: 定义 pure run 的运行时状态边界

- 目标：把“当前 run 持有什么、战斗后更新什么、run 结束清空什么”明确收口，避免继续沿用带出收益语义。
- 输入：`2026-06-24-pure-run-squad-prototype-design.md`、现有 Roguelike 结算与返回链路。
- 输出：一套最小 pure run 状态模型，覆盖三人小队当前装备、消耗品、金币、HP/资源磨损和 run 结束汇总。
- 涉及文件：
  - Modify: `Assets/Tactics/Scripts/Flow/RoguelikeFlowCoordinator.cs`
  - Modify: `Assets/Tactics/Scripts/Roguelike/RoguelikeBattleReturnHandler.cs`
  - Modify: `Assets/Tactics/Scripts/RoguelikeMap/RoguelikeSettlementContext.cs`
  - Modify: `Assets/Tactics/Scripts/RoguelikeMap/RunSummary.cs`
- 验收标准：
  - 战斗返回星图后，小队当前 HP、资源、金币和本场获得物会保留到当前 run 后续节点。
  - 结束当前 run 时，主成长不会作为下一局起始状态继续继承。
  - 代码中不再把“装备带出”“撤离收益”作为首轮 vertical slice 的必经流程。

### Task 2: 跑通最小星图推进闭环

- 目标：让玩家从星图进入战斗、完成结算、回到星图并继续推进到下一节点。
- 输入：现有 FTL 星图、节点交互框架、战斗返回流程。
- 输出：一个可连续推进至少 3 个节点的最小 run 闭环。
- 涉及文件：
  - Modify: `Assets/Tactics/Scripts/RoguelikeMap/Interaction/NodeInteractionManager.cs`
  - Modify: `Assets/Tactics/Scripts/UI/RoguelikeMapUIController.cs`
  - Modify: `Assets/Tactics/Scripts/Roguelike/RoguelikeBattleReturnHandler.cs`
  - Modify: `Assets/Tactics/Scripts/Flow/RoguelikeFlowCoordinator.cs`
- 验收标准：
  - 玩家可以从星图连续进入至少 3 次节点交互，其中至少 2 次为战斗节点。
  - 每次节点完成后，星图 UI 能正确回到当前 run，并允许继续选择下一节点。
  - 当前节点结果会影响下一次决策所见的小队状态信息。

### Task 3: 落地三人小队的最小构筑表达

- 目标：让 fixed squad 的职业分工、技能联动和小队职责在首轮就能被观察到。
- 输入：现有角色/职业/技能骨架，pure run 三人小队设计。
- 输出：一套最小但可辨识的三人小队配置，支持技能成长或技能选择影响角色分工。
- 涉及文件：
  - Modify: `Assets/Tactics/Scripts/Common/Battle/BattleController.cs`
  - Modify: `Assets/Tactics/Scripts/Common/Units/Unit.cs`
  - Modify: `Assets/Tactics/Scripts/UI/SkillSelectionUIController.cs`
  - Modify: `Assets/Tactics/Scripts/UI/BattleSettlementUIController.cs`
  - Reference: `Assets/Tactics/Scripts/Common/Units/Classes/*` 或现有角色配置入口
- 验收标准：
  - 三人小队在一场战斗内能体现至少 3 个不同职责，例如输出、控制、保护/补位。
  - 至少有 1 次技能成长会改变玩家对角色定位或操作顺序的判断。
  - 玩家在战斗后能说出“这件成长是给谁、为什么给他”。

### Task 4: 落地装备与消耗品的即时分配价值

- 当前状态：已完成实现、自动化验证和 Unity 人工 UI/交互验收；稳定规则见 [Pure Run 三人小队原型设计](../docs/2026-06-24-pure-run-squad-prototype-design.md)，实现与回归入口见 `Assets/Tactics/Scripts/Common/Roster/CharacterLoadoutService.cs`、`Assets/Tactics/Scripts/UI/InventoryUIController.cs` 和 `Assets/Tactics/Tests/PlayMode/ConsumableBattleUseTests.cs`。
- 目标：让掉落不是单纯数值奖励，而是小队内部的分配选择。
- 输入：Treasure/Store 节点、Inventory UI、现有装备与背包交互。
- 输出：最小装备槽与消耗品槽分配体验，支持战斗掉落、宝箱或商店获得后立即进入当前 run 构筑。
- 涉及文件：
  - Modify: `Assets/Tactics/Scripts/RoguelikeMap/Interaction/TreasureNodeHandler.cs`
  - Modify: `Assets/Tactics/Scripts/RoguelikeMap/Interaction/StoreNodeHandler.cs`
  - Modify: `Assets/Tactics/Scripts/RoguelikeMap/Interaction/RoguelikeRewardHelper.cs`
  - Modify: `Assets/Tactics/Scripts/UI/InventoryUIController.cs`
  - Modify: `Assets/Tactics/Scripts/RoguelikeMap/Economy/RunGoldManager.cs`
- 验收标准：
  - 至少存在 2 种不同来源的当前 run 奖励，例如战斗掉落与商店购买。
  - 获得装备或消耗品后，玩家可以在三人之间做分配，而不是只能自动装备给单人。
  - 分配结果会在下一场战斗中立刻生效，并能感受到职责侧重变化。

### Task 5: 让血量与资源磨损成为推进压力

- 目标：把推进压力从“能否带出”切换为“小队还能否继续推进”。
- 输入：当前战斗结果、小队 HP/资源、补给节点逻辑。
- 输出：一套最小磨损闭环，推动玩家在战斗、商店和补给节点之间做取舍。
- 涉及文件：
  - Modify: `Assets/Tactics/Scripts/Roguelike/RoguelikeBattleReturnHandler.cs`
  - Modify: `Assets/Tactics/Scripts/RoguelikeMap/Interaction/RestSiteNodeHandler.cs`
  - Modify: `Assets/Tactics/Scripts/RoguelikeMap/Economy/RunGoldManager.cs`
  - Modify: `Assets/Tactics/Scripts/UI/RoguelikeMapUIController.cs`
  - Modify: `Assets/Tactics/Scripts/Common/Units/Unit.cs`
- 验收标准：
  - 连续 3 场推进中，角色 HP、技能资源或消耗品库存会真实变化，并影响后续节点偏好。
  - 补给节点的价值来自恢复当前小队状态，而不是提供长期数值成长。
  - 玩家在推进中会出现“要不要先补给再打下一场”的真实判断。

### Task 6: 建立 vertical slice 的试玩与验证回路

- 目标：让这次开发不是“代码能跑”就结束，而是能直接判断 pure run 方向是否成立。
- 输入：前 5 个任务产出的最小可玩切片。
- 输出：一套可重复执行的试玩流程、记录口径和回归检查。
- 涉及文件：
  - Modify: `Assets/Tactics/Scripts/UI/RoguelikeMapUIController.cs`
  - Modify: `Assets/Tactics/Scripts/UI/BattleSettlementUIController.cs`
  - Add or Modify: `Tests/gameplay-specs/` 下与 map/battle integration 相关的用例文档
  - Optional Modify: 现有测试或调试入口，若需要最小辅助按钮/日志
- 验收标准：
  - 能按固定流程完成 1 个短 run，至少包含 3 次连续推进和 3 场战斗观察。
  - 试玩记录可以回答：三人协同是否成立、装备分配是否有价值、资源磨损是否只是补给税。
  - 至少有 1 条回归检查覆盖“战斗返回星图后继续推进”的关键链路。

## Risks & Open Questions

- 现有 Roguelike 运行时状态可能默认依赖跨节点持久化甚至跨局持久化，若边界不清，Task 1 会决定后续任务复杂度。
- 三人小队的“协同感”很容易退化成三个角色分别变强；如果 Task 3 只做成数值增长，整个方向会被误判为无聊。
- 如果掉落仍然主要体现为更高数值，而非分配与职责变化，Task 4 会直接削弱 pure run 的重开动力。
- 资源磨损最容易做成“补给税”；Task 5 必须确保它改变节点偏好，而不是单纯拖慢节奏。
- 如果现有测试骨架不足，Task 6 需要先补一条最小 map-battle-playtest 验证路径。

## 验证方式

- 运行 1 个最小 pure run：从星图开始，连续完成至少 3 次推进，其中至少 2 次战斗、1 次非战斗节点交互。
- 检查战斗返回星图后，小队状态是否正确保留到当前 run 后续节点。
- 检查至少 1 次装备或消耗品获取是否产生“给谁用”的决策。
- 检查至少 1 次补给或商店选择是否因为当前小队磨损状态而发生。
- 使用现有 gameplay-spec 或新增最小集成用例，覆盖“星图进入战斗 -> 战斗结算 -> 返回星图 -> 继续推进”的主链路。

## 推荐执行顺序

1. Task 1: 定义 pure run 的运行时状态边界
2. Task 2: 跑通最小星图推进闭环
3. Task 5: 让血量与资源磨损成为推进压力
4. Task 4: 落地装备与消耗品的即时分配价值
5. Task 3: 落地三人小队的最小构筑表达
6. Task 6: 建立 vertical slice 的试玩与验证回路

说明：先把 run 的状态边界和推进链路收口，再补压力与奖励，最后强化职业构筑表达。这样即使中途停止，也能尽早得到一条可试玩、可复盘的真实链路。
