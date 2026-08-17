namespace Tactics.Application.Authoring;

public sealed class AuthoringSession<TDocument> where TDocument : class, IAuthoringDocument
{
    private TDocument _baseline;
    private string _baselineRevision;

    public AuthoringSession(AuthoringDocumentKind kind, TDocument document)
    {
        Kind = kind;
        _baseline = document ?? throw new ArgumentNullException(nameof(document));
        Draft = document;
        _baselineRevision = AuthoringRevision.Compute(document);
    }

    public AuthoringDocumentKind Kind { get; }
    public TDocument Baseline => _baseline;
    public TDocument Draft { get; private set; }
    public string ExpectedRevision => _baselineRevision;
    public string DraftRevision => AuthoringRevision.Compute(Draft);
    public bool IsDirty => !string.Equals(ExpectedRevision, DraftRevision, StringComparison.Ordinal);

    public void ReplaceDraft(TDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!string.Equals(document.ContentId, _baseline.ContentId, StringComparison.Ordinal))
            throw new InvalidOperationException("A session cannot change its ContentId.");
        Draft = document;
    }

    public bool HasExternalConflict(TDocument current) =>
        !string.Equals(_baselineRevision, AuthoringRevision.Compute(current), StringComparison.Ordinal);

    public void Revert() => Draft = _baseline;

    public void AcceptApplied(TDocument applied)
    {
        ArgumentNullException.ThrowIfNull(applied);
        if (!string.Equals(applied.ContentId, _baseline.ContentId, StringComparison.Ordinal))
            throw new InvalidOperationException("Applied document identity differs from the session identity.");
        _baseline = applied;
        Draft = applied;
        _baselineRevision = AuthoringRevision.Compute(applied);
    }
}
