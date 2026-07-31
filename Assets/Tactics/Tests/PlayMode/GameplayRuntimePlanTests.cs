using System;
using System.Collections;
using System.IO;
using System.Linq;
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
