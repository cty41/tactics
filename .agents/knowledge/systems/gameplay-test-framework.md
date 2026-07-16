---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Tools/gameplay-test-spec
title: Gameplay Test Framework
description: 将 Agent 编写的受控 gameplay spec 编译为 Unity adapters 可执行的确定性计划。
tags: [testing, gameplay, automation, unity]
timestamp: "2026-07-15T10:35:31+08:00"
status: active
catalog_scope: gameplay-test-framework
repo_paths:
  - .agents/docs/gameplay-test-framework.md
  - .agents/skills/gameplay-test-framework/SKILL.md
  - Tools/gameplay-test-spec
  - Assets/Tactics/Scripts/Common/Testing/Gameplay
  - Tests/gameplay-specs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:ac0c40ecf08a3791382ed3df8e4423f1995d9efab28c61384760bf8d7a649343
---

# Current State

Agent 编写的 `.gameplay-test.md`/`ScenarioSpec` 经 TypeScript validator 和 compiler 生成 `.plan.json`，Unity `GameplayRuntimeRunner` 再通过 Skill、Battle、Map、UI adapters 执行 setup、action 和 assertion。源 Spec 是维护对象，plan 是生成物。

框架支持真实 Unity 资产，并已有生命、法力、Buff、位置、行动状态、投射物和多阶段等专用断言。adapter 与断言支持面以当前 schema、compiler、Unity 代码和 fixtures 为准，不再按历史 Phase 文档判断。

# Relationships

- Battle adapter 验证[Monster AI](monster-ai.md)与[Battle System](battle.md)。
- Map adapter 验证[Roguelike Run](roguelike-run.md)。
- Skill adapter 验证[SkillGraph](skill-graph.md)。
- 严格事件顺序、动画完成断言和 CI 接入记录在[Project Known Gaps](../plans/project-known-gaps.md)。

# Verification Guidance

修改 Spec 工具、adapter 或 fixtures 后运行工具测试、validate/compile 和对应 Unity PlayMode 测试。需要证明实际行为时必须加载真实资产，不能用手写结果或日志文本替代。

# Citations

[1] [Gameplay spec tool](https://github.com/cty41/tactics/tree/main/Tools/gameplay-test-spec)
[2] [Unity gameplay adapters](https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/Testing/Gameplay)
