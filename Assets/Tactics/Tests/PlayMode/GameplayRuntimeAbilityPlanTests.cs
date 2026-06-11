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
    public class GameplayRuntimeAbilityPlanTests
    {
        [UnityTest]
        public IEnumerator RuntimeRunner_ConsumesMana_OnSuccessfulAbilityUse()
        {
            var task = ExecutePlan("mana-success.plan.json");
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitManaEquals" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "stepMessageContains" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_RejectsWhenManaIsInsufficient()
        {
            var task = ExecutePlan("mana-insufficient.plan.json");
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitManaEquals" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "lastErrorContains" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_RejectsTargetsOutOfRange()
        {
            var task = ExecutePlan("out-of-range-failure.plan.json");
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitManaEquals" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "lastErrorContains" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_RejectsWhenNoValidTargetExists()
        {
            var task = ExecutePlan("no-valid-target-failure.plan.json");
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitManaEquals" && assertion.Passed), Is.True);
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "stepMessageContains" && assertion.Passed), Is.True);
        }

        private static string GetPlanPath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tests", "gameplay-specs", fileName));
        }

        private static async Task<GameplayTestResult> ExecutePlan(string fileName)
        {
            var planPath = GetPlanPath(fileName);
            Assert.IsTrue(File.Exists(planPath), $"Plan file not found: {planPath}");

            var plan = ExecutableScenarioPlanLoader.FromFile(planPath);
            var runner = new GameplayRuntimeRunner();
            return await runner.ExecuteAsync(plan);
        }

        private static IEnumerator WaitForTask<T>(Task<T> task)
        {
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                throw task.Exception ?? new System.Exception("Task faulted.");
            }
        }
    }
}
