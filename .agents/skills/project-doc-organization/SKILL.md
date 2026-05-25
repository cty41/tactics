---
name: project-doc-organization
description: "Use when creating, moving, or organizing project documentation files — design docs, plans, usage guides, or drafts"
---

# 项目文档组织规范

定义 Tactics Unity 项目中各类文档的标准存放位置。核心区分：**工作区（临时）**与**知识库（持久）**。

## Quick Reference

| 文档类型 | 存放位置 | 生命周期 |
|---------|---------|---------|
| 设计文档 | `.agents/docs/design/` | 持久保留，随设计迭代更新 |
| 开发计划 | `.agents/docs/plans/` | 持久保留，随进度更新 |
| 使用指南 | `.agents/docs/usage/` | 持久保留 |
| 头脑风暴草稿 | `.sisyphus/drafts/` | 临时，确认后迁移或丢弃 |
| 执行中计划 | `.sisyphus/plans/` | 临时，完成后归档 |

**决策原则**：设计真相源 → `.agents/docs/design/`，计划真相源 → `.agents/docs/plans/`，执行工作区 → `.sisyphus/`。

## When to use

- 创建或移动设计文档、开发计划、使用指南时
- 头脑风暴结束后，需要决定草稿何去何从
- 发现文档放错目录，需要纠正
- 不确定某份文档该放哪里

## 目录结构

```
.agents/
├── docs/
│   ├── design/          ← 设计文档（完整方案，回答"做什么"和"为什么"）
│   ├── plans/           ← 开发计划（可执行任务，回答"怎么做"和"何时做"）
│   ├── usage/           ← 使用指南（回答"怎么用"）
│   └── screenshots/     ← 截图 / 视觉参考
├── skills/              ← Agent 技能定义
├── rules/               ← 编码规范
└── ARCHITECTURE.md      ← 项目架构总览（可选）

.sisyphus/
├── drafts/              ← 头脑风暴临时草稿
├── plans/               ← 执行中的工作区计划
├── notepads/            ← 会话记录
└── run-continuation/    ← 运行延续数据
```

## 文档分类规则

### `.agents/docs/design/` — 设计文档

存放**完整设计方案**，回答"做什么"和"为什么"：

- 系统架构设计
- 核心机制设计（战斗、属性、技能等）
- 数据结构与数据流设计
- 技术方案选型

示例：`attribute-system-design.md`、`roguelike-map-design.md`

### `.agents/docs/plans/` — 开发计划

存放**可执行开发计划**，回答"怎么做"和"何时做"：

- 由 `make-dev-plan` 技能输出的结构化计划
- 包含 Background / Scope / Tasks 三大块
- 每个 Task 有验收标准

示例：`战斗系统演进计划.md`、`Buff与DoT效果落地计划.md`

### `.agents/docs/usage/` — 使用指南

存放**面向使用者的操作指南**，回答"怎么用"：

- 命令行工具使用说明
- 调试/测试指南
- 工作流指南

示例：`CheatCodeGuide.md`

### `.sisyphus/drafts/` — 草稿区

存放**头脑风暴临时产物**：

- 初始灵感记录
- 未成形的想法
- 参考素材列表

特性：**临时性**，确认后应迁移或丢弃。

### `.sisyphus/plans/` — 执行区

`make-dev-plan` 技能在执行 `writing-plans` 工作流时可能使用的活跃工作区。

## Workflow

```text
头脑风暴阶段 ──→ 输出到 .sisyphus/drafts/
     │
     ▼ (设计确认)
迁移到 .agents/docs/design/
     │
     ▼ (制订计划)
输出到 .agents/docs/plans/
     │
     ▼ (执行)
从 .agents/docs/plans/ 读取，在 .sisyphus/ 工作区执行
```

1. **头脑风暴**：使用 `.sisyphus/drafts/` 作为临时区，随意记录
2. **设计确认后**：将成熟内容迁移到 `.agents/docs/design/`，删除草稿
3. **制订计划**：基于设计文档，输出到 `.agents/docs/plans/`
4. **执行**：从 `.agents/docs/plans/` 读取，在 `.sisyphus/` 工作区执行

## 关键原则

### 1. 单一真相源

每个主题只有一个真相源：
- 设计真相源：`.agents/docs/design/`
- 计划真相源：`.agents/docs/plans/`
- 执行工作区：`.sisyphus/plans/`（执行完成后归档或清理）

### 2. 临时 vs 持久

| 目录 | 性质 | 生命周期 |
|------|------|----------|
| `.sisyphus/drafts/` | 临时 | 单次会话，设计确认后删除 |
| `.sisyphus/plans/` | 临时 | 执行期间，完成后归档 |
| `.agents/docs/design/` | 持久 | 长期保留，随设计迭代更新 |
| `.agents/docs/plans/` | 持久 | 长期保留，随进度更新状态 |

### 3. 引用规范

文档之间引用使用相对路径：

```markdown
## 关联文档

- 设计文档：[属性系统设计](../design/attribute-system-design.md)
- 前置计划：[战斗系统演进](../plans/战斗系统演进计划.md)
```

## Anti-patterns

### ❌ 错误 1：将设计文档放在 plans 目录

```
# 错误
.agents/docs/plans/roguelike-map-design.md  ← 这是设计文档！

# 正确
.agents/docs/design/roguelike-map-design.md
```

### ❌ 错误 2：将临时草稿长期保留

```
# 错误
.sisyphus/drafts/brainstorm.md  ← 设计确认后应删除

# 正确
设计确认后删除草稿，完整设计在 .agents/docs/design/
```

### ❌ 错误 3：将执行计划当作知识库

```
# 错误
直接在 .agents/docs/plans/ 中修改执行状态

# 正确
.agents/docs/plans/ 保持稳定的计划结构
执行状态在 .sisyphus/plans/ 或任务追踪工具中维护
```

## 快速参考

| 文档类型 | 存放位置 | 示例 |
|----------|----------|------|
| 完整设计方案 | `.agents/docs/design/` | `roguelike-map-design.md` |
| 开发计划 | `.agents/docs/plans/` | `战斗系统演进计划.md` |
| 使用指南 | `.agents/docs/usage/` | `CheatCodeGuide.md` |
| 头脑风暴草稿 | `.sisyphus/drafts/` | `brainstorm-draft.md` |
| 执行中计划 | `.sisyphus/plans/` | `active-plan.md` |
| 执行证据 | `.sisyphus/evidence/` | `screenshot.png` |

## Checklist

- [ ] 设计文档放在 `.agents/docs/design/`
- [ ] 开发计划放在 `.agents/docs/plans/`
- [ ] 使用指南放在 `.agents/docs/usage/`
- [ ] 临时草稿放在 `.sisyphus/drafts/`
- [ ] 执行状态不直接写回稳定计划文档
