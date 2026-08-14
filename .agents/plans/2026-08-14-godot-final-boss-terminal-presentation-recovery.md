# Phase 7B–8E：最终 Boss 终局提交与表现队列恢复

## Summary

- 修复最终 Boss 被击败后仍停留在战斗页的问题。
- 现场主档 revision 148 停在 `PendingBattle / encounter.pure-run.special`，backup revision 147 停在 `ReadyForBoss`；没有提交 `BossVictory`。
- 修复范围限定为终局诊断、BattleResult 缓存、表现帧排空和 Boss Summary 路由；Save V5、Catalog 131、战斗数值和 AI 不变。

## Current State

- `PureRunFullRunService.CompleteBoss` 的纯逻辑测试已通过。
- 缺口位于最后击杀后的 `EvaluateTerminal → automatic frame drain → presentation completion → CompleteBattle` 生命周期。
- 现场游戏进程仍执行界面更新，但 runtime helper 已失去响应；真实主档和 backup 只读保护。

## Implementation

1. 增加只读 `BattleTerminalDiagnostics`，记录存活实体、控制权、BattleResult、自动帧队列和终局 marker，并通过 CheatConsole/Output 暴露。
2. 每次成功 Transition 和 AI frame 后统一检测终局；缓存 BattleResult，终局 pending 时拒绝新 gameplay intent。
3. 表现播放器为有 cue、空 cue 和异常 frame 都发出一次明确完成结果；异常时清理并 snap 到 committed After。
4. Main 使用单一 drain coordinator：frame 完成后继续 dequeue，队列为空时只提交一次缓存 BattleResult。
5. Boss BattleResult 继续调用 `PureRunFullRunService.CompleteBoss`；失败保留 PendingBattle/checkpoint 并显示结构化错误。

## Test Plan

- 覆盖玩家、AI 召唤物、状态 tick 和反击造成最终击杀。
- 覆盖零 cue、失效 Actor/Tween、Pause、Step、0.5×/1×/2×/4×、重复回调和 Reload。
- 覆盖不可见/无 controller 的存活实体 fault、BossVictory 单次提交、Return Home 摘要消费和 settlement 失败恢复。
- 使用 revision 148 存档副本验证；真实主档和 backup 的 hash/时间戳不得改变。
- 完整运行 `Tools/migration/Verify-GodotMigration.ps1`、双渲染器和 OKF/whitespace 门禁。

## Handoff Notes

- 先读取 `PlayableBattleSessionService`、`GodotBattlePresentationPlayer` 与 `GodotPlayableRunMain` 的终局链路。
- 正常表现不得由固定超时跳过；安全恢复只处理已提交 frame 的异常。
- 自动门禁和 review 通过后将 `MQA-GODOT-FULL-RUN` 从 `failed` 重开为 `pending`，人工通过前不得标记 `passed`。
- 完成后将长期结论并入权威设计与 OKF，删除本 active plan，由 Git 历史保留。
