# Combat Skills Quick Reference

## How to Add Skills to a Unit

### In Unity Editor

1. **Select a Unit Prefab** (e.g., `Assets/Tactics/Arts/Prefabs/Units/YourUnit.prefab`)

2. **Add Component:**
   - For Fireball: `FireballAbility`
   - For MeleeHeal: `MeleeHealAbility`
   - For RangedAttack: `RangedAttackAbility`

3. **Configure Parameters** (or use ScriptableObject configs):

**FireballAbility:**
```
Cast Range: 4
AOE Radius: 1
Damage: 8
Mana Cost: 3
Ignite Duration: 3
Ignite Damage Per Turn: 1
```

**MeleeHealAbility:**
```
Heal Range: 1
Heal Amount: 3
```

**RangedAttackAbility:**
```
Max Range: 5
Min Range: 1
```

---

## ScriptableObject Configuration (Recommended)

### Create Config Assets

1. **Right-click** in Project window
2. **Navigate:** Create → Tactics → Abilities
3. **Select:**
   - `Fireball Config`
   - `MeleeHeal Config`
   - `RangedAttack Config`

### Use Config in Abilities

Modify ability scripts to reference the config:

```csharp
public class FireballAbility : Ability
{
    [SerializeField] private FireballConfig _config;
    
    // Use _config.CastRange, _config.Damage, etc.
}
```

---

## Testing in Editor

### Manual Test Steps

1. **Setup:**
   - Open your test scene
   - Place units on the grid
   - Ensure units have the new ability components

2. **Test Fireball:**
   - Select unit with Fireball
   - Click Fireball ability button
   - Target a cell within 4 tiles
   - Verify:
     - [ ] AOE highlights 5 cells (cross pattern)
     - [ ] All units in AOE take damage
     - [ ] All units get Ignite buff
     - [ ] Mana is reduced by 3
     - [ ] Battle log shows damage events

3. **Test MeleeHeal:**
   - Select unit with MeleeHeal
   - Click MeleeHeal ability button
   - Target adjacent wounded friendly unit
   - Verify:
     - [ ] Only friendly units highlighted
     - [ ] Only wounded units targetable
     - [ ] Target heals for 3 HP
     - [ ] Cannot overheal
     - [ ] Battle log shows heal event

4. **Test RangedAttack:**
   - Select unit with RangedAttack
   - Click RangedAttack ability button
   - Try to target adjacent enemy (should fail)
   - Target enemy at distance 2-5
   - Verify:
     - [ ] Adjacent enemies NOT targetable
     - [ ] Enemies at distance 2-5 are targetable
     - [ ] Damage is calculated correctly
     - [ ] Battle log shows attack event

5. **Test Ignite Buff:**
   - Apply Ignite to a unit
   - End turn
   - Verify:
     - [ ] Unit takes 1 damage at turn start
     - [ ] Buff duration decreases
     - [ ] Buff is removed after 3 turns
     - [ ] Battle log shows buff damage

---

## Console Commands (Debug)

### View Buffs on Unit

```csharp
// In Unity Console (with unit selected)
var unit = Selection.activeGameObject.GetComponent<IUnit>();
foreach (var buff in unit.GetBuffs())
{
    Debug.Log($"{buff.BuffName}: {buff.RemainingDuration} turns");
}
```

### Manually Add Ignite

```csharp
using Tactics.Tbsf.Common.Units.Buffs;

var unit = Selection.activeGameObject.GetComponent<IUnit>();
unit.AddBuff(new IgniteBuff(3, 1f));
```

---

## Troubleshooting

### Fireball not dealing damage?
- Check: Unit has enough Mana (≥3)
- Check: Target cells are within 4 tile range
- Check: Units are actually in the AOE cells

### MeleeHeal not healing?
- Check: Target is friendly (same PlayerNumber)
- Check: Target is adjacent (distance = 1)
- Check: Target is wounded (Health < MaxHealth)

