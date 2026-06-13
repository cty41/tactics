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
        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesSelfHealPlanFromFile()
        {
            var task = ExecutePlan(GetPlanPath("self-heal.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesSingleTargetDamagePlanFromFile()
        {
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
            var task = ExecutePlan(GetPlanPath("mage-fireball.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Count(assertion => assertion.Kind == "unitHealthEquals" && assertion.Passed), Is.GreaterThanOrEqualTo(3));
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesChargeStrikePlanFromFile()
        {
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
            var task = ExecutePlan(GetPlanPath("melee-heal.plan.json"));
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Target == "ally" && assertion.Passed), Is.True);
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
    }
}
