# Godot Phase 4：Pure Run Unit 批次迁移

状态：active（Task 0–7 自动实施完成；Task 8 人工验收待执行）

上级计划：[Godot 迁移：Parity Closure 与 Agent 能力建设](2026-08-09-godot-migration-parity-and-agent-enablement.md)

唯一基线：`2ef5195460aeedea8338d18b296c5f4db5dc9f33`

## Summary

Phase 3 已完成 Poison Spear 真实垂直切片、迁移管线、Core/Application parity、ResourceSaver、Catalog、Godot Editor tooling 与人工视觉验收。本 Phase 4 只迁移 Pure Run Unit 类别，不进入 Buff/Item、Skill、AI/Encounter、Run/Persistence、Scene/UI/Input 或完整 Presentation。

本批采用完整 12 Unit 范围，并按三个稳定门依次推进：玩家三人 → 三个召唤物 → 六个山羊敌人。每个门都沿用冻结 Unity AssetDatabase exporter → disposable DTO → typed Application diagnostics → ResourceSaver/PackedScene → UID/ledger/Catalog → headless/runtime/GdUnit 的证据链。最终提供可在 canonical Editor 人工验收的 12 Unit Gallery 与 10×10 Spawn Smoke。

成功标准：

- 精确生成并加载 12 个 `unit` definition 和一个共享 Unit actor PackedScene。
- 迁移 19 张项目自有基础方向、死亡与阴影 PNG；不复制 Unity material/shader 或第三方视觉 payload，只将项目自有 `GoatBodyTint` 算法等价移植为 Godot shader。
- Unit definition 可以确定性创建不同 `UnitInstanceId` 的 `BattleUnitState`，且派生数值与冻结 Unity 合同一致。
- Unit Catalog、Resource UID、目标 hash、ledger、receipt、失败回滚和第二次生成 no-op 全部受测。
- 自动验证结束时标记 `Generated/UnityOwned + manual_visual_qa_pending`；只有用户完成 Gallery/Spawn/Reload 人验后才能晋升为 `Validated/UnityOwned`。
- 不 push、不自动 commit、不切换到后续 MCP Profile。

## Current State

- checkpoint `2ef51954` 的统一基线已通过：Core 31、Application 13、Unity Oracle 9、迁移 Python 58、Agent policy 8、GdUnit 6、OKF 14；Release 为 0 warning/0 error。
- real Poison Spear batch 为 `Validated/UnityOwned`，Presentation revision 与资源 SHA 基线已固定；Phase 4 不修改其语义或视觉接受范围。
- `ContentSchemaCatalog.RuntimeV1` 已声明 `unit` 与 `packed-scene`，但没有 typed Unit payload compiler。
- Core 已区分定义身份 `ContentId` 与局内实体身份 `UnitInstanceId`；当前 `UnitState/BattleUnitState` 只接收实例化后的数值，没有正式 `UnitDefinition`。
- Godot `GodotUnitNode` 目前直接暴露 ContentId、MoveRange、Initiative 和实例字段；尚未通过 Unit Resource/Factory 创建运行时状态。
- Unity 侧共有 12 个 Pure Run Unit Prefab：3 个玩家、3 个召唤物和 6 个山羊职责变体。玩家定义来自 `CreatePureRunState`/`CharacterDefinition`，敌人路径来自 `EncounterCatalog`，Prefabs 保存 Unit/视觉/移动组件。
- 项目本地 MCP Profile 已切换并检查为 `content-authoring` 23 tools；本轮没有 live Editor session，自动实施全部由后台 Unity batchmode 与 Godot console/headless 完成。

