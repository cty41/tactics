# Godot 迁移：Parity Closure 与 Agent 能力建设

状态：active

来源：用户于 2026-08-09 确认的《Godot 迁移修订计划：等价性收口、Agent 能力建设与批量迁移》。

## 当前判定

`d092a955` 是技术 Spike，只证明 Godot 4.7.1 Mono、.NET 9、C# EditorPlugin、GraphEdit、Undo、SubViewport、ResourceSaver、GdUnit 和 headless 链路可运行；它不证明冻结 Unity 行为与资产等价。Poison Spear 当前所有权仍为 `UnityOwned`。

## Phase 0：状态与 Agent 基础

- [x] Core 源码移至 `src/Tactics.Core`，移除未接入冻结运行时的 Unity Adapter。
- [x] 创建纯 .NET `Tactics.Application` Draft/Snapshot/Compiler/Diagnostics 骨架。
- [x] 建立唯一 solution、NuGet lock、runsettings 与串行验证入口。
- [x] 生产 Release 排除 GdUnit 和测试；GdUnit Runtime Runner 使用同程序集名、独立 `obj`/lock/包的测试宿主。godot-ai 的 Release 导出剥离留到发布门禁验证。
- [x] 创建 Godot Rule、路由/专项 Skills、Research Guide、Incident schema 与首批记录。
- [x] 新增 `godot-agent-workflow` OKF scope。
- [x] 实现项目级 godot-ai Attach 配置策略、幂等迁入/检查/Profile 切换脚本与漂移/回滚测试；`.codex/config.toml` 保持本机忽略，统一门禁在配置存在时自动检查。
- [x] 在 canonical Godot Editor 执行一次 Codex Configure，将生成块原子迁入项目配置并从用户配置移除；`phase3-observe` 精确启用 16 个工具，重复迁入为 no-op，Godot worktree 的 `codex mcp list` 已发现 pinned Attach。
- [x] 已从 Godot worktree 重启 Codex 任务并完成 `phase3-observe` MCP smoke：唯一 canonical Session、Editor/Scene/Resource 读取、`godot_ai_smoke` 3/3、main run、game/editor 日志、1280×720 非 stale 截图、stop 与 plugin reload/reconnect 全部通过。
- [x] 完成统一验证并更新 Phase 1A worktree 证据：Core 23、Application 3、迁移工具 14、Agent policy 8、OKF 14、GdUnit 4、Incident 7、Skill 6，且 Release 测试依赖隔离、Poison Spear headless、Golden schema v3 双端重放与 EditorPlugin 生命周期通过。

## Phase 1：Parity Closure

- [x] 建立 Oracle Matrix，绑定冻结 commit、Unity 测试、输入、预期和证据状态；缺失 Oracle 显式标记 pending。
- [x] Golden schema v3 已扩展显式 RNG 状态、命令序列、Poison 状态应用、顺序事件和最终 BattleState；真实 Buff tick/stack 与 Poison Spear 资产等价仍保留独立 Oracle 缺口。
- [x] NUnit 与 Godot 测试实际消费同一 Golden 文件。
- [x] Phase 1A 完整门禁通过；生产 Debug 程序集已在隔离 GdUnit TestHost 后恢复。人工重新打开 Editor 后，C# EditorPlugin、Dock、3 节点 Poison Spear Presentation、GraphEdit/SubViewport 与 godot-ai v3.1.2 连接均正常，且没有 duplicate type 或 assembly unload/reload 错误。
- [x] Phase 1B 建立独立 `Tactics.UnityOracle.Tests`：六个 linked source Git blob 与最终 Tag 一致，并实际执行冻结 Dijkstra/Heap 和 BattleInitiativeService；不依赖 Unity Editor，也不进入生产依赖。
- [x] Golden schema v4 区分单位定义 `ContentId` 与运行时 `UnitInstanceId`；Core 支持同一单位定义的多个实例，并按冻结合同执行 Dijkstra 与 `Initiative → PlayerNumber → SpawnOrdinal`。
- [x] Phase 1B 完整门禁通过：Core 24、Application 3、Unity Oracle 3、迁移工具 16、Agent policy 8、OKF 14、GdUnit 4；Release 依赖隔离、Poison Spear runtime/presentation、EditorPlugin headless 生命周期及生产 Debug 恢复均通过。
- [x] Phase 1C 将冻结 `BattleRuntimeScope`、`IBattleRuntimeScope` 和 `PresentationExecutionPlanCompiler` 原样链接进测试 Oracle；九个源 blob 均绑定最终 Tag，测试程序集同时执行冻结实现和 Core 实现。
- [x] Golden schema v5 增加当前轮动态先攻、RuntimeScope 生命周期和 Presentation Fork/Join 向量；Core、冻结 Unity Oracle 与 Godot TestHost 消费同一文件。
- [x] Core 新增不可变 `InitiativeRoundState`，明确“当前/已行动不重排，只排序 remaining”合同；`BattleState.WithInitiativeChanged` 将该合同接入不可变战斗状态更新。
- [x] Core `PresentationGraphCompiler` 保留 Fork/Join 边界，分支在 Join 前停止，Join 后 continuation 只编译一次；Runtime/Preview 后续消费同一 `PresentationExecutionPlan`。
- [x] Phase 1C 完整统一门禁通过：Core 26、Application 3、Unity Oracle 8、迁移工具 16、Agent policy 8、OKF 14、GdUnit 4；Release 隔离、Poison Spear runtime/presentation、EditorPlugin headless 生命周期、生产 Debug 恢复与 6 scopes/0 unmapped 均通过。
- [x] Phase 1D 通过 `contract-decisions.json` 明确关闭剩余合同：冻结 Unity 没有统一不可变 Command/Event 边界且混用全局/非确定 RNG，因此 Battle Transition 定性为版本化迁移合同，`splitmix64-v1` 定性为确定性替代合同；Phase 3 加入真实 Poison Spear 语义后升级为 v2，Phase 5A 加入 Status/Consumable 后升级为 `battle-transition-v3`。
- [x] Phase 1D 完整统一门禁通过：Core 27、Application 3、Unity Oracle 8、迁移工具 20、Agent policy 8、OKF 14、GdUnit 4；没有把迁移合同错误宣称为逐语句 Unity parity。真实 Poison Spear 数值、Buff tick/stack 与资产引用继续留在 Phase 2–3。

