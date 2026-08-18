using System.Text.Json;

namespace Tactics.Application.Runs;

public sealed record RunSaveDecodeResultV9(bool Succeeded, string? ErrorCode, PureRunSaveSnapshot? Snapshot,
    int MigratedFromSchema, bool RequiresNewRun);

/// <summary>V9 makes the complete tile-adventure state authoritative and save-backed.</summary>
public static class RunSaveDocumentV9
{
    public const int SchemaVersion = 9;

    public static string Encode(PureRunSaveSnapshot snapshot) =>
        ReplaceSchema(RunSaveDocumentV8.Encode(snapshot), SchemaVersion) + "\n";

    public static RunSaveDecodeResultV9 Decode(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new(false, "save.empty_document", null, 0, false);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("schemaVersion", out JsonElement schema))
                return new(false, "save.unsupported_schema", null, 0, false);
            int version = schema.GetInt32();
            if (version is < 1 or > SchemaVersion) return new(false, "save.unsupported_schema", null, 0, false);
            RunSaveDecodeResultV8 decoded = RunSaveDocumentV8.Decode(
                version == SchemaVersion ? ReplaceSchema(json, RunSaveDocumentV8.SchemaVersion) : json);
            if (!decoded.Succeeded || decoded.Snapshot is null)
                return new(false, decoded.ErrorCode, null, version < SchemaVersion ? version : 0, false);
            bool requiresNewRun = version < SchemaVersion &&
                (decoded.Snapshot.ActiveRun is not null || decoded.Snapshot.PendingRunSetup is not null);
            PureRunSaveSnapshot snapshot = requiresNewRun
                ? decoded.Snapshot with { ActiveRun = null, PendingRunSetup = null }
                : decoded.Snapshot;
            return new(true, null, snapshot, version < SchemaVersion ? version : 0, requiresNewRun);
        }
        catch (JsonException)
        {
            return new(false, "save.invalid_json", null, 0, false);
        }
    }

    private static string ReplaceSchema(string json, int schema)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("schemaVersion")) writer.WriteNumber(property.Name, schema);
                else property.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).TrimEnd('\r', '\n');
    }
}
