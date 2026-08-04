---
type: Operational Playbook
resource: https://github.com/cty41/tactics/tree/main/.agents/docs
title: Project Documentation
description: 当前设计、活跃计划、统一缺口与 OKF 综合层的文档生命周期。
tags: [operations, documentation, plans, knowledge]
timestamp: "2026-08-04T17:09:18+08:00"
status: active
catalog_scope: project-documentation
repo_paths:
  - .agents/docs
  - .agents/plans
  - .agents/skills/project-doc-organization/SKILL.md
  - .agents/skills/plan-mode-plan-writer/SKILL.md
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:004065f1b6210b6dfce81ebe7062ca75ece3a4fa61b4250ba27f81bddc683b5a
---

# Current State

`.agents/docs/` 主要保存当前设计与使用指南；同一主题优先维护一份权威文档。`brainstorm.md` 是唯一的临时灵感收集箱，不属于设计真相源或实施承诺。想法成熟后迁入主题设计、[项目已知缺口](../plans/project-known-gaps.md)或正式计划。

`.agents/plans/` 只保存仍需执行且 decision-complete 的计划。实现完成并验证后，长期规则回写 docs，未实施项进入已知缺口或经批准的新计划，completed plan 随后删除并由 Git 保留历史。

`.agents/knowledge/` 负责跨系统摘要、关系和导航，不复制完整设计或已完成计划。代码、Unity 资产和测试仍是当前行为的最终事实源。

编辑器演示元数据必须与 Runtime/玩法真相源明确分离。例如 Battle Presentation Graph 的 Preview Scenario 只描述代表性完整演示，运行时仍以语义 Entry 驱动，SkillGraph 仍负责伤害、Buff、目标和资源消耗；文档不得把 Preview Phase 当成真实结算流程。

技能表现文档必须区分程序化时序骨架、项目侧第三方 Prefab FX 与玩法结算责任。火球、骨矛和突刺的 Piloto 混合增强仍由 Presentation Graph 编排，Recipe 只保留 Marker、路径/命中快照与安全回退；供应商原资产保持只读，Runtime/Preview 的方向变换以共享实现为准。

# Relationships

- [OKF Maintenance](okf-maintenance.md)负责从路径变更反向同步知识 scope。
- [Unity Agent Workflow](unity-agent-workflow.md)定义代码、资产和验证的安全边界。
- [Project Known Gaps](../plans/project-known-gaps.md)集中保存尚未激活的真实缺口。

# Verification Guidance

整理文档时检查重复主题、失效链接、旧架构术语和计划状态；不以截图证明功能。删除历史文件后运行 OKF 影响检测与 bundle 校验。
