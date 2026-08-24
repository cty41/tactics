# Demonbound 恶魔失控形态实现计划

## Summary

目标:将魔剑士"腐化满 → 恶魔失控形态"从当前审计完成度(旧规格约 80%、目标规格约 55–60%)实现到可验收状态。目标规格已由 2026-08-22 grilling 收束并注册为 6 份 `DEMONBOUND-POSSESSED-*` 合同:六维+5 派生重算、已学技能临时升满、敌友统一候选池、幸运修正永久死亡、墓碑记录、缺员继续。

成功标准:全部 6 份合同从 `approved_target` 升级为 `verified_current`;P1 规则层、P2 AI/技能投影、P3 标识与人工验收按序完成;契约、代码、测试、OKF 一致。

明确不做:三个大师技能;召唤恶魔;正式角色美术、完整 VFX/音频(资产层形态差异化);非战斗场景的附身表现(附身仅限战斗内)。

## Current State

- **2026-08-22 实现完成(本计划执行中)**:
  - P1 全部完成:类型化形态状态、六维+5 强化投影、技能升满投影、敌友统一目标策略(TargetRelationshipStrategy)、幸运修正永久死亡后处理(统一 ApplyDefeat)、活跃 Party 与死亡历史分离(战斗工厂/checkpoint 过滤死者 + 结算保留墓碑)。
  - P2 完成:附身 AI 正式化为 Core `DemonboundPossessedAi`(Charger 配置 + 投影技能),固定种子回归通过。未新增静态 Godot AI Resource:附身技能要求动态投影,且 AI 目录为冻结迁移产物(10 个 AI、catalog 计数 166 契约)。
  - P3 完成:状态卡自动反映强化后 HP/MP 与投影技能等级;附身 tint 提升为 `PossessedFormTint` 形态专属配色;冥想按钮 UI 断言(正常可用/变身后禁用)已加入并通过;`POSSESSED` 脉冲标识复用。
  - 6 份合同全部升级 `verified_current`(验证路径指向真实测试文件),`validate-contracts` 通过。
  - Core 179 / Application 185 / FrozenOracle 15 / Godot 非 gameplay(含冥想按钮与形态状态卡断言)127+42 / GameplaySpec 29 项 GdUnit 与 20/20 报告全部通过;合同注册表 valid、OKF 校验、Python 政策门禁(ownership、manual-qa、incidents、compiler 5 项)均通过。
- 待办:死亡来源持久化存档字段(涉及 V1–V10 语义哈希,单独立项);三局人工 Run 与 30 固定样本批测;统一 `Verify-GodotProject.ps1`。
- **统一 verifier 执行记录(2026-08-22)**:用户已强制关闭此前 reload 满载的 Editor;`Verify-GodotProject.ps1` 已执行,全部内容阶段通过——Godot-ai 配置/vendor、依赖还原、全部构建、Core 179 / Application 185 / FrozenOracle 15、GameplaySpec 批编译 21/21、Python 门禁(ownership/incidents/skills/manual-qa/MCP)、compiler 31/32、**全部非 gameplay GdUnit 套件(AiEncounterBatch 11、AuthoringRoundTrip 6、AuthoringTransaction 9、BuffItemBatch 4、CoreGoldenVector 7、EditorReloadLifecycle 6、IsometricBoard 24、PlayableRunUi 41、ReloadSafe 7、StartingSkillBatch 2、UnitBatch 10、page-replacement 1)、GameplaySpec 报告 20/20**。唯一未完整跑完的是 GameplaySpec journeys GdUnit 套件——其 dotnet test 在 Godot 4.7 mono native host 上挂起(Godot 实例退出后 testhost 无限满载,verifier 重试 3 次、独立重跑 2 次均复现),属引擎已知 native-host 不稳定,非改动回归;该套件内容(29 项 GdUnit + 20/20 报告)已通过更早的独立运行完整验证。
- 相关既有计划:`.agents/plans/demonbound-loop-development.md`(持续循环计划,完成人工验收后可合并)。

## Relevant Context

- 权威设计与合同:`godot/project.godot`(唯一 Godot 项目)、`.agents/docs/demonbound-class-design.md`、`.agents/docs/attribute-system-implementation-contract.md`。
- 关键代码:
  - 腐化/附身转换:`src/Tactics.Core/Battle/BattleTransitionService.cs`
  - 战斗状态/恶魔状态:`src/Tactics.Core/Battle/BattleUnitState.cs`(`DemonboundBattleState`)
  - 附身 AI 接管与目标池:`src/Tactics.Application/Battle/PlayableBattleSessionService.cs`
  - 永久死亡判定:`src/Tactics.Core/Skills/SkillRuntimeService.cs`(主伤害路径)
  - 战后结算:`src/Tactics.Core/Runs/PureRunSettlementService.cs`
  - 后续战斗构建:`src/Tactics.Application/Runs/PureRunLayerFourNodeService.cs`、`src/Tactics.Application/Battle/PlayableBattleSessionFactory.cs`
  - AI 目标评分:`src/Tactics.Core/AI/AiDecisionService.cs`
  - Core 技能合法目标:`src/Tactics.Core/Skills/SkillRuntimeService.cs`(`IsHostile`)
- 既有测试:`src/Tactics.Core.Tests/DemonboundMeditationTests.cs`、`src/Tactics.Application.Tests/PlayableBattleSessionServiceTests.cs`、`src/Tactics.Core.Tests/PureRunRuntimeTests.cs`。
- 项目约束:Core/Application 不得引用 Godot;`.tres/.tscn` 只经受测生成链;统一用 `Tools/godot/Verify-GodotProject.ps1`;reload-sensitive 修改经 `godot-editor-lifecycle`。

