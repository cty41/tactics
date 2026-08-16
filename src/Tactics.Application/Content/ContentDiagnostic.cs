using Tactics.Core.Content;

namespace Tactics.Application.Content;

public enum ContentDiagnosticSeverity
{
    Warning,
    Error
}

public sealed record ContentDiagnostic(
    string Code,
    ContentDiagnosticSeverity Severity,
    string Message,
    ContentId? ContentId = null);
