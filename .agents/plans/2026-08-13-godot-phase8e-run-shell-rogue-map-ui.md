# Godot Phase 8E：Pure Run 流程 UI 与七层 Rogue Map

## Summary

以 `migration/godot`、当前 Phase 7B–8D 自动实现为基线，在不修改战斗、奖励、AI、Save V4 或内容数值的前提下，把仍由离散按钮页驱动的 Run 流程改造成与 Unity 语义一致的可恢复 Rogue Map 主循环：

`Home → Map → Node → Battle/Rest/Store/Mystery → Settlement/Progression → Map → Boss Summary`

今晚连续完成 review、自动测试和本地 checkpoint commit；不 push、不建 PR。Phase 7B–8E 保持人工验收 pending，早晨统一检查 UI 操作、地图观感和完整流程。

## Implementation Changes

### Checkpoint 1：审查修复与冻结 UI 合同

- 修复成长保底候选在移除前消耗随机数导致的三槽 Unity parity 漂移，并用固定 seed 固定完整三槽。
- 修复 Inventory/Progression ResourceSaver 对当前 124/125 项 Catalog 阶段不兼容的问题，恢复重复生成能力。
- 冻结 Unity `RoguelikeMapUIController`、`RoguelikeMapUINode`、`RoguelikeMapGenerator`、Home、Settlement、Inventory 和 LevelUp 的流程语义、节点状态、连接揭示、自动居中、重入和源码 hash；UXML/USS、背景、图标和第三方载荷只审计，不复制。

提交：

```text
fix: preserve growth offer and content rebuild parity
```

### Checkpoint 2：Run Flow 与 Map 只读投影

Application 新增 engine-neutral 的只读 UI 投影：

- `PureRunFlowSnapshot`：当前页面、可执行操作、Run revision、金币、队伍摘要、PendingProgression 和恢复诊断。
- `PureRunMapSnapshot`：固定七层节点、连接、节点状态、当前/选择/已访问身份及建议居中节点。
- 节点状态：`Locked`、`Available`、`Current`、`Selected`、`Pending`、`Completed`。
- 节点集合：Start、N1、N2、N3、Layer 4 四路线、Layer 5 Elite、Layer 6 四路线、Special Boss。

投影只读取 `PureRunState`、`PureRunMapState`、checkpoint 和 terminal summary；不修改或复制 Run 状态机。所有点击仍提交既有 `PureRunSessionService`、`PureRunLayerFourNodeService` 和 `PureRunFullRunService` intent。

固定连接为只前进图：Start→N1→N2→N3→Layer4 四选一→Layer5→Layer6 四选一→Boss。选择分支后只保留已选路线为已访问/当前，其余分支锁定；Reload 从 Save V4 得到相同投影。

提交：

```text
feat: project Pure Run state into a deterministic rogue map
```

### Checkpoint 3：Godot Rogue Map 与持久 Run Shell

新增原生 1600×900 程序化 Control：

- 左侧可平移/滚动的七层地图，层级从下到上排列。
- 节点连接线、节点类型符号、层号、标题和状态色。
- Available/Current 可点击；Locked、已完成和未选分支不可触发事务。
- 初次进入或进度变化时居中当前节点，随后不抢夺玩家滚动位置。
- Hover/选择显示节点说明、Encounter/事件身份和结构化不可用原因。
- 不复制 Unity UXML/USS、Tile、背景纹理、材质或 Shader；使用 Godot Control 自绘等价语义。

Main 增加持久 Run Shell：

- 顶部显示 Layer/节点、Gold、背包数和三名角色 HP/MP/等级。
- Map、Inventory、Home/Menu 入口按页面状态启用；Battle 表现播放期间不允许切页。
- Home 的 New/Continue 进入 Map；Ready/PendingBattle/节点处理中恢复到权威页面。
- N1–N3 Settlement 的 Continue 返回 Map，由玩家点击下一节点，不再直接创建下一战。
- Inventory 和 Progression 完成后返回原流程上下文；成长未完成时地图显示阻断并提供入口。
- Layer 4/6 的 Rest、Store、Mystery 和 Battle 从地图节点进入；完成后回到同一张地图。
- Boss 胜利/Defeated 仍进入 Summary，显式 Return Home 后消费摘要。

