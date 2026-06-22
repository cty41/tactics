using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Battle.Runtime;

namespace Tactics.Common.Testing.Gameplay
{
    public sealed class GameplayRuntimeRunner
    {
        private readonly Dictionary<string, IGameplayStepAdapter> _adapters;

        public GameplayRuntimeRunner()
            : this(new IGameplayStepAdapter[] { new SkillGameplayStepAdapter(), new BattleGameplayStepAdapter(), new MapGameplayStepAdapter(), new UiGameplayStepAdapter() })
        {
        }

        public GameplayRuntimeRunner(IEnumerable<IGameplayStepAdapter> adapters)
        {
            _adapters = adapters.ToDictionary(adapter => adapter.AdapterName, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<GameplayTestResult> ExecuteAsync(ExecutableScenarioPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            var executionTask = ExecuteCoreAsync(plan);
            _ = executionTask.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

            var completed = await Task.WhenAny(executionTask, Task.Delay(plan.TimeoutMs));
            if (completed != executionTask)
            {
                // 超时后不再等待原任务，直接返回超时结果
                return BuildTimeoutResult(plan);
            }

            return await executionTask;
        }

        private async Task<GameplayTestResult> ExecuteCoreAsync(ExecutableScenarioPlan plan)
        {
            var result = new GameplayTestResult { ScenarioName = plan.ScenarioName };

            // 创建 RuntimeScope 管理异步生命周期
            using var scope = new BattleRuntimeScope();
            using var context = new GameplayRuntimeContext();
            context.RuntimeScope = scope;

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
                or "setBattleTestMode";
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

        private static GameplayTestResult BuildTimeoutResult(ExecutableScenarioPlan plan)
        {
            var result = new GameplayTestResult { ScenarioName = plan.ScenarioName };
            var message = $"Scenario '{plan.ScenarioName}' timed out after {plan.TimeoutMs} ms.";
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
