# Pure Run 的 Mewgenics 归一化参考带

更新时间：2026-07-31。本文只保存第三方数据的聚合统计、证据边界与可迁移比例，不复制原始资源，也不把 Mewgenics 规则当作 Tactics 已实现规则。Pure Run 当前值见 [当前战斗数值](pure-run-current-combat-values.md)。

## 已验证的聚合数据

此前对外部只读工作区 `D:\codes\mewgenics_assets` 的 `.lvl` 与 GON 配置完成聚合检查：

- 成功解析 `2361` 个 `.lvl`；该批次没有格式解析失败；
- `2335` 个关卡识别到敌方单位，其中 `2308` 个配置 4 个玩家出生位，即 `2308 / 2335 = 98.84%`；
- 全库 easy 战斗的敌人数中位数为 `6`；教学 easy 典型为 2，多个区域 easy 主要集中在 5–7；
- `spawns.gon` 的 authored `value` 存在稳定层级：小型单位常见 `0–0.5`，标准单位约 `1`，强标准单位约 `1.5`，大型单位约 `2`；
- Alley easy 虽然身体数中位数为 6，但按显式 value、并对缺失值使用下述代理后的预算中位数约为 5；
- 多区域 Boss 关敌方实体数中位数为 `1`。这个统计只说明身体数，不证明单体 Boss 的行动结构。

这些数字是指定外部资产快照的聚合结果，不是对所有版本或所有运行时随机派生的普遍断言。

## 归一化公式

比较两个不同战斗系统时使用比例，不直接抄身体数或单次伤害：

```text
enemy_body_ratio = enemy_count / player_count
standard_equivalent_budget = sum(authored_enemy_weight) / player_count
damage_pressure = final_damage / target_max_hp
mana_pressure = mana_cost / (starting_mana + expected_in_battle_regen)
aoe_pressure = damage_pressure * expected_targets_hit
boss_action_budget = normal_actions + bonus_actions
                   + expected_summon_actions + reaction_actions
```

用于三人 Pure Run 的人数与标准单位换算：

```text
6 bodies × 3 / 4 = 4.5 bodies
5–6 standard-equivalent units × 3 / 4 = 3.75–4.5 standard units
```

`standard_equivalent_budget` 的除数用于比较每名玩家承受的预算；若讨论整场三人队总预算，则直接使用 `sum(weight) × 3/4`。文档和报告必须注明使用的是“每玩家比率”还是“整场等价物”，避免混用。

## 已验证 / 代理 / 未知

### 已验证

- 上述 2361 / 2335 / 2308 关卡计数与 98.84% 出生位比例；
- easy 敌人数中位数 6；
- 外部配置中显式 authored `value` 的小型、标准和大型分层；
- Boss 关敌方实体数中位数 1；
- Mewgenics 同时有 GenericBrain 与 PatternBrain 配置路线，Pattern 配置包含主行动与多类额外行动容器。

### 设计代理（不得伪装成运行时真值）

- 缺失 authored value 的单位暂按 `1` 计入 Alley easy 代理预算；
- `5–6 × 3/4 = 3.75–4.5` 是队伍规模换算带，不是 Tactics 自动生成预算；
- 对 Tactics 怪物赋予任何临时审计权重、AOE 期望命中人数或召唤物预期行动，均需另列假设；
- 技能比较中的“预计战斗内恢复”和“代表性最终伤害”必须来自固定 seed 遥测，而不是资产裸值。

### 未知

- Mewgenics `variant_of` 完整继承后的所有有效字段与最终运行时伤害链；
- stacked/dispersed bonus turn 的精确插入时序、游标推进和反应行为频率；
- 不同关卡随机派生、精英 Buff、奖励和玩家构筑对最终胜率的联合影响；
- Mewgenics 单 Boss 的实际每轮有效行动预算。实体数中位数 1 不能替代该数据。

## 对 Pure Run 的可执行边界

1. **不把 6 个身体直映为 6 个标准怪。** Mewgenics 的 6 敌人样本常混有 value 0–0.5 的小怪；Pure Run 当前 Charger/Ranged/AOE/Support 更接近标准单位。
2. 当前三人队面对 3 个前期怪、4 个中后期怪，与 `3.75–4.5` 标准单位参考带并不明显矛盾；应先测组合、布局、倍率与行动经济，再考虑加身体。
3. Boss 对齐的是 `boss_action_budget`，不是头数。单一实体只有在 Pattern、额外行动、阶段、召唤或反应行为补足有效行动时才可与单 Boss 参考相提并论。
4. 技能只按 `damage_pressure`、`mana_pressure` 与 `aoe_pressure` 比较。Mewgenics 的 mana/damage 量级只能用于寻找离群点，不能直接成为 Tactics 改值目标。
5. 这些预算仅用于离线设计审计；当前运行时继续使用显式 `EncounterRecipe`，不据此宣称存在动态 ThreatValue。

## 证据入口

- 仓库分析：`.agents/docs/mewgenics-config-analysis.md`、`.agents/docs/mewgenics-runtime-reverse-engineering.md`；
- 聚合检查记录：本地整合计划中的只读审计结果；
- 外部输入：`D:\codes\mewgenics_assets\data` 与 `D:\codes\mewgenics_assets\levels`（不纳入仓库）。
