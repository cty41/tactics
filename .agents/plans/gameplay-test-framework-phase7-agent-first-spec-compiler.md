# Gameplay Test Framework Phase 7 计划（Agent-First Spec Compiler）

## Summary

- 目标：把 `Tools/gameplay-test-spec` 从“自由文本生成器”收束成“面向 AI agent 的受控 spec 编译/校验工具”，重点继续完善 `Skill` 自动化测试能力。
- 成功标准：
  - agent 不需要依赖关键词模板命中，也能稳定产出可执行 skill 测试 spec；
  - `validator` 成为主要防线，坏 spec 在 TS 层优先失败；
  - `compiler` 持续稳定生成 `*.plan.json`；
  - Unity 侧继续沿用现有文件驱动 PlayMode 执行链路。
- 当前明确不纳入本阶段：
  - `AP`
  - `Cooldown`
  - 强鲁棒自然语言解析器

## Current State

- 当前框架已经跑通 `Skill` 文件驱动链路：
  - `*.gameplay-test.md`
  - `validator`
  - `compiler`
  - `*.plan.json`
  - Unity PlayMode 执行
- 当前宿主侧 `generator.ts` 仍以关键词路由和固定模板函数为主：
  - `counter`
  - `mark`
  - `charge`
  - `aoe`
  - `buff`
  - `self-heal`
  - `single-target-damage`
- 当前更真实的瓶颈不是 NLP，而是 framework 接口面仍偏薄：
  - `targetSet` 还没有正式断言
  - `buff unique/stack` 还没有正式断言
  - `projectile lifecycle` 还没有正式断言或 probe
  - 多阶段状态仍主要依赖终态断言
- 当前 Skill 回归已覆盖：
  - `self-heal`
  - `single-target-damage`
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

## Relevant Context

- 宿主侧工具目录：`Tools/gameplay-test-spec/`
- Unity 执行层核心：
  - `Assets/Tactics/Scripts/Common/Testing/Gameplay/GameplayRuntimeRunner.cs`
  - `Assets/Tactics/Scripts/Common/Testing/Gameplay/SkillGameplayStepAdapter.cs`
- 运行时图工厂：
  - `Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphTestGraphFactory.cs`
- 正式 fixture：
  - `Tests/gameplay-specs/*.gameplay-test.md`
  - `Tests/gameplay-specs/*.plan.json`
- 当前真实运行时语义能力：
  - `CollectTargetsInArea`
  - `ForEachTarget`
  - `ApplyBuff`
  - `ProjectileLaunch`
  - `OnHit`
  - `DashToTarget`
- 当前文档工作流：
  - `gameplay-test-framework` skill 已作为 agent 使用说明入口
  - `generate-spec` 目前仍保留，但不应再作为长期主能力建设重点

## Key Changes

### 1. 重新定义宿主工具职责

- `generator.ts` 降级为可选薄层，不再承担"任意自然语言理解"的长期职责。
- 正式主入口改成两种：
  - agent 直接产出 `*.gameplay-test.md`
  - agent 先产出受控中间描述，再由工具转成 `ScenarioSpec`
- `validator.ts` 和 `compiler.ts` 升为主干能力；后续稳定性主要依赖这两层，而不是依赖自然语言模板命中。
- 当前阶段状态更新：部分新断言接口已在 `SkillGameplayStepAdapter` 中出现（如 `unitBuffCountEquals`、`unitBuffIsUnique`、`unitCountInArea`、`projectileLaunched`、`multiStageStateEquals` 等），但尚未全部形成正式 fixture + PlayMode 回归闭环。


### 2. 定义 agent-first 输入边界

- 新增一个受控输入约定，供 agent 输出，至少覆盖：
  - `feature`
  - `scenario`
  - `setup`
  - `actions`
  - `assertions`
  - `requiredAdapters`
  - `timeoutMs`
- 该输入可以是：
  - 完整 `ScenarioSpec`
  - 或更薄的中间对象，例如 `ScenarioDraft` / `AgentScenarioInput`
- 工具只承诺“受控输入可被稳定校验与编译”，不再承诺“任意自然语言都能理解”。

### 3. Skill 测试框架继续扩动作/断言，而不是扩 NLP

- 本阶段优先补 framework 缺口，而不是继续扩关键词覆盖面。
- 重点扩展这些正式接口：
  - `targetSet` / 命中目标集合相关断言
  - `buff unique/stack` 相关断言
  - `projectile lifecycle` 相关断言或 probe
  - 必要的多阶段执行状态观测
