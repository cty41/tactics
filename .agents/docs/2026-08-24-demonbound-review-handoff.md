# Demonbound 恶魔失控形态 — Code Review 结论与交接（2026-08-24）

> 给接手的开发 Agent：本文是本次"未提交改动 → code review → 按功能分组提交"的完整交接文档。
> 上游权威：`.agents/docs/demonbound-class-design.md`（`DEMONBOUND-POSSESSED-*` 六份合同，均 `verified_current`）、
> `.agents/plans/demonbound-possessed-form-implementation.md`、`.agents/docs/manual-acceptance.md`（人工验收账本）。

## 一、背景

- 任务：工作区遗留 39 个未提交文件（28 改 + 11 新），要求 code review 通过后按功能（by function）分组提交。
- 方式：三组并行只读审查（Demonbound 核心 / Battle·会话 / Pure Run Layer-4·结算）+ 文档·知识·账本组自审；所有被引用调用方均已核对。
- 结果：发现 3 个必须修问题 + 5 个建议项修复，全部修复并验证后按 3 个功能提交落地。

## 二、Review 发现与修复（8 项，全部已验证）

| # | 问题 | 严重度 | 修复 |
|---|---|---|---|
| 1 | **结算软锁**：`PureRunSessionService.BeginEncounter` 与 `PureRunFullRunService.BeginBattle` 的 checkpoint 未过滤永久死亡成员，而工厂跳过死者 + `PureRunSettlementService.Validate` 严格按 `Checkpoint.Party` 比对 → 永久死亡后任何后续战斗（楼层 1-3、Layer-5/6/Boss）软锁 `run.result_invalid_party` | 🔴 必须修 | 两处统一 `Where(c => !c.IsDead)`，对齐 `PureRunLayerFourNodeService.BeginNodeBattle`；补 `BeginEncounter`/`BeginBoss` 带墓碑入口集成测试 |
| 2 | **IsHostile 行为回归 + 策略不一致**：诱饵排除（`IsNonActingDecoy`）被放在最顶层，普通敌人也无法攻击诱饵（Decoy 挡刀失效）；AI Standard 分支与技能合法目标判定不一致，违背合同"AI 候选与技能消费同一目标策略" | 🔴 必须修 | 排除收窄到唯一需要的场景——`IsPossessed` 统一池；普通分支恢复阵营判定（`PlayerNumber`）。附身 AI（`UnifiedAll`）与技能判定现在共享同一规则 |
| 3 | **账本重复稳定 ID**：`.agents/docs/manual-acceptance.md` 的 `Last Emitted Order` 三行引用同一 `MQA-GODOT-DEMONBOUND-POSSESSION` → agent-policy 门禁红 | 🔴 必须修 | 合并为一行并保留三方面描述；agent-policy 17/17 转绿 |
| 4 | 永久死亡后处理器对**自身/召唤物**也掷随机数并打无效标记（浪费 RNG、语义噪声，违背"非正式队员不参与正式生死结算"） | 🟡 建议修 | 加 `InstanceId == actor` 与 `SummonOwnerId is not null` 守卫 |
| 5 | 技能投影同级 tie-break 依赖字典枚举序（非语言契约） | 🟡 建议修 | 追加 `ThenBy(ContentId.Value, Ordinal)` 显式 tie-break |
| 6 | 结算 `Validate` 缺 `result.Party` 与 checkpoint 的**集合一致**检查（数量相同但成员不同会打穿到 `MergeParty` 的 KeyNotFound） | 🟡 建议修 | 加成员集合相等校验 |
| 7 | `MergeParty` 用 `survivors.Concat(tombstones)` 把墓碑挪到队尾，破坏 `state.Party` 原顺序 | 🟡 建议修 | 按 `state.Party` 原顺序映射合并；注释明确墓碑物品保留是有意行为 |
| 8 | `DemonboundPossessedAi.For` 的 `active` 参数为死参数 | 🟡 建议修 | 删除参数并更新 3 处调用 |

另修复文档不一致：`.agents/knowledge/operations/project-documentation.md` 原写"实现与验证尚未排期"，与设计文档（六合同 `verified_current`）及缺口清单矛盾，已更新为一致表述。

## 三、未修记录项（供后续开发 Agent 决策，不阻塞）

