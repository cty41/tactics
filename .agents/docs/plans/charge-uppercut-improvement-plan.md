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

1. ChargeAttack冲锋技能的移动+攻击流程设计
2. Uppercut击退效果的碰撞伤害设计
3. 与现有系统（MoveCommand、KnockbackEffect、GridState）的兼容性

### Out of Scope

- 不修改DamageEffect、HealEffect等基础效果类
- 不修改TargetingStrategy基类
- 不新增美术资源或特效
- 不修改AI行为树逻辑

---

## 设计方案

### 一、ChargeAttack (冲锋) 完善方案

#### 1.1 当前问题分析

```
当前执行流程：
1. 玩家点击目标格子
2. GenericAbilityImpl.OnCellClicked() 被调用
3. 由于不是"Move"，进入else分支
4. 直接执行 AbilityCommand → ExecuteEffectsAsync()
5. 只执行了 DamageEffect，没有任何移动
```

**缺失的功能**：
- ❌ 没有计算从当前位置到目标附近的路径
- ❌ 没有执行MoveCommand进行位移动画
- ❌ 没有处理冲锋路径上的碰撞/伤害

#### 1.2 设计思路

**方案A：修改GenericAbilityImpl（侵入式）**

在`GenericAbilityImpl.OnCellClicked()`中增加对`MoveThenAttackTargeting`的特殊处理：

```csharp
// 伪代码
if (_config.TargetingStrategy is MoveThenAttackTargeting moveThenAttack)
{
    // 1. 找到目标旁边的可到达格子（最近且可行走的）
    ICell adjacentCell = FindBestAdjacentCell(targetCell, casterCell);
    
    // 2. 计算路径
    var path = _owner.FindPath(adjacentCell, gridController.CellManager);
    
    // 3. 先执行MoveCommand
    await _owner.HumanExecuteAbility(
        new MoveCommand(_owner.CurrentCell, adjacentCell, path), 
        gridController);
    
    // 4. 再执行攻击效果
    await ExecuteEffectsAsync(_pendingTargets, gridController);
}
```

**问题**：
- 修改GenericAbilityImpl，影响所有技能
- 需要处理"移动后目标位置变化"的问题

**方案B：新增ChargeAttackEffect（推荐）**

不修改GenericAbilityImpl，而是**新增一个AbilityEffect子类**：

```csharp
[Serializable]
public class ChargeAttackEffect : AbilityEffect
{
    [SerializeField] private int _maxMoveRange;
    [SerializeField] private float _collisionDamage = 2f; // 碰撞伤害
    
    public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
    {
        foreach (var target in targets)
        {
            if (target == null) continue;
            
            // 1. 找到目标旁边的最佳格子
            ICell targetCell = target.CurrentCell;
            ICell casterCell = caster.CurrentCell;
            ICell bestCell = FindBestAdjacentCell(targetCell, casterCell, gridController);
            
            if (bestCell != null && bestCell != casterCell)
            {
                // 2. 计算路径
                var path = caster.FindPath(bestCell, gridController.CellManager);
                
                // 3. 检查路径上的碰撞
                var collisions = CheckPathCollisions(path, caster, gridController);
                
                // 4. 执行移动
                await ExecuteMoveWithAnimation(caster, casterCell, bestCell, path);
                
                // 5. 对碰撞单位造成伤害
                foreach (var collision in collisions)
                {
                    CombatComponent.ApplyDamage(caster, collision, _collisionDamage, false, ElementType.None);
                }
            }
            
            // 6. 执行攻击（使用配置的DamageEffect）
            // 这部分由AbilityConfig中的Effects列表处理
        }
    }
}
```

**优点**：
- 不修改现有系统
- 符合Effect-based架构
- 可配置碰撞伤害

**配置变更**：
```yaml
# ChargeAttack.asset 修改后
effects:
  - ChargeAttackEffect  # 替代原来的DamageEffect
    _maxMoveRange: 5
    _collisionDamage: 2
  - DamageEffect  # 攻击伤害
    _baseDamage: 0
    _scalingType: 0
```

#### 1.3 详细设计

**ChargeAttackEffect类**：

