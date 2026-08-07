using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using UnityEditor;
using UnityEngine;

namespace Tactics.Tests.PlayMode
{
    public class FixedBoardGameplayBoundaryTests
    {
        [TestCase(1, 0)]
        [TestCase(0, 1)]
        public void ProjectileTargeting_DiagonalCornerUsesEitherSupercoverBlocker(int blockerX, int blockerY)
        {
            using var world = CreateFixedBoard();
            ICell casterCell = Cell(world, 0, 0);
            var caster = world.CreateUnit("Caster", 0, casterCell);
            var target = world.CreateUnit("Target", 1, Cell(world, 2, 2));
            Cell(world, blockerX, blockerY).IsTaken = true;

            SkillGraphAbilityImpl ability = CreateAbility(caster, "IceBolt_Graph_Ability.asset", world);
            AbilityTargetResult result = ability.QueryTargets(new AbilityTargetQuery(
                caster,
                casterCell,
                world.GridController,
                new IUnit[] { target }));

            Assert.That(result.Options.Any(option => ReferenceEquals(option.PrimaryTarget, target)), Is.False,
                $"A diagonal ray must not skip the supercover blocker at ({blockerX},{blockerY}).");
        }

        [Test]
        public void ProjectileTargeting_FromMinimumCornerHonorsInclusiveRangeFour()
        {
            using var world = CreateFixedBoard();
            ICell casterCell = Cell(world, 0, 0);
            var caster = world.CreateUnit("Caster", 0, casterCell);
            var atRange = world.CreateUnit("AtRange", 1, Cell(world, 4, 0));
            var beyondRange = world.CreateUnit("BeyondRange", 1, Cell(world, 5, 0));
            var oppositeCorner = world.CreateUnit("OppositeCorner", 1, Cell(world, 9, 9));

            SkillGraphAbilityImpl ability = CreateAbility(caster, "IceBolt_Graph_Ability.asset", world);
            AbilityTargetResult result = ability.QueryTargets(new AbilityTargetQuery(
                caster,
                casterCell,
                world.GridController,
                new IUnit[] { atRange, beyondRange, oppositeCorner }));

            Assert.That(result.Options.Select(option => option.PrimaryTarget), Has.Member(atRange));
            Assert.That(result.Options.Select(option => option.PrimaryTarget), Has.No.Member(beyondRange));
            Assert.That(result.Options.Select(option => option.PrimaryTarget), Has.No.Member(oppositeCorner));
            Assert.That(result.Options.All(option => BattleBoardSpec.Contains(
                option.TargetPoint.GridCoordinates.x,
                option.TargetPoint.GridCoordinates.y)), Is.True);
        }

        [Test]
        public void ProjectileTargeting_FromMaximumCornerHonorsInclusiveRangeFour()
        {
            using var world = CreateFixedBoard();
            ICell casterCell = Cell(world, 9, 9);
            var caster = world.CreateUnit("Caster", 0, casterCell);
            var atRange = world.CreateUnit("AtRange", 1, Cell(world, 5, 9));
            var beyondRange = world.CreateUnit("BeyondRange", 1, Cell(world, 4, 9));

            SkillGraphAbilityImpl ability = CreateAbility(caster, "IceBolt_Graph_Ability.asset", world);
            AbilityTargetResult result = ability.QueryTargets(new AbilityTargetQuery(
                caster,
                casterCell,
                world.GridController,
                new IUnit[] { atRange, beyondRange }));

            Assert.That(result.Options.Select(option => option.PrimaryTarget), Has.Member(atRange));
            Assert.That(result.Options.Select(option => option.PrimaryTarget), Has.No.Member(beyondRange));
            Assert.That(result.Options.All(option => BattleBoardSpec.Contains(
                option.TargetPoint.GridCoordinates.x,
                option.TargetPoint.GridCoordinates.y)), Is.True);
        }

        [Test]
        public void TeleportTargeting_RespectsAuthoredLineOfSightPolicy()
        {
            using var world = CreateFixedBoard();
            ICell casterCell = Cell(world, 0, 0);
            ICell destination = Cell(world, 2, 0);
            var caster = world.CreateUnit("Caster", 0, casterCell);
            Cell(world, 1, 0).IsTaken = true;

            SkillGraphAbilityImpl levelOne = CreateAbility(caster, "Teleport_Graph_Ability.asset", world);
            SkillGraphAbilityImpl levelTwo = CreateAbility(caster, "Teleport_Lv2_Graph_Ability.asset", world);
            levelOne.OnAbilitySelected(world.GridController);
            levelTwo.OnAbilitySelected(world.GridController);

            IReadOnlyCollection<ICell> blockedTargets = ValidTargetCells(levelOne);
            IReadOnlyCollection<ICell> unrestrictedTargets = ValidTargetCells(levelTwo);

            Assert.That(blockedTargets, Has.No.Member(destination),
                "Teleport Lv1 requires line of sight and must reject a destination behind a blocker.");
            Assert.That(unrestrictedTargets, Has.Member(destination),
                "Teleport Lv2 explicitly ignores line of sight and keeps the same destination legal.");
        }

