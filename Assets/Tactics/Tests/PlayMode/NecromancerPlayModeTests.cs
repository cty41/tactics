using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class NecromancerPlayModeTests
    {
        [UnityTest]
        public IEnumerator EnemyDeath_IsDowned_AfterLethalDamage()
        {
            // Arrange: create a unit without BattleController
            var go = new GameObject("Enemy");
            var unit = go.AddComponent<Unit>();
            unit.Health = 10;
            unit.MaxHealth = 10;
            unit.DefenceFactor = 0;
            unit.Initialize(null);
            unit.Health = 10; // Initialize resets Health
            yield return null;

            // Act: kill enemy
            unit.ModifyHealth(-999, null);
            yield return null;

            // Assert
            Assert.That(unit.IsDowned, Is.True, "Enemy should be downed after lethal damage.");
            Assert.That(unit.Health, Is.LessThanOrEqualTo(0), "Enemy HP should be <= 0.");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [UnityTest]
        public IEnumerator Curse_AmplifiesDamage_By30Percent()
        {
            // Arrange
            var casterGo = new GameObject("Caster");
            var caster = casterGo.AddComponent<Unit>();
            caster.AttackFactor = 1;
            caster.Strength = 5;
            caster.Initialize(null);
            yield return null;

            var targetGo = new GameObject("Target");
            var target = targetGo.AddComponent<Unit>();
            target.DefenceFactor = 0;
            target.Initialize(null);
            target.Health = 100;
            yield return null;

            // Apply curse buff
            var curseConfig = ScriptableObject.CreateInstance<BuffConfig>();
            SetPrivateField(curseConfig, "_buffName", "CurseDamageAmplifier");
            SetPrivateField(curseConfig, "_effectType", BuffEffectType.CurseDamageAmplifier);
            SetPrivateField(curseConfig, "_defaultDuration", 6);
            SetPrivateField(curseConfig, "_curseCategory", "Curse");

            var curse = new Buff(curseConfig, caster, 6);
            target.AddBuff(curse);
            yield return null;

            // Verify buff applied
            Assert.That(target.GetActiveBuffs().Count, Is.GreaterThanOrEqualTo(1), "Curse buff should be applied.");
            Assert.That(target.BuffComponent.HasBuff(BuffEffectType.CurseDamageAmplifier), Is.True, "HasBuff should return true.");

            // Act: deal 10 damage
            CombatComponent.ApplyDamage(caster, target, 10f, false, ElementType.None,
                canTriggerBeforeAttacked: false, canCrit: false, canTriggerDamageTaken: false);
            yield return null;

            // Assert: 10 * 1.3 = 13, HP = 100 - 13 = 87
            Assert.That(target.Health, Is.EqualTo(87f), $"Cursed target should take 30% more damage. Actual HP: {target.Health}");

            UnityEngine.Object.DestroyImmediate(casterGo);
            UnityEngine.Object.DestroyImmediate(targetGo);
        }

        [UnityTest]
        public IEnumerator Curse_MutualExclusion_LaterReplacesEarlier()
        {
            // Arrange
            var casterGo = new GameObject("Caster");
            var caster = casterGo.AddComponent<Unit>();
            caster.Initialize(null);
            yield return null;

            var targetGo = new GameObject("Target");
            var target = targetGo.AddComponent<Unit>();
            target.Initialize(null);
            yield return null;

            var curseA = ScriptableObject.CreateInstance<BuffConfig>();
            SetPrivateField(curseA, "_buffName", "CurseA");
            SetPrivateField(curseA, "_effectType", BuffEffectType.CurseDamageAmplifier);
            SetPrivateField(curseA, "_defaultDuration", 6);
            SetPrivateField(curseA, "_curseCategory", "Curse");

            var curseB = ScriptableObject.CreateInstance<BuffConfig>();
            SetPrivateField(curseB, "_buffName", "CurseB");
            SetPrivateField(curseB, "_effectType", BuffEffectType.CurseDamageAmplifier);
            SetPrivateField(curseB, "_defaultDuration", 4);
            SetPrivateField(curseB, "_curseCategory", "Curse");

            // Act: apply CurseA then CurseB
            target.AddBuff(new Buff(curseA, caster, 6));
            target.AddBuff(new Buff(curseB, caster, 4));
            yield return null;

            // Assert: only CurseB remains
            var buffs = target.GetActiveBuffs();
            Assert.That(buffs.Count, Is.EqualTo(1), "Only one curse should remain.");
            Assert.That(buffs[0].BuffName, Is.EqualTo("CurseB"), "Later curse should replace earlier.");
            Assert.That(buffs[0].RemainingTurns, Is.EqualTo(4), "CurseB duration should be 4.");

            UnityEngine.Object.DestroyImmediate(casterGo);
            UnityEngine.Object.DestroyImmediate(targetGo);
        }

        [UnityTest]
        public IEnumerator LinkedDeath_NecromancerDeath_KillsSummonedUnit()
        {
            // Arrange
            var ownerGo = new GameObject("Owner");
            var owner = ownerGo.AddComponent<Unit>();
            owner.Initialize(null);
            yield return null;

            var summonedGo = new GameObject("Summoned");
            var summoned = summonedGo.AddComponent<Unit>();
            summoned.Initialize(null);
            yield return null;

            // Set up ownership
            summoned.OwnerUnitId = owner.UnitID;
            summoned.OwnerUnit = owner;
            owner.SummonedUnit = summoned;

            // Act: kill owner
            owner.ModifyHealth(-999, null);
            yield return null;

            // Assert: summoned should also be dead
            Assert.That(summoned.IsDowned, Is.True, "Summoned unit should die when owner dies.");
            Assert.That(owner.SummonedUnit, Is.Null, "Owner's SummonedUnit reference should be cleared.");

            UnityEngine.Object.DestroyImmediate(ownerGo);
            UnityEngine.Object.DestroyImmediate(summonedGo);
        }

        [UnityTest]
        public IEnumerator LinkedDeath_SummonedDeath_ClearsOwnerReference()
        {
            // Arrange
            var ownerGo = new GameObject("Owner");
            var owner = ownerGo.AddComponent<Unit>();
            owner.Initialize(null);
            yield return null;

            var summonedGo = new GameObject("Summoned");
            var summoned = summonedGo.AddComponent<Unit>();
            summoned.Initialize(null);
            yield return null;

            summoned.OwnerUnitId = owner.UnitID;
            summoned.OwnerUnit = owner;
            owner.SummonedUnit = summoned;

            // Act: kill summoned
            summoned.ModifyHealth(-999, null);
            yield return null;

            // Assert: owner reference cleared
            Assert.That(owner.SummonedUnit, Is.Null, "Owner's SummonedUnit should be cleared when summoned dies.");

            UnityEngine.Object.DestroyImmediate(ownerGo);
            UnityEngine.Object.DestroyImmediate(summonedGo);
        }

        [UnityTest]
        public IEnumerator Corpse_IsTaken_BlocksCell()
        {
            // Arrange: create a cell and unit
            var cellGo = new GameObject("Cell");
            var cell = cellGo.AddComponent<Square>();
            cell.GridCoordinates = new Vector2IntImpl(0, 0);
            cell.WorldPosition = new Vector3Impl(0, 0, 0);
            cell.MovementCost = 1f;
            yield return null;

            var unitGo = new GameObject("Unit");
            var unit = unitGo.AddComponent<Unit>();
            unit.Health = 10;
            unit.MaxHealth = 10;
            unit.CurrentCell = cell;
            cell.CurrentUnits.Add(unit);
            cell.IsTaken = true;
            yield return null;

            // Assert: cell is taken
            Assert.That(cell.IsTaken, Is.True, "Cell with unit should be taken.");

            // Act: mark as corpse
            unit.IsCorpse = true;
            yield return null;

            // Assert: cell still taken (corpse blocks)
            Assert.That(cell.IsTaken, Is.True, "Corpse cell should remain blocked.");
            Assert.That(cell.CurrentUnits.Contains(unit), Is.True, "Corpse should remain in cell's CurrentUnits.");

            UnityEngine.Object.DestroyImmediate(cellGo);
            UnityEngine.Object.DestroyImmediate(unitGo);
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }
}
