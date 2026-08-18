using GdUnit4;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests.GameplaySpec;

[TestSuite]
public sealed class GodotDemonboundLongRunTests
{
    [TestCase]
    [RequireGodotRuntime]
    public async Task DemonboundProductionThirtySeedRunsUseRealInputAndWriteReplayableMetrics()
    {
        string[][] parties =
        [
            ["pure_run_mage", "pure_run_amazon", "pure_run_demonbound"],
            ["pure_run_mage", "pure_run_necromancer", "pure_run_demonbound"],
            ["pure_run_amazon", "pure_run_necromancer", "pure_run_demonbound"]
        ];
        GodotGameplayScenarioPlan basePlan = GodotGameplayRuntimeRunnerTests.LoadCompiledPlan("adventure-fixed-seed-full-run");
        var samples = new List<GodotDemonboundProductionSample>();
        var failures = new List<string>();
        foreach (string[] party in parties)
        foreach (int seed in Enumerable.Range(0, 10))
        {
            string partyId = string.Join("+", party.Select(value => value[9..]));
            GodotGameplayScenarioPlan plan = GodotGameplayRuntimeRunnerTests.WithParty(basePlan, party,
                $"{basePlan.ScenarioName}.{partyId}.seed{seed:D2}") with
            {
                Watchdog = basePlan.Watchdog with { ScenarioTimeoutMs = 120_000 }
            };
            GodotGameplayScenarioResult result = await new GodotGameplayRuntimeRunner().ExecuteAsync(plan,
                new GodotGameplayRunOptions(seed, $"demonbound-{partyId}-{seed:D2}"));
            GodotDemonboundRunMetrics metrics = result.DemonboundMetrics
                ?? throw new InvalidOperationException("Demonbound metrics were not captured.");
            samples.Add(new GodotDemonboundProductionSample(partyId, seed, metrics,
                result.FailureKind?.ToString(), result.ErrorCode, result.Trace));
            AssertThat(result.ProductionSaveUnchanged).IsTrue();
            AssertThat(result.RemainingTemporaryNodes).IsEqual(0);
            if (!result.Succeeded || metrics.Outcome is null)
                failures.Add($"{partyId}:{seed}:{result.ErrorCode ?? "terminal_outcome_missing"}");
            AssertThat(metrics.CorruptionPeak).IsLessEqual(10);
        }
        AssertThat(samples.Count).IsEqual(30);
        AssertThat(samples.Select(value => (value.Party, value.Seed)).Distinct().Count()).IsEqual(30);
        (string json, string csv) = GodotDemonboundProductionMetricsWriter.Write(samples);
        AssertThat(File.Exists(json)).IsTrue();
        AssertThat(File.Exists(csv)).IsTrue();
        AssertThat(failures).IsEmpty();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task DemonboundProductionSeedZeroWritesReplayableFailureEvidence()
    {
        const string partyId = "mage+amazon+demonbound";
        string[] party = ["pure_run_mage", "pure_run_amazon", "pure_run_demonbound"];
        GodotGameplayScenarioPlan basePlan = GodotGameplayRuntimeRunnerTests.LoadCompiledPlan("adventure-fixed-seed-full-run");
        GodotGameplayScenarioPlan plan = GodotGameplayRuntimeRunnerTests.WithParty(basePlan, party,
            $"{basePlan.ScenarioName}.{partyId}.seed00") with
        {
            Watchdog = basePlan.Watchdog with { ScenarioTimeoutMs = 120_000 }
        };
        var runner = new GodotGameplayRuntimeRunner();
        GodotGameplayScenarioResult first = await runner.ExecuteAsync(plan,
            new GodotGameplayRunOptions(0, "demonbound-diagnostic-seed00-first"));
        GodotGameplayScenarioResult replay = await runner.ExecuteAsync(plan,
            new GodotGameplayRunOptions(0, "demonbound-diagnostic-seed00-replay"));
        string path = GodotDemonboundProductionMetricsWriter.WriteSeedDiagnostic(
            new GodotDemonboundProductionSeedDiagnostic(partyId, 0, first, replay));

        AssertThat(File.Exists(path)).IsTrue();
        AssertThat(first.ProductionSaveUnchanged).IsTrue();
        AssertThat(replay.ProductionSaveUnchanged).IsTrue();
        AssertThat(first.RemainingTemporaryNodes).IsEqual(0);
        AssertThat(replay.RemainingTemporaryNodes).IsEqual(0);
        AssertThat(first.FailureKind).IsEqual(replay.FailureKind);
        AssertThat(string.Equals(first.ErrorCode, replay.ErrorCode, StringComparison.Ordinal)).IsTrue();
        AssertThat(first.DemonboundMetrics).IsNotNull();
        AssertThat(replay.DemonboundMetrics).IsNotNull();
        AssertThat(string.Equals(first.DemonboundMetrics!.Outcome, replay.DemonboundMetrics!.Outcome,
            StringComparison.Ordinal)).IsTrue();
        AssertThat(first.Succeeded).IsTrue();
        AssertThat(first.DemonboundMetrics.Outcome).IsNotNull();
    }
}
