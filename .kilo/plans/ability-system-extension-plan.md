# Ability 系统扩展与回滚支持 - 实施计划

## 概述

本计划旨在扩展现有的 Ability 系统，支持以下新功能：
1. **治疗技能** (HealAbility)
2. **范围 AOE 技能** (FireballAbility)
3. **远程攻击** (RangedAttackAbility)
4. **完整的回滚支持** (Undo/Redo)

---

## 现有架构分析

### 核心接口

#### [`IAbility`](Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/IAbility.cs:12)
- 所有技能的基础接口
- 包含生命周期方法：`Initialize`, `Display`, `CleanUp`
- 事件处理：`OnUnitClicked`, `OnCellClicked`, `OnCellHighlighted`
- 状态检查：`CanPerform`

#### [`ICommand`](Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/ICommand.cs:10)
```csharp
public interface ICommand
{
    Task Execute(IUnit unit, IGridController controller);      // 执行
    Task Undo(IUnit unit, IGridController controller);         // 回滚
    Dictionary<string, object> Serialize();                     // 序列化
    ICommand Deserialize(...);                                  // 反序列化
}
```

#### [`AttackAbilityImpl`](Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/AttackAbilityImpl.cs:14)
- 现有的攻击能力实现
- 使用 `AttackCommand` 执行攻击
- 通过 `DamageScalingAbility` 支持伤害缩放

---

## 实施计划

### 阶段 1: 基础架构扩展

#### 1.1 创建 Command 基类
**文件**: `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/CommandBase.cs`

```csharp
public abstract class CommandBase : ICommand
{
    public abstract Task Execute(IUnit unit, IGridController controller);
    public abstract Task Undo(IUnit unit, IGridController controller);
    public abstract Dictionary<string, object> Serialize();
    public abstract ICommand Deserialize(Dictionary<string, object> data, IGridController controller);
}
```

#### 1.2 创建 Ability 基类
**文件**: `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/AbilityBase.cs`

```csharp
public abstract class AbilityBase : IAbility
{
    public IUnit UnitReference { get; set; }
    
    public virtual void Initialize(IGridController gridController) { }
    public virtual void Display(IGridController gridController) { }
    public virtual void CleanUp(IGridController gridController) { }
    public virtual bool CanPerform(IGridController gridController) => true;
    
    // 默认空实现的事件处理方法
    public virtual void OnUnitClicked(IUnit unit, IGridController grid) { }
    public virtual void OnCellClicked(ICell cell, IGridController grid) { }
    // ... 其他事件
}
```

#### 1.3 创建 Command 历史记录管理器
**文件**: `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/controllers/CommandHistory.cs`

```csharp
public class CommandHistory
{
    private Stack<ICommand> _history = new Stack<ICommand>();
    private Stack<ICommand> _redoStack = new Stack<ICommand>();
    
    public async Task ExecuteCommand(ICommand command, IUnit unit, IGridController controller)
    {
        await command.Execute(unit, controller);
        _history.Push(command);
        _redoStack.Clear(); // 清空重做栈
    }
    
    public async Task UndoLast(IUnit unit, IGridController controller)
    {
        if (_history.Count > 0)
        {
            var command = _history.Pop();
            await command.Undo(unit, controller);
            _redoStack.Push(command);
        }
    }
    
    public async Task RedoLast(IUnit unit, IGridController controller)
    {
        if (_redoStack.Count > 0)
        {
            var command = _redoStack.Pop();
            await command.Execute(unit, controller);
            _history.Push(command);
        }
    }
}
```

---

### 阶段 2: 治疗技能实现

#### 2.1 治疗命令
**文件**: `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/HealCommand.cs`

```csharp
public class HealCommand : CommandBase
{
    private IUnit _target;
    private float _healAmount;
    private float _previousHealth;
    
    public HealCommand(IUnit target, float healAmount)
    {
        _target = target;
        _healAmount = healAmount;
    }
    
    public override async Task Execute(IUnit unit, IGridController controller)
    {
        _previousHealth = _target.Health;
        _target.ModifyHealth(_healAmount, unit);
        await PlayHealEffect(_target, controller);
    }
    
    public override async Task Undo(IUnit unit, IGridController controller)
    {
        _target.Health = _previousHealth;
        await PlayUndoEffect(_target, controller);
    }
    
    public override Dictionary<string, object> Serialize()
    {
        return new Dictionary<string, object>
        {
            ["targetId"] = _target.UnitID,
            ["healAmount"] = _healAmount,
            ["previousHealth"] = _previousHealth
        };
    }
    
    public override ICommand Deserialize(Dictionary<string, object> data, IGridController controller)
    {
        var target = controller.UnitManager.GetUnitById((int)data["targetId"]);
        return new HealCommand(target, (float)data["healAmount"])
        {
            _previousHealth = (float)data["previousHealth"]
        };
    }
}
```

