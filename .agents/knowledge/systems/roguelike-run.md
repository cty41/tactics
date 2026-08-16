---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/RoguelikeMap
title: Roguelike Run
description: 7 层只前进地图、节点交互、冒险状态和三人小队局内成长主链。
tags: [gameplay, roguelike, map, progression]
timestamp: "2026-08-16T12:55:23+08:00"
status: active
catalog_scope: roguelike-run
repo_paths:
  - .agents/docs/2026-06-24-pure-run-squad-prototype-design.md
  - .agents/docs/roguelike-event-editor-design.md
  - .agents/docs/roguelike-map-editor-manual-test.md
  - Assets/Tactics/Scripts/Common/RoguelikeMapGenerator.cs
  - Assets/Tactics/Scripts/Roguelike/RoguelikeMapRuntimeState.cs
  - Assets/Tactics/Scripts/Roguelike/PureRunSessionStore.cs
  - Assets/Tactics/Scripts/Roguelike/PureRunSummaryRecorder.cs
  - Assets/Tactics/Scripts/Common/Roster/PlayerAdventureState.cs
  - Assets/Tactics/Scripts/Common/Roster/CharacterDefinition.cs
  - Assets/Tactics/Scripts/Common/Roster/PlayerAdventureStateStore.cs
  - Assets/Tactics/Scripts/UI/RoguelikeMapUIController.cs
  - Assets/Tactics/Scripts/UI/InventoryUIController.cs
  - Assets/Tactics/Scripts/UI/LevelUpPanelController.cs
  - Assets/Tactics/Scripts/UI/HomeUIController.cs
  - Assets/Tactics/Scenes/Home.unity
  - Assets/Tactics/Arts/UI/Inventory.uxml
  - Assets/Tactics/Arts/UI/Inventory.uss
  - Assets/Tactics/Scripts/Editor/RoguelikeEventEditor
  - Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
  - Assets/Tactics/Tests/Editor/RoguelikeMapEditorTests.cs
  - Assets/Tactics/Tests/Editor/HomeSceneCompositionEditorTests.cs
  - Assets/Tactics/Tests/PlayMode/HomeSceneInputSmokeTests.cs
  - Tests/gameplay-specs/ui/home-options-player-input-smoke.gameplay-test.md
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:0876ab4b0368e4edf53d80bb4163e46cbe5dcbbe162c1a334e7b16a2f104077e
---

# Current State

Pure Run v1 由 `RoguelikeMapGenerator.GetPureRunMap` 生成 7 层只前进地图，单局实际战斗数为 5、6 或 7；第 4、6 层均在战斗、休息、商店和随机事件之间四选一。节点沿 outgoing 揭示，已访问节点不会重新可选。地图布局版本为 2。

Demo 使用单一全局 Run，不经过三存档槽。`PureRunSessionStore` 将版本 5 冒险状态与地图作为配对数据保存；Home 提供 New Run 和 Continue Run。普通战斗胜利结算后回到地图，失败或 Boss 胜利显示 RunEndSummary 并清理本局状态。

Godot Phase 6B 只迁移 N1→N2→N3 三战垂直切片，不宣称完整七层 Run parity。它使用单一 `user://pure-run/save-v1.json`，以固定字段顺序 JSON、payload SHA-256 和单调 revision 保存 active run 与 terminal summary；写入先生成并重读 temp，再保留最后有效 backup 并提升，主档损坏时回退 backup，双档损坏时以内容 hash 前缀隔离证据且不静默覆盖。每场战斗前先持久化完整队伍 checkpoint；进程退出后重开同一 Encounter，而非恢复逐回合 BattleState。

Godot Run 层不结算战斗命令，只发出带 Run/revision/Encounter 的请求并消费验证后的 `BattleResult`。胜利奖励、恢复、死亡卸装、击杀和掉落由冻结规则重新计算，稳定 transaction key 防止重复结算；N1/N2 推进，N3 产生明确的 `SliceCompleted` 而非 Boss Victory。失败、放弃与完成摘要在 active run 清除后保留到显式消费；成长只记录最低等级存活角色的 pending identity，尚不应用等级、属性或技能。

