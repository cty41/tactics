using System.Text.Json;

namespace Tactics.Application.Runs;

public sealed record RunSaveDecodeResultV5(bool Succeeded, string? ErrorCode, PureRunSaveSnapshot? Snapshot, int MigratedFromSchema);

/// <summary>V5 adds resumable new-run starting-skill selection without changing run gameplay state.</summary>
public static class RunSaveDocumentV5
{
    public const int SchemaVersion = 5;

    public static string Encode(PureRunSaveSnapshot snapshot) => ReplaceSchema(RunSaveDocumentV4.Encode(snapshot), SchemaVersion) + "\n";

    public static RunSaveDecodeResultV5 Decode(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new(false, "save.empty_document", null, 0);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("schemaVersion", out JsonElement schema))
                return new(false, "save.unsupported_schema", null, 0);
            int version = schema.GetInt32();
            if (version is < 1 or > SchemaVersion) return new(false, "save.unsupported_schema", null, 0);
            RunSaveDecodeResultV4 decoded = RunSaveDocumentV4.Decode(version == SchemaVersion ? ReplaceSchema(json, 4) : json);
            return decoded.Succeeded
                ? new(true, null, decoded.Snapshot, version < SchemaVersion ? version : 0)
                : new(false, decoded.ErrorCode, null, version < SchemaVersion ? version : 0);
        }
        catch (JsonException) { return new(false, "save.invalid_json", null, 0); }
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