#### 2.2 治疗技能
**文件**: `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/HealAbility.cs`

```csharp
public class HealAbility : AbilityBase
{
    [SerializeField] private float _baseHeal = 10f;
    [SerializeField] private int _range = 3;
    
    private HashSet<IUnit> _targetableUnits;
    
    public override void Initialize(IGridController gridController)
    {
        base.Initialize(gridController);
        _targetableUnits = new HashSet<IUnit>();
    }
    
    public override void OnAbilitySelected(IGridController gridController)
    {
        _targetableUnits.Clear();
        var friendlyUnits = gridController.UnitManager.GetFriendlyUnits(UnitReference.PlayerNumber);
        
        foreach (var unit in friendlyUnits)
        {
            if (unit.CurrentCell.GetDistance(UnitReference.CurrentCell) <= _range 
                && unit.Health < unit.MaxHealth)
            {
                _targetableUnits.Add(unit);
            }
        }
    }
    
    public override void Display(IGridController gridController)
    {
        gridController.UnitManager.MarkAsFriendly(_targetableUnits);
    }
    
    public override void OnUnitClicked(IUnit unit, IGridController grid)
    {
        if (_targetableUnits.Contains(unit))
        {
            var healAmount = CalculateHealAmount();
            var command = new HealCommand(unit, healAmount);
            UnitReference.ExecuteAbility(command, PreAction, PostAction);
        }
    }
    
    private float CalculateHealAmount()
    {
        return _baseHeal + (UnitReference.Intelligence - 5) * 0.5f;
    }
}
```

---

### 阶段 3: 范围 AOE 技能

#### 3.1 AOE 命令
**文件**: `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/FireballCommand.cs`

```csharp
public class FireballCommand : CommandBase
{
    private ICell _targetCell;
    private int _radius;
    private float _damage;
    private List<(IUnit unit, float previousHealth)> _affectedUnits;
    
    public FireballCommand(ICell targetCell, int radius, float damage)
    {
        _targetCell = targetCell;
        _radius = radius;
        _damage = damage;
    }
    
    public override async Task Execute(IUnit unit, IGridController controller)
    {
        _affectedUnits = new List<(IUnit, float)>();
        
        var cells = GetCellsInRange(_targetCell, _radius, controller);
        foreach (var cell in cells)
        {
            foreach (var u in cell.CurrentUnits)
            {
                _affectedUnits.Add((u, u.Health));
                u.ModifyHealth(-_damage, unit);
            }
        }
        
        await PlayAOEAnimation(_targetCell, _radius, controller);
    }
    
    public override async Task Undo(IUnit unit, IGridController controller)
    {
        foreach (var (u, previousHealth) in _affectedUnits)
        {
            u.Health = previousHealth;
        }
        await PlayUndoAnimation(_targetCell, _radius, controller);
    }
    
    // Serialize/Deserialize 实现...
}
```

#### 3.2 火球术技能
**文件**: `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/FireballAbility.cs`

```csharp
public class FireballAbility : AbilityBase
{
    [SerializeField] private float _baseDamage = 15f;
    [SerializeField] private int _aoeRadius = 2;
    [SerializeField] private int _range = 5;
    
    private ICell _selectedTarget;
    
    public override void Display(IGridController grid)
    {
        var range = grid.GridHelper.GetCellsInRange(UnitReference.CurrentCell, _range);
        foreach (var cell in range)
        {
            cell.MarkAsTargetable();
        }
    }
    
    public override void OnCellClicked(ICell cell, IGridController grid)
    {
        if (cell.GetDistance(UnitReference.CurrentCell) <= _range)
        {
            _selectedTarget = cell;
            HighlightAOEPreview(cell, _aoeRadius, grid);
        }
    }
    
    public override void OnUnitClicked(IUnit unit, IGridController grid)
    {
        if (_selectedTarget != null && UnitReference.ActionPoints > 0)
        {
            var damage = CalculateDamage();
            var command = new FireballCommand(_selectedTarget, _aoeRadius, damage);
            UnitReference.ExecuteAbility(command, PreAction, PostAction);
        }
    }
}
```

