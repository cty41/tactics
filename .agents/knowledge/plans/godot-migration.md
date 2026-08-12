---
type: Game System
resource: https://github.com/cty41/tactics
title: Godot migration implementation
description: Unity frozen Oracle to Godot migration boundaries, parity closure, content compilation and batch ownership.
tags: [migration, godot, core, parity, testing]
timestamp: "2026-08-12T16:33:29+08:00"
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
verified_revision: 2b341cb3
source_fingerprint: sha256:5a6dba49656607fd8b36121e8ea4cb0df03f29ee67d50af10cf18ed74eb69925
---

# Current state

Unity `w1` 与 `unity-final-2026-08-08` 是只读 Oracle；唯一 Godot 项目为 `godot/project.godot`。`d092a955` 定性为技术 Spike：C#、GraphEdit、Undo、SubViewport、ResourceSaver、GdUnit4Net 和 headless 可运行，但没有证明 Unity 行为或真实资产等价。

Core 已移至 `src/Tactics.Core`，不再由 Unity `Assets` 反向编译；未接入冻结运行时的临时 Unity Adapter 已移除。`src/Tactics.Application` 已建立纯 .NET `ContentDraft → ContentCompiler/Diagnostics → ContentSnapshot` 边界。Godot Catalog 记录严格小写 ContentId、ResourceType、UID、诊断路径和 SchemaVersion；Godot Resource 只保留在 Adapter registry，不进入 Snapshot。

Phase 1A 已建立不可变 `BattleState/BattleUnitState`、typed `BattleCommand/BattleEvent/BattleTransition` 与稳定 `SplitMix64 v1` RNG。Phase 1B 将 Golden 升级为 schema v4，区分单位定义 `ContentId` 与运行时 `UnitInstanceId`，允许同一定义的多个战斗实例；命令、事件、状态键和回合顺序均使用实例 ID。

冻结 Unity 的 Dijkstra、Heap 与 `BattleInitiativeService` 通过独立 `Tactics.UnityOracle.Tests` 作为 linked source 原样编译；Phase 3 又把 Amazon 投矛、Ability Mana 和 Poison Buff 的冻结源码加入 blob 绑定。当前 Oracle Matrix 共绑定 15 个最终 Tag blob；Core 路径、先攻及 Poison Spear 的 Lv1 damage、Mana、持矛/掉落、Buff duration/tick/AddDuration 均有真实 AssetDatabase export 与冻结源码交叉证据。该测试层不引用 UnityEngine，也不进入 Core/Application/Godot Release。

Phase 1B 完整统一门禁已通过，覆盖 locked restore、单节点 solution build、Core/Application/Unity Oracle NUnit、Python、Skill/Incident、隔离 GdUnit、生产 Debug 恢复、Release 测试依赖排除、Poison Spear runtime/presentation、EditorPlugin headless 与 OKF。该结论关闭路径和初始先攻 tie-break 缺口。

Phase 1C 将冻结 `IBattleRuntimeScope/BattleRuntimeScope` 与 `PresentationExecutionPlanCompiler` 加入 linked-source Oracle，并将 Golden 升级为 schema v5。`InitiativeRoundState` 只重排当前轮 remaining，`BattleState.WithInitiativeChanged` 保留当前/已行动前缀；RuntimeScope 的 ownership、fault observation、re-entrant dispose 和 timeout callback 边界由冻结/Core 双实现测试；Presentation branch 在 Join 前停止且 continuation 只追加一次。完整统一门禁通过：Core 26、Application 3、Unity Oracle 8、迁移工具 16、Agent policy 8、OKF 14、GdUnit 4；Release 隔离、Poison Spear runtime/presentation、EditorPlugin headless 生命周期、生产 Debug 恢复与 6 scopes/0 unmapped 均通过。

Phase 1D 用版本化决策而不是伪造 parity 关闭剩余合同：冻结 Unity 没有统一不可变 `BattleCommand → BattleTransition`，其技能执行由 Controller/Executor 直接产生副作用；随机源同时存在 `UnityEngine.Random`、无种子 `System.Random` 与 `Guid.NewGuid` 排序。Battle Transition 是迁移合同，`splitmix64-v1` 是确定性替代合同；Phase 3 加入真实 Poison Spear 语义后升级为 v2，Phase 5A 加入 Status/Consumable 后升级为 `battle-transition-v3`，Phase 5B 的统一技能入口升级为 `battle-transition-v4`。

