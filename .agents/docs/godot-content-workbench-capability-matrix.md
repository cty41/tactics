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
| `Assets/Tactics/Scripts/Editor/RoguelikeEventEditor` | 事件选项、检定、成功/失败结果与预览 | Event 图 / `EventAuthoringDocument` | partial | 显式 Start→Option→Check→Outcome→End 投影、typed outcome、Catalog picker、固定 Seed 预览、布局 revision 与回滚；Auto Success 隐藏 Failure 但不删数据 | 节点连线、Inspector 与 Reload 人验 |
| Unity Map 内嵌 Treasure 数据 | 金币和三类 weighted table | Event 页 Treasure 类别 / `TreasureAuthoringDocument` | partial | Root→Gold/Table 图、三类 picker、排序、精确概率/固定 Seed 抽样、布局 round-trip、同批 rebind/Delete | 真实 Editor 人验 |
| `Assets/Tactics/Scripts/Editor/BattleTest` | Encounter、队伍/敌人、出生格、阻挡格、固定场景预览 | 后台/MCP / `EncounterAuthoringDocument`、`BattleLayoutAuthoringDocument` | partial | 可见顶层页已移除；组合校验、双 Resource 原子 Apply、固定 Seed预览和 TS→MCP 作者链保留 | MCP 实连与 Skill fixture 人验 |
| `Assets/Tactics/Scripts/Editor/MonsterAIEditor` | Intent/Rule/Score/Curve、边、Profile、技能与射程偏好 | 后台/MCP / `AiAuthoringDocument` | partial | 可见顶层页已移除；DAG/类型校验、六类无损 round-trip、TS→MCP 作者链保留；运行时忽略字段继续诊断 | MCP 实连人验 |
| `Assets/Tactics/Scripts/Editor/SkillGraphEditor` | 技能全字段、编译、测试世界 | Skill Definition / `SkillAuthoringDocument` | partial | ExecutionKind 分组 Inspector、全字段/召唤 round-trip；隔离 `BattleTransitionService` 的合法/非法预览与 fingerprint 一致性 | 真实 Editor 人验 |
| `Assets/Tactics/Scripts/Editor/PresentationWorkbench`、`PresentationGraph` | 表现图、场景、作用域预览、时间线、播放控制 | Presentation / `PresentationGraphDocument` | partial | 全部原生 Skill/Status/Unit Profile 可用相同 runtime player 播放，含作用域、速度、活动节点、共享叶 Duplicate & Rebind 和 cleanup；未消费的 Delay/Parallel fail-closed | 视觉、拖动与 Reload 人验 |
| `Assets/Tactics/Scripts/Editor/UnitTweenVisualEditor.cs` | UnitTween 参数和运行时诊断 | Skill / Presentation 与全局状态区 | replacement | Godot-native runtime/preview 共用 player；全局诊断保留活动 Tween、临时节点与 cleanup | 真实视觉人验 |
| `Assets/Tactics/Scripts/Editor/MCP/PresentationGraphMcpTools.cs`、`SkillGraphMcpTools.cs` | 远程 list/get/validate/apply/preview | Tactics Authoring MCP / Application 作者合同 | partial | 六工具、八类 typed TS spec、batch validate、Create/Duplicate `initialSnapshot`、同批 rebind/Delete、单 Undo 与 fail-closed；Workbench 顶层固定 Map/Event/Skill & Presentation | canonical Editor 实连人验 |

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