```csharp
[Serializable]
public class ChargeAttackEffect : AbilityEffect
{
    [SerializeField] private float _collisionDamage = 2f;
    [SerializeField] private bool _stopOnCollision = true;
    
    public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
    {
        foreach (var target in targets)
        {
            if (target == null) continue;
            
            ICell targetCell = target.CurrentCell;
            ICell casterCell = caster.CurrentCell;
            
            // 找到目标旁边的最佳相邻格子
            ICell destinationCell = FindBestAdjacentCell(targetCell, casterCell, gridController);
            
            if (destinationCell != null && destinationCell != casterCell)
            {
                // 计算路径
                var path = caster.FindPath(destinationCell, gridController.CellManager);
                
                if (path != null && path.Any())
                {
                    // 检查路径碰撞
                    var (validPath, collisions) = ResolvePathCollisions(path, caster, gridController);
                    
                    // 执行移动（带动画）
                    await ExecuteChargeMovement(caster, casterCell, validPath.LastOrDefault() ?? casterCell, validPath);
                    
                    // 对碰撞单位造成伤害
                    foreach (var collision in collisions)
                    {
                        CombatComponent.ApplyDamage(caster, collision, _collisionDamage, false, ElementType.None);
                    }
                }
            }
        }
    }
    
    private ICell FindBestAdjacentCell(ICell targetCell, ICell casterCell, IGridController gridController)
    {
        // 找到目标周围距离施法者最近的、可行走的格子
        var neighbors = targetCell.GetNeighbours(gridController.CellManager);
        ICell bestCell = null;
        float minDistance = float.MaxValue;
        
        foreach (var neighbor in neighbors)
        {
            if (!gridController.CellManager.IsCellWalkable(neighbor)) continue;
            if (neighbor.IsTaken && neighbor.CurrentUnits.Any(u => u.PlayerNumber != casterCell.CurrentUnits.FirstOrDefault()?.PlayerNumber)) continue;
            
            float dist = Vector2Int.Distance(neighbor.GridCoordinates, casterCell.GridCoordinates);
            if (dist < minDistance)
            {
                minDistance = dist;
                bestCell = neighbor;
            }
        }
        
        return bestCell;
    }
    
    private (List<ICell> path, List<IUnit> collisions) ResolvePathCollisions(
        List<ICell> path, IUnit caster, IGridController gridController)
    {
        var validPath = new List<ICell>();
        var collisions = new List<IUnit>();
        
        foreach (var cell in path)
        {
            // 检查格子上是否有敌方单位
            var enemyUnits = cell.CurrentUnits.Where(u => u.PlayerNumber != caster.PlayerNumber).ToList();
            
            if (enemyUnits.Any())
            {
                collisions.AddRange(enemyUnits);
                
                if (_stopOnCollision)
                {
                    // 在当前格前停止
                    break;
                }
            }
            
            validPath.Add(cell);
        }
        
        return (validPath, collisions);
    }
    
    private async Task ExecuteChargeMovement(IUnit unit, ICell source, ICell destination, List<ICell> path)
    {
        // 使用现有的MoveCommand逻辑，但可能需要自定义动画速度
        var moveCommand = new MoveCommand(source, destination, path);
        await moveCommand.Execute(unit, null); // 需要传入controller
    }
}
```

#### 1.4 风险与注意事项

1. **路径计算**：需要确保`FindPath`能找到到达目标旁边格子的路径
2. **目标移动**：如果目标在冲锋过程中移动（如被其他技能影响），需要处理
3. **移动点数**：冲锋是否消耗移动点数？建议不消耗（作为技能效果）
4. **动画速度**：冲锋动画应该比普通移动更快

---

### 二、Uppercut (上勾拳) 完善方案

#### 2.1 当前问题分析

```
当前KnockbackEffect执行流程：
1. 计算击退方向（caster → target）
2. FindLandingCell：逐格检查，直到不可行走或超出边界
3. PerformKnockbackFlight：抛物线动画
4. 更新目标位置到landing cell
```

**缺失的功能**：
- ❌ 不检查路径上的其他单位
- ❌ 被击退单位撞到其他单位时无伤害
- ❌ 落地时如果格子被占据，无处理逻辑

#### 2.2 设计思路

**方案：增强KnockbackEffect**

在现有`KnockbackEffect`基础上增加碰撞检测：

```csharp
public class KnockbackEffect : AbilityEffect
{
    [SerializeField] private int _distance;
    [SerializeField] private float _duration = 0.5f;
    [SerializeField] private float _height = 2f;
    [SerializeField] private float _collisionDamage = 1f; // 新增：碰撞伤害
    [SerializeField] private bool _damageOnLanding = true; // 新增：落地伤害
    
    public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
    {
        foreach (var target in targets)
        {
            // ... 现有代码 ...
            
            // 增强：检查路径碰撞
            var (landingCell, pathCollisions) = FindLandingCellWithCollisions(
                targetCell, dirX, dirY, _distance, gridController, target);
            
            if (landingCell != null && landingCell != targetCell)
            {
                // ... 移除旧位置 ...
                
                // 执行击退动画
                await PerformKnockbackFlight(target, landingCell, _duration, _height);
                
                // 对路径上的碰撞单位造成伤害
                foreach (var collision in pathCollisions)
                {
                    CombatComponent.ApplyDamage(caster, collision, _collisionDamage, false, ElementType.None);
                }
                
                // 更新到新位置
                target.CurrentCell = landingCell;
                // ...
                
                // 检查落地格子是否有其他单位
                var landingUnits = landingCell.CurrentUnits.Where(u => u != target).ToList();
                if (landingUnits.Any() && _damageOnLanding)
                {
                    // 对落地格子的单位造成伤害（碾压效果）
                    foreach (var unit in landingUnits)
                    {
                        CombatComponent.ApplyDamage(caster, unit, _collisionDamage, false, ElementType.None);
                    }
                }
            }
        }
    }
}
```