Phase 1A 的自动完整门禁及随后 canonical Godot Editor 人工 reopen/reload 闸门均已通过；EditorPlugin、Dock、3 节点 Presentation、GraphEdit/SubViewport 和 godot-ai 连接正常，未复现已记录的 C# Assembly Reload 重复类型故障。

Poison Spear 现在分成两条不会混淆的台账：旧 `poison-spear.json` 是 `Generated/UnityOwned` 技术 Spike；新 `poison-spear-lv1-real.json` 已达到 `Validated/UnityOwned`。`unity-assetdatabase-v1` 从最终 Tag 对应的 7 个真实资产导出 25 个对象、24761 个 SerializedObject 字段；一次性 typed Draft 经 Application 编译为 6 个内容条目，再由 ResourceSaver 生成 7 个 Godot Resource/Scene/Catalog。最终资产显式序列化迁移语义，不依赖 C# 默认值；连续生成、UID、hash、冲突保护、失败回滚和 receipt 均受测。当前项目自有程序化占位的人工视觉验收已通过；未来若迁入真实 Piloto 视觉，购买/EULA 证据仍必须另行补齐。

Application 使用固定内容类型/Schema catalog，未知类型和超前版本 fail-fast；真实 Poison Spear 目标依赖图包含 Skill → Presentation/Buff、Presentation → Projectile/Impact。ResourceSaver converter 与 `Tools/migration/staging.py` 共同覆盖 dry-run、语义无变化、UID 保留/漂移拒绝、目标人工修改保护、失败回滚、原子台账和重复执行幂等。

Phase 3 Editor authoring 坐标由 Unity AssetDatabase DTO 一次迁入最终 Godot `AuthoringNodePositions`；纯 Application 坐标进入 normalized Revision，拖拽和确定性 Auto Layout 使用 typed ChangeSet。GraphEdit 按稳定 ID 增量 reconcile，显示语义标题并在 Tooltip 保留完整 ID，状态切换不再重建节点或丢失位置/ScrollOffset；保存路径通过 `ResourceSaver` 后恢复既有 UID，并同时覆盖成功 UID 保留与失败 byte rollback。Tactics Tooling 使用 Godot 官方 Main Screen Plugin 进入中央工作区，Graph 与 SubViewport 采用 child stretch ratio 驱动、可折叠且可调的 64/36 左右分栏，不再占用 Output 底部区域；Preview 由居中的 `AspectRatioContainer(Fit)` 保持 `640:180` 逻辑画布比例。canonical Editor 完整重启后的 Main Screen、6 节点/4 edge、Undo/Redo、Save、Assembly Reload、Runtime 与等比 Preview 人工验收均已通过。

状态晋升后的 Phase 3 closure 统一门禁通过：Core NUnit 31、Application NUnit 13、冻结 Unity Oracle 9、迁移 Python 58、Agent policy 8、GdUnit 6、OKF 14；ResourceSaver 连续两次生成 byte-identical，真实两行坐标进入 Resource，Compatibility/Forward+、Runtime/Tween/Scope、Release 测试依赖隔离和 EditorPlugin headless enter/exit 均通过。real batch 已晋升为 `Validated/UnityOwned`；该状态只覆盖当前项目自有程序化占位，不得把 Presentation/Skill 整类直接切换为 GodotOwned。

