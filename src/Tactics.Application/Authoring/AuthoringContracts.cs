using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Tactics.Application.Authoring;

public enum AuthoringDocumentKind
{
    Map,
    Event,
    Treasure,
    Encounter,
    BattleLayout,
    Ai,
    Skill,
    Presentation
}

public enum AuthoringDiagnosticSeverity { Info, Warning, Error }

public sealed record AuthoringDiagnostic(
    string Code,
    AuthoringDiagnosticSeverity Severity,
    string Message,
    string? Path = null);

public interface IAuthoringDocument
{
    string ContentId { get; }
    int SchemaVersion { get; }
    IReadOnlyList<string> Dependencies { get; }
    void WriteCanonical(Utf8JsonWriter writer);
}

public static class AuthoringRevision
{
    public static string Compute(IAuthoringDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            document.WriteCanonical(writer);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    public static string ComputeStrings(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var canonical = new StringBuilder();
        foreach (string value in values.Order(StringComparer.Ordinal))
            canonical.Append(value.Length).Append(':').Append(value).Append(';');
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }
}

public sealed class AuthoringDocumentEnvelope
{
    public AuthoringDocumentEnvelope(
        AuthoringDocumentKind kind,
        IAuthoringDocument document,
        IEnumerable<AuthoringDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(document.ContentId))
            throw new ArgumentException("Authoring documents require a ContentId.", nameof(document));
        if (document.SchemaVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(document));
        Kind = kind;
        Document = document;
        ContentId = document.ContentId;
        SchemaVersion = document.SchemaVersion;
        Revision = AuthoringRevision.Compute(document);
        Dependencies = Array.AsReadOnly(document.Dependencies.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
        Diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<AuthoringDiagnostic>()).ToArray());
    }

    public AuthoringDocumentKind Kind { get; }
    public string ContentId { get; }
    public int SchemaVersion { get; }
    public string Revision { get; }
    public IReadOnlyList<string> Dependencies { get; }
    public IAuthoringDocument Document { get; }
    public IReadOnlyList<AuthoringDiagnostic> Diagnostics { get; }
}

public abstract record AuthoringOperation;

public enum AuthoringAssetChangeKind { Create, Duplicate, Delete, Rebind }

public sealed record AuthoringAssetChange(
    AuthoringAssetChangeKind Kind,
    string ContentId,
    string? SourceContentId = null,
    string? ResourceType = null,
    string? Path = null,
    string? ExpectedReferenceRevision = null);

public sealed class AuthoringChangeSet
{
    public AuthoringChangeSet(
        string changeId,
        AuthoringDocumentKind kind,
        string contentId,
        string expectedRevision,
        IEnumerable<AuthoringOperation> operations,
        IEnumerable<AuthoringAssetChange>? assetChanges = null,
        string? expectedReferenceRevision = null)
    {
        if (string.IsNullOrWhiteSpace(changeId)) throw new ArgumentException("ChangeId is required.", nameof(changeId));
        if (string.IsNullOrWhiteSpace(contentId)) throw new ArgumentException("ContentId is required.", nameof(contentId));
        if (string.IsNullOrWhiteSpace(expectedRevision)) throw new ArgumentException("ExpectedRevision is required.", nameof(expectedRevision));
        ArgumentNullException.ThrowIfNull(operations);
        AuthoringOperation[] operationArray = operations.ToArray();
        AuthoringAssetChange[] assetChangeArray = (assetChanges ?? Array.Empty<AuthoringAssetChange>()).ToArray();
        if (operationArray.Length == 0 && assetChangeArray.Length == 0)
            throw new ArgumentException("A ChangeSet requires operations or asset changes.", nameof(operations));
        ChangeId = changeId;
        Kind = kind;
        ContentId = contentId;
        ExpectedRevision = expectedRevision;
        Operations = Array.AsReadOnly(operationArray);
        AssetChanges = Array.AsReadOnly(assetChangeArray);
        ExpectedReferenceRevision = expectedReferenceRevision;
    }

    public string ChangeId { get; }
    public AuthoringDocumentKind Kind { get; }
    public string ContentId { get; }
    public string ExpectedRevision { get; }
    public IReadOnlyList<AuthoringOperation> Operations { get; }
    public IReadOnlyList<AuthoringAssetChange> AssetChanges { get; }
    public string? ExpectedReferenceRevision { get; }
}

