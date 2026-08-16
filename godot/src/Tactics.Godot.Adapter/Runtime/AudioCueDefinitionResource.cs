using Godot;

namespace Tactics.Godot.Adapter.Runtime;

public enum TacticsAudioBus
{
    Music,
    Sfx,
    Ui
}

[GlobalClass]
public partial class AudioCueDefinitionResource : Resource
{
    [Export] public string ContractId { get; set; } = "audio-cue-v1";
    [Export] public string CueId { get; set; } = string.Empty;
    [Export] public TacticsAudioBus Bus { get; set; } = TacticsAudioBus.Sfx;
    [Export] public AudioStream[] Variants { get; set; } = Array.Empty<AudioStream>();
    [Export(PropertyHint.Range, "1,16,1")] public int MaxConcurrent { get; set; } = 4;
    [Export(PropertyHint.Range, "-40,12,0.5")] public float VolumeDb { get; set; }

    public void Validate()
    {
        if (ContractId != "audio-cue-v1") throw new InvalidOperationException("Audio cue contract is invalid.");
        if (string.IsNullOrWhiteSpace(CueId)) throw new InvalidOperationException("Audio cue ID is required.");
        if (MaxConcurrent is < 1 or > 16) throw new InvalidOperationException("Audio cue concurrency is outside 1..16.");
        if (Variants.Any(value => value is null)) throw new InvalidOperationException("Audio cue contains a missing stream variant.");
    }
}

[GlobalClass]
public partial class AudioCueCatalogResource : Resource
{
    [Export] public string ContractId { get; set; } = "audio-cue-catalog-v1";
    [Export] public string[] EventIds { get; set; } = Array.Empty<string>();
    [Export] public AudioCueDefinitionResource[] Cues { get; set; } = Array.Empty<AudioCueDefinitionResource>();

    public IReadOnlyDictionary<string, AudioCueDefinitionResource> Compile()
    {
        if (ContractId != "audio-cue-catalog-v1" || EventIds.Length != Cues.Length)
            throw new InvalidOperationException("Audio cue catalog contract is invalid.");
        var result = new Dictionary<string, AudioCueDefinitionResource>(StringComparer.Ordinal);
        for (int index = 0; index < EventIds.Length; index++)
        {
            AudioCueDefinitionResource cue = Cues[index]
                ?? throw new InvalidOperationException("Audio cue catalog contains a missing cue.");
            if (string.IsNullOrWhiteSpace(EventIds[index]) || !result.TryAdd(EventIds[index], cue))
                throw new InvalidOperationException("Audio cue event IDs must be non-empty and unique.");
            cue.Validate();
        }
        return result;
    }
}
