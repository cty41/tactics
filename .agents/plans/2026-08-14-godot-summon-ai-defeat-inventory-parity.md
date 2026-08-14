# Phase 7B–8E 定向收口：召唤物 AI、失败终局与 Inventory parity

## Summary

以 Catalog 125、Save V5 和第一场 Elite 现场为基线，恢复 Unity 的友方召唤物 AI、打通全灭后的 Defeated Summary → Return Home，并将 Inventory 收敛为 Rogue Map 单入口及可审计装备属性界面。

## Checkpoints

1. 冻结 BasicMeleeBrain、FireDemonBrain、Skeleton Warrior Lv1/Lv2 与 Skeleton Mage Lv1/Lv2 合同，通过 ResourceSaver 生成 2 个 AI 和 4 个内部 Skill Resource；Catalog 125→131。
2. 依据 Unit ContentId 解析友方召唤物控制权；Skeleton Warrior、Skeleton Mage、Fire Demon 自动行动，Decoy 不进入普通行动；玩家阵营全灭统一进入 Defeated Summary。
3. 由 Application 提供 Inventory Snapshot，显示基础/bonus/总属性、装备槽和物品详情；Inventory 仅从 Rogue Map 进入。

## Gates

- 每个 checkpoint 先写失败回归测试，再实现并运行窄门禁、code review、完整统一门禁和 scoped commit。
- ResourceSaver 连续两次一致，canonical Catalog 精确 131。
- 不修改 Save V5 schema，不覆盖用户主档/backup，不 push、不建 PR。
- 自动门禁通过后更新权威迁移设计、OKF 与人工验收账本；Phase 7B–8E 仍等待人工验收。

## Manual Acceptance

- 友方召唤物在自身回合自动行动；仅召唤物存活时战斗继续。
- 玩家阵营全部死亡后显示 Defeated Summary，Return Home 清除 Run。
- Inventory 仅从 Rogue Map 进入，装备属性/总值/槽位和 Reload 状态可观察。
- 补测存活单位 LOS；Output 无资源、Tween、存档或流程错误。
