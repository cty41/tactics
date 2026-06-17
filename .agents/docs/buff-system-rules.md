# Buff System Rules（战斗 Buff 系统规则文档）

## 概述

Buff 系统由 4 个核心类组成：`BuffConfig`（配置）→ `Buff`（实例）→ `BuffBehavior`（行为逻辑）→ `BuffComponent`（容器管理）。

## BuffConfig 参数详解

| 参数 | 类型 | 默认值 | 规则 |
|------|------|--------|------|
| `BuffName` | string | — | Buff 显示名称，用于日志和 UI |
| `Icon` | Sprite | null | Buff 图标，用于头顶显示 |
| `DefaultDuration` | int | 3 | 默认持续回合数（创建 Buff 时传入的 duration） |
| `CanAct` | bool | **true** | `false` 时该 buff 会**禁止单位行动**（如冰冻）。多个 buff 时，任一 `CanAct=false` 即无法行动 |
| `EffectType` | enum | None | Buff 效果分类：`None`、`Frozen`、`Marked`。用于 `HasBuff()` 查询和冰破逻辑 |
| `TriggerTiming` | enum | None | 效果触发时机，决定 `BuffBehavior` 何时执行效果（见下表） |
| `DamagePerTurn` | float | 0 | 每回合伤害值，仅 `TriggerTiming=TurnStart` 时生效 |
| `ElementType` | enum | None | 元素类型：`None`、`Fire`、`Ice`、`Water`、`Earth`、`Wind`。用于冰破逻辑和伤害计算 |

## TriggerTiming 详解

| 值 | 触发时机 | 实际行为 | 典型用途 |
|----|---------|---------|---------|
| `None` | 不触发 | 纯状态标记，无自动效果 | Frozen（仅标记状态，效果由外部逻辑处理） |
| `TurnStart` | 回合开始时 | 对 Owner 造成 `DamagePerTurn` 伤害 | DoT（如 Ignite 灼烧） |
| `DamageTaken` | 受到伤害时 | 如果攻击者在 1 格内，Owner 反击攻击者 | Counter（反伤/反击） |
| `BeforeAttacked` | 被攻击前 | 强制将 `isCritical` 设为 `true` | Mark（标记，被攻击时必定暴击） |

## 生命周期

```
new Buff(config, source, duration)  →  BuffComponent.AddBuff(buff)
                                          ↓
                              同 Config 已存在？→ 刷新 RemainingTurns = max(旧, 新)
                                          ↓ 否
                              buff.Owner = _owner; _activeBuffs.Add(buff)
                                          ↓
                              buff.OnApplied()  →  BuffBehavior.OnApplied()
                                          ↓
                              [运行中] OnTurnStart / OnBeforeAttacked / OnDamageTaken
                                          ↓
                              OnTurnEnd: RemainingTurns--  →  IsExpired?  →  RemoveBuff
                                          ↓
                              buff.OnRemoved()  →  BuffBehavior.OnRemoved()
```

## 关键规则

### 1. 全局唯一规则
- 每个 BuffConfig 在同一单位上**只能有 1 个实例**
- 重复施加时**刷新** `RemainingTurns`（取 `max(当前剩余, 新持续时间)`）
- 刷新时**不触发** `OnApplied()`，不触发效果，仅更新时长

### 2. CanAct 规则
- `BuffComponent.CanAct` 遍历所有 active buffs，**任一** `CanAct=false` → 单位无法行动
- `Buff.CanAct` 先检查 `_config.CanAct`，再检查 `_behavior.CanAct`（两者都为 true 才可行动）

### 3. 冰破（Ice Break）规则
- 当受到攻击时，如果目标有 `Frozen` buff（`EffectType=Frozen`），攻击者为 `Ice` 元素 → **移除所有 Frozen buff**
- 代码位置：`CombatComponent.cs:137-147`

### 4. 过期规则
- `IsExpired = RemainingTurns <= 0`
- `OnTurnEnd()` 中先执行 `RemainingTurns--`，再检查过期
- 过期 buff 被批量 `RemoveBuff()`，触发 `BuffChanged(Removed)`

### 5. Duration 规则
- 创建时传入 `duration`，通常等于 `BuffConfig.DefaultDuration`
- 也可自定义（如 `AbilityEffect` 中的 `_duration` 字段）
- 同 Config 重复施加取 `max(当前, 新)`，不会缩短

## 现有 Buff 资产

| 资产 | EffectType | TriggerTiming | CanAct | 元素 |
|------|-----------|---------------|--------|------|
| Frozen.asset | Frozen | None | **false** | Ice |
| Ignite.asset | — | TurnStart | true | Fire |
| Counter.asset | — | DamageTaken | true | — |
| Mark.asset | Marked | BeforeAttacked | true | — |

## BuffChanged 事件类型

| ChangeType | 触发时机 |
|------------|---------|
| `Added` | 新 buff 实例被添加 |
| `Removed` | buff 被移除（过期/销毁/冰破） |
| `Refreshed` | 已有 buff 的 RemainingTurns 被刷新 |
| `TurnChanged` | 回合结束时 RemainingTurns 递减（未过期的 buff） |

## 测试框架可用断言

- `unitHasBuff(buffName)` — 检查是否有指定 buff
- `unitBuffDurationEquals(buffName, expected)` — 检查 buff 剩余回合
- `unitBuffCountEquals(buffName, expected)` — 检查同名 buff 数量