Phase 4 Unit 已完成并通过人工视觉闸门：冻结 Golden 与 Unity AssetDatabase export 覆盖 12 个 Pure Run Unit、12 个 Prefab audit root、19 个项目自有 PNG payload 和 6 个只审计 Material root；29 个运行时依赖继续 deferred，第三方与 Unity Material/Shader payload 未复制。项目自有 `GoatBodyTint` 算法等价移植为共享 Godot CanvasItem shader，每只山羊 Resource 保存独立 BodyTint/BaseBodyColor 参数；普通单位继续使用 multiply。Core/Application 新增 engine-neutral Unit Definition、派生数值规则和编译器，运行时实例继续使用独立 `UnitInstanceId`。事务管线先复制 19 个 PNG，再由 ResourceSaver 生成 12 个 Unit Resource、共享 Unit Actor、精确 13 项 Catalog、4×3 Gallery 与可视 10×10 SpawnFixture，共 16 个 ledger artifact。方向契约为 South=DR、North=UL、East=UL+Body 水平镜像、West=DR+Body 水平镜像；死亡图不继承镜像，Shadow 永不镜像，召唤物缺少死亡图时安全回退。`unity-unit-sprite-geometry-v1` 另行冻结 living pivot `(0.5,0.078125)`、death pivot `(0.5,0.5)`、Body/Shadow 128/64 PPU 和 Shadow `localY=-0.03`、scale `0.8`、alpha `0.9`；Godot Resource 显式保存 Body offsets `(0,-108)/(0,0)` 与 Shadow offset `(0,3.84)`、scale `1.6`、alpha `0.9`。Unit 预览采用原生 1600×900 逻辑画布与同尺寸 override，Gallery 使用 `ground-baseline-native-1600x900-v2`；Spawn 固定出生格收在1..8，并验证8px网格外框与24px viewport安全区。Gallery 四向、死亡/Reset、Goat tint、比例/Shadow、resize、Spawn 外框与 Reload/Output 人验通过；batch/receipt 已晋升为 `Validated/UnityOwned + passed_for_migrated_project_owned_unit_visuals`。

Phase 4 二次视觉修复后自动门禁通过：Core 35、Application 19、冻结 Unity Oracle 11、迁移 Python 86、Agent policy 8、GdUnit 11、OKF 14；Debug/Release 零警告零错误，Unity export 两次独立运行、PNG 事务复制和 ResourceSaver 连续两次生成均 byte-identical，headless Editor UID/import 扫描、Compatibility/Forward+ Catalog/Factory/Fixture 运行和程序化 Gallery/10×10 Spawn 截图均通过。程序化截图由 Godot Image 使用已导入纹理、`goat-body-mask-v1` CPU 参考和 Sprite pivot/Shadow 几何换算确定性合成；共享 goat 图像在缩放前复制，避免后续实例逐个缩小，并有源纹理尺寸不变测试。截图只证明资源组合、位置与可检查性，不替代用户对 tint、体量、朝向、死亡图与 Shadow 的视觉接受。

Phase 5A Buff/Item 已完成为独立 `pure-run-buffs-items-v1` batch：Unity 6000.3.11f1 通过 AssetDatabase/SerializedObject 导出 14 个 Buff 根资产，两次独立 batchmode 输出 SHA-256 均为 `c01194701068cf8447063ff8ac26d787c2f99ded4f1442f647a93ad4fb8d0ad8`；3 个 Consumable 与 12 个 Equipment 直接绑定最终 Tag 的 JSON Git blob 和工作树 SHA-256。typed converter 严格核对字段、枚举、GUID、依赖 hash、ContentId/SourceId、Ice Armor Lv2 → Slow 引用与唯一的外部 `buff.poison` 依赖。ResourceSaver 生成 13 个新 Buff、3 个 Consumable、12 个 Equipment 与 29 项分批 Catalog；29 个批次独占 ledger artifact、UID、目标/语义 hash 和连续两次 byte-identical 生成受测。Phase 5B 加入后 canonical Catalog 为 58 个唯一 ContentId，作为跨批次组合输出运行验证而不由单批 ledger 独占。3 个 Buff icon 只审计不复制；该批保持 `Validated/UnityOwned`。

Phase 5B 已冻结并生成 12 项起始技能合同：Mage、Necromancer、Amazon 各 3 项，公共 Magic/Melee Attack 与隐藏 Pickup Spear；`skill.poison-spear.lv1` 继续引用原 Poison Spear batch。通用技能运行时处理 Mana 原子性、确定性命中/闪避/暴击、直线首目标、状态、Summon Skeleton、Pickup Spear 与 Combat Techniques Lv1，并通过兼容入口保持 Poison Spear 语义。ResourceSaver 生成 11 个新 Skill Resource、12 项 Catalog、1600×900 `SkillFixture`，canonical Catalog 达到 58 项；13 个批次独占 artifact 两轮 byte-identical，Compatibility/Forward+ 与 GdUnit 通过。用户已完成 Fixture、操作与 Assembly Reload 人工 smoke，状态晋升为 `Validated/UnityOwned`；正式 VFX、AI、Persistence、升级 UI/Input 不在本批范围。

