# Task4: Buff、DoT 与击退效果落地

## 目标

让 `AbilityConfig` 中已有的 `ApplyBuffEffect`、`DamageOverTimeEffect`、`KnockbackEffect` 真正在战斗中生效。

---

## 输入

- 现有 `AbilityEffect` 子类的 TODO 实现
- `BuffComponent` 和 `Buff` 基类
- `MoveComponent`
- `CombatComponent`

---

## 输出

### 1. ApplyBuffEffect

**功能**：根据配置创建对应的 Buff 实例并施加到目标。

**配置字段**：
- `_buffType`：Buff 类型标识（如 "Ignite"、"Poison" 等）
- `_duration`：持续回合数

**执行逻辑**：
- 根据 `_buffType` 查找对应的 Buff 工厂或创建方法
- 创建 Buff 实例并调用 `target.AddBuff()`
- 支持已有 Buff 类型的扩展（MarkBuff、FrozenBuff 在 Task 2 中实现）

### 2. DamageOverTimeEffect（持续伤害）

**功能**：创建持续伤害 Buff，每回合开始时对目标造成伤害。

**配置字段**：
- `_damagePerTurn`：每回合伤害值
- `_duration`：持续回合数
- `_damageType`：伤害类型（可选，用于抗性计算）

**执行逻辑**：
- 创建 DoT Buff（如 `PoisonBuff`）
- Buff 在 `OnTurnStart` 时对持有者造成伤害
- 持续回合结束后自动移除

### 3. KnockbackEffect（击退）

**功能**：将目标沿攻击方向推离一定距离。

**配置字段**：
- `_distance`：击退格数
- `_damageOnCollision`：碰撞到障碍物时的额外伤害

**执行逻辑**：
- 计算击退方向（从攻击者指向目标）
- 调用 `MoveComponent` 或新增强制位移接口
- 沿直线逐格移动目标
- 若遇到障碍物（墙、其他单位）：
  - 停止移动
  - 施加碰撞伤害（如有配置）
  - 可附加眩晕效果（可选扩展）

---

## 技能配置示例

为已有技能添加 Buff/DoT/击退效果：

| 职业 | 技能 | 新增效果 |
|------|------|----------|
| Mage | 火球术 | 附加点燃（Ignite，DoT） |
| Barbarian | 上勾拳 | 击退 3 格 |
| Hunter | 毒箭 | 附加中毒（Poison，DoT） |

---

## 核心代码改动

| 文件 | 改动 |
|------|------|
| `ApplyBuffEffect.cs` | 补完 TODO 实现 |
| `DamageOverTimeEffect.cs` | 补完 TODO 实现 |
| `KnockbackEffect.cs` | 补完 TODO 实现 |
| `PoisonBuff.cs` / `IgniteBuff.cs` | 新建或完善 DoT Buff |
| `MoveComponent.cs` | 新增强制位移接口（或扩展现有移动方法） |

---

## 验收标准

1. **DoT 测试**：施加点燃后，目标每回合开始时受到 DoT 伤害，持续 3 回合。
2. **击退测试**：击退效果将敌人推到悬崖/障碍物时有额外反馈（伤害或眩晕）。
3. **Buff 叠加**：同一类型的 DoT Buff 是否可以叠加需要明确规则（建议：不叠加，取最高值刷新持续时间）。

---

## 风险与注意事项

1. **击退与地形交互**：击退到火焰地表/水面等需考虑地形效果触发
2. **强制位移与 AI**：被击退的单位可能需要 AI 重新评估位置
3. **DoT 伤害来源**：需记录 DoT 的施加者，用于击杀归属判定

---

*计划生成时间：2026-04-26*
