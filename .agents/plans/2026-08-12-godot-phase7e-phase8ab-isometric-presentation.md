# Godot Phase 7E–8B：三小时等距战场与首批表现层迁移

## 目标

以 `migration/godot`、`e05454ec` 为基线，连续交付四个本地 checkpoint：

1. Phase 7E：原生 1600×900 的 10×10 等距可玩战场。
2. Phase 8A：由战斗事件驱动的通用单位表现播放器。
3. Phase 8B：Fireball、Bone Spear、Thrust 的项目自有程序化表现。
4. 完整 Run、双渲染器、Reload、截图与确定性证据强化。

不 push、不建 PR、不改写历史；任一 checkpoint 未通过统一门禁则停在上一个绿色提交。Phase 7B–7D 与本批次在最终合并人工验收前均不晋升。

## Phase 7E：等距棋盘

- 冻结 Unity `Grid`、`TilemapCellGeometry`、相机拟合及 Highlight 合同，只迁移数据和语义。
- 画布 1600×900；Tile 菱形 96×48；棋盘顶部中心 `(550,145)`。
- 格子中心公式：`(550,169) + ((x-y)×48, (x+y)×24)`。
- 反向点击使用菱形包含测试；边界按距离、Y、X 稳定消歧。
- 单位、尸体、掉矛和数值条以格心为脚底锚点，绘制顺序按 `x+y`、X、稳定实例 ID。
- blocked cell 与现有 Snapshot 高亮只改变表现，不复制寻路或目标判定。
- ResourceSaver 生成 `battle-board.pure-run.isometric-v1`，canonical Catalog 114→115。

Checkpoint：`feat: render playable Godot battles on an isometric board`

## Phase 8A：通用单位表现

- MCP Profile 从 `ui-input` 切到 `presentation`。
- 两次冻结 Unity `StandardUnitTweenProfile`、动作、时长、缓动、位移、Release/恢复 marker 和来源 hash；动作 Sprite 与第三方载荷只审计。
- Application 提供只读 `BattlePresentationCue`、`BattlePresentationFrame`、`PresentationCueKind` 及 transition 前后 snapshot。
- Godot 统一播放 Move、Melee、Ranged、Cast、Hit、Defeat；Shadow 和 HP/MP 跟随根节点但不继承 Body 的局部表现。
- 玩家与 AI 共用 cue 队列；Pause/Step/1×/2×只影响表现节奏。失败时记录诊断并对齐最终 snapshot。
- ResourceSaver 生成 `presentation.unit.standard-v1`，Catalog 115→116。

Checkpoint：`feat: add deterministic Godot unit presentation playback`

## Phase 8B：三技能程序化表现

- 冻结 Fireball、Bone Spear、Thrust 的 Unity Presentation Graph、项目自有 Recipe/Profile、等级参数和源码 hash。
- Fireball：真实射线上的火核/尾迹/Impact；Lv1 无 AOE，Lv2 次反馈只来自真实 Damage event。
- Bone Spear：canonical 射线路径、切线旋转和短残影；Impact 只对应实际命中。
- Thrust：不改变 gameplay cell，只沿实际轴向路径绘制枪芒并对真实命中显示反馈。
- 生成三个 presentation Resource，Catalog 116→119；不复制 Piloto、纹理、材质、Shader 或 Audio。

Checkpoint：`feat: add programmatic Godot presentation for three core skills`

## 自动证据强化

- 自动覆盖 N1→BossVictory 与 Defeated、Layer 4/6 四路线、成长、Inventory 和 Save V4 Reload。
- 固定截图覆盖等距初始棋盘、路径、范围/AOE、三个技能、尸体、掉矛和召唤物。
- Compatibility 与 Forward+ 跑完整 Main smoke；Assembly Reload 后无重复信号、Tween 或临时节点。
- ResourceSaver 连续生成两次 byte-identical；Catalog 精确 119；运行完整 `Verify-GodotMigration.ps1` 与 OKF/UID/receipt/whitespace 门禁。

可选稳定性 checkpoint：`test: harden Godot isometric presentation journeys`

## 状态与人工闸门

自动阶段完成后保持：

`Generated/UnityOwned + manual_isometric_and_presentation_qa_pending`

最终人工验收合并覆盖 Phase 7B–8B：Inventory/成长、Layer 4–Boss/Save V4、等距点击和高亮、脚底锚点与排序、玩家/AI 动画、三个技能表现、播放控制、resize/Reload/Continue 和 Output 错误检查。

## 边界

- Core gameplay、Save V4、AI、奖励和技能数值不变。
- 不进入 Lv3、Treasure、Audio、Piloto/第三方表现载荷或 Windows Release/PCK。
- 不拆程序集、不重构目录、不切换 worktree。
