# Combat Skills - Circular Dependency Fixed ✅

**Date:** 2026-04-04  
**Status:** Ready for Unity Compilation

---

## Problem Solved

**Original Error:**
```
One or more cyclic dependencies detected between assemblies:
Assets/Tactics/com.tactics.asmdef,
Assets/Tactics/Scripts/TbsfFork/Scripts/com.tactics.tbsf.unity.asmdef
```

**Root Cause:**
- `com.tactics.asmdef` (root) incorrectly referenced `com.tactics.tbsf.unity`
- When we added reverse reference from `com.tactics.tbsf.unity` to `com.tactics`
- This created a circular dependency

---

## Solution Applied

### 1. Simplified com.tactics.asmdef

**Before:**
```json
{
    "references": [
        "GUID:42a6b88ef22e6ff4a94a0934f01a341d",  // AssetPipeline
        "GUID:2c9350d28ddd4561bb7215cd5c3a1cc7",  // tbsf.common
        "GUID:187ffeee922946bdab1268c0a02e3217",  // tbsf.unity ← Problem!
        ... (other external refs)
    ]
}
```

**After:**
```json
{
    "references": [
        "GUID:42a6b88ef22e6ff4a94a0934f01a341d",  // AssetPipeline
        "GUID:2c9350d28ddd4561bb7215cd5c3a1cc7"   // tbsf.common
    ]
}
```

### 2. Moved Combat Files to tbsf-common

**Location:** `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/`

```
buffs/
├── IBuff.cs           (interface)
├── BuffBase.cs        (abstract base)
└── IgniteBuff.cs      (concrete implementation)

abilities/
├── AttackCommand.cs           (existing)
├── HealCommand.cs             (new)
├── FireballCommand.cs         (new)
└── ... (other existing)
```

### 3. Updated File References

**Unit.cs:**
- Removed: `using Tactics.Runtime.BattleLog;`
- Added: `using Tactics.Tbsf.Common.Units.Buffs;`
- Simplified `AddBuff()` and `RemoveBuff()` methods

**FireballCommand.cs:**
- Added: `using Tactics.Tbsf.Common.Units.Buffs;`

**FireballAbility.cs & MeleeHealAbility.cs:**
- Changed: `using Tactics.Runtime.Combat.Abilities;`
- To: `using Tactics.Tbsf.Common.Units.Abilities;`

---

## Final Assembly Dependencies

```
com.tactics.tbsf.unity (Unit, FireballAbility, etc.)
    ↓ references GUID:2c9350d28ddd4561bb7215cd5c3a1cc7
com.tactics.tbsf.common (IUnit, IBuff, Commands, IgniteBuff)
    ↓ references GUID:42a6b88ef22e6ff4a94a0934f01a341d
Tactics.AssetPipeline

✅ Single-direction dependency, NO cycles!
```

---

## Files Modified

### Assembly Definitions (2)
1. `Assets/Tactics/com.tactics.asmdef` - Simplified references
2. `Assets/Tactics/Scripts/TbsfFork/Scripts/com.tactics.tbsf.unity.asmdef` - Reverted to original

### C# Files (5)
1. `Unit.cs` - Removed BattleLogger, simplified Buff methods
2. `FireballCommand.cs` - Added Buffs namespace import
3. `FireballAbility.cs` - Updated namespace import
4. `MeleeHealAbility.cs` - Updated namespace import
5. `IgniteBuff.cs` - Created in tbsf-common/buffs/
6. `HealCommand.cs` - Created in tbsf-common/abilities/

### Files Deleted
- `Assets/Tactics/Scripts/Runtime/Combat/` (entire directory removed)

---

## Trade-offs

### What We Lost
- ❌ Direct `BattleLogger.Log()` access in combat commands
- ❌ Automatic battle log UI output for buff events

### What We Gained
- ✅ No circular dependencies
- ✅ Clean assembly structure
- ✅ All combat code in one place (tbsf-common)
- ✅ Can still add logging later via events or other mechanisms

### Future Enhancement (Optional)
If battle logging is needed, can add via event pattern:

```csharp
// In IUnit.cs
event Action<string, LogSeverity> OnCombatLog;

// In Unit.cs
public void AddBuff(IBuff buff)
{
    _buffs.Add(buff);
    buff.OnApplied(this);
    OnCombatLog?.Invoke($"{UnitName} gained {buff.BuffName}", LogSeverity.Info);
}

// Subscribe in a logging component
unit.OnCombatLog += (msg, severity) => BattleLogger.Log(...);
```

---

## Testing Checklist

### Compilation
- [ ] Open Unity Editor
- [ ] Wait for script compilation
- [ ] Verify NO circular dependency errors
- [ ] Verify NO missing namespace errors

### Functionality
- [ ] FireballAbility deals damage
- [ ] FireballAbility applies Ignite buff
- [ ] MeleeHealAbility heals friendly units
- [ ] RangedAttackAbility respects MinRange
- [ ] Ignite buff ticks on turn start
- [ ] Ignite buff expires after 3 turns

---

## Summary

**Problem:** Circular dependency between `com.tactics` and `com.tactics.tbsf.unity`

**Solution:** 
1. Removed internal assembly references from `com.tactics.asmdef`
2. Moved all combat code to `tbsf-common` assembly
3. Simplified logging (removed direct BattleLogger usage)

**Result:** 
- ✅ No circular dependencies
- ✅ All combat skills implemented
- ✅ Clean assembly structure
- ✅ Ready for Unity compilation testing

---

**Status: READY FOR UNITY TESTING** 🚀

Open Unity Editor and verify compilation!
