# Tactics Unity → Godot 迁移设计

状态：设计已确认；`d092a955` 是技术 Spike。当前真实 Poison Spear Lv1、Phase 4 Pure Run Unit 与无视觉载荷的 Phase 5A Buff/Item batch 均已达到 `Validated/UnityOwned`。Poison Spear 的 Revision/typed ChangeSet/Undo/保存回滚、自动等价性门禁以及 canonical Editor 中的 Graph/Undo/Reload/Runtime/等比 Preview 人工验收均已收口。Unity authoring 坐标由 DTO 一次迁移到最终 Godot Resource，后续位置拖动、自动布局、保存和 Reload 均由 Godot typed ChangeSet 管理。当前 VFX 是已通过人工验收、且不复制未确认 Piloto 资产的项目自有程序化占位；Unit 晋升只覆盖本批迁入的项目自有 PNG 与 Goat shader 等价实现，不代表 deferred/第三方视觉已迁移，也不把内容类别切换为 `GodotOwned`。

日期：2026-08-07

## 目标与范围

本迁移面向 Windows/Steam 首发，使用 Godot 4.7.1 .NET 编辑器和 .NET 9 SDK。第一阶段不接入 Steam SDK、Steam Cloud、DLC、Mod 或运行时下载；Godot 源码构建只作为官方版本无法规避问题时的诊断 fallback。

目标不是在 Godot 中逐类复刻 Unity API，而是将项目重组为：

```text
Pure .NET 9 Core
    ├── 玩法规则、AI、寻路、技能执行、存档 DTO、确定性 RNG
    └── 不引用 Unity 或 Godot
            ↓
Godot Runtime Adapter
    ├── Resource → Pure Draft → ContentSnapshot
    ├── Scene、UI、Input、Audio、Presentation
    └── Windows Release
```

迁移完成必须满足：

- 生产玩法所需 Core 规则和最终 Godot Resource 全部迁移；
- TBSFramework、Odin、TilemapSet 等 Unity 第三方依赖不进入最终 Godot 工程；
- ContentSnapshot、存档、图编辑器、输入、UI、表现和 Windows 导出通过自动门禁；
- 实际 Windows Release 能从主 PCK 加载全部必要内容，进入 Pure Run 并完成代表性战斗；
- Poison Spear 垂直切片通过运行时与 Editor SubViewport 双重验证；
- C# EditorPlugin Reload、多态 Resource 往返和 Godot C# 测试链路通过既定 Spike 或启用已定义 fallback；
- 迁移期不夹带玩法规则重设计，已有语义先保持版本化一致。

当前 Unity `main` 基线已增加固定 `10×10` 局部棋盘契约（`BattleBoardSpec`、`Test1_10x10_Probe`、`Probe10x10Encounter/Party`）。Godot 首个战斗 fixture 应以 `(0…9, 0…9)` 局部坐标和该探针的布局契约为输入，不再把旧 `18×18` 坐标作为迁移目标；探针是否推广为 Unity 正式 `Test1` 仍是 Unity 侧独立审批，不在本设计中默认替换。

## 仓库、分支与代码边界

长期分支固定为 `migration/godot`，复用现有 worktree。Unity `w1` 与 `unity-final-2026-08-08` 已冻结为只读 Oracle；Godot 专属工程、Resource、场景和 Adapter 不反向合并到 Unity 主线。

冻结后不再需要 Unity 对共享 Core 的源码管理。Core 已移至 `src/Tactics.Core`，Application 位于 `src/Tactics.Application`；迁移 worktree 中的 Unity 工程只作为 AssetDatabase 导出宿主。早期未接入冻结运行时的临时 Unity Adapter 已移除，避免制造虚假的双引擎共用状态。

最终代码层次为：

```text
Tactics.Core
Tactics.Application
Tactics.Godot.Adapter
Godot Editor / Content
```

`Tactics.Core` 不引用 `UnityEngine` 或 Godot。Godot 最终内容直接使用 `Resource`/`PackedScene`；迁移 DTO 只存在于具体导出/导入步骤，不成为长期资产格式。

## 内容真相源、身份与迁移批次

### 所有权与处理状态

