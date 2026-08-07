# Pure Run 当前随机池基线试玩协议

## 1. 目的与基线声明

本文用于建立 **Pure Run 当前实现的体验基线**，回答三类问题：一局是否可完成、不同遭遇的战斗压力如何、玩家是否能理解并主动改变策略。

> **重要：这是当前“每个普通战斗节点都从 N1–N6 全池按 `runSeed + nodeId` 取配方”的随机规则基线，不是未来按层级/进度分层后的稳定难度基线。**

当前普通池中的 N1/N2 为 3 个敌方本体，N3–N6 为 4 个敌方本体；它们可在任一普通战斗节点出现。因此早期压力、后期压力和路线间差异会混入“全池抽取”效应。数据可用于记录当前状态、发现极端样本和形成分层前假设，但不得直接解释为“第 X 层的稳定难度”。精英节点仍从 E1/E2 中选择，Boss 使用 Special。

本阶段只证明 **3 局手工记录是否可行**。不新增分析脚本、不接生产 telemetry，也不为了填表修改运行时代码。

## 2. 记录单位与约定

- CSV **每行代表一场战斗中的一名正式玩家角色**。同一场战斗通常写 3 行。
- 战斗级字段（例如 `result`、`rounds`、队伍总 HP/MP、双方有效行动数、评分）在该战斗的 3 行中重复；分析战斗级胜率、回合数和评分前，必须按 `build_id + tester + run_seed + node_id` 去重，不能把 3 个角色行计成 3 场战斗。
- Run 级字段 `run_result` 在整局结束后回填到该局全部角色行；分析完整 run 通关率前，必须按 `build_id + tester + run_seed` 去重，不能按战斗行或角色行计数。
- 角色级字段（`character`、`learned_skills`、`successful_skill_uses`、`basic_attack_uses`）只写该角色的数据。
- 推荐唯一键：`build_id + tester + run_seed + node_id + character`。同一测试者重跑相同组合时，在 `build_id` 后附本地序号，避免覆盖。
- 多值字段使用 `|` 分隔；字段内容包含逗号、双引号或换行时必须遵循标准 CSV 引号规则。
- 空白表示“没有可靠记录或不适用”，不是 0。只有确认没有发生时才填 `0`。

### 枚举与量表

- `experience_level`：`new` / `familiar` / `expert`。
- `result`：当前战斗结果，取 `victory` / `defeat` / `abandoned` / `blocked`。
- `run_result`：完整 run 终局，取 `victory` / `defeat` / `abandoned` / `blocked`；进行中留空，终局确定后回填该局全部行。
- offered/chosen 字段：`1` / `0`；不适用留空。
- `meaningful_choice_score`、`clarity_score`：1–5 整数。
  - meaningful choice 1：没有可辨认取舍；2：看似有选项但存在明显支配答案；3：至少出现一次结果不显然、需要权衡的选择；4：出现多次清楚取舍；5：取舍持续且明显改变战斗计划。
  - clarity 1：无法理解关键状态；2：频繁猜测；3：基本可理解但仍需猜测；4：关键反馈足以决策；5：关键状态和后果始终清楚。
  - 计算“至少一次非显然选择”的战斗比例时，`meaningful_choice_score >= 3` 记为 1，1–2 记为 0；缺失评分不进入分母，并同时报告评分覆盖率。
- `dominant_strategy`：一句短标签，例如 `focus_fire`、`kite_and_burst`、`summon_screen`、`basic_attack_conserve_mp`。

## 3. 字段口径

### 构建、路线与遭遇

