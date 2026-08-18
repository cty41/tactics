using System.Text.Json;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Application.Authoring;

public enum EventOutcomeType { Gold, Damage, Item, Buff, Debuff, Nothing }
public enum EventOutcomeTarget { All, Self }

public sealed record EventOutcomeAuthoring(
    EventOutcomeType Type,
    EventOutcomeTarget Target,
    int Amount,
    string? EffectContentId,
    string Description);

public sealed record EventOptionAuthoring(
    string OptionId,
    string Text,
    RunEventAttribute Attribute,
    int BaseSuccessRate,
    EventOutcomeAuthoring Success,
    EventOutcomeAuthoring? Failure);

public sealed class EventAuthoringDocument : IAuthoringDocument
{
    public EventAuthoringDocument(string contentId, string sourceId, string title, string description,
        IEnumerable<EventOptionAuthoring> options, string? sourcePath = null, string? sourceSha256 = null,
        AuthoringGraphLayout? graphLayout = null)
    {
        ContentId = Require(contentId, nameof(contentId));
        SourceId = Require(sourceId, nameof(sourceId));
        Title = Require(title, nameof(title));
        Description = description ?? string.Empty;
        Options = Array.AsReadOnly((options ?? throw new ArgumentNullException(nameof(options))).ToArray());
        SourcePath = sourcePath;
        SourceSha256 = sourceSha256;
        GraphLayout = graphLayout ?? new AuthoringGraphLayout();
        Validate();
    }

    public string ContentId { get; }
    public int SchemaVersion => 2;
    public string SourceId { get; }
    public string Title { get; }
    public string Description { get; }
    public IReadOnlyList<EventOptionAuthoring> Options { get; }
    public string? SourcePath { get; }
    public string? SourceSha256 { get; }
    public AuthoringGraphLayout GraphLayout { get; }
    public IReadOnlyList<string> Dependencies => Array.AsReadOnly(Options
        .SelectMany(value => new[] { value.Success.EffectContentId, value.Failure?.EffectContentId })
        .OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());

    public void Validate()
    {
        if (Options.Count == 0 || Options.Any(value => string.IsNullOrWhiteSpace(value.OptionId) ||
                string.IsNullOrWhiteSpace(value.Text) || value.BaseSuccessRate is < 0 or > 100))
            throw new ArgumentException("Events require non-empty options and rates from 0 to 100.");
        if (Options.Select(value => value.OptionId).Distinct(StringComparer.Ordinal).Count() != Options.Count)
            throw new ArgumentException("Event option identities must be unique.");
        foreach (EventOutcomeAuthoring outcome in Options.SelectMany(value => value.Failure is null
                     ? new[] { value.Success }
                     : new[] { value.Success, value.Failure }))
        {
            if (outcome.Amount < 0) throw new ArgumentOutOfRangeException(nameof(outcome.Amount));
            bool requiresReference = outcome.Type is EventOutcomeType.Item or EventOutcomeType.Buff or EventOutcomeType.Debuff;
            if (requiresReference && string.IsNullOrWhiteSpace(outcome.EffectContentId))
                throw new ArgumentException($"Event outcome '{outcome.Type}' requires a ContentId.");
            if (!requiresReference && !string.IsNullOrWhiteSpace(outcome.EffectContentId))
                throw new ArgumentException($"Event outcome '{outcome.Type}' cannot carry a ContentId.");
        }
        HashSet<string> layoutNodeIds = ["start", "end"];
        foreach (EventOptionAuthoring option in Options)
            foreach (string role in new[] { "option", "check", "success", "failure" })
                layoutNodeIds.Add($"{role}:{option.OptionId}");
        if (GraphLayout.Nodes.Any(value => !layoutNodeIds.Contains(value.NodeId)))
            throw new ArgumentException("Event graph layout contains an unknown stable node identity.");
    }

    public void WriteCanonical(Utf8JsonWriter writer) => WritePayload(writer, includeSchemaVersion: true);

