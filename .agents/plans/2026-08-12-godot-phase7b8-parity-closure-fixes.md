# Godot Phase 7B–8D 合并验收前 parity 收口修复

## Summary

以 `migration/godot`、HEAD `c2a54cff` 为基线，修复人工验收发现的战斗时间线、自动播放、尸体占格、目标预览朝向、棋盘外观、Progression 和调试 HUD 偏差。本轮不修改玩法数值、Encounter 出生格、Save V4 格式或正式第三方表现载荷。

实施顺序：

1. 修复玩家 Transition 的 `After` snapshot 被未来 AI 回合污染，以及表现完成回调早于 Tween 清理导致自动播放停顿。
2. 将尸体纳入 Core movement board，占格规则同时约束玩家和 AI；targeting hover 按 Unity FacingResolver 只读预览朝向。
3. 等价迁移项目自有 Pure Run tile 和 BattleBackdrop shader，移除 Unity 不存在的全棋盘 focus/shake。
4. 将 Progression 恢复为属性分配与技能选择两个可恢复步骤。
5. 缩小 HP/MP 调试条，并按 Unity 顺序支持 `1x → 2x → 4x → 0.5x → 1x`。

## Root Causes

- `PlayableBattleSessionService.ApplyCommand` 在捕获玩家 `After` snapshot 前同步计算完 AI 回合，导致玩家动作帧提前呈现未来伤害或死亡。
- `GodotBattlePresentationPlayer` 在从 active Tween 集合移除前触发 `FrameFinished`，自动 drain 将仍在播放的状态误判为忙，只能由 Timer/Step 偶然继续。
- movement board 与 destination validation 只考虑存活单位，遗漏 `BattleState.Corpses`。
- Godot targeting 只显示范围/合法性，没有复用 Unity 选择阶段的 Facing preview。
- 当前棋盘只画平色菱形，未使用项目自有 `pure_run_tile_warm_gray`；全棋盘镜头 focus/shake 是 Godot 额外行为，不是 Unity parity。
- Godot Progression UI 把属性与技能合并为单个候选按钮，并以技能门槛反推唯一属性，偏离 Unity `AttributeAllocation → SkillSelection`。

## Checkpoints

### 1. Chronological playback

- 在玩家 Transition 后、AI 自动推进前捕获玩家 `After` snapshot。
- AI frame 显式携带各自 Before/After，完成事件驱动下一帧，不依赖固定 Timer。
- Frame completion 只能在 Tween 清理后触发；Pause/Resume/Step 不改变状态或事件序列。

提交：

```text
fix: preserve chronological battle playback in Godot
```

### 2. Corpse occupancy and facing preview

- movement board、路径和目标格显式拒绝尸体格。
- Snapshot 增加只读 preview facing；Move 使用 canonical path 首步，技能使用目标方向，取消恢复权威朝向。

提交：

```text
fix: align corpse occupancy and targeting facing
```

### 3. Tile/backdrop parity and camera removal

- 通过迁移资产管线生成/复制项目自有 warm-gray tile 与 Godot CanvasItem backdrop shader。
- 棋盘使用 Unity 同源 tile 的明暗面和轮廓，不发明未冻结 checkerboard 规则。
- 删除全棋盘 focus/shake、Camera Motion 控件及对应 Catalog ownership；保留单位局部受击反馈。

提交：

```text
fix: restore Pure Run battle board visual parity
```

### 4. Progression phases

- Progression draft 持久化属性分配和技能选择；先选择任一合法属性，再重新计算技能候选。
- 存在候选时必须选择技能；无候选时允许确认属性。
- Reload 不重复等级、奖励或事务。

提交：

```text
fix: restore staged Pure Run progression flow
```

### 5. HUD and speed parity

- HP/MP 调试条收紧到约 `60×18`，单条约 `7px`，保持数值可读。
- 播放倍率固定支持 `0.5x/1x/2x/4x`，循环与 Unity 一致；修改当前和后续 Tween 速度。

提交：

```text
fix: align battle diagnostics and playback speeds
```

## Automated Gates

- 每项行为先增加失败测试并确认红灯，再最小实现至绿灯。
- Core/Application 覆盖 snapshot 时间边界、尸体占格、preview facing、staged progression 和速度循环。
- GdUnit/Adapter 覆盖 Tween completion、自动 drain、tile/backdrop、无全局镜头运动、HUD bounds 和 Reload cleanup。
- Compatibility 与 Forward+ 运行 Main smoke。
- ResourceSaver 连续生成两次一致；更新 UID、ledger、receipt 和准确 Catalog 数量。
- 执行 `Tools/migration/Verify-GodotMigration.ps1`、Debug/Release、Python、Oracle、OKF、敏感信息和 whitespace 门禁。

## Manual QA Gate

本轮自动 checkpoint 不晋升 ownership。完成后仍停在 Phase 7B–8D 合并人工验收：

- targeting 朝向、AI 自动 Move→Attack、尸体阻挡。
- Tile、Backdrop、无全棋盘晃动、紧凑 HP/MP。
- 属性选择后再选技能，Reload 保留。
- 0.5x/1x/2x/4x、Pause/Step、Assembly Reload。

## Boundaries

- 不修改 Core GridPoint、Encounter 出生格、技能/AI 数值、Save V4 schema。
- 不复制 Piloto 或第三方 Prefab、纹理、材质、Shader、Audio。
- 不 push、不建 PR、不改写历史、不切换 worktree。
- 继续排除既有无关状态：`OneLineSettings.asset.meta`、Exporter `.meta`、`godot/project.godot` 默认归一化。
