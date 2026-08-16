# 战斗 Buff 系统当前规则

## 结构

Buff 主链为：

```text
BuffConfig（资产配置）
  -> Buff（来源、拥有者、剩余行动或层数）
  -> BuffBehavior（触发行为）
  -> BuffComponent（单位容器、合并、刷新和生命周期）
```

`BuffConfig` 除名称、图标、持续时间、行动许可和正负面分类外，还保存效果类型、触发时机、诅咒分类、周期伤害、独立的 `DamageCategory`/`ElementType`、刷新策略、速度修正和减伤比例。`DamageCategory` 只区分 `Physical`/`Magic`，元素独立区分 `None`、`Fire`、`Ice`、`Water`、`Earth`、`Wind`、`Lightning`；`Magic + None` 是合法组合。

## 合并、刷新与互斥

### 标准状态

`Burning`、`Poison`、`Slow`、`Stun` 按 `BuffEffectType` 识别同一运行时状态，即使来自不同 `BuffConfig` 也不会创建第二个实例：

| 状态 | 首次施加与重复施加 |
|---|---|
| `Burning` | 施加值作为层数；重复施加累加层数，无上限 |
| `Poison` | 每次成功施加固定增加 3 个目标行动周期；伤害不叠加 |
| `Slow` | 固定 `Speed -2`、最低 1；重复施加刷新为新持续时间 |
| `Stun` | 固定保留 1 次待跳过行动；重复施加只刷新，不延长为多次跳过 |

### 其他 Buff

其他效果在引用同一个 `BuffConfig` 时按 `RefreshStrategy` 处理：`AddDuration` 累加持续时间，`RefreshDuration` 覆盖为新持续时间，`AddStacks` 累加层数。刷新触发 `BuffChangeType.Refreshed`，不会再次调用 `OnApplied`。

不同 Config 若具有相同且非空的 `CurseCategory`，新诅咒会先移除旧诅咒；同一分类最终只保留后施加者。

### 地图层 Pending Buff

`CharacterDefinition.PendingBuffSnapshot` 持久化名称、资产路径、持续时间、行动许可、正负面、效果/触发类型、诅咒分类、周期伤害、伤害大类、元素、刷新策略、速度修正和减伤比例。进入战斗时还原运行时 Config，应用后清空 pending 列表；旧存档缺失伤害大类时按 `Magic` 补全。

地图 Buff 可能在单位正式 `Initialize` 前恢复。`Unit` 会提前建立 Buff 容器并保留状态，随后绑定战场控制器；初始化不得清空已恢复状态。这样行动许可、速度修正和后续先攻重排从第一回合起一致生效。

## 回合生命周期

```text
AddBuff
  -> 新实例：设置 Owner、OnApplied、Added
  -> 同一运行时状态：按标准规则或 RefreshStrategy 刷新

单位行动开始
  -> 对每个 Buff 调用 OnTurnStart
  -> Burning/Poison 结算周期伤害
  -> Burning 层数减 1，降到 0 立即移除

单位行动结束
  -> 非 Burning Buff 的 RemainingTurns 减 1
  -> 未过期：TurnChanged
  -> 已过期：RemoveBuff、OnRemoved、Removed
```

销毁单位时会对所有 Buff 调用 `OnRemoved` 并清空容器。

## 标准状态结算

- `Burning`：目标行动开始时造成等于当前层数的伤害，再减 1 层。
- `Poison`：目标行动开始时固定造成 2 点伤害；已附着的周期伤害不可闪避。
- `Slow`：立即重算移动上限与先攻，并重排当前轮尚未行动单位；解除时同样重排。当前剩余移动点只会被向下限制，不会因解除而在本行动额外增加。
- `Stun`：`CanAct == false`，目标下一次进入行动时被跳过，并在该行动结束后移除。
- DoT 不暴击、不触发直接命中闪避，并绕过普通防御计算，但仍通过统一 `CombatComponent.ApplyDamage`、伤害分类和战斗日志链路。
- 即时伤害只有在 `ApplyBuff.RequiresSuccessfulHit` 显式开启时才把后续 Buff 视为附带状态；同一目标的该次命中被闪避、格挡或判定未命中时，附带状态不生效。普通独立 Buff 不读取历史伤害结果。

## UI 与事件

`BuffChanged` 事件类型：

| 类型 | 含义 |
|---|---|
| `Added` | 新实例加入 |
| `Removed` | 主动移除、过期或销毁 |
| `Refreshed` | 同一运行时状态被刷新 |
| `TurnChanged` | 层数或剩余周期变化且仍未过期 |

`BattleUIController` 监听事件维护单位头顶图标和数值；`TBattleLog` 记录添加、刷新与移除结果。

## 验证入口

- `Assets/Tactics/Scripts/Common/Units/Buffs/`
- `Assets/Tactics/Scripts/Common/Units/CombatComponent.cs`
- `Assets/Tactics/Tests/PlayMode/SharedBattlePrimitivesTests.cs`
- `Tests/gameplay-specs/shared/status-turn-semantics.gameplay-test.md`
- `Tests/gameplay-specs/shared/facing-and-initiative.gameplay-test.md`
