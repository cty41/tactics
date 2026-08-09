#if TOOLS
using System.Text.Json;
using Tactics.Application.Content;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Editor;

internal sealed class PoisonSpearMigrationDraft
{
    public int SchemaVersion { get; init; }
    public string BatchId { get; init; } = string.Empty;
    public string Classification { get; init; } = string.Empty;
    public PoisonSpearDraftSource Source { get; init; } = new();
    public PoisonSpearDraftContent[] Contents { get; init; } = Array.Empty<PoisonSpearDraftContent>();

    public PoisonSpearDraftContent Get(string contentId) =>
        Contents.Single(item => string.Equals(item.ContentId, contentId, StringComparison.Ordinal));

    public ContentSnapshot CompileApplicationSnapshot()
    {
        ContentDraft[] drafts = Contents.Select(item => new ContentDraft(
            new ContentId(item.ContentId),
            item.ResourceTypeId,
            item.SchemaVersion,
            item.References.Select(value => new ContentId(value)),
            item.Properties.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.GetRawText(),
                StringComparer.Ordinal))).ToArray();
        ContentCompileResult result = new ContentCompiler().Compile(drafts);
        if (!result.Succeeded || result.Snapshot is null)
        {
            throw new InvalidOperationException(
                "Poison Spear typed draft failed Application compilation: " +
                string.Join("; ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        }

        return result.Snapshot;
    }

    public static PoisonSpearMigrationDraft Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Poison Spear typed migration draft is missing.", path);
        PoisonSpearMigrationDraft? draft = JsonSerializer.Deserialize<PoisonSpearMigrationDraft>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (draft is null || draft.SchemaVersion != 1 ||
            draft.Classification != "disposable_typed_migration_draft" ||
            draft.BatchId != "poison-spear-lv1-real")
        {
            throw new InvalidOperationException("Poison Spear typed migration draft identity is invalid.");
        }
        return draft;
    }
}

internal sealed class PoisonSpearDraftSource
{
    public string SourceTag { get; init; } = string.Empty;
    public string SourceCommit { get; init; } = string.Empty;
    public string ExporterVersion { get; init; } = string.Empty;
    public string ExportHash { get; init; } = string.Empty;
}

internal sealed class PoisonSpearDraftContent
{
    public string ContentId { get; init; } = string.Empty;
    public string ResourceTypeId { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public string[] References { get; init; } = Array.Empty<string>();
    public JsonElement Properties { get; init; }

    public JsonElement Property(string name)
    {
        if (!Properties.TryGetProperty(name, out JsonElement value))
            throw new InvalidOperationException($"Content '{ContentId}' is missing property '{name}'.");
        return value;
    }

    public string String(string name) => Property(name).GetString()
        ?? throw new InvalidOperationException($"Content '{ContentId}' property '{name}' is null.");

    public int Integer(string name)
    {
        double value = Property(name).GetDouble();
        if (value != Math.Truncate(value) || value < int.MinValue || value > int.MaxValue)
            throw new InvalidOperationException($"Content '{ContentId}' property '{name}' is not an integer.");
        return checked((int)value);
    }

    public float Single(string name) => Property(name).GetSingle();
    public bool Boolean(string name) => Property(name).GetBoolean();
}
#endif