- `build_id`：优先填测试包版本或 Git revision；脏工作区测试时写 `<revision>-dirty-<序号>`，不要假装是干净版本。
- `tester`：稳定测试者代号，不写敏感个人信息。
- `run_seed`：本局 `RunSeed`。
- `route`：从 Start 到当前节点的完整已选路径，节点 ID 以 `>` 连接。
- `node_id`：当前战斗节点稳定 ID。
- `recipe_id`：实际配方 `N1`–`N6`、`E1`/`E2` 或 `Special`。
- `encounter_variant`：仅 `Special` 必填实际生成的稳定怪物 ID：`elite_charger` 或 `elite_poison`；N1–N6/E1/E2 的组成已由 `recipe_id` 唯一确定，留空即可。
- `enemy_body_count`：战斗开始时配方生成的正式敌方本体数；不计战斗中召唤物、诱饵和测试对象。
- `standard_equivalent_budget`：**仅供手工横向比较的 HP 体量代理，不是运行时威胁预算**。按 `enemy_body_count × recipe HealthMultiplier` 填写：N1–N6 乘 1.0，E1/E2 乘 1.3，Special 乘 1.8。保留一位小数。该值不包含 OutputMultiplier、AI、能力、地形或召唤，不能单独代表难度。

当前配方速查：

| 配方 | 正式敌方本体 | body count | standard equivalent |
|---|---|---:|---:|
| N1 | 2 Charger + 1 Ranged | 3 | 3.0 |
| N2 | 2 Ranged + 1 Support | 3 | 3.0 |
| N3 | 1 AOE + 2 Charger + 1 Support | 4 | 4.0 |
| N4 | 2 Ranged + 1 AOE + 1 Charger | 4 | 4.0 |
| N5 | 2 Support + 1 Charger + 1 AOE | 4 | 4.0 |
| N6 | 2 Charger + 1 Ranged + 1 AOE | 4 | 4.0 |
| E1/E2 | 分别复用 N3/N4，HP ×1.3 | 4 | 5.2 |
| Special | EliteCharger 或 ElitePoisonCaster，HP ×1.8 | 1 | 1.8 |

### 战斗结果与资源快照

- `rounds`：结算使用的 `BattleController.CurrentRound` 口径；不要根据日志行数自行猜算。
- `player_count`：战斗开始时正式玩家角色数，不计召唤物/诱饵。
- `party_hp_before` / `party_mp_before`：进入战斗并完成初始化后、玩家第一次操作前，正式玩家角色当前值之和。
- `party_hp_before_recovery` / `party_mp_before_recovery`：胜负确定后、战后恢复展示开始前的正式玩家角色当前值之和。
- `party_hp_after_recovery` / `party_mp_after_recovery`：战后恢复应用后、结算成长前的正式玩家角色当前值之和。战败无恢复时留空，不复制 before-recovery。
- `ko_count`：战斗结束时处于 downed/0 HP 的正式玩家角色数。
- `player_effective_actions` / `enemy_effective_actions`：成功提交并产生游戏状态变化或合法资源消耗的行动次数；移动、普通攻击、技能、消耗品各算 1，取消选择、非法目标和纯查看不计。召唤单位行动计入其所属阵营。
- `boss_pattern_cycles_seen`：仅 Boss 战填写；观察到完整 Pattern 从首项回到首项的次数。无法清楚判断时留空并在 `free_notes` 说明。

### 构筑、技能和节点选择

- `character`：稳定角色 ID；如只能看到显示名，先写显示名并在 `free_notes` 标注。
- `learned_skills`：该战斗开始时已学技能及等级，格式 `skill_id@LvN|skill_id@LvN`；基础移动不写入，职业普攻可省略。
- `successful_skill_uses`：该角色按技能记录的成功非普攻施放，格式 `skill_id:count|skill_id:count`，例如 `mage.fireball:2|mage.ice_bolt:1`。只计产生游戏状态变化的合法施放；伤害、治疗、Buff/Debuff、位移、召唤或合法资源变化均可算，取消、非法目标、执行失败和未改变任何状态的空放不计。职业普攻不写入此字段。
- `basic_attack_uses`：该角色成功提交的职业普攻次数。
- `consumables_used`：该角色本战成功消耗的物品，格式 `item_id:count|item_id:count`；确认没有使用填 `0`。
- `rest_offered/rest_chosen`、`store_offered/store_chosen`、`mystery_offered/mystery_chosen`：记录到达当前战斗前最近一个竞争层时对应节点是否可选、是否选择。尚未遇到竞争层或无法确认时留空。战斗的 3 行重复该上下文。

### 主观字段

