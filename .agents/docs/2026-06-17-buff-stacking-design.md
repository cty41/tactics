# Buff 叠加规则设计：全局唯一 + 刷新时长

## Background

- **当前问题**：`IsUnique=false` 的 buff（如 Frozen）重复施加会创建多个独立实例，各自独立计时。不符合游戏设计意图——冰冻不应该因为施加 2 次而持续 2 倍时间。
- **目标**：所有 buff 全局唯一（每个 Config 同一单位只能有 1 个实例），重复施加刷新持续时间。
- **预期收益**：简化 buff 系统设计，移除不必要的 `IsUnique` 字段，统一行为。

## 设计决策

### 核心规则

**所有 buff 默认唯一**。同一 BuffConfig 在同一单位上只能存在一个实例。重复施加时刷新 `RemainingTurns`（取 `max(当前剩余, 新持续时间)`）。

### 刷新策略：取较大值

为什么取 max 而非覆盖：
- 如果当前 Freeze 还剩 2 回合，新施加的 Freeze 持续 3 回合 → 应该变为 3 回合（延长）
- 如果当前 Freeze 还剩 5 回合，新施加的 Freeze 持续 3 回合 → 应该保持 5 回合（不缩短）

### 刷新时只更新时长，不触发效果

- 刷新时**不调用** `OnApplied()`、不触发 DoT 伤害、不触发任何行为效果
- 仅更新 `RemainingTurns` 并触发 `BuffChanged(Refreshed)` 事件
- 避免重复施加 Ignite 时立即造成额外伤害

### BuffChanged 事件变化

新增 `BuffChangeType.Refreshed`：当已有 buff 被刷新时触发，UI 可据此更新回合数显示。

## 具体改动

### 1. BuffConfig — 移除 IsUnique

- 删除 `_isUnique` 字段和 `IsUnique` 属性
- 所有 buff 行为统一为唯一实例

### 2. BuffComponent.AddBuff — 刷新逻辑

```csharp
public void AddBuff(Buff buff)
{
    // 查找已有同 Config 实例
    for (int i = 0; i < _activeBuffs.Count; i++)
    {
        if (_activeBuffs[i].Config == buff.Config)
        {
            // 刷新：取较大值
            int newDuration = Mathf.Max(_activeBuffs[i].RemainingTurns, buff.RemainingTurns);
            _activeBuffs[i].RemainingTurns = newDuration;
            BuffChanged?.Invoke(new BuffChangedEventArgs(BuffChangeType.Refreshed, _activeBuffs[i]));
            return;
        }
    }

    // 新 buff
    buff.Owner = _owner;
    _activeBuffs.Add(buff);
    buff.OnApplied();
    BuffChanged?.Invoke(new BuffChangedEventArgs(BuffChangeType.Added, buff));
}
```

### 3. BuffChangedEventArgs — 新增 Refreshed

```csharp
public enum BuffChangeType
{
    Added,
    Removed,
    Refreshed,  // 新增：持续时间刷新
    TurnChanged
}
```

### 4. BattleUIController — 处理 Refreshed

`OnBuffChanged` 中 `Refreshed` 时更新回合数显示（icon 不变，只更新数字）。

## 影响范围

| 文件 | 改动 |
|------|------|
| `BuffConfig.cs` | 移除 `_isUnique` / `IsUnique` |
| `BuffComponent.cs` | `AddBuff()` 改为唯一+刷新逻辑 |
| `BuffChangedEventArgs.cs` | 新增 `Refreshed` 枚举值 |
| `BattleUIController.cs` | 处理 `Refreshed` 事件 |
| `BuffConfig` 资产 (Frozen/Ignite/Counter/Mark) | 无需改动（IsUnique 字段被移除后自动忽略） |
| `SkillGraphTestGraphFactory.cs` | 移除 `_isUnique` 相关的 `SetPrivateField` 调用 |
| 测试断言 `unitBuffIsUnique` | 移除或改为验证"同 Config 只有 1 个实例" |

## 验收标准

- 施加 2 次 Freeze → 1 个 buff 实例，RemainingTurns = max(旧, 新)
- 头顶显示 1 个 icon，数字为 RemainingTurns
- `GetActiveBuffs()` 返回的列表中同一 Config 最多出现 1 次
- 编译通过，现有测试不受破坏
