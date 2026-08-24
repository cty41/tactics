# Pure Run Tile 视觉锚点统一开发计划

## Summary

建立一套由美术合同驱动、预览与 Godot 运行时共同消费的 Tile 视觉锚点规则。逻辑 Node 永远位于 Tile 几何中心；视觉 Sprite 只通过声明式 placement 数据调整缩放与位移。单格地面单位以脚爪/胶囊底部接地形体中心对齐 Tile 中心，多格场景资产以 footprint 最前方 Tile 中心作为逻辑落点，并将资产声明的接地形体中心对齐该点。

成功标准：状态机能验证并渲染不同母版尺寸与 footprint；同一 placement 在 Review 和 Godot 中产生一致结果；现有地面单位迁移后不再依赖统一硬编码 `-108`；逻辑格、命中、occupancy、排序点和阴影锚点不改变。

## Current State

- 等距网格权威合同规定角色脚底、格标记和落地物的逻辑锚点均为格中心，视觉偏移只能存在于 Sprite/Profile。
- 现有地面单位多数在 `.tres` 中使用 `DownRightBodyOffset/UpLeftBodyOffset = Vector2(0, -108)`；运行时 `GodotUnitActor` 将该值写入 `Sprite2D.Offset`。
- 标准角色母版为 256×256；历史管线用最低像素 `y=236` 作为 baseline，但人工验收已确认应改为接地圆形/半圆形的中心，而非最低像素。
- 堕落祭坛已以 384×384 母版晋升：`Tools/artworks/pure_run/props/approved/pure_run_prop_altar_v01.png`。用户确认的 V04 对比使用 50% 显示比例、2×2 footprint，并将接地形体中心对齐 Tile 中心。
- 状态机已完成第一步兼容：新合同可通过 `canvasSpec.masterSize` 声明非 256 母版；旧合同缺省仍按 256×256 验证。
- 当前 `render-review` 对标准母版仍只生成单 Tile 通用预览；footprint、显示比例和接地点尚未进入合同，也没有被 Godot 生成器消费。

## Decisions

1. `GridPoint`/Node Position 始终是逻辑真值，不因美术透明边距或体型改变。
2. 新统一结构命名为 `tilePlacementSpec`，至少包含：
   - `footprintTiles: [width, height]`
   - `displayScale`
   - `groundAnchorPx: [x, y]`
   - `anchorMode`
   - `boardRole`：`player/target/neutral`
   - `screenFacing`：`up_right/down_left/non_directional`
3. `anchorMode` 固定支持以下语义：
   - `contact_shape_center`：地面角色、站立 NPC、箱子、祭坛等接地资产。
   - `virtual_ground_point`：蝙蝠等悬浮单位，Sprite 可在逻辑落点上方。
   - `visual_bounds_center`：无脚底死亡图等以完整 AABB 中心落位的资产。
   - `texture_center`：投射物与纯 UI/图标类资产。
4. Review 与 Godot 使用同一公式：`visualOffsetPx = canvasCenterPx - groundAnchorPx`；显示缩放独立应用，不把缩放折算进逻辑 Node。
5. 2×2 footprint 的逻辑落点为等距 footprint 最前方 Tile 中心；Review 必须画出全部四格，运行时 occupancy 仍由玩法数据决定，不能从图片 footprint 反推。
6. `.tres/.tscn` 只通过 ResourceSaver/Editor API 或现有受测 Factory 生成，不手写。
7. Adventure Board 的构图权威为：我方/探索起点在左下，探索轴指向右上，敌人、商人和有正面的目标对象位于右上并朝左下；Preview 和运行时代码必须使用同一角色映射，不从文件名中的历史 `DR/UL` 猜测。

## Scope

### In Scope

