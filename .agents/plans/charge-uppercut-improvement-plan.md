# 冲锋(ChargeAttack)与上勾拳(Uppercut)技能逻辑完善计划

## 背景

### 当前问题

基于代码审查，发现以下技能存在功能缺失：

1. **ChargeAttack (冲锋)**：描述为"Move then attack an enemy"，但实际只执行了DamageEffect，**完全没有移动逻辑**
2. **Uppercut (上勾拳)**：描述为"Punch a unit into the air, lobbing it back 3 tiles"，击退效果已实现，但**路径上的碰撞单位无伤害处理**

### 目标

完善这两个技能的实际执行逻辑，使其符合描述预期。

---

## 范围

### In Scope

1. ChargeAttack冲锋技能的直线冲锋+攻击+击退流程设计
2. Uppercut击退效果的碰撞伤害设计
3. 与现有系统（MoveCommand、KnockbackEffect、GridState）的兼容性

### Out of Scope

- 不修改DamageEffect、HealEffect等基础效果类
- 不新增美术资源或特效
- 不修改AI行为树逻辑

---

## 设计方案

### 一、ChargeAttack (冲锋) 完善方案

#### 1.1 设计结论

| 设计点 | 决策 |
|--------|------|
| **移动方式** | **四方向直线冲锋**（上/下/左/右），不斜向，不使用A*寻路 |
| **冲锋范围** | 可配置（如`_maxChargeRange: 4`，即最多跨4个tile） |
| **碰撞行为** | **所有单位都停下**，友方/敌方单位均不可穿过 |
| **动画效果** | **快速逐格移动**（速度比普通移动快2-3倍） |
| **到达效果** | 对目标造成**技能伤害 + 击退1格** |

#### 1.2 当前问题分析

```
当前执行流程：
1. 玩家点击目标格子
2. GenericAbilityImpl.OnCellClicked() 被调用
3. 由于不是"Move"，进入else分支
4. 直接执行 AbilityCommand → ExecuteEffectsAsync()
5. 只执行了 DamageEffect，没有任何移动
```

**缺失的功能**：
- ❌ 没有直线冲锋逻辑（四方向、有范围限制、有碰撞检测）
- ❌ 没有冲锋位移动画
- ❌ 没有冲锋后的击退效果

#### 1.3 核心设计：直线冲锋逻辑

冲锋不是A*路径寻路，而是**沿四方向直线逐一格移动**，每格都检查碰撞。

**执行流程**：
```
1. 玩家点击一个敌人单位（必须与施法者同一条直线：同行或同列）
2. 计算方向：上/下/左/右（斜向无效）
3. 计算距离：若距离 > _maxChargeRange → 无效
4. 沿该方向逐格检查路径（从近到远）：
   a. 出界或不可行走 → 在该格前一格停下
   b. 有单位占据（任何阵营）→ 在该格前一格停下
   c. 到达目标格 → 冲到目标面前
5. 执行快速逐格移动动画（普通移动速度的2-3倍）
6. 如果到达目标：
   └─ 执行DamageEffect（配置中的伤害）
   └─ 将目标沿冲锋方向击退1格（KnockbackEffect）
```

**图示**（施法者C，目标T，范围4格）：
```
  . . . . . .
  . . . . . .
  . . C . . .
  . . . . . .
  . . . T . .    ← C和T同列，可以冲锋
  . . . . . .

  . . . . . .
  . . C . . .
  . . . . . .
  . . . . T .    ← T在斜向，不可冲锋
  . . . . . .
  . . . . . .
```

#### 1.4 代码设计：ChargeAttackEffect

