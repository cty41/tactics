---
type: Game System
resource: https://github.com/cty41/tactics
title: Godot migration implementation
description: Unity frozen Oracle to Godot migration boundaries, parity closure, content compilation and batch ownership.
tags: [migration, godot, core, parity, testing]
timestamp: "2026-08-09T22:03:25+08:00"
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
verified_revision: d092a955
source_fingerprint: sha256:859fc54bb74c6c4f0789901d0eab5af818720f34b6b4bb8bd0d75d4a5d179846
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

## Verification model

`Tools/migration/Verify-GodotMigration.ps1` 串行执行 locked restore、单节点 build、Core/Application NUnit、Python、Skill/Incident lint、隔离的 GdUnit test host、Release build、Godot runtime/editor headless 与 OKF。GdUnit 3.1.1 的 Runtime Runner 要求 C# runner 位于 `project.godot` 主程序集，因此 test host 使用相同程序集名，但测试源码、`obj`、lock 和包与生产 csproj 分离；Release 明确排除。

## Next gates

1. 为 Unit 批次编制详细范围、依赖、Oracle、转换器、台账和人工验收清单；
2. 将项目 MCP Profile 从 `phase3-observe` 切换为 `content-authoring`，再开始 Unit 批量迁移；
3. 后续按 Buff/Item → Skill → AI/Encounter → Run/Persistence → UI/Input → Presentation 批量迁移；Skill/Presentation 整类仍保持 `UnityOwned`，未来迁移真实 Piloto 第三方视觉前必须补齐购买/EULA 证据并重新验收。

Windows/Steam 仍是产品目标；Unity Windows Standalone 不执行，Godot Windows Release/PCK Smoke 延后到发布阶段。
