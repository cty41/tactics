---
type: Game System
resource: https://github.com/cty41/tactics
title: Godot migration implementation
description: Unity frozen Oracle to Godot migration boundaries, parity closure, content compilation and batch ownership.
tags: [migration, godot, core, parity, testing]
timestamp: "2026-08-10T21:52:00+08:00"
status: active
catalog_scope: godot-migration
repo_paths:
  - src/Tactics.Core
  - src/Tactics.Application
  - src/Tactics.Core.Tests
  - src/Tactics.Application.Tests
  - src/Tactics.UnityOracle.Tests
  - godot
  - Tests/golden
  - Tools/migration
  - .agents/plans/2026-08-09-godot-migration-parity-and-agent-enablement.md
  - .agents/plans/2026-08-10-godot-phase4-unit-batch-migration.md
verified_revision: d092a955
source_fingerprint: sha256:7452844dcb6ecae999e919ceb2a983efcd29a49bcb03fcb323caf692aedce8c5
---

# Current state

Unity `w1` 与 `unity-final-2026-08-08` 是只读 Oracle；唯一 Godot 项目为 `godot/project.godot`。`d092a955` 定性为技术 Spike：C#、GraphEdit、Undo、SubViewport、ResourceSaver、GdUnit4Net 和 headless 可运行，但没有证明 Unity 行为或真实资产等价。

Core 已移至 `src/Tactics.Core`，不再由 Unity `Assets` 反向编译；未接入冻结运行时的临时 Unity Adapter 已移除。`src/Tactics.Application` 已建立纯 .NET `ContentDraft → ContentCompiler/Diagnostics → ContentSnapshot` 边界。Godot Catalog 记录严格小写 ContentId、ResourceType、UID、诊断路径和 SchemaVersion；Godot Resource 只保留在 Adapter registry，不进入 Snapshot。

Phase 1A 已建立不可变 `BattleState/BattleUnitState`、typed `BattleCommand/BattleEvent/BattleTransition` 与稳定 `SplitMix64 v1` RNG。Phase 1B 将 Golden 升级为 schema v4，区分单位定义 `ContentId` 与运行时 `UnitInstanceId`，允许同一定义的多个战斗实例；命令、事件、状态键和回合顺序均使用实例 ID。

冻结 Unity 的 Dijkstra、Heap 与 `BattleInitiativeService` 通过独立 `Tactics.UnityOracle.Tests` 作为 linked source 原样编译；Phase 3 又把 Amazon 投矛、Ability Mana 和 Poison Buff 的冻结源码加入 blob 绑定。当前 Oracle Matrix 共绑定 15 个最终 Tag blob；Core 路径、先攻及 Poison Spear 的 Lv1 damage、Mana、持矛/掉落、Buff duration/tick/AddDuration 均有真实 AssetDatabase export 与冻结源码交叉证据。该测试层不引用 UnityEngine，也不进入 Core/Application/Godot Release。

Phase 1B 完整统一门禁已通过，覆盖 locked restore、单节点 solution build、Core/Application/Unity Oracle NUnit、Python、Skill/Incident、隔离 GdUnit、生产 Debug 恢复、Release 测试依赖排除、Poison Spear runtime/presentation、EditorPlugin headless 与 OKF。该结论关闭路径和初始先攻 tie-break 缺口。

Phase 1C 将冻结 `IBattleRuntimeScope/BattleRuntimeScope` 与 `PresentationExecutionPlanCompiler` 加入 linked-source Oracle，并将 Golden 升级为 schema v5。`InitiativeRoundState` 只重排当前轮 remaining，`BattleState.WithInitiativeChanged` 保留当前/已行动前缀；RuntimeScope 的 ownership、fault observation、re-entrant dispose 和 timeout callback 边界由冻结/Core 双实现测试；Presentation branch 在 Join 前停止且 continuation 只追加一次。完整统一门禁通过：Core 26、Application 3、Unity Oracle 8、迁移工具 16、Agent policy 8、OKF 14、GdUnit 4；Release 隔离、Poison Spear runtime/presentation、EditorPlugin headless 生命周期、生产 Debug 恢复与 6 scopes/0 unmapped 均通过。