- `meaningful_choice_score`：本战的战术/资源取舍是否真实影响决策。
- `clarity_score`：本战关键状态、敌方意图、技能可用性、结算与恢复是否足以支持决策。
- `loss_reason`：只在 `defeat`/`abandoned`/`blocked` 时填写。**先写第一判断，再查日志**，例如 `failed_focus_fire`、`mp_exhaustion`、`unclear_targeting`、`early_high_body_recipe`、`input_or_ui_blocker`。
- `free_notes`：记录异常、疑似 bug、关键转折和日志复核后的补充；不要反向改写已经记录的第一主观判断。

计算“技能支配性”时，在声明的同一 build/样本范围内汇总全部角色行的 `successful_skill_uses`：分母为所有技能 ID 的成功非普攻施放总数，分子为其中次数最多的单一 `skill_id`。基础攻击完全排除。总施放少于 10 次时只报告原始计数并标为样本不足，不套用绿/黄/红比例。

## 4. 统一试玩步骤

### A. 开始前（每局）

1. 记录 `build_id`、`tester`、`experience_level` 和计划使用的 `run_seed`。
2. 从 Home 以正式玩家流程创建 Pure Run；不使用 `completeNode`、测试 adapter 或状态注入替代正常操作。
3. 记录起始技能选择。确认 CSV 可写，并启动独立计时，测量“游玩以外的记录耗时”。
4. 除 same-seed A/B 外，不预先查 seed 对应的配方或事件结果，避免改变首见决策。

### B. 地图节点

1. 每次显示可选后继时，先记录 route 和 Rest/Store/Mystery 的 offered 状态。
2. 按自然决策选择节点，并记录 chosen 状态；不要为了补齐字段强制选服务节点。
3. 进入战斗后记录 `node_id`、实际 `recipe_id`；Special 还要记录 `encounter_variant`。随后记录敌方正式本体数、玩家人数、开战 HP/MP 总和及各角色当前已学技能。

### C. 战斗中

1. 使用正常鼠标/键盘游玩，不打开开发命令修改状态。
2. 用纸面计数、计数器或战后立即回忆，记录双方有效行动以及每名角色的技能/普攻/消耗品使用。
3. Boss 战只在能明确辨认 Pattern 时计完整循环；不确定就留空。
4. 不为填表频繁暂停到破坏正常决策；无法低成本记录的字段允许留空，并标记为记录负担。

### D. 胜负与结算

1. 胜负确定时，先记录 `result`、`rounds`、恢复前 HP/MP 和 KO。
2. 胜利时观察固定战后恢复展示，再记录恢复后 HP/MP；之后继续正常结算与升级。
3. 失败/卡住时，**在打开 Console 或搜索日志前**，先填 `loss_reason`、两个主观评分和第一印象 `free_notes`。
4. 再查看 Battle UI、结算页、RunSummary 与 Console/TLog，只把可核实事实追加为“日志复核：…”。日志与第一印象冲突时两者都保留。
5. 一场战斗结束后补齐该战斗的 3 个角色行，检查战斗级字段在三行一致。

### E. 每局结束

1. 记录 Run 终局并把统一的 `run_result`（`victory`/`defeat`/`abandoned`/`blocked`）回填到该局全部行；不要以最后一场战斗的 `result` 猜测整局终局。
2. 记录本局额外填表耗时、留空字段及最难记录字段到 `free_notes`（可放在本局最后一场的三行）。
3. 不用 RunSummary 反推它没有提供的逐战数据；RunSummary 只作累计金币、物品、装备、击败数、访问节点、事件数和终局结果的交叉核对。

## 5. 失败调查顺序

必须遵循以下顺序，避免日志知识污染第一体验判断：

1. 截止操作，保留现场。
2. 先写玩家主观原因、困惑点、当时打算做什么。
3. 截图/录屏如已有工具可用；本协议不要求新增采集设施。
4. 再查 Battle UI 和结算页。
5. 最后查 Console/TLog，并区分“日志明确证明”“日志支持猜测”“日志无数据”。

`TBattleLog` 的内存缓冲最多 50 条，战斗结束会清空；它适合辅助核对 Attack/Skill/Turn 等显示事件，不是完整、持久、可查询的 telemetry。不得把“日志中没看到”写成“没有发生”。

