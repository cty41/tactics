# Battle UI 移动按钮绑定与 AI 回合隐藏面板

## 需求概述

1. **移动按钮绑定玩家 Unit 的选择移动行为**：点击 Battle UI 的 "移动" 按钮后，进入移动模式，高亮可移动格子，玩家点击目标格子后 Unit 移动
2. **移动后置灰**：当 Unit 执行完移动后（ActionPoints 耗尽），移动按钮置灰不可用
3. **AI 回合隐藏下方 Button Panel**：当 AI Unit 行动时，隐藏 BottomPanel

## 现有系统分析

### 关键发现

| 项目 | 详情 |
|---|---|
| `GridState` setter | `UnityGridController.GridState` 有 public setter，可从外部设置 |
| `UnitMoved` 事件签名 | `Action<UnitMovedEventArgs>`（通过 `IMoveable` 接口） |
| `CanPerform` 判断 | `MoveAbilityImpl.CanPerform()` 检查 `ActionPoints > 0 && 有可移动格子` |
| `OnAbilitySelected` | 使用 `ActionPoints` 计算移动范围（第66行） |
| `PlayerType` | `HumanPlayer` vs `AutomatedPlayer` |
| BottomPanel | Battle.uxml 第10行已有 `<ui:VisualElement name="BottomPanel">` |

### 移动流程

```
点击 Move 按钮
    ↓
获取当前选中 Unit 的 MoveAbilityImpl
    ↓
调用 moveAbility.OnAbilitySelected(gridController)  // 计算移动范围
调用 moveAbility.Display(gridController)            // 高亮可移动格子
    ↓
等待玩家点击目标格子
    ↓
MoveAbilityImpl.OnCellClicked(cell, gridController)
    ↓
HumanExecuteAbility(MoveCommand, gridController)    // 执行移动
    ↓
GridStateUnitSelected 状态刷新 或返回 AwaitInput
    ↓
UnitMoved 事件触发 → 更新按钮状态
```

### AI 回合判断

`OnTurnStarted(TurnTransitionParams)` 中：
- `turnTransitionParams.TurnContext.CurrentPlayer.PlayerType == PlayerType.HumanPlayer` → 显示面板
- `turnTransitionParams.TurnContext.CurrentPlayer.PlayerType == PlayerType.AutomatedPlayer` → 隐藏面板

## 实现方案

### 修改文件

**仅修改**：`Assets/Tactics/Scripts/UI/BattleUIController.cs`

### 详细实现

#### 1. 添加字段

```csharp
private VisualElement _bottomPanel;
private MoveAbilityImpl _currentMoveAbility;
```

#### 2. WireButtons() 中添加 BottomPanel 引用

在 `_moveButton` 获取之后添加：
```csharp
_bottomPanel = root.Q<VisualElement>("BottomPanel");
```

#### 3. 修改 OnTurnStarted() - AI 回合隐藏面板 + 更新移动按钮状态

在现有代码基础上，添加 BottomPanel 可见性控制和移动按钮状态更新：

```csharp
private void OnTurnStarted(TurnTransitionParams turnTransitionParams)
{
    bool isHumanTurn = turnTransitionParams.TurnContext.CurrentPlayer.PlayerType == PlayerType.HumanPlayer;
    
    // AI 回合隐藏底部按钮面板
    if (_bottomPanel != null)
        _bottomPanel.style.display = isHumanTurn ? DisplayStyle.Flex : DisplayStyle.None;
    
    _canEndTurn = isHumanTurn;
    if (_endTurnButton != null)
        _endTurnButton.SetEnabled(isHumanTurn);

    // 更新 HP/MP 和移动按钮状态
    var playableUnits = turnTransitionParams.TurnContext.PlayableUnits();
    var currentUnit = playableUnits.FirstOrDefault();
    
    if (currentUnit != null)
    {
        if (_currentSelectedUnit is ICombatant oldCombatant && !ReferenceEquals(oldCombatant, currentUnit))
        {
            oldCombatant.HealthChanged -= OnUnitHealthChanged;
        }

        _currentSelectedUnit = currentUnit;
        UpdateHPMPBars();
        UpdateMoveButtonState(currentUnit);  // 新增

        if (_currentSelectedUnit is ICombatant newCombatant)
        {
            newCombatant.HealthChanged += OnUnitHealthChanged;
        }
    }
}
```

#### 4. 实现 OnMoveClicked() - 触发移动能力