---

### 阶段 4: 远程攻击

#### 4.1 远程攻击命令
**文件**: `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/RangedAttackCommand.cs`

```csharp
public class RangedAttackCommand : CommandBase
{
    private IUnit _target;
    private float _damage;
    private float _previousHealth;
    private ICell _shooterPosition;
    
    public RangedAttackCommand(IUnit target, float damage)
    {
        _target = target;
        _damage = damage;
    }
    
    public override async Task Execute(IUnit unit, IGridController controller)
    {
        _shooterPosition = unit.CurrentCell;
        _previousHealth = _target.Health;
        
        _target.ModifyHealth(-_damage, unit);
        await PlayProjectileAnimation(_shooterPosition, _target.CurrentCell, controller);
    }
    
    public override async Task Undo(IUnit unit, IGridController controller)
    {
        _target.Health = _previousHealth;
        await PlayUndoEffect(_target, controller);
    }
}
```

#### 4.2 远程攻击技能
**文件**: `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/RangedAttackAbility.cs`

```csharp
public class RangedAttackAbility : AttackAbility
{
    [SerializeField] private int _minRange = 2;
    [SerializeField] private int _maxRange = 6;
    
    public override bool IsUnitAttackable(IUnit other, ICell otherCell, ICell source)
    {
        var dist = otherCell.GetDistance(source);
        return dist >= _minRange && dist <= _maxRange;
    }
    
    public override void Display(IGridController grid)
    {
        var cells = grid.GridHelper.GetCellsInRange(UnitReference.CurrentCell, _maxRange);
        foreach (var cell in cells)
        {
            var dist = cell.GetDistance(UnitReference.CurrentCell);
            if (dist >= _minRange)
            {
                cell.MarkAsTargetable();
            }
        }
    }
}
```

---

### 阶段 5: 移动技能扩展

#### 5.1 移动命令（增强版）
**文件**: `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/MoveCommand.cs`

```csharp
public class MoveCommand : CommandBase
{
    private ICell _from;
    private ICell _to;
    private List<ICell> _path;
    private float _previousActionPoints;
    
    public MoveCommand(ICell to, List<ICell> path)
    {
        _to = to;
        _path = path;
    }
    
    public override async Task Execute(IUnit unit, IGridController controller)
    {
        _from = unit.CurrentCell;
        _previousActionPoints = unit.ActionPoints;
        
        await unit.MovementAnimation(_path, _to);
        unit.CurrentCell = _to;
        unit.ActionPoints -= CalculateMoveCost(_path);
    }
    
    public override async Task Undo(IUnit unit, IGridController controller)
    {
        var reversePath = _path.Reverse().ToList();
        await unit.MovementAnimation(reversePath, _from);
        unit.CurrentCell = _from;
        unit.ActionPoints = _previousActionPoints;
    }
}
```

---

### 阶段 6: UI 集成

#### 6.1 回滚按钮
**文件**: `Assets/Tactics/Scripts/Runtime/UI/CommandHistoryUI.cs`

```csharp
public class CommandHistoryUI : MonoBehaviour
{
    [SerializeField] private Button _undoButton;
    [SerializeField] private Button _redoButton;
    
    private CommandHistory _history;
    private IUnit _currentUnit;
    private IGridController _controller;
    
    private void Start()
    {
        _undoButton.onClick.AddListener(OnUndoClicked);
        _redoButton.onClick.AddListener(OnRedoClicked);
    }
    
    private async void OnUndoClicked()
    {
        if (_history != null)
        {
            await _history.UndoLast(_currentUnit, _controller);
            UpdateButtonState();
        }
    }
    
    private async void OnRedoClicked()
    {
        if (_history != null)
        {
            await _history.RedoLast(_currentUnit, _controller);
            UpdateButtonState();
        }
    }
    
    private void UpdateButtonState()
    {
        _undoButton.interactable = _history.HistoryCount > 0;
        _redoButton.interactable = _history.RedoCount > 0;
    }
}
```

---

## 文件结构

```
Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/
├── ICommand.cs                    (已有)
├── CommandBase.cs                 (新建)
├── AbilityBase.cs                 (新建)
├── AttackAbilityImpl.cs           (已有)
├── AttackCommand.cs               (已有)
├──{