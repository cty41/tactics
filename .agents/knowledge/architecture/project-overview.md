---
type: Project Architecture
resource: https://github.com/cty41/tactics
title: Tactics Project Overview
description: Tactics 的项目真相源、Unity 运行时基础设施和主要游戏系统总入口。
tags: [architecture, unity, agent-first]
timestamp: "2026-08-10T21:52:04+08:00"
status: active
catalog_scope: project-architecture
repo_paths:
  - AGENTS.md
  - .agents/ARCHITECTURE.md
  - Assets/Tactics/Scripts/Common/UIManager.cs
  - Assets/Tactics/Arts/Fonts/NotoSansSC.ttf
  - Assets/Tactics/Arts/UI
  - Assets/Tactics/UIToolkit/TextSettings.asset
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:bc22d2ca557cdf6f0cf872de8e013afa62348c6a16afe19eb0a6dc72499a7fb0
---

# Summary

Tactics 是 Agent 优先维护、正在从冻结 Unity 终版迁移到 Godot 的战棋项目。Unity `w1`/最终 Tag 是只读 Oracle；新运行时架构以纯 .NET Core/Application 和唯一 `godot/project.godot` 为目标。当前设计保存在 `.agents/docs/`，仍需执行的活跃计划保存在 `.agents/plans/`，当前行为由代码、资产和测试证明；本 OKF bundle 只提供跨系统综合和导航。

# Runtime Foundation

- 项目采用 ScriptableObject 驱动配置，通过 `GameAssetManager` 管理运行时资产生命周期。
- `UIManager` 统一加载和管理 UI，不允许直接使用 `Resources.Load`；运行时从只读的 `NotoSansSC.ttf` 创建共享的 Dynamic TextCore FontAsset（SDFAA、1024×1024、Multi Atlas、`DontSave`），并在每次 UIDocument 激活后从根节点统一继承。内存 `RuntimeDefaultFontOwner` 以确定性 marker 持有 source、FontAsset、Material 与已使用 atlas；owner 自身必须在修复前已带运行时 `DontSave` provenance，恢复时再匹配目标 source、Dynamic/Multi Atlas、Material→首 atlas 绑定和完整资源图，然后同步可修复的资源 `DontSave` 标志并执行严格校验，同名或无 provenance owner 对象不会被采用或修改。恢复和生命周期同步采用两阶段处理：先从全部可信 owner 中选定唯一保留图并冻结保留图及无 provenance 外部图的资源身份，再引用感知地清理失效或重复 owner；共享保留 FontAsset、Material 或已使用 atlas 的资源不会被销毁，销毁独立 FontAsset 前会先断开其资源引用，且只清理索引小于 `atlasTextureCount` 的已使用 atlas，未使用容量尾槽不属于销毁范围。Hide、Destroy、Subsystem Registration 与 Application quit 边界都会同步动态新增 atlas 的生命周期；字形 atlas 只存在于内存，不生成或修改项目内 FontAsset。
- UI Cancel 会区分键盘 Esc 与鼠标右键；战斗目标选择期间取消输入由 Battle UI 消费，不会同时打开 Pause。
- 通用日志使用 `TLog`，结构化战斗日志使用 `TBattleLog`。
- 修改 C# 后必须触发 Unity 编译并检查 Console 错误。
- Agent 默认在用户指定的 worktree 中完成审计和修复；新建、删除或切换 worktree 必须有活跃计划或用户明确授权。Unity worktree 的高成本导入和启动是该约束的一部分，具体规则见 `.agents/rules/agent-worktree.md`。
- Unity → Godot 迁移的 Unity 源快照不执行 Windows Standalone Smoke；迁移门禁采用 Editor/PlayMode、OKF 和依赖审计，平台发布验收另行处理，具体规则见 `.agents/rules/godot-migration.md`。
- Godot 迁移只使用 `godot/project.godot`；Core/Application 不引用引擎，未知 Godot 行为先研究与本地复现，详细路由见 [Godot Agent Workflow](../operations/godot-agent-workflow.md)。
- Agent 默认禁止 Computer Use、窗口激活和真实鼠标键盘等前台交互；实现、截图、视觉 QA、测试或连接恢复不构成例外授权。后台验证不足时停止为人工验证待办，完整规则由 [Unity Agent Workflow](../operations/unity-agent-workflow.md) 导航。

# System Map

- [SkillGraph](../systems/skill-graph.md)负责技能资产和解释执行。
- [Monster AI](../systems/monster-ai.md)生成、过滤、评分并执行战斗意图。
- [Battle System](../systems/battle.md)承接棋盘、回合、单位、结算和战斗反馈。
- [Roguelike Run](../systems/roguelike-run.md)组织地图、节点、冒险状态和 run 内成长。
- [Unity Agent Workflow](../operations/unity-agent-workflow.md)定义 Agent 修改和验证项目的安全路径。
- [Godot Agent Workflow](../operations/godot-agent-workflow.md)定义 Godot 迁移、研究证据、测试与 Incident 晋升路径。
- [Project Documentation](../operations/project-documentation.md)定义设计、活跃计划、统一缺口和历史清理的生命周期。
- [OKF Maintenance](../operations/okf-maintenance.md)将实现和文档变更反向映射到需要更新的知识 scope。

# Citations

[1] [Tactics AGENTS.md](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/AGENTS.md)
[2] [Tactics architecture overview](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/ARCHITECTURE.md)