1. **S3 DRY**：`SkillRuntimeService` 与 `BattleTransitionService` 各有一份行为相同的私有 `ApplyDefeat` 方法，建议后续统一到单一出口（`BattleDefeatResolver` 已是接收端）。
2. **S5 Boost delta 近似**：`DemonboundPossessedBoostService` 用增量叠加（旧派生 + delta）而非合同字面"从强化后属性纯派生重算"；`UnitDerivedStatRules.Calculate` 为公式计算，Explicit 模式下手写值（Move/Initiative）可能不一致。当前被 `DemonboundPossessedBoostTests` 锁定，需精确化时另立小项。
3. **附身 initiative 不重排**：数值已按强化后属性重算，但 `BattleState._turnOrder` 固定数组不因 `WithInitiativeChanged` 之外变化；当前"出手顺序当轮不变"。若要求"附身即刻改变后续轮次顺序"，需接入 `WithUnitAndInitiative`（`BattleTransitionService.cs` 已有该辅助），并重新校准 30-seed 探针。
4. **tick 致命伤归属**：`ApplyEndTurn` 对 0-damage 状态也写 `tickSourceId`（逻辑正确、可读性差）；tick 进永久死亡属合同范围外加固。
5. `IsHostile` 的 `state` 参数未使用（公共 API 预留）；技能投影目前无大师技能显式拦截，若目录将来新增大师定义需复核。

## 四、提交记录（worktree 已干净）

| Commit | 内容 | 规模 |
|---|---|---|
| `10865506` | `feat(core): demonbound possessed form rules, AI and settlement` | 18 文件，+1036/-57 |
| `3a459782` | `feat(run): demonbound possessed application, run integration and godot presentation` | 11 文件，+244/-15 |
| `e92ee682` | `docs(agents): demonbound possessed form contracts, plan, knowledge and acceptance`（含 8 个 OKF scope 指纹同步、incidents、账本修复） | 18 文件，+295/-52 |
| `<本文件>` | `docs(agents): export demonbound review handoff` | 1 文件 |

## 五、验证（全绿）

- `dotnet build Tactics.Godot.slnx`：0 警告 0 错误。
- `Tactics.Core.Tests`：**180/180**（含新增正向永久死亡全路径用例；30-seed 探针在 IsHostile 修复后保持稳定）。
- `Tactics.Application.Tests`：**187/187**（含新增 BeginEncounter/BeginBoss 带墓碑入口用例）。
- OKF `validate_bundle`：17 concepts OK；`agent-policy`：17/17 OK；`validate-skills.ps1`：13 技能（1 条既有 warning：pure-run-artwork 缺 Anti-patterns）。
- 工作区干净；`project.godot` 的无关改动已回退（见遗留 5）。

## 六、遗留待办（交接给开发 Agent）

1. **推送确认**：`origin/main`（`cty41/wooftactics`）领先 4 个提交（3 功能 + 本文档），未推送——需人工确认后 `git push origin main`。
2. **人工验收**（账本 `MQA-GODOT-DEMONBOUND-POSSESSION`，`pending`）：三局人工 Run、30 固定样本批测、状态卡/附身 tint/墓碑在下一局不出现的视觉可读性。验收通过后按 `manual-qa-handoff` 更新账本为 `passed`，并执行 plan 收尾（迁移长期知识、更新 known-gaps、删除 completed plan、OKF sync）。
3. **死亡来源存档字段**：`RunCharacterState` 正式持久化字段（涉及 V1–V10 存档语义哈希）单独立项；当前由 `RunPermanentDeathRolledEvent` 事件溯源支撑。
4. **统一 verifier**：GameplaySpec journeys GdUnit 套件在 Godot 4.7 mono native-host 上的挂起（incident `gdunit-gameplay-journey-native-host-hang`，`reproduced`）需引擎/工具链层面跟进或给 verifier 加恢复策略；当前按 incident 记录 non-blocking（GameplaySpec 报告 20/20，套件更早独立运行 29/29 通过）。
5. **project.godot 无关改动**（`config/name` 改 "tactics" + 删除 `window/stretch/aspect="keep"`）：已随 feature 提交回退并保存 diff（本机临时目录，可向会话索取）。若团队决定落地，注意 `config/name` 会改变 Godot 4 `user://`（app_userdata）目录 → `user://pure-run/save-v1.json` 存档与音频设置脱节。
6. **旧 in-flight V6 存档**：含死亡成员的 checkpoint 会命中 `run.result_invalid_party` 拒绝（`RunSaveNormalizer` 不解构 checkpoint 语义）——开发期重置存档即可，非缺陷。
7. 未修记录项（第三节）按团队优先级决定是否推进。

## 七、给开发 Agent 的起点指引

- 先读：`.agents/docs/demonbound-class-design.md`（六份合同）→ `.agents/plans/demonbound-possessed-form-implementation.md` → `.agents/docs/manual-acceptance.md`。
- 验收后收尾顺序：人工验收账本 → plan 收尾（`plan-mode-plan-writer`/`project-doc-organization`）→ OKF `catalog_impact.py sync` → 提交。
- 门禁基线：`Tools/godot/Verify-GodotProject.ps1`、`Tools/okf/validate_bundle.py`、`Tools/agent-policy` 单测、`.agents/scripts/validate-skills.ps1`。
- 分层与确定性约束：Core/Application 不得引用 Godot；随机一律走 `DeterministicRandom(state.RandomState)`；修改任何被 30-seed 探针锁定的路径需同步校准探针。