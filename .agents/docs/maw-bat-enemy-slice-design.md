# 大嘴蝠敌人纵切设计

本文是 `unit.pure-run.maw-bat`、咬击、Predatory Diver AI 与 N2 浅水布局的当前设计约束。模型只能据此产生候选 Draft；代码、Godot Resource 与测试仍是实现事实源。

## 单位与遭遇

- 大嘴蝠：HP 14、MoveRange 5、Speed 12、Initiative 24、Air、普通尸体。
- N2 使用 `battle-layout.pure-run.n2-shallow-water`，阵容为大嘴蝠、远程羊魔、辅助羊魔。
- 浅水格：`(3,2) (3,3) (4,3) (4,4) (4,5) (5,4) (5,5) (5,6)`。

## 咬击

- `skill.enemy.maw-bat-bite.lv1`，每回合一次，0 MP，射程 1，`DirectAttack`，固定物理基础伤害 4，可暴击。
- 命中、闪避、暴击、输出修正、防御、护盾和近战反击沿用标准攻击链。
- 吸血为实际 HP 扣除量的 50% 向下取整；无最小值，受缺失生命限制，致死伤害仍吸血，不受标准治疗资格影响。
- 事件顺序为伤害、恢复、死亡结算；满血仍产生 Amount=0 的恢复事件，但表现层不显示绿色 0。

## AI

- `PredatoryDiver` 可移动后咬击时依次按可击杀、当前 HP 最低、移动成本最低、稳定 ID 选择。
- 本回合不能咬击时接近当前 HP 最低的目标；低于 30% HP 仍攻击，不撤退。

## 地形与移动

```gameplay-contract
id: MOVE-TERRAIN-COST-001
status: verified_current
statement: Land 在 Ground 消耗 1 移动点、在 ShallowWater 消耗 2；Air 和 Swim 在两者均消耗 1，移动范围按累计移动点判断而 MovementCellsThisTurn 仍记录实际路径格数。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/BoardAndRulesTests.cs
dsl_support: partial
```

```gameplay-contract
id: MOVE-AIR-FLYOVER-001
status: verified_current
statement: Air 可以经过动态地面占用者和 flyover 地面障碍但不能停在其上，任何移动类型都不能经过 absolute 障碍，CanStop 同时约束移动、传送、召唤和强制位移的落点。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/BoardAndRulesTests.cs
dsl_support: partial
```

```gameplay-contract
id: SKILL-BITE-LIFESTEAL-001
status: verified_current
statement: 咬击按最终实际 HP 伤害的 50% 向下取整恢复攻击者，伤害事件先于恢复事件，恢复事件先于死亡结算。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/StartingSkillRuntimeTests.cs
dsl_support: partial
```

```gameplay-contract
id: AI-PREDATORY-TARGET-001
status: verified_current
statement: PredatoryDiver 能在本回合咬击时按可击杀、当前 HP、移动成本、稳定 ID 排序目标，不能咬击时接近当前 HP 最低的目标且不会因低血量撤退。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/AiEncounterRuntimeTests.cs
dsl_support: partial
```

## 表现约束

使用 `Tools/artworks/pure_run/enemies/approved/tomb_maw_bat_*` 已批准素材。存活时保留独立 3px、1.4 秒悬浮层；移动中临时覆盖地面单位，死亡时停止浮动、短暂下降再显示死亡图。浅水是静态蓝绿色 Tile 与静态浅波纹，悬停仅显示“浅水”。