public sealed record AuthoringValidationResult(
    bool Succeeded,
    string CurrentRevision,
    string PredictedRevision,
    IReadOnlyList<AuthoringDiagnostic> Diagnostics,
    AuthoringReferenceSnapshot? ReferenceImpact = null,
    bool PreviewAvailable = false);

public sealed record AuthoringApplyResult(
    bool Succeeded,
    string PreviousRevision,
    string NewRevision,
    IReadOnlyList<string> CreatedContentIds,
    IReadOnlyList<string> ModifiedContentIds,
    IReadOnlyList<string> DeletedContentIds,
    IReadOnlyList<AuthoringDiagnostic> Diagnostics,
    IReadOnlyList<AuthoringSaveEvidence>? SaveEvidence = null);

public enum AuthoringResourceOwnership { Protected, WorkbenchOwned, Tombstoned }

public sealed record AuthoringSaveEvidence(
    string ContentId,
    string ResourcePath,
    string ResourceUid,
    string Revision,
    bool ReloadValidated);

public sealed record AuthoringPreviewEvidence(
    string Kind,
    string Summary,
    IReadOnlyDictionary<string, string> Values);

public static class AuthoringPreviewCompiler
{
    public static AuthoringPreviewEvidence Compile(IAuthoringDocument document, int seed = 0)
    {
        ArgumentNullException.ThrowIfNull(document);
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["contentId"] = document.ContentId,
            ["revision"] = AuthoringRevision.Compute(document),
            ["seed"] = seed.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        return document switch
        {
            EventAuthoringDocument value => Event(value, values),
            TreasureAuthoringDocument value => Treasure(value, values),
            EncounterAuthoringDocument value => Encounter(value, values),
            BattleLayoutAuthoringDocument value => Layout(value, values),
            AiAuthoringDocument value => Ai(value, values),
            SkillAuthoringDocument value => Skill(value, values),
            PresentationProfileAuthoringDocument value => Presentation(value, values),
            _ => new AuthoringPreviewEvidence("validation", "This authoring kind has no domain preview.", values)
        };
    }

    private static AuthoringPreviewEvidence Event(EventAuthoringDocument document, SortedDictionary<string, string> values)
    {
        values["options"] = document.Options.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        values["successRates"] = string.Join(",", document.Options.Select(value => $"{value.OptionId}:{value.BaseSuccessRate}"));
        int seed = int.Parse(values["seed"], System.Globalization.CultureInfo.InvariantCulture);
        values["rolls"] = string.Join(",", document.Options.Select((value, index) =>
        {
            int roll = DeterministicIndex(document.ContentId + ":event:" + index, seed, 100) + 1;
            return $"{value.OptionId}:{roll}:{(roll <= value.BaseSuccessRate ? "success" : "failure")}";
        }));
        return new AuthoringPreviewEvidence("event", "Compiled deterministic event outcome table.", values);
    }

    private static AuthoringPreviewEvidence Treasure(TreasureAuthoringDocument document, SortedDictionary<string, string> values)
    {
        foreach (TreasureEntryKind kind in Enum.GetValues<TreasureEntryKind>())
        {
            TreasureEntryAuthoring[] entries = document.Entries.Where(value => value.Kind == kind).ToArray();
            int total = entries.Sum(value => value.Weight);
            values[kind.ToString()] = total == 0 ? "empty" : string.Join(",", entries.Select(value =>
                $"{value.ContentId}:{value.Weight * 100d / total:0.###}%"));
            if (total > 0)
            {
                int cursor = DeterministicIndex(document.ContentId + ":" + kind, int.Parse(values["seed"], System.Globalization.CultureInfo.InvariantCulture), total);
                values[kind + "Sample"] = entries.First(value => (cursor -= value.Weight) < 0).ContentId;
            }
        }
        values["gold"] = $"{document.GoldMinimum}-{document.GoldMaximum}";
        return new AuthoringPreviewEvidence("treasure", "Compiled exact treasure probabilities.", values);
    }

    private static AuthoringPreviewEvidence Encounter(EncounterAuthoringDocument document, SortedDictionary<string, string> values)
    {
        _ = document.ToCoreDefinition();
        values["layout"] = document.LayoutContentId;
        values["monsters"] = document.MonsterUnitContentIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new AuthoringPreviewEvidence("encounter", "Compiled encounter bindings for fixture preview.", values);
    }

