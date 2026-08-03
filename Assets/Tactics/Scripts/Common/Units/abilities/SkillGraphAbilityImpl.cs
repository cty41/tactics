using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Interactables;
using Tactics.Common.Battle;
using Tactics.Common.Battle.Runtime;
using Tactics.Common.Units;
using Tactics.Common.Units.Tween;
using Tactics.Common.Utilities;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Runtime.BattleLog;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Overrides resource legality and completion handling for runtime graph abilities.
    /// </summary>
    public interface ISkillGraphUsePolicy
    {
        string DisplayName { get; }
        bool CanPerform(IGridController gridController);
        void CommitCompletedUse(SkillExecutionContext context);
    }

    /// <summary>
    /// Optional policy extension for abilities that need a stable disabled reason.
    /// </summary>
    public interface ISkillGraphAvailabilityPolicy : ISkillGraphUsePolicy
    {
        AbilityAvailability GetAvailability(IGridController gridController);
    }

    /// <summary>
    /// SkillGraph 能力实现。
    /// 通过 SkillGraphRunner 执行技能图。
    /// </summary>
    public class SkillGraphAbilityImpl : IAbility, IAiExecutableAbility, IAbilityTargetingProvider, IPlannedAbilityExecutor, IAbilityAvailabilityProvider
    {
        public event Action<IAbility> AbilitySelected;
        public event Action<IAbility> AbilityDeselected;

        private readonly IUnit _owner;
        private readonly SkillGraphAbilityConfig _config;
        private IGridController _gridController;
        private HashSet<ICell> _validTargetCells;
        private HashSet<ICell> _displayCells;
        private HashSet<ICell> _spearLocationGuidanceCells;
        private HashSet<ICell> _spearPickupGuidanceCells;
        private readonly ISkillGraphUsePolicy _usePolicy;
        private OrderedTargetSelectionState _orderedSelection;
        private FacingDirection _lockedSelectionFacing;

        public IUnit UnitReference { get; set; }
        public string DisplayName => _usePolicy?.DisplayName ?? _config.DisplayName;
        public Sprite Icon => _config.Icon;
        public int Cost => _config.ManaCost;
        public SkillGraphAsset SkillGraphAsset => _config.SkillGraph;
        public SkillTargetingProtocol TargetingProtocol => _config.SkillGraph?.Targeting;
        public SkillTargetMode TargetMode => _config.SkillGraph?.ResolveTargetMode() ?? SkillTargetMode.PrimaryUnit;
        public int TargetRange => _config.TargetRange;

        public SkillGraphAbilityImpl(
            IUnit owner,
            SkillGraphAbilityConfig config,
            ISkillGraphUsePolicy usePolicy = null)
        {
            _owner = owner;
            _config = config;
            _usePolicy = usePolicy;
            UnitReference = owner;
        }

        public void Initialize(IGridController gridController)
        {
            _gridController = gridController;
        }

        public void OnAbilitySelected(IGridController gridController)
        {
            _gridController = gridController;
            if (TargetMode == SkillTargetMode.OrderedMultiTarget)
            {
                _lockedSelectionFacing = _owner.Facing;
                _orderedSelection = new OrderedTargetSelectionState(GetOrderedSelectionCount());
            }
            _validTargetCells = CalculateValidTargetCells();
            _displayCells = CalculateDisplayCells();
            PrepareAmazonSpearMovementGuidance();
        }

        public void Display(IGridController gridController)
        {
            if (_displayCells != null && _displayCells.Count > 0)
            {
                gridController.CellManager.MarkAsReachable(_displayCells);
            }
            if (_spearLocationGuidanceCells != null && _spearLocationGuidanceCells.Count > 0)
            {
                gridController.CellManager.MarkAsGuidance(
                    _spearLocationGuidanceCells,
                    CellGuidanceType.SpearLocation);
            }
            if (_spearPickupGuidanceCells != null && _spearPickupGuidanceCells.Count > 0)
            {
                gridController.CellManager.MarkAsGuidance(
                    _spearPickupGuidanceCells,
                    CellGuidanceType.SpearPickup);
            }
        }

        public void CleanUp(IGridController gridController)
        {
            if (_displayCells != null)
            {
                gridController.CellManager.UnMark(_displayCells);
                _displayCells = null;
            }
            if (_validTargetCells != null)
            {
                _validTargetCells = null;
            }
            if (_spearLocationGuidanceCells != null)
            {
                gridController.CellManager.UnMarkGuidance(
                    _spearLocationGuidanceCells,
                    CellGuidanceType.SpearLocation);
                _spearLocationGuidanceCells = null;
            }
            if (_spearPickupGuidanceCells != null)
            {
                gridController.CellManager.UnMarkGuidance(
                    _spearPickupGuidanceCells,
                    CellGuidanceType.SpearPickup);
                _spearPickupGuidanceCells = null;
            }
        }

        public void OnUnitClicked(IUnit unit, IGridController gridController)
        {
            if (!CanPerform(gridController)) return;
            if (unit.CurrentCell == null) return;
            if (_validTargetCells == null || !_validTargetCells.Contains(unit.CurrentCell)) return;

            if (_orderedSelection != null)
            {
                if (!_orderedSelection.TryAdd(unit)) return;
                if (_orderedSelection.Stage == OrderedSelectionStage.Ready)
                {
                    var targets = _orderedSelection.Commit();
                    _ = ExecuteSkillGraphAsync(unit.CurrentCell, gridController, targets);
                }
                return;
            }

            _ = ExecuteSkillGraphAsync(unit.CurrentCell, gridController);
        }

        public void OnCellClicked(ICell cell, IGridController gridController)
        {
            if (!CanPerform(gridController)) return;
            if (_validTargetCells == null || !_validTargetCells.Contains(cell)) return;

            _ = ExecuteSkillGraphAsync(cell, gridController);
        }

        /// <summary>
        /// Executes a recovery action against its owner without entering a separate target-selection state.
        /// </summary>
        public async Task<SkillGraphRuntimeTestResult> ExecuteRecoveryActionAsync(IGridController gridController)
        {
            if (TargetMode != SkillTargetMode.RecoveryAction || gridController == null || _owner?.CurrentCell == null)
            {
                var failed = CreateTestResult();
                failed.ExecutionState = SkillGraphExecutionState.Failed;
                failed.LastError = "Recovery action is not available.";
                return failed;
            }

            OnAbilitySelected(gridController);
            if (!CanPerform(gridController) || _validTargetCells == null || !_validTargetCells.Contains(_owner.CurrentCell))
            {
                var failed = CreateTestResult();
                failed.ExecutionState = SkillGraphExecutionState.Failed;
                failed.LastError = "Recovery action has no valid self target.";
                CleanUp(gridController);
                return failed;
            }

            try
            {
                return await ExecuteSkillGraphAsync(_owner.CurrentCell, gridController);
            }
            finally
            {
                CleanUp(gridController);
            }
        }

        public void OnUnitHighlighted(IUnit unit, IGridController gridController)
        {
            PreviewFacing(unit?.CurrentCell, gridController);
        }
        public void OnUnitDehighlighted(IUnit unit, IGridController gridController) { }
        public void OnUnitDestroyed(IGridController gridController) { }
        public void OnCellHighlighted(ICell cell, IGridController gridController)
        {
            PreviewFacing(cell, gridController);
        }
        public void OnCellDehighlighted(ICell cell, IGridController gridController) { }
        public void OnAbilityDeselected(IGridController gridController) { }
        public void OnTurnStart(IGridController gridController) { }
        public void OnTurnEnd(IGridController gridController) { }

        public bool CanPerform(IGridController gridController)
        {
            return GetAvailability(gridController).CanExecute;
        }

        public AbilityAvailability GetAvailability(IGridController gridController)
        {
            if (_config?.SkillGraph == null)
                return AbilityAvailability.Disabled("技能配置缺失");
            if (_config.MaxUsesPerTurn > 0 && string.IsNullOrWhiteSpace(_config.DisplayName))
                return AbilityAvailability.Disabled("技能标识缺失");
            if (_usePolicy is ISkillGraphAvailabilityPolicy availabilityPolicy)
            {
                var availability = availabilityPolicy.GetAvailability(gridController);
                if (!availability.CanExecute)
                    return availability;
            }
            else if (_usePolicy != null && !_usePolicy.CanPerform(gridController))
            {
                return AbilityAvailability.Disabled("当前无法使用");
            }
            var amazonAvailability = GetAmazonSpearAvailability(gridController);
            if (!amazonAvailability.CanExecute)
                return amazonAvailability;
            if (_config.IsBasicAbility && _owner.HasUsedBasicAbilityThisTurn(_config.DisplayName))
                return AbilityAvailability.Disabled("本回合已使用");
            if (_config.MaxUsesPerTurn > 0 &&
                _owner.GetAbilityUseCountThisTurn(_config.DisplayName) >= _config.MaxUsesPerTurn)
                return AbilityAvailability.Disabled("本回合使用次数已用完");
            if (!_config.IsBasicAbility && _owner.Mana < _config.ManaCost)
                return AbilityAvailability.Disabled($"需要 {_config.ManaCost} 点魔法");
            if (FirstSelectionRequiresCorpse() && !(gridController?.CellManager?.GetCells() ?? Enumerable.Empty<ICell>())
                    .Any(HasCorpseInteractable))
            {
                return AbilityAvailability.Disabled("没有可用尸体");
            }
            return AbilityAvailability.Enabled();
        }

        public OrderedTargetSelectionState OrderedSelection => _orderedSelection;

        public bool UndoLastOrderedTarget() => _orderedSelection?.UndoLast() == true;

        public void CancelOrderedSelection()
        {
            _orderedSelection?.Cancel();
            _orderedSelection = null;
        }

        public async Task ExecuteEffectsAsync(IEnumerable<IUnit> targets, IGridController gridController)
        {
            if (targets == null) return;

            _gridController = gridController;
            _validTargetCells = CalculateValidTargetCells();

            // Legacy callers can only express a unit target. Execute the graph once even when
            // an AOE caller supplies every affected unit; resource costs belong to the cast.
            var target = targets.FirstOrDefault(unit => unit?.CurrentCell != null);
            if (target == null) return;
            if (_validTargetCells == null || !_validTargetCells.Contains(target.CurrentCell))
            {
                TLog.Warning($"[SkillGraphAbilityImpl] AI target {target.UnitID} out of range for '{DisplayName}'.");
                return;
            }

            await ExecuteSkillGraphAsync(target.CurrentCell, gridController);
        }

        public async Task<bool> ExecuteMoveForAI(ICell destination, IEnumerable<ICell> path, IGridController gridController)
        {
            if (destination == null || path == null) return false;
            if (destination.Equals(_owner.CurrentCell)) return true;

            var pathList = path.ToList();
            if (pathList.Count == 0) return false;

            var tcs = new TaskCompletionSource<bool>();
            _owner.AIExecuteAbility(new MoveCommand(_owner.CurrentCell, destination, pathList), gridController, tcs);

            var timeoutTask = Task.Delay(2000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            if (completedTask == timeoutTask)
            {
                TLog.Warning("[SkillGraphAbilityImpl] ExecuteMoveForAI timed out (2s).");
                return destination.Equals(_owner.CurrentCell);
            }

            return await tcs.Task && destination.Equals(_owner.CurrentCell);
        }

        /// <summary>
        /// Enumerates target options that are legal for both player execution and AI execution.
        /// The AI layer must not reconstruct SkillGraph targeting rules independently.
        /// </summary>
        public AbilityTargetResult QueryTargets(AbilityTargetQuery query)
        {
            var options = new List<AbilityTargetOption>();
            if (query?.GridController?.CellManager == null || _owner == null)
                return new AbilityTargetResult(options);

            _gridController = query.GridController;
            var candidates = query.PotentialTargets
                .Where(unit => unit != null)
                .Distinct()
                .ToList();

            var origin = query.OriginCell ?? _owner.CurrentCell;
            var validCells = CalculateValidTargetCells(origin);
            if (validCells == null || validCells.Count == 0)
                return new AbilityTargetResult(options);

            if (FirstSelectionRequiresSelf())
            {
                if (origin != null && validCells.Contains(origin))
                    options.Add(new AbilityTargetOption(origin, new[] { _owner }));
                return new AbilityTargetResult(options);
            }

            var first = FindFirstSelectionNode();
            bool requiresAlly = first is SelectAllyNodeRecord;
            bool allyIncludesSelf = first is SelectAllyNodeRecord allySelection && allySelection.IncludeSelf;
            bool requiresCorpse = first is SelectCorpseTargetNodeRecord;

            if (first is SelectTargetPointNodeRecord || IsThrust())
            {
                var area = FindAreaCollectionNode();
                foreach (var targetCell in validCells)
                {
                    var affected = area == null
                        ? GetLiveUnitsAt(targetCell, candidates)
                        : CollectAreaTargets(targetCell, area, candidates);
                    options.Add(new AbilityTargetOption(targetCell, affected));
                }
                return new AbilityTargetResult(options);
            }

            if (requiresCorpse)
            {
                foreach (var targetCell in validCells)
                    options.Add(new AbilityTargetOption(targetCell, new List<IUnit>()));
                return new AbilityTargetResult(options);
            }

            foreach (var target in candidates)
            {
                if (target.IsDowned || target.CurrentCell == null || !validCells.Contains(target.CurrentCell))
                    continue;

                bool isAlly = target.PlayerNumber == _owner.PlayerNumber;
                if (requiresAlly != isAlly)
                    continue;
                if (requiresAlly && !allyIncludesSelf && ReferenceEquals(target, _owner))
                    continue;

                options.Add(new AbilityTargetOption(target.CurrentCell, new[] { target }));
            }

            return new AbilityTargetResult(options);
        }

        public async Task<AiActionExecutionResult> ExecuteAsync(AiActionPlan plan)
        {
            if (plan?.GridController == null || plan.TargetPoint == null)
                return AiActionExecutionResult.Failure("Missing grid or target point.");

            var query = new AbilityTargetQuery(
                _owner,
                _owner.CurrentCell,
                plan.GridController,
                plan.Targets);
            var legal = QueryTargets(query).Options.Any(option => option.TargetPoint.Equals(plan.TargetPoint));
            if (!legal || !CanPerform(plan.GridController))
                return AiActionExecutionResult.Failure("Target point is no longer legal.");

            var result = await ExecuteSkillGraphAsync(plan.TargetPoint, plan.GridController, allowCombatTechniqueFollowUp: false);
            return result.ExecutionState == SkillGraphExecutionState.Completed
                ? AiActionExecutionResult.Success(DisplayName, !Equals(plan.Origin, plan.Destination))
                : AiActionExecutionResult.Failure(result.LastError ?? "Skill graph failed.");
        }

        public async Task<SkillGraphRuntimeTestResult> ExecuteForTestAsync(
            ICell selectedCell,
            IGridController gridController,
            IBattleRuntimeScope runtimeScope = null)
        {
            if (gridController == null)
                throw new ArgumentNullException(nameof(gridController));

            OnAbilitySelected(gridController);

            var result = CreateTestResult();
            result.Caster = SkillGraphTestUnitSnapshot.Capture(_owner);
            result.PrimaryTarget = CaptureTargetSnapshot(selectedCell);

            if (!CanPerform(gridController))
            {
                result.ExecutionState = SkillGraphExecutionState.Failed;
                result.LastError = "Not enough mana.";
                result.StepCount = 0;
                return result;
            }

            if (selectedCell == null)
            {
                result.ExecutionState = SkillGraphExecutionState.Failed;
                result.LastError = "No target selected.";
                result.StepCount = 0;
                return result;
            }

            bool hasUnitsOnCell = selectedCell.CurrentUnits != null && selectedCell.CurrentUnits.Count > 0;
            if (_validTargetCells == null || _validTargetCells.Count == 0)
            {
                result.ExecutionState = SkillGraphExecutionState.Failed;
                result.LastError = hasUnitsOnCell ? "Target out of range." : "No valid target in range.";
                result.StepCount = 0;
                return result;
            }

            if (!_validTargetCells.Contains(selectedCell))
            {
                result.ExecutionState = SkillGraphExecutionState.Failed;
                result.LastError = hasUnitsOnCell ? "Target out of range." : "No valid target in range.";
                result.StepCount = 0;
                return result;
            }

            return await ExecuteSkillGraphAsync(
                selectedCell,
                gridController,
                runtimeScope: runtimeScope);
        }

        public async Task<SkillGraphRuntimeTestResult> ExecuteOrderedForTestAsync(
            IEnumerable<IUnit> targets,
            IGridController gridController)
        {
            var ordered = targets?.ToList() ?? new List<IUnit>();
            OnAbilitySelected(gridController);
            if (ordered.Count != GetOrderedSelectionCount() || ordered.Any(target =>
                    target?.CurrentCell == null || !_validTargetCells.Contains(target.CurrentCell)))
            {
                var failed = CreateTestResult();
                failed.ExecutionState = SkillGraphExecutionState.Failed;
                failed.LastError = "Invalid ordered targets.";
                return failed;
            }
            return await ExecuteSkillGraphAsync(ordered[^1].CurrentCell, gridController, ordered);
        }

        public void InvokeAbilitySelected()
        {
            AbilitySelected?.Invoke(this);
        }

        public void InvokeAbilityDeselected()
        {
            AbilityDeselected?.Invoke(this);
        }

        private async Task<SkillGraphRuntimeTestResult> ExecuteSkillGraphAsync(
            ICell selectedCell,
            IGridController gridController,
            IReadOnlyList<IUnit> orderedTargets = null,
            bool allowCombatTechniqueFollowUp = true,
            IBattleRuntimeScope runtimeScope = null)
        {
            var runtimeDef = SkillGraphRuntimeDefinition.FromAsset(_config.SkillGraph);
            var context = new SkillExecutionContext(_owner, _config.SkillGraph, runtimeDef, gridController)
            {
                VfxSink = _config.SkillVfxRecipe != null
                    ? new SkillVfxCoordinator(_config.SkillVfxRecipe, _owner)
                    : null,
                PresentationGraph = _config.PresentationGraph
            };
            if (runtimeScope != null)
                context.RuntimeScope = runtimeScope;
            var testResult = CreateTestResult();

            // Pre-set target from cell click
            var unitsOnCell = selectedCell.CurrentUnits;
            if (unitsOnCell != null && unitsOnCell.Count > 0)
            {
                context.PrimaryTarget = unitsOnCell[0];
            }
            context.TargetPoint = selectedCell;
            if (orderedTargets != null)
            {
                context.TargetSet = orderedTargets.ToList();
                context.PrimaryTarget = orderedTargets.FirstOrDefault();
            }
            testResult.PrimaryTarget = SkillGraphTestUnitSnapshot.Capture(context.PrimaryTarget);

            var originalFacing = _owner.Facing;
            bool changedFacing = orderedTargets == null && GetAmazonNode()?.SkillKind != AmazonSkillKind.Decoy &&
                FacingCoordinator.FaceTarget(_owner, selectedCell);

            LogSkillUse(context.PrimaryTarget);

            var runner = new SkillGraphRunner();
            var executionState = SkillGraphExecutionState.Failed;
            try
            {
                var cancellationToken = context.RuntimeScope?.Token ?? context.CancellationToken;
                if (_config.VisualAction == UnitVisualAction.Cast)
                {
                    Vector3 sourceWorldPosition = SkillVfxPositionUtility.ResolveUnitCenter(_owner);
                    Vector3 targetWorldPosition = context.PrimaryTarget != null
                        ? SkillVfxPositionUtility.ResolveUnitCenter(context.PrimaryTarget)
                        : selectedCell.WorldPosition.ToVector3();
                    await context.PlayVfxAsync(
                        SkillVfxCueKind.CastCharge,
                        new SkillVfxCueContext(
                            context.ResolveSkillLevel(),
                            sourceWorldPosition,
                            targetWorldPosition,
                            targetWorldPosition - sourceWorldPosition,
                            primaryHitWorldPosition: sourceWorldPosition));
                }
                int prepared = 0;
                void PrepareRelease()
                {
                    if (System.Threading.Interlocked.Exchange(ref prepared, 1) == 0 && IsPoisonSpear())
                        PreparePoisonSpearReleaseVisual();
                }

                if (BattlePresentationCoordinator.HasEntry(
                        _config.PresentationGraph,
                        PresentationCueKind.Action))
                {
                    Task<SkillGraphExecutionState> gameplayTask = null;
                    int released = 0;
                    void Release(PresentationMarkerKind marker)
                    {
                        if (marker == PresentationMarkerKind.Release &&
                            System.Threading.Interlocked.Exchange(ref released, 1) == 0)
                        {
                            PrepareRelease();
                            gameplayTask = ExecuteReleasedGraphAsync(runner, context);
                        }
                    }

                    var presentationContext = new PresentationExecutionContext(
                        _owner,
                        context.PrimaryTarget,
                        selectedCell,
                        context.ResolveSkillLevel(),
                        cancellationToken,
                        context.RuntimeScope,
                        _config.PoseFamily,
                        PrepareRelease);
                    bool handled = await BattlePresentationCoordinator.TryPlayCueAsync(
                        _config.PresentationGraph,
                        PresentationCueKind.Action,
                        presentationContext,
                        Release);
                    if (!handled)
                    {
                        executionState = await UnitAnimationCoordinator.PlayActionAsync(
                            _owner,
                            _config.VisualAction,
                            _config.PoseFamily,
                            selectedCell,
                            PrepareRelease,
                            () => ExecuteReleasedGraphAsync(runner, context),
                            cancellationToken);
                    }
                    else
                    {
                        if (gameplayTask == null && !cancellationToken.IsCancellationRequested)
                            Release(PresentationMarkerKind.Release);
                        cancellationToken.ThrowIfCancellationRequested();
                        executionState = gameplayTask != null
                            ? await gameplayTask
                            : SkillGraphExecutionState.Aborted;
                    }
                }
                else
                {
                    executionState = await UnitAnimationCoordinator.PlayActionAsync(
                        _owner,
                        _config.VisualAction,
                        _config.PoseFamily,
                        selectedCell,
                        PrepareRelease,
                        () => ExecuteReleasedGraphAsync(runner, context),
                        cancellationToken);
                }
            }
            finally
            {
                if (executionState != SkillGraphExecutionState.Completed && changedFacing)
                    _owner.Facing = originalFacing;
            }

            try
            {
                if (executionState == SkillGraphExecutionState.Completed)
                {
                    TLog.Info($"[SkillGraphAbility] '{DisplayName}' completed successfully.");
                }
                else
                {
                    TLog.Warning($"[SkillGraphAbility] '{DisplayName}' ended with state: {executionState}. Error: {context.LastError}");
                }

                if (executionState == SkillGraphExecutionState.Completed)
                {
                    if (_config.IsBasicAbility)
                        _owner.MarkBasicAbilityUsed(_config.DisplayName);
                    else if (_config.MaxUsesPerTurn > 0)
                        _owner.MarkAbilityUsedThisTurn(_config.DisplayName);

                    if (_usePolicy != null)
                        _usePolicy.CommitCompletedUse(context);
                    else if (!_config.IsBasicAbility)
                        _owner.Mana -= _config.ManaCost;

                    if (allowCombatTechniqueFollowUp && IsAmazonSpearBasicAttack() &&
                        context.GetBlackboard("LastDamageHit", false) && context.PrimaryTarget != null &&
                        !context.PrimaryTarget.IsDowned && context.PrimaryTarget.Health > 0f &&
                        CombatComponent.RollCombatTechniqueFollowUp(_owner))
                    {
                        context.RecordEvent("CombatTechniqueFollowUp", "combat-techniques", context.PrimaryTarget);
                        var followUp = new SkillExecutionContext(_owner, _config.SkillGraph, runtimeDef, gridController)
                        {
                            PrimaryTarget = context.PrimaryTarget,
                            TargetPoint = context.PrimaryTarget.CurrentCell,
                            VfxSink = context.VfxSink,
                            PresentationGraph = context.PresentationGraph
                        };
                        await runner.Execute(followUp);
                    }
                }

                testResult.ExecutionState = executionState;
                testResult.LastError = context.LastError;
                testResult.StepCount = context.StepCount;
                testResult.Caster = SkillGraphTestUnitSnapshot.Capture(_owner);
                testResult.PrimaryTarget = SkillGraphTestUnitSnapshot.Capture(context.PrimaryTarget);
                testResult.ExecutionEvents.AddRange(context.ExecutionEvents);
                testResult.StageResults.AddRange(context.StageResults);
                return testResult;
            }
            finally
            {
                if (gridController != null)
                    gridController.GridState = new GridStateAwaitInput();
                _orderedSelection = null;
            }
        }

        private void LogSkillUse(IUnit target)
        {
            if (!TBattleLog.IsBattleActive)
                return;

            TBattleLog.Log(new SkillLogData
            {
                Source = GetUnitName(_owner),
                SkillName = DisplayName,
                Target = GetUnitName(target)
            });
        }

        private void PreviewFacing(ICell targetCell, IGridController gridController)
        {
            if (_owner?.CurrentCell == null || targetCell == null || FirstSelectionRequiresSelf())
                return;

            if (FirstSelectionRequiresMoveDestination() &&
                _validTargetCells?.Contains(targetCell) == true &&
                gridController?.CellManager != null)
            {
                var path = _owner.FindPath(targetCell, gridController.CellManager);
                var firstStep = path?.FirstOrDefault(cell =>
                    cell != null && !ReferenceEquals(cell, _owner.CurrentCell));
                if (firstStep != null)
                {
                    FacingCoordinator.FaceStep(_owner, _owner.CurrentCell, firstStep);
                    return;
                }
            }

            FacingCoordinator.FaceTarget(_owner, targetCell);
        }

        private static string GetUnitName(IUnit unit)
        {
            if (unit is INamedUnit named && !string.IsNullOrWhiteSpace(named.UnitName))
                return named.UnitName;

            return unit == null ? null : $"Unit_{unit.UnitID}";
        }

        private SkillGraphRuntimeTestResult CreateTestResult()
        {
            return new SkillGraphRuntimeTestResult
            {
                Name = DisplayName,
                GraphName = _config.SkillGraph?.DisplayName ?? DisplayName
            };
        }

        private static SkillGraphTestUnitSnapshot CaptureTargetSnapshot(ICell selectedCell)
        {
            if (selectedCell?.CurrentUnits == null || selectedCell.CurrentUnits.Count == 0)
                return null;

            return SkillGraphTestUnitSnapshot.Capture(selectedCell.CurrentUnits[0]);
        }

        private HashSet<ICell> CalculateDisplayCells()
        {
            var displayCells = new HashSet<ICell>();
            if (_gridController == null) return displayCells;

            int maxRange = GetTargetRange();
            var allCells = _gridController.CellManager.GetCells();
            var ownerCell = _owner.CurrentCell;
            int minRange = GetMinRangeFromGraph();
            bool cardinalOnly = UsesCardinalDash() || IsThrust() || RequiresStraightLineEndpoint();

            if (TryAddAmazonSpecialTargets(displayCells, ownerCell, includeOnlyLegal: false))
                return displayCells;

            if (FirstSelectionRequiresSelf())
            {
                displayCells.Add(ownerCell);
                return displayCells;
            }

            if (FirstSelectionRequiresMoveDestination())
            {
                _owner.CachePaths(_gridController.CellManager);
                var destinations = _owner.GetAvailableDestinations(_gridController.CellManager.GetCells());
                foreach (var cell in destinations)
                    displayCells.Add(cell);
                return displayCells;
            }

            if (FirstSelectionRequiresTeleport())
            {
                foreach (var cell in allCells)
                {
                    int distance = cell.GetDistance(ownerCell);
                    if (distance > 0 && distance <= _config.TargetRange && !cell.IsTaken)
                        displayCells.Add(cell);
                }
                return displayCells;
            }

            if (FirstSelectionRequiresCorpse())
            {
                foreach (var cell in allCells)
                {
                    if (HasCorpseInteractable(cell))
                        displayCells.Add(cell);
                }
                return displayCells;
            }

            if (FirstSelectionRequiresAlly())
            {
                int allyRange = GetAllyRangeFromGraph();
                bool includeSelf = FirstAllySelectionIncludesSelf();
                foreach (var cell in allCells)
                {
                    int dist = cell.GetDistance(ownerCell);
                    if ((includeSelf || dist > 0) && dist <= allyRange && HasFriendlyUnit(cell, includeSelf))
                        displayCells.Add(cell);
                }
                return displayCells;
            }

            foreach (var cell in allCells)
            {
                int distance = cell.GetDistance(ownerCell);
                if (distance < minRange || distance > maxRange)
                    continue;

                if (cardinalOnly)
                {
                    int dx = cell.GridCoordinates.x - ownerCell.GridCoordinates.x;
                    int dy = cell.GridCoordinates.y - ownerCell.GridCoordinates.y;
                    bool isCardinal = (dx == 0) ^ (dy == 0);
                    if (!isCardinal)
                        continue;
                }

                displayCells.Add(cell);
            }

            return displayCells;
        }

        private int GetMinRangeFromGraph()
        {
            var first = FindFirstSelectionNode();
            if (first is SelectPrimaryTargetNodeRecord select)
                return select.MinRange;
            return 0;
        }

        private HashSet<ICell> CalculateValidTargetCells(ICell originCell = null)
        {
            var validCells = new HashSet<ICell>();
            if (_gridController == null) return validCells;

            int range = GetTargetRange();
            int minRange = GetMinRangeFromGraph();
            var allCells = _gridController.CellManager.GetCells();
            var ownerCell = originCell ?? _owner.CurrentCell;
            if (ownerCell == null) return validCells;
            bool cardinalOnly = UsesCardinalDash() || IsThrust() || RequiresStraightLineEndpoint();
            // Thrust selects a direction endpoint rather than a unit. An empty endpoint must remain
            // legal because enemies can be hit on any earlier cell along the scanned ray.
            bool requiresEnemy = FirstSelectionRequiresEnemy() && !IsThrust();
            bool requiresSelf = FirstSelectionRequiresSelf();

            if (TryAddAmazonSpecialTargets(validCells, ownerCell, includeOnlyLegal: true))
                return validCells;

            if (requiresSelf)
            {
                validCells.Add(ownerCell);
                return validCells;
            }

            if (FirstSelectionRequiresMoveDestination())
            {
                _owner.CachePaths(_gridController.CellManager);
                var destinations = _owner.GetAvailableDestinations(_gridController.CellManager.GetCells());
                foreach (var cell in destinations)
                    validCells.Add(cell);
                return validCells;
            }

            if (FirstSelectionRequiresTeleport())
            {
                foreach (var cell in allCells)
                {
                    int distance = cell.GetDistance(ownerCell);
                    if (distance > 0 && distance <= range && !cell.IsTaken)
                        validCells.Add(cell);
                }
                return validCells;
            }

            if (FirstSelectionRequiresCorpse())
            {
                foreach (var cell in allCells)
                {
                    if (HasCorpseInteractable(cell))
                        validCells.Add(cell);
                }
                return validCells;
            }

            if (FirstSelectionRequiresAlly())
            {
                int allyRange = GetAllyRangeFromGraph();
                bool includeSelf = FirstAllySelectionIncludesSelf();
                foreach (var cell in allCells)
                {
                    int dist = cell.GetDistance(ownerCell);
                    if ((includeSelf || dist > 0) && dist <= allyRange && HasFriendlyUnit(cell, includeSelf))
                        validCells.Add(cell);
                }
                return validCells;
            }

            foreach (var cell in allCells)
            {
                int distance = cell.GetDistance(ownerCell);
                if (distance < minRange || distance > range)
                    continue;

                if (cardinalOnly)
                {
                    int dx = cell.GridCoordinates.x - ownerCell.GridCoordinates.x;
                    int dy = cell.GridCoordinates.y - ownerCell.GridCoordinates.y;
                    bool isCardinal = (dx == 0) ^ (dy == 0);
                    if (!isCardinal)
                        continue;
                }

                if (requiresEnemy && !HasEnemyUnit(cell))
                    continue;

                if (IsPoisonSpear() && AmazonBattleState.For(_gridController).FindDropCell(_owner, cell, 3) == null)
                    continue;

                if (IsThrust() && !IsThrustTargetLegal(ownerCell, cell))
                    continue;

                if (TargetMode == SkillTargetMode.OrderedMultiTarget &&
                    (!IsWithinOrderedCone(ownerCell, cell) || HasPermanentTerrainOnRay(ownerCell, cell)))
                    continue;

                // Thrust resolves every cell on its authored ray. An enemy on an earlier cell is a
                // valid hit, not an obstruction that should hide the farther directional endpoint.
                if (!IsThrust() && RequiresLineOfSight() && !HasLineOfSight(ownerCell, cell))
                    continue;

                if (distance <= range)
                {
                    validCells.Add(cell);
                }
            }

            return validCells;
        }

        private int GetTargetRange()
        {
            var first = FindFirstSelectionNode();
            return first switch
            {
                SelectPrimaryTargetNodeRecord select => select.MaxRange,
                SelectTargetPointNodeRecord select => select.MaxRange,
                SelectCorpseTargetNodeRecord select => select.MaxRange,
                TeleportNodeRecord teleport => teleport.MaxRange,
                _ => _config.TargetRange
            };
        }

        private bool RequiresLineOfSight()
        {
            if (_config?.SkillGraph == null)
                return false;

            foreach (var node in _config.SkillGraph.Nodes)
            {
                if (node is TeleportNodeRecord teleport)
                    return teleport.RequiresLineOfSight;
                if (node is ProjectileLaunchNodeRecord projectile)
                    return projectile.RequiresLineOfSight;
                if (node is ApplyDamageNodeRecord damage && damage.IsRanged)
                    return true;
            }

            return false;
        }

        private bool RequiresStraightLineEndpoint()
        {
            return _config?.SkillGraph?.Nodes?
                .OfType<NecromancerSkillNodeRecord>()
                .Any(node => node.SkillKind == NecromancerSkillKind.BoneSpear) == true;
        }

        private bool HasLineOfSight(ICell origin, ICell target)
        {
            if (origin == null || target == null || _gridController?.CellManager == null)
                return false;
            if (origin.Equals(target))
                return true;

            int x = origin.GridCoordinates.x;
            int y = origin.GridCoordinates.y;
            int targetX = target.GridCoordinates.x;
            int targetY = target.GridCoordinates.y;
            int dx = targetX - x;
            int dy = targetY - y;
            int nx = Math.Abs(dx);
            int ny = Math.Abs(dy);
            int signX = dx == 0 ? 0 : dx > 0 ? 1 : -1;
            int signY = dy == 0 ? 0 : dy > 0 ? 1 : -1;
            int ix = 0;
            int iy = 0;

            // Supercover visits both cells when a ray crosses an exact grid corner. This keeps
            // player preview, AI planning, and execution from disagreeing on diagonal cover.
            while (ix < nx || iy < ny)
            {
                long horizontal = (1L + 2L * ix) * ny;
                long vertical = (1L + 2L * iy) * nx;
                if (horizontal == vertical)
                {
                    x += signX;
                    y += signY;
                    ix++;
                    iy++;
                }
                else if (horizontal < vertical)
                {
                    x += signX;
                    ix++;
                }
                else
                {
                    y += signY;
                    iy++;
                }

                if (x == targetX && y == targetY) break;
                var cell = _gridController.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(x, y));
                if (cell == null || IsLineBlocker(cell))
                    return false;
            }

            return true;
        }

        private static bool IsLineBlocker(ICell cell)
        {
            if (cell.CurrentUnits.Any(unit => unit != null && !unit.IsDowned))
                return true;

            if (cell.CurrentInteractables.Any(interactable => interactable is DroppedSpear))
                return false;

            // A taken cell without a live unit represents layout terrain or an occupying
            // interactable. Downed units deliberately do not provide line-of-sight cover.
            return cell.IsTaken && cell.CurrentUnits.Count == 0;
        }

        private CollectTargetsInAreaNodeRecord FindAreaCollectionNode()
        {
            return _config?.SkillGraph?.Nodes?.OfType<CollectTargetsInAreaNodeRecord>().FirstOrDefault();
        }

        private static List<IUnit> GetLiveUnitsAt(ICell cell, IEnumerable<IUnit> candidates)
        {
            return candidates.Where(unit => !unit.IsDowned && Equals(unit.CurrentCell, cell)).ToList();
        }

        private List<IUnit> CollectAreaTargets(
            ICell center,
            CollectTargetsInAreaNodeRecord area,
            IEnumerable<IUnit> candidates)
        {
            var targets = new List<IUnit>();
            foreach (var unit in candidates)
            {
                if (unit.IsDowned || unit.CurrentCell == null || unit.CurrentCell.GetDistance(center) > area.Radius)
                    continue;

                int dx = unit.CurrentCell.GridCoordinates.x - center.GridCoordinates.x;
                int dy = unit.CurrentCell.GridCoordinates.y - center.GridCoordinates.y;
                if (area.Shape == SkillGraphAreaShape.Cross && dx != 0 && dy != 0)
                    continue;

                bool samePlayer = unit.PlayerNumber == _owner.PlayerNumber;
                if (area.TargetFaction == SkillGraphTargetFaction.Enemies && samePlayer)
                    continue;
                if (area.TargetFaction == SkillGraphTargetFaction.Allies && !samePlayer)
                    continue;

                targets.Add(unit);
            }
            return targets;
        }

        private bool HasEnemyUnit(ICell cell)
        {
            if (_gridController == null || cell == null) return false;
            foreach (var unit in cell.CurrentUnits)
            {
                if (unit != null && unit.PlayerNumber != _owner.PlayerNumber)
                    return true;
            }
            return false;
        }

        public SkillGraphNodeRecord FindFirstSelectionNode()
        {
            if (_config?.SkillGraph == null) return null;
            var nodes = _config.SkillGraph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node is StartNodeRecord) continue;
                return node;
            }
            return null;
        }

        private bool FirstSelectionRequiresEnemy()
        {
            var first = FindFirstSelectionNode();
            if (first == null) return false;

            if (first is SelectPrimaryTargetNodeRecord)
            {
                return !GraphContainsDashToTarget();
            }

            return false;
        }

        private bool FirstSelectionRequiresSelf()
        {
            var first = FindFirstSelectionNode();
            return first is SelectSelfNodeRecord;
        }

        private bool FirstSelectionRequiresAlly()
        {
            var first = FindFirstSelectionNode();
            return first is SelectAllyNodeRecord;
        }

        private bool FirstSelectionRequiresMoveDestination()
        {
            var first = FindFirstSelectionNode();
            return first is SelectMoveDestinationNodeRecord;
        }

        private bool FirstSelectionRequiresTeleport()
        {
            var first = FindFirstSelectionNode();
            return first is TeleportNodeRecord;
        }

        private bool FirstSelectionRequiresCorpse()
        {
            var first = FindFirstSelectionNode();
            return first is SelectCorpseTargetNodeRecord;
        }

        private bool HasCorpseInteractable(ICell cell)
        {
            if (cell == null) return false;
            foreach (var interactable in cell.CurrentInteractables)
            {
                if (interactable is Corpse corpse && !corpse.IsDestroyed)
                    return true;
            }
            return false;
        }

        private int GetAllyRangeFromGraph()
        {
            var first = FindFirstSelectionNode();
            if (first is SelectAllyNodeRecord select)
                return select.MaxRange;
            return 1;
        }

        private bool FirstAllySelectionIncludesSelf()
        {
            return FindFirstSelectionNode() is SelectAllyNodeRecord select && select.IncludeSelf;
        }

        private bool HasFriendlyUnit(ICell cell, bool includeSelf = false)
        {
            if (_gridController == null || cell == null) return false;
            foreach (var unit in cell.CurrentUnits)
            {
                if (unit != null && unit.PlayerNumber == _owner.PlayerNumber &&
                    !unit.IsDowned && unit.Health > 0f && !AmazonBattleState.IsDecoy(unit) &&
                    (includeSelf || !ReferenceEquals(unit, _owner)))
                    return true;
            }
            return false;
        }

        private AbilityAvailability GetAmazonSpearAvailability(IGridController gridController)
        {
            if (!IsAmazonOwner())
                return AbilityAvailability.Enabled();
            var state = AmazonBattleState.For(gridController);
            var node = GetAmazonNode();
            bool requiresHeldSpear = IsAmazonSpearBasicAttack() || node?.SkillKind is
                AmazonSkillKind.Thrust or AmazonSkillKind.MultiStab or AmazonSkillKind.PoisonSpear;
            if (requiresHeldSpear && !state.IsSpearHeld(_owner))
                return AbilityAvailability.Disabled("需要先回收长矛");

            if (node?.SkillKind is AmazonSkillKind.RecoverSpear or AmazonSkillKind.PickupSpear)
            {
                var spearCell = state.GetSpearCell(_owner);
                if (spearCell == null)
                    return AbilityAvailability.Disabled("当前没有需要回收的长矛");
                if (node.SkillKind == AmazonSkillKind.PickupSpear)
                {
                    int dx = Math.Abs(spearCell.GridCoordinates.x - _owner.CurrentCell.GridCoordinates.x);
                    int dy = Math.Abs(spearCell.GridCoordinates.y - _owner.CurrentCell.GridCoordinates.y);
                    if (Math.Max(dx, dy) != 1)
                        return AbilityAvailability.Disabled("需要移动到长矛相邻格");
                }
            }
            return AbilityAvailability.Enabled();
        }

        private bool TryAddAmazonSpecialTargets(HashSet<ICell> result, ICell ownerCell, bool includeOnlyLegal)
        {
            var node = GetAmazonNode();
            if (node == null)
                return false;
            var state = AmazonBattleState.For(_gridController);
            if (node.SkillKind == AmazonSkillKind.RecoverSpear)
            {
                var cell = state.GetSpearCell(_owner);
                if (cell != null && cell.GetDistance(ownerCell) <= GetTargetRange())
                    result.Add(cell);
                return true;
            }
            if (node.SkillKind == AmazonSkillKind.PickupSpear)
            {
                if (!includeOnlyLegal || GetAmazonSpearAvailability(_gridController).CanExecute)
                    result.Add(ownerCell);
                return true;
            }
            if (node.SkillKind != AmazonSkillKind.Decoy)
                return false;

            var allCells = _gridController.CellManager.GetCells().Where(cell => cell != null && !cell.IsTaken).ToList();
            var firstRing = allCells.Where(cell => GetChebyshevDistance(ownerCell, cell) == 1).ToList();
            var chosen = firstRing.Count > 0
                ? firstRing
                : allCells.Where(cell => GetChebyshevDistance(ownerCell, cell) == 2).ToList();
            foreach (var cell in chosen)
                result.Add(cell);
            return true;
        }

        private bool IsThrustTargetLegal(ICell origin, ICell target)
        {
            int dx = target.GridCoordinates.x - origin.GridCoordinates.x;
            int dy = target.GridCoordinates.y - origin.GridCoordinates.y;
            if (dx != 0 && dy != 0)
                return false;
            int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
            int stepX = Math.Sign(dx);
            int stepY = Math.Sign(dy);
            for (int index = 1; index <= distance; index++)
            {
                var cell = _gridController.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(
                    origin.GridCoordinates.x + stepX * index,
                    origin.GridCoordinates.y + stepY * index));
                if (cell == null || cell.IsTaken && cell.CurrentUnits.Count == 0)
                    return false;
                if (index < distance && cell.CurrentUnits.Any(unit =>
                        unit != null && !unit.IsDowned && unit.PlayerNumber == _owner.PlayerNumber))
                    return false;
            }
            return true;
        }

        private bool IsWithinOrderedCone(ICell origin, ICell target)
        {
            int dx = target.GridCoordinates.x - origin.GridCoordinates.x;
            int dy = target.GridCoordinates.y - origin.GridCoordinates.y;
            int forward;
            int lateral;
            switch (_lockedSelectionFacing)
            {
                case FacingDirection.North: forward = dy; lateral = Math.Abs(dx); break;
                case FacingDirection.South: forward = -dy; lateral = Math.Abs(dx); break;
                case FacingDirection.West: forward = -dx; lateral = Math.Abs(dy); break;
                default: forward = dx; lateral = Math.Abs(dy); break;
            }
            return forward is >= 1 and <= 3 && lateral <= forward - 1;
        }

        private bool HasPermanentTerrainOnRay(ICell origin, ICell target)
        {
            int dx = target.GridCoordinates.x - origin.GridCoordinates.x;
            int dy = target.GridCoordinates.y - origin.GridCoordinates.y;
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            for (int index = 1; index < steps; index++)
            {
                int x = origin.GridCoordinates.x + Mathf.RoundToInt(dx * (index / (float)steps));
                int y = origin.GridCoordinates.y + Mathf.RoundToInt(dy * (index / (float)steps));
                var cell = _gridController.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(x, y));
                if (cell == null || cell.IsTaken && cell.CurrentUnits.Count == 0 && cell.CurrentInteractables.Count == 0)
                    return true;
            }
            return false;
        }

        private AmazonSkillNodeRecord GetAmazonNode() =>
            _config?.SkillGraph?.Nodes?.OfType<AmazonSkillNodeRecord>().FirstOrDefault();

        private bool IsPoisonSpear() => GetAmazonNode()?.SkillKind == AmazonSkillKind.PoisonSpear;
        private bool IsThrust() => GetAmazonNode()?.SkillKind == AmazonSkillKind.Thrust;

        private void PreparePoisonSpearReleaseVisual()
        {
            // Release ordering is visual state, pose clear, then graph execution. Gameplay state
            // remains authoritative and is reconciled after every graph outcome.
            UnitAnimationCoordinator.SetVisualState(_owner, UnitVisualState.Unarmed);
            UnitAnimationCoordinator.ClearPose(_owner);
        }

        private async Task<SkillGraphExecutionState> ExecuteReleasedGraphAsync(
            SkillGraphRunner runner,
            SkillExecutionContext context)
        {
            try
            {
                return await runner.Execute(context);
            }
            finally
            {
                if (IsPoisonSpear())
                    ReconcileAmazonSpearVisual(context.GridController);
            }
        }

        private void ReconcileAmazonSpearVisual(IGridController gridController)
        {
            var state = AmazonBattleState.For(gridController);
            if (state == null)
                return;

            UnitAnimationCoordinator.SetVisualState(
                _owner,
                state.IsSpearHeld(_owner) ? UnitVisualState.Default : UnitVisualState.Unarmed);
        }

        private bool IsAmazonOwner() => _owner is Unit unit &&
            (unit.GetLearnedSkillLevel("amazon.thrust") > 0 ||
             unit.GetLearnedSkillLevel("amazon.poison_spear") > 0 ||
             unit.GetLearnedSkillLevel("amazon.combat_techniques") > 0 ||
             unit.GetLearnedSkillLevel("amazon.multi_stab") > 0 ||
             unit.GetLearnedSkillLevel("amazon.recover_spear") > 0 ||
             unit.GetLearnedSkillLevel("amazon.decoy") > 0);

        private bool IsAmazonSpearBasicAttack()
        {
            // Basic movement shares the once-per-turn flag and range with melee attacks.
            // Only direct-damage graphs consume the Amazon's held spear.
            return _config.IsBasicAbility &&
                   _config.TargetRange <= 1 &&
                   !FirstSelectionRequiresMoveDestination() &&
                   _config.SkillGraph.Nodes.Any(node => node is ApplyDamageNodeRecord);
        }

        private void PrepareAmazonSpearMovementGuidance()
        {
            _spearLocationGuidanceCells = null;
            _spearPickupGuidanceCells = null;

            if (_gridController?.CellManager == null ||
                !_config.IsBasicAbility ||
                !FirstSelectionRequiresMoveDestination() ||
                !IsAmazonOwner())
            {
                return;
            }

            var state = AmazonBattleState.For(_gridController);
            var spearCell = state?.GetSpearCell(_owner);
            if (spearCell == null || state.IsSpearHeld(_owner))
                return;

            _spearLocationGuidanceCells = new HashSet<ICell> { spearCell };
            _spearPickupGuidanceCells = _gridController.CellManager.GetCells()
                .Where(cell => IsAvailableSpearPickupPosition(cell, spearCell))
                .ToHashSet();

            // Pickup guidance includes standable adjacent cells beyond the current
            // movement range, but never changes movement legality or cached paths.
            // It replaces cyan only where both guidance and reachability overlap.
            _displayCells?.ExceptWith(_spearPickupGuidanceCells);
        }

        private bool IsAvailableSpearPickupPosition(ICell cell, ICell spearCell)
        {
            if (cell == null || spearCell == null)
                return false;

            int dx = Math.Abs(cell.GridCoordinates.x - spearCell.GridCoordinates.x);
            int dy = Math.Abs(cell.GridCoordinates.y - spearCell.GridCoordinates.y);
            if (Math.Max(dx, dy) != 1)
                return false;

            if (ReferenceEquals(cell, _owner.CurrentCell))
                return true;

            return !cell.IsTaken && _gridController.CellManager.IsCellWalkable(cell);
        }

        private int GetOrderedSelectionCount() => GetAmazonNode()?.Level >= 2 ? 4 : 3;

        private static int GetChebyshevDistance(ICell left, ICell right) => Math.Max(
            Math.Abs(left.GridCoordinates.x - right.GridCoordinates.x),
            Math.Abs(left.GridCoordinates.y - right.GridCoordinates.y));

        private bool GraphContainsDashToTarget()
        {
            if (_config?.SkillGraph == null) return false;
            var nodes = _config.SkillGraph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is DashToTargetNodeRecord)
                    return true;
            }
            return false;
        }

        private bool UsesCardinalDash()
        {
            if (_config?.SkillGraph == null)
                return false;

            var nodes = _config.SkillGraph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is DashToTargetNodeRecord || nodes[i] is DashToAllyNodeRecord)
                    return true;
            }

            return false;
        }
    }
}
