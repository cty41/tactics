using Godot;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class GodotResourceEntry : Resource
{
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public string ResourceTypeIdValue { get; set; } = string.Empty;
    [Export] public string ResourceUidValue { get; set; } = string.Empty;
    [Export] public string DiagnosticPathValue { get; set; } = string.Empty;
    [Export] public int SchemaVersion { get; set; }
    [Export] public string[] ReferenceContentIds { get; set; } = Array.Empty<string>();

    public string ResourceLocator
    {
        get
        {
            // The path is part of the migration receipt and remains authoritative at runtime. A UID can
            // temporarily resolve to stale cache data while several headless ResourceSaver batches run in
            // one verifier pass, so it is retained for validation rather than used as the load locator.
            return !string.IsNullOrWhiteSpace(DiagnosticPathValue)
                ? DiagnosticPathValue
                : ResourceUidValue;
        }
    }
}