#### 2.3 详细设计

**增强的FindLandingCell方法**：

```csharp
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
        var candidateCell = gridController.CellManager.GetCellAt(coord);
        
        if (candidateCell == null) break; // 超出边界
        if (!gridController.CellManager.IsCellWalkable(candidateCell)) break; // 不可行走
        
        // 检查格子上是否有其他单位（不包括被击退的单位自己）
        var otherUnits = candidateCell.CurrentUnits
            .Where(u => u != knockedBackUnit)
            .ToList();
        
        if (otherUnits.Any())
        {
            // 发现碰撞单位
            collisions.AddRange(otherUnits);
            
            // 在碰撞单位前一格停止
            break;
        }
        
        lastValidCell = candidateCell;
    }
    
    return (lastValidCell != startCell ? lastValidCell : null, collisions);
}
```

#### 2.4 配置变更

```yaml
# Uppercut.asset 修改后
effects:
  - DamageEffect
    _baseDamage: 3
    _scalingType: 1
  - KnockbackEffect
    _distance: 3
    _collisionDamage: 1  # 新增：碰撞伤害
```

---

## 任务拆分

### Task 1: 设计ChargeAttackEffect类

- **目标**：创建ChargeAttackEffect，实现移动+攻击流程
- **输入**：现有MoveCommand、DamageEffect、Pathfinding系统
- **输出**：ChargeAttackEffect.cs
- **验收标准**：
  - [ ] 能找到目标旁边的最佳相邻格子
  - [ ] 能计算并执行移动路径
  - [ ] 能检测路径上的碰撞单位
  - [ ] 能对碰撞单位造成伤害
  - [ ] 最后执行攻击伤害

### Task 2: 修改ChargeAttack配置

- **目标**：更新ChargeAttack.asset使用新的ChargeAttackEffect
- **输入**：ChargeAttack.asset
- **输出**：更新后的ChargeAttack.asset
- **验收标准**：
  - [ ] 移除旧的纯DamageEffect配置
  - [ ] 添加ChargeAttackEffect配置（含碰撞伤害参数）
  - [ ] 保留攻击伤害的DamageEffect

### Task 3: 增强KnockbackEffect碰撞检测

- **目标**：在KnockbackEffect中添加路径碰撞检测
- **输入**：KnockbackEffect.cs
- **输出**：增强后的KnockbackEffect.cs
- **验收标准**：
  - [ ] 能检测击退路径上的其他单位
  - [ ] 在碰撞单位前一格停止
  - [ ] 对碰撞单位造成伤害
  - [ ] 检查落地格子的占用情况

### Task 4: 修改Uppercut配置

- **目标**：更新Uppercut.asset配置碰撞伤害参数
- **输入**：Uppercut.asset
- **输出**：更新后的Uppercut.asset
- **验收标准**：
  - [ ] 添加_collisionDamage字段

### Task 5: 测试验证

- **目标**：验证两个技能的完善效果
- **验收标准**：
  - [ ] ChargeAttack：单位先移动再攻击，路径上的敌人受到碰撞伤害
  - [ ] Uppercut：击退时路径上的敌人受到碰撞伤害
  - [ ] 边界情况：路径被完全阻挡时的处理

---

## 风险与注意事项

1. **性能**：路径计算和碰撞检测可能增加计算量，需要优化
2. **并发**：如果多个技能同时影响单位位置，需要处理竞态条件
3. **AI适配**：AI需要理解新的ChargeAttack行为（先移动再攻击）
4. **动画**：可能需要调整移动动画速度以体现"冲锋"感

---

## 推荐的实现顺序

```
Phase 1（核心功能）：
  Task 3 → Task 4
  理由：KnockbackEffect的碰撞检测是独立功能，不影响其他系统

Phase 2（冲锋功能）：
  Task 1 → Task 2
  理由：ChargeAttackEffect依赖现有移动系统，需要更多测试

Phase 3（验证）：
  Task 5
  理由：最后进行完整测试
```

---

*计划生成时间：2026-05-14*
*说明：本计划基于对GenericAbilityImpl、MoveCommand、KnockbackEffect、AbilityEffect等核心文件的代码分析制定。*