Phase 6A 的初次 Fixture 已验收，但 Phase 7A 可玩联调暴露了 parity 缺陷：旧 typed draft 只保存 archetype 与四个权重，Godot AI 也只在当前格无合法攻击时生成移动。收口修复把共享 `BasicMeleeGraph` 作为第 21 个 Unity AssetDatabase 根，两次导出 byte-identical，并冻结 13 个 Intent/Rule/Score 节点与 12 条边；Core 现在共同生成当前格攻击、Engage、移动后技能、FinishOff、Retreat 与 Hold 候选，一个 Turn Plan 可经 canonical transition 执行 Move→Skill→EndTurn。六类 AI、4 Skill、2 Layout、3 Encounter 与 canonical 73 项身份不变；自动 parity 重验通过后保持 `Validated/UnityOwned`，可视逐步行为仍由 Phase 7A 人工闸门确认。

Phase 6B 已完成三战 Pure Run 持久化垂直切片：冻结 Unity v5 的会话、结算、恢复与摘要语义，Core/Application 实现 N1→N2→N3、战前 checkpoint、确定性奖励/恢复/死亡卸装、成长待办、稳定事务去重和终局摘要。Godot Adapter 提供版本化 canonical JSON 单槽存档，正式路径为 `user://pure-run/save-v1.json`，写入经 temp 重读校验、有效 backup 保留、损坏证据 quarantine 和 revision 并发保护；PendingBattle 跨进程从战前快照重开，不序列化逐回合 BattleState。ResourceSaver 新增 1 个 Run Resource、Catalog 与自动诊断 Fixture，canonical Catalog 达到 74 项；该批无视觉载荷，由 Compatibility/Forward+、故障恢复与全量回归自动验收后晋升为 `Validated/UnityOwned`。完整七层地图、Rest/Store/Mystery、N4–N6、Boss、成长消费和正式 Run UI/Input 仍不在本批范围。

Phase 7A 已进入合同冻结 checkpoint：`pure-run-ui-input-v1` 绑定最终 Unity Home、Battle、Settlement、Summary UXML 与 Input Actions。五个根资产均采用 `audit-only-file`，只记录 GUID、LocalFileId、AssetDatabase dependency hash、原文件 SHA-256 和字节数，不遍历 Unity 导入对象、不复制 UI Toolkit/Input payload。两次独立 Unity 6000.3.11f1 batch 输出 byte-identical；batch 当前为 `Exported/UnityOwned`，可玩 Battle Session、Godot Scene/UI/Input 以及人工 UI/Input 闸门尚未完成。

Phase 7A 的 Application checkpoint 已加入 `PlayableBattleSessionFactory/Service`：由 `EncounterRequest`、Encounter/Layout 与 Unit/Skill/AI Catalog 组合确定性 BattleState；玩家 UI intent 只经 `BattleTransitionService`，敌方只经 `AiDecisionService/AiTurnService`，并输出只读棋盘、技能、合法移动/目标、状态、尸体、投矛与事件快照。AI 自动推进有 64-command guard，胜负只生成一个经过 Run/revision/Encounter 绑定的 `PureRunBattleResult`。死亡活动单位现在只允许 canonical EndTurn 跳过，仍禁止其移动或施法。Godot Scene/UI/Input 生成尚未开始。

Phase 7A 已生成并人工验收原生 1600×900 可玩 Main：普通可产尸体单位首次死亡原子写入 `BattleState.Corpses`，Summon Skeleton 消费同一状态；Amazon 显示 Held/Dropped/Pickup；AI Decision/Move/Skill/EndTurn 通过可暂停、单步、1×/2× frame 展示。Battle UI 渲染 canonical legal move/target、路径、AOE、尸体与掉矛高亮，并提供 Turn Order、悬停详情及可筛选结构化日志。Home、Settlement、Summary、Run/Persistence 与 canonical 74 项身份不变，正式 VFX/Audio 仍后移；batch 为 `Validated/UnityOwned + manual_ui_input_qa_passed`。

