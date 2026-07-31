using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common.Cells;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public sealed class FacingBehaviorPlayModeTests
    {
        [UnityTest]
        public IEnumerator MoveCommand_UpdatesFacingBeforeEveryPathSegment()
        {
            using var world = new SkillGraphTestWorld();
            var source = world.CreateSquareCell("Source", 0, 0);
            var east = world.CreateSquareCell("East", 1, 0);
            var north = world.CreateSquareCell("North", 1, 1);
            var west = world.CreateSquareCell("West", 0, 1);
            var unit = world.CreateUnit("Mover", 0, source);
            unit.MovementPoints = 10f;
            unit.MaxMovementPoints = 10f;
            unit.MovementAnimationSpeed = 10000f;
            unit.Facing = FacingDirection.South;

            var changes = new List<FacingDirection>();
            unit.FacingChanged += args => changes.Add(args.Current);

            var task = new MoveCommand(source, west, new[] { east, north, west })
                .Execute(unit, world.GridController);
            yield return WaitForTask(task);

            Assert.That(task.IsFaulted, Is.False);
            Assert.That(changes, Is.EqualTo(new[]
            {
                FacingDirection.East,
                FacingDirection.North,
                FacingDirection.West
            }));
            Assert.That(unit.CurrentCell, Is.SameAs(west));
        }

        [UnityTest]
        public IEnumerator ChargeRetreat_PreservesDisplacedTargetFacing()
        {
            using var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
            world.CreateSquareCell("ApproachCell", 1, 0);
            var targetCell = world.CreateSquareCell("TargetCell", 2, 0);
            var retreatCell = world.CreateSquareCell("RetreatCell", 3, 0);
            var caster = world.CreateUnit("Caster", 0, casterCell);
            var target = world.CreateUnit("Target", 1, targetCell);
            caster.MovementAnimationSpeed = 10000f;
            target.MovementAnimationSpeed = 10000f;
            caster.Facing = FacingDirection.South;
            target.Facing = FacingDirection.North;

            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            try
            {
                var start = graph.AddNode(SkillGraphNodeType.Start, Vector2.zero);
                var finish = graph.AddNode(SkillGraphNodeType.Finish, Vector2.right);
                graph.AddEdge(start.NodeId, finish.NodeId);
                var context = new SkillExecutionContext(
                    caster,
                    graph,
                    SkillGraphRuntimeDefinition.FromAsset(graph),
                    world.GridController)
                {
                    PrimaryTarget = target,
                    TargetPoint = targetCell
                };
                var record = new DashToTargetNodeRecord { MaxRange = 4 };

                var task = new DashToTargetNodeExecutor().Execute(record, context);
                yield return WaitForTask(task);

                Assert.That(task.IsFaulted, Is.False);
                Assert.That(task.Result.IsSuccess, Is.True, task.Result.FailReason);
                Assert.That(target.CurrentCell, Is.SameAs(retreatCell));
                Assert.That(target.Facing, Is.EqualTo(FacingDirection.North));
                Assert.That(caster.Facing, Is.EqualTo(FacingDirection.East));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graph);
            }
        }

        [UnityTest]
        public IEnumerator LaunchUnit_PreservesDisplacedTargetFacing()
        {
            using var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
            var targetCell = world.CreateSquareCell("TargetCell", 1, 0);
            var landingCell = world.CreateSquareCell("LandingCell", 2, 0);
            var caster = world.CreateUnit("Caster", 0, casterCell);
            var target = world.CreateUnit("Target", 1, targetCell);
            target.Facing = FacingDirection.North;

            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            try
            {
                var start = graph.AddNode(SkillGraphNodeType.Start, Vector2.zero);
                var finish = graph.AddNode(SkillGraphNodeType.Finish, Vector2.right);
                graph.AddEdge(start.NodeId, finish.NodeId);
                var context = new SkillExecutionContext(
                    caster,
                    graph,
                    SkillGraphRuntimeDefinition.FromAsset(graph),
                    world.GridController)
                {
                    PrimaryTarget = target,
                    TargetPoint = targetCell
                };
                var record = new LaunchUnitNodeRecord
                {
                    LaunchDistance = 1,
                    LandingDamage = 0f,
                    FlightDuration = 0.01f
                };

                var task = new LaunchUnitNodeExecutor().Execute(record, context);
                yield return WaitForTask(task);

                Assert.That(task.IsFaulted, Is.False);
                Assert.That(task.Result.IsSuccess, Is.True, task.Result.FailReason);
                Assert.That(target.CurrentCell, Is.SameAs(landingCell));
                Assert.That(target.Facing, Is.EqualTo(FacingDirection.North));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void SkillHover_UpdatesFacingForCellsAndUnitsWithoutLegalityRequirement()
        {
            using var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
            var northCell = world.CreateSquareCell("NorthCell", 0, 1);
            var farWestCell = world.CreateSquareCell("FarWestCell", -4, 0);
            var caster = world.CreateUnit("Caster", 0, casterCell);
            var northTarget = world.CreateUnit("NorthTarget", 1, northCell);
            var westTarget = world.CreateUnit("WestTarget", 1, farWestCell);
            var (graph, config) = CreateSingleTargetAbility("HoverFacing", 1);
            try
            {
                var ability = new SkillGraphAbilityImpl(caster, config);
                ability.Initialize(world.GridController);
                ability.OnAbilitySelected(world.GridController);

                caster.Facing = FacingDirection.South;
                ability.OnCellHighlighted(northCell, world.GridController);
                Assert.That(caster.Facing, Is.EqualTo(FacingDirection.North));

                ability.OnUnitHighlighted(westTarget, world.GridController);
                Assert.That(caster.Facing, Is.EqualTo(FacingDirection.West));

                ability.OnUnitDehighlighted(northTarget, world.GridController);
                ability.OnAbilityDeselected(world.GridController);
                Assert.That(caster.Facing, Is.EqualTo(FacingDirection.West));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void MoveHover_UsesReachablePathFirstSegment()
        {
            using var world = new SkillGraphTestWorld();
            var source = world.CreateSquareCell("Source", 0, 0);
            world.CreateSquareCell("East", 1, 0);
            world.CreateSquareCell("North", 0, 1);
            var destination = world.CreateSquareCell("Destination", 1, 1);
            var unreachable = world.CreateSquareCell("Unreachable", -5, 0);
            var caster = world.CreateUnit("Caster", 0, source);
            caster.MovementPoints = 10f;
            caster.MaxMovementPoints = 10f;
            caster.Facing = FacingDirection.South;

            var ability = new SkillGraphAbilityImpl(caster, SkillGraphAbilityConfig.CreateDefaultMoveConfig());
            ability.Initialize(world.GridController);
            ability.OnAbilitySelected(world.GridController);
            var path = caster.FindPath(destination, world.CellManager);
            Assert.That(path, Is.Not.Empty);
            Assert.That(
                FacingResolver.TryResolve(
                    source.GridCoordinates,
                    path[0].GridCoordinates,
                    caster.Facing,
                    out var expected),
                Is.True);

            ability.OnCellHighlighted(destination, world.GridController);

            Assert.That(caster.Facing, Is.EqualTo(expected));

            ability.OnCellHighlighted(unreachable, world.GridController);
            Assert.That(caster.Facing, Is.EqualTo(FacingDirection.West));
        }

        [Test]
        public void OrderedTargetHover_DoesNotChangeLockedCone()
        {
            using var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
            var eastCell = world.CreateSquareCell("EastCell", 1, 0);
            var northCell = world.CreateSquareCell("NorthCell", 0, 1);
            var caster = world.CreateUnit("Caster", 0, casterCell);
            var eastTarget = world.CreateUnit("EastTarget", 1, eastCell);
            var northTarget = world.CreateUnit("NorthTarget", 1, northCell);
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            var start = graph.AddNode(SkillGraphNodeType.Start, Vector2.zero);
            var multiStab = graph.AddNode(SkillGraphNodeType.MultiStab, Vector2.right);
            var finish = graph.AddNode(SkillGraphNodeType.Finish, Vector2.right * 2f);
            graph.AddEdge(start.NodeId, multiStab.NodeId);
            graph.AddEdge(multiStab.NodeId, finish.NodeId);
            var config = SkillGraphAbilityConfig.CreateRuntime("OrderedHover", graph, 3);
            caster.Facing = FacingDirection.East;

            try
            {
                var ability = new SkillGraphAbilityImpl(caster, config);
                ability.Initialize(world.GridController);
                ability.OnAbilitySelected(world.GridController);
                ability.OnCellHighlighted(northCell, world.GridController);
                Assert.That(caster.Facing, Is.EqualTo(FacingDirection.North));

                var options = ability.QueryTargets(new AbilityTargetQuery(
                    caster,
                    casterCell,
                    world.GridController,
                    new IUnit[] { eastTarget, northTarget })).Options;

                Assert.That(options.Any(option => option.TargetPoint == eastCell), Is.True);
                Assert.That(options.Any(option => option.TargetPoint == northCell), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void FacingCoordinator_UnsubscribesWhenAnimationFails()
        {
            var unitObject = new GameObject("ThrowingMovementUnit");
            var sourceObject = new GameObject("Source");
            var destinationObject = new GameObject("Destination");
            try
            {
                var unit = unitObject.AddComponent<ThrowingMovementUnit>();
                var source = sourceObject.AddComponent<Square>();
                var destination = destinationObject.AddComponent<Square>();
                source.GridCoordinates = new(0, 0);
                destination.GridCoordinates = new(1, 0);
                unit.CurrentCell = source;
                unit.Facing = FacingDirection.South;

                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await FacingCoordinator.AnimateMovementAsync(
                        unit,
                        new[] { destination },
                        destination,
                        MovementFacingPolicy.FollowPath));

                unit.Facing = FacingDirection.North;
                unit.InvokeUnitLeftCell(new UnitChangedGridPositionEventArgs(unit, source, destination));
                Assert.That(unit.Facing, Is.EqualTo(FacingDirection.North));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unitObject);
                UnityEngine.Object.DestroyImmediate(sourceObject);
                UnityEngine.Object.DestroyImmediate(destinationObject);
            }
        }

        private static (SkillGraphAsset graph, SkillGraphAbilityConfig config) CreateSingleTargetAbility(
            string displayName,
            int range)
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = displayName;
            var start = graph.AddNode(SkillGraphNodeType.Start, Vector2.zero);
            var select = graph.AddNode(SkillGraphNodeType.SelectPrimaryTarget, Vector2.right);
            var finish = graph.AddNode(SkillGraphNodeType.Finish, Vector2.right * 2f);
            graph.AddEdge(start.NodeId, select.NodeId);
            graph.AddEdge(select.NodeId, finish.NodeId);
            return (graph, SkillGraphAbilityConfig.CreateRuntime(displayName, graph, range));
        }

        private static IEnumerator WaitForTask(Task task)
        {
            while (!task.IsCompleted)
                yield return null;
        }
    }

    public sealed class ThrowingMovementUnit : Unit
    {
        public override Task MovementAnimation(IEnumerable<ICell> path, ICell destination)
        {
            return Task.FromException(new InvalidOperationException("Expected test failure."));
        }
    }
}
