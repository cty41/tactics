# Combat Skills Directory Restructuring - Completed

## Date: 2026-04-04

### Overview
Successfully restructured the combat skills codebase to resolve assembly reference issues. All Buff and Command files have been moved from `tbsf-common` to the `com.tactics` assembly under `Runtime/Combat/`.

---

## New Directory Structure

```
Assets/Tactics/Scripts/Runtime/
├── BattleLog/                    (existing)
│   ├── BattleLogger.cs
│   ├── BattleLogData.cs
│   ├── AttackLogData.cs
│   ├── SkillLogData.cs
│   ├── DamageLogData.cs
│   ├── DestroyLogData.cs
│   └── TurnLogData.cs
├── Combat/                       (NEW)
│   ├── Buffs/
│   │   ├── IBuff.cs
│   │   ├── BuffBase.cs
│   │   └── IgniteBuff.cs
│   └── Abilities/
│       ├── HealCommand.cs
│       └── FireballCommand.cs
├── UI/
│   └── BattleLogUIController.cs
└── Utilities/
    ├── Logger.cs
    └── LogLevel.cs

Assets/Tactics/Scripts/TbsfFork/Scripts/units/abilities/
├── FireballAbility.cs
├── MeleeHealAbility.cs
├── RangedAttackAbility.cs
└── AbilityConfigs.cs
```

---

## Files Created

### Runtime/Combat/Buffs/
1. **IBuff.cs** - `namespace Tactics.Runtime.Combat.Buffs`
2. **BuffBase.cs** - `namespace Tactics.Runtime.Combat.Buffs`
3. **IgniteBuff.cs** - `namespace Tactics.Runtime.Combat.Buffs`
   - Uses `BattleLogger.Log(new DamageLogData(...))` for damage ticks
   - Uses `BattleLogger.Log(new SkillLogData(...))` for buff removal

### Runtime/Combat/Abilities/
1. **HealCommand.cs** - `namespace Tactics.Runtime.Combat.Abilities`
   - Uses `BattleLogger.Log(new SkillLogData(...))`
2. **FireballCommand.cs** - `namespace Tactics.Runtime.Combat.Abilities`
   - Uses `BattleLogger.Log(new AttackLogData(...))`
   - Creates `IgniteBuff` instances

---

## Files Modified

### Unit.cs
- Added `using Tactics.Runtime.BattleLog`
- Added `using Tactics.Runtime.Combat.Buffs`
- Removed `using Tactics.Runtime.Utilities` (Logger)
- Updated `AddBuff()` to use `BattleLogger.Log(new SkillLogData(...))`
- Updated `RemoveBuff()` to use `BattleLogger.Log(new SkillLogData(...))`

### IUnit.cs
- Updated namespace import: `using Tactics.Runtime.Combat.Buffs`
- Removed: `using Tactics.Tbsf.Common.Units.Buffs`
- Removed: `using Tactics.Tbsf.Common.Utilities`

### FireballAbility.cs
- Added `using Tactics.Runtime.Combat.Abilities`
- Can now reference `FireballCommand` directly

### MeleeHealAbility.cs
- Added `using Tactics.Runtime.Combat.Abilities`
- Can now reference `HealCommand` directly

---

## Files Deleted

1. `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/buffs/` (entire directory)
2. `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/HealCommand.cs`
3. `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/units/abilities/FireballCommand.cs`
4. `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/common/utilities/Logger.cs` (temporary file)

---

## Assembly Configuration

### com.tactics.asmdef
- Location: `Assets/Tactics/com.tactics.asmdef`
- Contains: All `Runtime/` code including new `Combat/` directory
- References: Multiple Unity and plugin assemblies

### com.tactics.tbsf.common.asmdef
- Location: `Assets/Tactics/Scripts/TbsfFork/External/tbsf-common/com.tactics.tbsf.common.asmdef`
- References: `com.tactics` (GUID: 42a6b88ef22e6ff4a94a0934f01a341d)
- Can now access `Tactics.Runtime.BattleLog.BattleLogger` and `Tactics.Runtime.Combat.*`

### com.tactics.tbsf.unity.asmdef
- Location: `Assets/Tactics/Scripts/TbsfFork/Scripts/com.tactics.tbsf.unity.asmdef`
- References: `com.tactics` and other required assemblies
- Contains: Unity-specific Ability implementations

---

## Benefits

1. ✅ **Direct BattleLogger Access** - All combat code can now use `BattleLogger` without cross-assembly issues
2. ✅ **Clear Separation** - Combat logic separated from TBS framework core
3. ✅ **Better Organization** - All battle-related code in `Runtime/` directory
4. ✅ **Consistent Logging** - All combat events logged through `BattleLogger`
5. ✅ **Future Extensibility** - Easy to add new Buffs and Commands

---

## Namespace Reference

| Component | Old Namespace | New Namespace |
|-----------|--------------|---------------|
| IBuff | `Tactics.Tbsf.Common.Units.Buffs` | `Tactics.Runtime.Combat.Buffs` |
| BuffBase | `Tactics.Tbsf.Common.Units.Buffs` | `Tactics.Runtime.Combat.Buffs` |
| IgniteBuff | `Tactics.Tbsf.Common.Units.Buffs` | `Tactics.Runtime.Combat.Buffs` |
| HealCommand | `Tactics.Tbsf.Common.Units.Abilities` | `Tactics.Runtime.Combat.Abilities` |
| FireballCommand | `Tactics.Tbsf.Common.Units.Abilities` | `Tactics.Runtime.Combat.Abilities` |

---

## Testing Checklist

- [ ] Verify Unity compilation succeeds
- [ ] Test Fireball ability (damage + ignite)
- [ ] Test MeleeHeal ability
- [ ] Test RangedAttack ability
- [ ] Verify BattleLogger output to console
- [ ] Verify BattleLogger output to UI (if BattleLogUIController present)
- [ ] Test Buff duration ticks (Ignite damage per turn)
- [ ] Test Buff removal after expiration

---

## Next Steps

1. **Verify Compilation** - Open Unity and check for compilation errors
2. **Test in Editor** - Create a test scene with units and abilities
3. **Balance Tuning** - Adjust damage/healing values as needed
4. **Visual Effects** - Add VFX for Fireball, Heal, and Ignite
5. **UI Integration** - Ensure BattleLogUI displays all events correctly

---

## Notes

- All `.meta` files will be automatically created by Unity when it refreshes the AssetDatabase
- If compilation issues persist, check that `com.tactics.tbsf.common.asmdef` has the correct GUID reference to `com.tactics`
- The `Combat` namespace is intentionally separate from `BattleLog` to maintain clear boundaries between combat mechanics and logging infrastructure