Phase 7A 第二轮收口将 Unity 的基础技能回合限制显式迁入 `SkillDefinition/BattleUnitState`：每项基础攻击每回合只成功一次，非基础技能仅在冻结 `MaxUsesPerTurn>0` 时计数，失败不消耗并在该单位下次回合清零。Godot 可玩切片通过非 Catalog 的 `godot-playable-lv1-balance-v1` Resource 覆盖 Lv1 Mana/伤害和玩家/召唤物基础攻击，冻结 Phase 5 资源值保持不变；结束自身回合按 Intelligence 恢复 MP。召唤骷髅动态获得近战基础攻击，尸体消费后死亡 Sprite、标记和数值条一起移除；AI BasicAttack 接入冻结 `TargetHealth` score，日志保留目标类型、ID 与分项评分。所有可见单位显示即时 HP/MP 调试条。

Phase 7A 的技能目标预览分离 `RangeCells`、canonical `LegalTargets` 与只读 `ImpactPreview`：选择技能时即使当前无合法敌人也显示弱色几何射程，可执行目标使用强色；悬停后由同一次 `BattleTransitionService` 探测提供首个命中、受影响单位与拒绝原因。Fireball Lv1 保持单目标首个命中且无 AOE，Thrust 保持轴向，Summon/Pickup 只暴露尸体或掉矛特殊目标；Godot 不复制伤害、LOS 或目标结算规则。用户已完成复验。

Phase 7B 已完成自动实现 checkpoint，等待 Inventory/成长人工闸门。18 条玩家职业分支的 Lv1/Lv2 合同已冻结；通用 `SkillDefinition` 显式保存 branch、前置、成长可见性、所需属性/门槛及规范化执行参数。Skill Runtime 支持 Lv2 十字范围、分类召唤上限、尸体召唤、Teleport/Decoy 位移、Multi Stab 有序段数、Recover Spear 邻近电击、Ice Armor、Bone Shield 伤害吸收及 Combat Techniques 等级。Run 角色显式保存技能分支/等级，Inventory 与成长通过 revision-checked 原子服务执行装备、替换、卸下、携带、属性和技能选择；V2 单槽存档兼容读取 V1 并在下次写入升级。ResourceSaver 已生成 27 个新增 Skill Resource 和 27 项批次 Catalog，canonical Catalog 达 101 项；Inventory/Progression 功能占位页已接入 Home/Settlement，成长未消费时禁止进入下一战。冻结 Unity Skill Resource 仍保持来源值，Godot playable balance 仅覆盖既有 Lv1 切片；batch 保持 `Generated/UnityOwned + manual_inventory_progression_qa_pending`。

Phase 7C 自动实现 checkpoint 已完成，等待与 Phase 7B 合并人工闸门。Unity 七层图合同只授权到 Layer 4：N1→N3 胜利且成长消费后进入 battle/rest/store/mystery 四选一，选择后锁定其余路线；节点以可恢复的 Selected/Pending/Resolved/Committed 生命周期执行，完成后停在 `ReadyForLayerFive`，N5/N6/Elite/Boss 与 Lv3 后移。N4 使用标准 BattleResult 校验、队伍同步、恢复、奖励、掉落和失败摘要；Rest 原子恢复存活角色 30% HP/MP；Store 保存固定 3 件库存、稳定实例与购买状态；Mystery 保存角色、成功率、roll、结果及待带入下一战的状态。Save V3 可读 V1/V2 并保存全部 Layer 4 事务；Godot 页面只提交 Application intent。ResourceSaver 生成 7 个 Resource，canonical Catalog 保持 108 项；batch 为 `Generated/UnityOwned + manual_inventory_progression_and_layer4_qa_pending`。

Phase 7D 自动实现 checkpoint 已完成，等待与 Phase 7B/7C 合并人工闸门。冻结合同覆盖 N5/N6/E1/E2/Special、Elite/Special 倍率与七层终局；正式 Run 从 Layer 4 继续进入稳定选择的 Layer 5 Elite、Layer 6 battle/rest/store/mystery 四选一和 Layer 7 Special Boss。所有战斗继续复用统一 BattleResult 校验、恢复、奖励、掉落与幂等事务；Elite 使用 1.3 HP/1.15 output，Special 使用 1.8 HP/1.25 output，Boss 胜利产生 `BossVictory` 而不追加 Lv3 成长。Save V4 可读 V1–V3 并保存晚期 checkpoint、节点状态和 Boss summary。ResourceSaver 新增 5 个 Encounter 和 `special-open` Layout，canonical Catalog 为 114；N5/N6 只冻结与生成诊断内容，不进入正式地图。batch 保持 `Generated/UnityOwned + manual_inventory_progression_full_run_qa_pending`。

