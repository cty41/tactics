using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// 失败分类枚举
    /// </summary>
    public enum FailureCategory
    {
        None,
        Validation,
        Setup,
        Action,
        Assertion,
        Timeout,
        Asset
    }

    /// <summary>
    /// 失败详情
    /// </summary>
    public sealed class FailureInfo
    {
        public FailureCategory Category { get; set; }
        public string Phase { get; set; }
        public string Kind { get; set; }
        public string Adapter { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return $"[{Category}] {Phase}/{Kind}: {Message}";
        }
    }

    public sealed class GameplayTestResult
    {
        public string ScenarioName { get; set; }
        public List<GameplayStepResult> ExecutedSteps { get; } = new();
        public List<GameplayAssertionResult> Assertions { get; } = new();
        public List<string> Diagnostics { get; } = new();
        public List<ProbeSnapshot> Probes { get; } = new();
        public List<FailureInfo> Failures { get; } = new();
        public bool Passed => Failures.Count == 0 && Diagnostics.Count == 0 && Assertions.TrueForAll(assertion => assertion.Passed);
        public FailureCategory FailureCategory => Failures.FirstOrDefault()?.Category ?? FailureCategory.None;

        public void AddFailure(FailureCategory category, string phase, string kind, string adapter, string message)
        {
            Failures.Add(new FailureInfo
            {
                Category = category,
                Phase = phase,
                Kind = kind,
                Adapter = adapter,
                Message = message
            });
            Diagnostics.Add($"[{category}] {phase}/{kind}: {message}");
        }
    }

    public sealed class GameplayStepResult
    {
        public string Kind { get; set; }
        public string Adapter { get; set; }
        public bool Passed { get; set; }
        public string Message { get; set; }
        public FailureCategory FailureCategory { get; set; }

        public static GameplayStepResult Pass(string adapter, string kind, string message = null)
        {
            return new GameplayStepResult { Adapter = adapter, Kind = kind, Passed = true, Message = message };
        }

        public static GameplayStepResult Fail(string adapter, string kind, string message, string category = null)
        {
            var failureCategory = category?.ToLower() switch
            {
                "asset" => FailureCategory.Asset,
                "validation" => FailureCategory.Validation,
                "setup" => FailureCategory.Setup,
                "action" => FailureCategory.Action,
                _ => FailureCategory.None  // 默认不设置，让 runner 根据 action 类型决定
            };
            return new GameplayStepResult { Adapter = adapter, Kind = kind, Passed = false, Message = message, FailureCategory = failureCategory };
        }
    }

    public sealed class GameplayAssertionResult
    {
        public string Kind { get; set; }
        public string Adapter { get; set; }
        public string Target { get; set; }
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

    /// <summary>
    /// 批量测试结果摘要
    /// </summary>
    public sealed class BatchTestSummary
    {
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int TimedOut { get; set; }
        public Dictionary<FailureCategory, int> FailureCounts { get; } = new();
        public List<ScenarioSummary> Scenarios { get; } = new();

        public void AddResult(GameplayTestResult result)
        {
            Total++;
            if (result.Passed)
            {
                Passed++;
            }
            else
            {
                var category = result.FailureCategory;
                if (category == FailureCategory.Timeout)
                {
                    TimedOut++;
                }
                else
                {
                    Failed++;
                    if (category != FailureCategory.None)
                    {
                        FailureCounts[category] = FailureCounts.GetValueOrDefault(category) + 1;
                    }
                }
            }

            Scenarios.Add(new ScenarioSummary
            {
                Name = result.ScenarioName,
                Passed = result.Passed,
                FailureCategory = result.FailureCategory,
                FirstDiagnostic = result.Diagnostics.FirstOrDefault()
            });
        }
    }

    public sealed class ScenarioSummary
    {
        public string Name { get; set; }
        public bool Passed { get; set; }
        public FailureCategory FailureCategory { get; set; }
        public string FirstDiagnostic { get; set; }
    }
}
