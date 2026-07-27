# 属性系统当前规则

## 目的

本文只记录当前代码实际使用的属性和派生规则。技能伤害、命中范围或特殊效果若由 SkillGraph/Ability 资产配置，应以对应资产和测试为准，不从本文推导额外公式。

## 属性集合

`AttributeType` 包含七个成员：

| 属性 | 角色存储 | 当前直接作用 |
|---|---|---|
| Strength / 力量 | `int Strength` | 力量系技能门槛；旧 `SkillDatabase` 的野蛮人伤害加成 |
| Agility / 敏捷 | `int Agility` | 敏捷系技能门槛；旧 Hunter/Rogue 技能伤害加成 |
| Constitution / 体质 | `int Constitution` | 最大生命与战后生命恢复 |
| Intelligence / 智力 | `int Intelligence` | 智力系技能门槛与每回合法力恢复 |
| Charisma / 魅力 | `int Charisma` | 魅力系技能门槛、最大法力与战后法力恢复 |
| Luck / 幸运 | `int Luck` | 幸运系技能门槛；当前没有统一暴击公式 |
| Speed / 速度 | `float Speed` | 移动力与先攻 |

默认中性值均为 5。六项整数属性参与装备总值计算；速度目前不由 `EquipmentDefinition` 的六项属性加成接口统一计算。

## 当前派生公式

`CharacterDefinition` 与 `Unit.RecalculateDerivedStats` 共享以下核心语义：

```text
MaxHealth = max(1, Constitution * 4)
MaxMana = max(0, Charisma * 3)
MaxMovementPoints = max(1, Speed)
Initiative = Speed * 2
```

其他已实现规则：

```text
战斗初始化 Mana = Charisma
单位自身回合结束时法力恢复 = max(0, Intelligence)，上限 MaxMana
回合开始不恢复法力
战后生命恢复 = Constitution * 2，上限 MaxHealth
战后法力恢复 = Charisma，上限 MaxMana
```

`DodgeRate`、`AttackRange`、`Reach`、`DefenceFactor` 和基础攻击倍率是独立字段；当前不存在由七项属性统一推导它们的公共公式。

## 技能门槛与伤害

- `SkillDefinition.RequiredAttribute` 与 `MinimumAttribute` 决定技能能否学习。
- 三职业首切基础技能门槛通常为 5，高级技能门槛为 7；具体值见 `FirstSliceSkillCatalog`。
- `SkillDatabase.CalculateSkillDamage` 只服务旧定义：按职业从 Strength、Intelligence、Agility 或 Charisma 中取 `max(0, attribute - 5)`。
- First Slice SkillGraph 的伤害、范围、Buff、召唤和位移参数由 SkillGraph/AbilityConfig 资产决定，不能套用旧 `SkillDatabase` 公式。

## 成长与装备

- `AttributePointSystem.ApplyAttributePoint` 每消费 1 点属性点，将指定属性增加 1。
- Pure Run 每次合法胜利给被选中的角色 1 点属性点。
- `AllocatedAttributes` 记录加点来源，但最终属性值仍写回角色字段。
- `GetTotalStrength` 等六个 `GetTotal*` 方法将角色基础值与各装备槽的加成相加。
- 技能门槛当前读取角色基础字段，而不是所有 `GetTotal*` 装备后总值；修改此规则需同步 `SkillSystem` 和测试。

## 初始状态

`CharacterDefinition.CreateDefault` 创建等级 1 角色，默认：

- 七项属性为 5；
- `CurrentHp = 20`、`CurrentMp = 15`；
- `AttackRange = 1`、`AttackFactor = 1`、`DefenceFactor = 1`；
- 0 经验、0 属性点、0 金币、空技能与装备。

Pure Run 固定三人队使用相同中性属性，再由 run seed 赋予一个起始基础技能。

## 验证入口

- 数据结构：`Assets/Tactics/Scripts/Common/Roster/CharacterDefinition.cs`
- 战斗派生值：`Assets/Tactics/Scripts/Common/Units/Unit.cs`
- 加点：`Assets/Tactics/Scripts/Common/Battle/AttributePointSystem.cs`
- 技能门槛：`Assets/Tactics/Scripts/Common/Battle/SkillSystem.cs`
- Pure Run 初始值与成长：`FirstSliceSkillCatalogTests`
