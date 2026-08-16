# Phase 7B–8E：Boss 结算事务诊断与 CheatConsole 只读复制

## Summary

- 现场已证明 Boss BattleResult、敌方全灭和表现队列排空均完成；修复范围收窄到 `CompleteBoss → Save V5 → BossVictory Summary`。
- 结算使用显式一次性状态和持久可见诊断，不再用静默布尔门禁隐藏失败。
- CheatConsole 支持鼠标选择、原生复制、Copy Visible 与 Copy All，不支持编辑或粘贴。
- Save V5、Catalog 131、玩法数值和用户正式存档保持不变。

## Implementation

1. 修复 terminal transition 丢失递增 revision 导致的 `save.non_increasing_revision`，并覆盖 BossVictory/Defeated 终局。
2. Adapter 记录 `Idle → Submitting → Rejected/Saved → NavigationCompleted`，立即刷新 CheatConsole 与 Output；重复 callback 幂等拒绝。
3. Save/业务失败保留 PendingBattle；Save 成功后即使导航异常，Reload 仍恢复 Summary。
4. CheatConsole 开启 RichTextLabel 选择与右键复制，增加当前筛选和全部日志复制按钮，并以可替换 clipboard port 做自动测试。

## Test Plan

- 固定 Boss terminal transition 的 revision 严格递增，并验证 Summary 只提交一次。
- 覆盖重复 callback、拒绝后恢复、成功后禁止重复、Copy Visible/All 与输入隔离。
- 运行 Core/Application、Godot/GdUnit、Compatibility/Forward+、统一 verifier、OKF 和 whitespace 门禁。
- 人工复验 BossVictory Summary、Return Home、鼠标/Ctrl+C/右键复制与 Assembly Reload。

## Handoff Notes

- 用户主档与 backup 仅作只读证据，自动测试使用隔离状态。
- 自动门禁与 review 通过后将 `MQA-GODOT-FULL-RUN` 重开为 pending；用户明确通过前不得晋升。
