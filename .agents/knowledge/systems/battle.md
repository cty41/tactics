---
type: Game System
resource: https://github.com/cty41/tactics
title: Battle System
description: Godot Pure Run 的棋盘、回合、技能、状态、AI 合法性、结算与表现投影主链。
tags: [gameplay, battle, turn-based, godot]
timestamp: "2026-08-18T19:10:03+08:00"
status: active
catalog_scope: battle-system
repo_paths:
  - .agents/docs/battle-line-of-sight-rules.md
  - .agents/docs/attribute-system-design.md
  - .agents/docs/buff-system-rules.md
  - .agents/docs/three-class-skill-design.md
  - src/Tactics.Core/Battle
  - src/Tactics.Core/Board
  - src/Tactics.Core/Pathfinding
  - src/Tactics.Core/Skills
  - src/Tactics.Application/Battle
  - src/Tactics.Core.Tests/BoardAndRulesTests.cs
  - src/Tactics.Application.Tests/PlayableBattleSessionServiceTests.cs
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunMain.cs
  - godot/tests/CoreGoldenVectorGodotTests.cs
verified_revision: 04c75ec4
source_fingerprint: sha256:01aa83b6474e9179320a8196c1badc4d9bcde0fc786afdb7469d417f9bf496b7
---

# Current State

当前产品战斗主线分为三层：`Tactics.Core` 持有不可变战斗状态、命令、事件、技能解释、回合、路径和视线；
`Tactics.Application` 组合遭遇、玩家意图、AI 候选和 UI Snapshot；Godot Adapter 只负责输入、Resource 映射、
棋盘绘制和 committed event 表现，不重新裁决玩法结果。

固定战场使用 10×10、零基坐标。单位实例身份使用 `UnitInstanceId`，不能用内容 `ContentId` 代替。合法性预览、
AI 和真实 Transition 必须复用 Core 规则；表现 cue、Tween、伤害数字和 Sprite 姿态不能修改战斗状态。

## Line of Sight

当前 LoS 合同为 `godot-los-shadow-cone-v1`，完整规则见 `.agents/docs/battle-line-of-sight-rules.md`。
阻挡格的开放内部从观察者方向向后形成遮挡锥：目标中心射线进入格内才阻挡，仅擦过格边或格角时放行。
中间存活单位和阻挡地形参与判断；施法者、最终目标、尸体与落矛不作为普通阻挡物。Bone Spear 保留首敌
截获语义。`LineOfSightResult` 为 Godot 悬停详情提供最近阻挡格、类型和单位身份。

最终 Unity 的 diagonal supercover 仍保存在 FrozenOracle、Golden 和 `oracle-matrix.json` 中，作为退役产品的
历史事实；`contract-decisions.json` 显式记录它已被当前 Godot 合同替代，不能再约束当前 Core 行为。

## Battle Runtime

Adventure 事件战的随机状态会从 checkpoint revision 中扣除 Adventure 专属 revision，确保领队移动、路线点击和场景切换不会改变相同 run seed 的战斗序列。护送 NPC 的生命值读取正式 UnitDefinition 派生值，不再使用硬编码 12 HP；其存活、团灭与事件失败仍由现有 Protected NPC 胜负规则裁决。

战斗命令失败时保持源状态并发出稳定 rejection；成功状态在表现事件消费前已经完整。技能次数、Mana、移动、
状态触发、召唤上限、尸体消费、长矛和战后结果均由 Core/Application 事务维护。Godot 只消费 Snapshot 和事件，
缺失表现资源或取消表现不得改变命中、伤害、状态与终局。

Pure Run 三职业、敌人、召唤物和固定遭遇均从 Godot-owned Catalog/Resource 组合；N6/E2 使用的
`battle-layout.pure-run.split-flank` 已由正式 `BattleLayoutResource` 提供，不再由运行时硬编码补建。内容
ownership 与人工验收分开记录；自动测试通过不能把视觉或操作验收直接标为 passed。

魔剑士 `Demonbound` 作为第四名开局候选但仍维持三人参战。Core 保存战斗局部 0–10 腐化、正念等级、冥想资格与附身控制状态；附身只切换控制者、不改变阵营，AI 优先攻击存活队友并在队友全 Down 后回退敌人。友军致命伤只进行一次确定性 25% Run 永久死亡判定；仅剩附身魔剑士且敌人全灭仍是玩家胜利。Godot 左上状态卡只绑定当前行动者，复用 Unit Resource 显示头像/名称/HP/MP，并由 nullable Corruption 投影可选连续特殊资源条；Hover/LOS 详情迁入鼠标旁输入穿透浮层。结构与语义已自动覆盖，视觉可读性仍待人工验收。

厄运魔刃使用相邻方向输入并按近到远命中前方两格；墙体和第一格单位都不截断半月斩。Application 保存装备投影后的主属性伤害加值，只有显式启用缩放的技能读取该值。表现层把 Bane 编译为近战挥剑 Cue，并在半月斩抵达第一、第二格时依次插入受击和数字；规则提交仍早于表现，表现暂停或取消不改变结算。

固定种子数值循环已有 Core 规则层诊断代理：三种 Demonbound 队伍标签各跑相同 10 seed，复用正式 `AiDecisionService`、`AiTurnService` 与 `BattleTransitionService`，记录终局、腐化峰值、冥想、首次附身、友伤、Down、永久死亡和技能次数，并验证同 seed 重放一致。其无尸体诊断夹具和简化队友策略只用于证明采样管线及发现规则问题；未接入生产 Run 路线、Resource 数值和完整职业策略前，不得视为完整平衡证据或人工体验替代。

# Relationships

- 技能数据和执行图见 [Skill Graph](skill-graph.md)。
- 敌方候选与模式见 [Monster AI](monster-ai.md)。
- 战斗外状态和结算返回见 [Roguelike Run](roguelike-run.md)。
- Frozen Unity 到 Godot 的历史证据边界见 [Godot migration](../plans/godot-migration.md)。

# Verification Guidance

先运行 Core/Application 定向测试，再运行 `Tools/godot/Verify-GodotProject.ps1`。涉及输入、目标高亮、悬停信息
或实际战斗体验的改动，在自动门禁后仍需更新 `.agents/docs/manual-acceptance.md` 并由用户人工验收。

# Citations

- [Mewgenics Range and Area](https://mewgenics.wiki.gg/wiki/Range_and_Area) — 遮挡锥方向的外部机制参考；逐格边界由本项目合同定义。
