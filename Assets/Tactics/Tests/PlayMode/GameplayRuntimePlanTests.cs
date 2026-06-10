using System.Collections;
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
        public IEnumerator RuntimeRunner_ExecutesSelfHealPlan()
        {
            var task = ExecutePlan(SelfHealPlanJson);
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesSingleTargetDamagePlan()
        {
            var task = ExecutePlan(SingleTargetDamagePlanJson);
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "unitHealthEquals" && assertion.Passed), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_RejectsInvalidGraphPlan()
        {
            var task = ExecutePlan(InvalidGraphPlanJson);
            yield return WaitForTask(task);

            var result = task.Result;
            Assert.IsTrue(result.Passed, string.Join("\n", result.Diagnostics));
            Assert.That(result.Assertions.Any(assertion => assertion.Kind == "validationErrorCodeIncludes" && assertion.Passed), Is.True);
        }

        private static async Task<GameplayTestResult> ExecutePlan(string json)
        {
            var plan = ExecutableScenarioPlanLoader.FromJson(json);
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

        private const string SelfHealPlanJson = @"
{
  ""schemaVersion"": 1,
  ""scenarioName"": ""SkillGraph.SelfHealSkillRaisesCasterHealth"",
  ""requiredAdapters"": [""Skill""],
  ""setupActions"": [
    { ""adapter"": ""Skill"", ""kind"": ""createSkillTestWorld"", ""parameters"": {} },
    { ""adapter"": ""Skill"", ""kind"": ""createSkillGraph"", ""parameters"": { ""alias"": ""graph"", ""graphKind"": ""selfHeal"", ""healAmount"": 5 } },
    { ""adapter"": ""Skill"", ""kind"": ""createUnit"", ""parameters"": { ""alias"": ""caster"", ""playerNumber"": 0, ""health"": 6, ""maxHealth"": 10 } },
    { ""adapter"": ""Skill"", ""kind"": ""setTurnContext"", ""parameters"": { ""currentPlayerNumber"": 0, ""playableUnitAliases"": [""caster""] } }
  ],
  ""runtimeActions"": [
    { ""adapter"": ""Skill"", ""kind"": ""executeSkillGraph"", ""parameters"": { ""graphAlias"": ""graph"", ""casterAlias"": ""caster"" } }
  ],
  ""assertionPlans"": [
    { ""adapter"": ""Skill"", ""kind"": ""executionStateEquals"", ""expected"": ""Completed"", ""parameters"": {} },
    { ""adapter"": ""Skill"", ""kind"": ""unitHealthEquals"", ""target"": ""caster"", ""expected"": 10, ""parameters"": {} }
  ],
  ""timeoutMs"": 10000,
  ""probeRequests"": []
}";

        private const string SingleTargetDamagePlanJson = @"
{
  ""schemaVersion"": 1,
  ""scenarioName"": ""SkillGraph.SingleTargetDamageReducesTargetHealth"",
  ""requiredAdapters"": [""Skill""],
  ""setupActions"": [
    { ""adapter"": ""Skill"", ""kind"": ""createSkillTestWorld"", ""parameters"": {} },
    { ""adapter"": ""Skill"", ""kind"": ""createSkillGraph"", ""parameters"": { ""alias"": ""graph"", ""graphKind"": ""singleTargetDamage"", ""baseDamage"": 7 } },
    { ""adapter"": ""Skill"", ""kind"": ""createUnit"", ""parameters"": { ""alias"": ""caster"", ""playerNumber"": 0, ""cell"": { ""x"": 0, ""y"": 0 } } },
    { ""adapter"": ""Skill"", ""kind"": ""createUnit"", ""parameters"": { ""alias"": ""target"", ""playerNumber"": 1, ""health"": 10, ""maxHealth"": 10, ""defenceFactor"": 0, ""cell"": { ""x"": 1, ""y"": 0 } } },
    { ""adapter"": ""Skill"", ""kind"": ""setTurnContext"", ""parameters"": { ""currentPlayerNumber"": 0, ""playableUnitAliases"": [""caster""] } }
  ],
  ""runtimeActions"": [
    { ""adapter"": ""Skill"", ""kind"": ""executeSkillGraph"", ""parameters"": { ""graphAlias"": ""graph"", ""casterAlias"": ""caster"" } }
  ],
  ""assertionPlans"": [
    { ""adapter"": ""Skill"", ""kind"": ""executionStateEquals"", ""expected"": ""Completed"", ""parameters"": {} },
    { ""adapter"": ""Skill"", ""kind"": ""unitHealthEquals"", ""target"": ""target"", ""expected"": 3, ""parameters"": {} }
  ],
  ""timeoutMs"": 10000,
  ""probeRequests"": []
}";

        private const string InvalidGraphPlanJson = @"
{
  ""schemaVersion"": 1,
  ""scenarioName"": ""SkillGraph.InvalidGraphWithoutTerminalIsRejected"",
  ""requiredAdapters"": [""Skill""],
  ""setupActions"": [
    { ""adapter"": ""Skill"", ""kind"": ""createSkillTestWorld"", ""parameters"": {} },
    { ""adapter"": ""Skill"", ""kind"": ""createSkillGraph"", ""parameters"": { ""alias"": ""graph"", ""graphKind"": ""invalidSelfHeal"", ""healAmount"": 5 } },
    { ""adapter"": ""Skill"", ""kind"": ""createUnit"", ""parameters"": { ""alias"": ""caster"", ""playerNumber"": 0 } },
    { ""adapter"": ""Skill"", ""kind"": ""setTurnContext"", ""parameters"": { ""currentPlayerNumber"": 0, ""playableUnitAliases"": [""caster""] } }
  ],
  ""runtimeActions"": [
    { ""adapter"": ""Skill"", ""kind"": ""executeSkillGraph"", ""parameters"": { ""graphAlias"": ""graph"", ""casterAlias"": ""caster"" } }
  ],
  ""assertionPlans"": [
    { ""adapter"": ""Skill"", ""kind"": ""executionStateEquals"", ""expected"": ""Aborted"", ""parameters"": {} },
    { ""adapter"": ""Skill"", ""kind"": ""validationErrorCodeIncludes"", ""expected"": ""NoTerminalNode"", ""parameters"": {} }
  ],
  ""timeoutMs"": 10000,
  ""probeRequests"": []
}";
    }
}