每类内容（Unit、Skill、Buff、AI、Encounter、Presentation 等）独立迁移。内容所有权与迁移处理进度是两条正交状态，不得混用：

```text
UnityOwned
    ↓ 按 Unity commit 冻结
FreezePending
    ↓ 导出、转换、验证、修复引用
GodotOwned
```

```text
Planned → Exported → Generated → Validated
```

- `UnityOwned`：Unity 资产是真相源，Godot 产物仅供试验；
- `FreezePending`：该类别停止 Unity 编辑，绑定明确的源 commit 并开始验收；
- `GodotOwned`：Godot 资产成为唯一真相源，禁止再从 Unity 覆盖；
- `Planned/Exported/Generated/Validated`：只表示该批次加工与验证进度，不自动改变所有权；
- 失败时保持原状态，不允许一半 Unity、一半 Godot 的混合生产状态；
- 不建立长期双向同步。

### 资产身份

```text
Unity GUID + LocalFileId
          ↓
       ContentId
          ↓
Godot Resource path + uid://
```

- `ContentId` 是项目稳定的业务身份，用于存档、运行时查询、跨版本兼容和内容语义引用；
- `UnitInstanceId` 是单场战斗、重放或存档中的运行时实体身份；多个实例可以共享同一个单位定义 `ContentId`，二者禁止混用；
- Unity GUID/LocalFileId 只负责迁移期定位来源；
- Godot `uid://` 负责 Godot 工程内 Resource 的物理身份和路径移动后的引用稳定性；
- 重命名或移动保留 `ContentId`，复制成新内容必须生成新 ID；
- 存档和 Core 保存业务 `ContentId` 与需要持久化的 `UnitInstanceId`，不保存 `uid://` 或 `res://` 路径；
- 删除先检查反向引用和存档迁移策略，不自动级联删除；
- 迁移完成后可以删除 Unity 来源映射，但保留最终 `ContentId`。

### 直接生成最终 Godot 资产

每类 Unity 资产通过专用适配器直接生成最终格式：

```text
Unity AssetDatabase
    ↓
临时、可丢弃 DTO
    ↓
类别转换器
    ↓
Godot Resource / PackedScene
```

DTO、迁移缓存和报告不进入最终发行内容。`.tres/.tscn` 必须通过 Godot Resource API、`ResourceSaver` 和编辑器工具生成，不手写文本格式。转换器必须支持 dry-run、重复执行和语义 Diff；相同输入的重复转换必须保持语义一致。

### 批次顺序

先以 Poison Spear 做端到端垂直切片，验证身份、Resource、ContentSnapshot、Graph、运行时、PCK、测试和视觉闭环；其战斗 fixture 优先使用当前 `10×10` 局部坐标探针。垂直切片阶段的资源仍属于 `UnityOwned`，不提前改变正式所有权。

垂直切片通过后，按依赖关系批量迁移：

```text
迁移基础设施与身份台账
→ Core 基础定义
→ Unit / Buff / Item
→ Skill / AI / Encounter
→ Scene / UI / Input
→ Presentation / VFX / Audio
→ PCK、清理与最终切换
```

### 迁移台账

台账是临时审计和重试数据，不是中立资产格式。按批次保存，并生成确定性总索引：

```text
migration/
├── batches/
│   ├── poison-spear.json
│   ├── units.json
│   └── encounters.json
└── generated/
    └── migration-index.json
```

台账至少记录源 GUID/LocalFileId、`ContentId`、Godot 路径和 UID、批次、所有权状态、源 hash、转换器版本、目标语义 hash、引用诊断和验证结果。迁移完成后整个临时目录可删除。

### 冲突、失败与重试

导入前比较源 hash、上次生成的目标 hash、转换器版本和目标当前语义 hash。目标已被人工修改时默认严格停止；只有显式 `--force` 才允许覆盖，并且必须记录原因。

每个批次先写临时目录，转换、引用检查、Snapshot 编译和测试全部通过后，才原子提交目标 Resource 与台账。失败时清理临时输出、不更新正式台账、不改变所有权状态。

