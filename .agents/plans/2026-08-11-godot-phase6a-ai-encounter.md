# Godot Phase 6A：六类敌人 AI 与 N1–N3 Encounter 垂直切片

状态：active
基线：`migration/godot` / `44450116`

## 目标

冻结并迁移 Charger、Ranged、AOE、Support、EliteCharger、ElitePoisonCaster 六类 AI，补齐四项敌方技能，并生成 N1–N3 与两个 Elite 独立验证场景。所有 AI 行动复用通用技能和战斗结算。

## Checkpoints

1. 冻结 12 个 Brain/Profile、4 个 AbilityConfig、4 个 SkillGraph 与 EncounterConfig 源合同。
2. 增加确定性 AI/Encounter Core/Application runtime，升级 `battle-transition-v5`。
3. ResourceSaver 生成 4 Skill、6 AI、2 Layout、3 Encounter、73 项 canonical Catalog 与 1600×900 Fixture。
4. 人工 gameplay QA 后晋升 `Validated/UnityOwned` 并关闭本计划。

## 边界与闸门

- 正式 Encounter 仅 N1–N3；E1/E2/Special/N4–N6 不迁移。
- Elite 仅进入独立 Fixture；不接 Run/Persistence、UI/Input 或正式 VFX/Audio。
- checkpoint 3 后保持 `Generated/UnityOwned + manual_gameplay_qa_pending`。
- 每批完整统一验证；失败停在最后一个绿色 commit；不 push、不建 PR。

关联：[Godot 总迁移计划](2026-08-09-godot-migration-parity-and-agent-enablement.md)
