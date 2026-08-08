namespace Tactics.Core.Content;

public readonly record struct ContentId
{
    public ContentId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ContentId cannot be empty.", nameof(value));

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
