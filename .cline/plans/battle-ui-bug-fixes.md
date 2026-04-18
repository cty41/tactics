# 修复 BattleUIController 两个问题

## 问题分析

### 问题1：第一个AI行动时 BottomPanel 没有隐藏

**根因**：首次 `TurnStarted` 事件在 `UnityGridController.Start()` → `GridController.StartGame()` 中触发（Unity Start 阶段），此时 Battle UI 还未加载，`OnTurnStarted` 尚未订阅。

`InitializeCurrentTurnHPMP()` 在 `WireButtons()` 末尾调用，补偿了 HP/MP 更新，但**没有处理 BottomPanel 可见性和按钮状态**。

### 问题2：玩家Unit还没行动，移动按钮就被置灰

**根因**：`UpdateMoveButtonState()` 中调用 `unit.GetAvailableDestinations()` 依赖于 `CachePaths()` 先执行。但 `CachePaths()` 只在 `MoveAbilityImpl.OnAbilitySelected()` 中被调用。在 `OnTurnStarted` 时机，路径缓存不存在，`GetAvailableDestinations` 返回空列表 → canMove = false → 按钮被置灰。

**正确的判断逻辑**：应该直接检查 `MovementPoints > 0`（或 `ActionPoints > 0`）和是否有 MoveAbility，而不依赖 GetAvailableDestinations（因为 path cache 可能未初始化）。

## 修复方案

### 修改文件

**仅修改**：`Assets/Tactics/Scripts/UI/BattleUIController.cs`

### 修复1：在 InitializeCurrentTurnHPMP() 中补充面板状态初始化

```csharp
private void InitializeCurrentTurnHPMP()
{
    if (_gridController == null) return;

    var playableUnits = _gridController.TurnContext.PlayableUnits?.Invoke();
    var currentUnit = playableUnits?.FirstOrDefault();
    if (currentUnit != null)
    {
        _currentSelectedUnit = currentUnit;
        UpdateHPMPBars();
        UpdateMoveButtonState(currentUnit);

        if (_currentSelectedUnit is ICombatant combatant)
        {
            combatant.HealthChanged += OnUnitHealthChanged;
        }
    }

    // 根据当前回合玩家类型初始化面板可见性
    bool isHumanTurn = _gridController.TurnContext.CurrentPlayer.PlayerType == PlayerType.HumanPlayer;
    if (_bottomPanel != null)
        _bottomPanel.style.display = isHumanTurn ? DisplayStyle.Flex : DisplayStyle.None;
    _canEndTurn = isHumanTurn;
    if (_endTurnButton != null)
        _endTurnButton.SetEnabled(isHumanTurn);
}
```

### 修复2：修正 UpdateMoveButtonState() 判断逻辑

简化判断，不依赖 GetAvailableDestinations（因为 path cache 可能未初始化）：

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

    _moveButton.SetEnabled(canMove);
}
```

**关键改变**：移除 `GetAvailableDestinations` 检查，因为：
1. 它依赖 `CachePaths()` 先执行
2. `MoveAbilityImpl.CanPerform()` 已经做了相同检查（会调用 GetAvailableDestinations）
3. 在 OnTurnStarted 时 path cache 不存在，导致误判

如果确实需要检查"有可移动目的地"，应该在 `OnMoveClicked()` 时通过 `moveAbility.CanPerform()` 判断（已有此逻辑）。

## 实现步骤

| 步骤 | 操作 | 文件 |
|---|---|---|
| 1 | 修改 `InitializeCurrentTurnHPMP()` - 添加 BottomPanel 可见性和按钮状态初始化 | BattleUIController.cs |
| 2 | 修改 `UpdateMoveButtonState()` - 移除 GetAvailableDestinations 检查 | BattleUIController.cs |

## 验收标准

- [ ] 游戏启动时，如果第一个回合是 AI，BottomPanel 隐藏；如果是玩家，BottomPanel 显示
- [ ] 玩家回合开始时，移动按钮保持可用状态（不被误置灰）
- [ ] Unit 实际移动后（ActionPoints 耗尽），移动按钮正确置灰