    private static AuthoringPreviewEvidence Layout(BattleLayoutAuthoringDocument document, SortedDictionary<string, string> values)
    {
        _ = document.ToCoreDefinition();
        values["playerSpawns"] = document.PartySpawns.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        values["enemySpawns"] = document.EnemySpawns.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        values["blocked"] = document.BlockedCells.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new AuthoringPreviewEvidence("battle-layout", "Compiled 10x10 battle layout occupancy.", values);
    }

    private static AuthoringPreviewEvidence Ai(AiAuthoringDocument document, SortedDictionary<string, string> values)
    {
        _ = document.ToCoreDefinition();
        values["nodes"] = document.Nodes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        values["edges"] = document.Edges.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new AuthoringPreviewEvidence("ai", "Compiled AI definition for fixed-seed encounter preview.", values);
    }

    private static AuthoringPreviewEvidence Skill(SkillAuthoringDocument document, SortedDictionary<string, string> values)
    {
        values["executionKind"] = document.Definition.ExecutionKind.ToString();
        values["dependencies"] = string.Join(",", document.Dependencies);
        return new AuthoringPreviewEvidence("skill", "Compiled SkillDefinition for encounter legality preview.", values);
    }

    private static AuthoringPreviewEvidence Presentation(PresentationProfileAuthoringDocument document, SortedDictionary<string, string> values)
    {
        values["resourceKind"] = document.ResourceClass;
        values["propertyCount"] = document.Properties.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new AuthoringPreviewEvidence("presentation", "Compiled Godot-native presentation profile.", values);
    }

    private static int DeterministicIndex(string scope, int seed, int count)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(scope + ":" + seed.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return (int)(BitConverter.ToUInt32(hash, 0) % (uint)count);
    }
}

public sealed record AuthoringDocumentChange(
    AuthoringDocumentKind Kind,
    string ContentId,
    string ExpectedRevision,
    string Snapshot);

public sealed class AuthoringBatchChangeSet
{
    public AuthoringBatchChangeSet(
        string changeId,
        IEnumerable<AuthoringDocumentChange> documentChanges,
        IEnumerable<AuthoringAssetChange>? assetChanges = null,
        string? expectedReferenceRevision = null)
    {
        if (string.IsNullOrWhiteSpace(changeId)) throw new ArgumentException("ChangeId is required.", nameof(changeId));
        DocumentChanges = Array.AsReadOnly((documentChanges ?? throw new ArgumentNullException(nameof(documentChanges))).ToArray());
        AssetChanges = Array.AsReadOnly((assetChanges ?? Array.Empty<AuthoringAssetChange>()).ToArray());
        if (DocumentChanges.Count == 0 && AssetChanges.Count == 0)
            throw new ArgumentException("A batch requires document or asset changes.", nameof(documentChanges));
        if (DocumentChanges.GroupBy(value => (value.Kind, value.ContentId)).Any(group => group.Count() > 1))
            throw new ArgumentException("A batch cannot contain duplicate document identities.", nameof(documentChanges));
        if (AssetChanges.GroupBy(value => value.ContentId, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new ArgumentException("A batch cannot contain multiple lifecycle operations for the same ContentId.", nameof(assetChanges));
        foreach (AuthoringAssetChange assetChange in AssetChanges)
        {
            if (string.IsNullOrWhiteSpace(assetChange.ContentId))
                throw new ArgumentException("Lifecycle operations require a ContentId.", nameof(assetChanges));
            if (assetChange.Kind == AuthoringAssetChangeKind.Duplicate && string.IsNullOrWhiteSpace(assetChange.SourceContentId))
                throw new ArgumentException("Duplicate requires SourceContentId.", nameof(assetChanges));
            if (assetChange.Kind == AuthoringAssetChangeKind.Delete && string.IsNullOrWhiteSpace(assetChange.ExpectedReferenceRevision))
                throw new ArgumentException("Delete requires ExpectedReferenceRevision.", nameof(assetChanges));
            if (assetChange.Kind == AuthoringAssetChangeKind.Rebind)
                throw new ArgumentException("Rebind must be represented by typed document changes in the same batch.", nameof(assetChanges));
        }
        ChangeId = changeId.Trim();
        ExpectedReferenceRevision = expectedReferenceRevision;
    }

    public string ChangeId { get; }
    public IReadOnlyList<AuthoringDocumentChange> DocumentChanges { get; }
    public IReadOnlyList<AuthoringAssetChange> AssetChanges { get; }
    public string? ExpectedReferenceRevision { get; }
}
