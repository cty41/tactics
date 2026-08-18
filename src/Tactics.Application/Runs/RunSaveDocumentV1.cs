using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tactics.Core.Content;
using Tactics.Core.Board;
using Tactics.Core.Items;
using Tactics.Core.Units;

namespace Tactics.Application.Runs;

public sealed record RunSaveEnvelopeV1(
    string FormatId,
    int SchemaVersion,
    long Revision,
    string PayloadSha256,
    PureRunSaveSnapshot Payload);

public sealed record RunSaveDecodeResult(
    bool Succeeded,
    string? ErrorCode,
    RunSaveEnvelopeV1? Envelope);

public static class RunSaveDocumentV1
{
    public const string FormatId = "tactics-pure-run-save";
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions PayloadOptions = CreateOptions(indented: false);
    private static readonly JsonSerializerOptions EnvelopeOptions = CreateOptions(indented: true);

    public static string Encode(PureRunSaveSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        PureRunSaveSnapshot normalized = RunSaveNormalizer.Normalize(snapshot);
        string payload = JsonSerializer.Serialize(normalized, PayloadOptions);
        string hash = Sha256(payload);
        using JsonDocument payloadDocument = JsonDocument.Parse(payload);
        var wire = new RunSaveWireEnvelope(
            FormatId, SchemaVersion, normalized.Revision, hash, payloadDocument.RootElement.Clone());
        return JsonSerializer.Serialize(wire, EnvelopeOptions).ReplaceLineEndings("\n") + "\n";
    }

    public static RunSaveDecodeResult Decode(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Fail("save.empty_document");
        try
        {
            RunSaveWireEnvelope? wire = JsonSerializer.Deserialize<RunSaveWireEnvelope>(json, EnvelopeOptions);
            if (wire is null || wire.FormatId != FormatId)
                return Fail("save.invalid_format");
            if (wire.SchemaVersion != SchemaVersion)
                return Fail("save.unsupported_schema");
            string canonicalWirePayload = JsonSerializer.Serialize(wire.Payload, PayloadOptions);
            if (!FixedEquals(wire.PayloadSha256, Sha256(canonicalWirePayload)))
                return Fail("save.payload_hash_mismatch");
            PureRunSaveSnapshot? payload = wire.Payload.Deserialize<PureRunSaveSnapshot>(PayloadOptions);
            if (payload is null || payload.Revision != wire.Revision)
                return Fail("save.revision_mismatch");
            PureRunSaveSnapshot normalized = RunSaveNormalizer.Normalize(payload);
            return new RunSaveDecodeResult(true, null,
                new RunSaveEnvelopeV1(wire.FormatId, wire.SchemaVersion, wire.Revision, wire.PayloadSha256, normalized));
        }
        catch (JsonException)
        {
            return Fail("save.invalid_json");
        }
        catch (ArgumentException)
        {
            return Fail("save.invalid_payload");
        }
    }

    private static JsonSerializerOptions CreateOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new ContentIdJsonConverter());
        options.Converters.Add(new ItemInstanceIdJsonConverter());
        options.Converters.Add(new UnitAttributesJsonConverter());
        options.Converters.Add(new GridPointJsonConverter());
        return options;
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool FixedEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.ASCII.GetBytes(left ?? string.Empty);
        byte[] rightBytes = Encoding.ASCII.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static RunSaveDecodeResult Fail(string code) => new(false, code, null);

    private sealed record RunSaveWireEnvelope(
        string FormatId,
        int SchemaVersion,
        long Revision,
        string PayloadSha256,
        JsonElement Payload);

    private sealed class ContentIdJsonConverter : JsonConverter<ContentId>
    {
        public override ContentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("ContentId cannot be null."));

        public override void Write(Utf8JsonWriter writer, ContentId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }

    private sealed class ItemInstanceIdJsonConverter : JsonConverter<ItemInstanceId>
    {
        public override ItemInstanceId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("ItemInstanceId cannot be null."));

        public override void Write(Utf8JsonWriter writer, ItemInstanceId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }

    private sealed class UnitAttributesJsonConverter : JsonConverter<UnitAttributes>
    {
        public override UnitAttributes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            string[] names = { "strength", "agility", "constitution", "intelligence", "charisma", "luck" };
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != names.Length ||
                names.Any(name => !root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out _)))
                throw new JsonException("UnitAttributes must contain exactly six integer fields.");
            return new UnitAttributes(root.GetProperty("strength").GetInt32(), root.GetProperty("agility").GetInt32(),
                root.GetProperty("constitution").GetInt32(), root.GetProperty("intelligence").GetInt32(),
                root.GetProperty("charisma").GetInt32(), root.GetProperty("luck").GetInt32());
        }

        public override void Write(Utf8JsonWriter writer, UnitAttributes value, JsonSerializerOptions options)
        {
            writer.WriteStartObject(); writer.WriteNumber("strength", value.Strength); writer.WriteNumber("agility", value.Agility);
            writer.WriteNumber("constitution", value.Constitution); writer.WriteNumber("intelligence", value.Intelligence);
            writer.WriteNumber("charisma", value.Charisma); writer.WriteNumber("luck", value.Luck); writer.WriteEndObject();
        }
    }

    private sealed class GridPointJsonConverter : JsonConverter<GridPoint>
    {
        public override GridPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
                !root.TryGetProperty("x", out JsonElement x) || !x.TryGetInt32(out int xValue) ||
                !root.TryGetProperty("y", out JsonElement y) || !y.TryGetInt32(out int yValue))
                throw new JsonException("GridPoint must contain exactly x and y integers.");
            return new GridPoint(xValue, yValue);
        }

        public override void Write(Utf8JsonWriter writer, GridPoint value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", value.X);
            writer.WriteNumber("y", value.Y);
            writer.WriteEndObject();
        }
    }
}
