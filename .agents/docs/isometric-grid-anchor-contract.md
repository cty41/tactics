# 等距棋盘投影与锚点合同

## 定位

`GridPoint` 是玩法真值；Godot `Vector2` 是共享投影结果。战斗、Adventure、单位、落矛、指针命中与编辑预览必须复用同一投影，不得各自维护偏移表。

```gameplay-contract
id: GRID-PROJECTION-001
status: verified_current
statement: 当前棋盘为十乘十零基逻辑格，格子中心以 (550,601) 为首格中心、96 为菱形宽、48 为菱形高，并按 ((x-y)*48, -(x+y)*24) 投影到 Godot 局部坐标。
verification:
  - layer: godot_test
    path: godot/tests/IsometricBattleBoardGodotTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: GRID-HIT-ROUNDTRIP-001
status: verified_current
statement: 每个合法格中心必须可命中并往返为原 GridPoint；共享边界存在多个候选时按中心距离、Y、X 的稳定顺序选择。
verification:
  - layer: godot_test
    path: godot/tests/IsometricBattleBoardGodotTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: GRID-LOGIC-AUTHORITY-001
status: verified_current
statement: 路径、射程、视线、占位和效果范围只使用 GridPoint 裁决；屏幕坐标只用于输入映射与表现，不能直接参与玩法合法性。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/BoardAndRulesTests.cs
  - layer: application_test
    path: src/Tactics.Application.Tests/PlayableBattleSessionServiceTests.cs
dsl_support: partial
```

## 锚点约束

角色脚底、格标记和落地物的逻辑锚点都是格中心。视觉素材允许在 Sprite/Profile 内调整绘制偏移，但逻辑 Node、点击命中和状态标记不得复制该视觉偏移。
