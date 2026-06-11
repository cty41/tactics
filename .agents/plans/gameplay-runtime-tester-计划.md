# Gameplay Test Framework Phase 4 计划（基础设施收口与防漂移）

## Summary

- 目标：把当前 gameplay test framework 收口成一套一致、可回归、可交给 agent 稳定使用的文件驱动工具链。
- 当前正式链路固定为：
  - 自然语言 / 设计文档
  - `Tools/gameplay-test-spec` 生成 `*.gameplay-test.md`
  - `Tools/gameplay-test-spec` 编译 `*.plan.json`
  - Unity PlayMode 读取 `Tests/gameplay-specs/*.plan.json`
  - `GameplayRuntimeRunner` 执行并返回结构化结果
- 当前稳定支持的 MVP 路径：
  - `Skill` adapter
  - self heal
  - single target damage
  - mana success / insufficient / out of range / no valid target
  - invalid graph rejected before execution

## Key Changes

### 1. 统一文档与实现

- 重写本计划和 `.agents/skills/gameplay-test-framework/SKILL.md`，确保它们描述的是当前真实实现，而不是旧的 battle-only 设想。
- 所有正式样例 fixture 统一放在 `Tests/gameplay-specs/`，由 TS 工具和 Unity PlayMode 共用。

### 2. 默认回归收口

- `Tools/gameplay-test-spec/package.json` 的 `npm test` 必须跑完整个 `dist/tests` 下的测试，而不是只跑单个文件。
- TS 默认回归至少覆盖：
  - `compiler.test.ts`
  - `ability-resource.test.ts`
- 新增 ability 资源测试必须通过 fixture 驱动，而不是继续在测试里手工维护 `ExecutableScenarioPlan`。

### 3. Unity 侧硬校验与超时

- `ExecutableScenarioPlanLoader` 必须在反序列化后做最小结构校验，至少拒绝：
  - `schemaVersion != 1`
  - 空 `requiredAdapters`
  - 空 `runtimeActions`
  - 空 `assertionPlans`
  - 缺 `adapter` / `kind` 的 action/assertion/probe
- `GameplayRuntimeRunner` 必须尊重 `plan.TimeoutMs`，超时后返回失败结果和明确诊断。

### 4. 负向回归面

- 新增坏 plan fixture，覆盖：
  - unsupported schema version
  - missing required adapters
  - missing runtime actions
  - missing action metadata
- Unity PlayMode 测试要断言错误消息，而不是只断言“抛异常了”。

## Test Plan

- `Tools/gameplay-test-spec`：
  - `npm test` 必须 100% 通过
  - fixture round-trip 测试必须通过
- Unity PlayMode：
  - `GameplayRuntimePlanTests` 继续通过文件驱动 fixture
  - `GameplayRuntimeAbilityPlanTests` 继续通过文件驱动 fixture
  - loader negative tests 必须通过
  - timeout test 必须通过
- 文档：
  - 本计划与 `gameplay-test-framework` skill 必须和代码保持一致，避免下个 session 继续看到旧的 battle-centric 描述

## Assumptions

- 本阶段不扩 skill 语义，不引入 Buff/AoE/AP/Cooldown 等新断言。
- `schemaVersion` 继续固定为 `1`。
- Unity 侧只做执行安全所需的最小 plan 校验，不复制一整套 TS schema。
- `timeoutMs` 只表示整个场景的总超时，不定义 step 级超时。
