# Godot Phase 7C：第 4 层路线、非战斗节点与存档 V3

## Summary

以 `migration/godot`、基线 `4099fa1d` 将三战切片扩展为 `N1 → N2 → N3 → 第4层四选一 → ReadyForLayer5`。第4层包含正式 N4、Rest、Store 和三个 Unity Mystery 事件；技能继续封顶 Lv2，第5–7层、Elite/Boss 与正式表现留给 Phase 7D/8。

## Current State

- Phase 7B 自动门禁已通过，canonical Catalog 为101项，人工 Inventory/成长验收仍 pending。
- Run V2 已保存角色、技能等级、Inventory、装备与 PendingProgression，但没有 Map/NodeTransaction。
- 当前 Godot Encounter 只有 N1–N3；Unity Pure Run layout v2 定义七层以及第4/6层竞争节点。

## Implementation

1. 两次 Unity AssetDatabase 导出冻结 Map layout v2、N4、Rest/Store、三个 Mystery 与节点事务源码/引用；建立 `pure-run-layer4-map-nodes-v1` batch、Golden、Oracle、converter 和 receipt。
2. 新增 engine-neutral Map/Node/Transaction、Rest/Store/Mystery 命令与确定性服务；N3 后进入第4层，选择一条路线即锁定其余节点，Resolved/Committed 幂等恢复，完成后进入 `ReadyForLayer5`。
3. N4 复用 Battle/AI/Skill；Store 确定性生成库存并使用既有 Inventory；Rest 与 Mystery 严格消费冻结规则，事件结果不重掷、不重复奖励，致死进入统一 Defeated。
4. 新增 RunSaveDocumentV3，V2 验证后迁移并保留 backup；保存 Map、节点事务、Store 库存、Mystery 分配/结果与 ReadyForLayer5。
5. ResourceSaver 生成7个新内容资源，canonical Catalog 达108项；Main 增加 Map/Rest/Store/Mystery 功能页和真实 headless UI-flow 回归。

## Validation and Checkpoints

1. `feat: freeze Pure Run layer four map contracts`
2. `feat: add deterministic Pure Run map and node transactions`
3. `feat: complete Pure Run layer four route semantics`
4. `feat: persist Pure Run map transactions in save v3`
5. `feat: generate Godot layer four map and node flow`
6. 人工验收后关闭 Phase 7B/7C。

每个自动 checkpoint 经完整统一门禁、review、scoped staging 后自动提交；不 push、不建 PR、不改写历史。最终人工仅检查 Phase 7B Inventory/成长与第4层地图/节点 UI 的可读性、操作手感、resize 和 Reload。

## Assumptions and Completion

- 允许兼容扩展 Core/Application、Godot Adapter、迁移工具与存档，保持程序集/目录边界。
- Treasure、Lv3、第5–7层、N5/N6、E1/E2/Special/Boss 和正式 UI/VFX/Audio 不在本批。
- 完成后把长期结论并入权威设计/OKF，未完成内容进入 Phase 7D，删除本 active plan，由 Git 保存历史。
