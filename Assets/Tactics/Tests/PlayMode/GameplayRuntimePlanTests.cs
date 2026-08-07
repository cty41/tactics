using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common.Testing.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class GameplayRuntimePlanTests
    {
        [SetUp]
        public void SetUp()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesSelfHealPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecutePlan(GetPlanPath("self-heal.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesSingleTargetDamagePlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecutePlan(GetPlanPath("single-target-damage.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_RejectsInvalidGraphPlanFromFile()
        {
            var task = ExecutePlan(GetPlanPath("invalid-graph.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "validationErrorCodeIncludes" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesCounterPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecutePlan(GetPlanPath("barbarian-counter.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Target == "caster" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Target == "target" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHasBuff" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMarkPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecutePlan(GetPlanPath("hunter-mark.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHasBuff" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitBuffDurationEquals" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesFireballPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecutePlan(GetPlanPath("mage-fireball.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Count(assertion => assertion.Kind == "unitHealthEquals" && assertion.Passed), Is.GreaterThanOrEqualTo(3));
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesChargeStrikePlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecutePlan(GetPlanPath("barbarian-charge-strike.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitCellEquals" && assertion.Target == "caster" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitCellEquals" && assertion.Target == "target" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Target == "target" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesChargeStrikeBlockedRetreatPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecutePlan(GetPlanPath("barbarian-charge-blocked-retreat.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitCellEquals" && assertion.Target == "caster" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitCellEquals" && assertion.Target == "target" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Target == "target" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Target == "caster" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Target == "blocker" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMeleeHealPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecutePlan(GetPlanPath("melee-heal.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Target == "ally" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesFireballAreaCountPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecutePlan(GetPlanPath("mage-fireball-area-count.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitCountInArea" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Target == "targetA" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Target == "safeTarget" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesMarkBuffUniquePlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecutePlan(GetPlanPath("hunter-mark-buff-unique.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitBuffIsUnique" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHasBuff" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesCounterMultiStagePlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecutePlan(GetPlanPath("barbarian-counter-multi-stage.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "multiStageStateEquals" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Target == "caster" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesFrostNovaPlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecutePlan(GetPlanPath("frost-nova.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Target == "targetA" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Target == "safeTarget" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesProjectilePlanFromFile()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var task = ExecutePlan(GetPlanPath("projectile.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "executionStateEquals" && assertion.Passed), Is.True);
        }

        [Test]
        public void LoaderRejectsUnsupportedSchemaVersionPlan()
        {
            AssertPlanLoadFails("bad-schema-version.plan.json", "schemaVersion");
        }

        [Test]
        public void LoaderRejectsPlanWithoutRequiredAdapters()
        {
            AssertPlanLoadFails("missing-required-adapters.plan.json", "required adapter");
        }

        [Test]
        public void LoaderRejectsPlanWithoutRuntimeActions()
        {
            AssertPlanLoadFails("missing-runtime-actions.plan.json", "runtime action");
        }

        [Test]
        public void LoaderRejectsPlanWithoutAssertions()
        {
            AssertPlanLoadFails("missing-assertion-plans.plan.json", "assertion");
        }

        [Test]
        public void LoaderRejectsPlanWithMissingActionMetadata()
        {
            AssertPlanLoadFails("missing-action-metadata.plan.json", "adapter", "kind");
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_TimesOutWhenExecutionExceedsPlanTimeout()
        {
            var plan = new ExecutableScenarioPlan
            {
                ScenarioName = "SkillGraph.TimeoutIsReported",
                TimeoutMs = 50
            };

            plan.RequiredAdapters.Add("Skill");
            plan.SetupActions.Add(new ExecutableScenarioAction
            {
                Adapter = "Skill",
                Kind = "slowAction"
            });

            var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[] { new SlowSkillStepAdapter() });
            var task = runner.ExecuteAsync(plan);
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsFalse(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Contains("timed out", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(result.ExecutedSteps.Any(step => step.Kind == "timeout"), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_TimeoutWaitsForTopLevelExecutionBeforeDisposingContext()
        {
            var adapter = new CancellationGateAdapter();
            var plan = new ExecutableScenarioPlan
            {
                ScenarioName = "RuntimeRunner.TimeoutDrainsTopLevelExecution",
                TimeoutMs = 50
            };
            plan.RuntimeActions.Add(new ExecutableScenarioAction
            {
                Adapter = CancellationGateAdapter.Name,
                Kind = CancellationGateAdapter.ActionKind
            });

            var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[] { adapter });
            Task<GameplayTestResult> runnerTask = runner.ExecuteAsync(plan);

            try
            {
                yield return new WaitUntil(() => adapter.CancellationObserved.IsCompleted);
                float boundedReturnDeadline = Time.realtimeSinceStartup + 2.5f;
                yield return new WaitUntil(() =>
                    runnerTask.IsCompleted || Time.realtimeSinceStartup >= boundedReturnDeadline);

                Assert.That(runnerTask.IsCompleted, Is.True,
                    "A non-cooperative adapter must not turn TimeoutMs into an unbounded runner wait.");
                Assert.That(runnerTask.Result.Passed, Is.False);
                Assert.That(
                    runnerTask.Result.ExecutedSteps.Any(step => step.Kind == "timeout"),
                    Is.True);
                Assert.That(adapter.CleanupObserved.IsCompleted, Is.False,
                    "Deferred cleanup must keep the context alive until the adapter returns.");

                adapter.ReleaseExecution();
                yield return WaitForTask(adapter.CleanupObserved);

                Assert.That(adapter.AccessedDisposedContext, Is.False);
            }
            finally
            {
                adapter.ReleaseExecution();
            }
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_TimeoutReturnsStructuredResultWhenCancellationDrainFaults()
        {
            var adapter = new CancellationFaultAdapter();
            var plan = new ExecutableScenarioPlan
            {
                ScenarioName = "RuntimeRunner.TimeoutCancellationDrainFault",
                TimeoutMs = 50
            };
            plan.RuntimeActions.Add(new ExecutableScenarioAction
            {
                Adapter = CancellationFaultAdapter.Name,
                Kind = CancellationFaultAdapter.ActionKind
            });

            var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[] { adapter });
            Task<GameplayTestResult> runnerTask = runner.ExecuteAsync(plan);
            yield return WaitForTask(runnerTask);

            Assert.That(runnerTask.IsFaulted, Is.False);
            Assert.That(runnerTask.Result.Passed, Is.False);
            Assert.That(
                runnerTask.Result.ExecutedSteps.Single(step => step.Kind == "timeout").Message,
                Does.Contain(nameof(InvalidOperationException)));
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_RestoresSpeedAfterTimeout()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Double);

            try
            {
                var plan = new ExecutableScenarioPlan
                {
                    ScenarioName = "SkillGraph.TimeoutRestoresSpeed",
                    TimeoutMs = 50
                };

                plan.RequiredAdapters.Add("Skill");
                plan.SetupActions.Add(new ExecutableScenarioAction
                {
                    Adapter = "Skill",
                    Kind = "slowAction"
                });

                var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[] { new SlowSkillStepAdapter() });
                var startedAt = Time.realtimeSinceStartup;
                var task = runner.ExecuteAsync(plan);

                Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Quadruple));
                Assert.That(Time.timeScale, Is.EqualTo(4f));

                yield return WaitForTask(task);

                var result = task.Result;
                Assert.IsFalse(result.Passed, string.Join("\n", result.Diagnostics));
                Assert.That(result.ExecutedSteps.Any(step => step.Kind == "timeout"), Is.True);
                Assert.That(Time.realtimeSinceStartup - startedAt, Is.GreaterThanOrEqualTo(0.04f));
                Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Double));
                Assert.That(Time.timeScale, Is.EqualTo(2f));
            }
            finally
            {
                GameTimeService.ForceResume();
                GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_DefaultsToQuadrupleAndRestoresEnteringSpeed()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Double);

            try
            {
                var adapter = new PlaybackSpeedObservingAdapter();
                var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[] { adapter });
                var task = runner.ExecuteAsync(CreateSpeedObservationPlan());
                yield return WaitForTask(task);

                Assert.That(adapter.ObservedPlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Quadruple));
                Assert.That(adapter.ObservedTimeScale, Is.EqualTo(4f));
                Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Double));
                Assert.That(Time.timeScale, Is.EqualTo(2f));
            }
            finally
            {
                GameTimeService.ForceResume();
                GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_CanOptOutToNormalSpeed()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Double);

            try
            {
                var adapter = new PlaybackSpeedObservingAdapter();
                var runner = new GameplayRuntimeRunner(
                    new IGameplayStepAdapter[] { adapter },
                    GamePlaybackSpeed.Normal);
                var task = runner.ExecuteAsync(CreateSpeedObservationPlan());
                yield return WaitForTask(task);

                Assert.That(adapter.ObservedPlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Normal));
                Assert.That(adapter.ObservedTimeScale, Is.EqualTo(1f));
                Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Double));
                Assert.That(Time.timeScale, Is.EqualTo(2f));
            }
            finally
            {
                GameTimeService.ForceResume();
                GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_RestoresSpeedAfterAdapterException()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Double);

            try
            {
                var adapter = new ThrowingSpeedObservingAdapter();
                var plan = new ExecutableScenarioPlan { ScenarioName = "RuntimeRunner.AdapterExceptionRestoresSpeed" };
                plan.RuntimeActions.Add(new ExecutableScenarioAction
                {
                    Adapter = ThrowingSpeedObservingAdapter.Name,
                    Kind = ThrowingSpeedObservingAdapter.ActionKind
                });

                var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[] { adapter });
                var task = runner.ExecuteAsync(plan);
                yield return WaitForTask(Task.WhenAny(task));

                Assert.That(task.IsFaulted, Is.True);
                var exception = task.Exception?.GetBaseException();
                Assert.That(exception, Is.TypeOf<InvalidOperationException>());
                Assert.That(exception.Message, Is.EqualTo("adapter boom"));
                Assert.That(adapter.ObservedPlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Quadruple));
                Assert.That(adapter.ObservedTimeScale, Is.EqualTo(4f));
                Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Double));
                Assert.That(Time.timeScale, Is.EqualTo(2f));
            }
            finally
            {
                GameTimeService.ForceResume();
                GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_DrainsTrackedCleanupBeforeReturning()
        {
            var adapter = new TrackedCleanupAdapter();
            var plan = new ExecutableScenarioPlan
            {
                ScenarioName = "RuntimeRunner.DrainsTrackedCleanup",
                TimeoutMs = 1000
            };
            plan.RuntimeActions.Add(new ExecutableScenarioAction
            {
                Adapter = TrackedCleanupAdapter.Name,
                Kind = TrackedCleanupAdapter.ActionKind
            });

            var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[] { adapter });
            Task<GameplayTestResult> task = runner.ExecuteAsync(plan);
            bool cleanupCompletedWhenRunnerReturned = false;
            _ = task.ContinueWith(
                _ => cleanupCompletedWhenRunnerReturned = adapter.CleanupCompleted,
                TaskContinuationOptions.ExecuteSynchronously);
            yield return WaitForTask(task);

            Assert.That(task.Result.Passed, Is.True, string.Join("\n", task.Result.Diagnostics));
            Assert.That(cleanupCompletedWhenRunnerReturned, Is.True,
                "ExecuteAsync must cancel and drain tracked cleanup before returning to the next fixture.");
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_FailsFastWhenEnteringPaused()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Double);
            GameTimeService.Pause();

            try
            {
                var adapter = new PlaybackSpeedObservingAdapter();
                var runner = new GameplayRuntimeRunner(new IGameplayStepAdapter[] { adapter });
                var task = runner.ExecuteAsync(CreateSpeedObservationPlan());
                yield return WaitForTask(task.ContinueWith(_ => true));

                Assert.That(task.IsFaulted, Is.True);
                var exception = task.Exception?.GetBaseException();
                Assert.That(exception, Is.TypeOf<InvalidOperationException>());
                Assert.That(exception.Message.IndexOf("paused", StringComparison.OrdinalIgnoreCase), Is.GreaterThanOrEqualTo(0));
                Assert.That(adapter.WasExecuted, Is.False);
                Assert.That(GameTimeService.IsPaused, Is.True);
                Assert.That(GameTimeService.PlaybackSpeed, Is.EqualTo(GamePlaybackSpeed.Double));
                Assert.That(Time.timeScale, Is.EqualTo(0f));
            }
            finally
            {
                GameTimeService.ForceResume();
                GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            }
        }

        private static ExecutableScenarioPlan CreateSpeedObservationPlan()
        {
            var plan = new ExecutableScenarioPlan { ScenarioName = "RuntimeRunner.DefaultSpeed" };
            plan.RuntimeActions.Add(new ExecutableScenarioAction
            {
                Adapter = PlaybackSpeedObservingAdapter.Name,
                Kind = PlaybackSpeedObservingAdapter.ActionKind
            });
            return plan;
        }

        private static string GetPlanPath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "gameplay-specs", fileName));
        }

        private static async Task<GameplayTestResult> ExecutePlan(string planPath)
        {
            Assert.IsTrue(File.Exists(planPath), $"Plan file not found: {planPath}");

            var plan = ExecutableScenarioPlanLoader.FromFile(planPath);
            var runner = new GameplayRuntimeRunner();
            return await runner.ExecuteAsync(plan);
        }

        private static void AssertPlanLoadFails(string fileName, params string[] expectedMessageFragments)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ExecutableScenarioPlanLoader.FromFile(GetPlanPath(fileName)));
            Assert.IsNotNull(ex);

            foreach (var expectedFragment in expectedMessageFragments)
            {
                Assert.That(ex.Message, Does.Contain(expectedFragment));
            }
        }

        private static IEnumerator WaitForTask<T>(Task<T> task)
        {
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                throw task.Exception ?? new System.Exception("Task faulted.");
            }
        }

        private sealed class SlowSkillStepAdapter : IGameplayStepAdapter
        {
            public string AdapterName => "Skill";

            public bool CanExecute(ExecutableScenarioAction action)
            {
                return string.Equals(action.Kind, "slowAction", StringComparison.OrdinalIgnoreCase);
            }

            public async Task<GameplayStepResult> ExecuteAsync(GameplayRuntimeContext context, ExecutableScenarioAction action)
            {
                await Task.Delay(200);
                return GameplayStepResult.Pass("Skill", action.Kind, "slow action completed");
            }

            public bool CanAssert(ExecutableScenarioAssertion assertion)
            {
                return false;
            }

            public Task<GameplayAssertionResult> AssertAsync(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
            {
                return Task.FromResult(GameplayAssertionResult.Fail("Skill", assertion.Kind, "Assertions are not supported in the timeout adapter."));
            }

            public ProbeSnapshot CaptureProbe(GameplayRuntimeContext context, GameplayProbeRequest request)
            {
                return null;
            }
        }

        private sealed class CancellationGateAdapter : IGameplayStepAdapter
        {
            public const string Name = "CancellationGate";
            public const string ActionKind = "waitPastTimeout";
            private const string ContextMarker = "cancellation-gate-marker";

            private readonly TaskCompletionSource<bool> _cancellationObserved = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _releaseExecution = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _cleanupObserved = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public string AdapterName => Name;
            public Task CancellationObserved => _cancellationObserved.Task;
            public Task<bool> CleanupObserved => _cleanupObserved.Task;
            public bool AccessedDisposedContext { get; private set; }

            public bool CanExecute(ExecutableScenarioAction action)
            {
                return string.Equals(action.Kind, ActionKind, StringComparison.OrdinalIgnoreCase);
            }

            public async Task<GameplayStepResult> ExecuteAsync(
                GameplayRuntimeContext context,
                ExecutableScenarioAction action)
            {
                context.Cells[ContextMarker] = null;
                context.OwnedCleanupActions.Add(() => _cleanupObserved.TrySetResult(true));
                using var registration = context.RuntimeScope.Token.Register(
                    () => _cancellationObserved.TrySetResult(true));
                await _releaseExecution.Task;
                AccessedDisposedContext = !context.Cells.ContainsKey(ContextMarker);
                return GameplayStepResult.Pass(Name, action.Kind);
            }

            public bool CanAssert(ExecutableScenarioAssertion assertion)
            {
                return false;
            }

            public Task<GameplayAssertionResult> AssertAsync(
                GameplayRuntimeContext context,
                ExecutableScenarioAssertion assertion)
            {
                return Task.FromResult(GameplayAssertionResult.Fail(
                    Name,
                    assertion.Kind,
                    "Assertions are not supported by the cancellation gate adapter."));
            }

            public ProbeSnapshot CaptureProbe(GameplayRuntimeContext context, GameplayProbeRequest request)
            {
                return null;
            }

            public void ReleaseExecution()
            {
                _releaseExecution.TrySetResult(true);
            }
        }

        private sealed class CancellationFaultAdapter : IGameplayStepAdapter
        {
            public const string Name = "CancellationFault";
            public const string ActionKind = "faultAfterCancellation";

            public string AdapterName => Name;

            public bool CanExecute(ExecutableScenarioAction action)
            {
                return action.Kind == ActionKind;
            }

            public async Task<GameplayStepResult> ExecuteAsync(
                GameplayRuntimeContext context,
                ExecutableScenarioAction action)
            {
                var cancellationObserved = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = context.RuntimeScope.Token.Register(
                    () => cancellationObserved.TrySetResult(true));
                await cancellationObserved.Task;
                throw new InvalidOperationException("Synthetic cancellation drain failure.");
            }

            public bool CanAssert(ExecutableScenarioAssertion assertion) => false;

            public Task<GameplayAssertionResult> AssertAsync(
                GameplayRuntimeContext context,
                ExecutableScenarioAssertion assertion)
            {
                return Task.FromResult<GameplayAssertionResult>(null);
            }

            public ProbeSnapshot CaptureProbe(GameplayRuntimeContext context, GameplayProbeRequest request)
            {
                return null;
            }
        }

        private sealed class TrackedCleanupAdapter : IGameplayStepAdapter
        {
            public const string Name = "TrackedCleanup";
            public const string ActionKind = "trackCleanup";

            public string AdapterName => Name;
            public bool CleanupCompleted { get; private set; }

            public bool CanExecute(ExecutableScenarioAction action)
            {
                return string.Equals(action.Kind, ActionKind, StringComparison.OrdinalIgnoreCase);
            }

            public Task<GameplayStepResult> ExecuteAsync(
                GameplayRuntimeContext context,
                ExecutableScenarioAction action)
            {
                context.RuntimeScope.Track(CompleteAfterCancellationAsync(context.RuntimeScope.Token));
                return Task.FromResult(GameplayStepResult.Pass(Name, action.Kind));
            }

            public bool CanAssert(ExecutableScenarioAssertion assertion)
            {
                return false;
            }

            public Task<GameplayAssertionResult> AssertAsync(
                GameplayRuntimeContext context,
                ExecutableScenarioAssertion assertion)
            {
                return Task.FromResult(GameplayAssertionResult.Fail(
                    Name,
                    assertion.Kind,
                    "Assertions are not supported by the tracked cleanup adapter."));
            }

            public ProbeSnapshot CaptureProbe(GameplayRuntimeContext context, GameplayProbeRequest request)
            {
                return null;
            }

            private async Task CompleteAfterCancellationAsync(CancellationToken cancellationToken)
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // The cleanup continuation intentionally crosses the cancellation callback boundary.
                }

                await Task.Delay(50);
                CleanupCompleted = true;
            }
        }

        private sealed class ThrowingSpeedObservingAdapter : IGameplayStepAdapter
        {
            public const string Name = "ThrowingSpeedObserver";
            public const string ActionKind = "throwAfterObservingSpeed";

            public string AdapterName => Name;
            public GamePlaybackSpeed ObservedPlaybackSpeed { get; private set; }
            public float ObservedTimeScale { get; private set; }

            public bool CanExecute(ExecutableScenarioAction action)
            {
                return string.Equals(action.Kind, ActionKind, StringComparison.OrdinalIgnoreCase);
            }

            public Task<GameplayStepResult> ExecuteAsync(GameplayRuntimeContext context, ExecutableScenarioAction action)
            {
                ObservedPlaybackSpeed = GameTimeService.PlaybackSpeed;
                ObservedTimeScale = Time.timeScale;
                throw new InvalidOperationException("adapter boom");
            }

            public bool CanAssert(ExecutableScenarioAssertion assertion)
            {
                return false;
            }

            public Task<GameplayAssertionResult> AssertAsync(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
            {
                return Task.FromResult(GameplayAssertionResult.Fail(Name, assertion.Kind, "Assertions are not supported by the throwing speed observer."));
            }

            public ProbeSnapshot CaptureProbe(GameplayRuntimeContext context, GameplayProbeRequest request)
            {
                return null;
            }
        }

        private sealed class PlaybackSpeedObservingAdapter : IGameplayStepAdapter
        {
            public const string Name = "SpeedObserver";
            public const string ActionKind = "observeSpeed";

            public string AdapterName => Name;
            public bool WasExecuted { get; private set; }
            public GamePlaybackSpeed ObservedPlaybackSpeed { get; private set; }
            public float ObservedTimeScale { get; private set; }

            public bool CanExecute(ExecutableScenarioAction action)
            {
                return string.Equals(action.Kind, ActionKind, StringComparison.OrdinalIgnoreCase);
            }

            public Task<GameplayStepResult> ExecuteAsync(GameplayRuntimeContext context, ExecutableScenarioAction action)
            {
                WasExecuted = true;
                ObservedPlaybackSpeed = GameTimeService.PlaybackSpeed;
                ObservedTimeScale = Time.timeScale;
                return Task.FromResult(GameplayStepResult.Pass(Name, action.Kind));
            }

            public bool CanAssert(ExecutableScenarioAssertion assertion)
            {
                return false;
            }

            public Task<GameplayAssertionResult> AssertAsync(GameplayRuntimeContext context, ExecutableScenarioAssertion assertion)
            {
                return Task.FromResult(GameplayAssertionResult.Fail(Name, assertion.Kind, "Assertions are not supported by the speed observer."));
            }

            public ProbeSnapshot CaptureProbe(GameplayRuntimeContext context, GameplayProbeRequest request)
            {
                return null;
            }
        }
    }
}