自动实施结果：12 个 Unit definition、19 个 PNG、共享 Actor、13 项 Catalog、4×3 Gallery 与可视 10×10 SpawnFixture 已生成；两张程序化截图均由 Godot Image 使用已导入纹理、`goat-body-mask-v1` CPU 参考和 `unity-unit-sprite-geometry-v1` 几何合同确定性合成。首次截图暴露的共享 goat `Image` 被连续 resize、导致后续山羊逐个缩小的问题已修复，并由源纹理不变测试覆盖。方向现精确冻结为 South=DR、North=UL、East=UL+水平镜像、West=DR+水平镜像；Gallery 初始和 Reset 均为全 South、存活、goat tint 开启。人工首轮又识别出忽略 Unity Sprite pivot 与 Shadow PPU/Transform 的问题：存活 pivot `(0.5,0.078125)` 换算为 Godot Body offset `(0,-108)`，死亡 pivot `(0.5,0.5)` 保持零 offset；Shadow 的 64→128 PPU、`localY=-0.03`、scale `0.8`、alpha `0.9` 换算为 Godot offset `(0,3.84)`、scale `1.6`、alpha `0.9`。Gallery/Fixture 改用可辨阴影的中性灰蓝底。1280×720 逻辑画布的 F6 override 为 1600×900，并采用 `canvas_items + keep`。batch/receipt 为 `Generated/UnityOwned + manual_visual_qa_pending`。统一门禁通过 Core 35、Application 19、Unity Oracle 11、迁移 Python 86、Agent policy 8、GdUnit 11、OKF 14，Debug/Release 零警告零错误。Task 8 前不迁移后续类别、不提交、不 push。

## P0–P3 Decisions

### P0 Scope

- In scope 为全部 12 个 Pure Run Unit，而不是只做 Amazon 或玩家三人。
- 先交付玩家三人垂直切片，再扩展召唤物和敌人；任一门失败时保留已通过证据并停线。
- 自动成功边界为内容、实例化、基础视觉结构、Gallery/Spawn smoke 与完整门禁；最终视觉接受留给用户。

### P1 Approach and Tooling

- Unity 资产只通过 AssetDatabase、SerializedObject 与 PrefabUtility 导出，严禁解析或手改 Unity YAML。
- Core/Application 保持纯 .NET；Godot Node/Resource/UID 只存在于 Adapter；临时 DTO 不成为 runtime 输入。
- `.tres/.tscn` 只通过 ResourceSaver/PackedScene/Editor API 生成；PNG 通过受测复制管线从 allowlist 源路径进入 Godot。
- 复用 Phase 3 的 UID、hash、ledger、rollback 和 byte-idempotency 模式，但建立 Unit 专用 typed converter/factory，不把 Poison Spear factory 扩成无类型框架。

### P2 Core Design

- `UnitDefinition` 只保存战斗实例化需要的不可变 gameplay 数据；Character save state、装备、learned skills、pending buffs 和 encounter modifiers 不进入该类型。
- `GodotUnitDefinitionResource` 保存同一显式数值以及基础视觉引用；`GodotUnitActor` 负责方向、阴影和死亡显示，不改变 Core 状态。
- 玩家 Unit definition 以冻结 `CreatePureRunState` 的角色属性为准；召唤物/敌人的基础数值来自 Prefab Unit 组件。Encounter 倍率、AI Brain 和 AbilityConfig 只进入 deferred-dependency receipt，不成为 Unit Catalog 的 runtime 引用。
- Unit 四方向使用冻结矩阵：South=DownRight、North=UpLeft、East=UpLeft+Body 水平镜像、West=DownRight+Body 水平镜像；Shadow 永不镜像，死亡图不继承方向镜像。`unity-unit-sprite-geometry-v1` 同时冻结 living/death pivot、Body/Shadow PPU、Shadow Transform 和 alpha，并在 Godot 中换算为显式像素 offset/scale。Gallery 使用独立的 `ground-baseline-v1` 布局，Actor 根节点始终代表脚底/尸体落点，三行 Y 为 `155/385/615`，标签不再把根节点误作贴图中心。玩家和山羊可配置死亡图，三个召唤物没有死亡图并安全回退到存活图。

### P3 Permission Boundary

