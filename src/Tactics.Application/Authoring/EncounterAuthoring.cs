using System.Text.Json;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;

namespace Tactics.Application.Authoring;

public sealed record GridCellAuthoring(int X, int Y);

public sealed class BattleLayoutAuthoringDocument : IAuthoringDocument
{
    public BattleLayoutAuthoringDocument(string contentId, IEnumerable<GridCellAuthoring> partySpawns,
        IEnumerable<GridCellAuthoring> enemySpawns, IEnumerable<GridCellAuthoring> blockedCells)
    {
        ContentId = Require(contentId);
        PartySpawns = Read(partySpawns); EnemySpawns = Read(enemySpawns); BlockedCells = Read(blockedCells);
        Validate();
    }
    public string ContentId { get; }
    public int SchemaVersion => 1;
    public IReadOnlyList<GridCellAuthoring> PartySpawns { get; }
    public IReadOnlyList<GridCellAuthoring> EnemySpawns { get; }
    public IReadOnlyList<GridCellAuthoring> BlockedCells { get; }
    public IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public BattleLayoutDefinition ToCoreDefinition() => new(new ContentId(ContentId), Build(PartySpawns), Build(EnemySpawns), Build(BlockedCells));
    public void Validate()
    {
        GridCellAuthoring[] all = PartySpawns.Concat(EnemySpawns).Concat(BlockedCells).ToArray();
        if (all.Any(value => value.X is < 0 or >= 10 || value.Y is < 0 or >= 10)) throw new ArgumentOutOfRangeException(nameof(all), "Battle layout cells must be inside the 10x10 board.");
        if (all.Distinct().Count() != all.Length) throw new ArgumentException("Battle layout cells cannot overlap.");
        if (PartySpawns.Count == 0 || EnemySpawns.Count == 0) throw new ArgumentException("Battle layouts require party and enemy spawns.");
    }
    public void WriteCanonical(Utf8JsonWriter writer)
    {
        writer.WriteStartObject(); writer.WriteString("contentId", ContentId); writer.WriteNumber("schemaVersion", SchemaVersion);
        Write(writer, "partySpawns", PartySpawns); Write(writer, "enemySpawns", EnemySpawns); Write(writer, "blockedCells", BlockedCells); writer.WriteEndObject();
    }
    private static IReadOnlyList<GridCellAuthoring> Read(IEnumerable<GridCellAuthoring> values) => Array.AsReadOnly((values ?? throw new ArgumentNullException(nameof(values))).ToArray());
    private static GridPoint[] Build(IEnumerable<GridCellAuthoring> values) => values.Select(value => new GridPoint(value.X, value.Y)).ToArray();
    private static void Write(Utf8JsonWriter writer, string name, IEnumerable<GridCellAuthoring> cells) { writer.WriteStartArray(name); foreach (GridCellAuthoring cell in cells) { writer.WriteStartObject(); writer.WriteNumber("x", cell.X); writer.WriteNumber("y", cell.Y); writer.WriteEndObject(); } writer.WriteEndArray(); }
    private static string Require(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("ContentId is required.") : value;
}

public sealed class EncounterAuthoringDocument : IAuthoringDocument
{
    public EncounterAuthoringDocument(string contentId, string layoutContentId, IEnumerable<string> monsterUnitContentIds,
        IEnumerable<string> monsterAiContentIds, float healthMultiplier, float outputMultiplier, int minimumStartingMana,
        EncounterClass encounterClass)
    {
        ContentId = Require(contentId); LayoutContentId = Require(layoutContentId);
        MonsterUnitContentIds = Read(monsterUnitContentIds); MonsterAiContentIds = Read(monsterAiContentIds);
        HealthMultiplier = healthMultiplier; OutputMultiplier = outputMultiplier; MinimumStartingMana = minimumStartingMana; EncounterClass = encounterClass;
        _ = ToCoreDefinition();
    }
    public string ContentId { get; }
    public int SchemaVersion => 1;
    public string LayoutContentId { get; }
    public IReadOnlyList<string> MonsterUnitContentIds { get; }
    public IReadOnlyList<string> MonsterAiContentIds { get; }
    public float HealthMultiplier { get; }
    public float OutputMultiplier { get; }
    public int MinimumStartingMana { get; }
    public EncounterClass EncounterClass { get; }
    public IReadOnlyList<string> Dependencies => Array.AsReadOnly(new[] { LayoutContentId }.Concat(MonsterUnitContentIds).Concat(MonsterAiContentIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    public EncounterDefinition ToCoreDefinition()
    {
        if (MonsterUnitContentIds.Count == 0 || MonsterUnitContentIds.Count != MonsterAiContentIds.Count) throw new ArgumentException("Encounter unit and AI bindings must be non-empty and aligned.");
        if (HealthMultiplier <= 0 || OutputMultiplier <= 0 || MinimumStartingMana < 0) throw new ArgumentOutOfRangeException(nameof(HealthMultiplier));
        EncounterMonsterDefinition[] monsters = MonsterUnitContentIds.Select((value, index) =>
            new EncounterMonsterDefinition(new ContentId(value), new ContentId(MonsterAiContentIds[index]), Array.Empty<ContentId>())).ToArray();
        return new EncounterDefinition(new ContentId(ContentId), new ContentId(LayoutContentId), monsters,
            HealthMultiplier, OutputMultiplier, MinimumStartingMana, EncounterClass);
    }
    public void WriteCanonical(Utf8JsonWriter writer)
    {
        writer.WriteStartObject(); writer.WriteString("contentId", ContentId); writer.WriteNumber("schemaVersion", SchemaVersion); writer.WriteString("layoutContentId", LayoutContentId);
        Write(writer, "monsterUnitContentIds", MonsterUnitContentIds); Write(writer, "monsterAiContentIds", MonsterAiContentIds);
        writer.WriteNumber("healthMultiplier", HealthMultiplier); writer.WriteNumber("outputMultiplier", OutputMultiplier); writer.WriteNumber("minimumStartingMana", MinimumStartingMana); writer.WriteString("encounterClass", EncounterClass.ToString()); writer.WriteEndObject();
    }
    private static IReadOnlyList<string> Read(IEnumerable<string> values) => Array.AsReadOnly((values ?? throw new ArgumentNullException(nameof(values))).Select(Require).ToArray());
    private static void Write(Utf8JsonWriter writer, string name, IEnumerable<string> values) { writer.WriteStartArray(name); foreach (string value in values) writer.WriteStringValue(value); writer.WriteEndArray(); }
    private static string Require(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Content identity is required.") : value;
}

public static class EncounterAuthoringJson
{
    public static string Serialize(EncounterAuthoringDocument document) { using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true })) document.WriteCanonical(writer); return System.Text.Encoding.UTF8.GetString(stream.ToArray()); }
    public static EncounterAuthoringDocument Deserialize(string json)
    {
        using JsonDocument payload = JsonDocument.Parse(json); JsonElement root = payload.RootElement;
        return new EncounterAuthoringDocument(root.GetProperty("contentId").GetString()!, root.GetProperty("layoutContentId").GetString()!, root.GetProperty("monsterUnitContentIds").EnumerateArray().Select(value => value.GetString()!), root.GetProperty("monsterAiContentIds").EnumerateArray().Select(value => value.GetString()!), root.GetProperty("healthMultiplier").GetSingle(), root.GetProperty("outputMultiplier").GetSingle(), root.GetProperty("minimumStartingMana").GetInt32(), Enum.Parse<EncounterClass>(root.GetProperty("encounterClass").GetString()!));
    }
}

public static class BattleLayoutAuthoringJson
{
    public static string Serialize(BattleLayoutAuthoringDocument document) { using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true })) document.WriteCanonical(writer); return System.Text.Encoding.UTF8.GetString(stream.ToArray()); }
    public static BattleLayoutAuthoringDocument Deserialize(string json)
    {
        using JsonDocument payload = JsonDocument.Parse(json); JsonElement root = payload.RootElement;
        static IEnumerable<GridCellAuthoring> Cells(JsonElement value, string name) => value.GetProperty(name).EnumerateArray().Select(cell => new GridCellAuthoring(cell.GetProperty("x").GetInt32(), cell.GetProperty("y").GetInt32()));
        return new BattleLayoutAuthoringDocument(root.GetProperty("contentId").GetString()!, Cells(root, "partySpawns"), Cells(root, "enemySpawns"), Cells(root, "blockedCells"));
    }
}

public static class EncounterLayoutAuthoringValidator
{
    public static void Validate(EncounterAuthoringDocument encounter, BattleLayoutAuthoringDocument layout)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(layout);
        _ = encounter.ToCoreDefinition();
        layout.Validate();
        if (!string.Equals(encounter.LayoutContentId, layout.ContentId, StringComparison.Ordinal))
            throw new InvalidOperationException("Encounter LayoutContentId differs from the staged Layout identity.");
        if (encounter.MonsterUnitContentIds.Count > layout.EnemySpawns.Count)
            throw new InvalidOperationException("Encounter monsters exceed enemy spawn cells.");
    }
}
