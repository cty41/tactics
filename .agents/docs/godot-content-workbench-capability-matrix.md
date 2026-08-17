# Godot Content Workbench 能力矩阵

本文是 `Tactics Tooling` Main Screen 的防回退基线。Unity 证据只来自只读标签
`unity-final-2026-08-08`；目标能力必须落在当前 Core/Application/Godot Resource 模型，不能恢复 Unity
ScriptableObject、AssetBundle、RuleTile 或旧运行时。

状态含义：`implemented` 已有可执行代码与自动证据；`replacement` 由现行 Godot/Core 流程明确替代；
`excluded` 不属于三职业 Pure Run 的有效作者能力；`partial` 已有可执行切片但仍有列出的验收缺口。矩阵没有
`unknown`。

## 保留能力

| Unity 最终标签证据 | 有效作者能力 | Godot 页面/合同 | 当前状态 | 自动证据 | 尚需人工/实现收口 |
|---|---|---|---|---|---|
| `Assets/Tactics/Scripts/Editor/RoguelikeMapEditor` | Map 节点、连接、Inspector、布局、导入导出 | Map / `MapAuthoringDocument` | partial | CRUD、确定性布局、Import/Export、完整图校验、全局草稿与原子 Apply All；Unity 语义色圆形节点与主题组件几何回归已受测 | 七层真实资源的图面密度、连接手感及 Apply→Undo→Redo→Reload 人验 |
| `Assets/Tactics/Scripts/Editor/RoguelikeEventEditor` | 事件选项、检定、成功/失败结果与预览 | Event / `EventAuthoringDocument` | partial | typed outcome、Catalog picker、固定 Seed 预览、round-trip、同批 rebind/Delete 与回滚 | 真实 Editor 人验 |
| Unity Map 内嵌 Treasure 数据 | 金币和三类 weighted table | Treasure / `TreasureAuthoringDocument` | partial | 三类 picker、排序、精确概率/固定 Seed 抽样、round-trip、同批 rebind/Delete 与回滚 | 真实 Editor 人验 |
| `Assets/Tactics/Scripts/Editor/BattleTest` | Encounter、队伍/敌人、出生格、阻挡格、固定场景预览 | Encounter Fixture / `EncounterAuthoringDocument`、`BattleLayoutAuthoringDocument` | partial | 10×10 草稿、组合校验、双 Resource 原子 Apply、固定 Seed 单步/整轮草稿预览 | 拖动手感、退出清理与 Reload 人验 |
| `Assets/Tactics/Scripts/Editor/MonsterAIEditor` | Intent/Rule/Score/Curve、边、Profile、技能与射程偏好 | AI / `AiAuthoringDocument` | partial | CRUD、Curve key、拖动坐标、DAG/类型校验、专用 picker、六类无损 round-trip；运行时忽略的 Rule 字段只读并诊断 | 真实 Editor 人验 |
| `Assets/Tactics/Scripts/Editor/SkillGraphEditor` | 技能全字段、编译、测试世界 | Skill Definition / `SkillAuthoringDocument` | partial | ExecutionKind 分组 Inspector、全字段/召唤 round-trip；隔离 `BattleTransitionService` 的合法/非法预览与 fingerprint 一致性 | 真实 Editor 人验 |
| `Assets/Tactics/Scripts/Editor/PresentationWorkbench`、`PresentationGraph` | 表现图、场景、作用域预览、时间线、播放控制 | Presentation / `PresentationGraphDocument` | partial | 全部原生 Skill/Status/Unit Profile 可用相同 runtime player 播放，含作用域、速度、活动节点、共享叶 Duplicate & Rebind 和 cleanup；未消费的 Delay/Parallel fail-closed | 视觉、拖动与 Reload 人验 |
| `Assets/Tactics/Scripts/Editor/UnitTweenVisualEditor.cs` | UnitTween 参数和运行时诊断 | Presentation/QA | replacement | Godot-native runtime/preview 共用 player；QA 显示活动 Tween、临时节点与 cleanup | 真实视觉人验 |
| `Assets/Tactics/Scripts/Editor/MCP/PresentationGraphMcpTools.cs`、`SkillGraphMcpTools.cs` | 远程 list/get/validate/apply/preview | Tactics Authoring MCP / Application 作者合同 | partial | 六工具、协议协商、notification、`isError`、typed preview、`changes+lifecycle`、同批 rebind/Delete、单 Undo 与 fail-closed 测试 | canonical Editor 实连人验 |

## 明确替代或排除

| Unity 最终标签证据 | 状态 | 处理结论 |
|---|---|---|
| `Assets/Tactics/Scripts/AssetPipeline/Editor` | replacement | Godot import、ResourceSaver、Catalog、Windows RC 构建链替代；不恢复 AssetBundle。 |
| `SkillGraphAbilityConfigGenerator` 与各职业/样本 builder | replacement | 冻结迁移 receipt 已完成；新作者输入直接编译为 `SkillDefinitionResource`。 |
| `PartyBootstrapSetupEditor.cs` | replacement | 当前 Pure Run Definition、Catalog 与 session factory 是权威。 |
| `DamageNumberConfigGenerator.cs` | replacement | Godot `GodotDamageNumberLayer` 与 Presentation Resource 是权威。 |
| `BattleTest/TestCorpsePrefabCreator.cs` | excluded | 一次性 Unity Prefab 生成器；Godot corpse/summon 语义由 Skill/Core 资源表达。 |
| Common/ThirdParty Grid、Brush、RuleTile Editor | excluded | 属于旧 Unity Tile/Grid 架构；10×10 Godot fixture 使用 Core `GridPoint` 与 SubViewport。 |
| `CompileToast` | excluded | 编辑器通知便利功能，不是内容作者能力。 |
| Odin、OneLine、TBSFramework、Unity UI Extensions | excluded | 第三方 Unity 编辑器 UI，不迁移。 |
| `Tool_UIToolkitDebug.cs`、`UnityMcpProjectBootstrap.cs` | excluded | Unity 调试/引导工具；不能进入 Godot 权威链。 |

## 统一安全合同

- 所有作者文档实现 `IAuthoringDocument`，revision 是规范化文档 SHA-256。
- `AuthoringSession<T>` 持有 baseline/draft/dirty/conflict；预览和 Import 不能修改正式 Resource。
- Workspace coordinator 汇总所有页面草稿；Validate All、Apply All 与 Revert All 使用同一个 expected revision
  集合和一个 `EditorUndoRedoManager` action。
- Adapter 将 Resource、Catalog、UID ledger、tombstone 与引用图全部 staging 后再用 ResourceSaver 保存；任一
  fault injection 点失败均恢复磁盘、内存身份、session revision 与 dirty 状态。
- Workbench-owned Create/Duplicate/Delete 与 typed document rebind 共用 `AuthoringBatchChangeSet`；Delete 必须
  匹配引用快照且 prospective graph 不再引用目标。正式 ownership/receipt 资源继续禁止删除，Undo/Redo
  恢复同一 UID，tombstone UID 永不分配给新资源。

## 完成门禁

矩阵中的 `partial` 只有在关联代码、自动测试、真实 Editor 人工步骤三者都有证据后才能改为
`implemented`。自动门禁不能替代 Assembly Reload、视觉可读性、Graph 连线手感、预览观感和 Windows RC
人工验收。
