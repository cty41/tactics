# 纯 Run 三人小队原型设计

## 文档状态

- 当前设计真相源，按 2026-07-15 仓库实现收敛。
- 只描述已启用的 Pure Run v1；未来技能、掉落和元进度见 [项目已知缺口](project-known-gaps.md)。
- 旧 5×4 自由探索、回溯和节点重入方案已经被 7 层单向路线替代。

## 定位与边界

Pure Run 是一局内完成的固定三人小队构筑体验：玩家在有限路线选择、战斗和服务节点之间取舍，成长只在当前 run 内生效，不带到局外。

当前固定队伍按稳定顺序创建：

1. 法师 `RoleType.Mage`
2. 死灵法师 `RoleType.Necromancer`
3. 亚马逊女战士 `RoleType.Amazon`

三人均从等级 1、六项整数属性和速度均为 5、0 金币、空背包开始。`runSeed` 为地图、起始技能分支和后续确定性候选提供共同随机源。

## 地图结构

`RoguelikeMapGenerator.GetPureRunMap` 生成起点加 7 个可玩层。路径只沿 `outgoing` 方向推进，已访问节点永远不能再次选择。

| 层 | 节点结构 | 作用 |
|---|---|---|
| 0 | Start | 初始化并揭示第 1 层 |
| 1–3 | MinorEnemy | 必经普通战斗 |
| 4 | MinorEnemy / RestSite / Store | 战斗与服务竞争 |
| 5 | EliteEnemy | 必经精英战斗 |
| 6 | EliteEnemy / RestSite / Store | 精英战斗与服务竞争 |
| 7 | Boss，配方 `Special` | 单局终点 |

因此一局实际包含 5、6 或 7 场战斗。`MapRevealSystem` 只将当前节点的一跳后继设为可选；更远节点可以进入雾中预览，但不可点击。

## 运行状态

### 地图状态

`RoguelikeMap` 和 `RoguelikeMapRuntimeState` 保存：

- `layoutVersion`、`runSeed`、当前节点和已访问路径；
- 已完成战斗节点集合与不重复累计的胜场数；
- 各商店节点已购买商品；
- 当前可达性和节点消费状态。

旧布局版本、已完成 Boss 的存档或无有效 run 时会生成新地图。

### 队伍状态

`PlayerAdventureState` 保存 roster、当前三人队、金币、背包、装备、已学技能、属性点和待带入下一场战斗的 Buff。Pure Run 使用 `Version = 3`、`IsPureRun = true`。

### 战斗往返

进入战斗前记录待处理节点和返回场景。战斗胜利后才提交战斗节点完成、胜场和成长；失败不能伪造胜场。返回地图后依据当前节点重新计算后继可达性，不能重新选择已完成节点。

## 成长规则

- 每次 Pure Run 战斗胜利只提升一名存活角色。
- 选择规则为“最低等级优先”，并以固定队伍顺序打破同级平局。
- 获选角色等级 `+1`、属性点 `+1`；最高等级沿用 `SkillSystem.MaxCharacterLevel = 12`。
- 每名角色的起始分支由 `runSeed + RoleType + partyIndex` 稳定选择，并直接学习对应基础技能 Lv1。
- 升级候选默认生成 3 个合法技能；角色主分支属性达到 7 后，尚未获得高级技能时，对应高级技能在槽位 0 获得一次保底。
- 保底只在对应高级技能确实出现在候选中时消费，并随角色状态持久化。

完整技能表及前置关系见 [三职业首切技能设计](three-class-skill-design.md)。

## 节点与经济

### 战斗节点

遭遇由显式配方 `N1–N6`、`E1–E2`、`Special` 和 `runSeed` 解析，不使用运行时威胁预算。结算负责金币、非 Pure Run 经验以及 Pure Run 单人成长。

### RestSite

休息节点通过统一节点结果写回队伍状态；它是与战斗、商店竞争的恢复机会，不承担旧设计中的训练或冥想子系统。

### Store

商店优先使用节点 `storeConfig` 中的显式商品；没有有效配置时，从 `EquipmentDatabase` fallback 生成 2–3 件商品：

- 每个槽位默认从 Common 池选择，并有 30% 机会尝试 Rare；
- 一次商店最多出现一件 Rare；
- 同一池仍有未选候选时避免重复；
- 购买状态按节点保存，重新打开不会恢复已购商品。

### Treasure 与 Mystery（当前 Pure Run 路线不生成）

通用地图模型和编辑器仍支持 Treasure 配置以及 Mystery 事件图，但 `GetPureRunMap` 当前 7 层路线不会生成这两类节点。它们不属于 Pure Run v1 成功路径；若其他地图使用，结果应通过统一奖励链写回 `PlayerAdventureState` 后再提交节点完成。

## 当前成功标准

- 同一 `runSeed` 生成相同路线、起始分支和确定性技能候选。
- 玩家只能选择当前层合法后继，不能回退或重复消费节点。
- 一局能从 Start 推进到 Special，并正确经历 5–7 场战斗。
- 战斗胜利仅成长一名最低等级存活角色；失败不成长。
- 场景往返后地图、队伍、金币、技能、装备和待生效 Buff 保持一致。

## 非目标

- 5×4 自由探索与回溯地图；
- 局外元成长；
- 已定版的大师技能或通用被动池；
- 已定版的战后奖励三选一、装备掉落或消耗品系统；
- 多区域长流程。

## 实现与验证入口

- 地图：`RoguelikeMapGenerator`、`RoguelikeMapRuntimeState`、`NodeStateManager`。
- 状态：`PlayerAdventureStateStore`、`PureRunProgression`。
- UI：`RoguelikeMapUIController`、各节点 handler。
- 自动化：`FirstSliceSkillCatalogTests`、`RoguelikeMapEditorTests`、`Tests/gameplay-specs/map/`。
