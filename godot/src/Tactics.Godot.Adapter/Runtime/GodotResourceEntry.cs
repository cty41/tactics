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

    public string ResourceLocator => string.IsNullOrWhiteSpace(ResourceUidValue)
        ? DiagnosticPathValue
        : ResourceUidValue;
}