## Phase 2：真实内容管线

- [x] Unity Editor-only `unity-assetdatabase-v1` exporter 已通过 Unity 6000.3.11f1 batchmode 编译和执行；只使用 AssetDatabase、SerializedObject 与 PrefabUtility，不解析 Unity YAML。
- [x] Disposable DTO 记录最终 Tag/commit、Git blob、GUID、LocalFileId、AssetDatabase dependency hash、对象层级、字段、引用、ContentId 与 exporter version；7 个真实源资产共导出 25 个对象/24761 个字段，Gradient 已覆盖且 0 warning，两次运行 byte-identical。
- [x] Application 使用固定 `ContentSchemaCatalog.RuntimeV1` 执行 ID/Schema/type/reference diagnostics，并通过包含 `buff.poison` 的真实 Poison Spear 目标依赖图测试。
- [x] `Tools/migration/staging.py` 已覆盖 dry-run、语义 no-op、UID 保留、目标 hash、未托管/人工修改冲突、失败回滚、原子台账和重复执行幂等；最终 ResourceSaver converter 与真实 Godot 资产生成属于 Phase 3。

## Phase 3：真实 Poison Spear

- [x] 从最终 Tag 导出真实 Lv1 数据与表现引用，不再使用硬编码 factory 作为真相源。
- [x] 源侧真实导出、typed Draft、Application diagnostics 和 ResourceSaver 生成已完成；真实 batch 在生成门禁时达到 `Generated/UnityOwned`，连续两次生成的 7 个目标和 ledger byte-identical，UID、目标 hash、人工修改保护、失败回滚与 generation receipt 均受测。
- [x] Core `battle-transition-v2` 覆盖 targeting、LOS、Mana、damage、Poison AddDuration/TurnStart tick/TurnEnd decrement、持矛前置、半径 3 确定性掉落与掉落后不可重复施放；Golden schema v6 与 15 个冻结源码 blob/真实 AssetDatabase export 共同取证。
- [x] Runtime/Preview 共用 PresentationExecutionPlan；真实 Presentation Graph 的 6 个 stable node ID、4 条 edge、Schema 和 ResourceSaver 保存/回滚已接入。
- [x] Application 提供规范化 SHA-256 Revision、`expectedRevision`、allow-listed typed ChangeSet 和失败原子性；Godot Dock 的一次 Toggle 对应一个 Undo action，并同步 Runtime/Preview plan。GdUnit 覆盖 stale revision 与保存失败字节回滚。
- [x] Phase 3 人工闸门反馈已收口到实现：真实 Unity 坐标进入 `AuthoringNodePositions` 和 Revision，GraphEdit 使用语义标题与稳定 ID Tooltip，Toggle 改为增量 reconcile，拖拽/Auto Layout 进入 typed ChangeSet/Undo；Tactics Tooling 进入中央 Main Screen，Graph 与 SubViewport 使用可调 64/36 左右分栏。
- [x] Compatibility/Forward+ headless 均通过。许可证技术审计确认当前 Godot Projectile/Impact 是项目自有程序化占位，不复制 Piloto 纹理/材质/Prefab；Piloto 购买/EULA 证据仍未找到，因此未来迁移真实第三方视觉前必须另行取证。
- [x] Phase 3 人工闸门前统一验证通过：Core 31、Application 10、Unity Oracle 9、迁移 Python 44、Agent policy 8、GdUnit 5、OKF 14；ResourceSaver 双生成、UID scan、Release 依赖隔离、Compatibility/Forward+、Runtime/Tween/Scope 与 6 scopes/0 unmapped 全部通过。
- [x] Phase 3 编辑器可用性修复后的统一门禁通过：Core 31、Application 13、Unity Oracle 9、迁移 Python 44、Agent policy 8、GdUnit 5、OKF 14；ResourceSaver 双生成、真实坐标、UID scan、Release 隔离、Compatibility/Forward+、Runtime/Tween/Scope、EditorPlugin headless 生命周期与 patch whitespace 全部通过。
- [x] 完整重启 canonical Editor 后，人工验证中央 `Tactics Tooling` Main Screen、真实 6 节点/4 edge 两行布局与语义标题、Toggle 后布局/ScrollOffset 不跳动、拖拽/Auto Layout → Ctrl+Z/Ctrl+Y、64/36 左右可调 SubViewport、保存、Assembly Reload 和 Runtime；当前程序化视觉占位已接受。Preview 通过居中的 `AspectRatioContainer(Fit)` 保持 `640:180`，人工确认分栏缩放时网格与圆形不再变形。
- [x] 等比 Preview 修复后的完整门禁通过：Core 31、Application 13、Unity Oracle 9、GdUnit 6；ResourceSaver 双生成、生产 Debug 恢复、Release 隔离、Compatibility/Forward+、Runtime/Tween/Scope、headless Main Screen、Incident/Skill/OKF 与 patch whitespace 全部通过。
- [x] 根据已完成的人工视觉证据，real batch 已从 `Generated/UnityOwned` 晋升为 `Validated/UnityOwned`；该状态仅覆盖项目自有程序化占位，不等于 Piloto 视觉等价，也不把 Presentation/Skill 整类切换为 GodotOwned。
- [x] 状态晋升后的 Phase 3 closure 门禁通过：Core 31、Application 13、Unity Oracle 9、迁移 Python 58、Agent policy 8、GdUnit 6、OKF 14；ResourceSaver 双生成、Compatibility/Forward+、Runtime/Tween/Scope、Release 隔离、headless Main Screen 与 patch whitespace 全部通过。