进入 `FreezePending` 时记录 Unity 源 commit、批次范围和 hash；完成自动验证、导出包 Smoke Test、视觉验收后，才切换为 `GodotOwned`。

## 第三方资产边界

- Odin 只属于 Unity 编辑器能力，不迁移其配置和运行时依赖；
- TBSFramework 和 TilemapSet 资产默认不进入 Godot；
- 先做引用审计，只有确认属于项目自有且仍有实际价值的文件才迁移；
- `pure_run_tile_warm_gray`、`pure_run_tile_cool_gray` 等项目生成内容在 Godot 中直接重建最终资产，不携带 TBSFramework 依赖；
- Piloto 等仍被使用的第三方视觉内容采用语义重建和最小原料保留，原始素材必须通过许可证闸门；
- DOTween 的运行时表现语义改用 Godot 原生 Tween，编辑器可逆预览使用确定性的采样器；
- OneLine、Unity UI Extensions 等无实际运行时引用的包在 Unity 清理阶段退役，Godot 不建立替代依赖。

## 运行时内容编译、加载与构建

### 内容数据流

```text
Godot Resource / PackedScene
        ↓
生成并提交的轻量 ContentCatalog
        ↓
Godot Resource Adapter
        ↓
ContentCompiler
        ↓
不可变 ContentSnapshot
        ↓
Pure .NET Core
```

Locator Catalog 记录 `ContentId → ResourceType + uid://`，运行时不扫描内容目录。Core/存档使用 `ContentId` 表达业务内容、使用 `UnitInstanceId` 表达具体战斗实体；UID 只在 Godot 适配层使用。

正式 `.tres/.tscn` 直接进入主 PCK。Godot 原生 `ResourceLoader`、`PackedScene` 和场景作用域替代 Unity AssetBundle、BundleCache、依赖加载顺序和手工引用计数。第一阶段不机械模拟 AssetBundle，也不建设 DLC、Mod 或运行时下载包。

`BattleRuntimeScope` 的取消、任务跟踪、排空和异常可见性是运行时 Adapter 的生命周期契约：每场战斗拥有独立作用域；场景切换、战斗结束或销毁时先取消并等待已跟踪异步任务排空，再释放资源；取消后的 continuation 不得访问已销毁节点；排空期间观察到的非取消异常必须显式暴露。Godot 实现可以更换具体 API，但不能丢失这条所有权边界。

启动时事务性编译全部 Gameplay 内容。单位、技能、Buff、AI、Encounter 或必要场景缺失时 fail-fast，不跳过坏内容继续运行；可选 VFX、音频和非阻塞表现只有在显式标记后才允许 no-op。

## 编辑器资产创作与工具边界

运行时保留自定义 Resource、Graph 数据模型、`NodeTypeId`、端口契约、编译器和内容校验。`EditorPlugin`、`GraphEdit`、Inspector 扩展、Undo、SubViewport、迁移工具和 Problems 面板属于编辑器侧，不进入 Release 包。

人工编辑、批量迁移和 Agent 操作共用 `Graph Mutation Kernel`：

```text
GraphEdit / Inspector
        ↓
Mutation Kernel
        ↓
Undo 或 typed ChangeSet
        ↓
校验、规范化、保存
```

Graph 根资产为独立 `.tres`；图独占节点优先作为派生 C# Resource 子资源；可复用叶资产保存为外部 `.tres`；视觉 Prefab 转为 `PackedScene`。节点复制由领域 Kernel 显式完成，叶资产默认共享，只有显式 Duplicate & Rebind 才复制叶资产。

当前 Unity 基线已经提供 `PresentationExecutionPlan`：Graph 编译为纯数据的 Sequence / Parallel / Leaf 结构，Runtime 和 Preview 使用不同叶执行器消费同一计划。Core 的 `PresentationGraphCompiler` 已由冻结 Unity linked-source Oracle 验证 Fork/Join 结构：每条 branch 在 Join 前停止，Join 后 continuation 只追加一次；Godot 应消费这一计划，不迁移 DOTween、Unity Editor 或玩法状态到计划对象中。