GdUnit AI Fixture 曾在 Runtime Runner 中通过 UID locator 随机加载不到不同技能并以 `-1073741795` 退出；Catalog UID/path 校验本身正常。Fixture 现在先验证 Catalog，再使用已验证的 `DiagnosticPathValue + CacheMode.Ignore` 加载 Skill/AI/Layout/Encounter，缺失或 UID 漂移仍立即失败。隔离 GdUnit 连续两轮 30/30、随后统一迁移门禁全绿。

Pure Run schema 仍为 v1，但 `UnitAttributes` 现在由显式 JSON converter 稳定读写六项字段。已存在的固定三人全零属性存档在身份匹配时从 Run Definition 修复，并在下一次合法事务写回；部分损坏或身份不匹配拒绝。胜利结算对本场阵亡角色也应用 `Constitution×2` HP 与 `Charisma` MP 恢复，恢复后可进入下一场，死亡卸装仍保留；失败不复活。Battle/Settlement 明示 N1/N2/N3，只有结算 Continue 可开始下一场，并以一次性提交与导航日志阻止旧 Timer、重复 BattleResult 或双击创建重复战斗。

`AiEncounterFixture` 的单步与整轮可观测性已改为真实结构化结果：Space 精确执行一个 AI actor，Enter 执行当前轮剩余全部 actor，并累计 actor、intent、skill、candidate、pattern cursor、events 与 state fingerprint。自动测试固定 N1/N2/N3 为 3/3/4 turns、两个 Elite 为 1 turn，验证整轮推进、64-command guard、Reset 重放及 Elite 单步/整轮等价；Compatibility/Forward+ 均实际执行单步和 N1 整轮，GdUnit 增至 26。人工闸门因此只保留 1600×900 可读性、操作响应与 Reload/Output smoke。

Phase 5A Core/Application checkpoint 已实现 `status-runtime-v1`、`battle-transition-v3` 与 Golden schema v7。`BattleStatusState` 捕获 polarity/effect/trigger/refresh、stack、速度与减伤/反击参数；`BattleUnitState` 捕获基础速度、携带 Consumable 和每轮成功使用记录。Poison/Burning 按 ContentId 顺序 tick，非 Burning 回合末递减、Burning 按 stack 消耗，Frozen/Stun 只阻止非 EndTurn，Slow 从基础速度重算 MoveRange/Initiative，同 curse category 后应用替换。Consumable 只允许存活的自身或曼哈顿距离 1 友军，合法零恢复仍消耗 charge，非法目标不消耗，净化只移除 Harmful，每单位每轮只成功使用一次。Equipment 唯一 slot 后投影六项属性并复用 `unity-unit-derived-v1`。Mark、伤害倍率、Counter、Ice Armor retaliation 与 Fear 仅输出强类型 policy，不越界接入 Skill/AI。

## Verification model

`Tools/migration/Verify-GodotMigration.ps1` 串行执行 locked restore、单节点 build、Core/Application NUnit、Python、Skill/Incident lint、隔离的 GdUnit test host、Release build、Godot runtime/editor headless 与 OKF。GdUnit 3.1.1 的 Runtime Runner 要求 C# runner 位于 `project.godot` 主程序集，因此 test host 使用相同程序集名，但测试源码、`obj`、lock 和包与生产 csproj 分离；Release 明确排除。

## Next gates

1. Phase 4 Unit 与 Phase 5A Buff/Item 均已完成并删除各自 active plan；结果由代码、设计、manifest、测试、OKF 与 Git 历史保存。
2. 项目 MCP Profile 已按 Phase 7A 切换为 `ui-input`，统一入口校验 27 个阶段白名单工具。
3. Phase 6B 已关闭；Phase 6A 自动 parity 已按完整图合同重验。Phase 7B–7D 的自动实现和统一门禁已完成，下一闸门是 Inventory/成长、Layer 4、Elite、Layer 6 与 Boss 的合并人工复验。完整 Presentation/VFX/Audio 仍属于 Phase 8。

Windows/Steam 仍是产品目标；Unity Windows Standalone 不执行，Godot Windows Release/PCK Smoke 延后到发布阶段。