    internal void WritePayload(Utf8JsonWriter writer, bool includeSchemaVersion)
    {
        writer.WriteStartObject();
        writer.WriteString("contentId", ContentId);
        if (includeSchemaVersion) writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteString("sourceId", SourceId);
        writer.WriteString("title", Title);
        writer.WriteString("description", Description);
        writer.WriteStartArray("options");
        foreach (EventOptionAuthoring option in Options)
        {
            writer.WriteStartObject();
            writer.WriteString("id", option.OptionId);
            writer.WriteString("text", option.Text);
            writer.WriteString("attribute", option.Attribute.ToString());
            writer.WriteNumber("baseSuccessRate", option.BaseSuccessRate);
            writer.WritePropertyName("success"); WriteOutcome(writer, option.Success);
            writer.WritePropertyName("failure");
            if (option.Failure is null) writer.WriteNullValue(); else WriteOutcome(writer, option.Failure);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WritePropertyName("graphLayout"); GraphLayout.WriteCanonical(writer);
        if (!string.IsNullOrWhiteSpace(SourcePath)) writer.WriteString("sourcePath", SourcePath);
        if (!string.IsNullOrWhiteSpace(SourceSha256)) writer.WriteString("sourceSha256", SourceSha256);
        writer.WriteEndObject();
    }

    private static void WriteOutcome(Utf8JsonWriter writer, EventOutcomeAuthoring outcome)
    {
        writer.WriteStartObject(); writer.WriteString("type", outcome.Type.ToString()); writer.WriteString("target", outcome.Target.ToString());
        writer.WriteNumber("amount", outcome.Amount);
        if (!string.IsNullOrWhiteSpace(outcome.EffectContentId)) writer.WriteString("itemId", outcome.EffectContentId);
        writer.WriteString("description", outcome.Description); writer.WriteEndObject();
    }

    private static string Require(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value;
}

public static class EventAuthoringJson
{
    public static EventAuthoringDocument Deserialize(string json)
    {
        using JsonDocument payload = JsonDocument.Parse(json);
        JsonElement root = payload.RootElement;
        return new EventAuthoringDocument(root.GetProperty("contentId").GetString()!, root.GetProperty("sourceId").GetString()!,
            root.GetProperty("title").GetString()!, root.GetProperty("description").GetString() ?? string.Empty,
            root.GetProperty("options").EnumerateArray().Select(ReadOption),
            root.TryGetProperty("sourcePath", out JsonElement path) ? path.GetString() : null,
            root.TryGetProperty("sourceSha256", out JsonElement sha) ? sha.GetString() : null,
            root.TryGetProperty("graphLayout", out JsonElement layout) ? AuthoringGraphLayout.Read(layout) : null);
    }

    public static string SerializePayload(EventAuthoringDocument document)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            document.WritePayload(writer, includeSchemaVersion: false);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static EventOptionAuthoring ReadOption(JsonElement option) => new(
        option.GetProperty("id").GetString()!, option.GetProperty("text").GetString()!,
        Enum.Parse<RunEventAttribute>(option.GetProperty("attribute").GetString()!),
        option.GetProperty("baseSuccessRate").GetInt32(), ReadOutcome(option.GetProperty("success")),
        option.TryGetProperty("failure", out JsonElement failure) && failure.ValueKind != JsonValueKind.Null ? ReadOutcome(failure) : null);

    private static EventOutcomeAuthoring ReadOutcome(JsonElement outcome) => new(
        Enum.Parse<EventOutcomeType>(outcome.GetProperty("type").GetString()!, true),
        Enum.Parse<EventOutcomeTarget>(outcome.GetProperty("target").GetString()!, true),
        outcome.TryGetProperty("amount", out JsonElement amount) ? amount.GetInt32() : 0,
        outcome.TryGetProperty("itemId", out JsonElement item) ? item.GetString() : null,
        outcome.TryGetProperty("description", out JsonElement description) ? description.GetString() ?? string.Empty : string.Empty);
}

public enum TreasureEntryKind { Equipment, Consumable, Buff }
public sealed record TreasureEntryAuthoring(TreasureEntryKind Kind, string ContentId, int Weight);

public sealed class TreasureAuthoringDocument : IAuthoringDocument
{
    public TreasureAuthoringDocument(string contentId, int goldMinimum, int goldMaximum,
        IEnumerable<TreasureEntryAuthoring> entries, AuthoringGraphLayout? graphLayout = null)
    {
        ContentId = string.IsNullOrWhiteSpace(contentId) ? throw new ArgumentException("ContentId is required.") : contentId;
        GoldMinimum = goldMinimum; GoldMaximum = goldMaximum;
        Entries = Array.AsReadOnly((entries ?? throw new ArgumentNullException(nameof(entries))).ToArray());
        GraphLayout = graphLayout ?? new AuthoringGraphLayout();
        _ = ToCoreDefinition();
        if (Entries.GroupBy(value => (value.Kind, value.ContentId)).Any(group => group.Count() > 1))
            throw new ArgumentException("Treasure entries must be unique within each table.");
        string[] layoutNodeIds = ["treasure:root", "treasure:gold", "treasure:equipment", "treasure:consumable", "treasure:buff"];
        if (GraphLayout.Nodes.Any(value => !layoutNodeIds.Contains(value.NodeId, StringComparer.Ordinal)))
            throw new ArgumentException("Treasure graph layout contains an unknown stable node identity.");
    }

    public string ContentId { get; }
    public int SchemaVersion => 2;
    public int GoldMinimum { get; }
    public int GoldMaximum { get; }
    public IReadOnlyList<TreasureEntryAuthoring> Entries { get; }
    public AuthoringGraphLayout GraphLayout { get; }
    public IReadOnlyList<string> Dependencies => Array.AsReadOnly(Entries.Select(value => value.ContentId)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());

    public PureRunTreasureDefinition ToCoreDefinition()
    {
        var value = new PureRunTreasureDefinition(new ContentId(ContentId), GoldMinimum, GoldMaximum,
            Build(TreasureEntryKind.Equipment), Build(TreasureEntryKind.Consumable), Build(TreasureEntryKind.Buff));
        value.Validate();
        return value;
    }

    public void WriteCanonical(Utf8JsonWriter writer)
    {
        writer.WriteStartObject(); writer.WriteString("contentId", ContentId); writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteNumber("goldMinimum", GoldMinimum); writer.WriteNumber("goldMaximum", GoldMaximum);
        writer.WriteStartArray("entries");
        foreach (TreasureEntryAuthoring entry in Entries.OrderBy(value => value.Kind).ThenBy(value => value.ContentId, StringComparer.Ordinal))
        { writer.WriteStartObject(); writer.WriteString("kind", entry.Kind.ToString()); writer.WriteString("contentId", entry.ContentId); writer.WriteNumber("weight", entry.Weight); writer.WriteEndObject(); }
        writer.WriteEndArray();
        writer.WritePropertyName("graphLayout"); GraphLayout.WriteCanonical(writer);
        writer.WriteEndObject();
    }

    private WeightedContentDefinition[] Build(TreasureEntryKind kind) => Entries.Where(value => value.Kind == kind)
        .Select(value => new WeightedContentDefinition(new ContentId(value.ContentId), value.Weight)).ToArray();
}

public static class TreasureAuthoringJson
{
    public static string Serialize(TreasureAuthoringDocument document)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            document.WriteCanonical(writer);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static TreasureAuthoringDocument Deserialize(string json)
    {
        using JsonDocument payload = JsonDocument.Parse(json);
        JsonElement root = payload.RootElement;
        return new TreasureAuthoringDocument(root.GetProperty("contentId").GetString()!,
            root.GetProperty("goldMinimum").GetInt32(), root.GetProperty("goldMaximum").GetInt32(),
            root.GetProperty("entries").EnumerateArray().Select(value => new TreasureEntryAuthoring(
                Enum.Parse<TreasureEntryKind>(value.GetProperty("kind").GetString()!),
                value.GetProperty("contentId").GetString()!, value.GetProperty("weight").GetInt32())),
            root.TryGetProperty("graphLayout", out JsonElement layout) ? AuthoringGraphLayout.Read(layout) : null);
    }
}