持久化使用稳定 `NodeId`、`PortId` 和端口基数语义 `Single / UnorderedMany / OrderedMany`，不依赖 GraphEdit 的临时整数端口或数组首项。图 Schema 使用统一 `SchemaVersion`、稳定 `NodeTypeId` 和显式逐版本迁移。

### 编辑器技术闸门

大规模编辑器迁移前，必须用 Poison Spear 通过：

```text
C# EditorPlugin
＋ GraphEdit
＋ EditorUndoRedoManager
＋ SubViewport
＋ Assembly Reload
```

至少连续验证多次编译和 Reload 不崩溃、资产不丢失、不重复创建 UI/信号、不泄漏预览对象，且 Reload 后新 Undo/Redo 正常。纯 C# 不稳定时，允许降级为薄 GDScript 外壳加 C# 核心；不改变最终资产格式和运行时架构。

Agent/MCP 只是传输入口，不是领域核心：离线验证、迁移、审计和报告优先走 C# Application Service/CLI；普通场景和资源操作可由通用 Godot MCP 处理；只有依赖当前 Editor、Undo 或 Preview 状态的少量事务操作才需要 typed MCP adapter。当前 Unity `PresentationGraphMcpTools`/`PresentationAuthoringFacade` 的 list/get/validate/apply/preview、规范化 revision、`expectedRevision`、typed ChangeSet 和单一 Undo 语义，是 Godot 侧应保留的应用层合同，而不是必须保留的传输实现。具体 `godot-ai` 版本和扩展接口仍需 Spike 锁定，但不作为迁移架构的单点依赖。

## 测试、对照基线与验收

### Unity Windows Standalone 验证规约

迁移阶段不构建或启动 Unity Windows Standalone，也不把 Unity Player Smoke Test 作为 Unity 终版冻结、迁移启动或迁移批次的阻塞门禁。Unity 源快照使用 Editor 编译、定向 EditMode/PlayMode 测试、固定探针场景人工验证、OKF 校验和依赖审计作为验收证据；这次生命周期修复已由 Editor PlayMode 回归测试覆盖。

该规约只约束 Unity → Godot 迁移过程中的中间验证，不改变 Windows/Steam 的产品目标。Godot Windows 导出是否通过最终发布验收，属于 Godot 产品发布阶段的独立决策，不反向要求 Unity 侧构建 Standalone。

### 三层测试

```text
Tactics.Core.Tests
    └─ NUnit / .NET 9

Tactics.Godot.TestHost
    └─ GdUnit4Net 隔离测试宿主，使用项目程序集名注册 Resource、Node、Scene、输入和运行时脚本；生产 Release 不引用测试包

Tactics.Godot.EditorSpike
    └─ EditorPlugin、GraphEdit、Undo、SubViewport、Assembly Reload
```

Unity 测试在最终切换前继续保留；不为保持 Godot 工程整洁而提前删除。修改 Core 时按影响范围运行 Unity、Core 和 Godot 测试，垂直切片完成及合并前执行关键路径双引擎全量验证。

迁移前生成并提交固定 Golden Test Vectors，记录固定种子、棋盘、单位、技能和指令序列，以及合法行动、路径、伤害、状态和事件结果。向量默认只读，更新必须单独审查。

当前 Golden schema v7 已把显式 SplitMix64 状态与 `MoveUnitCommand → UsePoisonSpearCommand → EndTurnCommand` 重放为不可变 `BattleTransition(State + Events)`，并增加 Status refresh/tick/cleanse、Consumable charge/target/round-use 和 Equipment unique-slot projection 合同。单位定义 `ContentId` 与运行时 `UnitInstanceId` 继续严格分离。冻结 Unity 的纯 C# Dijkstra、堆、`BattleInitiativeService`、`BattleRuntimeScope` 和 `PresentationExecutionPlanCompiler` 以 linked source 方式编译到独立测试程序集；Poison Spear 与 Buff/Item 的冻结源码、AssetDatabase DTO 和 JSON blob 提供语义证据。测试程序集不得成为 Core、Godot Adapter 或 Release 依赖。

