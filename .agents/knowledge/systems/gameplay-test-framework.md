---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Tools/gameplay-test-spec
title: Gameplay Test Framework
description: 将自然语言 gameplay spec 编译为可由 Unity runtime adapters 执行的确定性测试计划。
tags: [testing, gameplay, automation, unity]
timestamp: "2026-07-14T23:27:23+08:00"
status: active
catalog_scope: gameplay-test-framework
repo_paths:
  - Tools/gameplay-test-spec
  - Assets/Tactics/Scripts/Common/Testing/Gameplay
  - Tests/gameplay-specs
verified_revision: d5f1730d3527
source_fingerprint: sha256:5d4ab233e01cba7e6371f498b746679e238e8583282891667409a3e92611ad37
---

# Current State

TypeScript 编译器和 validator 将作者编写的 gameplay spec 编译为稳定的 `.plan.json`。Unity 运行时通过 Battle、Map、Skill 和 UI adapters 执行动作与断言。

Pure Run 怪物首版补充了固定 run seed、strict asset、角色等级/SkillId 断言，以及结构化 AI 回合结果字段，包括技能、目的地、目标点、目标数量、fallback 与 Pattern 步骤。计划文件仍由编译器生成，不能绕过源 spec 手写。

# Relationships

- Battle adapter 验证[Monster AI](monster-ai.md)和[Battle System](battle.md)的真实运行结果。
- Map adapter 验证[Roguelike Run](roguelike-run.md)的地图种子与成长状态。
- Skill adapter 验证[SkillGraph](skill-graph.md)的目标与执行语义。

# Verification Guidance

修改 compiler、validator、runtime adapters 或 specs 后，运行 `npm test`，并编译对应 Unity Editor/PlayMode 测试程序集。真实 AI 断言必须加载实际 Brain，不能用手写结果替代。
