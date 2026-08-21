---
type: Planning Backlog
resource: https://github.com/cty41/tactics/blob/main/.agents/docs/project-known-gaps.md
title: Project Known Gaps
description: 已从当前实现确认但尚未获批为活跃开发计划的集中缺口目录。
tags: [planning, backlog, gaps]
timestamp: "2026-08-21T14:19:43+08:00"
status: active
catalog_scope: project-known-gaps
repo_paths:
  - .agents/docs/project-known-gaps.md
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:def1db21930fb114da1c7eb337b308b1e9402f1c3103699258962dfc12276142
---

# Current State

权威缺口清单按 `verified-gap`、`needs-decision`、`deferred` 和 `idea` 区分确定性。当前覆盖 Monster AI、事件编辑器、Gameplay Test、SkillGraph、Godot Content Workbench、Adventure Tile 与公开发布整合、Pure Run 内容扩展、战斗反馈和配置硬编码。Adventure 已采用当前节点内的即时后继出口，不再使用开局 Route Overview；正式节点表现、出口信息/误触规范、旧存档提示以及公开 `main` 的资产 provenance 门禁仍是当前缺口。Workbench 的自动作者闭环已经完成，仅真实 Editor 的视觉、操作与 Assembly Reload 验收保持 deferred。自动门禁通过不等于人工项目完成。

`.agents/docs/demonbound-class-design.md` 是魔剑士数值与玩法语义权威；非大师技能、腐化/冥想、附身 AI、Run/Resource/Workbench 和自动测试已进入活跃实现。当前 `deferred` 只保留三个大师技能、正式角色美术、完整 VFX 与音频，人工 Run/可读性门禁则由验收账本跟踪。

该清单不是活跃开发计划。某项只有在满足文档中的激活条件、范围获得确认并建立可验收计划后才进入执行。

# Relationships

- 文档生命周期由[Project Documentation](../operations/project-documentation.md)管理。
- 缺口分别关联 [Battle System](../systems/battle.md)、[Monster AI](../systems/monster-ai.md)、[Roguelike Run](../systems/roguelike-run.md)、[SkillGraph](../systems/skill-graph.md)和[Gameplay Test Framework](../systems/gameplay-test-framework.md)。

# Verification Guidance

新增条目前必须给出当前代码、资产或测试证据；完成或失效条目应从权威清单移除，并同步相关系统页，而不是保留模糊 TODO。视觉、输入手感、完整 Run 和 Editor 生命周期只在人工验收账本维护，避免与实现缺口形成两份状态源。