```csharp
[Serializable]
public class ChargeAttackEffect : AbilityEffect
{
    [SerializeField] private int _maxChargeRange = 4;        // 冲锋最大范围
    [SerializeField] private bool _stopOnAllUnits = true;    // 所有单位都停下
    [SerializeField] private float _speedMultiplier = 3f;    // 动画速度倍率
    [SerializeField] private float _collisionDamage = 1f;    // 碰撞伤害
    
    public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
    {
        foreach (var target in targets)
        {
            if (target == null) continue;
            
            ICell targetCell = target.CurrentCell;
            ICell casterCell = caster.CurrentCell;
            
            // 步骤1：计算四方向向量（不在同一直线上则无效）
            var (dirX, dirY) = GetCardinalDirection(casterCell.GridCoordinates, targetCell.GridCoordinates);
            if (dirX == 0 && dirY == 0) continue;  // 斜向或无方向
            
            // 步骤2：检查距离是否在范围内
            int distance = Mathf.Abs(targetCell.GridCoordinates.x - casterCell.GridCoordinates.x)
                         + Mathf.Abs(targetCell.GridCoordinates.y - casterCell.GridCoordinates.y);
            if (distance > _maxChargeRange) continue;
            
            // 步骤3：沿直线逐格扫描，找到有效停止位置
            var (stopCell, obstacleCollision, hitTarget) = ScanChargePath(
                casterCell, dirX, dirY, distance, targetCell, gridController);
            
            if (stopCell == null || stopCell == casterCell) continue;
            
            // 步骤4：构建直线路径
            var path = BuildStraightLinePath(casterCell, stopCell, dirX, dirY, gridController);
            if (path.Count == 0) continue;
            
            // 步骤5：执行冲锋移动动画
            await ExecuteChargeMovement(caster, casterCell, stopCell, path);
            
            // 步骤6：对碰撞单位造成伤害
            if (obstacleCollision != null)
            {
                CombatComponent.ApplyDamage(caster, obstacleCollision, _collisionDamage, false, ElementType.None);
            }
            
            // 步骤7：如果到达目标，执行击退
            if (hitTarget && target.Health > 0)
            {
                // 击退：沿冲锋方向继续推1格
                var knockbackCell = GetRelativeCell(targetCell, dirX, dirY, 1, gridController);
                if (knockbackCell != null && knockbackCell != targetCell
                    && gridController.CellManager.IsCellWalkable(knockbackCell)
                    && !knockbackCell.CurrentUnits.Any())
                {
                    // 执行击退位移
                    target.CurrentCell.CurrentUnits.Remove(target);
                    target.CurrentCell.IsTaken = target.CurrentCell.CurrentUnits.Count > 0;
                    target.CurrentCell = knockbackCell;
                    knockbackCell.CurrentUnits.Add(target);
                    knockbackCell.IsTaken = true;
                    target.WorldPosition = knockbackCell.WorldPosition;
                }
            }
        }
    }
    
    /// <summary>
    /// 获取四方向单位向量。只在同一行或同一列时有效，斜向返回(0,0)
    /// </summary>
    private (int dx, int dy) GetCardinalDirection(Vector2IntImpl from, Vector2IntImpl to)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        
        if (dx != 0 && dy != 0) return (0, 0);  // 斜向，无效
        return (dx == 0 ? 0 : (dx > 0 ? 1 : -1),
                dy == 0 ? 0 : (dy > 0 ? 1 : -1));
    }
    
    /// <summary>
    /// 沿方向逐格扫描路径，找到停止格和碰撞单位
    /// </summary>
    private (ICell stopCell, IUnit obstacle, bool hitTarget) ScanChargePath(
        ICell startCell, int dirX, int dirY, int maxDist, 
        ICell targetCell, IGridController gridController)
    {
        for (int i = 1; i <= maxDist; i++)
        {
            var cell = GetRelativeCell(startCell, dirX, dirY, i, gridController);
            if (cell == null)
                return (GetRelativeCell(startCell, dirX, dirY, i - 1, gridController), null, false);
            
            if (!gridController.CellManager.IsCellWalkable(cell))
                return (GetRelativeCell(startCell, dirX, dirY, i - 1, gridController), null, false);
            
            // 检查是否有单位占据
            var occupants = cell.CurrentUnits.ToList();
            if (occupants.Any())
            {
                if (cell == targetCell && occupants.Contains(targetCell.CurrentUnits.FirstOrDefault()))
                {
                    // 这就是目标格子 → 到达目标
                    return (cell, null, true);
                }
                // 非目标单位阻挡 → 在前一格停下
                return (GetRelativeCell(startCell, dirX, dirY, i - 1, gridController), occupants.First(), false);
            }
        }
        
        // 跑完最大距离但没碰到目标或碰撞
        return (GetRelativeCell(startCell, dirX, dirY, maxDist, gridController), null, false);
    }
    
    private ICell GetRelativeCell(ICell origin, int dirX, int dirY, int steps, IGridController gridController)
    {
        var coord = new Vector2IntImpl(
            origin.GridCoordinates.x + dirX * steps,
            origin.GridCoordinates.y + dirY * steps);
        return gridController.CellManager.GetCellAt(coord);
    }
    
    private List<ICell> BuildStraightLinePath(ICell start, ICell end, int dirX, int dirY, IGridController gridController)
    {
        var path = new List<ICell>();
        int steps = Mathf.Max(
            Mathf.Abs(end.GridCoordinates.x - start.GridCoordinates.x),
            Mathf.Abs(end.GridCoordinates.y - start.GridCoordinates.y));
        for (int i = 1; i <= steps; i++)
        {
            var cell = GetRelativeCell(start, dirX, dirY, i, gridController);
            if (cell != null) path.Add(cell);
        }
        return path;
    }
    
    private async Task ExecuteChargeMovement(IUnit unit, ICell source, ICell destination, List<ICell> path)
    {
        var moveCommand = new MoveCommand(source, destination, path);
        await moveCommand.Execute(unit, null);
    }
}
```

