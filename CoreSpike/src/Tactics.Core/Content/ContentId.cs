namespace Tactics.Core.Content;

/// <summary>
/// Stable project-level content identity. Engine-specific GUIDs, paths, and UIDs
/// are migration metadata and must not be stored in this value.
/// </summary>
public readonly record struct ContentId
{
    public ContentId(string value)
    {
        if (!TryNormalize(value, out var normalized))
            throw new ArgumentException("ContentId must contain a non-empty value.", nameof(value));

        Value = normalized;
    }

    public string Value { get; }

    public static bool TryCreate(string? value, out ContentId contentId)
    {
        if (TryNormalize(value, out var normalized))
        {
            contentId = new ContentId(normalized, normalizedAlreadyValidated: true);
            return true;
        }

        contentId = default;
        return false;
    }

    public override string ToString() => Value;

    private ContentId(string normalizedValue, bool normalizedAlreadyValidated)
    {
        _ = normalizedAlreadyValidated;
        Value = normalizedValue;
    }

    private static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length > 0;
    }
}
