# Gameplay Runtime Tester 计划

## Summary

- 目标：建立一套由 agent 通过 MCP 触发的 Gameplay Runtime Tester，能够基于 `.agents/docs/` 中的策划/设计文档自动生成“可执行测试用例文档”，并在 Unity Editor PlayMode 下执行玩法验证。
- 首阶段只覆盖战斗闭环：准备状态 -> 进入战斗 -> 推进或结束战斗 -> 结算 -> 返回地图/目标场景。
- 成功标准：
  - agent 能从设计文档生成测试用例文档；
  - agent 能通过项目内专用测试入口触发 MCP 执行；
  - 执行结果是结构化通过/失败，而不是依赖人工读日志。

## Current State

- 现有仓库已有少量测试基础，但没有“设计文档 -> 测试用例文档 -> MCP 执行”的完整链路。
- 当前可复用的主流程入口已经存在：
  - `BattleFlowCoordinator`
  - `BattleController`
  - `BattleSettlementCoordinator`
  - `RoguelikeFlowCoordinator`
  - `UIManager`
  - `PlayerAdventureStateStore`
- 现状缺口：
  - 没有统一的测试 spec 格式；
  - 没有统一 runner；
  - 没有统一断言与结果模型；
  - 没有项目内稳定的 MCP 测试触发入口。

## Relevant Context

- 关键代码入口：
  - `Assets/Tactics/Scripts/Flow/Battle/BattleFlowCoordinator.cs`
  - `Assets/Tactics/Scripts/Common/Battle/BattleController.cs`
  - `Assets/Tactics/Scripts/Common/Battle/BattleSettlementCoordinator.cs`
  - `Assets/Tactics/Scripts/Common/Roster/PlayerAdventureStateStore.cs`
  - `Assets/Tactics/Scripts/Common/UIManager.cs`
- 文档真相源：
  - 设计文档：`.agents/docs/`
  - 开发计划：`.agents/plans/`
  - 自动生成的测试用例文档：第一阶段也放 `.agents/docs/`
- 项目约束：
  - 资源加载必须走 `GameAssetManager`
  - 日志以 `TLog` / `TBattleLog` 为准
  - 首阶段运行环境固定为 Unity Editor PlayMode
  - 新增 `.cs` 后必须 `refresh_unity(compile="request")`

## Implementation Changes

### 1. 设计文档驱动的测试用例文档

- 上游输入固定为 `.agents/docs/` 中的设计文档，不直接以聊天自由文本作为正式输入。
- 定义固定结构的 Markdown 测试用例文档，至少包含：
  - `Feature`
  - `Scenario`
  - `Preconditions`
  - `Setup`
  - `Execution Steps`
  - `Assertions`
  - `Timeout / Retry`
  - `Required Scene / UI / Save Slot / Encounter`
- 第一阶段不引入复杂 DSL；优先做“人可读 + agent 易解析”的固定模板。

### 2. 项目内专用 MCP 测试入口

- 不让 agent 长期直接拼 `run_tests`、`manage_editor`、`execute_code` 完成整条流程。
- 项目内提供单一稳定入口，负责：
  - 接收测试用例文档路径或逻辑 ID；
  - 解析文档为运行时测试任务；
  - 触发对应 PlayMode 测试；
  - 返回结构化执行结果。
- 对 agent 来说，这个入口必须是单一触发点，而不是临时拼装的多步命令序列。

### 3. 运行时测试核心模型

- 新增最小核心类型：
  - `GameplayTestCaseSpec`
  - `GameplayRuntimeTestRunner`
  - `GameplayTestContext`
  - `GameplayTestResult`
  - `GameplayAssertion`
- 执行策略采用白盒编排：
  - 优先调用现有 coordinator / controller；
  - 不模拟玩家逐帧输入；
  - 通过结构化状态判定通过/失败。
- runner 需要统一等待能力：
  - 场景切换完成；
  - `BattleController` ready / active；
  - `BattleSettlementCoordinator` 阶段推进；
  - 地图 UI ready；
  - 指定 UI 显示/隐藏完成。

### 4. 首批战斗闭环用例

- 第一阶段至少实现 3 个用例：
  - `BattleEntrySmoke`
  - `BattleWinSettlementReturn`
  - `BattleLossReturn`
- 断言来源限定为结构化状态：
  - 当前场景
  - UI 可见性
  - `BattleController` 生命周期状态
  - `BattleSettlementCoordinator` 当前阶段
  - `PlayerAdventureStateStore` 中的关键字段变化
- 第一阶段不覆盖：
  - 视觉比对
  - 完整 Roguelike 多节点 run
  - 技能动画细节
  - 全部菜单/设置流程

### 5. Agent 执行链路

- 推荐固定流程：
  1. 读取设计文档；
  2. 生成或更新测试用例文档；
  3. 通过 MCP 调项目专用测试入口；
  4. 入口解析文档并触发 PlayMode 测试；
  5. 返回结构化结果；
  6. agent 基于失败步骤和断言定位问题。
- 若生产代码缺少稳定观测点，只允许补只读状态、事件或 probe helper，不允许加入业务作弊分支。

## Interfaces / Data Flow

- 数据流：
  - `.agents/docs/*.md` -> `.agents/docs/*-test-cases.md`
  - 测试用例文档 -> `GameplayTestCaseSpec`
  - `GameplayTestCaseSpec` -> `GameplayRuntimeTestRunner`
  - Runner -> coordinator / controller / state store
  - Runner -> `GameplayTestResult` -> MCP 返回给 agent
- 建议新增接口/类型：
  - `IGameplayTestSpecParser`
  - `IGameplayTestRunner`
  - `IGameplayAssertion`
  - `GameplayTestExecutionSummary`

## Test Plan

- 自动验证：
  - 新增 PlayMode 测试集，覆盖 3 个战斗闭环场景；
  - 验证“设计文档 -> 测试用例文档 -> MCP 执行 -> 结构化结果”闭环；
  - 新增 `.cs` 后执行编译和 Console 检查。
- 手工验证：
  - 用真实设计文档生成测试用例文档；
  - 再通过 MCP 触发执行；
  - 核对文档内容、执行行为、结果报告三者一致。
- 回归触发模块：
  - `BattleController`
  - `BattleFlowCoordinator`
  - `BattleSettlementCoordinator`
  - `PlayerAdventureStateStore`
  - `UIManager`

## Risks / Open Questions

- 单例和 `PlayerPrefs` 状态较多，setup/teardown 不严谨会导致测试互相污染。
- 某些流程缺少明确 ready 信号，可能需要补少量只读观测点。
- 文档抽取可能有语义歧义，第一阶段应限制输入文档风格。
- 首版只覆盖战斗闭环，不能误认为已具备完整 QA 自动化。

## Assumptions

- 上游策划输入以 `.agents/docs/` 中的设计文档为准。
- 自动生成的测试用例文档既给人看，也作为 agent 执行输入。
- 第一阶段必须有项目内专用测试入口。
- 第一阶段固定为 Unity Editor PlayMode。
- 第一阶段采用白盒编排和状态/事件断言。

## Handoff Notes

- 新 session 实施前先读：
  - `BattleFlowCoordinator`
  - `BattleController`
  - `BattleSettlementCoordinator`
  - `PlayerAdventureStateStore`
  - `UIManager`
- 第一实施步先跑通一个最小 `BattleEntrySmoke` 文档驱动闭环，再从重复逻辑中抽 `SpecParser / Runner / Assert / MCP 入口`。
- 不要一开始扩成通用平台；首版先把战斗闭环与文档驱动执行做稳。