- 扩展 artwork contract/CLI、validator、Review renderer 和文档。
- 为已批准资产记录可审计的 placement 数据，并为接地点提供人工 Review 证据。
- 在 Godot Adapter 增加可复用的视觉 placement Resource/值对象及计算器。
- 让 Unit Asset Factory 从 placement 数据生成 Sprite offset/scale 配置。
- 迁移现有地面单位、悬浮单位和死亡图到明确 anchor mode；保持纹理像素不变。
- 提供未来 1×1/2×2 Adventure Board 场景道具可复用的 placement API 和测试 Fixture。
- 建立 Python、C#、Godot headless 与人工 Tilemap 截图验收。

### Out of Scope

- 修改 GridPoint、寻路、碰撞、技能范围、occupancy 或战斗排序算法。
- 重新绘制现有角色或批量修改已批准 PNG。
- 本计划内直接把祭坛、篝火、宝箱等加入具体 Adventure Board 事件场景。
- 祭品叠加、祭坛交互逻辑、碰撞和事件玩法。
- 通过运行时猜测 alpha 包围盒自动决定美术接地点；接地点必须来自受审计合同。

## File Structure

- `.agents/skills/pure-run-artwork-pipeline/scripts/artwork_pipeline.py` — 合同、placement 验证和 Tilemap Review 的唯一状态机入口。
- `.agents/skills/pure-run-artwork-pipeline/tests/test_artwork_pipeline.py` — schema 兼容、锚点计算和多格 Review 回归。
- `.agents/skills/pure-run-artwork-pipeline/SKILL.md` — placement 操作流程与人工门禁。
- `.agents/skills/pure-run-artwork-pipeline/references/sprite-size-contract.md` — 母版、显示比例、接地形体中心和 footprint 规范。
- `.agents/docs/isometric-grid-anchor-contract.md` — 逻辑锚点与视觉 placement 的系统权威。
- `.agents/docs/pure-run-artwork-guidelines.md` — 不同 asset kind 对应的 anchor mode。
- `Tools/artworks/pipeline/contracts/*.json` — 不可变 placement 合同记录。
- `Tools/artworks/pipeline/supporting-artifacts/*.json` — 用户确认的 Tilemap 对比证据。
- `godot/src/Tactics.Godot.Adapter/Runtime/TileVisualPlacement.cs` — 无 Godot Node 状态的 placement 计算模型。
- `godot/src/Tactics.Godot.Adapter/Runtime/TileVisualPlacementResource.cs` — 可由 ResourceSaver 写入的 Adapter Resource。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotUnitActor.cs` — 消费已解析的视觉 offset/scale，不改变根节点位置。
- `godot/src/Tactics.Godot.Adapter/Runtime/UnitDefinitionResource.cs` — 引用或内嵌 placement 配置，保留必要兼容读取。
- `godot/src/Tactics.Godot.Adapter/Editor/UnitAssetFactory.cs` — 从受审计 placement 输入生成 Unit Resource。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotTileVisual.cs` — 未来单格/多格场景道具共用的 Sprite 放置组件。
- `godot/tests/IsometricBattleBoardGodotTests.cs` — 逻辑根、单位 Sprite 和多格视觉落点回归。
- `godot/tests/UnitAssetFactoryGodotTests.cs` — ResourceSaver 输出与迁移幂等性测试；若现有测试职责文件名不同，则扩展现有对应 Factory 测试而不新建重复 Fixture。

## Implementation

### Phase A: 先用真实后续美术验证状态机

Phase A 只修改 artwork pipeline 和生产 Review，不修改 Godot。完成 Task 1–3 并取得用户对真实 Tilemap 预览的确认后，才能进入 Phase B。

### Task 1: 完成 artwork placement contract schema

- 目标：让每个需要 Tile 对齐的合同显式声明 footprint、显示比例、接地点与语义。
- 输入：现有 `canvasSpec.masterSize`、V04 祭坛对比和等距网格锚点合同。
- 输出：CLI 参数、不可变 `tilePlacementSpec` 和兼容读取逻辑。
- 涉及文件：pipeline script/tests、sprite-size contract、pipeline skill。
- 验收标准：
- `create-contract` 拒绝非正 footprint、越出母版的 anchor、非正 scale 和未知 anchor mode。
- placement 合同可显式声明 `boardRole/screenFacing`；`target` 只接受 `down_left` 或 `non_directional`，`player` 只接受 `up_right` 或 `non_directional`。
  - 旧合同没有 `tilePlacementSpec` 时继续使用历史 1×1/0.5/`y=236` 兼容值，哈希与读取不被改写。
  - 新地面合同必须显式提供 `contact_shape_center` 和 `groundAnchorPx`。
  - Python 回归覆盖 256×256 单格、384×384 四格、悬浮与 AABB 中心四类合同。

