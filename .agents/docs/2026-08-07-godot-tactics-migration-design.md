# Tactics Unity → Godot 迁移设计

状态：已完成设计确认，尚未进入实现计划或迁移执行。

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

建立长期 Godot migration branch 和独立 worktree。Unity 主线继续作为当前 Unity 与共享 Core 的权威源，需要保留的变更只单向汇入迁移分支；Godot 专属工程、Resource、场景和 Adapter 不反向合并到 Unity 主线。

共存期，正在剥离的 Pure .NET 源文件仍放在 Unity 能管理 `.meta` 的位置；独立 `.csproj`、测试项目和 Godot 工程通过显式 `Compile Include` 或 `ProjectReference` 复用，不维护两份源码，也不使用目录软链接。

最终代码层次为：

```text
Tactics.Core
Tactics.Unity.Adapter       # 迁移期临时存在
Tactics.Godot.Adapter
Godot Editor / Content
```

`Tactics.Core` 不引用 `UnityEngine` 或 Godot。Godot 最终内容直接使用 `Resource`/`PackedScene`；迁移 DTO 只存在于具体导出/导入步骤，不成为长期资产格式。

## 内容真相源、身份与迁移批次

### 所有权状态

每类内容（Unit、Skill、Buff、AI、Encounter、Presentation 等）独立迁移，并且只能处于一个状态：

```text
UnityOwned
    ↓ 按 Unity commit 冻结
FreezePending
    ↓ 导出、转换、验证、修复引用
GodotOwned
```

- `UnityOwned`：Unity 资产是真相源，Godot 产物仅供试验；
- `FreezePending`：该类别停止 Unity 编辑，绑定明确的源 commit 并开始验收；
- `GodotOwned`：Godot 资产成为唯一真相源，禁止再从 Unity 覆盖；
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
- Unity GUID/LocalFileId 只负责迁移期定位来源；
- Godot `uid://` 负责 Godot 工程内 Resource 的物理身份和路径移动后的引用稳定性；
- 重命名或移动保留 `ContentId`，复制成新内容必须生成新 ID；
- 存档和 Core 只保存 `ContentId`，不保存 `uid://` 或 `res://` 路径；
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

Locator Catalog 记录 `ContentId → ResourceType + uid://`，运行时不扫描内容目录。`ContentId` 只在 Core/存档层使用，UID 只在 Godot 适配层使用。

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

当前 Unity 基线已经提供 `PresentationExecutionPlan`：Graph 编译为纯数据的 Sequence / Parallel / Leaf 结构，Runtime 和 Preview 使用不同叶执行器消费同一计划。Godot 应迁移这一语义契约和编译边界，不迁移 DOTween、Unity Editor 或玩法状态到计划对象中。

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

Tactics.Godot.Tests
    └─ GdUnit4Net，Resource、Node、Scene、输入和运行时

Tactics.Godot.EditorSpike
    └─ EditorPlugin、GraphEdit、Undo、SubViewport、Assembly Reload
```

Unity 测试在最终切换前继续保留；不为保持 Godot 工程整洁而提前删除。修改 Core 时按影响范围运行 Unity、Core 和 Godot 测试，垂直切片完成及合并前执行关键路径双引擎全量验证。

迁移前生成并提交固定 Golden Test Vectors，记录固定种子、棋盘、单位、技能和指令序列，以及合法行动、路径、伤害、状态和事件结果。向量默认只读，更新必须单独审查。

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

1. 冻结 Unity 工程和最后源代码提交；
2. 创建 Unity 最终归档分支和 Tag，并完成最后构建、测试和依赖审计；
3. 将 Godot 迁移分支提升为后续主线；
4. 从 Godot 主线整体移除 Unity 工程、Unity Adapter、Unity `.meta`、临时 DTO、GUID 映射台账、转换器和不再需要的第三方包；
5. 保留最终 Godot `ContentId`、Catalog、Resource 和测试规范；
6. 用干净 worktree 验证主线可以独立完成 Windows 构建。

Unity 历史通过归档分支和 Tag 保留，不在 Git 主线长期维护双引擎工程。

## 非目标与未承诺事项

- 不承诺 Unity 项目一键转换；
- 不把临时 DTO 或迁移台账当成最终资产格式；
- 不在迁移阶段顺便重设计玩法规则；
- 不要求开源项目先于 Tactics 迁移完成；
- 不因 `godot-ai` 或其他 MCP 插件的具体版本未锁定而改变 Core、资产身份和批次所有权设计。
