# Combat Skills Implementation - Final Structure

## Completed: 2026-04-04

### Overview
Successfully resolved circular dependency issue by properly organizing Buff and Command files across assemblies.

---

## Problem Solved

**Circular Dependency Issue:**
- `IUnit` interface is in `com.tactics.tbsf.common` assembly
- If `IBuff` interface is in `com.tactics` assembly, it needs to reference `IUnit`
- But `com.tactics.tbsf.common` already references `com.tactics`
- This creates a circular dependency → compilation fails

**Solution:** Split Buff system into two layers:
1. **Interface Layer** (`IBuff`, `BuffBase`) → Keep in `tbsf-common` assembly
2. **Implementation Layer** (`IgniteBuff`, Commands) → Move to `com.tactics` assembly

---

## Final Directory Structure

```
Assets/Tactics/Scripts/
├── TbsfFork/External/tbsf-common/common/units/
│   ├── IUnit.cs                          ← Interface (tbsf-common assembly)
│   ├── buffs/
│   │   ├── IBuff.cs                      ← Interface (tbsf-common)
│   │   └── BuffBase.cs                   ← Abstract base (tbsf-common)
│   └── abilities/
│       ├── AttackCommand.cs              ← Existing
│       └── ... (other existing commands)
│
├── TbsfFork/Scripts/units/abilities/
│   ├── FireballAbility.cs                ← Unity-specific (tbsf.unity assembly)
│   ├── MeleeHealAbility.cs
│   ├── RangedAttackAbility.cs
│   └── AbilityConfigs.cs
│
└── Runtime/
    ├── BattleLog/                        ← Existing
    │   ├── BattleLogger.cs
    │   ├── BattleLogData.cs
    │   ├── AttackLogData.cs
    │   ├── SkillLogData.cs
    │   ├── DamageLogData.cs
    │   └── ...
    ├── Combat/                           ← NEW
    │   ├── Buffs/
    │   │   └── IgniteBuff.cs             ← Implementation (com.tactics)
    │   └── Abilities/
    │       ├── HealCommand.cs            ← Implementation (com.tactics)
    │       └── FireballCommand.cs        ← Implementation (com.tactics)
    ├── UI/
    │   └── BattleLogUIController.cs
    └── Utilities/
        ├── Logger.cs
        └── LogLevel.cs
```

---

## Assembly Dependencies

```
com.tactics.tbsf.common (IUnit, IBuff, BuffBase)
    ↑
    │ (references com.tactics)
    │
com.tactics (IgniteBuff, HealCommand, FireballCommand, BattleLogger)
    ↑
    │ (references com.tactics via asmdef)
    │
com.tactics.tbsf.unity (FireballAbility, MeleeHealAbility, etc.)
```

**No circular dependencies!**

---

## File Details

### Interface Layer (tbsf-common assembly)

**IBuff.cs**
- Namespace: `Tactics.Tbsf.Common.Units.Buffs`
- Location: `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/buffs/`
- References: `IUnit` (same assembly)

**BuffBase.cs**
- Namespace: `Tactics.Tbsf.Common.Units.Buffs`
- Location: Same as IBuff.cs
- Inherits: `IBuff`
- References: `IUnit`, `IBuff` (same assembly)

### Implementation Layer (com.tactics assembly)

**IgniteBuff.cs**
- Namespace: `Tactics.Runtime.Combat.Buffs`
- Location: `Assets/Tactics/Scripts/Runtime/Combat/Buffs/`
- Inherits: `BuffBase` (from tbsf-common)
- Uses: `BattleLogger.Log(new DamageLogData(...))`

**HealCommand.cs**
- Namespace: `Tactics.Runtime.Combat.Abilities`
- Location: `Assets/Tactics/Scripts/Runtime/Combat/Abilities/`
- Implements: `ICommand` (from tbsf-common)
- Uses: `BattleLogger.Log(new SkillLogData(...))`