### Task 2: 让 Review renderer 成为 placement 权威消费者

- 目标：自动生成与运行时同公式的 Tilemap Review，替代手工 ImageMagick 位移。
- 输入：`canvasSpec`、`tilePlacementSpec` 和可选标准单位参考资产。
- 输出：正确缩放、正确 footprint、正确接地点的 Review PNG 与测量 JSON。
- 涉及文件：pipeline script/tests、reviews/supporting-artifacts。
- 验收标准：
  - 1×1 赤柴亚马逊的接地圆心位于单 Tile 中心。
  - 2×2 祭坛自动复现用户确认的 V04 几何：四格完整显示，资产接地点位于最前方 Tile 中心。
- Review 报告记录画布尺寸、显示后尺寸、逻辑 Tile 中心、最终 Sprite 原点和 anchor screen point。
- Adventure Preview 同时画出左下我方参考 Tile、右上目标 footprint、探索方向和目标面对方向；目标资产不得再孤立显示到无法判断朝向。
  - 标准 overlay、preview128 和 Tilemap Review 不裁剪 384×384 母版。
  - 手工偏移量不再出现在单个预览制作命令中。

### Task 3: 用后续场景资产验证 Preview 状态机

- 目标：在接触 Godot 之前，用真实连续生产检验新 schema 和 renderer，而不只依赖单元测试与祭坛回放。
- 输入：下一张尚未生成的 1×1 Tilemap 场景资产 `exit_portal`、已批准的 1×1 宝箱、已批准的 2×2 祭坛，以及商人 idle/trade 两态。
- 输出：全部由状态机直接生成的 1×1/2×2 Tilemap Review、测量 JSON、用户反馈与 supporting artifacts。
- 涉及文件：后续资产 prompt/contract/job/attempt、pipeline reviews/reports/supporting-artifacts。
- 验收标准：
- `exit_portal` 从新合同开始完整走生成、去幕、placement validation 和自动 Tilemap Review，不使用手工 ImageMagick 位移。
- 至少一个既有 1×1 已批准道具通过兼容 placement 重渲染，证明旧资产可以迁移而不改 PNG。
- 2×2 祭坛由新 renderer 自动复现 V04 比例与接地点，anchor screen point 与 V04 相差不超过 1 px。
- 出口、宝箱、祭坛与商人均显示在右上目标位；有正面的资产朝左下。商人 idle/trade 必须同向镜像并保持身份、比例和接地点一致。
  - 三类 Review 均明确画出 footprint、Tile 中心和资产 anchor，且测量 JSON 可复算最终位置。
  - 用户分别确认 1×1 新资产、1×1 兼容资产和 2×2 祭坛无缩放或悬浮问题。
  - 任一真实资产失败时，只回到 Task 1–2 修正规则；在三项人工确认齐全前禁止开始 Godot 修改。

### Phase B: 将已验证规则带入 Godot

Phase B 只消费 Phase A 已确认的合同和测量公式。先生成/复制获准的实际资产，再验证 Resource 与运行时；不得用临时占位图代替最终运行时结论。

### Task 4: 建立 Godot placement 计算模型

