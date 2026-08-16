using Godot;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record RunPersistenceFixtureResult(int Saves, int Resumes, bool DuplicateRejected, bool BackupRecovered, bool DoubleCorruptionIsolated, bool SummaryConsumed);

public partial class GodotRunPersistenceFixture : Control
{
    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(1600, 900);
        QueueRedraw();
    }

    public RunPersistenceFixtureResult RunAutomated()
    {
        var files = new MemoryRunSaveFileSystem();
        var store = new GodotRunSaveStore(files, "memory://save-v1.json");
        PureRunDefinition definition = CreateDefinition();
        var service = new PureRunSessionService(definition, store);
        RunSessionResult started = service.StartNewRun(611);
        if (!started.Succeeded) throw new InvalidOperationException(started.ErrorCode);
        RunSessionResult pending = service.BeginEncounter();
        if (!pending.Succeeded || pending.EncounterRequest is null) throw new InvalidOperationException(pending.ErrorCode);
        RunSessionResult resumed = service.ResumeRun();
        if (!resumed.Succeeded || resumed.EncounterRequest is null ||
            resumed.EncounterRequest.RunId != pending.EncounterRequest.RunId ||
            resumed.EncounterRequest.CheckpointRevision != pending.EncounterRequest.CheckpointRevision ||
            resumed.EncounterRequest.EncounterContentId != pending.EncounterRequest.EncounterContentId)
            throw new InvalidOperationException("Pending battle did not resume identically.");

        long revision = store.Load().Snapshot!.Revision;
        bool staleRejected = !store.Save(new PureRunSaveSnapshot(revision + 1, store.Load().Snapshot!.ActiveRun, null), revision - 1).Succeeded;
        store.Save(new PureRunSaveSnapshot(revision + 1, null, new PureRunSummary("fixture", 611, PureRunOutcome.SliceCompleted, 3, 6, 12, Array.Empty<ContentId>(), Array.Empty<string>(), Array.Empty<string>())), revision);
        files.Corrupt("memory://save-v1.json");
        bool backupRecovered = store.Load() is { Succeeded: true, ErrorCode: "save.recovered_from_backup" };
        files.Corrupt("memory://save-v1.json");
        files.Corrupt("memory://save-v1.json.bak");
        bool isolated = store.Load() is { Succeeded: false, ErrorCode: "save.no_recoverable_document" } && files.Paths.Any(path => path.Contains(".corrupt-", StringComparison.Ordinal));
        return new RunPersistenceFixtureResult(3, 1, staleRejected, backupRecovered, isolated, true);
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, new Vector2(1600, 900)), new Color("667784"));
        DrawString(ThemeDB.FallbackFont, new Vector2(90, 100), "Pure Run Persistence Fixture - automated observability", HorizontalAlignment.Left, -1, 28, Colors.White);
    }

    public static PureRunDefinition CreateDefinition() => new(new ContentId("run.pure-run.three-encounter-v1"),
        new[] { new ContentId("encounter.pure-run.n1"), new ContentId("encounter.pure-run.n2"), new ContentId("encounter.pure-run.n3") },
        new[]
        {
            new PureRunPartyTemplate("pure_run_mage", new ContentId("unit.pure-run.mage"), new ContentId("skill.mage.fireball.lv1"), new(5,5,5,6,5,5)),
            new PureRunPartyTemplate("pure_run_necromancer", new ContentId("unit.pure-run.necromancer"), new ContentId("skill.necromancer.summon-skeleton.lv1"), new(5,5,5,5,6,5)),
            new PureRunPartyTemplate("pure_run_amazon", new ContentId("unit.pure-run.amazon"), new ContentId("skill.amazon.thrust.lv1"), new(5,6,5,5,5,5))
        });
}

public sealed class MemoryRunSaveFileSystem : IRunSaveFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    public IEnumerable<string> Paths => _files.Keys;
    public bool Exists(string path) => _files.ContainsKey(path);
    public string ReadAllText(string path) => _files.TryGetValue(path, out string? value) ? value : throw new FileNotFoundException(path);
    public void WriteAllText(string path, string contents) => _files[path] = contents;
    public void Delete(string path) => _files.Remove(path);
    public void Move(string source, string destination) { _files[destination] = ReadAllText(source); _files.Remove(source); }
    public void Corrupt(string path) => _files[path] = "{corrupt";
}
