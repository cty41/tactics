using Godot;
using Tactics.Application.Battle;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class PlayableEnemySpeedProfileResource : Resource
{
    [Export] public string ContractId { get; set; } = "godot-playable-enemy-speed-v1";
    [Export] public string[] UnitContentIds { get; set; } = Array.Empty<string>();
    [Export] public float[] Speeds { get; set; } = Array.Empty<float>();

    public PlayableEnemySpeedProfile ToCoreProfile()
    {
        if (ContractId != "godot-playable-enemy-speed-v1" || UnitContentIds.Length != Speeds.Length ||
            Speeds.Any(speed => !float.IsFinite(speed) || speed <= 0f))
            throw new InvalidOperationException("Playable enemy speed profile is invalid.");
        return new PlayableEnemySpeedProfile(Enumerable.Range(0, UnitContentIds.Length)
            .ToDictionary(index => new ContentId(UnitContentIds[index]), index => Speeds[index]));
    }
}