- 目标：把合同公式实现为 Adapter 内可单元测试的唯一计算器。
- 输入：canvas center、ground anchor、display scale、anchor mode 和 footprint。
- 输出：Sprite offset/scale、逻辑落点与 Review 相同的屏幕几何。
- 涉及文件：`TileVisualPlacement.cs`、`TileVisualPlacementResource.cs` 及对应测试。
- 验收标准：
  - `visualOffsetPx = canvasCenterPx - groundAnchorPx` 只有一个实现位置。
  - 计算器不引用 Core/Application，也不修改 GridPoint。
  - 256×256、ground anchor `(128,224)` 的地面单位得到 offset `(0,-96)`。
  - 384×384、祭坛 ground anchor `(192,342)` 得到 offset `(0,-150)`。
  - 非法尺寸、anchor 越界、scale 非正和 footprint 非正会被 Resource 校验拒绝。

### Task 5: 通过 ResourceSaver 迁移 Unit Definition

- 目标：现有单位不再依赖复制粘贴的 `-108`，而由 placement 数据生成视觉配置。
- 输入：已批准 DR/UL/Death 纹理、各自 anchor mode 和人工确认的接地点。
- 输出：幂等生成的 Unit Resource 与迁移 receipt。
- 涉及文件：`UnitDefinitionResource.cs`、`UnitAssetFactory.cs`、既有 Factory 测试和生成输出。
- 验收标准：
  - 所有 Unit Resource 的 offset/scale 都可追溯到 placement 输入。
  - 地面单位采用 `contact_shape_center`；蝙蝠采用 `virtual_ground_point`；死亡图采用 `visual_bounds_center`。
  - DR/UL 共用身份体量但允许各自声明 anchor，不通过镜像猜测 Y。
  - ResourceSaver 连续运行两次产物哈希一致。
  - 旧 offset 字段若为兼容保留，只能由生成器填充，运行时不存在第二套修正。

### Task 6: 统一运行时单位落位

- 目标：单位逻辑根保持 Tile 中心，Sprite 只消费 placement，阴影继续锚定逻辑落点。
- 输入：迁移后的 Unit Resource。
- 输出：`GodotUnitActor` 的统一放置行为和回归测试。
- 涉及文件：`GodotUnitActor.cs`、`UnitDefinitionResource.cs`、battle board tests。
- 验收标准：
  - Idle、移动、攻击、施法、受击切图不改变 ground anchor。
  - Tween 只改变表现层，结束后回到 placement 基线。
  - Shadow Position 不从 Sprite Offset/Position 反推。
  - 逻辑 Root、点击命中、格标记、路径和 occupancy 的测试结果保持不变。
  - DR/UL 以及水平镜像方向的接地点均落在同一 Tile 中心。

### Task 7: 提供多格场景视觉组件

- 目标：让祭坛等未来场景资产复用同一 placement 公式，而不复制单位 Actor 逻辑。
- 输入：2×2 祭坛 placement 与共享 IsometricGridProjection。
- 输出：轻量 `GodotTileVisual` 和只读测试 Fixture。
- 涉及文件：`GodotTileVisual.cs`、共享 placement 计算器、Godot tests。
- 验收标准：
  - 组件接收逻辑落点、纹理和 placement Resource 后即可显示 1×1 或 2×2 视觉。
  - 2×2 只影响绘制 footprint 和视觉落位，不隐式修改玩法 occupancy。
  - 祭坛获批母版通过正式资产复制/生成流程进入 `godot/assets` 后，Fixture 截图与自动 Review 的 anchor screen point 一致，允许误差不超过 1 px。
  - 组件不包含祭坛事件、交互或碰撞逻辑。

### Task 8: 全链路验证与人工验收

- 目标：证明同一 placement 在状态机、Resource 和运行时没有漂移。
- 输入：标准赤柴亚马逊、至少一个 UL 单位、蝙蝠、死亡图和祭坛。
- 输出：自动验证记录、后台截图和人工 QA 条目。
- 涉及文件：Python tests、Godot tests、`.agents/docs/manual-acceptance.md`、相关 OKF 正文。
- 验收标准：
  - artwork pipeline 全测试与 `check --strict` 通过。
  - 相关 .NET/Godot tests 通过，最后运行 `Tools/godot/Verify-GodotProject.ps1`。
  - 后台截图同时展示 1×1 单位、悬浮单位和 2×2 祭坛的 Tile 中心标记。
  - 自动检查只证明几何与配置一致；视觉是否自然仍登记为人工 QA，不自动标记通过。
  - 用户确认没有上浮、下沉、缩放突变或动作切图跳动后，才关闭人工验收项。