Godot 后续垂直切片已扩展到完整七层 Run，并在 Phase 8E 加入只读 Rogue Map UI 投影。固定图由 Start、N1–N3、Layer 4 四路线、Layer 5 Elite、Layer 6 四路线和 Special Boss 共 14 个节点及 19 条只前进连接组成；节点可视状态完全来自 Save V4 的 Run phase、MapState 和 NodeTransaction。Main 的地图点击只调用既有 Application intent，结算、成长、Inventory 和节点功能页返回同一流程 Shell；选择四选一路线后兄弟节点锁定，PendingBattle、Store/Mystery 处理中和 Boss terminal 都按存档恢复。Godot 使用程序化 Control 等价实现连线、状态色、Hover、拖动与首次当前节点居中，不复制 Unity UXML/USS 或视觉 payload。

Home 磁盘场景已精简为 `AudioListener`、`Bootstrap`、`EventSystem`、`Main Camera` 四个无子节点静态 root，不包含 Grid、Tilemap、UnitManager 或战斗单位；Home UI 仍由生产 `GameAssetManager`、`HomeFlowCoordinator` 和 `UIManager` 在运行时创建。Editor 结构测试始终通过 `OpenPreviewScene` 验证磁盘资产的精确 roots、组件白名单和禁用玩法组件。独立 PlayMode source spec 经 compiler 生成 plan，以虚拟 Mouse 通过生产 `PlayerInput` 点击 `OptionsButton`，断言 `OptionsRoot` 存在且可见，并覆盖测试设备释放；该 smoke 与较长旅程夹具隔离。

Home smoke 同时守护中文 runtime FontAsset 的动态 atlas、隐藏/重开、直接打开另一个 UI 时的可修复资源标志、静态引用丢失后的 owner 恢复、无 provenance owner 隔离，以及完整或部分共享 FontAsset/Material/atlas 时的引用感知清理；未使用 atlas 容量尾槽不会被当作自有资源销毁。修改 Home UI 字体或 UIManager 生命周期时，先运行 `PlayerInputGameplayPlanTests`，再运行完整 `HomeSceneInputSmokeTests`；这些自动测试模拟资源边界，不宣称单个 UnityTest 真正跨越 Play Mode 退出边界。

新建 Pure Run 角色的主属性为 6（法师智力、死灵法师魅力、亚马逊敏捷），其余属性为 5；既有存档不重写。升级时先强制完成属性点分配，再依据更新后的属性计算并展示技能候选；技能选择界面同时列出已学技能及每级实际 MP 消耗。地图每次回显后只根据当前进度节点执行一次 ScrollView 居中，之后仍由玩家自由拖拽浏览。

新建角色以当前魅力值而非最大 MP 开始本局；进入首场战斗时该持久化 MP 会覆盖单位初始化默认值。已有存档中的已保存 MP 保持不变。

Pure Run 的 Mystery、Rest 与 Store 通过持久化节点事务保护中断恢复。事务按 `Entered → Resolved → Committed` 推进，并在冒险状态中记录已应用的奖励键：效果结算前先保存 `Resolved` 快照，重入时恢复同一结果并只补发尚未应用的效果，只有继续/关闭等明确完成动作才提交和消费节点；商店按商品使用独立购买键，因此崩溃或返回地图后不会重复扣款或重复发货。

第 4、6 层的两个 Mystery 节点会由 run seed 确定性分配互不重复的正式事件，并在旧存档缺失分配时补全而不覆盖已有值。正式事件为诅咒宝箱、堕落祭坛和迷途村民；选项使用稳定 ID，检定概率以属性 5 为基准并限制在 5%–95%，检定投点由 run seed、节点 ID 和选项 ID 共同决定，因而重入不会重掷。事件结果页可跨重载恢复，事件致死会在提交后进入战败结算。

`CreatePureRunState` 建立法师、死灵法师和亚马逊固定三人队，等级 1、七项基础属性 5。每次胜利只让一名最低等级存活角色获得 1 级和 1 属性；起始分支主属性达到 7 时，高级技能有一次候选保底。

Pure Run 存档修复将已知旧等级技能 ID 迁移为稳定逻辑 ID、合并重复记录并保留最高等级。拥有投掷系技能的 Amazon 会幂等获得不占槽的 `amazon.pickup_spear` 持久化记录；其实际战斗拾取行为仍属于 Amazon 技能切片。角色 Lv2 起即可同时看到合法新技能和已学技能的已发布下一等级，选择升级后同一 `LearnedSkill` 等级会进入下一场战斗绑定。

