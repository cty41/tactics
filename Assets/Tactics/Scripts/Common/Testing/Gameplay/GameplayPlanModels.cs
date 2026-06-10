using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tactics.Common.Testing.Gameplay
{
    public sealed class ExecutableScenarioPlan
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }
        [JsonProperty("scenarioName")] public string ScenarioName { get; set; }
        [JsonProperty("requiredAdapters")] public List<string> RequiredAdapters { get; set; } = new();
        [JsonProperty("setupActions")] public List<ExecutableScenarioAction> SetupActions { get; set; } = new();
        [JsonProperty("runtimeActions")] public List<ExecutableScenarioAction> RuntimeActions { get; set; } = new();
        [JsonProperty("assertionPlans")] public List<ExecutableScenarioAssertion> AssertionPlans { get; set; } = new();
        [JsonProperty("timeoutMs")] public int TimeoutMs { get; set; } = 10000;
        [JsonProperty("probeRequests")] public List<GameplayProbeRequest> ProbeRequests { get; set; } = new();
    }

    public sealed class ExecutableScenarioAction
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("adapter")] public string Adapter { get; set; }
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("target")] public string Target { get; set; }
        [JsonProperty("parameters")] public JObject Parameters { get; set; } = new();
    }

    public sealed class ExecutableScenarioAssertion
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("adapter")] public string Adapter { get; set; }
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("target")] public string Target { get; set; }
        [JsonProperty("expected")] public JToken Expected { get; set; }
        [JsonProperty("parameters")] public JObject Parameters { get; set; } = new();
    }

    public sealed class GameplayProbeRequest
    {
        [JsonProperty("adapter")] public string Adapter { get; set; }
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("target")] public string Target { get; set; }
        [JsonProperty("parameters")] public JObject Parameters { get; set; } = new();
    }

    public sealed class GameplayTestResult
    {
        public string ScenarioName { get; set; }
        public List<GameplayStepResult> ExecutedSteps { get; } = new();
        public List<GameplayAssertionResult> Assertions { get; } = new();
        public List<string> Diagnostics { get; } = new();
        public List<ProbeSnapshot> Probes { get; } = new();
        public bool Passed => Diagnostics.Count == 0 && Assertions.TrueForAll(assertion => assertion.Passed);
    }

    public sealed class GameplayStepResult
    {
        public string Kind { get; set; }
        public string Adapter { get; set; }
        public bool Passed { get; set; }
        public string Message { get; set; }

        public static GameplayStepResult Pass(string adapter, string kind, string message = null)
        {
            return new GameplayStepResult { Adapter = adapter, Kind = kind, Passed = true, Message = message };
        }

        public static GameplayStepResult Fail(string adapter, string kind, string message)
        {
            return new GameplayStepResult { Adapter = adapter, Kind = kind, Passed = false, Message = message };
        }
    }

    public sealed class GameplayAssertionResult
    {
        public string Kind { get; set; }
        public string Adapter { get; set; }
        public bool Passed { get; set; }
        public string Message { get; set; }

        public static GameplayAssertionResult Pass(string adapter, string kind, string message = null)
        {
            return new GameplayAssertionResult { Adapter = adapter, Kind = kind, Passed = true, Message = message };
        }

        public static GameplayAssertionResult Fail(string adapter, string kind, string message)
        {
            return new GameplayAssertionResult { Adapter = adapter, Kind = kind, Passed = false, Message = message };
        }
    }

    public sealed class ProbeSnapshot
    {
        public string Adapter { get; set; }
        public string Kind { get; set; }
        public string Target { get; set; }
        public JObject Data { get; set; } = new();
    }
}
