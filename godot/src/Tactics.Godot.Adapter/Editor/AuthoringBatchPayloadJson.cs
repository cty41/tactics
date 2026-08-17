#if TOOLS
using System.Text.Json;
using System.Text.Json.Nodes;
using Tactics.Application.Authoring;

namespace Tactics.Godot.Adapter.Editor;

internal sealed record AuthoringBatchPayload(string ChangeId, AuthoringDocumentChange[] Changes,
    AuthoringAssetChange[] Lifecycle);

internal static class AuthoringBatchPayloadJson
{
    public static string Serialize(AuthoringBatchPayload payload) => new JsonObject
    {
        ["changeId"] = payload.ChangeId,
        ["changes"] = new JsonArray(payload.Changes.Select(ChangeJson).ToArray()),
        ["lifecycle"] = new JsonArray(payload.Lifecycle.Select(LifecycleJson).ToArray())
    }.ToJsonString();

    public static AuthoringBatchPayload Deserialize(string payload)
    {
        using JsonDocument parsed = JsonDocument.Parse(payload);
        JsonElement root = parsed.RootElement;
        return new AuthoringBatchPayload(
            root.GetProperty("changeId").GetString() ?? throw new InvalidOperationException("Batch ChangeId is missing."),
            root.GetProperty("changes").EnumerateArray().Select(ReadChange).ToArray(),
            root.GetProperty("lifecycle").EnumerateArray().Select(ReadLifecycle).ToArray());
    }

    private static JsonNode ChangeJson(AuthoringDocumentChange value) => new JsonObject
    {
        ["kind"] = value.Kind.ToString(), ["contentId"] = value.ContentId,
        ["expectedRevision"] = value.ExpectedRevision, ["snapshot"] = value.Snapshot
    };

    private static JsonNode LifecycleJson(AuthoringAssetChange value) => new JsonObject
    {
        ["kind"] = value.Kind.ToString(), ["contentId"] = value.ContentId,
        ["sourceContentId"] = value.SourceContentId, ["resourceType"] = value.ResourceType,
        ["path"] = value.Path, ["expectedReferenceRevision"] = value.ExpectedReferenceRevision
    };

    private static AuthoringDocumentChange ReadChange(JsonElement value) => new(
        Enum.Parse<AuthoringDocumentKind>(value.GetProperty("kind").GetString()!, true),
        value.GetProperty("contentId").GetString()!, value.GetProperty("expectedRevision").GetString()!,
        value.GetProperty("snapshot").GetString()!);

    private static AuthoringAssetChange ReadLifecycle(JsonElement value) => new(
        Enum.Parse<AuthoringAssetChangeKind>(value.GetProperty("kind").GetString()!, true),
        value.GetProperty("contentId").GetString()!, Optional(value, "sourceContentId"),
        Optional(value, "resourceType"), Optional(value, "path"), Optional(value, "expectedReferenceRevision"));

    private static string? Optional(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
#endif
