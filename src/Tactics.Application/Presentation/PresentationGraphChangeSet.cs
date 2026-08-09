namespace Tactics.Application.Presentation;

/// <summary>
/// Base type for allow-listed presentation graph mutations.
/// </summary>
public abstract record PresentationGraphOperation;

/// <summary>
/// Changes one stable node's enabled state without exposing arbitrary serialized properties.
/// </summary>
public sealed record SetPresentationNodeEnabledOperation(string NodeId, bool Enabled)
    : PresentationGraphOperation;

/// <summary>
/// Moves one stable node in authoring space without exposing engine-specific vector types.
/// </summary>
public sealed record SetPresentationNodePositionOperation(
    string NodeId,
    PresentationNodePosition Position) : PresentationGraphOperation;

/// <summary>
/// Typed, revision-fenced group of presentation graph operations applied atomically.
/// </summary>
public sealed class PresentationGraphChangeSet
{
    public PresentationGraphChangeSet(
        string changeId,
        string expectedRevision,
        IEnumerable<PresentationGraphOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (string.IsNullOrWhiteSpace(changeId))
            throw new ArgumentException("ChangeSet requires a stable change ID.", nameof(changeId));
        if (string.IsNullOrWhiteSpace(expectedRevision))
            throw new ArgumentException("ChangeSet requires an expected revision.", nameof(expectedRevision));

        PresentationGraphOperation[] operationArray = operations.ToArray();
        if (operationArray.Length == 0)
            throw new ArgumentException("ChangeSet requires at least one operation.", nameof(operations));
        ChangeId = changeId.Trim();
        ExpectedRevision = expectedRevision.Trim();
        Operations = Array.AsReadOnly(operationArray);
    }

    public string ChangeId { get; }
    public string ExpectedRevision { get; }
    public IReadOnlyList<PresentationGraphOperation> Operations { get; }
}

public sealed record PresentationMutationDiagnostic(string Code, string Message);

/// <summary>
/// Complete result of an atomic presentation graph mutation attempt.
/// </summary>
public sealed record PresentationGraphMutationResult(
    PresentationGraphDocument Document,
    bool Succeeded,
    bool Changed,
    IReadOnlyList<PresentationMutationDiagnostic> Diagnostics);