```csharp
private void OnMoveClicked()
{
    if (_currentSelectedUnit == null || _gridController == null)
        return;

    // 检查是否是当前回合的可操作 Unit
    var playableUnits = _gridController.TurnContext?.PlayableUnits()?.ToList();
    if (playableUnits == null || !playableUnits.Any(u => ReferenceEquals(u, _currentSelectedUnit)))
        return;

    // 获取 MoveAbility
    var moveAbility = _currentSelectedUnit.GetBaseAbilities()
        .OfType<MoveAbilityImpl>()
        .FirstOrDefault();
    
    if (moveAbility == null || !moveAbility.CanPerform(_gridController))
        return;

    _currentMoveAbility = moveAbility;

    // 如果已经在 GridStateUnitSelected 状态，调用 Display 高亮格子
    if (_gridController.GridState is GridStateUnitSelected)
    {
        moveAbility.OnAbilitySelected(_gridController);
        moveAbility.Display(_gridController);
        return;
    }

    // 如果不在选中状态，创建新的选中状态（仅包含移动能力）
    _gridController.GridState = new GridStateUnitSelected(_currentSelectedUnit, moveAbility);
}
```

#### 5. 实现移动后置灰

**添加方法**：
```csharp
private void UpdateMoveButtonState(IUnit unit)
{
    if (_moveButton == null || unit == null)
    {
        if (_moveButton != null) _moveButton.SetEnabled(false);
        return;
    }

    bool canMove = unit.ActionPoints > 0
        && unit.GetBaseAbilities().OfType<MoveAbilityImpl>().Any();
    
    // 更精确：检查是否有可移动的目的地
    if (canMove && _gridController != null)
    {
        canMove = unit.GetAvailableDestinations(_gridController.CellManager.GetCells()).Count > 0;
    }

    _moveButton.SetEnabled(canMove);
}
```

**订阅 UnitMoved 事件** - 在 `SubscribeToUnitEvents()` 中添加：
```csharp
private void SubscribeToUnitEvents()
{
    if (_gridController?.UnitManager == null) return;

    var units = _gridController.UnitManager.GetUnits();
    foreach (var unit in units)
    {
        unit.UnitSelected += OnUnitSelected;
        unit.UnitDeselected += OnUnitDeselected;
        
        // 订阅移动事件（通过 IMoveable 接口）
        if (unit is IMoveable moveable)
        {
            moveable.UnitMoved += OnUnitMoved;
        }
    }
}
```

**添加事件处理器**：
```csharp
private void OnUnitMoved(UnitMovedEventArgs args)
{
    // 移动完成后更新按钮状态
    if (ReferenceEquals(args.AffectedUnit, _currentSelectedUnit))
    {
        UpdateMoveButtonState(args.AffectedUnit);
    }
}
```

**在 UnsubscribeFromUnitEvents() 中取消订阅**：
```csharp
private void UnsubscribeFromUnitEvents()
{
    if (_gridController?.UnitManager == null) return;

    var units = _gridController.UnitManager.GetUnits();
    foreach (var unit in units)
    {
        unit.UnitSelected -= OnUnitSelected;
        unit.UnitDeselected -= OnUnitDeselected;
        
        if (unit is IMoveable moveable)
        {
            moveable.UnitMoved -= OnUnitMoved;
        }
    }
}
```

#### 6. OnGameEnded() 中禁用移动按钮

```csharp
private void OnGameEnded(GameResult gameResult)
{
    _canEndTurn = false;
    if (_endTurnButton != null)
        _endTurnButton.SetEnabled(false);
    if (_moveButton != null)
        _moveButton.SetEnabled(false);
    
    // 隐藏底部面板
    if (_bottomPanel != null)
        _bottomPanel.style.display = DisplayStyle.None;

    _currentSelectedUnit = null;
    if (_hpBar != null) _hpBar.value = 0;
    if (_mpBar != null) _mpBar.value = 0;
}
```

#### 7. 添加必要的 using

确保文件顶部有：
```csharp
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Units.Abilities;
```

## 实现步骤

| 步骤 | 操作 | 文件 |
|---|---|---|
| 1 | 添加 `_bottomPanel` 和 `_currentMoveAbility` 字段 | BattleUIController.cs |
| 2 | WireButtons() 中获取 BottomPanel 引用 | BattleUIController.cs |
| 3 | 修改 OnTurnStarted() - AI 回合隐藏 + 移动按钮状态 | BattleUIController.cs |
| 4 | 实现 OnMoveClicked() 完整逻辑 | BattleUIController.cs |
| 5 | 添加 UpdateMoveButtonState() 方法 | BattleUIController.cs |
| 6 | 订阅/取消订阅 UnitMoved 事件 | BattleUIController.cs |
| 7 | 修改 OnGameEnded() 禁用按钮和隐藏面板 | BattleUIController.cs |
| 8 | 添加必要的 using 语句 | BattleUIController.cs |

## 验收标准

- [ ] 人类玩家选中己方 Unit 后，点击"移动"按钮，场景中的可移动格子被高亮
- [ ] 点击高亮格子后，Unit 正常移动到目标位置
- [ ] Unit 移动完成后（ActionPoints 耗尽或无可移动格子），"移动"按钮置灰
- [ ] AI 回合开始时，底部按钮面板隐藏
- [ ] 人类玩家回合开始时，底部按钮面板恢复显示
- [ ] 游戏结束时，底部按钮面板隐藏，移动按钮禁用