#### 1.5 配置变更

```yaml
# ChargeAttack.asset 修改后
_displayName: Charge Attack
_manaCost: 0
_targetingStrategy:
  type: {class: MoveThenAttackTargeting}
  data:
    _moveRange: 4
_effects:
  - ChargeAttackEffect
    _maxChargeRange: 4    # 冲锋范围4格
    _stopOnAllUnits: true # 所有单位都停下
    _speedMultiplier: 3   # 3倍速动画
    _collisionDamage: 1   # 碰撞伤害
  - DamageEffect          # 攻击伤害
    _baseDamage: 2
    _scalingType: 1
```

#### 1.6 UI显示范围

玩家点击冲锋技能时，需要高亮四方向上范围内的敌人格子：

```
GenericAbilityImpl.OnAbilitySelected()/CalculateValidTargetCells():

对于 MoveThenAttackTargeting 策略：
1. 以施法者位置为中心
2. 在四个方向上分别延伸到 _maxChargeRange
3. 对每个方向扫描：
   - 有敌人单位 → 标记该格为可攻击（红色高亮）
   - 路径上有友方/障碍 → 显示到障碍前一格
```

**与现有系统的关系**：

不修改 `GenericAbilityImpl` 或 `CalculateValidTargetCells` 本身，而是**增强 `MoveThenAttackTargeting` 的 `DisplayPreview` 方法**，在该方法中实现四方向范围高亮。

或者简单方案：**冲锋使用 `SingleTargetEnemy` 类型**，让GenericAbilityImpl正常显示范围内敌人，然后由 `ChargeAttackEffect` 在执行时做直线有效性验证（不在直线上则跳过）。

---

### 二、Uppercut (上勾拳) 完善方案

#### 2.1 当前问题分析

```
当前KnockbackEffect执行流程：
1. 计算击退方向（caster → target）
2. FindLandingCell：逐格检查不可行走或出界停下
3. PerformKnockbackFlight：抛物线飞行动画
4. 更新目标位置到landing cell
```

**缺失的功能**：
- ❌ 不检查路径上的其他单位
- ❌ 被击退单位撞到其他单位时无伤害

#### 2.2 增强方案

在现有`KnockbackEffect`中增强碰撞检测逻辑：

