---
type: Project Architecture
resource: https://github.com/cty41/tactics
title: Tactics Project Overview
description: Tactics 的项目真相源、Unity 运行时基础设施和主要游戏系统总入口。
tags: [architecture, unity, agent-first]
timestamp: "2026-07-23T17:12:49+08:00"
status: active
catalog_scope: project-architecture
repo_paths:
  - AGENTS.md
  - .agents/ARCHITECTURE.md
  - Assets/Tactics/Scripts/Common/UIManager.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:38f691018a7cb17afb64c79bfa5b59000283f3fb2dad67c45104a7e05daeb3e1
---

# Summary

Tactics 是 Agent 优先维护的 Unity 战棋项目。当前设计保存在 `.agents/docs/`，仍需执行的活跃计划保存在 `.agents/plans/`，当前行为由代码、Unity 资产和测试证明；本 OKF bundle 只提供跨系统综合和导航。

# Runtime Foundation

- 项目采用 ScriptableObject 驱动配置，通过 `GameAssetManager` 管理运行时资产生命周期。
- `UIManager` 统一加载和管理 UI，不允许直接使用 `Resources.Load`。
- UI Cancel 会区分键盘 Esc 与鼠标右键；战斗目标选择期间取消输入由 Battle UI 消费，不会同时打开 Pause。
- 通用日志使用 `TLog`，结构化战斗日志使用 `TBattleLog`。
- 修改 C# 后必须触发 Unity 编译并检查 Console 错误。

# System Map

- [SkillGraph](../systems/skill-graph.md)负责技能资产和解释执行。
- [Monster AI](../systems/monster-ai.md)生成、过滤、评分并执行战斗意图。
- [Battle System](../systems/battle.md)承接棋盘、回合、单位、结算和战斗反馈。
- [Roguelike Run](../systems/roguelike-run.md)组织地图、节点、冒险状态和 run 内成长。
- [Unity Agent Workflow](../operations/unity-agent-workflow.md)定义 Agent 修改和验证项目的安全路径。
- [Project Documentation](../operations/project-documentation.md)定义设计、活跃计划、统一缺口和历史清理的生命周期。
- [OKF Maintenance](../operations/okf-maintenance.md)将实现和文档变更反向映射到需要更新的知识 scope。

# Citations

[1] [Tactics AGENTS.md](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/AGENTS.md)
[2] [Tactics architecture overview](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/ARCHITECTURE.md)
