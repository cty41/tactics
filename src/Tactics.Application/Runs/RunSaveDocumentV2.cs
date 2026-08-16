using System.Text.Json;

namespace Tactics.Application.Runs;

public sealed record RunSaveDecodeResultV2(bool Succeeded, string? ErrorCode, PureRunSaveSnapshot? Snapshot, bool MigratedFromV1);

/// <summary>V2 save envelope. The payload gains explicit learned-skill levels through RunCharacterState.</summary>
public static class RunSaveDocumentV2
{
    public const int SchemaVersion = 2;

    public static string Encode(PureRunSaveSnapshot snapshot)
    {
        string v1 = RunSaveDocumentV1.Encode(snapshot);
        using JsonDocument document = JsonDocument.Parse(v1);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("schemaVersion")) writer.WriteNumber(property.Name, SchemaVersion);
                else property.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n") + "\n";
    }

    public static RunSaveDecodeResultV2 Decode(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new(false, "save.empty_document", null, false);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("schemaVersion", out JsonElement schema)) return new(false, "save.unsupported_schema", null, false);
            bool migrated = schema.GetInt32() == 1;
            if (!migrated && schema.GetInt32() != SchemaVersion) return new(false, "save.unsupported_schema", null, false);
            string compatible = migrated ? json : ReplaceSchema(json, 1);
            RunSaveDecodeResult decoded = RunSaveDocumentV1.Decode(compatible);
            return decoded.Succeeded ? new(true, null, decoded.Envelope!.Payload, migrated) : new(false, decoded.ErrorCode, null, migrated);
        }
        catch (JsonException) { return new(false, "save.invalid_json", null, false); }
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
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