### RangedAttack not attacking adjacent?
- This is correct behavior! MinRange = 1 means distance must be > 1
- Effective range is 2-5 tiles

### Ignite not dealing damage?
- Check: Buff is applied (use debug command above)
- Check: Turn actually ends (OnTurnStart is called)
- Check: Unit doesn't die before buff ticks

### Battle Log not showing?
- Check: BattleLogUIController is in the scene
- Check: Event subscription is correct
- Check: Console for errors

---

## Balance Tuning Guide

### If Fireball is too strong:
- Reduce Damage (8 → 6)
- Increase Mana Cost (3 → 4)
- Reduce Ignite Duration (3 → 2)

### If Fireball is too weak:
- Increase Damage (8 → 10)
- Decrease Mana Cost (3 → 2)
- Increase Ignite Damage (1 → 2)

### If MeleeHeal is too strong:
- Reduce Heal Amount (3 → 2)
- Add Mana Cost (0 → 1)
- Reduce Range (1 → 0, self-cast only)

### If MeleeHeal is too weak:
- Increase Heal Amount (3 → 4)
- Increase Range (1 → 2)

### If RangedAttack is too strong:
- Increase MinRange (1 → 2, effective range 3-5)
- Apply damage penalty vs melee (half scaling)

### If RangedAttack is too weak:
- Decrease MinRange (1 → 0, can attack adjacent)
- Increase MaxRange (5 → 6)

---

## Performance Notes

- **Buff Processing:** O(n) per turn where n = number of buffs
- **Fireball AOE:** O(1) - fixed 5 cells maximum
- **MeleeHeal Targeting:** O(n) where n = number of friendly units
- **RangedAttack Targeting:** O(n) where n = number of enemy units

**Optimization Tips:**
- Limit max buffs per unit (e.g., 5)
- Cache targeting results for 1 frame
- Use object pooling for buff instances (if they become complex)

---

## Extension Points

### Add New Buff Types

1. Create new class inheriting `BuffBase`:
```csharp
public class FreezeBuff : BuffBase
{
    public FreezeBuff(int duration) : base(duration) { }
    
    public override void OnTurnStart(IUnit target)
    {
        // Freeze logic (skip turn, reduce speed, etc.)
    }
}
```

2. Add buff in ability/command:
```csharp
target.AddBuff(new FreezeBuff(2));
```

### Add New Skills

1. Create `*Ability.cs` inheriting `Ability`
2. Create `*Command.cs` implementing `ICommand` (optional)
3. Implement targeting logic in `OnAbilitySelected` and `OnCellHighlighted`
4. Execute effect in `OnCellClicked` or `OnUnitClicked`
5. Log to `BattleLogger`

### Add Visual Effects

1. Add VFX prefabs to ability component
2. Instantiate VFX in command execution
3. Use AssetBundles for VFX loading
4. Release VFX after playback

---

## API Reference

### IBuff Interface

```csharp
public interface IBuff
{
    string BuffName { get; }
    int RemainingDuration { get; }
    void OnApplied(IUnit target);
    void OnTurnStart(IUnit target);
    void OnTurnEnd(IUnit target);
    void OnRemoved(IUnit target);
    void DecreaseDuration();
    bool IsExpired { get; }
}
```

### IUnit Buff Methods

```csharp
public interface IUnit
{
    void AddBuff(IBuff buff);
    void RemoveBuff(IBuff buff);
    IEnumerable<IBuff> GetBuffs();
}
```

### BattleLogger

```csharp
// Log attack
BattleLogger.Log(new AttackLogData
{
    Attacker = "...",
    Target = "...",
    Damage = 15,
    IsCritical = false,
    SkillName = "Fireball"
});

// Log skill
BattleLogger.Log(new SkillLogData
{
    Source = "...",
    SkillName = "Heal",
    Target = "...",
    ExtraData = new Dictionary<string, object> { ... }
});
```