```csharp
[SerializeField] private float _collisionDamage = 1f; // 新增字段

// 原 FindLandingCell 替换为：
private (ICell landingCell, List<IUnit> collisions) FindLandingCellWithCollisions(
    ICell startCell, int dirX, int dirY, int maxDistance, 
    IGridController gridController, IUnit knockedBackUnit)
{
    ICell lastValidCell = startCell;
    var collisions = new List<IUnit>();
    
    for (int i = 1; i <= maxDistance; i++)
    {
        var coord = new Vector2IntImpl(
            startCell.GridCoordinates.x + dirX * i, 
            startCell.GridCoordinates.y + dirY * i);
        var cell = gridController.CellManager.GetCellAt(coord);
        if (cell == null) break;
        if (!gridController.CellManager.IsCellWalkable(cell)) break;
        
        // 检查是否有其他单位
        var otherUnits = cell.CurrentUnits.Where(u => u != knockedBackUnit).ToList();
        if (otherUnits.Any())
        {
            collisions.AddRange(otherUnits);
            break; // 在碰撞单位前一格停下
        }
        lastValidCell = cell;
    }
    
    return (lastValidCell != startCell ? lastValidCell : null, collisions);
}
```

执行流程修改：
```
1. 计算方向、扫描路径 (使用FindLandingCellWithCollisions)
2. 记录碰撞单位列表
3. 执行抛物线飞行动画
4. 碰撞伤害：ApplyDamage(caster, collision, _collisionDamage)
5. 更新目标位置
```

#### 2.3 配置变更

```yaml
# Uppercut.asset 修改后
_effects:
  - DamageEffect
    _baseDamage: 3
    _scalingType: 1
  - KnockbackEffect
    _distance: 3
    _height: 2
    _duration: 0.5
    _collisionDamage: 1
```

---

## 任务拆分

### Task 1: 新增ChargeAttackEffect类

- **目标**：实现四方向直线冲锋效果
- **输出文件**: `Assets/Tactics/Scripts/Common/Units/abilities/ChargeAttackEffect.cs`
- **验收标准**：
  - [x] 只允许上/下/左/右四方向冲锋（斜向无效），范围可配置（默认4格）
  - [x] 路径上遇到任何单位（敌/友）均在碰撞前一格停下
  - [x] 到达目标后：执行碰撞伤害 + 将目标沿冲锋方向击退1格
  - [x] 冲锋超出配置范围时跳过
  - [x] 快速逐格移动动画

### Task 2: 修改ChargeAttack配置

- **修改文件**: `Assets/Tactics/Arts/ScriptableObjects/Abilities/ChargeAttack.asset`
- **修改内容**：
  - 加入`ChargeAttackEffect`配置（`_maxChargeRange: 4`, `_collisionDamage: 1`）
  - 保留`DamageEffect`作为攻击伤害
- **验收**：配置正确加载

### Task 3: 增强KnockbackEffect碰撞检测

- **修改文件**: `AbilityEffect.cs` 中KnockbackEffect类
- **修改内容**：
  - 新增`_collisionDamage`序列化字段
  - `FindLandingCell` → `FindLandingCellWithCollisions`
  - 执行碰撞伤害
- **验收标准**：
  - [x] 击退路径上的单位受到碰撞伤害
  - [x] 在碰撞单位前一格停止

### Task 4: 修改Uppercut配置

- **修改文件**: `Assets/Tactics/Arts/ScriptableObjects/Abilities/Uppercut.asset`
- **修改内容**：添加`_collisionDamage: 1`
- **验收**：配置正确加载

### Task 5: 测试验证

- **ChargeAttack验证**：
  - [x] 同列4格内的敌人→冲锋并伤害+击退
  - [x] 同行3格内的敌人→冲锋并伤害+击退
  - [x] 斜向3格内的敌人→无法冲锋（无效目标）
  - [x] 路径上有友方→在友方前一格停下
  - [x] 路径上有其他敌方→在该敌方前一格停下
  - [x] 冲锋超出5格（配置4格）→无效
- **Uppercut验证**：
  - [x] 击退路径上的单位受到碰撞伤害
  - [x] 目标在碰撞单位前一格停下

---

## 推荐的实现顺序

```
Phase 1:
  Task 3 → Task 4   (上勾拳碰撞独立实现)

Phase 2:
  Task 1 → Task 2   (冲锋核心功能)

Phase 3:
  Task 5            (完整测试)
```

---

*计划生成时间：2026-05-14*
*冲锋设计：四方向直线冲锋 + 敌友均停 + 到达后伤害+击退1格 + 快速逐格动画*
*上勾拳设计：增强KnockbackEffect碰撞检测 + 碰撞伤害*