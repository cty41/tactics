---
name: plan-mode-plan-writer
description: "Use when producing a formal plan in Plan Mode — receives decision-complete plans from make-dev-plan (directly or after brainstorming -> make-dev-plan), saves to `.agents/plans/`, returns the path, and adds execution context for weaker LLM handoff"
---

# Plan Mode 计划落地规范

统一约束 **Plan Mode** 下正式计划的成品形态。核心目标只有两个：

1. 形成正式计划后，必须落地保存到 `.agents/plans/`，并告知用户路径。
2. 计划正文必须补齐足够的执行上下文，让新 session 中能力较弱的 LLM 也能接手实施。

## Quick Reference

| 步骤 | 要求 | 输出 |
|------|------|------|
| 判定正式计划 | 仅保存 decision-complete 的最终计划 | `<proposed_plan>` |
| 补齐上下文 | 写明当前状态、关键文件、接口、验证方式、风险 | 可交接执行包 |
| 落地保存 | 保存到 `.agents/plans/` | 稳定计划文件 |
| 告知路径 | 回复中明确文件路径与关联文档 | 可直接打开 |

## When to use

- 任何 **Plan Mode** 下需要输出正式计划的场景
- 用户要求生成开发计划、阶段计划、修复计划、review 后行动计划
- 需要把计划交接给新 session 或能力较弱的 LLM 执行

## Workflow

### Step 1: 先判断是否已达到正式计划状态

只有满足下面条件，才算正式计划，允许落地保存：

- 目标、成功标准、边界已经明确
- 关键实现路径已经收束
- 不再依赖高影响的未决选择
- 可以直接交给另一位工程师或 agent 执行

以下内容 **不保存**：

- 澄清问题
- 中间草案
- 尚未 decision-complete 的候选方案

### Step 2: 计划必须补齐执行上下文

正式计划默认包含以下信息；不要只给抽象任务名：

- `Summary`
  - 目标
  - 成功标准
  - 当前结论
- `Current State`
  - 当前实现状态
  - 已知问题
  - 已完成 / 未完成边界
- `Relevant Context`
  - 关键目录、文件、入口点、核心类型或服务
  - 已有项目约束
- `Implementation Changes`
  - 按阶段或任务拆分
  - 每项有目标、输入、输出、验收标准
- `Interfaces / Data Flow`
  - 需要修改或依赖的接口、状态流、调用链
- `Test Plan`
  - 自动检查
  - 手工验证
  - 回归场景
- `Risks / Open Questions`
  - 风险点
  - 外部依赖
  - 未决项
- `Assumptions`
  - 代用户做的默认选择
- `Handoff Notes`
  - 新 session 先读哪些文件
  - 先验证什么
  - 明确不要做什么

### Step 3: 统一输出格式

正式计划使用单个 `<proposed_plan>`，内容应当简洁但 decision-complete。

推荐结构：

```markdown
<proposed_plan>
# 标题

## Summary
...

## Key Changes
...

## Test Plan
...

## Assumptions
...
</proposed_plan>
```

当任务复杂、需要更强交接上下文时，在 `Key Changes` 中补充：

- `Current State`
- `Relevant Context`
- `Interfaces / Data Flow`
- `Handoff Notes`

### Step 4: 正式计划必须落地保存

默认规则：

- 所有正式计划都保存到 `.agents/plans/`
- 文件名使用“主题可读名 + `计划.md`”风格
- 子阶段计划允许拆成独立文件
- 若存在主计划，子计划中补充相对链接

保存后在回复中必须说明：

- 计划文件路径
- 如有主从关系，说明关联文档

### Step 5: 与其他 planning skill 协同

- `brainstorming` 负责需求不清晰时的设计收束，输出设计文档到 `.agents/docs/`（按需触发）
- `make-dev-plan` 负责澄清、范围和任务拆分（可接收 `brainstorming` 的设计输出作为输入）
- `plan-mode-plan-writer` 负责把正式计划整理成稳定文档并补齐交接上下文
- `project-doc-organization` 负责目录与真相源约定

典型链路有两种：

1. **需求不清晰**：`brainstorming` -> 设计文档 -> `make-dev-plan` -> `plan-mode-plan-writer`
2. **需求已清晰**：`make-dev-plan` -> `plan-mode-plan-writer`

无论哪种链路，最终都经由 `make-dev-plan` 产出计划骨架，再交给 `plan-mode-plan-writer` 落地。

## Anti-patterns

| 错误 | 正确 | 原因 |
|------|------|------|
| 计划只存在于聊天回复 | 正式计划保存到 `.agents/plans/` | 否则无法稳定交接 |
| 只写 Task 标题，不写上下文 | 补齐当前状态、入口点、验证方式 | 弱模型无法直接执行 |
| 把草案也写成稳定计划文件 | 只有 decision-complete 的正式计划才保存 | 避免污染计划真相源 |
| 计划保存了，但不告诉用户路径 | 回复中明确给出路径 | 用户无法确认落地结果 |
| 让后续执行者自己猜入口文件 | 在 `Relevant Context` / `Handoff Notes` 中写明 | 降低新 session 推断成本 |

## Checklist

- [ ] 当前内容已达到正式计划标准
- [ ] 计划使用单个 `<proposed_plan>` 输出
- [ ] 已补齐当前状态、关键上下文、验证方式、handoff 信息
- [ ] 正式计划保存到 `.agents/plans/`
- [ ] 回复中已明确告知路径
- [ ] 如有主从关系，已补关联链接