用户已恢复并确认执行此前讨论的 Phase 4 Unit-only 计划。允许本批直接需要的 Core/Application 公共 Unit 契约、Unity Editor-only exporter、迁移 converter/manifest/验证工具、Godot Adapter runtime/editor factory、`godot/content/units`、`godot/assets/units` 和验证场景变更。该授权不扩展到其他内容类别、冻结 Unity 玩法、第三方资产、发布、push 或自动 commit。

## Scope

### In Scope

- 12 个稳定 Unit ContentId、显式 gameplay definition 与来源映射。
- `UnitAttributes`、`UnitDerivedStats`、`UnitDefinition` 和 definition-to-battle-state 纯逻辑。
- Unit typed draft/compiler/diagnostics 与 12 定义 Golden。
- Unit AssetDatabase exporter、源 spec、DTO、receipt、generation ledger 与 ownership/status。
- 19 张项目自有 PNG：
  - 玩家 Mage/Necromancer/Amazon 的 DR、UL、death 共 9 张；
  - SkeletonWarrior/SkeletonMage/FireDemon 的 DR、UL 共 6 张；
  - 山羊共享 DR、UL、death 共 3 张；
  - 共享 Unit shadow 1 张。
- 12 个 `GodotUnitDefinitionResource`、共享 `UnitActor.tscn`、Unit Catalog、Factory、Gallery 和 10×10 Spawn Smoke。
- Core/Application/Unity Oracle/Python/GdUnit/headless/Compatibility/Forward+/Release/OKF 完整验证。
- 用户人工检查四方向、比例、格心、阴影、死亡策略、山羊 tint、Reload 和 Spawn。

### Out of Scope

- Buff、Consumable、Equipment、Skill、AI、Encounter、Run/Persistence、Scene/UI/Input 内容或运行时迁移。
- Learned skill、starting branch、equipment、pending buff、inventory、save slot 和 encounter multiplier 进入 Unit definition。
- Unit action pose、Tween、完整死亡动画、投射物、VFX、Audio 或 Presentation Graph 改写。
- Piloto、HeliSprite、FloatingUnitShader、Unity material/shader 或任何授权未确认的第三方 payload。
- Unity Windows Standalone、Godot Windows Release/PCK、CI、push、PR、自动 commit 或历史改写。
- 前台 Computer Use、真实鼠标键盘或自动声称人工视觉通过。

## Stable Identity and Content Set

### 玩家

- `unit.pure-run.mage` ← `pure_run_mage` / `PureRunMage.prefab`
- `unit.pure-run.necromancer` ← `pure_run_necromancer` / `PureRunNecromancer.prefab`
- `unit.pure-run.amazon` ← `pure_run_amazon` / `PureRunHunter.prefab`

### 召唤物

- `unit.pure-run.skeleton-warrior` ← `PureRunSkeletonWarrior.prefab`
- `unit.pure-run.skeleton-mage` ← `PureRunSkeletonMage.prefab`
- `unit.pure-run.fire-demon` ← `PureRunFireDemon.prefab`

### 山羊敌人

- `unit.pure-run.goat-charger` ← `PureRunGoatCharger.prefab`
- `unit.pure-run.goat-ranged` ← `PureRunGoatRanged.prefab`
- `unit.pure-run.goat-aoe` ← `PureRunGoatAoe.prefab`
- `unit.pure-run.goat-support` ← `PureRunGoatSupport.prefab`
- `unit.pure-run.goat-elite-charger` ← `PureRunGoatEliteCharger.prefab`
- `unit.pure-run.goat-elite-poison-caster` ← `PureRunGoatElitePoisonCaster.prefab`

共享 actor ContentId 为 `packed-scene.unit-actor`。Unity source ID、GUID、LocalFileId、Prefab path、Git blob 和 dependency hash 只进入 receipt，不替代业务 ContentId。

## Unit Definition Contract

每个 Unit definition 显式保存并校验：

