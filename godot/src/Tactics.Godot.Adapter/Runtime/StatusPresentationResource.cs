using Godot;

namespace Tactics.Godot.Adapter.Runtime;

[Tool]
[GlobalClass]
public partial class StatusPresentationResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = "presentation.status.standard-v1";
    [Export] public int MaximumVisibleStatuses { get; set; } = 4;
    [Export] public float PulseDuration { get; set; } = .22f;
    [Export] public string PayloadBoundary { get; set; } = "programmatic-only-no-third-party-payload";
    [Export(PropertyHint.MultilineText)] public string AuthoringGraphJsonValue { get; set; } = string.Empty;
}