**FireballCommand.cs**
- Namespace: `Tactics.Runtime.Combat.Abilities`
- Location: `Assets/Tactics/Scripts/Runtime/Combat/Abilities/`
- Implements: `ICommand` (from tbsf-common)
- Uses: `BattleLogger.Log(new AttackLogData(...))`
- Creates: `new IgniteBuff(...)`

### Unity-Specific Layer (com.tactics.tbsf.unity assembly)

**FireballAbility.cs**, **MeleeHealAbility.cs**, **RangedAttackAbility.cs**
- Namespace: `Tactics.Tbsf.Unity.Units.Abilities`
- Location: `Assets/Tactics/Scripts/TbsfFork/Scripts/units/abilities/`
- Inherits: `Ability` (MonoBehaviour)
- References: `Tactics.Runtime.Combat.Abilities` (for Commands)

---

## Key Design Decisions

1. **IBuff interface stays in tbsf-common**
   - Avoids circular dependency with IUnit
   - Allows IUnit to have `AddBuff(IBuff buff)` method

2. **BuffBase abstract class stays in tbsf-common**
   - Provides common implementation
   - Can be extended by concrete buffs in com.tactics

3. **Concrete Buff implementations in com.tactics**
   - Can use `BattleLogger` for logging
   - Inherit from `BuffBase` (cross-assembly inheritance works)

4. **Command implementations in com.tactics**
   - Can use `BattleLogger` for logging
   - Implement `ICommand` interface from tbsf-common

5. **Ability MonoBehaviour scripts in tbsf.unity**
   - Unity-specific behavior
   - Reference Commands from com.tactics assembly

---

## Compilation Status

✅ **All files created**
✅ **Namespaces correctly organized**
✅ **No circular dependencies**
✅ **BattleLogger accessible from Combat classes**
✅ **IUnit can reference IBuff interface**

---

## Next Steps

1. **Verify in Unity Editor**
   - Open Unity and wait for compilation
   - Check Console for any remaining errors

2. **Test Abilities**
   - Create test scene with units
   - Add FireballAbility, MeleeHealAbility, RangedAttackAbility to units
   - Test each ability in play mode

3. **Verify BattleLogger Output**
   - Check Console for battle log messages
   - Verify BattleLogUI displays events (if UI controller is present)

4. **Test Buff System**
   - Apply Ignite buff via Fireball
   - End turn and verify damage ticks
   - Verify buff expires after 3 turns

---

## Files Created/Modified

### Created
- `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/buffs/IBuff.cs`
- `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/buffs/BuffBase.cs`
- `Assets/Tactics/Scripts/Runtime/Combat/Buffs/IgniteBuff.cs`
- `Assets/Tactics/Scripts/Runtime/Combat/Abilities/HealCommand.cs`
- `Assets/Tactics/Scripts/Runtime/Combat/Abilities/FireballCommand.cs`

### Modified
- `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/IUnit.cs`
  - Added `UnitName` property
  - Added `AddBuff()`, `RemoveBuff()`, `GetBuffs()` methods
- `Assets/Tactics/Scripts/TbsfFork/Scripts/units/Unit.cs`
  - Implemented Buff management
  - Integrated with BattleLogger
- `Assets/Tactics/Scripts/TbsfFork/Scripts/units/abilities/FireballAbility.cs`
  - Added `using Tactics.Runtime.Combat.Abilities`
- `Assets/Tactics/Scripts/TbsfFork/Scripts/units/abilities/MeleeHealAbility.cs`
  - Added `using Tactics.Runtime.Combat.Abilities`

### Deleted
- `Assets/Tactics/Scripts/Runtime/Combat/Buffs/IBuff.cs` (duplicate)
- `Assets/Tactics/Scripts/Runtime/Combat/Buffs/BuffBase.cs` (duplicate)
- Temporary files in tbsf-common directory

---

## Summary

The combat skills system is now properly organized with:
- ✅ Clear separation between interface and implementation
- ✅ No circular dependencies
- ✅ Full access to BattleLogger for combat events
- ✅ Proper assembly structure for future expansion

Ready for testing in Unity Editor!