Phase 1D 用版本化决策而不是伪造 parity 关闭剩余合同：冻结 Unity 没有统一不可变 `BattleCommand → BattleTransition`，其技能执行由 Controller/Executor 直接产生副作用；随机源同时存在 `UnityEngine.Random`、无种子 `System.Random` 与 `Guid.NewGuid` 排序。Battle Transition 是迁移合同，`splitmix64-v1` 是确定性替代合同；Phase 3 加入真实 Poison Spear 语义后，前者升级为 `battle-transition-v2`。

Phase 1A 的自动完整门禁及随后 canonical Godot Editor 人工 reopen/reload 闸门均已通过；EditorPlugin、Dock、3 节点 Presentation、GraphEdit/SubViewport 和 godot-ai 连接正常，未复现已记录的 C# Assembly Reload 重复类型故障。

Poison Spear 现在分成两条不会混淆的台账：旧 `poison-spear.json` 是 `Generated/UnityOwned` 技术 Spike；新 `poison-spear-lv1-real.json` 已达到 `Validated/UnityOwned`。`unity-assetdatabase-v1` 从最终 Tag 对应的 7 个真实资产导出 25 个对象、24761 个 SerializedObject 字段；一次性 typed Draft 经 Application 编译为 6 个内容条目，再由 ResourceSaver 生成 7 个 Godot Resource/Scene/Catalog。最终资产显式序列化迁移语义，不依赖 C# 默认值；连续生成、UID、hash、冲突保护、失败回滚和 receipt 均受测。当前项目自有程序化占位的人工视觉验收已通过；未来若迁入真实 Piloto 视觉，购买/EULA 证据仍必须另行补齐。

Application 使用固定内容类型/Schema catalog，未知类型和超前版本 fail-fast；真实 Poison Spear 目标依赖图包含 Skill → Presentation/Buff、Presentation → Projectile/Impact。ResourceSaver converter 与 `Tools/migration/staging.py` 共同覆盖 dry-run、语义无变化、UID 保留/漂移拒绝、目标人工修改保护、失败回滚、原子台账和重复执行幂等。

Phase 3 Editor authoring 坐标由 Unity AssetDatabase DTO 一次迁入最终 Godot `AuthoringNodePositions`；纯 Application 坐标进入 normalized Revision，拖拽和确定性 Auto Layout 使用 typed ChangeSet。GraphEdit 按稳定 ID 增量 reconcile，显示语义标题并在 Tooltip 保留完整 ID，状态切换不再重建节点或丢失位置/ScrollOffset；保存路径通过 `ResourceSaver` 后恢复既有 UID，并同时覆盖成功 UID 保留与失败 byte rollback。Tactics Tooling 使用 Godot 官方 Main Screen Plugin 进入中央工作区，Graph 与 SubViewport 采用 child stretch ratio 驱动、可折叠且可调的 64/36 左右分栏，不再占用 Output 底部区域；Preview 由居中的 `AspectRatioContainer(Fit)` 保持 `640:180` 逻辑画布比例。canonical Editor 完整重启后的 Main Screen、6 节点/4 edge、Undo/Redo、Save、Assembly Reload、Runtime 与等比 Preview 人工验收均已通过。

状态晋升后的 Phase 3 closure 统一门禁通过：Core NUnit 31、Application NUnit 13、冻结 Unity Oracle 9、迁移 Python 58、Agent policy 8、GdUnit 6、OKF 14；ResourceSaver 连续两次生成 byte-identical，真实两行坐标进入 Resource，Compatibility/Forward+、Runtime/Tween/Scope、Release 测试依赖隔离和 EditorPlugin headless enter/exit 均通过。real batch 已晋升为 `Validated/UnityOwned`；该状态只覆盖当前项目自有程序化占位，不得把 Presentation/Skill 整类直接切换为 GodotOwned。