- `ContentId`、`SourceId`、`DisplayName`、`FamilyId`、`RoleId`；
- Strength、Agility、Constitution、Intelligence、Charisma、Luck；
- Speed、MaxHealth、MaxMana、StartingMana、MoveRange、Initiative；
- AttackRange、AttackFactor、DefenceFactor、MovementKind；
- `CanProduceCorpse`、DownRight/UpLeft/Death texture、各 Sprite pivot/PPU、Shadow texture/Transform/alpha、Body tint、tint mode 与 BaseBodyColor；
- 共享 actor ContentId 与 deferred dependency audit。

冻结派生合同 `unity-unit-derived-v1`：

- `MaxHealth = max(1, Constitution × 4)`；
- `MaxMana = max(0, Charisma × 3)`；
- `StartingMana = Charisma`；
- `MoveRange = clamp(ceil(Speed × 0.5), 1, 4)`；
- `Initiative = Speed × 2`。

最终 Godot Resource 必须显式序列化这些结果，并由 compiler 验证与属性一致，不依赖 C# Resource 默认值。

## File Structure

- `src/Tactics.Core/Units/UnitAttributes.cs` — 六维基础属性值对象。
- `src/Tactics.Core/Units/UnitDerivedStats.cs` — HP/MP/Move/Initiative 明确结果。
- `src/Tactics.Core/Units/UnitDefinition.cs` — 不可变 Unit gameplay definition 与 battle-state factory。
- `src/Tactics.Core/Units/UnitDerivedStatRules.cs` — `unity-unit-derived-v1` 纯 .NET 公式。
- `src/Tactics.Application/Units/UnitDefinitionDraft.cs` — disposable typed Unit 输入，不引用 Unity/Godot。
- `src/Tactics.Application/Units/UnitDefinitionCompiler.cs` — ID、数值、公式、视觉引用和完整 12 集合 diagnostics。
- `src/Tactics.Core.Tests/UnitDefinitionTests.cs` — 公式、身份、实例化和边界测试。
- `src/Tactics.Application.Tests/UnitDefinitionCompilerTests.cs` — schema、重复 ID、无效值、集合和引用测试。
- `src/Tactics.UnityOracle.Tests/` — 可安全 linked 的冻结派生规则与 blob/semantic assertions；不得成为生产依赖。
- `Assets/Tactics/Scripts/Editor/Migration/TacticsUnitExporter.cs` — Unit 专用 AssetDatabase/SerializedObject/PrefabUtility exporter。
- `Tools/migration/manifest/export-batches/pure-run-units-v1.json` — 12 Unit、source tag/commit/blob、目标 ID 与视觉 allowlist。
- `Tools/migration/unit_converter.py`、`unit_receipt.py` — DTO 到 typed draft/receipt。
- `Tools/migration/manifest/batches/pure-run-units-v1.json` — batch scope、状态、ownership 与目标路径。
- `Tools/migration/manifest/receipts/`、`state/` — export/generation/license receipt 与 ledger。
- `Tests/golden/unit-batch-v1.json` — 12 Unit 版本化预期，供 Core/Application/Godot 消费。
- `Tests/golden/oracle-matrix.json` — Unit source tests/blob/export/Golden 证据关系。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotUnitDefinitionResource.cs` — Unit Resource 字段与 Core 转换。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotUnitActor.cs` — Body/Shadow、方向、tint 和 defeated 显示。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotUnitFactory.cs` — definition + spawn context → actor + `BattleUnitState`。
- `godot/src/Tactics.Godot.Adapter/Runtime/UnitBatchValidator.cs` — Catalog/Golden/资源/场景/runtime smoke。
- `godot/src/Tactics.Godot.Adapter/Editor/UnitAssetFactory.cs` — ResourceSaver/PackedScene 生成与原子 transaction。
- `godot/content/units/definitions/` — 12 个生成的 Unit Resource。
- `godot/content/units/UnitCatalog.tres` — 12 unit + 1 packed-scene entry。
- `godot/assets/units/` — 19 张受 allowlist/receipt 管理的 PNG。
- `godot/scenes/units/UnitActor.tscn` — 共享 actor PackedScene。
- `godot/scenes/validation/UnitBatchGallery.tscn` — 12 Unit、方向/死亡/阴影人工 Gallery。
- `godot/scenes/validation/UnitSpawnSmoke.tscn` — 固定 10×10 单位实例化与位置 smoke。
- `godot/tests/UnitBatchTests.cs` — Resource/Factory/Gallery/Spawn/UID/Godot Golden GdUnit 测试。
- `Tools/migration/Verify-GodotMigration.ps1` — Unit converter、双生成、filesystem scan、runtime smoke 与 Unit receipt gate。

## Implementation

### Task 0: 执行起点与 Profile

- 目标：确认执行按钮启动于唯一 checkpoint 和干净的迁移环境。
- 输入：HEAD、工作树、项目本地 MCP 配置、Godot/Unity Editor 进程。
- 输出：基线报告与 `content-authoring` Profile。
- 验收标准：
  - HEAD 为 `2ef51954`；除 OneLine 换行假性状态和本计划文档外没有未解释变更。
  - 完整 `Verify-GodotMigration.ps1` 仍全绿。
  - Godot Editor/MCP session 为 0 后再切换项目本地 `content-authoring`；Profile 检查为 23 tools、用户配置不变、永久 deny-list 未扩大。
  - 不创建第二个项目/worktree，不启动前台 UI。

### Task 1: 冻结 Unit Oracle 与 Golden

- 目标：先固定来源、12 Unit 集合和派生公式，再实现 runtime。
- 输入：最终 tag/commit、CharacterDefinition/CreatePureRunState、Unit/UnitDerivedStatRules、相关 Unity tests 与 12 Prefab paths。
- 输出：Oracle Matrix Unit 条目、frozen blob binding、`unit-batch-v1.json`。
- 验收标准：
  - source tag 为 `unity-final-2026-08-08`、commit 为 `168d19345d7e0f7f22ce2516351eda9cef2e1cb1`，所有引用 blob 与冻结 commit 一致。
  - Golden 精确列出 12 个 ContentId、分类、数值、基础视觉和 corpse policy。
  - Oracle 明确区分 linked-source、Unity test passed、AssetDatabase export 和 migration contract；不能逐语句对齐的边界不得伪装 parity。
  - Oracle/Python/Core Golden schema tests 先红后绿，并继续保持 Release 无测试依赖。

### Task 2: Core/Application Unit 契约

- 目标：建立纯 .NET Unit definition 与 typed compile boundary。
- 输入：Task 1 Golden 和 `unity-unit-derived-v1`。
- 输出：`UnitAttributes`、`UnitDerivedStats`、`UnitDerivedStatRules`、`UnitDefinition`、`UnitDefinitionDraft/Compiler`。
- 验收标准：
  - 公式覆盖 Speed 1/4/5/6/8/12/99，与冻结移动上限和 Initiative 合同一致。
  - `UnitDefinition.CreateBattleState` 要求调用者提供 `UnitInstanceId`、position、player number 和 spawn ordinal；同一定义可生成不同实例。
  - Draft 拒绝空/非规范 ID、非有限数值、负 range/factor、公式不一致、未知 movement kind、缺失 actor/texture 引用和重复 definition。
  - Core/Application 测试消费同一 Unit Golden；程序集不引用 Unity、Godot 或 DTO serializer。

### Task 3: Unity Unit AssetDatabase Exporter

- 目标：从冻结语义和真实 Prefab/Texture 引用导出 disposable Unit DTO。
- 输入：12 Unit spec、玩家 fixed seed roster、12 Prefab、19 PNG allowlist。
- 输出：`pure-run-units-v1.unity.json` 与 deterministic export receipt。
- 验收标准：
  - Unity Editor-only `.cs` 变更后通过规定的 Editor compile/`refresh_unity` 门禁；不执行 Unity Windows Standalone。
  - Exporter 使用 AssetDatabase/SerializedObject/PrefabUtility，记录 GUID、LocalFileId、Git blob、dependency hash、组件类型、数值、texture/import contract、tint 和 deferred dependencies。
  - 玩家 stats 来源于 `CreatePureRunState` 的角色语义；召唤物/敌人 stats 来源于 Prefab Unit 组件；不把 starting skills/equipment/buffs/AI/Encounter multiplier 写入 definition。
  - 两次独立导出 byte-identical；unsupported property/node/reference 全部显式报告，不能静默丢弃。
  - 任何源 hash、GUID、Prefab count、PNG allowlist 或第三方路径漂移立即 fail-closed。

### Task 4: 玩家三人 ResourceSaver 垂直切片

- 目标：先打通 Mage/Necromancer/Amazon 的完整生成与 runtime。
- 输入：Unit DTO/typed draft、9 玩家 PNG、共享 shadow。
- 输出：3 Unit Resource、共享 UnitActor、Unit Catalog 初版、Factory、3 人 Gallery/Spawn fixture。
- 验收标准：
  - 每个 Resource 显式保存全部 Unit contract 字段和 actor/texture refs，Catalog 使用稳定 UID。
  - Actor 严格使用 South=DR、North=UL、East=UL+Body 水平镜像、West=DR+Body 水平镜像；Shadow 不镜像；living 使用冻结脚底 pivot，death 使用冻结中心 pivot，切换 texture/offset 时清除方向镜像且不改变 Core alive state。
  - Unity Shadow 的 `64 PPU × 0.8 scale` 在 `128 PPU` Body 坐标中换算为 `1.6`，`localY=-0.03` 按 Y 轴翻转换算为 Godot `+3.84px`，Renderer alpha 保持 `0.9`。
  - Factory 创建的 `BattleUnitState` HP/MP/Move/Initiative 与 Golden 一致；三个实例 position/player/spawn ordinal 可独立指定。
  - ResourceSaver 连续两次输出和 ledger byte-identical；失败注入恢复所有目标和 ledger 原字节。
  - headless filesystem scan 后由独立 runtime 加载 Catalog、3 Resource 和 PackedScene。

### Task 5: 扩展三个召唤物

- 目标：复用玩家切片加入 SkeletonWarrior、SkeletonMage 和 FireDemon。
- 输入：6 方向 PNG 与 3 Prefab DTO。
- 输出：6 Unit definitions 总集和 6 Unit Gallery/Spawn。
- 验收标准：
  - 三个召唤物使用各自 DR/UL 和共享 shadow，不配置 Death texture，`CanProduceCorpse=false`。
  - movement kind、stats、tint 和基础 actor 引用与 DTO/Golden 一致。
  - 不引入 Summon skill、尸体消费、上限/替换、普通治疗策略或 AI runtime。
  - Unit batch 6 定义重复生成 no-op；窄 Core/Application/Python/GdUnit/runtime tests 全绿。

### Task 6: 扩展六个山羊敌人

- 目标：完成全部 12 Unit，同时保留六种职责视觉区分。
- 输入：共享 goat DR/UL/death、六个 Prefab tint/Unit DTO。
- 输出：12 Unit Resource、最终 Unit Catalog、12 Unit Gallery/Spawn。
- 验收标准：
  - 六个山羊共享三张 goat texture 与 shadow；各自 Resource 保存 `goat-body-mask-v1`、BodyTint、BaseBodyColor 与独立 ShaderMaterial 参数，并共享项目自有算法的 Godot shader；Unity `.mat/.shader` payload 不复制。
  - 身体色距蒙版、亮度补偿和 alpha 语义与冻结 Unity `GoatBodyTint` 合同一致；Body Modulate 保持白色，轮廓、角、手柄和刀刃不应被整图压暗。
  - `CanProduceCorpse=true` 与共享 goat death 只影响基础死亡显示，不实现 Encounter corpse gameplay。
  - AI Brain、AbilityConfig、minimum mana 和 Encounter multiplier 仅进入 receipt 的 deferred dependency 清单，Catalog 不引用未迁移内容。
  - Catalog 精确包含 12 个 `unit` 和 1 个 `packed-scene`，无重复 ContentId/UID/type/path。
  - 12 Unit 双生成、runtime load、Factory spawn 和 Golden 全部通过。

### Task 7: 自动验证与程序化验收准备

- 目标：在请求人工 Editor QA 前完成所有可自动证明的证据。
- 输入：完整 Unit batch、receipts、ledgers、tests 与两 validation scenes。
- 输出：自动验收报告、程序化截图、人工清单和 `manual_visual_qa_pending` 状态。
- 验收标准：
  - `Verify-GodotMigration.ps1` 串行通过 locked restore、build、Core/Application/Unity Oracle、Python、GdUnit、Release、filesystem scan、Compatibility/Forward+、Unit Gallery/Spawn headless、Skill/Incident/OKF 和 whitespace。
  - 连续两次 Unit export 和 generation 均 byte-identical；19 PNG SHA、尺寸、alpha、import 与来源 allowlist 通过。
  - 程序化截图覆盖 12 Unit Gallery 和 10×10 Spawn；使用 `goat-body-mask-v1` CPU 参考及 Sprite pivot/Shadow PPU/Transform 换算，并证明共享纹理不会因截图缩放被就地修改；截图只证明场景可见/非 stale，不代替视觉接受。
  - Poison Spear 6-entry Catalog、Presentation revision、目标资源 SHA 和现有 runtime smoke 不漂移。
  - Godot Editor/MCP session 在交接时为 0，工作树无临时 DTO、build cache、凭据、第三方 payload 或意外 staged 文件。

### Task 8: 用户人工验收与 Phase 4 收口

- 目标：由用户在 canonical Editor 完成 Unit 类别的视觉/Reload 接受。
- 输入：自动全绿报告、`UnitBatchGallery.tscn`、`UnitSpawnSmoke.tscn`。
- 输出：用户确认、`Validated/UnityOwned` receipt、Phase 4 closure 验证。
- 验收标准：
  - Gallery 中 12 Unit 比例、格心、精确 DR/UL+镜像矩阵、阴影不镜像、玩家/山羊死亡图、召唤物无死亡图和六种山羊身体蒙版 tint 均人工确认。
  - `T` 对比时只有山羊身体区域变化；轮廓、角、锈色手柄和钢制刀刃保持不变，身体高光/阴影层次保留，不出现整只角色近黑。
  - Resize 不产生非等比拉伸；Assembly Reload 后 UID、Catalog、纹理、方向和当前 scene 无 missing/disposed/duplicate type 错误。
  - Spawn Smoke 中固定位置、实例 ID、definition ID、玩家编号和 spawn ordinal 正确，无重叠或越界。
  - Output 无 Unicode/NUL、UID、missing Resource、duplicate ContentId、assembly reload 或 disposed object 错误。
  - 用户明确确认后才更新 batch 为 `Validated/UnityOwned`，再次运行完整统一验证；不自动开始 Buff/Item。

## Test Plan

### 自动节奏

1. Task 0 写入前完整基线。
2. 每个 Task 运行对应 Core/Application/Oracle/Python/GdUnit 窄测试，禁止并行争抢 Core `obj`。
3. Task 4 玩家切片、Task 6 全 12、Task 7 最终自动门分别运行完整 `Verify-GodotMigration.ps1`。
4. 每次新增 Resource 路径固定执行：standalone ResourceSaver generation → headless Editor filesystem scan → 独立 runtime validation。
5. 最终运行 `git diff --check`、secret/binary/license/path、tracked/untracked/cache、UID/ContentId/GUID 和 `.meta` 配对审计。

### 人工步骤

1. 打开 canonical Godot Editor 和 `godot/content/units/UnitGallery.tscn`，按 F6；窗口应约为 1600×900，初始 12 Unit 全部 South、存活、goat tint 开启。三排角色应位于各自标签上方且第一排不得侵入两行 HUD；`D/R` 切换时 Actor 的脚底/尸体落点保持不动。
2. 按 `1/2/3/4` 逐项检查：South=DR、North=UL、East=UL 水平镜像、West=DR 水平镜像；重点观察不对称的面部、武器和轮廓，Shadow 始终不镜像。
3. 按 `D`：玩家和山羊显示死亡图且不继承方向镜像，living 脚底与 death 尸体应落在同一 Actor/Shadow 基准，不再出现切换后整体偏上；三个召唤物保持存活图。按 `R` 后必须恢复全部 South、存活、goat tint 开启。
4. 对六种山羊反复按 `T`：只有身体区域改变，轮廓、角、锈色手柄、钢制刀刃不变，身体高光/阴影层次保留；允许冻结 tint 偏暗，但不得整只角色近黑。
5. 把窗口调整为不同宽高比，确认 `canvas_items + keep` 以黑边保持比例，没有横向或纵向拉伸。
6. 打开 `godot/content/units/UnitSpawnFixture.tscn` 并按 F6；中性浅灰蓝底上应能直接看到每个单位脚底的椭圆阴影，再检查 10×10 固定位置、体量和边界，无重叠或越界。
7. 执行 Assembly Reload 后重跑 Gallery/Spawn；Output 不得出现 Unicode/NUL、shader、UID、missing Resource、duplicate type 或 disposed object 错误。
8. 关闭 Editor 后回复验收结果；Agent 再做 status promotion、closure verifier 和提交准备。

## Automatic Stop Conditions

- source tag/commit/blob/GUID、12 Prefab 或 19 PNG allowlist 不一致。
- 第二次导出/生成仍不一致，且两轮窄修复后不能收敛。
- 目标无匹配 ledger、既有 UID 漂移、人工目标改写冲突或回滚不能恢复原字节。
- 导出依赖包含 Piloto、HeliSprite、FloatingUnitShader 或其他未确认第三方 payload。
- Core/Application 出现 Unity/Godot 依赖，或 Release 出现 GdUnit/TestPlatform/dev-only 依赖。
- Unit 定义必须依赖未迁移 Skill/AI/Encounter/Persistence 才能加载。
- 基线/Poison Spear 回归无法归因，或继续需要前台 UI 自动化。

停线时保留已通过证据，记录准确错误、路径和下一步；不使用 reset/checkout 丢弃用户或本任务变更。

## Risks and Assumptions

- Unity 中 Unit 真相分散在代码生成角色状态和 Prefab serialization；Exporter 必须明确来源优先级，不能把 Prefab 默认值误当玩家最终 stats。
- 三个召唤物与六个敌人的 Skill/AI/Encounter 行为不属于 Unit 类别；本批仅保证 definition、基础 actor 与实例化。
- 19 张 PNG 被现有项目测试/文档定义为项目自有或已批准来源；执行时仍必须逐路径做 allowlist/hash/license 审计。
- `content-authoring` 仅在 Task 0 执行时启用；Phase 4 结束后不自动升级 `ui-input` 或 `presentation`。
- 计划允许必要的 Unit 公共契约和工具链变更，但不建设面向未来所有内容类别的通用无类型框架。

## Handoff Notes

- 先读本计划、上级计划、`.agents/knowledge/plans/godot-migration.md` 与 `.agents/knowledge/operations/godot-agent-workflow.md`。
- 实现时加载 `godot-csharp-development`、`godot-content-migration`、`godot-testing-diagnostics`；修改 Unity exporter 前同时加载 `project-coding-reference`、`unity-auto-compile-guard`，修改 ResourceSaver Editor factory 时加载 `godot-editor-tooling`。
- 禁止手写 `.tres/.tscn`、解析 Unity YAML、创建第二个 Godot 项目/worktree、执行 Unity Windows Standalone、抢占前台、迁移后续类别或 push。
- Phase 4 实现与人工验收完成后，将 Unit 长期合同并入 `.agents/docs/2026-08-07-godot-tactics-migration-design.md`，真实未完成项进入统一缺口或获批的新计划，按影响报告同步 OKF，删除本 completed plan，由 Git 保存历史。