## Phase 4–5

按 Unit → Buff/Item → Skill → AI/Encounter → Pure Run/Persistence → Scene/UI/Input → Presentation/VFX/Audio 批量迁移；最终完成新开档到继续游戏闭环，并在发布阶段首次执行 Godot Windows Release/PCK Smoke。

- [ ] 当前仍保留子计划 [Godot Phase 4：Pure Run Unit 批次迁移](2026-08-10-godot-phase4-unit-batch-migration.md)：12 Unit 的定义、基础视觉、Catalog、Factory、Gallery/Spawn 与自动门禁已完成，只剩人工验收；不进入 Skill、AI/Encounter、Persistence 或后续 Profile。
- [ ] Phase 4 自动实施已在 `2b341cb3` checkpoint，仍等待 Unit Gallery/Spawn/Reload 人工视觉闸门，不因后续无视觉批次而晋升。
- [x] Phase 5A Buff/Item 已收口：冻结 14 Buff/3 Consumable/12 Equipment，完成 `status-runtime-v1`、`battle-transition-v3`、Golden schema v7、28 个新 Resource、29 项分批 Catalog 与 47 项 canonical Catalog；双生成、UID、Compatibility/Forward+、GdUnit 与 receipt 门禁通过后达到 `Validated/UnityOwned`，且没有视觉 payload。完成计划已从 active plan 目录删除，由 Git 历史保留。

## 自动与人工门禁

Agent 自动执行代码、转换器、台账、NUnit/GdUnit/headless、Skill/Incident/OKF 验证；godot-ai 按阶段白名单自动执行 Editor 状态、Resource/Scene 读取、运行、日志、截图和重复结构编辑。Editor Assembly Reload/Undo、Poison Spear SubViewport 和每批次视觉验收暂停等待人工确认。禁止新增 worktree、第二个 Godot 项目和 Unity Windows Standalone。
