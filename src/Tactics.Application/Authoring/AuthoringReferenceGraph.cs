namespace Tactics.Application.Authoring;

public sealed record AuthoringReferenceSnapshot(
    string ContentId,
    IReadOnlyList<string> ForwardReferences,
    IReadOnlyList<string> ReverseReferences,
    string Revision);

public sealed class AuthoringReferenceGraph
{
    private readonly IReadOnlyDictionary<string, AuthoringDocumentEnvelope> _documents;

    public AuthoringReferenceGraph(IEnumerable<AuthoringDocumentEnvelope> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        AuthoringDocumentEnvelope[] values = documents.ToArray();
        if (values.Select(value => value.ContentId).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException("Authoring document ContentIds must be unique.", nameof(documents));
        _documents = values.ToDictionary(value => value.ContentId, StringComparer.Ordinal);
    }

    public AuthoringReferenceSnapshot Capture(string contentId)
    {
        if (!_documents.TryGetValue(contentId, out AuthoringDocumentEnvelope? document))
            throw new KeyNotFoundException($"Unknown authoring document '{contentId}'.");
        string[] forward = document.Dependencies.Order(StringComparer.Ordinal).ToArray();
        string[] reverse = _documents.Values
            .Where(value => value.Dependencies.Contains(contentId, StringComparer.Ordinal))
            .Select(value => value.ContentId).Order(StringComparer.Ordinal).ToArray();
        string revision = AuthoringRevision.ComputeStrings(
            new[] { contentId, document.Revision }
                .Concat(forward.Select(value => "f:" + value))
                .Concat(reverse.Select(value => "r:" + value)));
        return new AuthoringReferenceSnapshot(
            contentId,
            Array.AsReadOnly(forward),
            Array.AsReadOnly(reverse),
            revision);
    }

    public AuthoringValidationResult ValidateDelete(
        string contentId,
        string expectedReferenceRevision,
        IEnumerable<AuthoringDocumentEnvelope>? prospectiveDocuments = null)
    {
        AuthoringReferenceSnapshot snapshot = Capture(contentId);
        var diagnostics = new List<AuthoringDiagnostic>();
        if (!string.Equals(snapshot.Revision, expectedReferenceRevision, StringComparison.Ordinal))
        {
            diagnostics.Add(new AuthoringDiagnostic(
                "authoring.reference_revision_conflict",
                AuthoringDiagnosticSeverity.Error,
                $"Expected reference revision '{expectedReferenceRevision}', actual '{snapshot.Revision}'."));
        }

        var prospective = _documents.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (AuthoringDocumentEnvelope replacement in prospectiveDocuments ?? Array.Empty<AuthoringDocumentEnvelope>())
        {
            if (string.Equals(replacement.ContentId, contentId, StringComparison.Ordinal))
            {
                diagnostics.Add(new AuthoringDiagnostic(
                    "authoring.delete_target_modified",
                    AuthoringDiagnosticSeverity.Error,
                    $"Delete target '{contentId}' cannot also be modified by the same transaction."));
                continue;
            }

            prospective[replacement.ContentId] = replacement;
        }

        prospective.Remove(contentId);
        string[] blockers = prospective.Values
            .Where(value => value.Dependencies.Contains(contentId, StringComparer.Ordinal))
            .Select(value => value.ContentId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (blockers.Length > 0)
        {
            diagnostics.Add(new AuthoringDiagnostic(
                "authoring.delete_referenced",
                AuthoringDiagnosticSeverity.Error,
                $"'{contentId}' is still referenced by: {string.Join(", ", blockers)}."));
        }
        bool succeeded = diagnostics.All(value => value.Severity != AuthoringDiagnosticSeverity.Error);
        return new AuthoringValidationResult(
            succeeded,
            snapshot.Revision,
            snapshot.Revision,
            Array.AsReadOnly(diagnostics.ToArray()),
            snapshot);
    }
}