## 6. Same-seed A/B 规则

Same-seed A/B 用于比较策略或单一选择，不用于宣称整场战斗随机序列完全相同。

1. A/B 必须使用相同 `build_id`、`run_seed`、到目标节点的 `route`、起始技能选择和目标 `node_id`。
2. 预先声明唯一变量，例如“竞争层选 Rest vs Battle”或“优先集火 Ranged vs Support”。若路线变化导致目标 `node_id`/配方变化，则该对只比较路线结果，不比较同遭遇微操。
3. 除声明变量外，尽量保持升级选择、属性分配、消耗品装载与之前节点操作一致；无法保持时在 `free_notes` 标出偏差。
4. 至少做成对样本；多人/多对时交替顺序 `A→B`、`B→A`，减轻学习和疲劳偏差。
5. 同 seed 能稳定约束地图、节点配方、起始分支和确定性候选，但当前协议不假设命中、暴击、闪避和 AI 的全部运行时随机都能被同一 `runSeed` 完整重放。
6. 因当前 N1–N6 未按层分池，不把不同节点之间的 A/B 差异归因于层级；必须同时报告实际 `recipe_id`。

## 7. 数据来源与可信边界

### 当前可可靠读取或交叉核对

- 存档/地图状态：`run_seed`、route、`node_id`、正式队伍与各角色已学技能。
- Pending encounter/当前遭遇：`recipe_id`；Special 的实际生成单位可给出 `encounter_variant`；配方表可给出 `enemy_body_count` 和比较用 `standard_equivalent_budget`。
- 战斗控制器/结算：`result`、`rounds`、参与单位集合；玩家/敌方正式本体可从战场交叉核对。
- 单位与战后恢复流程：HP/MP 快照、downed 状态；胜利恢复为存活人类单位 HP `Constitution × 2`、MP `Charisma`，受上限约束。
- 角色状态/Inventory：`character`、`learned_skills`、携带及剩余消耗品。
- RunSummary：终局结果、累计正向金币、累计获得物品/装备、击败敌人数、访问节点数、完成事件数、Boss 是否击败。
- Battle/Console 日志可辅助核对攻击、技能、回合、伤害、治疗和 Buff 的部分显示事件。

### 当前必须人工填或人工计数

- `tester`、`experience_level`、测试时采用的 `build_id` 说明。
- 双方 `effective_actions`、逐角色 `successful_skill_uses`/`basic_attack_uses`、`boss_pattern_cycles_seen`。
- offered/chosen 路线语境、两个主观评分、`dominant_strategy`、第一主观 `loss_reason` 和 `free_notes`。
- 三个关键时点的队伍 HP/MP 总和虽有运行时状态，但当前没有生产导出；本阶段由玩家观察/手工求和。
- `standard_equivalent_budget` 是本文定义的人工代理值，不是代码中的运行时指标。

## 8. 手工可行性门槛（先做 3 局）

完成 3 局后再决定是否需要任何采集改进：

- 每局记录“额外填表耗时”和需要回看日志/录屏的次数。
- 汇总每列缺失率，以及是否出现因记录而明显改变战术的情况。
- 若大部分战斗能在结算后一次补齐、每局额外记录不超过约 10 分钟、关键客观字段缺失不超过 10%，判定模板可继续手工使用。
- 若任一逐行动字段频繁遗失、每局额外记录超过约 10 分钟，或记录行为明显干扰决策，判定“手工困难”；先删减/改口径，再讨论最小采集支持。
- 3 局之前不创建分析脚本或生产 telemetry。Python 仅可用于验证 CSV 结构，不用于自动收集或分析试玩数据。

## 9. 自动化与人工验证边界

现有 Gameplay Test Framework、Editor/PlayMode 测试适合证明确定性配方、路线事务、真实输入链、结算/恢复规则、RunSummary 和功能回归；它们不能替代玩家对策略意义、信息清晰度、疲劳、挫败原因和记录负担的判断。

人工试玩也不替代自动化逻辑断言。发现可稳定复现的功能错误时，应单独记录复现步骤，后续补自动化；本 P0-C 只建立协议与空 CSV 模板，不修改测试、C# 或 Unity 资产。
