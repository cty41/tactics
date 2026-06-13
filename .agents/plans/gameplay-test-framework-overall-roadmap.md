# Gameplay Test Framework 总体路线图

## Summary

- 目标：把当前已经可用的 `Skill` 白盒自动化测试框架，逐步扩展成项目级 gameplay test framework。
- 当前已完成的核心闭环：
  - `Tools/gameplay-test-spec` 负责 `spec -> validate -> compile -> plan.json`
  - Unity 侧 `ExecutableScenarioPlanLoader + GameplayRuntimeRunner + SkillGameplayStepAdapter` 负责执行
  - `Tests/gameplay-specs/*.gameplay-test.md / *.plan.json` 作为正式 fixture
  - `Tactics.Tests.PlayMode` 已能跑 `Skill` 文件驱动回归
- 当前不再作为主线的内容：
  - `AP`
  - `Cooldown`
  - 强自然语言解析器
- 这份计划只回答“整个 test framework 还剩什么”，不替代具体 phase 实施计划。

## Current State

- 当前正式支持面仍然是 `Skill-only`。
- 当前 skill 回归已覆盖：
  - `self-heal`
  - `single-target-damage`
  - `invalid-graph`
  - `mana-success`
  - `mana-insufficient`
  - `out-of-range-failure`
  - `no-valid-target-failure`
  - `mark`
  - `counter`
  - `charge`
  - `aoe`
  - `ally-heal`
  - `apply-buff`
- 当前宿主侧已经开始转向 Agent-First：
  - agent 可直接输出 `ScenarioDraft` / `ScenarioSpec`
  - `validator` 与 `compiler` 已是主干
  - `generate-spec` 保留为兼容入口
- 当前执行环境仍是轻量 `SkillGraphTestWorld`：
  - 不加载真实 Unity 场景
  - 不走 `GameAssetManager`
  - 不验证正式资产装配链

## Remaining Roadmap

### 1. Skill 能力面补齐

- 这是最近阶段的主线，目标是把当前 skill 框架从“够用”扩到“真实回归可持续”。
- 还需要补的正式接口：
  - `targetSet` / 命中目标集合断言
  - `buff unique/stack` 行为断言
  - `projectile lifecycle` 断言和 probe
  - 多阶段执行状态观测
- 已经有底层运行时能力、但尚未形成完整正式回归的重点对象：
  - `fireball`
  - `mark`
  - `counter`
  - `applyBuff`
  - 未来的 projectile-based skills
- 完成标志：
  - 新断言在 `validator`、`SkillGameplayStepAdapter`、fixture、PlayMode 回归中全部落地
  - 这些能力不再只存在于 runtime，而是存在于正式测试接口里

### 2. Agent-First 输入链收口

- 当前方向已经确定，但还需要继续收口：
  - 让 `ScenarioDraft` / `ScenarioSpec` 成为正式主入口
  - 把 `generator.ts` 进一步降级为兼容层
  - 把 `validator` 做成真正的主防线
- 还需要补的内容：
  - 受控中间输入对象的稳定约定
  - 更强的 TS 语义校验
  - agent 输出模板与 skill 文档统一
  - fixture round-trip 与 controlled input round-trip 回归
- 完成标志：
  - agent 不依赖关键词命中，也能稳定生成可执行 spec
  - 坏 spec 优先在 TS 层失败，而不是漏到 Unity runtime

### 3. 真实内容集成测试层

- 当前 skill 框架测试的是 runtime 逻辑，不是项目内容集成链。
- 后续需要新增一层“真实内容模式”，至少补：
  - 真实 `SkillGraphAsset` 载入
  - 正式技能配置资产载入
  - 必要时接入 `GameAssetManager`
  - 区分“测试图工厂模式”和“真实资产模式”
- 这一步的目标不是替代现有轻量白盒测试，而是新增一层更高置信度的内容回归。
- 完成标志：
  - 同一个框架可以跑两类 skill 测试：
    - 轻量 graph-factory 逻辑回归
    - 真实资产集成回归

### 4. Battle Adapter

- 这是从 `Skill framework` 走向 `Gameplay framework` 的第一道真正扩展。
- 最小目标：
  - 新增 `BattleAdapter`
  - 支持 battle world / battle-ready context
  - 支持进入战斗、推进回合、结束战斗、读取 battle probe
  - 支持战斗结果、单位状态、回合流的基础断言
