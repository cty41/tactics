using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tactics.Common.Testing.Gameplay
{
    public sealed class GameplayRuntimeRunner
    {
        private readonly Dictionary<string, IGameplayStepAdapter> _adapters;

        public GameplayRuntimeRunner()
            : this(new IGameplayStepAdapter[] { new SkillGameplayStepAdapter() })
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
                // 等待 ExecuteCoreAsync 完成（包括 Dispose），避免 context 泄漏
                try { await executionTask; } catch { /* 忽略，超时结果已构建 */ }
                return BuildTimeoutResult(plan);
            }

            return await executionTask;
        }

        private async Task<GameplayTestResult> ExecuteCoreAsync(ExecutableScenarioPlan plan)
        {
            var result = new GameplayTestResult { ScenarioName = plan.ScenarioName };

            using var context = new GameplayRuntimeContext();
            foreach (var action in plan.SetupActions.Concat(plan.RuntimeActions))
            {
                var adapter = ResolveAdapter(action.Adapter);
                if (adapter == null || !adapter.CanExecute(action))
                {
                    var failure = GameplayStepResult.Fail(action.Adapter, action.Kind, $"No adapter can execute action '{action.Kind}'.");
                    result.ExecutedSteps.Add(failure);
                    result.Diagnostics.Add(failure.Message);
                    return result;
                }

                var stepResult = await adapter.ExecuteAsync(context, action);
                result.ExecutedSteps.Add(stepResult);
                context.LastStepMessage = stepResult.Message;
                if (!stepResult.Passed)
                {
                    result.Diagnostics.Add(stepResult.Message);
                    return result;
                }
            }

            foreach (var assertion in plan.AssertionPlans)
            {
                var adapter = ResolveAdapter(assertion.Adapter);
                if (adapter == null || !adapter.CanAssert(assertion))
                {
                    var failure = GameplayAssertionResult.Fail(assertion.Adapter, assertion.Kind, $"No adapter can assert '{assertion.Kind}'.");
                    failure.Target = assertion.Target;
                    result.Assertions.Add(failure);
                    result.Diagnostics.Add(failure.Message);
                    continue;
                }

                var assertionResult = await adapter.AssertAsync(context, assertion);
                assertionResult.Target = assertion.Target;
                result.Assertions.Add(assertionResult);
                if (!assertionResult.Passed)
                {
                    result.Diagnostics.Add(assertionResult.Message);
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

        private static GameplayTestResult BuildTimeoutResult(ExecutableScenarioPlan plan)
        {
            var result = new GameplayTestResult { ScenarioName = plan.ScenarioName };
            var message = $"Scenario '{plan.ScenarioName}' timed out after {plan.TimeoutMs} ms.";
            result.ExecutedSteps.Add(GameplayStepResult.Fail("Runner", "timeout", message));
            result.Diagnostics.Add(message);
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
