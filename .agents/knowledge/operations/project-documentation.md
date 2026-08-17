---
type: Operational Playbook
resource: https://github.com/cty41/tactics/tree/main/.agents/docs
title: Project Documentation
description: 当前设计、活跃计划、统一缺口与 OKF 综合层的文档生命周期。
tags: [operations, documentation, plans, knowledge]
timestamp: "2026-08-17T16:37:20+08:00"
status: active
catalog_scope: project-documentation
repo_paths:
  - .agents/docs
  - .agents/plans
  - .agents/skills/project-doc-organization/SKILL.md
  - .agents/skills/plan-mode-plan-writer/SKILL.md
  - .agents/skills/manual-qa-handoff/SKILL.md
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:ff4e0b889a7ea7c9ef361243bb02c296315a585c05ae74cb01ba025c65db2c08
---

# Current State

`.agents/docs/` 主要保存当前设计与使用指南；同一主题优先维护一份权威文档。`brainstorm.md` 是唯一的临时灵感收集箱，不属于设计真相源或实施承诺。想法成熟后迁入主题设计、[项目已知缺口](../plans/project-known-gaps.md)或正式计划。

`.agents/plans/` 只保存仍需执行且 decision-complete 的计划。实现完成并验证后，长期规则回写 docs，未实施项进入已知缺口或经批准的新计划，completed plan 随后删除并由 Git 保留历史。

`.agents/knowledge/` 负责跨系统摘要、关系和导航，不复制完整设计或已完成计划。代码、Unity 资产和测试仍是当前行为的最终事实源。

跨 Unity/Godot 的当前人工验收状态由 `.agents/docs/manual-acceptance.md` 以稳定 ID 维护。实现通过 code review 与自动门禁后，`manual-qa-handoff` 只重开受本轮行为、UI、表现、流程或 Editor 生命周期影响的项目，并输出本轮重点、累计待验收、自动覆盖边界和最短操作旅程；自动证据不能把人工项晋升为 passed，只有用户明确反馈可以更新人工结论。

Godot Content Workbench 的能力防回退基线保存在
`.agents/docs/godot-content-workbench-capability-matrix.md`。它将最终 Unity 标签中的有效自定义编辑器能力逐项映射为
`implemented`、`partial`、`replacement` 或 `excluded`，并绑定当前 Godot 页面、Application 作者合同、自动证据
和人工收口条件；不得用笼统的 `replaced_by_godot_design` 代替实际证据。`partial` 只有在代码、自动门禁和真实
Editor 人验均完成后才能晋升。

迁移阶段的验证边界以 `.agents/docs/2026-08-07-godot-tactics-migration-design.md` 为准；其中明确 Unity Windows Standalone 不属于迁移门禁，避免在 OKF 摘要中复制整份迁移设计。

当前 Godot 总迁移任务保存在 `.agents/plans/2026-08-09-godot-migration-parity-and-agent-enablement.md`；Phase 0–3 checkpoint 为 `2ef51954`，Phase 4 自动实施与 Editor lifecycle checkpoint 为 `2b341cb3`。当前只保留 `.agents/plans/2026-08-10-godot-phase4-unit-batch-migration.md` 作为 Godot active plan，等待 Unit Gallery/Spawn/Reload 人工视觉验收。Phase 5A Buff/Item 已完成源合同、运行时、ResourceSaver 与 canonical Catalog 自动门禁，其完成计划已删除并由 Git 历史保留；该自动门禁不替代 Phase 4 人工闸门。

最终 Boss 终局现场修复由 `.agents/plans/2026-08-14-godot-final-boss-terminal-presentation-recovery.md` 跟踪；实现、review、统一门禁和人工复验完成后，将长期结论保留在 Godot 迁移设计与人工验收账本，并删除该 active plan。

# Relationships

- [OKF Maintenance](okf-maintenance.md)负责从路径变更反向同步知识 scope。
- [Godot Agent Workflow](godot-agent-workflow.md)定义当前代码、Resource 和验证安全边界；[Archived Unity Agent Workflow](unity-agent-workflow.md)仅用于历史追溯。
- [Project Known Gaps](../plans/project-known-gaps.md)集中保存尚未激活的真实缺口。
- [Godot Agent Workflow](godot-agent-workflow.md)导航 Research Guide、Incidents 与 verified 结论。

# Verification Guidance

整理文档时检查重复主题、失效链接、旧架构术语和计划状态；不以截图证明功能。删除历史文件后运行 OKF 影响检测与 bundle 校验。
