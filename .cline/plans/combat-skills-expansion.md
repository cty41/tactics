# 战斗技能开发计划

## 概述

扩展战斗技能系统，实现近战攻击、远程攻击、近战治疗、火球术(含点燃Buff)四个新技能。点燃效果通过Buff系统实现，Buff由每个Unit独立管理(非单例)。

## 现有架构摘要

| 层级 | 模式 | 关键文件 |
|------|------|---------|
| Ability (Unity) | `Ability : MonoBehaviour, IAbility` | `Ability.cs` |
| Ability 逻辑 (纯C#) | `*Impl : IAbility` | `AttackAbilityImpl.cs`, `MoveAbilityImpl.cs` |
| Command | `readonly struct : ICommand` | `AttackCommand.cs`, `MoveCommand.cs` |
| 战斗计算 | `CombatComponent` (纯C#) | `CombatComponent.cs` |
| 日志 | `BattleLogger.Log(BattleLogData)` | `BattleLogger.cs` |
| Unit | `Unit : MonoBehaviour, IUnit` | `Unit.cs` |

### 核心模式
- **Ability**: MonoBehaviour 包装纯C# Impl，代理所有调用
- **Command**: 不可变 readonly struct，包含 Execute/Undo/Serialize/Deserialize
- **伤害流程**: `Ability.OnUnitClicked → HumanExecuteAbility → Command.Execute → ModifyHealth(-dmg)`
- **日志**: 通过 `BattleLogger.Log()` → `Logger.Info()` (非Unity Debug)

## 缺失内容 (必须创建)

1. **Buff/状态效果系统** - 无现有实现
2. **治疗技能/命令** - 仅存在伤害逻辑
3. **独立的近战/远程攻击** - 单个 `AttackAbility` 使用 `IsRangedDamage` 标志
4. **Buff相关日志类型** - 无 `BattleActionType.Buff`

---

## 实施计划

### 阶段1: Buff系统 (基础)

#### 1.1 Buff数据模型
**文件**: `Assets/Tactics/Scripts/Common/Units/Buffs/Buff.cs`
```
- abstract class Buff (纯C#, 非MonoBehaviour)
- 属性:
  - string BuffName { get; }          // Buff名称
  - IUnit Owner { get; }              // Buff所有者
  - IUnit Source { get; }             // Buff施加来源
  - int RemainingTurns { get; set; }  // 剩余回合数
  - bool IsExpired => RemainingTurns <= 0;
- 方法:
  - virtual void OnApplied()                              // Buff施加时调用
  - virtual void OnTurnStart(IGridController gridController)  // 回合开始时调用
  - virtual void OnTurnEnd(IGridController gridController)    // 回合结束时调用
  - virtual void OnRemoved()                              // Buff移除时调用
```

#### 1.2 Buff组件 (每Unit独立, 非单例)
**文件**: `Assets/Tactics/Scripts/Common/Units/Buffs/BuffComponent.cs`
```
- class BuffComponent (纯C#)
- 通过组合模式由每个Unit持有 (类似CombatComponent)
- 内部: List<Buff> _activeBuffs
- 方法:
  - void AddBuff(Buff buff)                                          // 添加Buff
  - void RemoveBuff(Buff buff)                                       // 移除Buff
  - void OnTurnStart(IGridController gridController)                 // 触发所有Buff的回合开始
  - void OnTurnEnd(IGridController gridController)                   // 触发所有Buff的回合结束
  - void OnUnitDestroyed()                                           // Unit销毁时清理所有Buff
  - IReadOnlyList<Buff> GetActiveBuffs()                             // 获取当前活跃Buff列表
```

#### 1.3 点燃Buff (具体实现)
**文件**: `Assets/Tactics/Scripts/Common/Units/Buffs/IgniteBuff.cs`
```
- class IgniteBuff : Buff
- 每tick伤害: 1 (通过构造函数配置)
- 持续时间: 3回合
- OnTurnStart: 通过 owner.ModifyHealth(-damage, Source) 对所有者造成伤害
- 日志: BattleLogger.Log(new DamageLogData { ... })
```

#### 1.4 BuffComponent集成到Unit
**文件**: `Assets/Tactics/Scripts/Common/Units/Unit.cs`
- 添加 `private BuffComponent _buffComponent;`
- 在 `Initialize()` 中: `_buffComponent = new BuffComponent(this);`
- 在 `OnTurnStart()` 中: `_buffComponent.OnTurnStart(gridController);`
- 在 `OnTurnEnd()` 中: `_buffComponent.OnTurnEnd(gridController);`
- 在 `OnDestroyed()` 中: `_buffComponent.OnUnitDestroyed();`
- 添加公共访问器: `public BuffComponent BuffComponent => _buffComponent;`

**文件**: `Assets/Tactics/Scripts/Common/Units/IUnit.cs`
- 添加: `void AddBuff(Buff buff);`
- 添加: `IReadOnlyList<Buff> GetActiveBuffs();`

#### 1.5 战斗日志 - Buff动作类型
**文件**: `Assets/Tactics/Scripts/Battle/BattleLog/BattleActionType.cs`
- 添加枚举值: `Buff, Heal`

**文件**: `Assets/Tactics/Scripts/Battle/BattleLog/HealLogData.cs` (新建)
```
- class HealLogData : BattleLogData
- 属性: Healer(治疗者), Target(目标), HealAmount(治疗量), NewHealth(新生命值)
- ActionType => BattleActionType.Heal
```

**文件**: `Assets/Tactics/Scripts/Battle/BattleLog/BuffLogData.cs` (新建)
```
- class BuffLogData : BattleLogData
- 属性: Source(施加者), Target(目标), BuffName(Buff名称), Duration(持续时间)
- ActionType => BattleActionType.Buff
```

---

### 阶段2: 近战攻击 (替代现有AttackAbility)

#### 2.1 MeleeAttackAbility (Unity组件)
**文件**: `Assets/Tactics/Scripts/Common/Units/abilities/MeleeAttackAbility.cs`
```
- class MeleeAttackAbility : Ability
- 范围: 1格(仅相邻)
- 无魔法消耗, 无AP覆盖(使用标准1 AP)
- 设置 _isRangedDamage = false, _hasHalfScaling = false
```

#### 2.2 MeleeAttackAbilityImpl (纯C#)
**文件**: `Assets/Tactics/Scripts/Common/Units/abilities/MeleeAttackAbilityImpl.cs`
```
- class MeleeAttackAbilityImpl : IAbility
- OnAbilitySelected: 查找范围1内的敌人
- Display: 标记可攻击目标
- OnUnitClicked: 如果目标有效, 执行 MeleeAttackCommand
- CanPerform: AP > 0 且 范围1内有有效目标
```

#### 2.3 MeleeAttackCommand
**文件**: `Assets/Tactics/Scripts/Common/Units/abilities/MeleeAttackCommand.cs`
```
- readonly struct MeleeAttackCommand : ICommand
- 字段: _target (IUnit), _damage (float), _actionCost (int = 1)
- Execute:
  1. _target.ModifyHealth(-_damage, unit)                    // 应用伤害
  2. _target.InvokeAttacked(...)                             // 触发被攻击事件
  3. unit.ActionPoints -= _actionCost                        // 扣除行动点
  4. 视觉高亮 (攻击者/防御者)
  5. BattleLogger.Log(new AttackLogData { Attacker=unit.name, Target=_target.name, Damage=_damage })
- Undo: 恢复生命值, 返还AP
- Serialize/Deserialize: 完整实现
```

---

### 阶段3: 远程攻击

#### 3.1 RangedAttackAbility (Unity组件)
**文件**: `Assets/Tactics/Scripts/Common/Units/abilities/RangedAttackAbility.cs`
```
- class RangedAttackAbility : Ability
- 范围: 5格, 最小射程2格(不能攻击相邻敌人)
- 无魔法消耗, 使用标准1 AP
- 设置 _isRangedDamage = true, _hasHalfScaling = false
```

#### 3.2 RangedAttackAbilityImpl (纯C#)
**文件**: `Assets/Tactics/Scripts/Common/Units/abilities/RangedAttackAbilityImpl.cs`
```
- class RangedAttackAbilityImpl : IAbility
- OnAbilitySelected: 查找范围[2, 5]内的敌人
- Display: 标记可攻击目标
- OnUnitClicked: 如果目标有效, 执行 RangedAttackCommand
- CanPerform: AP > 0 且 范围[2, 5]内有有效目标
- 射程验证: distance >= 2 && distance <= 5
```

#### 3.3 RangedAttackCommand
**文件**: `Assets/Tactics/Scripts/Common/Units/abilities/RangedAttackCommand.cs`
```
- readonly struct RangedAttackCommand : ICommand
- 结构与 MeleeAttackCommand 相同
- Execute 包含远程攻击的 BattleLogger.Log
```

---

### 阶段4: 近战治疗

#### 4.1 MeleeHealAbility (Unity组件)
**文件**: `Assets/Tactics/Scripts/Common/Units/abilities/MeleeHealAbility.cs`
```
- class MeleeHealAbility : Ability
- 范围: 1格(仅相邻友方单位)
- 无魔法消耗, 消耗1 AP
- 不实现 IDamageScalingAbility (治疗不是伤害)
```

#### 4.2 MeleeHealAbilityImpl (纯C#)
**文件**: `Assets/Tactics/Scripts/Common/Units/abilities/MeleeHealAbilityImpl.cs`
```
- class MeleeHealAbilityImpl : IAbility
- OnAbilitySelected: 查找范围1内的友方单位(排除自身)
- Display: 标记可治疗友军
- OnUnitClicked: 如果目标有效, 执行 HealCommand
- CanPerform: 范围1内有可治疗目标
- 过滤条件: 相同玩家编号, 生命值 < 最大生命值
```

#### 4.3 HealCommand
**文件**: `Assets/Tactics/Scripts/Common/Units/abilities/HealCommand.cs`
```
- readonly struct HealCommand : ICommand
- 字段: _target (IUnit), _healAmount (float = 3), _caster (IUnit)
- Execute:
  1. 计算实际治疗量: actualHeal = min(_healAmount, _target.MaxHealth - _target.Health)
  2. _target.ModifyHealth(+actualHeal, _caster)                         // 应用治疗
  3. _caster.ActionPoints -= _actionCost (默认1)                        // 扣除行动点
  4. BattleLogger.Log(new HealLogData { Healer=_caster.name, Target=_target.name, HealAmount=actualHeal, NewHealth=_target.Health })
- Undo: 应用负治疗(减少生命值), 返还AP
- Serialize/Deserialize: 完整实现
```

---

### 阶段5: 火球术 (AOE伤害 + 点燃Buff)

#### 5.1 FireballAbility (Unity组件)
**文件**: `Assets/Tactics/Scripts/Common/Units/abilities/FireballAbility.cs`
```
- class FireballAbility : Ability
- 射程: 4格(以格子为目标, 非单位)
- 魔法消耗: 3
- AOE范围: 目标格 + 相邻4格(十字型)
- 设置 _isRangedDamage = true, _hasHalfScaling = false
```

#### 5.2 FireballAbilityImpl (纯C#)
**文件**: `Assets/Tactics/Scripts/Common/Units/abilities/FireballAbilityImpl.cs`
```
- class FireballAbilityImpl : IAbility
- OnCellClicked: 验证范围<=4, 显示AOE预览
- OnCellHighlighted: 高亮AOE区域(目标格+相邻格)
- OnCellDehighlighted: 移除AOE高亮
- Execute: FireballCommand
- CanPerform: Mana >= 3 && AP > 0
```

#### 5.3 FireballCommand
**文件**: `Assets/Tactics/Scripts/Common/Units/abilities/FireballCommand.cs`
```
- readonly struct FireballCommand : ICommand
- 字段: _targetCell (ICell), _damage (float), _caster (IUnit), _aoeCells (List<ICell>)
- Execute:
  1. 遍历AOE区域每个格子:
     a. 查找格子上所有单位
     b. 应用伤害: unit.ModifyHealth(-damage, _caster)
     c. 施加点燃Buff: unit.AddBuff(new IgniteBuff(_caster, duration: 3, damagePerTurn: 1))
  2. _caster.Mana -= 3
  3. _caster.ActionPoints -= 1
  4. BattleLogger.Log(new SkillLogData { Source=_caster.name, SkillName="Fireball", Target=主要目标 })
  5. AOE爆炸视觉效果
- Undo: 复杂 - 恢复所有目标生命值, 移除点燃Buff, 返还Mana/AP
- Serialize/Deserialize: 包含格子引用、目标ID
```

#### 5.4 点燃Buff DOT日志
- 每次IgniteBuff触发伤害时:
  `BattleLogger.Log(new DamageLogData { Source="Ignite", Target=unit.name, Damage=1, RemainingHealth=unit.Health })`

---

## 文件结构

```
Assets/Tactics/Scripts/Common/Units/
├── Unit.cs                        # 修改: 添加BuffComponent集成
├── IUnit.cs                       # 修改: 添加AddBuff/GetActiveBuffs
├── CombatComponent.cs             # 不变
├── Buffs/                         # 新建文件夹
│   ├── Buff.cs                    # 新建: 抽象基类
│   ├── BuffComponent.cs           # 新建: 每Unit独立的Buff组件
│   └── IgniteBuff.cs              # 新建: 点燃/Buff实现
└── abilities/
    ├── MeleeAttackAbility.cs      # 新建: 近战攻击Unity组件
    ├── MeleeAttackAbilityImpl.cs  # 新建: 近战攻击逻辑
    ├── MeleeAttackCommand.cs      # 新建: 近战攻击命令
    ├── RangedAttackAbility.cs     # 新建: 远程攻击Unity组件
    ├── RangedAttackAbilityImpl.cs # 新建: 远程攻击逻辑
    ├── RangedAttackCommand.cs     # 新建: 远程攻击命令
    ├── MeleeHealAbility.cs        # 新建: 近战治疗Unity组件
    ├── MeleeHealAbilityImpl.cs    # 新建: 近战治疗逻辑
    ├── HealCommand.cs             # 新建: 治疗命令
    ├── FireballAbility.cs         # 新建: 火球术Unity组件
    ├── FireballAbilityImpl.cs     # 新建: 火球术逻辑
    └── FireballCommand.cs         # 新建: 火球术命令

Assets/Tactics/Scripts/Battle/BattleLog/
├── BattleActionType.cs            # 修改: 添加Buff, Heal
├── HealLogData.cs                 # 新建: 治疗日志
└── BuffLogData.cs                 # 新建: Buff日志
```

## 实施顺序

1. **Buff系统** (Buff, BuffComponent, IgniteBuff) + Unit集成
2. **战斗日志** (HealLogData, BuffLogData, BattleActionType新增枚举)
3. **近战攻击** (Ability + Impl + Command)
4. **远程攻击** (Ability + Impl + Command)
5. **近战治疗** (Ability + Impl + HealCommand)
6. **火球术** (Ability + Impl + FireballCommand + 点燃应用)

## 设计决策

| 决策 | 理由 |
|------|------|
| Buff使用纯C#类 | 无Unity依赖, 更易测试 |
| BuffComponent每Unit组合 | 遵循现有CombatComponent模式, 无单例 |
| HealCommand固定治疗量 | 与AttackCommand预计算伤害保持一致 |
| 火球术以格子为目标 | AOE天然需要基于格子的目标选择 |
| 点燃伤害=1, 持续3回合 | 按需求规格 |
| 治疗钳制到MaxHealth | 防止过度治疗漏洞 |
| 所有Command为readonly struct | 不可变, 遵循现有模式 |

## 不在范围内 (根据用户要求)

- 技能的视觉/音效 (VFX, SFX)
- ScriptableObject配置技能数据 (暂用硬编码)
- 超出基础Serialize/Deserialize的网络序列化
- AI行为树集成新技能
- 单元测试 (后续单独添加)

## 风险缓解

| 风险 | 缓解措施 |
|------|---------|
| Buff触发时机与回合系统冲突 | 接入现有Unit.OnTurnStart回调 |
| 火球术Undo复杂性 | 记录执行前状态快照用于回滚 |
| 治疗时生命值溢出 | 在HealCommand中钳制到MaxHealth |
| 同一Unit多个技能 | 每个技能是独立的MonoBehaviour组件 |
 