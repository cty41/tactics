using System.Text;
using System.Text.Json;
using Godot;

namespace Tactics.Godot.Tests.GameplaySpec;

public sealed record GodotGameplaySpecReport(string Schema, string Runtime, int Total, int Passed, int Failed,
    IReadOnlyList<GodotGameplayReportScenario> Scenarios)
{
    public static GodotGameplaySpecReport Create(IReadOnlyList<GodotGameplayReportScenario> scenarios) =>
        new("godot-gameplay-spec-result-v1", "Godot", scenarios.Count, scenarios.Count(value => value.Succeeded),
            scenarios.Count(value => !value.Succeeded), scenarios);
}

public sealed record GodotGameplayReportScenario(string ScenarioName, bool Succeeded, string? FailureKind,
    string? ErrorCode, string? CheckpointId, string? CheckpointSource, string? CheckpointPath,
    string? CheckpointSemanticHash, bool ProductionSaveUnchanged, string ProductionSaveBefore,
    string ProductionSaveAfter, int RemainingTemporaryNodes, IReadOnlyList<GodotGameplayTraceEntry> Trace)
{
    public static GodotGameplayReportScenario From(GodotGameplayScenarioPlan plan, GodotGameplayScenarioResult result) =>
        new(result.ScenarioName, result.Succeeded, result.FailureKind?.ToString(), result.ErrorCode,
            plan.Checkpoint?.Id, plan.Checkpoint?.Source, plan.Checkpoint?.Path, plan.Checkpoint?.SemanticHash,
            result.ProductionSaveUnchanged, result.ProductionSaveBefore, result.ProductionSaveAfter,
            result.RemainingTemporaryNodes, result.Trace);
}

public static class GodotGameplaySpecReportWriter
{
    public static string Write(GodotGameplaySpecReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        string output = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "artifacts",
            "gameplay-specs", "godot", "godot-gameplay-spec-result-v1.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        File.WriteAllText(output, json + System.Environment.NewLine, new UTF8Encoding(false));
        return output;
    }
}