提交：

```text
feat: add the Godot Pure Run shell and rogue map UI
```

### Checkpoint 4：自动流程旅程与恢复证据

- Application 单测覆盖所有 Run phase 到页面、节点状态、连接、可用操作和建议居中节点的映射。
- GdUnit/Headless smoke 覆盖 New/Continue、N1→N3、Layer4、Layer5、Layer6、Boss、Defeated、Inventory、Progression 和 Summary 导航。
- 四路线分别覆盖选择锁定、节点事务、Reload 后同节点/商品/事件恢复。
- 连续点击、旧 callback 和旧 revision 不得重复开始 Encounter 或应用节点效果。
- Save V1–V4 升级后 Map 投影保持角色、物品、技能、节点和当前页面。
- Compatibility 与 Forward+ 均运行 Main flow smoke；Assembly Reload 后信号、页面、地图滚动状态和输入不会重复绑定。
- 完整执行 `Verify-GodotMigration.ps1`、Debug/Release、Core/Application、Oracle、GdUnit、Python、UID、OKF、敏感信息和 whitespace 门禁。

若有独立测试/稳定性修改，提交：

```text
test: harden Godot rogue map flow and recovery
```

## Public Interfaces

- Application 增加只读 `PureRunFlowSnapshot`、`PureRunMapSnapshot`、节点/页面枚举及 projector。
- Godot Adapter 增加 `GodotRogueMapView` 和 Run Shell；页面不直接构造最终 `PureRunState`。
- Core gameplay、Save V4 schema、ContentId、canonical Catalog 124、技能、AI、奖励和表现事件不变。
- UI/地图 Control 不伪装为 gameplay Catalog 内容。

## Test Plan

自动验证：

- 每个 Run phase 对应唯一权威页面和可执行操作。
- 七层节点和连接固定，四选一前四节点可用，选择后仅选中分支保留。
- N1/N2/N3、Elite 和 Boss 身份与现有 Run/Map service 一致。
- PendingBattle、Progression、Rest、Store、Mystery、Defeated 和 Boss summary Reload 路由正确。
- 地图点击只触发现有 Application intent；不可用节点无状态副作用。
- Settlement Continue 只返回地图，不自动开始下一 Encounter。
- Map/Inventory 往返及页面重建不重复回调。
- 1600×900 和非 16:9 keep 下关键控件在安全区。
- 完整自动旅程、双渲染器、Reload、ResourceSaver 和统一门禁全绿。

早晨人工验收：

1. Home New/Continue 后进入七层地图，当前可用节点和连接清晰。
2. N1→N3 每次结算/成长后回地图，再点击下一节点开始战斗。
3. Layer 4/6 四选一后其他路线锁定；Rest/Store/Mystery/战斗均可返回地图。
4. Inventory、Progression 与地图往返，Reload 后物品、技能、节点和页面不丢失。
5. Layer 5、Boss、Defeated 和 BossVictory 导航正确。
6. 地图拖动/滚动、首次自动居中、Hover 和不可用提示正确。
7. 非 16:9 resize、Continue 和 Assembly Reload 正常。
8. 连同尚未关闭的 Phase 7B–8D：成长、装备、等距战斗、技能/状态表现、镜头和播放控制一并复验。
9. Output 无 Unicode/NUL、UID、Resource、duplicate type、disposed object、重复信号、存档或流程死锁错误。

## Assumptions

- 当前七层 Run、Layer 4/6、Elite、Boss 和 Save V4 gameplay 已由既有服务实现，本阶段迁移其 UI/地图外壳，不另建第二套状态机。
- Unity 的只前进连接、状态揭示、当前节点居中和重入语义是权威；视觉采用 Godot 程序化占位实现。
- 正式地图背景 Shader、图标、UI 动效、Audio 和第三方视觉载荷继续后移。
- 每个 checkpoint 自动 review、统一门禁和 scoped commit；失败停在最后一个绿色 commit。
- 不 push、不建 PR、不改写历史、不切换 worktree；MCP Profile 保持 `presentation`。
