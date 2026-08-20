---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Tools/gameplay-test-spec
title: Gameplay Test Framework
description: 将受控 gameplay spec 编译为 Godot runtime runner 可执行的确定性计划。
tags: [testing, gameplay, automation, godot]
timestamp: "2026-08-20T21:53:49+08:00"
status: active
catalog_scope: gameplay-test-framework
repo_paths:
  - .agents/docs/gameplay-test-framework.md
  - .agents/skills/gameplay-test-framework/SKILL.md
  - Tools/gameplay-test-spec
  - godot/tests/GameplaySpec
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunTestContext.cs
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunMain.cs
  - Tests/gameplay-specs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:4c1b21b5a182a1445076047e5c3dcfdc56722daa04a17f4b0ba920584b50ec27
---

# Current State

维护对象是 `.gameplay-test.md`/`ScenarioSpec`；TypeScript validator/compiler 生成声明 Godot runtime、capability、adapter、checkpoint、隔离存档和 watchdog 的 plan。目标 runtime 不支持的步骤、错误 adapter 与被篡改的 capability/checkpoint 均 fail-closed，生成 plan 不手改。

`GodotGameplayRuntimeRunner` 加载正式 `Main.tscn`，并通过 `Viewport.PushInput` 驱动生产 GUI/Input 链。每个场景使用隔离 `user://qa-runner/<scenario>/<attempt>/`，执行前后验证生产主档与 backup 未变化，并在退出时释放 Main、临时节点和隔离目录。

框架覆盖 Battle、Map、UI、Skill、Adventure 和作者 spec。Adventure 合同使用 `exitCommitted`、`immediateSuccessorNodeIdsEqual` 和权威 Adventure revision/state hash 验证节点内即时出口，不再表达全局 RouteNode 预提交。自动化证明规则、事务、生产输入、重载和清理；视觉、动画、可读性、Editor Assembly Reload 与操作手感仍由人工验收。

# Relationships

- Battle 场景验证 [Battle System](battle.md)与[Monster AI](monster-ai.md)。
- Skill 场景验证 [SkillGraph](skill-graph.md)。
- Map/Adventure 场景验证 [Roguelike Run](roguelike-run.md)。

# Verification Guidance

修改 schema、compiler、adapter 或 runner 后，运行 TypeScript 测试、源 spec validate/compile、Godot runtime batch 和清理断言。玩家输入场景的状态变化必须来自生产输入链，不能以直接调用业务服务代替。
