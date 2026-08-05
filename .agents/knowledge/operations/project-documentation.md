---
type: Operational Playbook
resource: https://github.com/cty41/tactics/tree/main/.agents/docs
title: Project Documentation
description: 当前设计、活跃计划、统一缺口与 OKF 综合层的文档生命周期。
tags: [operations, documentation, plans, knowledge]
timestamp: "2026-08-05T16:29:28+08:00"
status: active
catalog_scope: project-documentation
repo_paths:
  - .agents/docs
  - .agents/plans
  - .agents/skills/project-doc-organization/SKILL.md
  - .agents/skills/plan-mode-plan-writer/SKILL.md
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:b29ac70db0543418488a7d2af16e89be5cc3a81f471663a3f71ca26559c65243
---

# Current State

`.agents/docs/` 主要保存当前设计与使用指南；同一主题优先维护一份权威文档。`brainstorm.md` 是唯一的临时灵感收集箱，不属于设计真相源或实施承诺。想法成熟后迁入主题设计、[项目已知缺口](../plans/project-known-gaps.md)或正式计划。

`.agents/plans/` 只保存仍需执行且 decision-complete 的计划。实现完成并验证后，长期规则回写 docs，未实施项进入已知缺口或经批准的新计划，completed plan 随后删除并由 Git 保留历史。

`.agents/knowledge/` 负责跨系统摘要、关系和导航，不复制完整设计或已完成计划。代码、Unity 资产和测试仍是当前行为的最终事实源。

编辑器演示元数据必须与 Runtime/玩法真相源明确分离。例如 Battle Presentation Graph 的 Preview Scenario 只描述代表性完整演示，运行时仍以语义 Entry 驱动，SkillGraph 仍负责伤害、Buff、目标和资源消耗；文档不得把 Preview Phase 当成真实结算流程。

Tween Preview 的组合演示也必须标明复用边界：`Lethal Hit → Corpse` 复用 Runtime 的致死受击、Handoff 和落地 Sequence，但不执行伤害结算或死亡判定。时间轴用于验证表现生命周期，尸体当帧占格、活体移除等玩法事实仍以战斗系统实现为准。

表现状态调试只允许通过 Play Mode Inspector 的只读快照呈现，不得把 Lifecycle、活动 Tween、generation 或 Handoff 序列化为可编辑配置，也不得提供绕过运行时入口的强制状态按钮。

技能表现文档必须区分程序化时序骨架、项目侧第三方 Prefab FX 与玩法结算责任。火球、骨矛和突刺的 Piloto 混合增强仍由 Presentation Graph 编排，Recipe 只保留 Marker、路径/命中快照与安全回退；供应商原资产保持只读，Runtime/Preview 的方向变换以共享实现为准。

Editor 稳定性文档必须把自动验证能力与人工硬件门禁分开记录。Presentation Workbench 的 retained Image、固定 RenderTexture 和 resize 状态机属于当前实现事实；真实 D3D11 连续 resize 在目标 GPU 上通过之前，只能标记为 `manual_visual_qa_pending`，不能写成“自动测试已证明崩溃解决”。

等距战场坐标的权威术语、消费者职责与防漂移规则集中在 `.agents/docs/isometric-grid-anchor-contract.md`。实现或工具不得分别发明半格补偿；当前事实必须回到共享几何代码、Prefab 作者基线和对应测试核对。

该契约也区分锁定逻辑格的静态提示与连续跟随单位根的动态状态高亮；移动中的脚底状态不得重新通过 Cell 事件或世界点拾取换算位置，阵营可见性及 Targetable 例外同样以该文档为准。

# Relationships

- [OKF Maintenance](okf-maintenance.md)负责从路径变更反向同步知识 scope。
- [Unity Agent Workflow](unity-agent-workflow.md)定义代码、资产和验证的安全边界。
- [Project Known Gaps](../plans/project-known-gaps.md)集中保存尚未激活的真实缺口。

# Verification Guidance

整理文档时检查重复主题、失效链接、旧架构术语和计划状态；不以截图证明功能。删除历史文件后运行 OKF 影响检测与 bundle 校验。
