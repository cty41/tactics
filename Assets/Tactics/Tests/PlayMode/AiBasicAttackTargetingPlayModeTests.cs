using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Utilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    /// <summary>
    /// Verifies that BasicAttack planning and execution share the ability's authoritative
    /// target legality contract.
    /// </summary>
    /// <remarks>
    /// The fixture initializes real Barbarian ability assets but keeps both players human,
    /// so no turn autoplay or recursive transition can mask a targeting mismatch.
    /// </remarks>
    public sealed class AiBasicAttackTargetingPlayModeTests
    {
        private GameObject _battleRoot;
        private GameObject _cellManagerRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_cellManagerRoot != null)
            {
                Object.DestroyImmediate(_cellManagerRoot);
                _cellManagerRoot = null;
            }

            if (_battleRoot != null)
            {
                Object.DestroyImmediate(_battleRoot);
                _battleRoot = null;
            }

            TestGameAssetHelper.Cleanup();
            yield return null;
            Assert.That(BattleController.Instance, Is.Null,
                "BasicAttack targeting fixture leaked the BattleController singleton.");
        }

        [UnityTest]
        public IEnumerator TargetInRange_UsesAuthoritativeBasicAttackTargetQuery()
        {
            var initialization = TestGameAssetHelper.EnsureInitialized();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!initialization.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(initialization.IsCompleted, Is.True,
                "GameAssetManager initialization exceeded the 10 second realtime deadline.");
            Assert.That(initialization.IsFaulted, Is.False,
                initialization.Exception?.ToString());

            CreateIsolatedBattle(out var battleController, out var attacker, out var target);
            var gridController = GetGridController(battleController);
            var attackAbility = attacker.GetBaseAbilities()
                .FirstOrDefault(ability => ability.DisplayName == "Melee Attack");

            Assert.That(attackAbility, Is.Not.Null,
                "The real Barbarian fixture must expose its Melee Attack ability.");
            Assert.That(attackAbility, Is.InstanceOf<IAbilityTargetingProvider>(),
                "Melee Attack must expose the authoritative target query contract.");

            var targetingProvider = (IAbilityTargetingProvider)attackAbility;
            var targetQuery = new AbilityTargetQuery(
                attacker,
                attacker.CurrentCell,
                gridController,
                new[] { target });
            bool queryAcceptsTarget = targetingProvider.QueryTargets(targetQuery).Options.Any(option =>
                Equals(option.TargetPoint, target.CurrentCell) && option.Targets.Contains(target));

            var attackInfo = new AbilityInfo(
                attackAbility.DisplayName,
                attacker.AttackRange,
                attackAbility.CanPerform(gridController),
                attackAbility,
                AbilityAiTags.Damage);
            var brain = AiBrainTestHelper.CreateAttackBrain();
            var intentNode = brain.DecisionGraph.Nodes
                .OfType<IntentNodeRecord>()
                .Single(node => node.IntentType == IntentType.BasicAttack);
            var candidate = new IntentCandidate(
                IntentType.BasicAttack,
                ActionType.Attack,
                target,
                attacker.CurrentCell,
                null,
                intentNode.BasePriority,
                sourceIntentNodeId: intentNode.NodeId);
            var context = new AiContext(
                attacker,
                gridController,
                new List<IUnit> { target },
                new List<IUnit>(),
                new List<ICell> { attacker.CurrentCell },
                new List<IUnit> { target },
                new List<AbilityInfo> { attackInfo },
                brain,
                new AiDecisionLog(false));

            RuleFilter.Filter(new List<IntentCandidate> { candidate }, context);

            Assert.That(candidate.PassedRules, Is.EqualTo(queryAcceptsTarget),
                $"BasicAttack target legality diverged: RuleFilter={candidate.PassedRules}, " +
                $"QueryTargets={queryAcceptsTarget}, attacker={attacker.CurrentCell?.GridCoordinates}, " +
                $"target={target.CurrentCell?.GridCoordinates}.");
        }

        [UnityTest]
        public IEnumerator BasicAttack_TargetDestroyedAfterPlanning_ReturnsStructuredFailure()
        {
            var initialization = TestGameAssetHelper.EnsureInitialized();
            float initializationDeadline = Time.realtimeSinceStartup + 10f;
            while (!initialization.IsCompleted && Time.realtimeSinceStartup < initializationDeadline)
                yield return null;

            Assert.That(initialization.IsCompleted, Is.True,
                "GameAssetManager initialization exceeded the 10 second realtime deadline.");
            Assert.That(initialization.IsFaulted, Is.False,
                initialization.Exception?.ToString());

            CreateIsolatedBattle(out var battleController, out var attacker, out var target);
            var gridController = GetGridController(battleController);
            var attackAbility = new PlannedAttackAbility();
            var attackInfo = new AbilityInfo(
                attackAbility.DisplayName,
                attacker.AttackRange,
                true,
                attackAbility,
                AbilityAiTags.Damage);
            var brain = AiBrainTestHelper.CreateAttackBrain();
            var context = new AiContext(
                attacker,
                gridController,
                new List<IUnit> { target },
                new List<IUnit>(),
                new List<ICell> { attacker.CurrentCell },
                new List<IUnit> { target },
                new List<AbilityInfo> { attackInfo },
                brain,
                new AiDecisionLog(false));

            var plannedAttack = AiBasicAttackTargeting.Resolve(
                context,
                target,
                attacker.CurrentCell);
            Assert.That(plannedAttack.Succeeded, Is.True, plannedAttack.FailureReason);

            var selected = new IntentCandidate(
                IntentType.BasicAttack,
                ActionType.Attack,
                target,
                attacker.CurrentCell,
                plannedAttack.Ability,
                50f,
                plannedAttack.TargetOption.Targets.ToList(),
                plannedAttack.TargetOption.TargetPoint);
            bool abilityUsed = false;
            attacker.BasicAbilityUsed += _ => abilityUsed = true;
            attacker.AbilityUsed += _ => abilityUsed = true;

            Object.DestroyImmediate(target.gameObject);
            var execution = IntentExecutor.ExecuteWithResult(selected, context);
            float executionDeadline = Time.realtimeSinceStartup + 2f;
            while (!execution.IsCompleted && Time.realtimeSinceStartup < executionDeadline)
                yield return null;

            Assert.That(execution.IsCompleted, Is.True,
                "Invalidated BasicAttack execution exceeded the 2 second realtime deadline.");
            Assert.That(execution.IsFaulted, Is.False,
                execution.Exception?.ToString());
            Assert.That(execution.Result.Succeeded, Is.False,
                "A target destroyed after planning must not be reported as a successful attack.");
            Assert.That(execution.Result.FailureReason, Does.Contain("destroyed"));
            Assert.That(abilityUsed, Is.False,
                "An invalidated BasicAttack must not publish AbilityUsed.");
            Assert.That(attackAbility.ExecuteCalled, Is.False,
                "Execution-time invalidation must happen before the planned executor is invoked.");
        }

        private void CreateIsolatedBattle(
            out BattleController battleController,
            out Unit attacker,
            out Unit target)
        {
            _battleRoot = new GameObject("AiBasicAttackTargetingBattle");
            _battleRoot.SetActive(false);
            battleController = _battleRoot.AddComponent<BattleController>();

            var controllerType = typeof(BattleController);
            controllerType.GetField("_startImmediatelly", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(battleController, false);

            _cellManagerRoot = new GameObject("AiBasicAttackTargetingCells");
            var cellManager = _cellManagerRoot.AddComponent<RegularCellManager>();
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    var cellObject = new GameObject($"Cell_{x}_{y}");
                    cellObject.transform.SetParent(_cellManagerRoot.transform);
                    var cell = cellObject.AddComponent<Square>();
                    cell.GridCoordinates = new Vector2IntImpl(x, y);
                    cell.WorldPosition = new Vector3Impl(x, y, 0);
                    cell.MovementCost = 1f;
                }
            }

            controllerType.GetField("_cellManager", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(battleController, cellManager);
            _battleRoot.SetActive(true);

            var gridController = GetGridController(battleController);
            gridController.GetType().GetProperty("BeforeUnitManagerInitialize")?.SetValue(gridController, null);
            battleController.SetPlayers(1, 1);

            var unitContainerField = controllerType.GetField(
                "_unitContainer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var unitContainer = unitContainerField?.GetValue(battleController) as Transform;
            if (unitContainer == null)
            {
                var containerObject = new GameObject("UnitContainer");
                containerObject.transform.SetParent(_battleRoot.transform);
                unitContainer = containerObject.transform;
                unitContainerField?.SetValue(battleController, unitContainer);
            }

            attacker = TestUnitFactory.CreateBarbarian(
                unitContainer,
                "BasicAttackAttacker",
                1,
                FindCell(0, 0));
            target = TestUnitFactory.CreateBarbarian(
                unitContainer,
                "BasicAttackTarget",
                2,
                FindCell(1, 0));

            battleController.InitializeAndStart(false);
        }

        private ICell FindCell(int x, int y)
        {
            return _cellManagerRoot.GetComponentsInChildren<Square>()
                .FirstOrDefault(cell =>
                    cell.GridCoordinates.x == x && cell.GridCoordinates.y == y);
        }

        private static IGridController GetGridController(BattleController battleController)
        {
            var field = typeof(BattleController).GetField(
                "_controller",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var gridController = field?.GetValue(battleController) as IGridController;
            Assert.That(gridController, Is.Not.Null,
                "BattleController must expose its initialized grid controller to the fixture.");
            return gridController;
        }

        private sealed class PlannedAttackAbility : IAbility, IAbilityTargetingProvider, IPlannedAbilityExecutor
        {
            public bool ExecuteCalled { get; private set; }
            public event System.Action<IAbility> AbilitySelected;
            public event System.Action<IAbility> AbilityDeselected;
            public IUnit UnitReference { get; set; }
            public string DisplayName => "Melee Attack";
            public Sprite Icon => null;
            public int Cost => 0;

            public void Initialize(IGridController gridController) { }
            public void Display(IGridController gridController) { }
            public void CleanUp(IGridController gridController) { }
            public void OnUnitClicked(IUnit unit, IGridController gridController) { }
            public void OnUnitHighlighted(IUnit unit, IGridController gridController) { }
            public void OnUnitDehighlighted(IUnit unit, IGridController gridController) { }
            public void OnUnitDestroyed(IGridController gridController) { }
            public void OnCellClicked(ICell cell, IGridController gridController) { }
            public void OnCellHighlighted(ICell cell, IGridController gridController) { }
            public void OnCellDehighlighted(ICell cell, IGridController gridController) { }
            public void OnAbilitySelected(IGridController gridController) { }
            public void OnAbilityDeselected(IGridController gridController) { }
            public void OnTurnStart(IGridController gridController) { }
            public void OnTurnEnd(IGridController gridController) { }
            public bool CanPerform(IGridController gridController) => true;
            public void InvokeAbilitySelected() => AbilitySelected?.Invoke(this);
            public void InvokeAbilityDeselected() => AbilityDeselected?.Invoke(this);

            public AbilityTargetResult QueryTargets(AbilityTargetQuery query)
            {
                var target = query.PotentialTargets.FirstOrDefault(candidate =>
                    candidate != null && !ReferenceEquals(candidate, query.Caster));
                return target?.CurrentCell == null
                    ? new AbilityTargetResult(null)
                    : new AbilityTargetResult(new[]
                    {
                        new AbilityTargetOption(target.CurrentCell, new[] { target })
                    });
            }

            public Task<AiActionExecutionResult> ExecuteAsync(AiActionPlan plan)
            {
                ExecuteCalled = true;
                return Task.FromResult(AiActionExecutionResult.Success(DisplayName));
            }
        }
    }
}
