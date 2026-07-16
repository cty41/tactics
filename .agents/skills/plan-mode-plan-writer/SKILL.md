---
name: plan-mode-plan-writer
description: "Use when producing a formal plan in Plan Mode — receives decision-complete plans from make-dev-plan, saves active plans to `.agents/plans/`, and adds execution context for handoff"
---

# Plan Mode 计划落地规范

将已经收束的方案保存为可直接执行、可交接且有明确结束条件的活跃计划。

## Quick Reference

| 阶段 | 要求 |
|---|---|
| 收束 | 目标、边界、关键决策和成功标准完整 |
| 补上下文 | 当前状态、入口文件、约束、风险和验证方式可复核 |
| 落地 | 保存到 `.agents/plans/` 并告知用户路径 |
| 收尾 | 实施验证后迁移长期知识并删除 completed plan |

## When to use

- Plan Mode 中需要交付正式开发、修复或迁移计划。
- `make-dev-plan` 已提供 decision-complete 的计划骨架。
- 计划需要交给新 session 或其他 Agent 直接执行。

## Workflow

### 1. 判断是否可落地

只有同时满足以下条件才创建文件：

- 目标与成功标准明确；
- 范围和明确不做的内容已写清；
- 高影响选择已经决定；
- 任务能按验收标准逐项完成。

澄清问题、候选方案和中间草案不写入 `.agents/plans/`。

### 2. 补齐执行上下文

正式计划至少包含：

- Summary：目标、成功标准、当前结论；
- Current State：已实现/未实现边界和证据；
- Relevant Context：关键目录、类型、数据流和项目约束；
- Implementation：按依赖顺序拆分的任务与验收；
- Test Plan：自动验证、必要的人工验证和回归范围；
- Risks / Assumptions：风险、外部依赖和默认选择；
- Handoff Notes：先读什么、先验证什么、明确不要做什么。

### 3. 保存与回复

- 文件名使用清晰主题名，必要时带日期。
- 保存到 `.agents/plans/`；如果存在仍活跃的上级计划，用相对链接关联。
- 回复中给出计划路径和关键范围。

### 4. 定义收尾动作

每份计划必须在 Handoff 或验收部分写明：完成实现与验证后，按 `project-doc-organization` 执行以下动作：

1. 将长期设计结论并入 `.agents/docs/` 权威文档；
2. 将真正未完成项写入统一缺口，或经用户批准建立新计划；
3. 更新受影响 OKF scope；
4. 删除已完成计划，由 Git 保存历史。

## 协作链路

- 需求不清晰：`brainstorming` → `make-dev-plan` → 本 skill。
- 需求已清晰：`make-dev-plan` → 本 skill。
- 文档位置与完成后清理：`project-doc-organization`。

## Anti-patterns

| 错误 | 正确 |
|---|---|
| 计划只存在聊天中 | 将正式计划保存到 plans |
| 只有任务标题 | 补当前状态、入口、验证与交接上下文 |
| 草案也写入 plans | 只保存 decision-complete 内容 |
| 实施完成仍长期保留计划 | 迁移知识、更新 OKF 后删除 |
| 将所有想法塞入当前计划 | 非当前范围进入统一缺口 |

## Checklist

- [ ] 内容已 decision-complete。
- [ ] 当前状态和关键路径来自仓库证据。
- [ ] 每个任务有明确验收标准。
- [ ] 自动/人工验证边界写清。
- [ ] 文件已保存到 `.agents/plans/`，路径已告知用户。
- [ ] 计划定义了完成后的知识迁移和删除动作。
