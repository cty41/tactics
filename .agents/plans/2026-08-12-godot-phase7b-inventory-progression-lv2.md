# Godot Phase 7B：Inventory、成长消费与全 Lv1/Lv2 技能闭环

## Summary

以 `migration/godot`、基线 `8dccc6f5` 完成三战切片的 Inventory、Unity parity 成长消费、18 条职业技能分支 Lv1/Lv2 完整玩法，以及 V1→V2 确定性存档迁移。功能 UI 使用 Godot Control 占位表现；正式图标、VFX、Audio 和三战之外的 Run 节点不在本批。

## Current State

- Phase 7A 已关闭，canonical Catalog 为 74 项。
- Run 已保存背包、装备、携带消耗品和待成长身份，但技能仍是无等级 ContentId 列表。
- Starting Skill batch 只有九项玩家起始 Lv1、两个基础攻击和隐藏 Pickup；Poison Spear 由既有 batch 所有。
- `RunSaveDocumentV1` 与 Godot 单槽存储已经具备 canonical JSON、hash、revision、temp/backup/quarantine。

## Implementation

1. 两次 Unity AssetDatabase 导出冻结 18 分支 Lv1/Lv2、Inventory 和成长合同，生成 `pure-run-inventory-progression-v1` typed draft、Oracle、Golden 和 receipt；拒绝 Lv3、未知节点、非法引用与视觉 payload。
2. 扩展通用 Skill Runtime，完整实现新增召唤、防御、位移、恐惧、多击、长矛召回、分身和 Lv2 语义；预览与结算复用同一合法性路径。
3. 新增显式技能等级、原子成长与 Loadout 命令；实现严格 Unity 成长顺序。新增 RunSaveDocumentV2，验证后自动迁移 V1，保留原有恢复证据。
4. ResourceSaver 生成缺失技能与 101 项 canonical Catalog；Main 增加 Inventory/Progression 页面，完成 Reload 和三战导航。

## Validation

- Unity DTO 两次 byte-identical；ResourceSaver 两次 byte-identical。
- Core/Application/Oracle/Python/GdUnit、Compatibility/Forward+、Debug/Release、UID、receipt、OKF 和 whitespace 全绿。
- 人工验证 Inventory、成长选择、三职业新增技能、Lv2、V1 迁移、N1→N3、resize 和 Assembly Reload。

## Checkpoints

1. `feat: freeze Pure Run inventory and progression contracts`
2. `feat: complete playable Pure Run level two skills`
3. `feat: add deterministic loadout progression and save v2`
4. `feat: generate Godot inventory and progression flow`
5. 人工验收后 `feat: close Godot inventory and progression vertical slice`

每次提交仅 scoped staging，不 push、不建 PR、不改写历史；提交前按项目规则展示精确文件数并请求确认。

## Handoff and Completion

实现完成后将长期设计并入 `.agents/docs/`，同步受影响 OKF scope，把未完成项写入统一缺口或经批准的新计划，并删除本计划，由 Git 保存历史。