当前轮先攻以 `InitiativeRoundState` 表达不可变 partition：当前单位与已行动集合保持稳定，只有 remaining 在 Initiative 变化后重新排序；`BattleState.WithInitiativeChanged` 是 Slow/cleanse 等 initiative 变化接入这一合同的显式入口。`StatusRuntimeService` 捕获不可变状态参数，以 ContentId 顺序执行 Poison/Burning tick，按冻结规则覆盖 refresh strategy，并将 Mark、伤害倍率、Counter、Ice Armor retaliation 与 Fear 输出为强类型 policy，不提前依赖尚未迁移的 Skill/AI。Consumable 只处理存活的自身/相邻友军、charge 与每轮成功使用记录；Equipment 先验证唯一 slot、投影六项属性，再复用 `unity-unit-derived-v1`。冻结 Unity 不存在统一不可变 Command/Event 边界，且随机源混用全局/非确定 RNG，因此 `battle-transition-v3` 明确定性为版本化迁移合同，`splitmix64-v1` 明确定性为确定性替代合同；不得把二者宣传为逐语句 Unity parity。

真实内容源管线使用 Unity Editor-only AssetDatabase exporter，不解析 YAML。Poison Spear Lv1 的 7 个根资产已通过 `unity-assetdatabase-v1` 导出 GUID、LocalFileId、最终 Tag blob、dependency hash、对象层级、字段和引用，补入技术 Spike 漏掉的 Poison Buff；两次 DTO byte-identical。临时 DTO 经 Application diagnostics 后只作为 ResourceSaver 的一次性输入，现已生成 Poison、Skill、Presentation、10×10 fixture、Projectile/Impact PackedScene 与 Catalog。目标语义显式序列化，UID、hash、人工修改保护、回滚和 byte idempotency 受测；项目自有程序化 Projectile/Impact 已通过人工视觉验收，因此 real batch 晋升为 `Validated/UnityOwned`。Piloto 纹理、材质、Prefab 或派生视觉 payload 仍需独立的购买/EULA 证据和重新验收，当前晋升不覆盖它们，也不改变类别所有权。

Phase 4 Unit 预览使用原生 1600×900 逻辑画布与同尺寸窗口 override，`canvas_items + keep` 只处理用户后续 resize。Gallery 的已验收 1280 逻辑布局按 1.25 倍迁移到 `ground-baseline-native-1600x900-v2`，同时放大坐标、字体、Control bounds 和 Actor scale，避免逻辑分辨率切换后视觉缩小。SpawnFixture 使用 10×10、`GridOrigin=(440,90)`、72px cell 与 0.375 Actor scale；固定出生格只使用 1..8，完整 Body/Shadow AABB 必须同时位于8px网格外框安全区和24px viewport安全区。Unit 可跨内部格线，但不得跨网格外框。运行场景、程序化截图、ResourceSaver semantic、receipt 与 GdUnit 共用该布局合同。Gallery 四向、死亡/Reset、六种 Goat tint、比例/Shadow、1600×900 resize、Spawn 外框与 Assembly Reload/Output 均通过人工验收，batch 已晋升为 `Validated/UnityOwned + passed_for_migrated_project_owned_unit_visuals`。

Phase 5A `pure-run-buffs-items-v1` 继续复用同一源管线：14 个 Buff 由固定 Unity AssetDatabase exporter 冻结，3 个 Consumable 与 12 个 Equipment 绑定最终 Tag JSON；typed draft 保留 `SourceId`，并将 `buff.poison` 声明为 `poison-spear-lv1-real` 的唯一外部内容依赖。ResourceSaver 生成 13 个新 Buff、3 个 Consumable、12 个 Equipment 和 29 项分批 Catalog，再将 Poison 6 项、Unit 13 项与本批内容去重合成为 47 个唯一 `ContentId` 的 canonical Catalog。30 个 ledger artifact、UID、目标/语义 hash 和两次 byte-identical 生成均受测；Compatibility 与 Forward+ 使用同一 typed runtime validator。三个 Buff 图标只保存路径、GUID、LocalFileId 与 dependency hash，没有复制 PNG、Unity Material/Shader 或第三方 payload，因此本批以 `visualAcceptance=not_applicable_no_visual_payload` 晋升为 `Validated/UnityOwned`，不会替代 Phase 4 Unit 的人工视觉闸门。

