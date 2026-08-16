---
type: Project Architecture
resource: https://github.com/cty41/tactics
title: Tactics Project Overview
description: Tactics 的 Godot 产品主线、纯 .NET 分层、运行时和主要游戏系统总入口。
tags: [architecture, godot, agent-first]
timestamp: "2026-08-17T01:26:53+08:00"
status: active
catalog_scope: project-architecture
repo_paths:
  - README.md
  - AGENTS.md
  - .agents/ARCHITECTURE.md
  - Tactics.Godot.slnx
  - src/Tactics.Core
  - src/Tactics.Application
  - godot/src/Tactics.Godot.Adapter
  - godot/project.godot
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:90df4c0407e8f9867c6ed0a94b12f5f4791288b24c04c82afaf797df63639e23
---

# Summary

Tactics 是 Agent 优先维护并准备公开发布的 Godot 4.7 C# 战棋项目。根 `README.md` 面向访问仓库的玩家与开发者介绍 Pure Run、环境、运行、验证、许可和两档 Windows 构建入口；本页继续负责架构综合。远程 `main` 是产品与治理权威，运行时由纯 .NET Core/Application、Godot Adapter 和唯一 `godot/project.godot` 组成。公开根采用 Apache-2.0 代码许可、逐文件登记的 CC BY 4.0 项目资产与独立商标边界；完整 Unity 历史仅保存在私有归档，Frozen Oracle、Golden、迁移 receipt 和 OKF 作为公开历史/测试证据。当前设计保存在 `.agents/docs/`，仍需执行的活跃计划保存在 `.agents/plans/`，当前行为由代码、Resource 和测试证明。

# Runtime Foundation

- `Tactics.Core` 与 `Tactics.Application` 是纯 .NET 9；Godot Node、Resource、Scene、文件系统和 UI 只进入 Adapter/Editor 层。
- 最终内容由 Godot Resource/PackedScene 与轻量 Catalog 驱动；迁移 DTO、Unity GUID 和历史 receipt 不进入运行时。
- 三职业 Pure Run 使用 Save V6、确定性 Battle/Run 状态、Catalog 142 与单一 `Main.tscn`。
- `Tools/godot/Verify-GodotProject.ps1` 是本地主线统一门禁；Windows RC 使用只读 staging、包审计和双 renderer EXE smoke。
- `Tools/public-release` 固定公开文件策略、资产来源哈希、依赖清单与单 root 候选重建；运行时不依赖这些审计工具。
- Agent 默认在用户指定的 worktree 中完成审计和修复；新建、删除或切换 worktree 必须有活跃计划或用户明确授权。
- Godot 只使用 `godot/project.godot`；未知 Godot 行为先研究与本地复现，详细路由见 [Godot Agent Workflow](../operations/godot-agent-workflow.md)。
- Agent 默认禁止 Computer Use、窗口激活和真实鼠标键盘等前台交互；实现、截图、视觉 QA、测试或连接恢复不构成例外授权。后台验证不足时停止为人工验证待办，完整规则由 [Godot Agent Workflow](../operations/godot-agent-workflow.md) 导航。

# System Map

- [SkillGraph](../systems/skill-graph.md)负责技能资产和解释执行。
- [Monster AI](../systems/monster-ai.md)生成、过滤、评分并执行战斗意图。
- [Battle System](../systems/battle.md)承接棋盘、回合、单位、结算和战斗反馈。
- [Roguelike Run](../systems/roguelike-run.md)组织地图、节点、冒险状态和 run 内成长。
- [Godot Agent Workflow](../operations/godot-agent-workflow.md)定义 Agent 修改和验证项目的安全路径；[Archived Unity Agent Workflow](../operations/unity-agent-workflow.md)仅保留历史来源。
- [Project Documentation](../operations/project-documentation.md)定义设计、活跃计划、统一缺口和历史清理的生命周期。
- [OKF Maintenance](../operations/okf-maintenance.md)将实现和文档变更反向映射到需要更新的知识 scope。

# Citations

[1] [Tactics AGENTS.md](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/AGENTS.md)
[2] [Tactics architecture overview](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/ARCHITECTURE.md)
