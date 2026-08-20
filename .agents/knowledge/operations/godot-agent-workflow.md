---
type: Game System
resource: https://github.com/cty41/tactics
title: Godot agent workflow
description: Godot 4.7 C# 主线的项目、分层、Editor 生命周期、验证和发布边界。
tags: [godot, agent, workflow, testing]
timestamp: "2026-08-20T21:53:50+08:00"
status: active
catalog_scope: godot-agent-workflow
repo_paths:
  - AGENTS.md
  - .agents/rules/godot-agent-workflow.md
  - .agents/skills/godot-workflow
  - .agents/skills/godot-editor-lifecycle
  - .agents/incidents/godot
  - Tactics.Godot.slnx
  - Tools/godot/Verify-GodotProject.ps1
  - Tools/godot/Build-GodotWindows.ps1
  - Tools/migration/manifest/godot-tooling.json
verified_revision: d092a955
source_fingerprint: sha256:5b2304686c2c152f3a136251d595e2a2b2e50eeb46b305301285f698a4251b22
---

# Current State

Godot 4.7 C# 与 `godot/project.godot` 是唯一产品和编辑权威。Core/Application 保持纯 .NET；Adapter 承载 Node、Resource、文件系统、UI、EditorPlugin 与运行时集成。项目不携带公开运行时所需的本地 AI 插件或 helper。

Godot 修改先由 `godot-workflow` 路由到最小 Specialist Skill。C#、ResourceSaver、生成器和 reload-sensitive 工作遵循 `godot-editor-lifecycle`；只正常关闭该流程确认的 canonical Editor，并只恢复由本流程关闭的会话。

统一入口 `Tools/godot/Verify-GodotProject.ps1` 串行执行 restore/build、Core/Application/FrozenOracle、Gameplay Spec、Python、Skill/Incident、GdUnit、Release/Runtime/Editor headless、renderer、receipt 与 OKF。FrozenOracle、Golden 和 receipt 只是历史/确定性证据，不能替代视觉、手感、真实 Editor Reload 或干净 Windows 启动。

Windows 构建使用单一 `Windows Desktop` preset、锁定工具链和受审计 staging。Debug/Release 包必须通过架构、PCK/managed runtime、顶层 allowlist、测试/缓存/本地配置排除、manifest/hash 与隔离用户目录启动验证。

# Relationships

- 当前规则：`.agents/rules/godot-agent-workflow.md`
- 迁移 provenance：[Godot migration provenance](../plans/godot-migration.md)
- 文档生命周期：[Project Documentation](project-documentation.md)
- 历史工具索引：[Archived Unity Agent Workflow](unity-agent-workflow.md)

# Verification Guidance

未知 API、生命周期、插件或引擎错误必须按 Research Guide 和本地复现取证。自动门禁只报告覆盖到的层级，人工和发布边界必须单独记录。