Phase 4 Unit 自动实施已完成但仍等待人工视觉闸门：冻结 Golden 与 Unity AssetDatabase export 覆盖 12 个 Pure Run Unit、12 个 Prefab audit root、19 个项目自有 PNG payload 和 6 个只审计 Material root；29 个运行时依赖继续 deferred，第三方与 Unity Material/Shader payload 未复制。项目自有 `GoatBodyTint` 算法等价移植为共享 Godot CanvasItem shader，每只山羊 Resource 保存独立 BodyTint/BaseBodyColor 参数；普通单位继续使用 multiply。Core/Application 新增 engine-neutral Unit Definition、派生数值规则和编译器，运行时实例继续使用独立 `UnitInstanceId`。事务管线先复制 19 个 PNG，再由 ResourceSaver 生成 12 个 Unit Resource、共享 Unit Actor、精确 13 项 Catalog、4×3 Gallery 与可视 10×10 SpawnFixture，共 16 个 ledger artifact。方向契约为 South=DR、North=UL、East=UL+Body 水平镜像、West=DR+Body 水平镜像；死亡图不继承镜像，Shadow 永不镜像，召唤物缺少死亡图时安全回退。`unity-unit-sprite-geometry-v1` 另行冻结 living pivot `(0.5,0.078125)`、death pivot `(0.5,0.5)`、Body/Shadow 128/64 PPU 和 Shadow `localY=-0.03`、scale `0.8`、alpha `0.9`；Godot Resource 显式保存 Body offsets `(0,-108)/(0,0)` 与 Shadow offset `(0,3.84)`、scale `1.6`、alpha `0.9`。Gallery 另用 `ground-baseline-v1`，Actor 根节点统一表示脚底/尸体落点，三行 Y 为 `155/385/615`，标签独立放置，避免 pivot 修复后把旧贴图中心布局解释为地面而导致整批上移。Gallery 初始与 `R` 均为全 South/存活/tint 开启，`T` 仅切换六种山羊身体 shader；Gallery/Fixture 使用可辨阴影的中性灰蓝底，1280×720 逻辑画布使用 1600×900 F6 override 与 `canvas_items + keep`。当前 batch 与 generation receipt 为 `Generated/UnityOwned`，`visualAcceptance=manual_visual_qa_pending`；只有用户完成 Gallery/Spawn/Reload 人验后才能晋升为 `Validated/UnityOwned`。

Phase 4 二次视觉修复后自动门禁通过：Core 35、Application 19、冻结 Unity Oracle 11、迁移 Python 86、Agent policy 8、GdUnit 11、OKF 14；Debug/Release 零警告零错误，Unity export 两次独立运行、PNG 事务复制和 ResourceSaver 连续两次生成均 byte-identical，headless Editor UID/import 扫描、Compatibility/Forward+ Catalog/Factory/Fixture 运行和程序化 Gallery/10×10 Spawn 截图均通过。程序化截图由 Godot Image 使用已导入纹理、`goat-body-mask-v1` CPU 参考和 Sprite pivot/Shadow 几何换算确定性合成；共享 goat 图像在缩放前复制，避免后续实例逐个缩小，并有源纹理尺寸不变测试。截图只证明资源组合、位置与可检查性，不替代用户对 tint、体量、朝向、死亡图与 Shadow 的视觉接受。

## Verification model

`Tools/migration/Verify-GodotMigration.ps1` 串行执行 locked restore、单节点 build、Core/Application NUnit、Python、Skill/Incident lint、隔离的 GdUnit test host、Release build、Godot runtime/editor headless 与 OKF。GdUnit 3.1.1 的 Runtime Runner 要求 C# runner 位于 `project.godot` 主程序集，因此 test host 使用相同程序集名，但测试源码、`obj`、lock 和包与生产 csproj 分离；Release 明确排除。

## Next gates

1. 当前活跃的 Phase 4 Unit 计划 `.agents/plans/2026-08-10-godot-phase4-unit-batch-migration.md` 以 checkpoint `2ef51954` 为基线；自动实施和统一门禁已经完成，下一步只做 canonical Editor 中的 Gallery/方向/死亡/Shadow/Reload 人工验收，未通过前不关闭计划。
2. 项目 MCP Profile 已切换为 `content-authoring` 并由统一入口校验 23 个阶段白名单工具；本轮没有可用 live Editor session，所有实现与自动 QA 均在后台/无窗口完成。人工视觉、Assembly Reload 和组合场景接受仍留给用户，不使用前台 UI 自动化。
3. Buff/Item、Skill、AI/Encounter、Run/Persistence、UI/Input 与完整 Presentation/VFX/Audio 均不进入 Phase 4；Unit batch 完成人验后保持 `Validated/UnityOwned`，不会自动开始下一批或晋升类别 ownership。

Windows/Steam 仍是产品目标；Unity Windows Standalone 不执行，Godot Windows Release/PCK Smoke 延后到发布阶段。