## Implementation

按依赖顺序,每项独立验收:

### P1 统一状态与伤害结算

1. **类型化恶魔形态状态**(P1.1)
   - `DemonboundBattleState` 升级为携带形态身份/强化配置身份/是否已应用的形态投影;进入形态一次性、幂等。
   - 验收:同场重复事件与 Save/Reload 不重复应用;测试 `DemonboundPossessedFormTests.cs`。

2. **属性强化投影**(P1.2,合同 `DEMONBOUND-POSSESSED-BOOST-001`)
   - 六维各 +5,派生(MaxHP/MP、移动、先攻、命中、闪避、暴击)按统一派生规则重算;当前 HP/MP 按比例保持;战斗结束随 BattleState 丢弃。
   - 验收:变身前/后快照 A/B、Save/Reload 不重复。

3. **技能强化投影**(P1.3,合同 `DEMONBOUND-POSSESSED-SKILL-PROJECTION-001`)
   - 已学技能临时升满,不临时解锁未学/大师;不改 `LearnedSkills`、不写存档;AI 候选、状态卡、战斗日志读同一投影。
   - 验收:投影一致性;AI 用 Lv3 而 HUD 显示 Lv3。

4. **统一目标关系策略**(P1.4,合同 `DEMONBOUND-POSSESSED-TARGET-POOL-001`)
   - 显式目标关系策略替换 `targetOwnFaction` 布尔;`AiDecisionService.Decide` 与 `SkillRuntimeService.IsHostile` 消费同一策略;候选池=存活正式敌我+存活召唤物,排除自身与诱饵;范围技能按失控合法目标计数;保持确定性排序。
   - 验收:敌友同池、混合范围、同 seed 同选择回归。

5. **统一永久死亡后处理**(P1.5,合同 `DEMONBOUND-PERMADEATH-LUCK-001`)
   - 判定移入统一击败/伤害后处理服务;概率=25% 基线,目标幸运 >5 每点 -2%,clamp ≥0;一次存活→死亡转换一次确定性 RNG;范围攻击每个首死目标分别判定。
   - 验收:主伤害、Detonation、MultiStab、RecoverSpear 二次伤害全覆盖。

6. **活跃 Party 与死亡历史分离**(P1.6,合同 `DEMONBOUND-DEATH-RECORD-001`、`DEMONBOUND-SQUAD-SHORTFALL-001`)
   - 战斗构建/checkpoint/出生序/回合序仅含 `!IsDead` 角色;不物理删除,拆分活跃 Party + 已故记录(死亡来源/战斗);常规 UI 隐藏死者;Summary 显示墓碑与来源;缺员继续。
   - 验收:死亡→结算→保存→Reload→下一战无该单位端到端。

### P2 恶魔 AI 与技能强化

- 正式 `AiDefinition`(`ai.demonbound.possessed`)替代 Session 内临时硬编码;敌友统一候选评分 + 击杀价值/自疗/范围覆盖策略;AI 决策配置纳入 Resource、Catalog、Workbench、固定种子回归。

### P3 表现、持久化与验收

- P3 前段:HUD 显示强化后属性与技能等级;附身转换程序标识;永久死亡结算、缺员阵型与 Summary 墓碑表现;冥想按钮 UI 自动断言(正常可用/变身后禁用)。
- **P3.7 形态标识一致性(程序层,本轮做)**:用现有 `BodyTint/Material` 机制做形态专属配色(非通用紫染),进形态切换 tint 材质、退出恢复;状态卡 `POSSESSED`+脉冲保留;标识与强化状态同步纳入人工验收。附身仅限战斗内出现。
- **P3.8 形态美术差异化(资产层,本轮不做)**:形态专属贴图集或挂点 FX/粒子;等待美术预算确认后单独立项,遵守 `pure-run-artwork-pipeline` sprite 合同。

## Test Plan

- 自动:Core NUnit(新增 `DemonboundPossessedFormTests.cs` 及既有测试扩展)、Application NUnit、Godot GdUnit(含冥想按钮断言)、Gameplay Spec validate/compile、统一 `Verify-GodotProject.ps1`。
- 数值:单变量 same-seed A/B,由用户决定采用或回退。
- 人工:三局完整 Run;形态标识可读性(腐化条/POSSESSED/强化状态同步)与缺员/墓碑体验;写 `manual-acceptance.md`,自动证据不得替代人工通过。

## Risks / Assumptions

- 附身会话构造时 AI 自动推进会改变状态快照:测试只断言目标行为,不依赖具体 AI 动作。
- 强化倍率等细节已收束(+5 固定、HP/MP 按比例、技能升满、幸运 -2%/点、无下限 clamp 0)。
- 目标池争议点(召唤物/诱饵)已按"正式敌我+存活召唤物,排除诱饵"定稿。
- 若反射注入私有字段在测试中脆弱,可降级为 presented 快照隔离断言并在文档说明。

## Handoff Notes

- 先读:`.agents/docs/demonbound-class-design.md`(含新合同)、`.agents/docs/attribute-system-implementation-contract.md`、本计划、`.agents/knowledge/operations/godot-agent-workflow.md`。
- 先记录精确 dirty scope(`godot/project.godot` 未提交修改不得触碰),隔离 unrelated artwork/文档改动。
- Editor/reload-sensitive 修改必须经 `godot-editor-lifecycle` 正常关闭并恢复。
- 每项 P1 完成后同步合同状态、测试、OKF;全部验收完成后升级 6 份合同为 `verified_current`,按 `project-doc-organization` 迁移长期知识、更新受影响 OKF scope、删除本计划。