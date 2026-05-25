# Unity Logging Rules

## 强制约束

**禁止使用 Unity 原生 API**：

```csharp
// ❌ Forbidden
Debug.Log("message");
Debug.LogWarning("message");
Debug.LogError("message");
```

**通用日志使用项目 TLog**：

```csharp
// ✅ Correct
using Tactics.Runtime.Utilities;

TLog.Info("message");
TLog.Warning("message");
TLog.Error("message");
```

**战斗日志使用 TBattleLog**：

```csharp
// ✅ Correct
using Tactics.Runtime.BattleLog;

TBattleLog.Log(new AttackLogData(attacker, target, damage));
TBattleLog.Log(new SkillLogData(caster, skillName, damage));
TBattleLog.Log(new HealLogData(healer, target, amount));
TBattleLog.Log(new DamageLogData(attacker, target, damage));
TBattleLog.Log(new BuffLogData(target, buffName, duration));
```

## API 对照

| 场景 | 正确 API |
|------|----------|
| 通用日志 | `TLog.Info/Warning/Error` |
| 攻击日志 | `TBattleLog.Log(new AttackLogData(...))` |
| 技能日志 | `TBattleLog.Log(new SkillLogData(...))` |
| 治疗日志 | `TBattleLog.Log(new HealLogData(...))` |
| 伤害日志 | `TBattleLog.Log(new DamageLogData(...))` |
| Buff 日志 | `TBattleLog.Log(new BuffLogData(...))` |

## 例外情况

1. `TLog.cs` 内部实现（TLog 本身需要调用 Debug.Log）
2. 第三方代码（不可修改的外部库）
3. 编辑器工具代码（仅在 Editor 模式下运行）
