# Unity Logging Rules

## 强制约束

**禁止使用 Unity 原生 API**：

```csharp
// ❌ Forbidden
Debug.Log("message");
Debug.LogWarning("message");
Debug.LogError("message");
```

**通用日志使用项目 Logger**：

```csharp
// ✅ Correct
using Tactics.Runtime.Utilities;

Logger.Info("message");
Logger.Warning("message");
Logger.Error("message");
```

**战斗日志使用 BattleLogger**：

```csharp
// ✅ Correct
using Tactics.Runtime.BattleLog;

BattleLogger.Log(new AttackLogData(attacker, target, damage));
BattleLogger.Log(new SkillLogData(caster, skillName, damage));
BattleLogger.Log(new HealLogData(healer, target, amount));
BattleLogger.Log(new DamageLogData(attacker, target, damage));
BattleLogger.Log(new BuffLogData(target, buffName, duration));
```

## API 对照

| 场景 | 正确 API |
|------|----------|
| 通用日志 | `Logger.Info/Warning/Error` |
| 攻击日志 | `BattleLogger.Log(new AttackLogData(...))` |
| 技能日志 | `BattleLogger.Log(new SkillLogData(...))` |
| 治疗日志 | `BattleLogger.Log(new HealLogData(...))` |
| 伤害日志 | `BattleLogger.Log(new DamageLogData(...))` |
| Buff 日志 | `BattleLogger.Log(new BuffLogData(...))` |

## 例外情况

1. `Logger.cs` 内部实现（Logger 本身需要调用 Debug.Log）
2. 第三方代码（不可修改的外部库）
3. 编辑器工具代码（仅在 Editor 模式下运行）
