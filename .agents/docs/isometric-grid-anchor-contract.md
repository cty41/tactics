---
title: 等距战场网格与视觉锚点契约
status: active
---

# 等距战场网格与视觉锚点契约

本文定义 Tilemap 战场中逻辑格、可见地面、单位与输入的唯一坐标语义。实现事实仍以 `TilemapCellGeometry`、对应消费者和自动测试为准。

## 坐标术语

| 术语 | 定义 | 允许用途 |
| --- | --- | --- |
| Cell Origin | `CellToWorld` 返回的格坐标原点；在当前等距布局中不是菱形中心 | 调试显示、推导坐标轴 |
| Ground Center | 可见菱形中心，也是逻辑地面落点 | `ICell.WorldPosition`、单位根、尸体、掉落物、高亮、Scene Handle、自动化点击 |
| Visual Anchor | 相对单位根的作者局部 Transform | Sprite 脚底 Pivot、Shadow、血条或显式 VFX 高度 |
| Screen Pointer | Camera Ray 与战场平面的原始世界交点 | 直接交给 `WorldToCell` |

`TilemapCellGeometry.GetGroundCenterWorld` 是 Cell 坐标到 Ground Center 的唯一共享实现。`TilemapCellGeometry.WorldToCell` 直接调用 Unity 映射；禁止叠加 `GetCellCenterWorld - CellToWorld`、Tile Anchor 或固定半格补偿。

## 消费者职责

- `TilemapCellManager` 初始化时把 Ground Center 写入 `VirtualSquareCell.WorldPosition`；移动、生成、召唤、击退和尸体逻辑继续消费该值。
- `ProceduralTileHighlightRenderer` 从相邻 Ground Center 推导菱形轴与顶点。Hover、范围、路径、AoE 和技能引导属于静态格子提示，中心锁定 `ICell.WorldPosition`；Friendly、Selected、Finished、Targetable 属于动态单位状态，中心在 `LateUpdate` 读取单位根世界坐标并连续跟随。两类提示使用独立 Mesh，Sorting Order 和材质队列处理渲染层级，禁止用世界 Y 偏移解决遮挡。
- `PlayerInputGameplayStepAdapter` 将 `ICell.WorldPosition` 投影到屏幕；不得为测试单独重算 Tilemap 原点。
- Battle Test Scene Handle、`UnitBrush` 与 `CellBrush` 使用同一 Ground Center。其 `_offset` 只表示显式作者调整，不能承担网格校正。
- 单位根只表示 Ground Center。Pure Run Sprite/VisualRoot 使用零作者基线，Shadow 使用 Prefab 保存的 `localY=-0.03`；Tween 只相对 Prefab 基线工作。Legacy 非 Tween 单位可以保存不同的显式 Sprite 基线，但运行时不得再次改写。
- 技能目标中心、浮字高度与 VFX 接触高度是具名表现锚点，不属于网格换算，也不能反向改变 `ICell.WorldPosition`。

单位状态高亮的阵营规则固定为：`PlayerNumber == 0` 的 Friendly、Selected、Finished、Targetable 均可见；非 0 阵营隐藏 Friendly、Selected、Finished，仅在可攻击时显示 Targetable。动态高亮绑定单位根，不绑定 Sprite/VisualRoot，因此 Idle、Move Cycle、Action、Hit 与 Dying 的局部 Tween 不会让地面菱形上浮或抖动。

## 防漂移规则

- 新代码需要格子世界落点时优先消费 `ICell.WorldPosition`；只有没有 Cell 实例的网格工具才调用 `TilemapCellGeometry`。
- 禁止把 `CellToWorld` 结果命名为 center、ground 或 landing point。
- 禁止在输入、高亮和单位三条链路分别保存补偿常量。
- 禁止通过 `UnitLeftCell`、`CurrentCell` 或 `WorldToCell` 更新移动中的单位状态高亮；逻辑 Cell 可以在分段移动开始时切换，地面视觉必须持续跟随单位根。
- 禁止在 `TilemapUnit.Initialize` 后写 Sprite/Shadow 局部基线；Prefab 作者状态和 `UnitTweenVisual` 的捕获/恢复是唯一视觉基线链。
- 自动测试必须同时验证选中的 Cell、Highlight Mesh 中心和单位根，而不是只验证 `WorldToCell` 往返。

## 验证入口

- `EncounterConfigTests`：转换、Grid Transform、静态与动态 Highlight Mesh 几何。
- `SharedBattlePrimitivesTests`：单位状态阵营规则、状态替换/清理与移动中连续跟随。
- `Test1BattleMapLayoutEditorTests`：正式 Grid/Tilemap 配置。
- `PureRunUnitShadowEditorTests`：Legacy 与 Pure Run 的作者 Sprite/Shadow 基线。
- `PlayerInputGameplayPlanTests`：生产输入模块与虚拟 Pointer 闭环。
