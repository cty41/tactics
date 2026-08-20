---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/src/Tactics.Core/Skills
title: SkillGraph
description: Godot Pure Run 的技能定义、解释执行、共享目标规则和 typed authoring 主链。
tags: [gameplay, skills, skill-graph, godot]
timestamp: "2026-08-20T20:45:05+08:00"
status: active
catalog_scope: skill-graph
repo_paths:
  - src/Tactics.Core/Skills
  - src/Tactics.Application/Content
  - src/Tactics.Application/Authoring
  - godot/content/skills
  - godot/src/Tactics.Godot.Adapter/Editor
  - godot/src/Tactics.Godot.Adapter/Runtime
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:69eda98326054e598fd0caafa3a78f89bb6aeaa0fb3444ef0d698861cd902cba
---

# Current State

`SkillDefinition`、目标协议与 `BattleTransitionService` 是技能玩法语义权威。玩家输入、AI、预览和执行前重验证共享射程、LOS、阵营、资源、次数、状态与目标结论；表现缺失或取消不得改变玩法提交边界。

Godot typed Resource 与 Catalog 保存正式技能和 Presentation 引用。Tactics Tooling 通过 Application typed ChangeSet、revision、Undo/Redo、ResourceSaver 和 reload-safe bridge 修改资源；不允许手写 `.tres/.tscn` 或开放任意属性 patch。

表现层只消费强类型玩法结果并负责动作、投射物、状态反馈、时间线与清理。当前程序化表现是可玩基线，不代表正式 VFX 或人工体验通过。

# Relationships

- [Battle System](battle.md)提供单位、棋盘、目标和效果环境。
- [Monster AI](monster-ai.md)复用相同合法性与执行入口。
- [Gameplay Test Framework](gameplay-test-framework.md)验证目标、状态和旅程结果。
- [Roguelike Run](roguelike-run.md)提供成长、装备和局内持久化上下文。

# Verification Guidance

验证技能时同时核对 Core 定义/transition、Application 作者合同、Godot Resource/Catalog、运行时表现与对应测试。自动测试不替代真实 Editor Reload、视觉和手感验收。
