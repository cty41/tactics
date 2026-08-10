---
type: Operational Playbook
resource: https://github.com/cty41/tactics/tree/main/.agents/docs
title: Project Documentation
description: 当前设计、活跃计划、统一缺口与 OKF 综合层的文档生命周期。
tags: [operations, documentation, plans, knowledge]
timestamp: "2026-08-11T02:04:39+08:00"
status: active
catalog_scope: project-documentation
repo_paths:
  - .agents/docs
  - .agents/plans
  - .agents/skills/project-doc-organization/SKILL.md
  - .agents/skills/plan-mode-plan-writer/SKILL.md
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:98b5dc7eac78d458e2ed6f299655b00ec5ea35920e927df4a6da94fecd530ef3
---

# Current State

`.agents/docs/` 主要保存当前设计与使用指南；同一主题优先维护一份权威文档。`brainstorm.md` 是唯一的临时灵感收集箱，不属于设计真相源或实施承诺。想法成熟后迁入主题设计、[项目已知缺口](../plans/project-known-gaps.md)或正式计划。

`.agents/plans/` 只保存仍需执行且 decision-complete 的计划。实现完成并验证后，长期规则回写 docs，未实施项进入已知缺口或经批准的新计划，completed plan 随后删除并由 Git 保留历史。

`.agents/knowledge/` 负责跨系统摘要、关系和导航，不复制完整设计或已完成计划。代码、Unity 资产和测试仍是当前行为的最终事实源。

迁移阶段的验证边界以 `.agents/docs/2026-08-07-godot-tactics-migration-design.md` 为准；其中明确 Unity Windows Standalone 不属于迁移门禁，避免在 OKF 摘要中复制整份迁移设计。

当前 Godot 总迁移任务保存在 `.agents/plans/2026-08-09-godot-migration-parity-and-agent-enablement.md`；Phase 0–3 checkpoint 为 `2ef51954`，Phase 4 自动实施与 Editor lifecycle checkpoint 为 `2b341cb3`。当前同时保留两个互不替代的 active plan：`.agents/plans/2026-08-10-godot-phase4-unit-batch-migration.md` 等待 Unit Gallery/Spawn/Reload 人工视觉验收；`.agents/plans/2026-08-11-godot-phase5-buff-item-overnight.md` 执行无视觉载荷的 Buff/Item 源合同、运行时和 ResourceSaver 批次。Phase 5A 自动门禁不得代替 Phase 4 人工闸门；各计划完成后仍按计划生命周期迁移长期知识并删除 completed plan。

# Relationships

- [OKF Maintenance](okf-maintenance.md)负责从路径变更反向同步知识 scope。
- [Unity Agent Workflow](unity-agent-workflow.md)定义代码、资产和验证的安全边界。
- [Project Known Gaps](../plans/project-known-gaps.md)集中保存尚未激活的真实缺口。
- [Godot Agent Workflow](godot-agent-workflow.md)导航 Research Guide、Incidents 与 verified 结论。

# Verification Guidance

整理文档时检查重复主题、失效链接、旧架构术语和计划状态；不以截图证明功能。删除历史文件后运行 OKF 影响检测与 bundle 校验。
