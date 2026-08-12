# Godot Phase 7D：完整七层 Pure Run、Elite 与 Boss 终局

## Summary

以 `migration/godot`、基线 `ef39ccad` 将 `ReadyForLayerFive` 扩展为 Layer 5 Elite、Layer 6 四选一、Layer 7 Special Boss 与 BossVictory/Defeated 终局。技能继续封顶 Lv2；N5/N6 只进入冻结合同、Catalog 和诊断，不进入正式地图。自动 checkpoint 连续执行，最终与 Phase 7B/7C 合并人工验收。

## Current State

- Phase 7B 已生成 Inventory、成长和36个 Lv1/Lv2 条目，人工闸门 pending。
- Phase 7C 已完成 Layer 4 四路线、Save V3 和108项 Catalog，人工闸门 pending。
- Unity layout v2 已确认 Layer 5 为固定 Elite、Layer 6 为 Elite/Rest/Store/Mystery、Layer 7 为 Special。

## Implementation

1. 两次 Unity AssetDatabase 导出冻结 N5/N6/E1/E2/Special、三种布局、倍率、七层图、奖励和 BossVictory；建立 `pure-run-full-seven-layer-v1` batch、Golden、Oracle、converter、receipt。
2. 泛化 Map/Node 事务支持 Layer 5–7；E1/E2 和 Special 变体按 seed/node 稳定选择；Elite/Boss 倍率通过通用 Encounter/Battle/Damage；Layer 5/6 结算、成长、失败与 BossVictory 均幂等。
3. 新增 Save V4，兼容 V1–V3，保存 Layer 5–7 assignment、Layer 6 节点事务、Elite/Boss checkpoint 与终局摘要；V3 `ReadyForLayerFive` 升级后继续 Layer 5。
4. ResourceSaver 生成5个 Encounter、`special_open` Layout 和完整 Map，canonical Catalog 为114；Main 接入 Layer 5、Layer 6 和 Boss 页面及完整自动 Fixture。

## Validation and Checkpoints

1. `feat: freeze Pure Run elite and boss contracts`
2. `feat: complete deterministic seven-layer run runtime`
3. `feat: persist complete Pure Run progression in save v4`
4. `feat: generate playable Godot elite and boss run flow`

每个 checkpoint 通过 scoped staging、review 和完整统一门禁后自动本地提交；不 push、不建 PR、不改写历史。第四个 checkpoint 后保持 `Generated/UnityOwned + manual_inventory_progression_full_run_qa_pending`。

## Boundaries and Handoff

- 不迁移 Lv3、Treasure、正式 VFX/Audio 或 Windows Release/PCK。
- N5/N6 不进入正式地图；Layer 5/6 使用 E1/E2，Layer 7 使用 Special。
- 完成后把长期结论并入权威迁移设计和 OKF；最终人工验收通过后删除 Phase 7B/7C/7D active plans，由 Git 保存历史。
