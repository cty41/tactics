# Phase 7B–8E 定向修复：职业属性、毒伤反馈与完整 Action Pose

## Summary

- Mage 技能统一使用 Intelligence，Necromancer 技能统一使用 Charisma；Bone Spear 的 Intelligence 例外按源合同错误修正。
- 成长保证使用玩家实际选择的起始技能，而不是角色模板默认技能。
- Poison tick 在明确的表现 marker 刷新 HP 与动态数字，并补齐持久落地长矛标记。
- 迁移 Mage、Necromancer、Amazon 已批准的 Cast/Melee/Thrown/Hit Pose；死亡单位不再显示状态图标。
- Catalog 保持 124，Core 战斗数值、AI、RNG 和 ContentId 不变。

## Checkpoints

1. `fix: restore selected growth branches and class attributes`
2. `fix: expose poison ticks and persistent dropped spears`
3. `feat: migrate Pure Run player action poses to Godot`
4. 必要时追加测试稳定性 checkpoint；人工验收通过前保持 `Generated/UnityOwned`。

## Verification

- 测试先行覆盖实际起始分支、职业属性、旧 V5 修复、Poison tick/number、掉矛恢复、Pose marker/方向/fallback、死亡清理和 Reload。
- Unity AssetDatabase 导出与 Godot ResourceSaver 均连续运行两次并验证 UID、ledger、receipt 和 semantic hash。
- 完整运行 `Tools/migration/Verify-GodotMigration.ps1`、双渲染器、OKF、敏感信息与 whitespace 门禁。
- 自动 review 和 scoped commit 后，使用 `manual-qa-handoff` 更新并输出人工复验清单。
