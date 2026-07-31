using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Cells;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Units.Buffs;
using Tactics.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public sealed class SharedBattlePrimitivesTests
    {
        [Test]
        public void TurnEndManaRegen_UsesIntelligenceExactlyOnceAndSignalsActualGain()
        {
            using var world = new SkillGraphTestWorld();
            var unit = world.CreateUnit("ManaRegen", 0, world.CreateSquareCell("ManaRegenCell", 0, 0));
            unit.Intelligence = 7;
            unit.MaxMana = 30f;
            unit.Mana = 20f;

            TurnEndManaRestoredEventArgs? restoration = null;
            unit.TurnEndManaRestored += args => restoration = args;

            unit.OnTurnEnd(world.GridController);

            Assert.That(unit.Mana, Is.EqualTo(27f));
            Assert.That(restoration.HasValue, Is.True);
            Assert.That(restoration.Value.NewMana - restoration.Value.OldMana, Is.EqualTo(7f));
            Assert.That(restoration.Value.WorldPosition, Is.EqualTo(unit.transform.position));

            unit.PrepareForTurn();
            Assert.That(unit.Mana, Is.EqualTo(27f), "回合开始不应再次恢复 MP。");

            restoration = null;
            unit.Mana = unit.MaxMana;
            unit.OnTurnEnd(world.GridController);
            Assert.That(restoration.HasValue, Is.False, "MP 已满时不应触发恢复浮字事件。");
        }

        [Test]
        public void DamageCategoryAndElement_AreIndependent()
        {
            using var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
            var targetCell = world.CreateSquareCell("TargetCell", 1, 0);
            var caster = world.CreateUnit("Caster", 0, casterCell);
            var target = world.CreateUnit("Target", 1, targetCell);
            target.Health = 20f;
            target.DefenceFactor = 0;

            CombatComponent.ApplyDamageShield(target, 5f);
            var magic = CombatComponent.ApplyDamage(
                caster, target, 4f, false, DamageCategory.Magic, ElementType.None,
                canTriggerBeforeAttacked: false, canCrit: false, canTriggerDamageTaken: false,
                bypassDefense: true);
            Assert.That(magic.WasHit, Is.True);
            Assert.That(target.Health, Is.EqualTo(16f));
            Assert.That(CombatComponent.GetDamageShield(target), Is.EqualTo(5f));

            var lightningPhysical = CombatComponent.ApplyDamage(
                caster, target, 4f, false, DamageCategory.Physical, ElementType.Lightning,
                canTriggerBeforeAttacked: false, canCrit: false, canTriggerDamageTaken: false,
                bypassDefense: true);
            Assert.That(lightningPhysical.WasHit, Is.True);
            Assert.That(target.Health, Is.EqualTo(16f));
            Assert.That(CombatComponent.GetDamageShield(target), Is.EqualTo(1f));

            CombatComponent.ApplyDamage(
                caster, target, 1f, false, DamageCategory.Physical, ElementType.None,
                canTriggerBeforeAttacked: false, canCrit: false, canTriggerDamageTaken: false,
                bypassDefense: true);
            Assert.That(CombatComponent.GetDamageShield(target), Is.Zero);
        }

        [Test]
        public async Task FailedDirectHit_DoesNotApplyAttachedStatus()
        {
            using var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
            var targetCell = world.CreateSquareCell("TargetCell", 1, 0);
            var caster = world.CreateUnit("Caster", 0, casterCell);
            var target = world.CreateUnit("Target", 1, targetCell);

            var frozenConfig = CreateBuffConfig("Frozen", BuffEffectType.Frozen, 1);
            var attachedConfig = CreateBuffConfig("AttachedSlow", BuffEffectType.Slow, 2, speedModifier: -2f);
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            try
            {
                target.AddBuff(new Buff(frozenConfig, caster, 1));

                var start = graph.AddNode(SkillGraphNodeType.Start, Vector2.zero);
                var damage = (ApplyDamageNodeRecord)graph.AddNode(SkillGraphNodeType.ApplyDamage, Vector2.right);
                damage.BaseDamage = 5f;
                damage.DamageType = SkillGraphDamageType.Magical;
                damage.ElementType = ElementType.None;
                var applyBuff = (ApplyBuffNodeRecord)graph.AddNode(SkillGraphNodeType.ApplyBuff, Vector2.right * 2f);
                applyBuff.BuffConfig = attachedConfig;
                applyBuff.Duration = 2;
                applyBuff.RequiresSuccessfulHit = true;
                var end = graph.AddNode(SkillGraphNodeType.Finish, Vector2.right * 3f);
                graph.AddEdge(start.NodeId, damage.NodeId);
                graph.AddEdge(damage.NodeId, applyBuff.NodeId);
                graph.AddEdge(applyBuff.NodeId, end.NodeId);

                var definition = SkillGraphRuntimeDefinition.FromAsset(graph);
                var context = new SkillExecutionContext(caster, graph, definition, world.GridController)
                {
                    PrimaryTarget = target,
                    TargetPoint = targetCell
                };
                var result = await new SkillGraphRunner().Execute(context);

                Assert.That(result, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(target.GetActiveBuffs().Any(buff => buff.BuffName == "AttachedSlow"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graph);
                UnityEngine.Object.DestroyImmediate(frozenConfig);
                UnityEngine.Object.DestroyImmediate(attachedConfig);
            }
        }

        [Test]
        public void FacingResolver_UsesDominantAxisAndStableTieRule()
        {
            Assert.That(
                FacingResolver.TryResolve(new(0, 0), new(1, 3), FacingDirection.East, out var dominant),
                Is.True);
            Assert.That(dominant, Is.EqualTo(FacingDirection.North));

            FacingResolver.TryResolve(new(0, 0), new(-2, 2), FacingDirection.North, out var preservedTie);
            Assert.That(preservedTie, Is.EqualTo(FacingDirection.North));

            FacingResolver.TryResolve(new(0, 0), new(-2, 2), FacingDirection.South, out var horizontalTie);
            Assert.That(horizontalTie, Is.EqualTo(FacingDirection.West));
        }

        [Test]
        public void FearMovement_FacesEscapeDestinationBeforeRelocation()
        {
            using var world = new SkillGraphTestWorld();
            var sourceCell = world.CreateSquareCell("FearSource", 0, 0);
            var targetCell = world.CreateSquareCell("FearTarget", 1, 0);
            var escapeCell = world.CreateSquareCell("FearEscape", 2, 0);
            var source = world.CreateUnit("FearSourceUnit", 0, sourceCell);
            var target = world.CreateUnit("FearTargetUnit", 1, targetCell);
            var fearConfig = CreateBuffConfig("Fear", BuffEffectType.Fear, 1);
            try
            {
                target.Facing = FacingDirection.West;
                target.AddBuff(new Buff(fearConfig, source, 1));

                target.OnTurnStart(world.GridController);

                Assert.That(target.CurrentCell, Is.SameAs(escapeCell));
                Assert.That(target.Facing, Is.EqualTo(FacingDirection.East));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fearConfig);
            }
        }

        [Test]
        public void TilemapUnitDestroy_AfterCellManagerDestroy_DoesNotAccessDestroyedManager()
        {
            var managerObject = new GameObject("DestroyedCellManager");
            var unitObject = new GameObject("UnitDestroyedAfterCellManager");
            try
            {
                managerObject.AddComponent<TilemapCellManager>();
                var unit = unitObject.AddComponent<TilemapUnit>();
                unit.CurrentCell = new VirtualSquareCell(
                    new(0, 0),
                    new(0f, 0f, 0f),
                    1,
                    false,
                    null);
                unit.MarkAsFriendly();

                UnityEngine.Object.DestroyImmediate(managerObject);
                Assert.DoesNotThrow(() => UnityEngine.Object.DestroyImmediate(unitObject));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (managerObject != null)
                    UnityEngine.Object.DestroyImmediate(managerObject);
                if (unitObject != null)
                    UnityEngine.Object.DestroyImmediate(unitObject);
            }
        }

        [Test]
        public void RemoveUnitStateHighlight_DoesNotRecreateDestroyedRenderer()
        {
            var managerObject = new GameObject("CellManagerWithoutRenderer");
            try
            {
                var manager = managerObject.AddComponent<TilemapCellManager>();
                var renderer = managerObject.GetComponent<ProceduralTileHighlightRenderer>();
                Assert.That(renderer, Is.Not.Null);
                UnityEngine.Object.DestroyImmediate(renderer);

                manager.RemoveUnitStateHighlight(
                    new VirtualSquareCell(new(0, 0), new(0f, 0f, 0f), 1, false, null),
                    TileHighlightType.UnitFriendly);

                Assert.That(
                    managerObject.GetComponent<ProceduralTileHighlightRenderer>(),
                    Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void SummonRegistry_CleansAllCategoriesWhenOwnerDies()
        {
            using var world = new SkillGraphTestWorld();
            var owner = world.CreateUnit("Owner", 0, world.CreateSquareCell("OwnerCell", 0, 0));
            var fire = world.CreateUnit("Fire", 0, world.CreateSquareCell("FireCell", 1, 0));
            var skeleton = world.CreateUnit("Skeleton", 0, world.CreateSquareCell("SkeletonCell", 2, 0));
            var registry = SummonRegistry.For(world.GridController);

            registry.Register(owner, "FireDemon", fire, 2);
            registry.Register(owner, "Skeleton", skeleton, 3);
            registry.HandleUnitDeath(owner);

            Assert.That(registry.Entries, Is.Empty);
            Assert.That(fire.IsDowned, Is.True);
            Assert.That(skeleton.IsDowned, Is.True);
        }

        [Test]
        public void SummonRegistry_ClearRemovesLegacyOwnerReference()
        {
            using var world = new SkillGraphTestWorld();
            var owner = world.CreateUnit("Owner", 0, world.CreateSquareCell("OwnerCell", 0, 0));
            var summon = world.CreateUnit("Summon", 0, world.CreateSquareCell("SummonCell", 1, 0));
            var registry = SummonRegistry.For(world.GridController);

            registry.Register(owner, "Default", summon, 1);
            Assert.That(owner.SummonedUnit, Is.SameAs(summon));

            registry.Clear(despawnSummons: false);

            Assert.That(owner.SummonedUnit, Is.Null);
            Assert.That(summon.OwnerUnit, Is.Null);
            Assert.That(summon.IsDowned, Is.False);
        }

        [Test]
        public void StandardStatuses_MergeAcrossConfigsAndUseLockedTurnSemantics()
        {
            using var world = new SkillGraphTestWorld();
            var source = world.CreateUnit("Source", 0, world.CreateSquareCell("SourceCell", 0, 0));
            var target = world.CreateUnit("Target", 1, world.CreateSquareCell("TargetCell", 1, 0));
            target.Health = 30f;
            target.Speed = 8f;

            var burningA = CreateBuffConfig("BurningA", BuffEffectType.Burning, 2);
            var burningB = CreateBuffConfig("BurningB", BuffEffectType.Burning, 1);
            var poisonA = CreateBuffConfig("PoisonA", BuffEffectType.Poison, 99);
            var poisonB = CreateBuffConfig("PoisonB", BuffEffectType.Poison, 1);
            var slowA = CreateBuffConfig("SlowA", BuffEffectType.Slow, 2, speedModifier: -99f);
            var slowB = CreateBuffConfig("SlowB", BuffEffectType.Slow, 3, speedModifier: -1f);
            var stunA = CreateBuffConfig("StunA", BuffEffectType.Stun, 5);
            var stunB = CreateBuffConfig("StunB", BuffEffectType.Stun, 9);
            SetField(stunA, "_canAct", false);
            SetField(stunB, "_canAct", false);

            var configs = new[] { burningA, burningB, poisonA, poisonB, slowA, slowB, stunA, stunB };
            try
            {
                target.AddBuff(new Buff(burningA, source, 2));
                target.AddBuff(new Buff(burningB, source, 1));
                target.AddBuff(new Buff(poisonA, source, 99));
                target.AddBuff(new Buff(poisonB, source, 1));
                target.AddBuff(new Buff(slowA, source, 2));
                target.AddBuff(new Buff(slowB, source, 3));
                target.AddBuff(new Buff(stunA, source, 5));
                target.AddBuff(new Buff(stunB, source, 9));

                Assert.That(target.GetActiveBuffs().Count(buff => buff.Config.EffectType == BuffEffectType.Burning), Is.EqualTo(1));
                Assert.That(target.GetActiveBuffs().Single(buff => buff.Config.EffectType == BuffEffectType.Burning).StackCount, Is.EqualTo(3));
                Assert.That(target.GetActiveBuffs().Count(buff => buff.Config.EffectType == BuffEffectType.Poison), Is.EqualTo(1));
                Assert.That(target.GetActiveBuffs().Single(buff => buff.Config.EffectType == BuffEffectType.Poison).RemainingTurns, Is.EqualTo(6));
                Assert.That(target.GetActiveBuffs().Single(buff => buff.Config.EffectType == BuffEffectType.Stun).RemainingTurns, Is.EqualTo(1));
                Assert.That(target.Speed, Is.EqualTo(6f));
                Assert.That(target.CanAct, Is.False);

                target.OnTurnStart(world.GridController);
                Assert.That(target.Health, Is.EqualTo(25f));
                Assert.That(target.GetActiveBuffs().Single(buff => buff.Config.EffectType == BuffEffectType.Burning).StackCount, Is.EqualTo(2));

                target.OnTurnEnd(world.GridController);
                Assert.That(target.GetActiveBuffs().Any(buff => buff.Config.EffectType == BuffEffectType.Stun), Is.False);
                Assert.That(target.CanAct, Is.True);
                Assert.That(target.GetActiveBuffs().Single(buff => buff.Config.EffectType == BuffEffectType.Poison).RemainingTurns, Is.EqualTo(5));
            }
            finally
            {
                foreach (var config in configs)
                    UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void TargetProtocols_ExposeAllSharedModesAndInference()
        {
            var expectedModes = new[]
            {
                SkillTargetMode.PrimaryUnit,
                SkillTargetMode.AnyCellCenter,
                SkillTargetMode.DirectionCone,
                SkillTargetMode.OrderedMultiTarget,
                SkillTargetMode.PhysicalObjectCell,
                SkillTargetMode.RecoveryAction,
                SkillTargetMode.PathlessMove
            };
            Assert.That(Enum.GetValues(typeof(SkillTargetMode)).Cast<SkillTargetMode>(), Is.EquivalentTo(expectedModes));

            AssertInferredTargetMode(SkillGraphNodeType.SelectTargetPoint, SkillTargetMode.AnyCellCenter);
            AssertInferredTargetMode(SkillGraphNodeType.SelectCorpseTarget, SkillTargetMode.PhysicalObjectCell);
            AssertInferredTargetMode(SkillGraphNodeType.MultiStab, SkillTargetMode.OrderedMultiTarget);
            AssertInferredTargetMode(SkillGraphNodeType.Teleport, SkillTargetMode.PathlessMove);
        }

        [Test]
        public void PendingBuffSnapshot_PreservesSharedStatusFields()
        {
            var config = CreateBuffConfig("Slow", BuffEffectType.Slow, 2, speedModifier: -2f);
            SetField(config, "_damageCategory", DamageCategory.Physical);
            SetField(config, "_elementType", ElementType.Lightning);
            SetField(config, "_refreshStrategy", BuffRefreshStrategy.RefreshDuration);
            SetField(config, "_damageReductionPercent", 0.25f);
            try
            {
                var snapshot = Tactics.Roster.CharacterDefinition.PendingBuffSnapshot.FromConfig(config);
                var restored = snapshot.ToRuntimeConfig();
                try
                {
                    Assert.That(restored.DamageCategory, Is.EqualTo(DamageCategory.Physical));
                    Assert.That(restored.ElementType, Is.EqualTo(ElementType.Lightning));
                    Assert.That(restored.RefreshStrategy, Is.EqualTo(BuffRefreshStrategy.RefreshDuration));
                    Assert.That(restored.SpeedModifier, Is.EqualTo(-2f));
                    Assert.That(restored.DamageReductionPercent, Is.EqualTo(0.25f));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(restored);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [TestCase("facing-and-initiative.plan.json")]
        [TestCase("status-turn-semantics.plan.json")]
        [TestCase("summon-registry-order.plan.json")]
        [TestCase("ability-availability-reason.plan.json")]
        [TestCase("ordered-target-selection-state.plan.json")]
        public async Task GameplayRunner_ExecutesSharedPrimitivePlan(string fileName)
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Tests",
                "gameplay-specs",
                "shared",
                fileName));
            Assert.That(File.Exists(path), Is.True, $"Plan file not found: {path}");

            var plan = ExecutableScenarioPlanLoader.FromFile(path);
            var result = await new GameplayRuntimeRunner().ExecuteAsync(plan);
            Assert.That(result.Passed, Is.True, string.Join("\n", result.Diagnostics));
        }

        private static BuffConfig CreateBuffConfig(
            string name,
            BuffEffectType effectType,
            int duration,
            float speedModifier = 0f)
        {
            var config = ScriptableObject.CreateInstance<BuffConfig>();
            SetField(config, "_buffName", name);
            SetField(config, "_effectType", effectType);
            SetField(config, "_defaultDuration", duration);
            SetField(config, "_speedModifier", speedModifier);
            SetField(config, "_polarity", BuffPolarity.Harmful);
            return config;
        }

        private static void AssertInferredTargetMode(SkillGraphNodeType nodeType, SkillTargetMode expected)
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            try
            {
                graph.AddNode(nodeType, Vector2.zero);
                Assert.That(graph.ResolveTargetMode(), Is.EqualTo(expected));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graph);
            }
        }

        private static void SetField<T>(BuffConfig config, string fieldName, T value)
        {
            typeof(BuffConfig).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(config, value);
        }

    }
}
