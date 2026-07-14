---
name: project-doc-organization
description: "Use when creating, moving, or organizing project documentation files — design docs, plans, usage guides, or drafts"
---

# 项目文档组织规范

定义 Tactics Unity 项目中各类文档的标准存放位置。核心区分：**工作区（临时）**与**知识库（持久）**。

## Quick Reference

| 文档类型 | 存放位置 | 生命周期 |
|---------|---------|---------|
| 设计文档 | `.agents/docs/` | 持久保留，随设计迭代更新 |
| 开发计划 | `.agents/plans/` | 持久保留，随进度更新 |
| 使用指南 | `.agents/docs/` | 持久保留 |
| 跨系统知识目录 | `.agents/knowledge/` | OKF 综合层，持续维护 |

**决策原则**：设计真相源 → `.agents/docs/`，计划真相源 → `.agents/plans/`。

## When to use

- 创建或移动设计文档、开发计划、使用指南时
- 头脑风暴结束后，需要决定草稿何去何从
- 发现文档放错目录，需要纠正
- 不确定某份文档该放哪里

## 目录结构

```
.agents/
├── docs/                    ← 设计文档、使用指南（扁平结构）
│   ├── roguelike-map-gameplay-design.md
│   ├── attribute-system-design.md
│   ├── CheatCodeGuide.md
│   └── screenshots/         ← 截图 / 视觉参考
├── plans/                   ← 开发计划（可执行任务）
│   ├── 战斗系统演进计划.md
│   └── roguelike-map-gameplay-开发计划.md
├── knowledge/               ← 独立 OKF bundle（导航、综合、关系）
│   ├── index.md
│   ├── log.md
│   └── systems/
├── skills/                  ← Agent 技能定义
├── rules/                   ← 编码规范
├── shared-rules/            ← 共享规则
└── ARCHITECTURE.md          ← 项目架构总览
```

## 文档分类规则

### `.agents/docs/` — 设计文档与使用指南

存放**完整设计方案**和**使用指南**：

- 系统架构设计
- 核心机制设计（战斗、属性、技能等）
- 数据结构与数据流设计
- 技术方案选型
- 命令行工具使用说明
- 调试/测试指南

示例：`attribute-system-design.md`、`roguelike-map-gameplay-design.md`、`CheatCodeGuide.md`

### `.agents/plans/` — 开发计划

存放**可执行开发计划**，回答"怎么做"和"何时做"：

- 由 `make-dev-plan`、`plan-mode-plan-writer` 等 planning skill 输出的正式计划
- 包含 Background / Scope / Tasks 三大块
- 每个 Task 有验收标准
- 对于 Plan Mode 的正式计划，默认作为稳定真相源落地保存

示例：`战斗系统演进计划.md`、`Buff与DoT效果落地计划.md`

### `.agents/knowledge/` — OKF 综合知识层

存放符合 OKF v0.1 的跨系统概念页、渐进索引和更新日志：

- 不复制完整设计、计划或代码
- 通过 `repo_paths`、`resource` 和正文引用连接原始真相源
- 汇总当前状态、系统关系、替代关系和验证 revision
- 查询实现状态时仍必须回到代码、Unity 资产和测试复核

详细维护流程见 `../knowledge-maintenance/SKILL.md` 和 `../../rules/knowledge-maintenance.md`。

## Workflow

```text
设计阶段 ──→ 输出到 .agents/docs/
     │
     ▼ (制订计划)
输出到 .agents/plans/
     │
     ▼ (执行)
从 .agents/plans/ 读取并执行
```

1. **设计**：将设计文档保存到 `.agents/docs/`
2. **制订计划**：基于设计文档，输出到 `.agents/plans/`
3. **执行**：从 `.agents/plans/` 读取并执行
4. **综合**：将跨系统当前状态更新到 `.agents/knowledge/`，不改变原始真相源职责

## 关键原则

### 1. 单一真相源

每个主题只有一个真相源：
- 设计真相源：`.agents/docs/`
- 计划真相源：`.agents/plans/`（包含 Plan Mode 下正式落地的计划）
- 实现真相源：代码、Unity 资产和测试
- OKF 综合层：`.agents/knowledge/`（负责导航和综合，不替代上述真相源）

### 2. 引用规范

文档之间引用使用相对路径：

```markdown
## 关联文档

- 设计文档：[属性系统设计](../docs/attribute-system-design.md)
- 前置计划：[战斗系统演进](../plans/战斗系统演进计划.md)
```

### 3. Plan Mode 计划落地规则

对于 **Plan Mode** 产出的正式计划：

- 默认保存到 `.agents/plans/`
- 不应只停留在聊天上下文
- 回复中应告知用户文件路径
- 若是主计划的子阶段，应在文档中补充关联链接

## Anti-patterns

### ❌ 错误 1：将设计文档放在 plans 目录

```
# 错误
.agents/plans/roguelike-map-design.md  ← 这是设计文档！

# 正确
.agents/docs/roguelike-map-design.md
```

### ❌ 错误 2：将计划文档放在 docs 目录

```
# 错误
.agents/docs/战斗系统演进计划.md  ← 这是开发计划！

# 正确
.agents/plans/战斗系统演进计划.md
```

## 快速参考

| 文档类型 | 存放位置 | 示例 |
|----------|----------|------|
| 完整设计方案 | `.agents/docs/` | `roguelike-map-gameplay-design.md` |
| 使用指南 | `.agents/docs/` | `CheatCodeGuide.md` |
| 开发计划 | `.agents/plans/` | `战斗系统演进计划.md` |

## Checklist

- [ ] 设计文档放在 `.agents/docs/`
- [ ] 开发计划放在 `.agents/plans/`
- [ ] Plan Mode 的正式计划已落地到 `.agents/plans/`
- [ ] 使用指南放在 `.agents/docs/`
- [ ] 跨系统综合页放在 `.agents/knowledge/`，且引用原始真相源
