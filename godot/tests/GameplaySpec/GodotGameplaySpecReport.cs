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
    string ProductionSaveAfter, int RemainingTemporaryNodes, IReadOnlyList<GodotGameplayTraceEntry> Trace,
    GodotDemonboundRunMetrics? DemonboundMetrics = null)
{
    public static GodotGameplayReportScenario From(GodotGameplayScenarioPlan plan, GodotGameplayScenarioResult result) =>
        new(result.ScenarioName, result.Succeeded, result.FailureKind?.ToString(), result.ErrorCode,
            plan.Checkpoint?.Id, plan.Checkpoint?.Source, plan.Checkpoint?.Path, plan.Checkpoint?.SemanticHash,
            result.ProductionSaveUnchanged, result.ProductionSaveBefore, result.ProductionSaveAfter,
            result.RemainingTemporaryNodes, result.Trace, result.DemonboundMetrics);
}

public sealed record GodotDemonboundProductionSample(string Party, int Seed, GodotDemonboundRunMetrics Metrics,
    string? FailureKind, string? ErrorCode, IReadOnlyList<GodotGameplayTraceEntry> Trace);

public sealed record GodotDemonboundProductionSeedDiagnostic(string Party, int Seed,
    GodotGameplayScenarioResult First, GodotGameplayScenarioResult Replay);

public static class GodotDemonboundProductionMetricsWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static (string JsonPath, string CsvPath) Write(IReadOnlyList<GodotDemonboundProductionSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        string directory = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "artifacts",
            "gameplay-specs", "godot"));
        Directory.CreateDirectory(directory);
        string jsonPath = Path.Combine(directory, "demonbound-production-metrics-v1.json");
        string csvPath = Path.Combine(directory, "demonbound-production-metrics-v1.csv");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(samples, JsonOptions) + System.Environment.NewLine, new UTF8Encoding(false));
        var csv = new StringBuilder("party,seed,outcome,failure_kind,error_code,battles_completed,encounters_observed,corruption_peak,meditations,first_possession_round,friendly_damage,downs,permanent_deaths,bane_uses,bane_double_target_uses\n");
        foreach (GodotDemonboundProductionSample sample in samples)
        {
            GodotDemonboundRunMetrics value = sample.Metrics;
            csv.Append(sample.Party).Append(',').Append(sample.Seed).Append(',').Append(value.Outcome).Append(',')
                .Append(sample.FailureKind).Append(',').Append(Csv(sample.ErrorCode)).Append(',')
                .Append(value.BattlesCompleted).Append(',').Append(value.EncountersObserved).Append(',')
                .Append(value.CorruptionPeak).Append(',').Append(value.Meditations).Append(',')
                .Append(value.FirstPossessionRound?.ToString() ?? string.Empty).Append(',').Append(value.FriendlyDamage).Append(',')
                .Append(value.Downs).Append(',').Append(value.PermanentDeaths).Append(',').Append(value.BaneUses).Append(',')
                .Append(value.BaneDoubleTargetUses).AppendLine();
        }
        File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(false));
        return (jsonPath, csvPath);
    }

    public static string WriteSeedDiagnostic(GodotDemonboundProductionSeedDiagnostic diagnostic)
    {
        string directory = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "artifacts",
            "gameplay-specs", "godot"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"demonbound-production-seed-{diagnostic.Seed:D2}-diagnostic-v1.json");
        File.WriteAllText(path, JsonSerializer.Serialize(diagnostic, JsonOptions) + System.Environment.NewLine, new UTF8Encoding(false));
        return path;
    }

    private static string Csv(string? value) => string.IsNullOrEmpty(value) ? string.Empty :
        '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
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