Phase 7B–8D 合并人验前的战斗表现必须保持严格时间边界：玩家 Transition 的表现 `After` snapshot 在任何 AI 自动推进前捕获；AI Decision、Move、Attack/Cast、Hit、Defeat 与 EndTurn 由 Tween 完成事件串行驱动，不使用固定 Timer 猜测动作完成。Pause、Step 和 `0.5x/1x/2x/4x` 只改变表现节奏。尸体与掉矛都属于 movement board 占格，玩家与 AI 共享同一 Core 校验。目标选择阶段只读预览 Unity FacingResolver 的结果，取消后恢复权威朝向。

Pure Run 棋盘外观复用项目自有 warm/cool gray tile 调色和 `BattleBackdrop` 算法的 Godot CanvasItem 等价实现；不复制第三方视觉载荷。Unity 当前未提供全棋盘 focus/shake 合同，因此 Godot 不保留 `presentation.camera.battle-focus-v1`，单位局部受击反馈仍由 StandardUnitTweenProfile 管理。状态与七项程序化技能表现保留后，canonical Catalog 为 124 项。成长 UI 采用 `AttributeAllocation → SkillSelection` 两阶段，但两阶段的属性和技能选择都只是内存草稿；只有最终技能确认才以单次 revision-checked 事务提交属性、技能、guarantee 消费和节点解锁。历史 V5 中曾持久化的 `ProposedAttributes`/`SelectedSkillContentId` 在恢复时丢弃，保留原始 `PendingProgression` 资格并从属性步骤重开。

Phase 8E 的战斗取景从完整 100 格菱形 AABB 与 HUD 安全区计算统一 scale/translation，棋盘、Actor、Highlight、FX、悬浮数值和点击逆变换共享同一表现根。Esc 在 targeting/Console 之后打开非破坏性暂停菜单；Main Menu 与 Save and Quit 保留战前 checkpoint，不产生 Abandoned。动态 Damage Number 只消费 committed damage/roll/heal/mana/status tick 事件，并随表现队列共同暂停、倍速和清理；多段同目标伤害以事件序号逐 Hit 展示，不参与任何结算。

每批次必须通过：

- 源/目标数量核对；
- `ContentId` 唯一性、缺失和重复检查；
- 引用解析和未支持类型报告；
- 重复导入语义无变化；
- Resource 重载和 ContentSnapshot 编译；
- `10×10` 局部坐标、出生格、阻挡格和边界校验；
- `BattleRuntimeScope` 的取消、排空、异常可见性和销毁后 continuation 隔离；
- PresentationExecutionPlan 的 Sequence / Parallel / Leaf 结构在 Runtime 与 Preview 中语义一致；
- Core 与 Godot 测试；
- Unity Windows Standalone Smoke Test 不属于迁移批次门禁；
- 必要的截图回归和人工视觉验收。

覆盖率是辅助指标，不设置统一适用于 Core、Runtime、UI 和 EditorPlugin 的百分比硬门槛；关键系统以契约清单覆盖为准。

## 最终切换与 Unity 退役

所有内容类别进入 `GodotOwned` 且最终 Windows Release 验收通过后：

1. 继续保持既有 Unity 最终归档分支与 `unity-final-2026-08-08` Tag 只读；
2. 将 Godot 迁移分支提升为后续主线；
3. 从 Godot 主线整体移除 Unity 工程、Unity `.meta`、临时 DTO、GUID 映射台账、转换器和不再需要的第三方包；
4. 保留最终 Godot `ContentId`、Catalog、Resource 和测试规范；
5. 在发布阶段用干净环境验证 Godot Windows Release/PCK。

Unity 历史通过归档分支和 Tag 保留，不在 Git 主线长期维护双引擎工程。

## 非目标与未承诺事项

- 不承诺 Unity 项目一键转换；
- 不把临时 DTO 或迁移台账当成最终资产格式；
- 不在迁移阶段顺便重设计玩法规则；
- 不要求开源项目先于 Tactics 迁移完成；
- 不因 `godot-ai` 或其他 MCP 插件的具体版本未锁定而改变 Core、资产身份和批次所有权设计。
