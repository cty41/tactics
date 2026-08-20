---
type: Game System
resource: https://github.com/cty41/tactics
title: Godot migration provenance
description: 已完成迁移的冻结来源、Godot ownership、生成批次与验证边界。
tags: [migration, godot, provenance, testing]
timestamp: "2026-08-20T21:53:51+08:00"
status: active
catalog_scope: godot-migration
repo_paths:
  - src/Tactics.Core
  - src/Tactics.Application
  - src/Tactics.FrozenOracle.Tests
  - godot
  - Tests/golden
  - Tools/migration
  - Tools/migration/manifest/retirement
verified_revision: 2b341cb3
source_fingerprint: sha256:25f6f331892c89d8e4d8aa649445c30347318d293a5c178d8bf5dc78bf7cbbef
---

# Current State

Godot 4.7 C# 是唯一产品主线，`godot/project.godot` 是唯一项目。Core/Application 保持引擎无关，Godot Adapter 承担 Node、Resource、文件系统、UI、作者工具和运行时集成。

迁移来源已冻结在最终 Tag、FrozenOracle、Golden、DTO、receipt、ownership ledger 与 retirement manifest 中。它们只用于来源审计、确定性回归和许可证证明，不提供 live 旧编辑器、旧 MCP 或旧资产写入路径。

内容生成只消费已绑定的冻结输入，经 Application typed draft、ResourceSaver/PackedScene、Catalog/UID、target hash、幂等与 rollback 门禁进入 Godot。自动验证不能替代视觉、操作、真实 Editor Reload 或干净 Windows 启动验收。

# Relationships

- 当前开发与验证入口见 [Godot Agent Workflow](../operations/godot-agent-workflow.md)。
- 历史 Agent 工具退役索引见 [Archived Unity Agent Workflow](../operations/unity-agent-workflow.md)。
- 当前人工状态见 `.agents/docs/manual-acceptance.md`。

# Verification Guidance

迁移事实必须同时核对冻结来源、receipt/manifest、生成目标、Catalog/UID 与测试。禁止恢复已退役目录作为当前实现旁路，也禁止改写 retirement evidence 来迁就当前工作树。
