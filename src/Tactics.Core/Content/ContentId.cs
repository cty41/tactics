namespace Tactics.Core.Content;

public readonly record struct ContentId
{
    public ContentId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ContentId cannot be empty.", nameof(value));

        string normalized = value.Trim();
        if (!IsValid(normalized))
        {
            throw new ArgumentException(
                "ContentId must be a strict lowercase business ID containing only lowercase letters, digits, '.' and '-' separators.",
                nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;

    private static bool IsValid(string value)
    {
        bool previousWasSeparator = true;
        foreach (char character in value)
        {
            bool isSeparator = character is '.' or '-';
            bool isAllowed = character is >= 'a' and <= 'z' || character is >= '0' and <= '9' || isSeparator;
            if (!isAllowed || isSeparator && previousWasSeparator)
                return false;

            previousWasSeparator = isSeparator;
        }

        return !previousWasSeparator;
    }
}
