using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Tactics.Common.Testing.Gameplay
{
    public static class ExecutableScenarioPlanLoader
    {
        public static ExecutableScenarioPlan FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Plan JSON is empty.", nameof(json));

            var plan = JsonConvert.DeserializeObject<ExecutableScenarioPlan>(json);
            if (plan == null)
                throw new InvalidOperationException("Plan JSON could not be deserialized.");

            Validate(plan);
            return plan;
        }

        public static ExecutableScenarioPlan FromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Plan path is empty.", nameof(path));

            return FromJson(File.ReadAllText(path));
        }

        private static void Validate(ExecutableScenarioPlan plan)
        {
            var errors = new List<string>();

            if (plan.SchemaVersion != 1)
                errors.Add($"Unsupported schemaVersion '{plan.SchemaVersion}'. Expected 1.");

            if (string.IsNullOrWhiteSpace(plan.ScenarioName))
                errors.Add("ScenarioName is required.");

            if (plan.TimeoutMs <= 0)
                errors.Add("TimeoutMs must be positive.");

            if (plan.RequiredAdapters == null || plan.RequiredAdapters.Count == 0)
                errors.Add("Plan must declare at least one required adapter.");
            else if (plan.RequiredAdapters.Any(string.IsNullOrWhiteSpace))
                errors.Add("RequiredAdapters contains an empty adapter name.");

            if (plan.RuntimeActions == null || plan.RuntimeActions.Count == 0)
                errors.Add("Plan must define at least one runtime action.");

            if (plan.AssertionPlans == null || plan.AssertionPlans.Count == 0)
                errors.Add("Plan must define at least one assertion.");

            ValidateActions(plan.SetupActions, "setupActions", errors);
            ValidateActions(plan.RuntimeActions, "runtimeActions", errors);
            ValidateAssertions(plan.AssertionPlans, "assertionPlans", errors);
            ValidateProbes(plan.ProbeRequests, "probeRequests", errors);

            if (errors.Count > 0)
                throw new InvalidOperationException("Plan validation failed: " + string.Join(" ", errors));
        }

        private static void ValidateActions(IEnumerable<ExecutableScenarioAction> actions, string sectionName, ICollection<string> errors)
        {
            if (actions == null)
            {
                errors.Add($"{sectionName} is required.");
                return;
            }

            int index = 0;
            foreach (var action in actions)
            {
                if (action == null)
                {
                    errors.Add($"{sectionName}[{index}] is null.");
                    index++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(action.Adapter))
                    errors.Add($"{sectionName}[{index}] is missing adapter.");

                if (string.IsNullOrWhiteSpace(action.Kind))
                    errors.Add($"{sectionName}[{index}] is missing kind.");

                index++;
            }
        }

        private static void ValidateAssertions(IEnumerable<ExecutableScenarioAssertion> assertions, string sectionName, ICollection<string> errors)
        {
            if (assertions == null)
            {
                errors.Add($"{sectionName} is required.");
                return;
            }

            int index = 0;
            foreach (var assertion in assertions)
            {
                if (assertion == null)
                {
                    errors.Add($"{sectionName}[{index}] is null.");
                    index++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(assertion.Adapter))
                    errors.Add($"{sectionName}[{index}] is missing adapter.");

                if (string.IsNullOrWhiteSpace(assertion.Kind))
                    errors.Add($"{sectionName}[{index}] is missing kind.");

                index++;
            }
        }

        private static void ValidateProbes(IEnumerable<GameplayProbeRequest> probes, string sectionName, ICollection<string> errors)
        {
            if (probes == null)
            {
                errors.Add($"{sectionName} is required.");
                return;
            }

            int index = 0;
            foreach (var probe in probes)
            {
                if (probe == null)
                {
                    errors.Add($"{sectionName}[{index}] is null.");
                    index++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(probe.Adapter))
                    errors.Add($"{sectionName}[{index}] is missing adapter.");

                if (string.IsNullOrWhiteSpace(probe.Kind))
                    errors.Add($"{sectionName}[{index}] is missing kind.");

                index++;
            }
        }
    }
}
