# 项目级 Manual QA Handoff Skill

## Summary

新增 `manual-qa-handoff` 项目 Skill 与稳定人工验收账本。在修复通过 review 和自动门禁后，主动输出本轮重点、累计 pending、自动覆盖边界和最短人工旅程；只有用户明确反馈才能把项目晋升为 passed。

## Implementation

1. 先以 Agent policy 测试固定 Skill 结构、触发边界、稳定 ID、最近输出序号映射和账本状态集合。
2. 通过 Skill Creator 初始化 `.agents/skills/manual-qa-handoff`，保持简短 `SKILL.md`，把输出/账本细节放入单层 reference。
3. 创建 `.agents/docs/manual-acceptance.md`，从当前 Phase 7B–8E pending 人工闸门初始化稳定项目。
4. 运行 Skill、Agent policy、OKF 与 Godot migration 完整门禁；review 后 scoped commit，不 push。

## Constraints

- 不修改 Runtime、Catalog 或存档。
- 不把自动测试、截图或 Agent 判断当作人工通过。
- 自动门禁失败时只报告阻断，不发起正式人工验收。
- 完成后迁移长期规则、同步 OKF 并删除本计划，由 Git 保留历史。
