# Godot Phase 8C–8D：第二批技能表现、状态可视化与战斗镜头

## Summary

以 `7575eaee` 与 canonical Catalog 119 为基线，增加 Ice Bolt、Lightning、Poison Spear、Amplify Damage 的程序化表现、通用状态覆盖层和确定性棋盘镜头；不复制 Piloto/第三方载荷，不改变 Core 玩法、Save V4、AI、奖励或技能数值。最终 Catalog 为 125，状态保持 `Generated/UnityOwned + manual_isometric_and_presentation_qa_pending`。

## Current State

- Phase 7E 已建立唯一等距投影和可玩棋盘。
- Phase 8A 已建立只读 `BattlePresentationFrame` 与通用 Tween 播放器。
- Phase 8B 已生成 Fireball、Bone Spear、Thrust 的 programmatic-only Resource；Catalog 为119。
- Godot Profile 为 `presentation`；三个已知行尾/默认配置假状态不得暂存。

## Implementation

1. 扩展表现帧，保存真实 Status、Spear 和 Damage event facts；实现四项技能 FX，生成四个 Resource，Catalog 123。
2. 为 Snapshot 增加结构化状态详情，为 Actor 增加独立状态覆盖层与生命周期反馈，生成状态 Resource，Catalog 124。
3. 增加只作用于棋盘根的有界确定性镜头控制、Motion toggle 与异常复位，生成镜头 Resource，Catalog 125。
4. 增加 Application/GdUnit/生成幂等、双渲染器、Reload、完整 Run 与截图结构证据。

## Validation and Checkpoints

- 每批先跑窄测试，再运行 `Tools/migration/Verify-GodotMigration.ps1`。
- ResourceSaver 连续两次生成 byte-identical；UID、ledger、receipt、Catalog 必须一致。
- scoped staging，检查 `.meta`、敏感信息与 whitespace；不 push、不建 PR。
- 提交依次为计划指定的三个 `feat`，仅在产生额外测试/稳定性变更时创建第四个 `test`。
- 最终只停在 Phase 7B–8D 合并人工验收，不晋升 ownership。

## Handoff and Closing

先检查本计划、`BattlePresentationFrame.cs`、`GodotBattlePresentationPlayer.cs` 与 `IsometricPresentationAssetFactory.cs`。禁止从视觉层重新计算命中、LOS、AOE、状态或掉矛位置。人工验收通过后，将长期规则合并到权威迁移设计和 OKF，删除本计划，由 Git 历史保留。