## Test Plan

### Python

- contract schema 默认值与非法参数。
- 256/384 master size validation。
- 四种 anchor mode 的解析与报告。
- 1×1/2×2 footprint 等距中心计算。
- `player/target/neutral` 与 `up_right/down_left/non_directional` 的合法组合及 Preview 布局。
- Review 输出尺寸、anchor screen point 与不裁剪断言。
- 使用 `exit_portal` 的真实新生产验证 1×1 新合同与 Preview。
- 使用既有 approved 道具重渲染验证 1×1 兼容迁移。
- 使用祭坛自动重渲染验证 2×2 placement 与 V04 的 1 px 等价性。
- pipeline `check --strict`。

### .NET / Godot

- placement 纯计算单元测试。
- Unit Resource 生成、保存、重载与幂等性。
- DR/UL/action pose 的 anchor 不变量。
- Shadow 与逻辑 Root 不受 Sprite offset 影响。
- 2×2 visual 与共享 IsometricGridProjection 的屏幕坐标一致性。
- 统一 Godot verifier。

### Manual QA

- Phase A 门禁：先验收 `exit_portal`、一个既有 1×1 道具和 2×2 祭坛的状态机 Preview；三项未全部通过时不开始 Godot 修改。
- 朝向门禁：Preview 中我方参考位必须在左下、目标 footprint 在右上，目标正面朝左下；商人两态方向一致。
- 标准赤柴亚马逊脚爪接地圆心与 Tile 中心重合。
- 角色在 Idle/Move/Melee/Cast/Hit 间切换时无上下跳动。
- 悬浮单位保留稳定虚拟落点，不被强压到地面。
- 死亡图按 AABB 中心落地，不复用站立脚底。
- 祭坛覆盖四格、前台脚中心落在最前方 Tile 中心，视觉大小与 V04 一致。

## Risks / Assumptions

- 现有 `y=236/-108` 是“最低像素基线”历史值，不能机械统一改为 `y=224/-96`；每个资产族需要测量并由用户抽样确认。
- 透明 alpha AABB 不能可靠推断接地形体中心，尤其有武器、尾巴、光效或碎片时；状态机只验证声明合法性和 Review 几何，接地点仍需人工确认。
- Adventure Board 当前没有正式场景道具渲染链；本计划提供共享组件和 Fixture，但不擅自接入具体事件。
- Phase A 使用后续真实资产验证状态机；Phase B 必须先把获准资产通过正式复制/ResourceSaver 流程放入 Godot，随后产生的运行时截图才可作为后续验证证据。
- 祭坛当前获批 master 只有画布尺寸进入合同；其 `footprint/groundAnchor/displayScale` 需在 Task 1 通过新不可变 placement 合同补录，不能改写已晋升历史合同。
- Godot 修改属于 reload-sensitive C#/ResourceSaver 工作，实施时必须通过 `godot-editor-lifecycle` 正常关闭并恢复由该流程管理的 Editor。

## Handoff Notes

- 首先读取 `.agents/docs/isometric-grid-anchor-contract.md`、`.agents/knowledge/operations/pure-run-artwork.md`、本计划和祭坛 V04 supporting artifact。
- 先完成 Phase A（Task 1–3），依次用 `exit_portal`、既有 1×1 道具和祭坛验证 Preview；只有用户确认三类真实预览后才进入 Godot。
- Phase B 先走正式资产换入流程，再验证 Resource/运行时；不得把未接入资产的离线预览写成 Godot 通过。
- 不手写 `.tres/.tscn`，不移动逻辑 Root，不修改 GridPoint/occupancy，不把视觉 footprint 当玩法占位。
- 实施与验证完成后，按 `project-doc-organization` 将稳定规则并入权威文档，更新受影响 OKF scope，将真正未完成项写入统一缺口，并删除本完成计划，由 Git 保存历史。
