---
name: gameplay-design-constraints
description: "Use when designing or changing a character, combat skill, status, grid interaction, facing rule, or gameplay mechanic — requires explicit project contracts and drift checks before implementation"
---

# Gameplay Design Constraints

新角色、技能、状态或战斗机制必须先对照当前合同，避免实现、数值、测试和文档各自漂移。

## Quick Reference

| 任务 | 要求 |
|---|---|
| 查规则 | 读取相关设计合同并列出 Contract ID |
| 改规则 | 先新增或替代合同，再改实现与验证 |
| 写回归 | 在 ScenarioSpec 的 `contractIds` 引用合同 |
| 查覆盖 | 运行 `contract-coverage` |

## When to use

- 设计或修改角色、技能、状态、战斗数值、朝向、棋盘交互或玩法机制。
- 审查实现是否偏离既有游戏设计。
- 把设计规则连接到自动回归测试。

## Workflow

1. 读取与需求直接相关的合同文档：
   - `../../docs/attribute-system-design.md`
   - `../../docs/buff-system-rules.md`
   - `../../docs/battle-facing-rules.md`
   - `../../docs/isometric-grid-anchor-contract.md`
   - `../../docs/skill-graph-system.md`
   - 视线另读 `../../docs/battle-line-of-sight-rules.md`
   - 职业数值另读 `../../docs/three-class-skill-design.md` 与 `../../docs/pure-run-current-combat-values.md`
2. 列出本次设计依赖的 Contract ID、保持不变的规则和确需修改的规则。
3. 若现有合同不足，先新增 `approved_target` 合同和验证路径；不得用实现细节暗中改变 `verified_current` 合同。
4. gameplay spec 使用 `contractIds` 追踪覆盖。先运行合同校验，再走 `gameplay-test-framework` 编译和执行。
5. 实现后把合同状态、测试、Resource/Catalog 和 OKF 同步到同一事实；视觉与手感仍交人工验收。

## Contract block

只在明确 fenced block 中声明机器可读合同：

```yaml
id: DOMAIN-SUBJECT-001
status: verified_current
statement: 一条可独立验证的规则。
verification:
  - layer: core_test
    path: path/to/test.cs
dsl_support: partial
```

实际文档使用 ``gameplay-contract`` fence。ID 发布后不得复用；规则替代使用 `supersedes` / `superseded_by`，并保持引用可解析。

## Output

设计或变更说明至少包含：Contract IDs、玩法语义、数值依据、失败/边界语义、自动验证、人工验收边界，以及任何尚未支持的 DSL 覆盖。

## Anti-patterns

- 从旧 Unity 类结构反推当前规则。
- 未声明合同就改变数值或边界语义。
- 把模型候选、自动测试或表现截图当成人工体验通过。

## Checklist

- [ ] 已读取相关合同并列出 Contract ID。
- [ ] 规则变化具有显式替代关系与验证路径。
- [ ] Spec、Core/Application、Godot Resource 与文档没有互相漂移。
- [ ] 自动证据与人工验收边界已分开。
