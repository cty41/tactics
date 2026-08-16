using Godot;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class SkillPresentationResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public string SkillBranch { get; set; } = string.Empty;
    [Export] public string ProgrammaticKind { get; set; } = string.Empty;
    [Export] public Color PrimaryColor { get; set; } = Colors.White;
    [Export] public Color SecondaryColor { get; set; } = Colors.White;
    [Export] public float TravelDuration { get; set; } = 0.28f;
    [Export] public float ImpactDuration { get; set; } = 0.16f;
    [Export] public int MaximumGhosts { get; set; }
    [Export] public bool LevelOneHasAreaEffect { get; set; }
    [Export] public string PayloadBoundary { get; set; } = "programmatic-only-no-piloto-payload";
}