        [Test]
        public void DynamicPlacementAcrossOppositeEdgesKeepsOccupancyConsistent()
        {
            using var world = CreateFixedBoard();
            ICell source = Cell(world, 0, 0);
            ICell destination = Cell(world, 9, 9);
            var unit = world.CreateUnit("Mover", 0, source);

            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Teleport_Lv2_Graph_Ability.asset").SkillGraph;
            var context = new SkillExecutionContext(
                unit,
                graph,
                SkillGraphRuntimeDefinition.FromAsset(graph),
                world.GridController)
            {
                TargetPoint = destination
            };

            SkillNodeExecutionResult result = new TeleportNodeExecutor()
                .Execute(new TeleportNodeRecord(), context)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.ResultType, Is.EqualTo(SkillNodeResultType.Success));
            Assert.That(source.CurrentUnits, Is.Empty);
            Assert.That(source.IsTaken, Is.False);
            Assert.That(destination.CurrentUnits, Is.EqualTo(new IUnit[] { unit }));
            Assert.That(destination.IsTaken, Is.True);
            Assert.That(unit.CurrentCell, Is.SameAs(destination));
            Assert.That(unit.CurrentCell.GridCoordinates.x, Is.EqualTo(9));
            Assert.That(unit.CurrentCell.GridCoordinates.y, Is.EqualTo(9));
        }

        [TestCase(0, 0, 4, 0, 3)]
        [TestCase(0, 4, 4, 4, 4)]
        [TestCase(4, 4, 4, 0, 5)]
        public void CrossAoe_RadiusOneClipsAtFixedBoardEdges(
            int centerX,
            int centerY,
            int casterX,
            int casterY,
            int expectedTargets)
        {
            using var world = CreateFixedBoard();
            var buffConfig = ScriptableObject.CreateInstance<BuffConfig>();
            try
            {
                var caster = world.CreateUnit("Caster", 0, Cell(world, casterX, casterY));
                var enemies = new List<IUnit>();
                IUnit center = null;
                foreach ((int dx, int dy) in new[] { (0, 0), (-1, 0), (1, 0), (0, -1), (0, 1) })
                {
                    int x = centerX + dx;
                    int y = centerY + dy;
                    if (!BattleBoardSpec.Contains(x, y))
                        continue;

                    IUnit enemy = world.CreateUnit($"Enemy_{x}_{y}", 1, Cell(world, x, y));
                    enemies.Add(enemy);
                    if (dx == 0 && dy == 0)
                        center = enemy;
                }

                IUnit diagonal = null;
                if (BattleBoardSpec.Contains(centerX + 1, centerY + 1))
                {
                    diagonal = world.CreateUnit(
                        "DiagonalControl",
                        1,
                        Cell(world, centerX + 1, centerY + 1));
                    enemies.Add(diagonal);
                }

                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });
                world.SetTurnContext(world.PlayerTwo, enemies);
                var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(
                    "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Curse_Lv2_Graph_Ability.asset").SkillGraph;
                var context = new SkillExecutionContext(
                    caster,
                    graph,
                    SkillGraphRuntimeDefinition.FromAsset(graph),
                    world.GridController);
                context.PrimaryTarget = center;
                context.TargetPoint = Cell(world, centerX, centerY);
                var record = new NecromancerSkillNodeRecord
                {
                    SkillKind = NecromancerSkillKind.AmplifyDamage,
                    Level = 2,
                    AmplifyDamageBuff = buffConfig
                };

                new NecromancerSkillNodeExecutor().Execute(record, context).GetAwaiter().GetResult();

                Assert.That(context.TargetSet.Count, Is.EqualTo(expectedTargets));
                if (diagonal != null)
                    Assert.That(context.TargetSet, Has.No.Member(diagonal), "Cross AOE must not include a diagonal cell.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(buffConfig);
            }
        }

        private static SkillGraphTestWorld CreateFixedBoard()
        {
            var world = new SkillGraphTestWorld();
            for (int x = 0; x < BattleBoardSpec.Width; x++)
            {
                for (int y = 0; y < BattleBoardSpec.Height; y++)
                    world.CreateSquareCell($"Cell_{x}_{y}", x, y);
            }
            return world;
        }

        private static SkillGraphAbilityImpl CreateAbility(Unit caster, string fileName, SkillGraphTestWorld world)
        {
            var config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(
                $"Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/{fileName}");
            Assert.That(config, Is.Not.Null, fileName);
            var ability = new SkillGraphAbilityImpl(caster, config);
            ability.Initialize(world.GridController);
            return ability;
        }

        private static ICell Cell(SkillGraphTestWorld world, int x, int y)
        {
            return world.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(x, y));
        }

        private static IReadOnlyCollection<ICell> ValidTargetCells(SkillGraphAbilityImpl ability)
        {
            FieldInfo field = typeof(SkillGraphAbilityImpl).GetField(
                "_validTargetCells",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (IReadOnlyCollection<ICell>)field.GetValue(ability);
        }
    }
}