- 不新增 `Cooldown` 支持。
- 现有 `mark`、`counter`、`fireball`、`charge`、`applyBuff` 作为首批验证对象。

### 4. validator 改成真正的主防线

- 强化 `validator.ts` 的目标不是理解自然语言，而是保证：
  - alias 引用完整
  - graph/action/assertion 种类合法
  - 某动作需要的 `target` / `targetPoint` / `graphAlias` 已存在
  - 某断言需要的 `target` / `buffName` / `expected` 类型正确
  - 当前框架不支持的语义在 TS 层被明确拒绝
- 对 skill 语义补最小但明确的前置规则：
  - `areaDamage` 需要 target point
  - `applyBuff` 需要 `buffName` / `duration` / `selection`
  - `projectile` 类场景需要明确命中与完成条件
  - `targetSet` 类断言需要可观测目标集合来源

### 5. 文档和工作流同步

- 更新 `gameplay-test-framework` skill：
  - 明确“自然语言理解主要由 agent 负责”
  - 工具主链路改写为 `spec/controlled input -> validate -> compile -> run`
  - `generate-spec` 降级为辅助入口，而不是主推荐入口
- 更新现有主计划或关联计划中的框架定位表述：
  - 删除“增强 generator 自然语言覆盖面”的主线
  - 改成“Agent-first + validator/compiler 主导”
- 保留现有 fixture 和 PlayMode 入口，不改文件驱动测试结构。

## Interfaces / Data Flow

- 正式稳定契约保持不变：
  - `ScenarioSpec`
  - `ExecutableScenarioPlan`
- 可新增一个受控中间输入类型，例如：
  - `ScenarioDraft`
  - 或 `AgentScenarioInput`
- 期望数据流：
  1. 用户或设计文档给 agent 测试描述
  2. agent 输出受控描述或完整 `ScenarioSpec`
  3. `validator` 校验
  4. `compiler` 生成 `*.plan.json`
  5. Unity PlayMode 读取 `plan.json`
  6. `GameplayRuntimeRunner` 执行并返回结构化结果

## Test Plan

- TS：
  - `validator` 针对受控输入和正式 spec 做更严格负向测试
  - `compiler` 继续验证 `spec -> plan.json` 稳定输出
  - 若新增中间输入类型，补 `agent input -> spec` 的最小单测
- Unity PlayMode：
  - 保持现有 skill 文件驱动回归
  - 新增的动作/断言必须至少被 1-2 个真实 skill fixture 使用
  - 后续扩 `targetSet / buff unique / projectile / multi-stage probe` 时，每项都要有正式 plan 回归
- 验收标准：
  - agent 不需要依赖自由文本关键词命中才能产出可执行测试
  - 坏 spec 在 TS 层优先失败
  - skill 自动化能力扩展不再受 generator 模板数量限制

## Risks / Open Questions

- 如果 agent 输出边界定义不清，可能只是把不稳定性从 generator 挪到 prompt 层。
- 如果 validator 规则补得过浅，坏 spec 仍会漏到 Unity runtime 才失败。
- 如果先扩中间输入而不补断言/probe，agent 能表达的内容仍然会被 runtime 能力面限制。
- 当前 `generate-spec` 仍被现有 workflow 使用，收缩职责时要注意兼容，不要直接破坏已有脚本和 skill 示例。

## Assumptions

- 未来主要使用者是 AI agent，而不是人类直接手写自由文本。
- `Cooldown` 暂不纳入测试框架正式支持。
- 当前阶段仍然只做 `Skill` 自动化测试框架增强，不启动 `Battle/UI/Map`。
- `generate-spec` 可以继续保留做 MVP/兼容入口，但不再作为长期能力建设重点。

## Handoff Notes

- 新 session 实施前先读：
  - `Tools/gameplay-test-spec/src/generator.ts`
  - `Tools/gameplay-test-spec/src/validator.ts`
  - `Assets/Tactics/Scripts/Common/Testing/Gameplay/SkillGameplayStepAdapter.cs`
  - `Tests/gameplay-specs/mage-fireball.gameplay-test.md`
  - `Tests/gameplay-specs/hunter-mark.gameplay-test.md`
- 实施顺序建议：
  1. 先定义受控输入对象和 validator 边界
  2. 再补 `targetSet / buff unique / projectile / multi-stage` 对应接口
  3. 最后再收缩 `generator.ts` 的职责与 skill 文档
- 不要做的事：
  - 不要把 generator 重构成强自然语言解析器
  - 不要把 `Cooldown` 再拉回正式支持面
  - 不要改变现有 `plan.json` 主契约和 PlayMode 主入口
