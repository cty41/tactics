---
name: knowledge-maintenance
description: "Use when querying, ingesting, superseding, or linting cross-system project knowledge in the Tactics OKF bundle"
---

# Tactics OKF Knowledge Maintenance

维护 `.agents/knowledge/` 中的独立 OKF v0.1 bundle，并保持它与设计、计划、代码、Unity 资产和测试一致。

## Quick Reference

| 操作 | 入口 |
|------|------|
| 查询 | `.agents/knowledge/index.md` |
| 写入/更新 | 查找并更新已有 `catalog_scope` |
| 替代旧概念 | `status: superseded` + `superseded_by` |
| 检测代码影响 | `python Tools/okf/catalog_impact.py report --worktree` |
| 同步受影响 scope | `python Tools/okf/catalog_impact.py sync --worktree --scope <scope> --write` |
| 校验 | `python Tools/okf/validate_bundle.py` |

## When to use

- 回答跨多个系统、文档或历史决策的问题
- 将外部资料或持久分析沉淀为项目知识
- 实现变化后更新系统综合页和验证 revision
- 标记旧知识被新结论替代
- 检查 OKF frontmatter、链接、index 或 log 健康度

## Workflow

### Step 1: 选择操作模式

- `query` 默认只读。
- `ingest`、`supersede` 和产生修复的 `lint` 会修改知识库，必须在任务范围内明确授权。

### Step 2: 渐进读取

先读取根 index，再读取相关子 index 和概念页：

```text
.agents/knowledge/index.md
  -> systems/index.md
    -> systems/skill-graph.md
```

不要先扫描并加载整个 bundle。

### Step 3: 回到真相源

概念页只是综合层：

- 设计问题核对 `.agents/docs/`
- 计划问题核对 `.agents/plans/`
- 当前实现核对 `repo_paths` 指向的代码、Unity 资产和测试
- Agent 约束核对 `AGENTS.md`、rules 和 skills

### Step 4: 更新概念

遵循 `.agents/rules/knowledge-maintenance.md`。新概念至少使用：

```yaml
---
type: Game System
title: Example System
description: 一句话摘要。
timestamp: 2026-07-14T00:00:00+08:00
status: active
catalog_scope: example-system
---
```

关系必须同时写在正文 Markdown 链接中，不能只存在于 frontmatter。

### Step 5: 更新导航与日志

- 将概念加入所在目录的 `index.md`。
- 新目录加入父级 index。
- 知识发生变化时，在根 `log.md` 的最新日期下记录一条 Creation、Update、Deprecation 或 Lint。

实现变化触发的维护先运行影响检测。读取报告列出的概念和真实 diff，更新正文后再对本任务实际影响的 scope 执行 `sync`。不要因为工作区已有其他未提交修改而同步无关 scope。

### Step 6: 校验

```powershell
python Tools/okf/validate_bundle.py
python -m unittest discover Tools/okf -p "test_*.py"
```

校验失败时先修复 frontmatter、断链、index 覆盖或重复 `catalog_scope`，再交付结果。

## Anti-patterns

| 错误 | 正确 | 原因 |
|------|------|------|
| 为每次聊天创建页面 | 更新已有 scope 或保持只读 | 防止知识碎片化 |
| 把 OKF 当实现真相源 | 回到代码、资产和测试 | 综合页可能过时 |
| 复制完整设计或代码 | 摘要并引用 `repo_paths` | 避免双重维护 |
| 删除过时概念 | 标记 superseded 并链接替代页 | 保留决策历史 |
| 只更新概念页 | 同步 index、log 并 lint | 保持可发现和可验证 |

## Checklist

- [ ] 从根 index 渐进读取
- [ ] 已核对对应真相源
- [ ] `catalog_scope` 没有重复 active 页面
- [ ] 内部关系已写为 Markdown 链接
- [ ] index 和必要的 log 已更新
- [ ] OKF 校验与单元测试通过
