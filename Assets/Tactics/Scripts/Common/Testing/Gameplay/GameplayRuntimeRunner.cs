using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Battle.Runtime;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.Testing.Gameplay
{
    public sealed class GameplayRuntimeRunner
    {
        private const int CancellationDrainTimeoutMs = 1000;

        private readonly Dictionary<string, IGameplayStepAdapter> _adapters;
        private readonly GamePlaybackSpeed _executionSpeed;

        public GameplayRuntimeRunner()
            : this(CreateDefaultAdapters(), GamePlaybackSpeed.Quadruple)
        {
        }

        public GameplayRuntimeRunner(IEnumerable<IGameplayStepAdapter> adapters)
            : this(adapters, GamePlaybackSpeed.Quadruple)
        {
        }

        public GameplayRuntimeRunner(GamePlaybackSpeed executionSpeed)
            : this(CreateDefaultAdapters(), executionSpeed)
        {
        }

        public GameplayRuntimeRunner(IEnumerable<IGameplayStepAdapter> adapters, GamePlaybackSpeed executionSpeed)
        {
            _adapters = (adapters ?? throw new ArgumentNullException(nameof(adapters)))
                .ToDictionary(adapter => adapter.AdapterName, StringComparer.OrdinalIgnoreCase);
            _executionSpeed = executionSpeed;
        }

        public async Task<GameplayTestResult> ExecuteAsync(ExecutableScenarioPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (GameTimeService.IsPaused)
                throw new InvalidOperationException("Gameplay runtime execution cannot start while gameplay is paused.");

            var enteringSpeed = GameTimeService.PlaybackSpeed;
            GameTimeService.SetPlaybackSpeed(_executionSpeed);

            try
            {
                var scope = new BattleRuntimeScope();
                var context = new GameplayRuntimeContext { RuntimeScope = scope };
                bool cleanupDeferred = false;
                try
                {
                    var executionTask = ExecuteCoreAsync(plan, context, scope);

                    var completed = await Task.WhenAny(executionTask, Task.Delay(plan.TimeoutMs));
                    if (completed != executionTask)
                    {
                        // Cancel the runtime before disposing its context so adapters can leave
                        // their current PlayerLoop wait and release owned input devices safely.
                        scope.Cancel();
                        Task<Exception> cancellationDrainTask = ObserveTimedOutExecutionAsync(executionTask, scope);
                        Task drainCompleted = await Task.WhenAny(
                            cancellationDrainTask,
                            Task.Delay(CancellationDrainTimeoutMs));
                        if (drainCompleted != cancellationDrainTask)
                        {
                            // A non-cooperative adapter must not turn the plan timeout into an
                            // unbounded wait. Keep its context alive and clean it up when the
                            // adapter eventually returns.
                            cleanupDeferred = true;
                            _ = CompleteDeferredCleanupAsync(
                                cancellationDrainTask,
                                context,
                                scope,
                                plan.ScenarioName);
                            return BuildTimeoutResult(plan, cancellationDrainExceeded: true);
                        }

                        Exception cancellationDrainException = await cancellationDrainTask;
                        return BuildTimeoutResult(
                            plan,
                            cancellationDrainException: cancellationDrainException);
                    }

                    return await executionTask;
                }
                finally
                {
                    if (!cleanupDeferred)
                        await CleanupRuntimeAsync(context, scope);
                }
            }
            finally
            {
                GameTimeService.SetPlaybackSpeed(enteringSpeed);
            }
        }

        private static IEnumerable<IGameplayStepAdapter> CreateDefaultAdapters()
        {
            return new IGameplayStepAdapter[]
            {
                new SkillGameplayStepAdapter(),
                new BattleGameplayStepAdapter(),
                new MapGameplayStepAdapter(),
                new UiGameplayStepAdapter(),
                new PlayerInputGameplayStepAdapter()
            };
        }

        private async Task<GameplayTestResult> ExecuteCoreAsync(
            ExecutableScenarioPlan plan,
            GameplayRuntimeContext context,
            IBattleRuntimeScope scope)
        {
            var result = new GameplayTestResult { ScenarioName = plan.ScenarioName };

            foreach (var action in plan.SetupActions.Concat(plan.RuntimeActions))
            {
                // 检查是否已取消
                if (scope.IsCancelling)
                {
                    result.AddFailure(FailureCategory.Setup, "setup", action.Kind, action.Adapter, "Execution cancelled.");
                    return result;
                }

                var adapter = ResolveAdapter(action.Adapter);
                if (adapter == null || !adapter.CanExecute(action))
                {
                    var isSetup = IsSetupAction(action);
                    var category = isSetup ? FailureCategory.Setup : FailureCategory.Action;
                    var phase = isSetup ? "setup" : "action";
                    var failure = GameplayStepResult.Fail(action.Adapter, action.Kind, $"No adapter can execute action '{action.Kind}'.");
                    result.ExecutedSteps.Add(failure);
                    result.AddFailure(category, phase, action.Kind, action.Adapter, failure.Message);
                    return result;
                }

                var stepResult = await adapter.ExecuteAsync(context, action);
                result.ExecutedSteps.Add(stepResult);
                context.LastStepMessage = stepResult.Message;
                if (!stepResult.Passed)
                {
                    // Use the failure category from the step result if set, otherwise determine from action type
                    var category = stepResult.FailureCategory != FailureCategory.None
                        ? stepResult.FailureCategory
                        : (IsSetupAction(action) ? FailureCategory.Setup : FailureCategory.Action);
                    var phase = ResolvePhase(category, action);
                    result.AddFailure(category, phase, action.Kind, action.Adapter, stepResult.Message);
                    return result;
                }
            }

            foreach (var assertion in plan.AssertionPlans)
            {
                // 检查是否已取消
                if (scope.IsCancelling)
                {
                    result.AddFailure(FailureCategory.Assertion, "assertion", assertion.Kind, assertion.Adapter, "Execution cancelled.");
                    return result;
                }

                var adapter = ResolveAdapter(assertion.Adapter);
                if (adapter == null || !adapter.CanAssert(assertion))
                {
                    var failure = GameplayAssertionResult.Fail(assertion.Adapter, assertion.Kind, $"No adapter can assert '{assertion.Kind}'.");
                    failure.Target = assertion.Target;
                    result.Assertions.Add(failure);
                    result.AddFailure(FailureCategory.Assertion, "assertion", assertion.Kind, assertion.Adapter, failure.Message);
                    continue;
                }

                var assertionResult = await adapter.AssertAsync(context, assertion);
                assertionResult.Target = assertion.Target;
                result.Assertions.Add(assertionResult);
                if (!assertionResult.Passed)
                {
                    result.AddFailure(FailureCategory.Assertion, "assertion", assertion.Kind, assertion.Adapter, assertionResult.Message);
                }
            }

            foreach (var probeRequest in plan.ProbeRequests)
            {
                var adapter = ResolveAdapter(probeRequest.Adapter);
                var probe = adapter?.CaptureProbe(context, probeRequest);
                if (probe != null)
                    result.Probes.Add(probe);
            }

            return result;
        }

        private static bool IsSetupAction(ExecutableScenarioAction action)
        {
            return action.Kind is "createSkillTestWorld"
                or "createSkillGraph"
                or "createCell"
                or "createUnit"
                or "createSkillAbilityConfig"
                or "createSkillAbility"
                or "setTurnContext"
                or "selectAbility"
                or "bindBattleController"
                or "createAiBrain"
                or "useRealAssets"
                or "loadSkillGraphAsset"
                or "loadRoguelikeMap"
                or "loadTestPartyConfig"
                or "loadTestEncounterConfig"
                or "setBattleTestMode"
                or "setAdventureGold"
                or "setRosterCharacterState"
                or "initializePlayerInput";
        }

        private static string ResolvePhase(FailureCategory category, ExecutableScenarioAction action)
        {
            return category switch
            {
                FailureCategory.Setup => "setup",
                FailureCategory.Asset => IsSetupAction(action) ? "setup" : "action",
                FailureCategory.Validation => "validation",
                FailureCategory.Action => "action",
                FailureCategory.Assertion => "assertion",
                FailureCategory.Timeout => "timeout",
                _ => IsSetupAction(action) ? "setup" : "action"
            };
        }

        private static async Task<Exception> ObserveTimedOutExecutionAsync(
            Task executionTask,
            IBattleRuntimeScope scope)
        {
            try
            {
                await executionTask;
                return null;
            }
            catch (OperationCanceledException) when (scope.IsCancelling)
            {
                // Cancellation is the expected terminal state after a timeout.
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static async Task CompleteDeferredCleanupAsync(
            Task<Exception> cancellationDrainTask,
            GameplayRuntimeContext context,
            BattleRuntimeScope scope,
            string scenarioName)
        {
            Exception cancellationDrainException = await cancellationDrainTask;
            if (cancellationDrainException != null)
                TLog.Error($"[GameplayRuntimeRunner] Timed-out scenario '{scenarioName}' failed while draining: {cancellationDrainException}");

            try
            {
                await CleanupRuntimeAsync(context, scope);
            }
            catch (Exception ex)
            {
                TLog.Error($"[GameplayRuntimeRunner] Deferred cleanup failed for scenario '{scenarioName}': {ex}");
            }
        }

        private static async Task CleanupRuntimeAsync(
            GameplayRuntimeContext context,
            BattleRuntimeScope scope)
        {
            try
            {
                scope.Cancel();
            }
            finally
            {
                try
                {
                    await scope.WhenIdleAsync();
                }
                finally
                {
                    try
                    {
                        if (context.BattleController != null)
                            await context.BattleController.TeardownRuntimeScopeAsync();
                    }
                    finally
                    {
                        context.Dispose();
                        scope.Dispose();
                    }
                }
            }
        }

        private static GameplayTestResult BuildTimeoutResult(
            ExecutableScenarioPlan plan,
            bool cancellationDrainExceeded = false,
            Exception cancellationDrainException = null)
        {
            var result = new GameplayTestResult { ScenarioName = plan.ScenarioName };
            string drainDetail = cancellationDrainExceeded
                ? $" Cancellation did not drain within {CancellationDrainTimeoutMs} ms; cleanup was deferred."
                : string.Empty;
            if (cancellationDrainException != null)
            {
                drainDetail +=
                    $" Cancellation drain failed with {cancellationDrainException.GetType().Name}: " +
                    cancellationDrainException.Message;
            }
            var message = $"Scenario '{plan.ScenarioName}' timed out after {plan.TimeoutMs} ms.{drainDetail}";
            result.ExecutedSteps.Add(GameplayStepResult.Fail("Runner", "timeout", message));
            result.AddFailure(FailureCategory.Timeout, "timeout", "timeout", "Runner", message);
            return result;
        }

        private IGameplayStepAdapter ResolveAdapter(string adapterName)
        {
            if (string.IsNullOrWhiteSpace(adapterName))
                return null;

            _adapters.TryGetValue(adapterName, out var adapter);
            return adapter;
        }
    }
}
