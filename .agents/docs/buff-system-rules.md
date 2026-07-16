# 战斗 Buff 系统当前规则

## 结构

Buff 主链为：

```text
BuffConfig（资产配置）
  -> Buff（来源、拥有者、剩余回合实例）
  -> BuffBehavior（触发行为）
  -> BuffComponent（单位上的容器、唯一性和生命周期）
```

`BuffConfig` 的当前字段包括名称、图标、默认持续时间、能否行动、效果类型、触发时机、诅咒分类、每回合伤害、元素和减伤比例。

## 添加与互斥规则

### 同一 Config

同一单位再次添加引用同一个 `BuffConfig` 对象的 Buff 时：

- 不创建第二个实例；
- `RemainingTurns = 旧剩余回合 + 新 Buff 回合`；
- 触发 `BuffChangeType.Refreshed`；
- 不再次调用 `OnApplied`。

这是当前实现的“唯一并延长”规则，不是取两个持续时间的最大值。

### 同一诅咒分类

不同 Config 若具有相同且非空的 `CurseCategory`：

- 新诅咒添加前移除旧诅咒；
- 同一分类在一个单位上最终只保留后施加的 Config；
- 同一个 Config 会先命中刷新逻辑，因此累计时长而不是替换自身。

当前伤害加深诅咒与恐惧诅咒都应遵守这一分类互斥规则。

### 地图层 Pending Buff

`CharacterDefinition.AddPendingBuff` 以 `BuffName` 防止重复快照。进入战斗时将快照还原为运行时 Config，应用后清空 pending 列表。

## 生命周期

```text
AddBuff
  -> 新实例：设置 Owner、OnApplied、Added
  -> 同 Config：累计时长、Refreshed

单位回合开始
  -> 对每个 Buff 调用 OnTurnStart

单位回合结束
  -> Buff.OnTurnEnd 递减剩余回合
  -> 未过期：TurnChanged
  -> 已过期：RemoveBuff、OnRemoved、Removed
```

销毁单位时会对所有 Buff 调用 `OnRemoved` 并清空容器。

## 行为规则

| `TriggerTiming` | 当前行为 |
|---|---|
| `None` | 只提供状态或由外部战斗逻辑消费 |
| `TurnStart` | 以 `DamagePerTurn` 对拥有者造成不可暴击的周期伤害 |
| `DamageTaken` | 受到伤害后，若攻击者在 1 格内则反击 |
| `BeforeAttacked` | 攻击结算前将本次攻击标记为暴击 |

当前 `BuffEffectType`：`None`、`Frozen`、`Marked`、`CurseDamageAmplifier`、`DamageReduction`、`Poison`。

- 任一活动 Buff 的 `CanAct == false`，单位即不能行动。
- `Frozen` 的解除、诅咒增伤和减伤由战斗组件按 `EffectType` 消费。
- DoT 通过 `CombatComponent.ApplyDamage` 进入统一伤害与战斗日志链路。

## UI 与事件

`BuffChanged` 事件类型：

| 类型 | 含义 |
|---|---|
| `Added` | 新实例加入 |
| `Removed` | 主动移除、过期或销毁 |
| `Refreshed` | 同 Config 累计时长 |
| `TurnChanged` | 回合结束后时长变化且仍未过期 |

`BattleUIController` 监听这些事件维护单位头顶图标和回合数；`TBattleLog` 记录添加、刷新与移除结果。

## 验证入口

- `Assets/Tactics/Scripts/Common/Units/Buffs/`
- `Assets/Tactics/Scripts/Common/Units/CombatComponent.cs`
- `Assets/Tactics/Tests/PlayMode/NecromancerPlayModeTests.cs`
- `Assets/Tactics/Tests/PlayMode/GameplayRuntimePlanTests.cs`
- `Tests/gameplay-specs/necromancer/curse-refreshes-duration.gameplay-test.md`
- `Tests/gameplay-specs/necromancer/curse-replaces-other-curse.gameplay-test.md`
