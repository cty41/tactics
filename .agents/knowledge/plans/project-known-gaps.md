---
type: Planning Backlog
resource: https://github.com/cty41/tactics/blob/main/.agents/docs/project-known-gaps.md
title: Project Known Gaps
description: 已从当前实现确认但尚未获批为活跃开发计划的集中缺口目录。
tags: [planning, backlog, gaps]
timestamp: "2026-07-23T04:13:54+08:00"
status: active
catalog_scope: project-known-gaps
repo_paths:
  - .agents/docs/project-known-gaps.md
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:e4d35b0e657887807aad11144f214dc99e631b7398ca52b64043c2ddacfeff38
---

# Current State

权威缺口清单按 `verified-gap`、`needs-decision`、`deferred` 和 `idea` 区分确定性。当前覆盖 Monster AI、事件编辑器、Gameplay Test、SkillGraph、Pure Run 内容扩展、战斗反馈和配置硬编码。

该清单不是活跃开发计划。某项只有在满足文档中的激活条件、范围获得确认并建立可验收计划后才进入执行。

# Relationships

- 文档生命周期由[Project Documentation](../operations/project-documentation.md)管理。
- 缺口分别关联 [Battle System](../systems/battle.md)、[Monster AI](../systems/monster-ai.md)、[Roguelike Run](../systems/roguelike-run.md)、[SkillGraph](../systems/skill-graph.md)和[Gameplay Test Framework](../systems/gameplay-test-framework.md)。

# Verification Guidance

新增条目前必须给出当前代码、资产或测试证据；完成或失效条目应从权威清单移除，并同步相关系统页，而不是保留模糊 TODO。