- 不要求一开始就覆盖完整 Roguelike 流程，但要形成独立正式 adapter。
- 完成标志：
  - `requiredAdapters` 不再只有 `Skill`
  - 有文件驱动的 battle plan fixture 和 PlayMode 回归

### 5. UI / Map / Roguelike Adapter

- 在 `BattleAdapter` 成立后，后续才有意义扩到：
  - UI 测试步骤
  - Map / Roguelike 节点与事件测试
  - 场景切换与返回链路
- 这一层的目标是把“纯 runtime skill 测试”扩展成“gameplay flow 测试”。
- 重点能力：
  - 场景加载步骤
  - 正式资产加载步骤
  - UI 可见性与状态断言
  - Map 节点进入 / 事件 / 战斗 / 返回链
- 完成标志：
  - 能正式跑 battle/map/ui 的多 adapter 计划，而不是只跑 skill graph

### 6. 结果与执行基础设施

- 这部分不是玩法能力，但决定框架是否能长期用。
- 还需要逐步补齐：
  - 更好的结果摘要和失败分类
  - fixture/tag/filter 级执行选择
  - 批量 spec 生成的正式 CLI 能力
  - CI / 自动回归入口
  - 更清晰的 probes 与失败诊断输出
- 完成标志：
  - agent、工程师、策划都能稳定看懂结果
  - 大量 fixture 的运行和筛选不依赖手工点测试名

## Recommended Order

1. 先完成 `Skill` 断言 / probe / controlled input 收口  
2. 再补“真实资产模式”  
3. 再做 `BattleAdapter`  
4. 最后扩 `UI / Map / Roguelike`  
5. 在上述各阶段持续补执行与结果基础设施

## Interfaces / Public Surface

- 当前应保持稳定：
  - `ScenarioSpec`
  - `ExecutableScenarioPlan`
  - `GameplayRuntimeRunner`
  - `IGameplayStepAdapter`
- 后续新增建议：
  - `ScenarioDraft` / `AgentScenarioInput`
  - `BattleAdapter`
  - 真实资产载入相关 setup/action
  - `targetSet` / `projectile` / `multi-stage` / `buff unique/stack` 断言
- 不建议近期改动：
  - `schemaVersion`
  - 当前 `plan.json` 主契约
  - 现有 `Tests/gameplay-specs` 文件驱动模式

## Test Plan

- Skill 阶段：
  - TS `validator/compiler` 回归持续全绿
  - PlayMode skill fixture 持续文件驱动
  - 每新增断言至少有一个正式 fixture 覆盖
- 真实资产阶段：
  - 同一 skill 同时具备 graph-factory 和 asset-backed 两类回归
- Battle 阶段：
  - battle lifecycle、turn flow、settlement 的 plan 回归
- UI/Map 阶段：
  - 场景加载、节点交互、返回链路的多 adapter 回归
- 基础设施阶段：
  - 批量执行、tag/filter、结果汇总、CI 入口验证

## Assumptions

- `AP` 将废除，`Cooldown` 暂不作为正式测试框架能力建设主线。
- 未来主要使用者是 AI agent；自然语言理解应更多放在 agent 层，而不是 TS 生成器层。
- 当前 `SkillGraphTestWorld` 仍然有价值，不会因为后续扩真实资产模式而被替换；两者应该并存。
- 框架近期的主要增量仍然是 `Skill`，不是直接跳到 `UI/Map`。

## Handoff Notes

- 新 session 先读：
  - `.agents/plans/gameplay-runtime-tester-计划.md`
  - `.agents/plans/gameplay-test-framework-phase7-agent-first-spec-compiler.md`
  - `.agents/skills/gameplay-test-framework/SKILL.md`
  - `Tools/gameplay-test-spec/src/validator.ts`
  - `Assets/Tactics/Scripts/Common/Testing/Gameplay/SkillGameplayStepAdapter.cs`
- 先判断当前任务属于哪一层：
  - `Skill` 接口补齐
  - Agent-first 输入收口
  - 真实资产集成
  - `BattleAdapter`
  - `UI/Map` 扩展
- 不要在没有独立 phase 计划的情况下，直接同时推进 `Skill + Battle + UI` 三层。