LevelUp 面板按实际 `LearnedSkill.Level` 显示当前技能和混合候选，候选明确区分 Lv1 新技能与下一等级升级，并读取对应等级资产描述。Inventory 的技能区是只读视图：主动技能优先、被动技能随后，显示实际等级；点击仅打开详情 popover，不提供装配、卸载或替换操作，`ExtraUtility` 与其他地图隐藏技能不显示。

Inventory、LevelUp、BattleSettlement 与 RunEndSummary 在每次显示时重新绑定当前 UIDocument 的 VisualElement 树，并在隐藏时注销回调、清除旧树引用。Inventory 因而可以在同一缓存实例上反复打开，且会重新读取隐藏期间发生的角色和背包状态变化。

地图层待生效 Buff 快照除名称、持续时间和正负面外，还持久化效果/触发类型、诅咒分类、周期伤害、伤害大类、元素、刷新策略、速度修正和减伤比例；进入战斗时按这些字段还原运行时配置。旧存档缺失伤害大类时按 `Magic` 补全，避免升级后改变既有事件 Buff 的语义。

事件编辑器当前支持 UI Toolkit 图编辑、Inspector、Preview、搜索、连线、删除及 JSON 导入导出；进阶编辑效率和专用测试仍属于缺口。

消耗品按定义、加权池和独立实例三层组织。首批为 `1/1` 的生命药剂、魔法药剂和净化药水；角色各有 1 个携带槽，未携带实例与装备共同显示在统一 Inventory，并通过单击 popover 执行携带、装备、一步替换或卸下。角色死亡在战斗结算或事件应用后自动卸下全部装载。

新局不自带消耗品。普通/精英胜利分别按 25%/30% 概率掉落，Boss 不掉落；每个商店确定性展示 3 件商品、至少 1 件且不重复同一种药水，事件只在配置明确指定时发放。获得反馈显示名称与次数，地图顶部没有消耗品总数或新物品角标。

进行中的 `RunSummary` 与 Pure Run 状态共同持久化，所有奖励、节点和击杀流水都使用稳定 transaction key 去重。`totalGold` 只累计实际提交的正向金币，不因购买扣款回退；获得过的装备和物品记录稳定 ID，即使后来装备、花费或使用仍保留。节点仅在提交完成时计数，Mystery 同时增加事件数，正式敌人死亡只在玩家胜利结算时写入。

战斗失败、Boss 胜利和 Mystery 导致的全灭统一先从进行中统计生成结局快照，再清除活动 session。RunEndSummary 读取该快照并解析装备/消耗品显示名，关闭总结时才消费快照；因此 UI 不依赖已经清理的角色背包或地图运行时状态。

真实输入路线从 Home 的 New Run 入口开始，以生产鼠标/键盘完成三场自然战斗、三次显式升级、Inventory、Store 和多次场景重入；地图节点使用稳定运行时元素名并可通过真实指针拖动滚入视口。快速 `journey-integration` 继续覆盖五场胜利、Boss、RunSummary、自然战斗团灭和 Mystery 事件团灭。两层自动化都不把视觉、动画和操作手感判断伪装成逻辑断言，这些只留给最终人工验收。

# Relationships

- 战斗节点进入[Battle System](battle.md)并在结算后返回地图。
- 技能成长由当前三职业目录与[SkillGraph](skill-graph.md)承接。
- 地图 seed、成长和节点状态可由[Gameplay Test Framework](gameplay-test-framework.md)验证。
- 战斗内消耗品通过[SkillGraph](skill-graph.md)复用目标合法性和效果执行。
- 未实施的内容扩展与编辑器增强见[Project Known Gaps](../plans/project-known-gaps.md)。

# Verification Guidance

实现判断核对地图生成、运行状态、节点事务、结算代码、配置资产和测试。Home 磁盘结构通过 `HomeSceneCompositionEditorTests` 的 preview-scene 契约验证；Home Options 生产输入链通过 `HomeSceneInputSmokeTests` 及 `home-options-player-input-smoke.gameplay-test.md` 编译生成的 plan 独立验证。Mystery/Rest/Store 的自动化验证必须覆盖 Resolved 状态重载、奖励幂等和最终提交；最终玩家流人工验收使用可复现操作与状态结果，不使用截图证明功能。

# Citations

[1] [Pure Run design](https://github.com/cty41/tactics/blob/main/.agents/docs/2026-06-24-pure-run-squad-prototype-design.md)
[2] [Roguelike runtime](https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/RoguelikeMap)
