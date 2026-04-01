---
name: battle-log-code-generator
description: Automatically adds battle logging code to combat-related C# files
globs: ["**/Combat*.cs", "**/Attack*.cs", "**/Damage*.cs", "**/Health*.cs", "**/Ability*.cs", "**/Unit*.cs"]
alwaysApply: false
---

# Battle Log Code Generator Skill

This skill automatically adds battle logging code to combat-related C# files in the Unity project.

## Description

Analyzes C# code for combat-related methods and automatically inserts `BattleLogger.Log()` calls with appropriate `BattleLogData` subclasses.

## Capabilities

1. **Attack Logging** - Adds `AttackLogData` logging for attack actions
2. **Damage Logging** - Adds `DamageLogData` logging for damage calculations  
3. **Skill Logging** - Adds `SkillLogData` logging for ability/skill usage
4. **Turn Logging** - Adds `TurnLogData` logging for turn start/end events
5. **Destroy Logging** - Adds `DestroyLogData` logging for unit destruction

## Usage

Request examples:
- "Add battle logs to AttackAbilityImpl.cs"
- "Add logging to CombatComponent.cs"
- "Add damage logging to HealthSystem.cs"
- "Add turn logging to TurnManager.cs"

## Log Data Types

| Log Type | Class | Properties | Display Format |
|----------|-------|------------|----------------|
| Attack | `AttackLogData` | Attacker, Target, Damage, IsCritical | `[CRIT] Attacker -> Target : X dmg (CRITICAL!)` |
| Damage | `DamageLogData` | Source, Target, Damage, RemainingHealth | `[DMG] Target : HP X -> Y` |
| Skill | `SkillLogData` | Source, SkillName, Target | `[SKILL] Source used SkillName -> Target` |
| Turn | `TurnLogData` | PlayerNumber, TurnNumber, IsStart | `[TURN] Player X turn started/ended (Turn Y)` |
| Destroy | `DestroyLogData` | DestroyedUnit, Killer | `[DESTROY] DestroyedUnit was destroyed by Killer` |

## Log Templates

### Attack Log Template
```csharp
BattleLogger.Log(new AttackLogData
{
    Attacker = attackerName,
    Target = targetName,
    Damage = damage,
    IsCritical = isCritical
});
```

### Damage Log Template
```csharp
BattleLogger.Log(new DamageLogData
{
    Source = sourceName,
    Target = targetName,
    Damage = damage,
    RemainingHealth = remainingHealth
});
```

### Skill Log Template
```csharp
BattleLogger.Log(new SkillLogData
{
    Source = sourceName,
    SkillName = skillName,
    Target = targetName
});
```

### Turn Log Template
```csharp
BattleLogger.Log(new TurnLogData
{
    PlayerNumber = playerNumber,
    TurnNumber = turnNumber,
    IsStart = true // or false for turn end
});
```

### Destroy Log Template
```csharp
BattleLogger.Log(new DestroyLogData
{
    DestroyedUnit = unitName,
    Killer = killerName
});
```

## Integration Points

| Class | Method | Log Type |
|-------|--------|----------|
| CombatComponent | ModifyHealth | DamageLogData |
| CombatComponent | CalculateDamageDealt | AttackLogData |
| AttackCommand | Execute | AttackLogData + DamageLogData |
| Unit | InvokeDestroyed | DestroyLogData |
| Ability | Use | SkillLogData |
| TurnManager | StartTurn | TurnLogData |
| TurnManager | EndTurn | TurnLogData |

## Example Output

### Damage Logging Example
```csharp
// Before
public void ModifyHealth(float healthChangeAmount, IUnit sourceUnit)
{
    _unitReference.Health += healthChangeAmount;
    _unitReference.InvokeHealthChanged(new HealthChangedEventArgs(_unitReference, sourceUnit, healthChangeAmount));
}

// After
public void ModifyHealth(float healthChangeAmount, IUnit sourceUnit)
{
    float damage = Mathf.Abs(healthChangeAmount);
    float remainingHealth = _unitReference.Health;
    
    BattleLogger.Log(new DamageLogData
    {
        Source = sourceUnit?.name ?? "Unknown",
        Target = _unitReference.name,
        Damage = damage,
        RemainingHealth = remainingHealth
    });
    
    _unitReference.Health += healthChangeAmount;
    _unitReference.InvokeHealthChanged(new HealthChangedEventArgs(_unitReference, sourceUnit, healthChangeAmount));
}
```

### Attack Logging Example
```csharp
// Before
public void DealDamage(IUnit target, float damage, bool isCritical = false)
{
    target.ModifyHealth(-damage, this);
}

// After
public void DealDamage(IUnit target, float damage, bool isCritical = false)
{
    BattleLogger.Log(new AttackLogData
    {
        Attacker = this.name,
        Target = target.name,
        Damage = damage,
        IsCritical = isCritical
    });
    
    target.ModifyHealth(-damage, this);
}
```

### Turn Logging Example
```csharp
// Before
public void StartTurn(int playerNumber, int turnNumber)
{
    _currentPlayer = playerNumber;
    _currentTurn = turnNumber;
}

// After
public void StartTurn(int playerNumber, int turnNumber)
{
    BattleLogger.Log(new TurnLogData
    {
        PlayerNumber = playerNumber,
        TurnNumber = turnNumber,
        IsStart = true
    });
    
    _currentPlayer = playerNumber;
    _currentTurn = turnNumber;
}
```

## BattleLogger API

```csharp
// Log a battle event
BattleLogger.Log(BattleLogData data);

// Subscribe to UI events
BattleLogger.OnLogToUI += (data) => { /* Display in UI */ };

// Control output
BattleLogger.SetOutputToUI(false); // Only log to console/file
```

## Best Practices

1. **Log at the right level** - Use specific log types (Attack, Damage, Skill) rather than generic messages
2. **Include context** - Always include source and target names for clarity
3. **Log before state changes** - Log the action before modifying game state for accurate debugging
4. **Use null-safe access** - Use `?.name ?? "Unknown"` for optional references
5. **Consistent formatting** - Follow the display string format for UI consistency
